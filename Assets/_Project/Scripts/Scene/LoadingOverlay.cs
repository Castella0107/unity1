using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Shows a loading progress indicator after a configurable delay.
// Only appears when a load takes longer than _showThresholdSec.
public class LoadingOverlay : MonoBehaviour
{
    public static LoadingOverlay Instance { get; private set; }

    [SerializeField] Canvas          _canvas;
    [SerializeField] Slider          _progressBar;
    [SerializeField] Image           _progressBarFill;   // optional Image-fill alternative
    [SerializeField] TextMeshProUGUI _stageText;
    [SerializeField] TextMeshProUGUI _tipText;

    [SerializeField] float _showThresholdSec = 0.3f;   // delay before overlay appears

    Coroutine _showDelayRoutine;
    bool      _wantsVisible;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SetVisible(false);
    }

    // Begin loading: start threshold timer. Pass initialStage for the label.
    public IEnumerator Show(string initialStage = "Loading...")
    {
        _wantsVisible = true;
        SetProgress(0f, initialStage);
        if (_tipText != null) _tipText.text = GetRandomTip();

        // Start delay; if load finishes fast the overlay never shows.
        if (_showDelayRoutine != null) StopCoroutine(_showDelayRoutine);
        _showDelayRoutine = StartCoroutine(ShowAfterDelay());

        yield return null;
    }

    // Update progress (0–1) and optional label.
    public void SetProgress(float value, string stage = null)
    {
        float v = Mathf.Clamp01(value);
        if (_progressBar     != null) _progressBar.value     = v;
        if (_progressBarFill != null) _progressBarFill.fillAmount = v;
        if (stage != null && _stageText != null) _stageText.text = stage;
    }

    // Hide immediately (cancels any pending show).
    public IEnumerator Hide()
    {
        _wantsVisible = false;
        if (_showDelayRoutine != null)
        {
            StopCoroutine(_showDelayRoutine);
            _showDelayRoutine = null;
        }
        SetVisible(false);
        yield return null;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void SetVisible(bool visible)
    {
        if (_canvas != null) _canvas.enabled = visible;
    }

    IEnumerator ShowAfterDelay()
    {
        yield return new WaitForSecondsRealtime(_showThresholdSec);
        if (_wantsVisible) SetVisible(true);
        _showDelayRoutine = null;
    }

    static readonly string[] Tips =
    {
        "Tip: ピックフェーズで難易度は後出しできます",
        "Tip: 楽曲毎にオフセット調整が可能",
        "Tip: PVP では弱者先攻でピック順が決まる",
        "Tip: Mirror / Random もスコア集計対象",
    };

    static string GetRandomTip() => Tips[Random.Range(0, Tips.Length)];
}
