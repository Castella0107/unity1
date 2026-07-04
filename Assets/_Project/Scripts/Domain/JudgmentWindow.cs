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

    // ── ワイドプロファイル (PLAY OPTIONS「判定幅」= WIDE) ──────────────────────
    // Good/Miss 境界は標準と同じ 100ms (GoodMs = FrameMs*6 = 100.0) のため、
    // ノーツ寿命・オートミス・コンボ/ミス数は両プロファイルで完全に一致する。
    // 変わるのは P+/P/Great の内側等級のみ(= スコア配分のみ)。
    // サーバー(Go)は標準プロファイルのみ実装のため、PVP では WideActive を常に false にすること。

    /// <summary>ワイド時の PerfectPlus 許容差(±ms)。</summary>
    public const double WidePerfectPlusMs = 25.0;
    /// <summary>ワイド時の Perfect 許容差(±ms)。</summary>
    public const double WidePerfectMs     = 50.0;
    /// <summary>ワイド時の Great 許容差(±ms)。Good は標準と同じ GoodMs (100.0)。</summary>
    public const double WideGreatMs       = 75.0;

    /// <summary>ワイド判定でプレイしたことをリプレイ/プレイ記録の Modifiers に残すタグ。</summary>
    public const string WideModifierTag = "JudgeWide";

    /// <summary>ワイドプロファイルが有効か。GamePlayController / ReplayPlaybackController が
    /// セッション開始時に毎回明示的に設定する(PVP・テストは常に false = 標準)。</summary>
    public static bool WideActive { get; set; }

    /// <summary>現在有効な PerfectPlus 許容差(±ms)。</summary>
    public static double ActivePerfectPlusMs => WideActive ? WidePerfectPlusMs : PerfectPlusMs;
    /// <summary>現在有効な Perfect 許容差(±ms)。</summary>
    public static double ActivePerfectMs     => WideActive ? WidePerfectMs     : PerfectMs;
    /// <summary>現在有効な Great 許容差(±ms)。</summary>
    public static double ActiveGreatMs       => WideActive ? WideGreatMs       : GreatMs;

    /// <summary>
    /// Returns the judgment for a given timing delta.
    /// deltaMs = inputTimeMs - noteTimeMs  (positive = late, negative = early)
    /// </summary>
    public static Judgment FromDeltaMs(double deltaMs)
    {
        double abs = Math.Abs(deltaMs);
        if (abs <= ActivePerfectPlusMs) return Judgment.PerfectPlus;
        if (abs <= ActivePerfectMs)     return Judgment.Perfect;
        if (abs <= ActiveGreatMs)       return Judgment.Great;
        if (abs <= GoodMs)              return Judgment.Good;
        return Judgment.Miss;
    }
}
