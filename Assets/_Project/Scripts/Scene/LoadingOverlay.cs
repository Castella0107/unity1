using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 設定可能な遅延後にロード進捗インジケーターを表示するシングルトン MonoBehaviour。
/// ロードが _showThresholdSec より長くかかった場合にのみオーバーレイを表示する。
/// </summary>
/// <summary>
/// ロード進捗インジケータを設定可能な遅延後に表示するシングルトン。
/// ロードが閾値 (_showThresholdSec) より長くかかった場合のみ表示される。
/// </summary>
public class LoadingOverlay : MonoBehaviour
{
    /// <summary>シングルトンインスタンス。</summary>
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

    /// <summary>ロード開始。閾値タイマーを開始する(早く終わればオーバーレイは出ない)。<paramref name="initialStage"/> はラベル文字列。</summary>
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

    /// <summary>進捗(0〜1)と任意のステージラベルを更新する。</summary>
    public void SetProgress(float value, string stage = null)
    {
        float v = Mathf.Clamp01(value);
        if (_progressBar     != null) _progressBar.value     = v;
        if (_progressBarFill != null) _progressBarFill.fillAmount = v;
        if (stage != null && _stageText != null) _stageText.text = stage;
    }

    /// <summary>即座に非表示にする(保留中の表示もキャンセル)。</summary>
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
