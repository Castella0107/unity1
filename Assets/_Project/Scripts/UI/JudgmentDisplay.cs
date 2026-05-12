using System.Collections;
using TMPro;
using UnityEngine;

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

    private Coroutine _active;

    private void OnEnable()
    {
        if (_system != null) _system.OnJudged += OnJudged;
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
            _timingText.color = new Color(1f, 1f, 1f, 1f);
        }

        if (_active != null) StopCoroutine(_active);
        _active = StartCoroutine(FadeRoutine());
    }

    private IEnumerator FadeRoutine()
    {
        // Hold phase (full opacity)
        Color c = _judgeText.color;
        c.a = 1f;
        _judgeText.color = c;

        float elapsed = 0f;
        while (elapsed < _holdSeconds) { elapsed += Time.deltaTime; yield return null; }

        // Fade-out phase
        float fadeElapsed = 0f;
        while (fadeElapsed < _fadeSeconds)
        {
            fadeElapsed  += Time.deltaTime;
            float alpha   = 1f - fadeElapsed / _fadeSeconds;
            c             = _judgeText.color;
            c.a           = Mathf.Clamp01(alpha);
            _judgeText.color = c;
            if (_timingText != null)
            {
                Color tc = _timingText.color; tc.a = c.a; _timingText.color = tc;
            }
            yield return null;
        }

        c.a = 0f;
        _judgeText.color = c;
        if (_timingText != null) { Color tc = _timingText.color; tc.a = 0f; _timingText.color = tc; }
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
            case Judgment.PerfectPlus: return new Color(1.00f, 0.92f, 0.20f);
            case Judgment.Perfect:     return new Color(0.30f, 1.00f, 1.00f);
            case Judgment.Great:       return new Color(0.30f, 1.00f, 0.40f);
            case Judgment.Good:        return new Color(0.30f, 0.60f, 1.00f);
            case Judgment.Miss:        return new Color(0.55f, 0.55f, 0.55f);
            default:                   return Color.white;
        }
    }
}
