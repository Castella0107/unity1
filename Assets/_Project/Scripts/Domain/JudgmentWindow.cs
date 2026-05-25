using System;

// Unity-independent. No UnityEngine references allowed in this assembly.
// Shared verbatim with the server-side (ASP.NET Core) judgment pipeline.
/// <summary>
/// 判定ウィンドウの定数（ミリ秒）と、タイミング差分から判定を算出するメソッドを提供する静的クラス。
/// サーバーサイドの判定パイプラインと共有される。
/// </summary>
public static class JudgmentWindow
{
    /// <summary>PerfectPlus 判定の許容タイミング差(±ms)。</summary>
    public const int PerfectPlusMs = 16;
    /// <summary>Perfect 判定の許容タイミング差(±ms)。</summary>
    public const int PerfectMs     = 33;
    /// <summary>Great 判定の許容タイミング差(±ms)。</summary>
    public const int GreatMs       = 50;
    /// <summary>Good 判定の許容タイミング差(±ms)。これを超えると Miss。</summary>
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
