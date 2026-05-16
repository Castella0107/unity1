using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// コルーチンベースのスクリーン遷移エフェクトを提供する MonoBehaviour。
/// FadeOut でオーバーレイが画面を覆い、FadeIn で画面が再び見える状態に戻る。
/// Time.unscaledDeltaTime を使用するため Time.timeScale が 0 でも動作する。
/// </summary>
// Coroutine-based screen transition effect.
// FadeOut → screen goes black (overlay alpha 0→1).
// FadeIn  → screen becomes visible (overlay alpha 1→0).
// Uses Time.unscaledDeltaTime so it works even when Time.timeScale == 0.
public class TransitionFx : MonoBehaviour
{
    [SerializeField] CanvasGroup _canvasGroup;
    [SerializeField] Image       _colorImage;      // tint color (black for FadeBlack, white for FadeWhite)
    [SerializeField] float       _fadeDuration = 0.3f;

    void Awake()
    {
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha          = 0f;
            _canvasGroup.blocksRaycasts = false;
        }
    }

    // Screen fades OUT (overlay covers the screen).
    public IEnumerator FadeOut(TransitionStyle style)
    {
        if (style == TransitionStyle.None || style == TransitionStyle.FastCut)
        {
            yield break;
        }

        ApplyColor(style);
        yield return TweenAlpha(0f, 1f);
    }

    // Screen fades IN (overlay retreats, revealing the new scene).
    public IEnumerator FadeIn(TransitionStyle style)
    {
        if (style == TransitionStyle.None || style == TransitionStyle.FastCut)
        {
            yield break;
        }

        yield return TweenAlpha(1f, 0f);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void ApplyColor(TransitionStyle style)
    {
        if (_colorImage == null) return;
        _colorImage.color = style == TransitionStyle.FadeWhite ? Color.white : Color.black;
    }

    IEnumerator TweenAlpha(float from, float to)
    {
        if (_canvasGroup == null) yield break;

        bool toOpaque = to > from;
        if (toOpaque) _canvasGroup.blocksRaycasts = true;

        float elapsed = 0f;
        while (elapsed < _fadeDuration)
        {
            elapsed              += Time.unscaledDeltaTime;
            _canvasGroup.alpha    = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / _fadeDuration));
            yield return null;
        }
        _canvasGroup.alpha = to;

        if (!toOpaque) _canvasGroup.blocksRaycasts = false;
    }
}
