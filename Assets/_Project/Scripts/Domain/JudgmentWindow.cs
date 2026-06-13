using System;

// Unity-independent. No UnityEngine references allowed in this assembly.
// Shared verbatim with the server-side (ASP.NET Core) judgment pipeline.
/// <summary>
/// 判定ウィンドウの定数（ミリ秒）と、タイミング差分から判定を算出するメソッドを提供する静的クラス。
/// サーバーサイドの判定パイプラインと共有される。
/// </summary>
public static class JudgmentWindow
{
    /// <summary>1 フレーム(60fps)の長さ(ms) = 1000/60 ≈ 16.6667。判定幅のフレーム基準。
    /// サーバー(Go)も必ず同一の式 1000.0/60.0 で算出すること(丸めリテラル禁止)。</summary>
    public const double FrameMs = 1000.0 / 60.0;

    /// <summary>PerfectPlus 判定の許容タイミング差(±ms)。≈1 フレーム(整数維持)。</summary>
    public const int PerfectPlusMs = 16;
    /// <summary>Perfect 判定の許容タイミング差(±ms)。≈2 フレーム(整数維持)。</summary>
    public const int PerfectMs     = 33;
    /// <summary>Great 判定の許容タイミング差(±ms)。4 フレーム = 1000/60×4 ≈ 66.6667。</summary>
    public const double GreatMs    = FrameMs * 4;
    /// <summary>Good 判定の許容タイミング差(±ms)。これを超えると Miss。6 フレーム = 1000/60×6 = 100.0。</summary>
    public const double GoodMs     = FrameMs * 6;

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
