using System;
using System.Collections.Generic;

/// <summary>
/// 譜面エディタの編集中状態を保持するクラス。
/// 楽曲メタ・現在の譜面 (ChartData) ・編集UI状態 (snap, 選択中ノーツ, BPM等) をまとめて管理する。
/// EditCommand 経由でのみ Notes を変更すること (Undo/Redo に乗せるため)。
/// </summary>
public sealed class EditorState
{
    // ── Loaded song / chart ────────────────────────────────────────────────
    /// <summary>ロード中の楽曲メタデータ。</summary>
    public SongMetadata Metadata     { get; set; }
    /// <summary>編集中の譜面データ。Notes は EditCommand 経由でのみ変更すること。</summary>
    public ChartData    Chart        { get; set; }
    /// <summary>楽曲フォルダの絶対パス。</summary>
    public string       SongBasePath { get; set; }
    /// <summary>楽曲ID。</summary>
    public string       SongId       { get; set; }
    /// <summary>編集中の難易度。</summary>
    public string       Difficulty   { get; set; }
    /// <summary>音源の長さ(ms)。</summary>
    public double       AudioDurationMs { get; set; }

    /// <summary>現在の再生/編集ヘッド時刻(ms)。</summary>
    public double       CurrentTimeMs   { get; set; }
    /// <summary>スナップ分母(1/4 拍なら 4)。</summary>
    public int          SnapDenominator { get; set; } = 4;
    /// <summary>基準 BPM。</summary>
    public double       Bpm             { get; set; } = 120.0;
    /// <summary>BPM 格子の基準時刻(1拍目、ms)。</summary>
    public double       ChartOffsetMs   { get; set; } = 0.0;
    /// <summary>配置するノーツ種別(パレット)。</summary>
    public NoteType     PaletteType     { get; set; } = NoteType.Tap;
    /// <summary>未保存の変更があるか。</summary>
    public bool         IsDirty         { get; set; }

    /// <summary>選択中ノーツのID集合。</summary>
    public readonly HashSet<int> SelectedNoteIds = new HashSet<int>();

    /// <summary>ループ開始時刻(ms、-1=未設定)。</summary>
    public double LoopStartMs { get; set; } = -1.0;
    /// <summary>ループ終了時刻(ms、-1=未設定)。</summary>
    public double LoopEndMs   { get; set; } = -1.0;
    /// <summary>ループ再生が有効か。</summary>
    public bool   LoopEnabled { get; set; }
    /// <summary>有効なループ範囲が設定されているか。</summary>
    public bool   HasLoopRange => LoopStartMs >= 0 && LoopEndMs > LoopStartMs;

    // ── ID issuance ─────────────────────────────────────────────────────────
    int _nextNoteId;

    /// <summary>新しいノーツIDを採番して返す。</summary>
    public int IssueNoteId()
    {
        int id = _nextNoteId++;
        return id;
    }

    /// <summary>現在の譜面の最大ノーツIDを走査し、採番カウンタを再構築する。</summary>
    public void RebuildNoteIdCounter()
    {
        int max = -1;
        if (Chart != null && Chart.Notes != null)
            foreach (var n in Chart.Notes)
                if (n.Id > max) max = n.Id;
        _nextNoteId = max + 1;
    }

    // ── Snap helpers (tempo-map aware) ───────────────────────────────────────

    /// <summary>
    /// テンポマップの1セグメント。StartMs を起点に Bpm × 拍子 (Num/Den) の拍格子が貼られる。
    /// bpm / timesig どちらの変化点でも新しい区間が始まる (= 格子の貼り直し起点)。
    /// </summary>
    public readonly struct BpmSegment
    {
        /// <summary>区間開始時刻(ms)。拍/小節格子の起点。</summary>
        public readonly double StartMs;
        /// <summary>この区間の BPM。</summary>
        public readonly double Bpm;
        /// <summary>この区間の拍子の分子(1小節の拍数)。</summary>
        public readonly int Num;
        /// <summary>この区間の拍子の分母(1拍の音価。4=四分/8=八分)。</summary>
        public readonly int Den;
        public BpmSegment(double startMs, double bpm, int num, int den)
        { StartMs = startMs; Bpm = bpm; Num = num; Den = den; }
    }

    /// <summary>
    /// Chart.Events の "bpm" / "timesig" イベントから、時刻昇順のテンポ区間列を構築する。
    /// 先頭区間は ChartOffsetMs (Beat1) を起点に基準 BPM・基準拍子。ChartOffsetMs より後の
    /// bpm / timesig イベントごとに新しい区間が始まり、その時刻が拍/小節の格子起点になる
    /// (= 変化点で格子を貼り直す。TimelineRenderer の小節線アンカーと同一規則)。
    /// </summary>
    public List<BpmSegment> BuildBpmSegments()
    {
        var segs = new List<BpmSegment>();

        // 基準値: ChartOffsetMs 以前にある最後の bpm / timesig イベント。無ければ this.Bpm と 4/4。
        double baseBpm = Bpm > 0.0 ? Bpm : 120.0;
        int baseNum = 4, baseDen = 4;
        List<TempoEvent> tempoEvents = null;
        if (Chart != null && Chart.Events != null)
        {
            tempoEvents = new List<TempoEvent>();
            for (int i = 0; i < Chart.Events.Count; i++)
            {
                var e = Chart.Events[i];
                if (e == null) continue;
                if (e.Type == "bpm" && e.Bpm > 0.0) tempoEvents.Add(e);
                else if (e.Type == "timesig" && e.Numerator > 0 && e.Denominator > 0) tempoEvents.Add(e);
            }
            tempoEvents.Sort((a, b) => a.TimeMs.CompareTo(b.TimeMs));
            for (int i = 0; i < tempoEvents.Count; i++)
            {
                var e = tempoEvents[i];
                if (e.TimeMs > ChartOffsetMs + 0.001) break;
                if (e.Type == "bpm") baseBpm = e.Bpm;
                else { baseNum = e.Numerator; baseDen = e.Denominator; }
            }
        }

        segs.Add(new BpmSegment(ChartOffsetMs, baseBpm, baseNum, baseDen));
        if (tempoEvents != null)
        {
            double curBpm = baseBpm;
            int curNum = baseNum, curDen = baseDen;
            for (int i = 0; i < tempoEvents.Count; i++)
            {
                var e = tempoEvents[i];
                if (e.TimeMs <= ChartOffsetMs + 0.001) continue;   // 基準として消化済み
                if (e.Type == "bpm") curBpm = e.Bpm;
                else { curNum = e.Numerator; curDen = e.Denominator; }
                var seg = new BpmSegment(e.TimeMs, curBpm, curNum, curDen);
                var last = segs[segs.Count - 1];
                if (Math.Abs(e.TimeMs - last.StartMs) < 0.001)
                    segs[segs.Count - 1] = seg;   // 同時刻重複は最後を採用
                else
                    segs.Add(seg);
            }
        }
        return segs;
    }

    /// <summary>指定時刻が属する BPM 区間を返す。</summary>
    public BpmSegment SegmentAt(double timeMs)
    {
        var segs = BuildBpmSegments();
        var cur = segs[0];
        for (int i = 0; i < segs.Count; i++)
        {
            if (timeMs >= segs[i].StartMs - 0.001) cur = segs[i];
            else break;
        }
        return cur;
    }

    /// <summary>指定時刻における実効 BPM を返す。</summary>
    public double LocalBpmAt(double timeMs) => SegmentAt(timeMs).Bpm;

    /// <summary>
    /// 指定時刻におけるスナップ単位(ms)。その時刻の区間 BPM を用いる。
    /// 記譜規約: 1/N = 全音符の 1/N (= 4/4 における 4 拍を SnapDenominator で分割)。
    /// 例: SnapDenominator=4 → 四分音符 = 1拍、=16 → 十六分音符 = 1/4拍、=12 → 三連 = 1/3拍。
    /// </summary>
    public double SnapStepMsAt(double timeMs)
    {
        double bpm = LocalBpmAt(timeMs);
        if (bpm <= 0.0) return 0.0;
        double beatMs = 60000.0 / bpm;
        return (4.0 * beatMs) / SnapDenominator;
    }

    /// <summary>現在ヘッド時刻におけるスナップ単位(ms)。</summary>
    public double SnapStepMs() => SnapStepMsAt(CurrentTimeMs);

    /// <summary>
    /// 任意の時刻を、その区間の格子に沿って最寄りのスナップ位置へ丸める。
    /// 格子は TimelineRenderer の描画と同一規則:
    /// 区間起点(bpm/timesig 変化点)から「分母音価の拍」を刻み、スナップ細分は各拍頭でリセット。
    /// スナップ単位が1拍より粗い場合は拍頭(=描画される拍線)に吸着する。
    /// </summary>
    public double SnapTime(double timeMs)
    {
        var segs = BuildBpmSegments();
        int idx = 0;
        for (int i = 0; i < segs.Count; i++)
        {
            if (timeMs >= segs[i].StartMs - 0.001) idx = i;
            else break;
        }
        var seg = segs[idx];
        if (seg.Bpm <= 0.0 || SnapDenominator <= 0) return timeMs;

        double quarterMs = 60000.0 / seg.Bpm;
        double step      = (4.0 * quarterMs) / SnapDenominator;
        int    den       = seg.Den > 0 ? seg.Den : 4;
        double denBeatMs = quarterMs * 4.0 / den;       // 1拍(分母音価)の長さ
        if (step <= 0.0 || denBeatMs <= 0.0) return timeMs;

        double rel = timeMs - seg.StartMs;
        if (rel < 0.0) rel = 0.0;
        double beatStart = Math.Floor(rel / denBeatMs) * denBeatMs;
        double relB      = rel - beatStart;
        double downAbs   = seg.StartMs + beatStart + Math.Floor(relB / step) * step;
        double upAbs     = seg.StartMs + beatStart
                         + Math.Min(Math.Floor(relB / step) * step + step, denBeatMs);   // 拍内に収まらない分は次の拍頭へ
        // 次のアンカー(bpm/timesig 変化点)では小節が切られ必ず線が引かれる → up 候補をそこへクランプ。
        if (idx + 1 < segs.Count && upAbs > segs[idx + 1].StartMs)
            upAbs = segs[idx + 1].StartMs;
        double snapped = (timeMs - downAbs <= upAbs - timeMs) ? downAbs : upAbs;

        if (snapped < 0.0) snapped = 0.0;
        if (snapped > AudioDurationMs) snapped = AudioDurationMs;
        return snapped;
    }

    /// <summary>空の譜面(BPM イベント1つのみ)を持つ新規 EditorState を生成する。</summary>
    public static EditorState NewEmpty(string songId, string difficulty, double bpm, double durationMs)
    {
        var s = new EditorState
        {
            SongId          = songId,
            Difficulty      = difficulty,
            Bpm             = bpm,
            AudioDurationMs = durationMs,
            Chart = new ChartData
            {
                Version    = 1,
                SongId     = songId,
                Difficulty = difficulty,
                Level      = 1,
                Tags       = new List<string>(),
                Events     = new List<TempoEvent>
                {
                    new TempoEvent { Type = "bpm", TimeMs = 0.0, Bpm = bpm, Multiplier = 1.0 }
                },
                Notes      = new List<NoteData>(),
            },
        };
        s.RebuildNoteIdCounter();
        return s;
    }
}
