// Unity-independent. No UnityEngine references allowed in this assembly.
public enum NoteType { Tap, Hold, FxTap, FxHold }

// LaneRef is the Domain-layer lane identifier (mirrors LaneId in the Input layer).
// Kept separate so the Domain assembly has no dependency on the Input assembly.
public enum LaneRef  { Lane0, Lane1, Lane2, Lane3, FxL, FxR }

public class NoteData
{
    public int      Id;
    public NoteType Type;
    public LaneRef  Lane;
    public double   TimeMs;
    public double   DurationMs; // Hold / FxHold only
}
