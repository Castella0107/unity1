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

    // ── Snap helpers ────────────────────────────────────────────────────────

    /// <summary>BPMとSnap分母から1スナップ単位(ms)を返す。</summary>
    public double SnapStepMs()
    {
        if (Bpm <= 0.0) return 0.0;
        double beatMs = 60000.0 / Bpm;
        return beatMs / SnapDenominator;
    }

    /// <summary>任意の時刻を最も近いスナップ位置に丸める。</summary>
    public double SnapTime(double timeMs)
    {
        double step = SnapStepMs();
        if (step <= 0.0) return timeMs;
        double rel = timeMs - ChartOffsetMs;
        double snapped = Math.Round(rel / step) * step + ChartOffsetMs;
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
