using System;

// Unity-independent. No UnityEngine references allowed in this assembly.
// Shared verbatim with the server-side (ASP.NET Core) judgment pipeline.
public static class JudgmentWindow
{
    public const int PerfectPlusMs = 16;
    public const int PerfectMs     = 33;
    public const int GreatMs       = 50;
    public const int GoodMs        = 83;

    /// <summary>
    /// Returns the judgment for a given timing delta.
    /// deltaMs = inputTimeMs - noteTimeMs  (positive = late, negative = early)
    /// </summary>
    public static Judgment FromDeltaMs(double deltaMs)
    {
        double abs = Math.Abs(deltaMs);
        if (abs <= PerfectPlusMs) return Judgment.PerfectPlus;
        if (abs <= PerfectMs)     return Judgment.Perfect;
        if (abs <= GreatMs)       return Judgment.Great;
        if (abs <= GoodMs)        return Judgment.Good;
        return Judgment.Miss;
    }
}
