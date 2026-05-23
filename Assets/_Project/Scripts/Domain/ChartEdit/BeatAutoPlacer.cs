using System;
using System.Collections.Generic;

// Unity-independent. Part of Domain assembly.

/// <summary>
/// BeatDetector が検出したオンセット列から自動的にノーツ列を生成する。
/// レーン選択ストラテジ・スナップ・既存ノーツとの重複回避ポリシーを持つ。
/// 生成されたノーツは PasteNotesCommand に渡してUndo可能に挿入する想定。
/// </summary>
public static class BeatAutoPlacer
{
    /// <summary>レーン選択ストラテジ。</summary>
    public enum LaneStrategy
    {
        /// <summary>Lane0→Lane1→Lane2→Lane3→Lane0… 循環。</summary>
        RoundRobin,
        /// <summary>Lane0↔Lane3 交互 (外側だけ)。</summary>
        AltOuter,
        /// <summary>Lane1↔Lane2 交互 (内側だけ)。</summary>
        AltInner,
        /// <summary>ランダム (直前と同じレーンを避ける)。</summary>
        RandomNoRepeat,
    }

    /// <summary>自動配置のオプション。</summary>
    public sealed class Options
    {
        /// <summary>配置対象の開始時刻 (ms)。これより前のオンセットは無視。</summary>
        public double StartMs = 0;
        /// <summary>配置対象の終了時刻 (ms)。これより後のオンセットは無視。</summary>
        public double EndMs = double.MaxValue;
        /// <summary>生成するノーツ種別。</summary>
        public NoteType Type = NoteType.Tap;
        /// <summary>レーン選択ストラテジ。</summary>
        public LaneStrategy Strategy = LaneStrategy.RoundRobin;
        /// <summary>EditorState.SnapTime() で量子化するか。</summary>
        public bool Snap = true;
        /// <summary>既存ノーツと近接 (同レーン &amp; |Δt| ≤ DedupeToleranceMs) する場合スキップ。</summary>
        public bool SkipNearExisting = true;
        /// <summary>重複判定の許容時間 (ms)。</summary>
        public double DedupeToleranceMs = 30.0;
        /// <summary>2つのオンセットが近すぎる場合 (≤ MinIntervalMs) 後者をスキップ。0 で無効。</summary>
        public double MinIntervalMs = 0.0;
        /// <summary>RandomNoRepeat 用の seed。</summary>
        public int RandomSeed = 0;
    }

    /// <summary>
    /// オンセット列から生成されたノーツ列を返す。state.IssueNoteId() を消費するので注意。
    /// </summary>
    public static List<NoteData> Generate(EditorState state, IList<double> onsetTimesMs, Options opt)
    {
        var result = new List<NoteData>();
        if (state == null || onsetTimesMs == null || onsetTimesMs.Count == 0) return result;
        opt ??= new Options();

        var lanes = SelectLanePool(opt.Strategy);
        var rng = new Random(opt.RandomSeed);
        int idx = 0;
        LaneRef lastLane = LaneRef.Lane0;
        bool hasLast = false;

        var existing = state.Chart?.Notes;
        // batch-internal dedupe set: (laneIdx, snapped-ms-rounded)
        var seen = new HashSet<long>();
        double lastPlacedMs = double.NegativeInfinity;

        for (int i = 0; i < onsetTimesMs.Count; i++)
        {
            double raw = onsetTimesMs[i];
            if (raw < opt.StartMs || raw > opt.EndMs) continue;

            double t = opt.Snap ? state.SnapTime(raw) : raw;
            if (t < 0) continue;
            if (state.AudioDurationMs > 0 && t > state.AudioDurationMs) continue;

            if (opt.MinIntervalMs > 0 && hasLast && (t - lastPlacedMs) < opt.MinIntervalMs)
                continue;

            LaneRef lane = PickLane(lanes, opt.Strategy, idx, lastLane, hasLast, rng);

            if (opt.SkipNearExisting && existing != null)
            {
                bool collision = false;
                for (int j = 0; j < existing.Count; j++)
                {
                    var n = existing[j];
                    if (n.Lane != lane) continue;
                    if (Math.Abs(n.TimeMs - t) <= opt.DedupeToleranceMs) { collision = true; break; }
                }
                if (collision) { idx++; continue; }
            }

            long key = ((long)Math.Round(t)) * 8L + (long)TimelineLayout.LaneIndex(lane);
            if (!seen.Add(key)) { idx++; continue; }

            result.Add(new NoteData
            {
                Id         = state.IssueNoteId(),
                Type       = opt.Type,
                Lane       = lane,
                TimeMs     = t,
                DurationMs = 0,
            });
            lastLane = lane;
            hasLast  = true;
            lastPlacedMs = t;
            idx++;
        }

        return result;
    }

    static LaneRef[] SelectLanePool(LaneStrategy s)
    {
        switch (s)
        {
            case LaneStrategy.AltOuter: return new[] { LaneRef.Lane0, LaneRef.Lane3 };
            case LaneStrategy.AltInner: return new[] { LaneRef.Lane1, LaneRef.Lane2 };
            default: return new[] { LaneRef.Lane0, LaneRef.Lane1, LaneRef.Lane2, LaneRef.Lane3 };
        }
    }

    static LaneRef PickLane(LaneRef[] pool, LaneStrategy s, int idx, LaneRef last, bool hasLast, Random rng)
    {
        if (s == LaneStrategy.RandomNoRepeat)
        {
            if (pool.Length <= 1) return pool[0];
            LaneRef chosen;
            int safety = 0;
            do { chosen = pool[rng.Next(pool.Length)]; }
            while (hasLast && chosen == last && ++safety < 8);
            return chosen;
        }
        // RoundRobin / AltOuter / AltInner: cycle through the pool
        return pool[idx % pool.Length];
    }
}
