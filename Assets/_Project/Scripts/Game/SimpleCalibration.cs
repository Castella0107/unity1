using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Judgment/visual offset calibration panel shown from the Title screen.
// Offsets are stored in PlayerPrefs and applied in GamePlayController.

/// <summary>
/// タイトル画面から呼び出せる判定オフセット・ビジュアルオフセットのキャリブレーションパネル。
/// スライダーで -100 〜 +100 ms の範囲で調整でき、値は PlayerPrefs に保存される。
/// 静的メソッド GetJudgmentOffset() / GetVisualOffset() で GamePlayController からも参照される。
/// </summary>
public class SimpleCalibration : MonoBehaviour
{
    [SerializeField] GameObject      _panel;
    [SerializeField] Slider          _judgeSlider;
    [SerializeField] Slider          _visualSlider;
    [SerializeField] TextMeshProUGUI _judgeValueText;
    [SerializeField] TextMeshProUGUI _visualValueText;
    [SerializeField] Button          _closeButton;

    private const string KEY_JUDGE  = "JudgmentOffsetMs";
    private const string KEY_VISUAL = "VisualOffsetMs";

    private void Start()
    {
        if (_panel != null) _panel.SetActive(false);

        if (_judgeSlider  != null)
        {
            _judgeSlider.minValue   = -100f;
            _judgeSlider.maxValue   =  100f;
            _judgeSlider.wholeNumbers = true;
            _judgeSlider.value      = PlayerPrefs.GetInt(KEY_JUDGE, 0);
            _judgeSlider.onValueChanged.AddListener(_ => Refresh());
        }
        if (_visualSlider != null)
        {
            _visualSlider.minValue  = -100f;
            _visualSlider.maxValue  =  100f;
            _visualSlider.wholeNumbers = true;
            _visualSlider.value     = PlayerPrefs.GetInt(KEY_VISUAL, 0);
            _visualSlider.onValueChanged.AddListener(_ => Refresh());
        }
        if (_closeButton  != null) _closeButton.onClick.AddListener(Close);
        Refresh();
    }

    /// <summary>キャリブレーションパネルを開き、保存済みのオフセット値をスライダーに反映する。</summary>
    public void Open()
    {
        if (_judgeSlider  != null) _judgeSlider.value  = PlayerPrefs.GetInt(KEY_JUDGE,  0);
        if (_visualSlider != null) _visualSlider.value = PlayerPrefs.GetInt(KEY_VISUAL, 0);
        Refresh();
        if (_panel != null) _panel.SetActive(true);
    }

    private void Close()
    {
        Save();
        if (_panel != null) _panel.SetActive(false);
    }

    private void Refresh()
    {
        int jv = _judgeSlider  != null ? (int)_judgeSlider.value  : 0;
        int vv = _visualSlider != null ? (int)_visualSlider.value : 0;
        if (_judgeValueText  != null) _judgeValueText .text = string.Format("{0:+0;-0;0} ms", jv);
        if (_visualValueText != null) _visualValueText.text = string.Format("{0:+0;-0;0} ms", vv);
    }

    private void Save()
    {
        if (_judgeSlider  != null) PlayerPrefs.SetInt(KEY_JUDGE,  (int)_judgeSlider.value);
        if (_visualSlider != null) PlayerPrefs.SetInt(KEY_VISUAL, (int)_visualSlider.value);
        PlayerPrefs.Save();
    }

    /// <summary>保存済みの判定オフセット(ms)を返す。曲開始時に GamePlayController が参照する。</summary>
    public static int GetJudgmentOffset() => PlayerPrefs.GetInt(KEY_JUDGE,  0);
    /// <summary>保存済みの映像オフセット(ms)を返す。</summary>
    public static int GetVisualOffset()   => PlayerPrefs.GetInt(KEY_VISUAL, 0);
}
