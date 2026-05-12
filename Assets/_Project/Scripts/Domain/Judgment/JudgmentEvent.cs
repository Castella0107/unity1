// Unity-independent. No UnityEngine references allowed in this assembly.

public enum NoteKind { Tap, HoldHead, HoldTick, HoldTail }

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
