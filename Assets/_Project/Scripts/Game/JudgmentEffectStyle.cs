using UnityEngine;

public enum JudgmentEffectStyle { Subtle = 0, Normal = 1, Bold = 2 }

public static class JudgmentEffectStyleHelper
{
    public static JudgmentEffectStyle GetSaved()
    {
        return (JudgmentEffectStyle)PlayerPrefs.GetInt("JudgmentEffectStyleIdx", 1);
    }

    public static float GetParticleMultiplier(JudgmentEffectStyle style)
    {
        switch (style)
        {
            case JudgmentEffectStyle.Subtle: return 0.5f;
            case JudgmentEffectStyle.Normal: return 1.0f;
            case JudgmentEffectStyle.Bold:   return 1.5f;
            default:                         return 1.0f;
        }
    }

    public static float GetTextScale(JudgmentEffectStyle style)
    {
        switch (style)
        {
            case JudgmentEffectStyle.Subtle: return 0.8f;
            case JudgmentEffectStyle.Normal: return 1.0f;
            case JudgmentEffectStyle.Bold:   return 1.2f;
            default:                         return 1.0f;
        }
    }
}
