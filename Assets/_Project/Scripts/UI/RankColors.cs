using UnityEngine;

/// <summary>ランク文字列・難易度文字列に対応する表示色を提供する静的ユーティリティ。</summary>
public static class RankColors
{
    /// <summary>ランク (S+/S/A+/A/B/C/D) に対応する表示色を返す。</summary>
    public static Color GetRankColor(string rank)
    {
        switch (rank)
        {
            case "S+": return new Color(1.00f, 0.84f, 0.00f);
            case "S":  return new Color(1.00f, 0.95f, 0.40f);
            case "A+": return new Color(0.40f, 1.00f, 0.40f);
            case "A":  return new Color(0.40f, 0.85f, 1.00f);
            case "B":  return new Color(0.85f, 0.85f, 0.85f);
            case "C":  return new Color(0.95f, 0.60f, 0.30f);
            default:   return new Color(1.00f, 0.40f, 0.40f);
        }
    }

    /// <summary>難易度 (easy/normal/hard/extra) に対応する表示色を返す。</summary>
    public static Color GetDifficultyColor(string diff)
    {
        switch (diff?.ToLower())
        {
            case "easy":   return new Color(0.40f, 1.00f, 0.40f);
            case "normal": return new Color(0.40f, 0.85f, 1.00f);
            case "hard":   return new Color(1.00f, 0.60f, 0.20f);
            case "extra":  return new Color(1.00f, 0.35f, 0.35f);
            default:       return Color.white;
        }
    }
}
