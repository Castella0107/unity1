using System;
using System.Collections.Generic;

// Unity-independent. Part of Domain assembly.

/// <summary>
/// OnsetAnalyzer の解析結果とテンポマップ (EditorState.BuildBpmSegments) から、難易度別の
/// 「下書き譜面」を自動生成する。人が ChartEditor で仕上げる前提のドラフト品質を狙う。
///
/// パイプライン:
///   1. テンポマップに沿って許容音価の格子スロットを列挙 (拍頭ほど高い拍重み)
///   2. 統合オンセットを最寄りスロットへ割当ててスコアリング (強度 × 拍重み)
///   3. 持続音 → ホールドを先に配置 (レーン占有区間を確保)
///   4. 小節ごとの密度目標 (基準NPS × エネルギープロファイル) に合わせ上位スロットを選抜し Tap 配置
///   5. 低域オンセット → FX レーン (難易度に応じた密度上限)
///
/// レーン割当は「同レーン連打(ジャック)禁止間隔」「左右手の交互」「ホールド占有の回避」を制約に
/// シード付き乱数で決定的に行う。同じ入力+同じシードなら常に同じ譜面を返す。
/// </summary>
public static class AutoChartGenerator
{
    /// <summary>難易度ごとの生成パラメータ。</summary>
    public sealed class DifficultyPreset
    {
        /// <summary>許容する最小音価 (1/N 全音符基準。8=八分, 16=十六分)。</summary>
        public int    Subdivision;
        /// <summary>エネルギー最大区間での目標ノーツ密度 (notes/sec)。</summary>
        public double BaseNps;
        /// <summary>同レーンに次のノーツを置ける最小間隔 (ms)。ジャック抑制。</summary>
        public double JackMinMs;
        /// <summary>同時押し(2レーン)を置く確率 [0,1]。強アクセントのスロットのみ対象。</summary>
        public double ChordRate;
        /// <summary>ホールドを使うか。</summary>
        public bool   UseHolds;
        /// <summary>同時に鳴ってよいホールド本数の上限。</summary>
        public int    MaxConcurrentHolds;
        /// <summary>ホールドとして採用する持続音の最小長 (ms)。</summary>
        public double HoldMinMs;
        /// <summary>FX レーンを使うか。</summary>
        public bool   UseFx;
        /// <summary>FX ノーツの最小間隔 (拍に対する倍率。1.0=1拍ごとまで)。</summary>
        public double FxMinGapBeats;
    }

    /// <summary>生成オプション。</summary>
    public sealed class Options
    {
        /// <summary>難易度 ("easy" | "normal" | "hard" | "extra")。プリセット選択に使う。</summary>
        public string Difficulty = "normal";
        /// <summary>密度スケール。1.0 = プリセット既定。0.5〜1.5 程度を想定。</summary>
        public double DensityScale = 1.0;
        /// <summary>レーン割当・同時押し抽選のシード。同一入力+同一シード → 同一出力。</summary>
        public int    Seed = 0;
        /// <summary>生成対象の開始時刻 (ms)。</summary>
        public double StartMs = 0;
        /// <summary>生成対象の終了時刻 (ms)。</summary>
        public double EndMs = double.MaxValue;
        /// <summary>プリセットの明示上書き (null ならDifficultyから選択)。</summary>
        public DifficultyPreset Preset;
    }

    /// <summary>難易度名からプリセットを返す。未知の名前は normal 扱い。</summary>
    public static DifficultyPreset PresetFor(string difficulty)
    {
        switch ((difficulty ?? "").ToLowerInvariant())
        {
            case "easy":
                return new DifficultyPreset { Subdivision = 8,  BaseNps = 1.3, JackMinMs = 450,
                    ChordRate = 0.0,  UseHolds = true, MaxConcurrentHolds = 1, HoldMinMs = 500,
                    UseFx = false, FxMinGapBeats = 2.0 };
            case "hard":
                return new DifficultyPreset { Subdivision = 16, BaseNps = 3.6, JackMinMs = 200,
                    ChordRate = 0.14, UseHolds = true, MaxConcurrentHolds = 2, HoldMinMs = 400,
                    UseFx = true,  FxMinGapBeats = 0.5 };
            case "extra":
                return new DifficultyPreset { Subdivision = 16, BaseNps = 5.2, JackMinMs = 130,
                    ChordRate = 0.24, UseHolds = true, MaxConcurrentHolds = 2, HoldMinMs = 400,
                    UseFx = true,  FxMinGapBeats = 0.25 };
            default: // normal
                return new DifficultyPreset { Subdivision = 8,  BaseNps = 2.3, JackMinMs = 320,
                    ChordRate = 0.05, UseHolds = true, MaxConcurrentHolds = 1, HoldMinMs = 450,
                    UseFx = true,  FxMinGapBeats = 1.0 };
        }
    }

    // ── 内部表現 ─────────────────────────────────────────────────────────────

    /// <summary>格子スロット (候補位置)。</summary>
    sealed class Slot
    {
        public double TimeMs;
        public double MetricWeight;   // 拍重み: 小節頭1.0 / 拍頭0.8 / 半拍0.55 / それ未満0.35
        public double Strength;       // 割当てられたオンセットの最大強度 (未割当は0)
        public bool   HasOnset;
        public bool   HasLowBand;     // 低域オンセットが重なっているか
        public int    MeasureIndex;   // 密度制御の単位
        public double Score => HasOnset ? Strength * (0.55 + 0.45 * MetricWeight) : 0.0;
    }

    /// <summary>レーン占有区間 (ホールド・既存ノーツ由来)。</summary>
    sealed class LaneBusy
    {
        public double  StartMs;
        public double  EndMs;
    }

    /// <summary>
    /// レーン占有区間をレーン別バケットで保持する索引。IsBusy / ジャック間隔スキャンは元々
    /// 全走査後にレーンでフィルタしていたため、レーン別に分けても走査集合・判定結果は完全同一
    /// (よって PickLane の乱数消費順=生成結果も不変)。全区間を毎回舐める O(N²) を該当レーン分だけに縮小する。
    /// LaneRef は 0..5 の 6 値なので配列添字で引く。
    /// </summary>
    sealed class BusyMap
    {
        readonly List<LaneBusy>[] _byLane = new List<LaneBusy>[6];

        public void Add(LaneRef lane, double startMs, double endMs)
            => (_byLane[(int)lane] ??= new List<LaneBusy>())
               .Add(new LaneBusy { StartMs = startMs, EndMs = endMs });

        /// <summary>[startMs, endMs] に重なる占有区間が lane にあるか。</summary>
        public bool IsBusy(LaneRef lane, double startMs, double endMs)
        {
            var list = _byLane[(int)lane];
            if (list == null) return false;
            for (int i = 0; i < list.Count; i++)
                if (list[i].StartMs <= endMs && list[i].EndMs >= startMs) return true;
            return false;
        }

        /// <summary>同レーンの直近使用終端から startMs までの最小正ギャップ (ジャック回避用)。無ければ MaxValue。</summary>
        public double NearestPrevGap(LaneRef lane, double startMs)
        {
            var list = _byLane[(int)lane];
            double best = double.MaxValue;
            if (list == null) return best;
            for (int j = list.Count - 1; j >= 0; j--)
            {
                double d = startMs - list[j].EndMs;
                if (d >= 0 && d < best) best = d;
            }
            return best;
        }
    }

    const double TapBusyPadMs = 90.0;   // 既存/生成済み Tap の前後をこのパディングで占有扱い
    const double ChordStrengthMin = 0.72; // 同時押し対象になる最小強度

    static readonly LaneRef[] MainLanes = { LaneRef.Lane0, LaneRef.Lane1, LaneRef.Lane2, LaneRef.Lane3 };

    /// <summary>
    /// 譜面を自動生成して返す (state.IssueNoteId() を消費)。返り値は PasteNotesCommand に渡して
    /// Undo 可能に挿入する想定。既存ノーツとの衝突 (同時刻同レーン・ホールド占有) は避ける。
    /// </summary>
    public static List<NoteData> Generate(EditorState state, OnsetAnalyzer.Result analysis, Options opt)
    {
        var result = new List<NoteData>();
        if (state == null || analysis == null) return result;
        opt ??= new Options();
        var preset = opt.Preset ?? PresetFor(opt.Difficulty);
        var rng = new Random(opt.Seed);

        // SnapTime はエディタUIの SnapDenominator に従うため、生成中だけプリセットの音価に合わせる
        int savedSnap = state.SnapDenominator;
        state.SnapDenominator = preset.Subdivision;
        try
        {

            double endMs = Math.Min(opt.EndMs, state.AudioDurationMs > 0 ? state.AudioDurationMs : opt.EndMs);
            if (endMs <= opt.StartMs) return result;

            // 1. 格子スロット列挙
            var slots = BuildSlots(state, preset.Subdivision, opt.StartMs, endMs, out var measureStarts);
            if (slots.Count == 0) return result;

            // 2. オンセット割当て
            AssignOnsets(slots, analysis);

            // 既存ノーツの占有区間 (生成が既存譜面を壊さないように)
            var busy = new BusyMap();
            var existing = state.Chart?.Notes;
            if (existing != null)
            {
                for (int i = 0; i < existing.Count; i++)
                {
                    var n = existing[i];
                    double e = n.TimeMs + Math.Max(n.DurationMs, 0);
                    busy.Add(n.Lane, n.TimeMs - TapBusyPadMs, e + TapBusyPadMs);
                }
            }

            // 3. ホールド配置 (先にレーンを確保)
            if (preset.UseHolds)
                PlaceHolds(state, analysis, preset, opt, rng, busy, endMs, result);

            // 4. Tap 配置 (小節ごとの密度目標に合わせて選抜)
            PlaceTaps(state, analysis, preset, opt, rng, slots, measureStarts, busy, result);

            // 5. FX 配置 (低域オンセット)
            if (preset.UseFx)
                PlaceFx(state, analysis, preset, opt, busy, endMs, result);

            result.Sort((a, b) => a.TimeMs.CompareTo(b.TimeMs));
            return result;
        }
        finally
        {
            state.SnapDenominator = savedSnap;
        }
    }

    // ── 1. 格子スロット列挙 ──────────────────────────────────────────────────

    static List<Slot> BuildSlots(EditorState state, int subdivision, double startMs, double endMs,
                                 out List<double> measureStarts)
    {
        var slots = new List<Slot>();
        measureStarts = new List<double>();
        var segs = state.BuildBpmSegments();
        int measureIndex = -1;

        for (int si = 0; si < segs.Count; si++)
        {
            var seg = segs[si];
            if (seg.Bpm <= 0) continue;
            double segEnd = (si + 1 < segs.Count) ? segs[si + 1].StartMs : endMs;
            if (segEnd > endMs) segEnd = endMs;
            if (segEnd <= seg.StartMs) continue;

            double quarter   = 60000.0 / seg.Bpm;
            int    den       = seg.Den > 0 ? seg.Den : 4;
            int    num       = seg.Num > 0 ? seg.Num : 4;
            double denBeat   = quarter * 4.0 / den;
            double measureMs = denBeat * num;
            double step      = 4.0 * quarter / subdivision;
            if (step <= 0 || denBeat <= 0) continue;

            // セグメント起点から step 刻み (k*step で計算し浮動小数の累積ドリフトを防ぐ)。
            double eps = Math.Min(1.0, step * 0.05);
            for (long k = 0; ; k++)
            {
                double t = seg.StartMs + k * step;
                if (t >= segEnd - eps) break;
                if (t < startMs - eps) continue;
                double rel  = t - seg.StartMs;
                double inMeasure = rel % measureMs;
                if (inMeasure > measureMs - eps) inMeasure = 0;
                double inBeat = rel % denBeat;
                if (inBeat > denBeat - eps) inBeat = 0;

                double w;
                if      (inMeasure <= eps)               w = 1.0;
                else if (inBeat    <= eps)               w = 0.8;
                else if (Math.Abs(inBeat - denBeat * 0.5) <= eps) w = 0.55;
                else                                     w = 0.35;

                if (inMeasure <= eps)
                {
                    measureIndex++;
                    measureStarts.Add(t);
                }
                if (measureIndex < 0) { measureIndex = 0; measureStarts.Add(seg.StartMs); }

                slots.Add(new Slot { TimeMs = t, MetricWeight = w, MeasureIndex = measureIndex });
            }
        }
        return slots;
    }

    // ── 2. オンセット割当て ──────────────────────────────────────────────────

    static void AssignOnsets(List<Slot> slots, OnsetAnalyzer.Result analysis)
    {
        MapOntoSlots(slots, analysis.MergedOnsets, (slot, o) =>
        {
            slot.HasOnset = true;
            if (o.Strength > slot.Strength) slot.Strength = o.Strength;
        });
        MapOntoSlots(slots, analysis.BandOnsets[(int)OnsetAnalyzer.Band.Low],
                     (slot, o) => slot.HasLowBand = true);
    }

    /// <summary>各オンセットを最寄りスロットへ写像 (許容差 = 隣接スロット間隔の45% or 45ms の小さい方)。</summary>
    static void MapOntoSlots(List<Slot> slots, List<OnsetAnalyzer.Onset> onsets,
                             Action<Slot, OnsetAnalyzer.Onset> apply)
    {
        if (onsets == null || onsets.Count == 0 || slots.Count == 0) return;
        int si = 0;
        for (int oi = 0; oi < onsets.Count; oi++)
        {
            var o = onsets[oi];
            while (si + 1 < slots.Count && slots[si + 1].TimeMs <= o.TimeMs) si++;
            int best = si;
            if (si + 1 < slots.Count &&
                Math.Abs(slots[si + 1].TimeMs - o.TimeMs) < Math.Abs(slots[si].TimeMs - o.TimeMs))
                best = si + 1;

            double spacing = (best + 1 < slots.Count ? slots[best + 1].TimeMs - slots[best].TimeMs
                             : (best > 0 ? slots[best].TimeMs - slots[best - 1].TimeMs : 45.0));
            double tol = Math.Min(45.0, spacing * 0.45);
            if (Math.Abs(slots[best].TimeMs - o.TimeMs) <= tol)
                apply(slots[best], o);
        }
    }

    // ── 3. ホールド配置 ──────────────────────────────────────────────────────

    static void PlaceHolds(EditorState state, OnsetAnalyzer.Result analysis, DifficultyPreset preset,
                           Options opt, Random rng, BusyMap busy, double endMs, List<NoteData> result)
    {
        double lastHoldEnd = double.NegativeInfinity;
        for (int i = 0; i < analysis.Sustains.Count; i++)
        {
            var s = analysis.Sustains[i];
            if (s.StartMs < opt.StartMs || s.StartMs > endMs) continue;
            if (s.DurationMs < preset.HoldMinMs) continue;

            double start = state.SnapTime(s.StartMs);
            var seg = state.SegmentAt(start);
            double denBeat = 60000.0 / Math.Max(1.0, seg.Bpm) * 4.0 / Math.Max(1, seg.Den);
            // 長すぎる持続音 (パッド等) は2小節相当で打ち切る
            double capEnd = start + denBeat * Math.Max(1, seg.Num > 0 ? seg.Num : 4) * 2;
            double end    = state.SnapTime(Math.Min(Math.Min(s.EndMs, endMs), capEnd));
            if (end - start < denBeat * 0.9) continue;              // 短すぎるホールドは捨てる
            if (start < lastHoldEnd + denBeat) continue;            // ホールド連発の抑制

            // 同時ホールド本数の上限
            int concurrent = 0;
            for (int j = 0; j < result.Count; j++)
            {
                var n = result[j];
                if ((n.Type == NoteType.Hold) && n.TimeMs < end && n.TimeMs + n.DurationMs > start)
                    concurrent++;
            }
            if (concurrent >= preset.MaxConcurrentHolds) continue;

            var lane = PickLane(MainLanes, start, end, busy, preset.JackMinMs, rng, preferAlternate: null);
            if (lane == null) continue;

            result.Add(new NoteData
            {
                Id = state.IssueNoteId(), Type = NoteType.Hold, Lane = lane.Value,
                TimeMs = start, DurationMs = end - start,
            });
            busy.Add(lane.Value, start - TapBusyPadMs, end + TapBusyPadMs);
            lastHoldEnd = end;
        }
    }

    // ── 4. Tap 配置 ──────────────────────────────────────────────────────────

    static void PlaceTaps(EditorState state, OnsetAnalyzer.Result analysis, DifficultyPreset preset,
                          Options opt, Random rng, List<Slot> slots, List<double> measureStarts,
                          BusyMap busy, List<NoteData> result)
    {
        // 小節ごとにスロットをグループ化
        int measures = measureStarts.Count;
        var byMeasure = new List<Slot>[measures];
        for (int i = 0; i < slots.Count; i++)
        {
            int m = slots[i].MeasureIndex;
            if (m < 0 || m >= measures) continue;
            (byMeasure[m] ??= new List<Slot>()).Add(slots[i]);
        }

        LaneRef? lastLane = null;
        double quotaCarry = 0;   // 小数クォータの持ち越し (easy の 2.4 notes/measure 等を実現)

        for (int m = 0; m < measures; m++)
        {
            var list = byMeasure[m];
            if (list == null) continue;
            double mStart = measureStarts[m];
            double mEnd   = (m + 1 < measures) ? measureStarts[m + 1] : (list[list.Count - 1].TimeMs + 1);
            double lenSec = Math.Max(0.001, (mEnd - mStart) / 1000.0);

            // この小節の密度目標: 基準NPS × スケール × エネルギー
            double energy = analysis.EnergyAt((mStart + mEnd) * 0.5);
            double target = preset.BaseNps * opt.DensityScale * (0.35 + 0.65 * energy) * lenSec + quotaCarry;
            int    quota  = (int)Math.Floor(target);
            quotaCarry    = target - quota;
            if (quota <= 0) continue;

            // スコア上位から選抜 (同スコアは時刻昇順で安定)
            var ranked = new List<Slot>(list);
            ranked.Sort((a, b) =>
            {
                int c = b.Score.CompareTo(a.Score);
                return c != 0 ? c : a.TimeMs.CompareTo(b.TimeMs);
            });

            var chosen = new List<Slot>();
            for (int i = 0; i < ranked.Count && chosen.Count < quota; i++)
            {
                var s = ranked[i];
                if (s.Score <= 0) break;   // オンセットの無いスロットは埋めない (静寂は静寂のまま)
                chosen.Add(s);
            }
            chosen.Sort((a, b) => a.TimeMs.CompareTo(b.TimeMs));

            for (int i = 0; i < chosen.Count; i++)
            {
                var s = chosen[i];
                var lane = PickLane(MainLanes, s.TimeMs, s.TimeMs, busy, preset.JackMinMs, rng, lastLane);
                if (lane == null) continue;

                result.Add(new NoteData
                {
                    Id = state.IssueNoteId(), Type = NoteType.Tap, Lane = lane.Value,
                    TimeMs = s.TimeMs, DurationMs = 0,
                });
                busy.Add(lane.Value, s.TimeMs - TapBusyPadMs, s.TimeMs + TapBusyPadMs);

                // 同時押し: 強アクセント + 拍頭のみ + 抽選
                if (preset.ChordRate > 0 && s.Strength >= ChordStrengthMin && s.MetricWeight >= 0.8
                    && rng.NextDouble() < preset.ChordRate)
                {
                    var lane2 = PickLane(MainLanes, s.TimeMs, s.TimeMs, busy, preset.JackMinMs, rng, lane);
                    if (lane2 != null)
                    {
                        result.Add(new NoteData
                        {
                            Id = state.IssueNoteId(), Type = NoteType.Tap, Lane = lane2.Value,
                            TimeMs = s.TimeMs, DurationMs = 0,
                        });
                        busy.Add(lane2.Value, s.TimeMs - TapBusyPadMs, s.TimeMs + TapBusyPadMs);
                    }
                }
                lastLane = lane;
            }
        }
    }

    // ── 5. FX 配置 ──────────────────────────────────────────────────────────

    static void PlaceFx(EditorState state, OnsetAnalyzer.Result analysis, DifficultyPreset preset,
                        Options opt, BusyMap busy, double endMs, List<NoteData> result)
    {
        var lows = analysis.BandOnsets[(int)OnsetAnalyzer.Band.Low];
        bool left = true;
        double lastFxMs = double.NegativeInfinity;
        for (int i = 0; i < lows.Count; i++)
        {
            var o = lows[i];
            if (o.TimeMs < opt.StartMs || o.TimeMs > endMs) continue;
            if (o.Strength < 0.35) continue;

            double t = state.SnapTime(o.TimeMs);
            var seg = state.SegmentAt(t);
            double denBeat = 60000.0 / Math.Max(1.0, seg.Bpm) * 4.0 / Math.Max(1, seg.Den);
            if (t - lastFxMs < denBeat * preset.FxMinGapBeats) continue;

            var lane = left ? LaneRef.FxL : LaneRef.FxR;
            if (busy.IsBusy(lane, t, t)) { lane = left ? LaneRef.FxR : LaneRef.FxL; if (busy.IsBusy(lane, t, t)) continue; }

            result.Add(new NoteData
            {
                Id = state.IssueNoteId(), Type = NoteType.FxTap, Lane = lane,
                TimeMs = t, DurationMs = 0,
            });
            busy.Add(lane, t - TapBusyPadMs, t + TapBusyPadMs);
            left = !left;
            lastFxMs = t;
        }
    }

    // ── レーン割当て ─────────────────────────────────────────────────────────

    /// <summary>
    /// [startMs, endMs] を占有可能なレーンを選ぶ。優先度: 占有と非衝突 → ジャック回避 →
    /// 直前ノーツと逆の手 (Lane0/1=左手, Lane2/3=右手) を軽く優遇。同点はシード乱数で決定的に。
    /// 全滅なら null。
    /// </summary>
    static LaneRef? PickLane(LaneRef[] pool, double startMs, double endMs, BusyMap busy,
                             double jackMinMs, Random rng, LaneRef? preferAlternate)
    {
        LaneRef? best = null;
        double bestScore = double.NegativeInfinity;
        for (int i = 0; i < pool.Length; i++)
        {
            var lane = pool[i];
            if (busy.IsBusy(lane, startMs, endMs)) continue;

            // 同レーン直近使用からの経過 (ジャック回避)。
            double sinceLast = busy.NearestPrevGap(lane, startMs);
            if (sinceLast < jackMinMs - TapBusyPadMs) continue;

            double score = Math.Min(sinceLast, 4000.0) / 4000.0;
            if (preferAlternate != null)
            {
                bool lastIsLeft = preferAlternate == LaneRef.Lane0 || preferAlternate == LaneRef.Lane1;
                bool laneIsLeft = lane == LaneRef.Lane0 || lane == LaneRef.Lane1;
                if (lastIsLeft != laneIsLeft) score += 0.35;   // 手の交互を優遇
                if (lane == preferAlternate)  score -= 0.5;    // 完全同一レーンはさらに抑制
            }
            score += rng.NextDouble() * 0.25;                  // 決定的な揺らぎ
            if (score > bestScore) { bestScore = score; best = lane; }
        }
        return best;
    }
}
