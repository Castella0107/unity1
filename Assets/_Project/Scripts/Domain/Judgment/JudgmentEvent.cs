// Unity-independent. No UnityEngine references allowed in this assembly.

/// <summary>
/// ノーツのイベント種別を表す列挙型。タップ・ホールド頭・ホールドティック・ホールド尾の4種類。
/// </summary>
public enum NoteKind { Tap, HoldHead, HoldTick, HoldTail }

/// <summary>
/// 判定エンジンが発行する判定イベントを表す読み取り専用構造体。
/// ノーツID・種別・レーン・判定結果・タイミング差分・発生時刻・コンボ数・自動ミスフラグを含む。
/// </summary>
public readonly struct JudgmentEvent
{
    public readonly int      NoteId;
    public readonly NoteKind Kind;
    public readonly LaneRef  Lane;
    public readonly Judgment Judgment;
    public readonly double   DeltaMs;    // input - note time; 0 for Miss/Tick/auto-Tail
    public readonly double   TimeMs;     // when the event occurred
    public readonly int      Combo;      // combo value after this event
    public readonly bool     IsAutoMiss;

    public JudgmentEvent(
        int noteId, NoteKind kind, LaneRef lane, Judgment judgment,
        double deltaMs, double timeMs, int combo, bool isAutoMiss = false)
    {
        NoteId     = noteId;
        Kind       = kind;
        Lane       = lane;
        Judgment   = judgment;
        DeltaMs    = deltaMs;
        TimeMs     = timeMs;
        Combo      = combo;
        IsAutoMiss = isAutoMiss;
    }
}
