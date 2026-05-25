using UnityEngine;

/// <summary>
/// 判定エフェクトの強度を表す列挙型。Subtle（控えめ）・Normal（標準）・Bold（強調）の 3 段階。
/// </summary>
public enum JudgmentEffectStyle { Subtle = 0, Normal = 1, Bold = 2 }

/// <summary>
/// JudgmentEffectStyle の保存・取得と、スタイルに応じたパーティクル量倍率・テキストスケール倍率を提供する静的ヘルパークラス。
/// </summary>
public static class JudgmentEffectStyleHelper
{
    /// <summary>PlayerPrefs に保存された判定エフェクトスタイルを取得する(既定 Normal)。</summary>
    public static JudgmentEffectStyle GetSaved()
    {
        return (JudgmentEffectStyle)PlayerPrefs.GetInt("JudgmentEffectStyleIdx", 1);
    }

    /// <summary>スタイルに応じたパーティクル量の倍率を返す(Subtle=0.5 / Normal=1.0 / Bold=1.5)。</summary>
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

    /// <summary>スタイルに応じたテキストスケール倍率を返す(Subtle=0.8 / Normal=1.0 / Bold=1.2)。</summary>
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
