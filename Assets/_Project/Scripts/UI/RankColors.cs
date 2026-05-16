using UnityEngine;

public static class RankColors
{
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
