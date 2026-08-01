using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 判定テキスト（PERFECT+、GREAT など）を画面中央に短いフェードアウト付きで表示するコンポーネント。
/// JudgmentSystem.OnJudged イベントを購読し、オーディオクロックとの同期のため WaitForSeconds を使わずフレームステップで動作する。
/// </summary>
// Shows judgment text (PERFECT+, GREAT, etc.) at screen centre with a brief fade-out.
// Subscribes to JudgmentSystem.OnJudged.
// Uses yield return null (frame-step) instead of WaitForSeconds to stay audio-clock-safe.

public class JudgmentDisplay : MonoBehaviour
{
    [SerializeField] JudgmentSystem  _system;
    [SerializeField] TextMeshProUGUI _judgeText;
    [SerializeField] TextMeshProUGUI _timingText;   // optional FAST / LATE sub-label
    [SerializeField] float           _holdSeconds  = 0.30f;
    [SerializeField] float           _fadeSeconds  = 0.20f;
    /// <summary>表示中の最大不透明度。1未満で常時半透過にし、譜面の視認を妨げない。</summary>
    [SerializeField, Range(0f, 1f)]  float _maxAlpha = 0.6f;
    /// <summary>文字のふち幅 (SDF アウトライン 0〜1)。背景と色が被っても判定文字が読めるように。</summary>
    [SerializeField, Range(0f, 1f)]  float _outlineWidth = 0.22f;

    // ふち色はほぼ黒 (どの判定色・背景でもコントラストが出る)。alpha はフェードで文字に追従。
    static readonly Color OutlineColor = new Color(0.04f, 0.04f, 0.07f, 1f);

    private Coroutine _active;

    /// <summary>レーンに寝かせる角 (X 軸回転、度)。GameHud.LaneTiltDeg と同じ値にして
    /// コンボ・最大ランクの「手元 (レーン溶け込み)」表示と見た目を揃える (K 指示 2026-08-01)。</summary>
    const float LaneTiltDeg = 55f;

    private void OnEnable()
    {
        if (_system != null) _system.OnJudged += OnJudged;
        // ふち幅は一度だけ適用 (fontMaterial のインスタンス化を伴うため毎フレームは避ける)
        if (_judgeText  != null) _judgeText.outlineWidth  = _outlineWidth;
        if (_timingText != null) _timingText.outlineWidth = _outlineWidth;
        // 判定テキストもレーンに寝かせる (HUD は Screen Space Camera なので回転で遠近がつく)
        if (_judgeText  != null) _judgeText.rectTransform.localRotation  = Quaternion.Euler(LaneTiltDeg, 0f, 0f);
        if (_timingText != null) _timingText.rectTransform.localRotation = Quaternion.Euler(LaneTiltDeg, 0f, 0f);
    }

    private void OnDisable()
    {
        if (_system != null) _system.OnJudged -= OnJudged;
    }

    private void OnJudged(Judgment j, double deltaMs)
    {
        if (_judgeText == null) return;

        _judgeText.text  = ToLabel(j);
        _judgeText.color = ToColor(j);

        if (_timingText != null)
        {
            if      (deltaMs < -2.0) _timingText.text = "FAST";
            else if (deltaMs >  2.0) _timingText.text = "LATE";
            else                     _timingText.text = "";
            _timingText.color = new Color(1f, 1f, 1f, _maxAlpha);
        }

        if (_active != null) StopCoroutine(_active);
        _active = StartCoroutine(FadeRoutine());
    }

    private IEnumerator FadeRoutine()
    {
        // Hold phase (最大不透明度 = _maxAlpha の半透過表示)
        ApplyAlpha(_maxAlpha);

        float elapsed = 0f;
        while (elapsed < _holdSeconds) { elapsed += Time.deltaTime; yield return null; }

        // Fade-out phase (_maxAlpha → 0)
        float fadeElapsed = 0f;
        while (fadeElapsed < _fadeSeconds)
        {
            fadeElapsed += Time.deltaTime;
            ApplyAlpha(Mathf.Clamp01((1f - fadeElapsed / _fadeSeconds) * _maxAlpha));
            yield return null;
        }

        ApplyAlpha(0f);
    }

    // 文字色とふちの alpha をまとめて設定する (ふちはフェードに追従させないと残像になる)。
    private void ApplyAlpha(float a)
    {
        Color c = _judgeText.color; c.a = a; _judgeText.color = c;
        _judgeText.outlineColor = new Color(OutlineColor.r, OutlineColor.g, OutlineColor.b, a);
        if (_timingText != null)
        {
            Color tc = _timingText.color; tc.a = a; _timingText.color = tc;
            _timingText.outlineColor = new Color(OutlineColor.r, OutlineColor.g, OutlineColor.b, a);
        }
    }

    private static string ToLabel(Judgment j)
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

    private static Color ToColor(Judgment j)
    {
        switch (j)
        {
            // COLOR_SPEC「琥珀&深緑」: PERFECT=#94F9E0 / GREAT=P-Text #E5C06C /
            // GOOD=#9AB4C2 / MISS=#DD705F。PERFECT+ は PERFECT の上位として S-Core 寄りの白ミント。
            case Judgment.PerfectPlus: return new Color(223 / 255f, 251 / 255f, 244 / 255f); // #DFFBF4
            case Judgment.Perfect:     return new Color(148 / 255f, 249 / 255f, 224 / 255f); // #94F9E0
            case Judgment.Great:       return new Color(229 / 255f, 192 / 255f, 108 / 255f); // #E5C06C
            case Judgment.Good:        return new Color(154 / 255f, 180 / 255f, 194 / 255f); // #9AB4C2
            case Judgment.Miss:        return new Color(221 / 255f, 112 / 255f,  95 / 255f); // #DD705F
            default:                   return Color.white;
        }
    }
}
