using UnityEngine;

/// <summary>
/// 判定種別（PerfectPlus / Perfect / Great / Good / Miss）に対応する Color 定数と
/// 表示テキスト文字列を提供する静的ユーティリティクラス。
/// </summary>
public static class JudgmentColors
{
    // プレイフィールド 画面設計 (export-src.html) のシアン基調パレット。
    /// <summary>PerfectPlus 判定の表示色 (最上位 = 最も明るい白シアン)。</summary>
    public static readonly Color PerfectPlus = new Color(0.918f, 0.988f, 1.0f);   // #EAFCFF
    /// <summary>Perfect 判定の表示色。</summary>
    public static readonly Color Perfect     = new Color(0.494f, 0.941f, 0.973f); // #7EF0F8
    /// <summary>Great 判定の表示色。</summary>
    public static readonly Color Great       = new Color(0.329f, 0.784f, 0.910f); // #54C8E8
    /// <summary>Good 判定の表示色。</summary>
    public static readonly Color Good        = new Color(0.561f, 0.659f, 0.722f); // #8FA8B8
    /// <summary>Miss 判定の表示色。</summary>
    public static readonly Color Miss        = new Color(0.878f, 0.373f, 0.373f); // #E05F5F

    /// <summary>判定に対応する表示色を返す。</summary>
    public static Color Get(Judgment j)
    {
        switch (j)
        {
            case Judgment.PerfectPlus: return PerfectPlus;
            case Judgment.Perfect:     return Perfect;
            case Judgment.Great:       return Great;
            case Judgment.Good:        return Good;
            case Judgment.Miss:        return Miss;
            default:                   return Color.white;
        }
    }

    /// <summary>判定に対応する表示テキスト("PERFECT+" 等)を返す。</summary>
    public static string GetText(Judgment j)
    {
        switch (j)
        {
            case Judgment.PerfectPlus: return "PERFECT+";
            case Judgment.Perfect:     return "PERFECT";
            case Judgment.Great:       return "GREAT";
            case Judgment.Good:        return "GOOD";
            case Judgment.Miss:        return "MISS";
            default:                   return "";
        }
    }
}
