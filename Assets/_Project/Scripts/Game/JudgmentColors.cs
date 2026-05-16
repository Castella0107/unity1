using UnityEngine;

/// <summary>
/// 判定種別（PerfectPlus / Perfect / Great / Good / Miss）に対応する Color 定数と
/// 表示テキスト文字列を提供する静的ユーティリティクラス。
/// </summary>
public static class JudgmentColors
{
    public static readonly Color PerfectPlus = new Color(1.0f, 0.85f, 0.2f);
    public static readonly Color Perfect     = new Color(0.3f, 0.95f, 0.4f);
    public static readonly Color Great       = new Color(0.3f, 0.6f,  1.0f);
    public static readonly Color Good        = new Color(0.7f, 0.4f,  1.0f);
    public static readonly Color Miss        = new Color(1.0f, 0.3f,  0.3f);

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
