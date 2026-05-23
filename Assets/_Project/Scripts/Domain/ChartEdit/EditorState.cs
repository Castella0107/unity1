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
    public SongMetadata Metadata     { get; set; }
    public ChartData    Chart        { get; set; }
    public string       SongBasePath { get; set; }
    public string       SongId       { get; set; }
    public string       Difficulty   { get; set; }
    public double       AudioDurationMs { get; set; }

    // ── Edit UI state ───────────────────────────────────────────────────────
    public double       CurrentTimeMs   { get; set; }
    public int          SnapDenominator { get; set; } = 4;     // 1/4 拍刻み
    public double       Bpm             { get; set; } = 120.0;
    public double       ChartOffsetMs   { get; set; } = 0.0;   // BPM 基準時刻 (1拍目)
    public NoteType     PaletteType     { get; set; } = NoteType.Tap;
    public bool         IsDirty         { get; set; }

    // ── Selection ───────────────────────────────────────────────────────────
    public readonly HashSet<int> SelectedNoteIds = new HashSet<int>();

    // ── Loop range (難所反復用) ──────────────────────────────────────────────
    public double LoopStartMs { get; set; } = -1.0;   // -1 = 未設定
    public double LoopEndMs   { get; set; } = -1.0;
    public bool   LoopEnabled { get; set; }
    public bool   HasLoopRange => LoopStartMs >= 0 && LoopEndMs > LoopStartMs;

    // ── ID issuance ─────────────────────────────────────────────────────────
    int _nextNoteId;

    public int IssueNoteId()
    {
        int id = _nextNoteId++;
        return id;
    }

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
