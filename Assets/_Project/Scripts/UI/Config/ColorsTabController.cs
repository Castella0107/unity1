using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Config「色」タブ。ノーツ(レーン別6)・レーン仕切り線・判定線の色を RGB(0〜255)スライダーで編集する。
/// 行頭の色見本クリックで「選択中の行」を切り替え、その行に対して
///   ・プリセットパレット(クリックで色を適用)
///   ・コピー / 貼り付け(行間で色を共有バッファ経由でコピー)
/// を行える。変更は即時 GameColorSettings(PlayerPrefs)へ保存しプレビュー兼用。確定/破棄は
/// ConfigController のグローバル保存モデルに従う。
///
/// 行インデックス: 0〜5 = ノーツ各レーン / 6 = 仕切り線 / 7 = 判定線。配線は BuildConfigScene.BuildColorsTab。
/// </summary>
public class ColorsTabController : MonoBehaviour
{
    [Header("Rows")]
    [SerializeField] Image[]           _swatches;          // 各行のプレビュー色見本(=選択ボタン)
    [SerializeField] Button[]          _rowSelectButtons;  // 行選択ボタン(色見本上)
    [SerializeField] Image[]           _rowBackgrounds;    // 行の背景(選択ハイライト用)
    [SerializeField] Slider[]          _rSliders;          // R (0-255)
    [SerializeField] Slider[]          _gSliders;          // G
    [SerializeField] Slider[]          _bSliders;          // B
    [SerializeField] TextMeshProUGUI[] _rValues;
    [SerializeField] TextMeshProUGUI[] _gValues;
    [SerializeField] TextMeshProUGUI[] _bValues;

    [Header("Tools (選択中の行に適用)")]
    [SerializeField] Image            _activeSwatch;       // 選択中の行の色
    [SerializeField] TextMeshProUGUI  _activeNameLabel;    // 選択中の行名
    [SerializeField] Button           _copyButton;
    [SerializeField] Button           _pasteButton;
    [SerializeField] Button[]         _paletteButtons;     // プリセットパレット
    [SerializeField] Image[]          _paletteSwatches;    // パレット各セルの色(参照元)
    [SerializeField] string[]         _rowNames;

    [Header("2D カラーピッカー")]
    [SerializeField] ColorSquarePicker _picker;            // 右側の SV 四角 + 色相バー

    const int NoteCount = GameColorSettings.LaneCount; // 6 (= 仕切り線インデックス)

    static readonly Color RowIdle   = new Color(1f, 1f, 1f, 0.06f);
    static readonly Color RowActive = new Color(0.42f, 0.32f, 0.85f, 0.35f);

    int  Count => _rSliders != null ? _rSliders.Length : 0;
    bool _wired;
    bool _loading;
    int  _activeRow;
    bool _hasCopy;
    bool _fromPicker;   // ピッカー発の変更中は逆方向同期(SetColor)を抑止
    Color _copyBuffer;

    void Start()
    {
        for (int i = 0; i < Count; i++)
        {
            int idx = i;
            if (_rSliders[i] != null) _rSliders[i].onValueChanged.AddListener(_ => OnChanged(idx));
            if (_gSliders[i] != null) _gSliders[i].onValueChanged.AddListener(_ => OnChanged(idx));
            if (_bSliders[i] != null) _bSliders[i].onValueChanged.AddListener(_ => OnChanged(idx));
            if (_rowSelectButtons != null && i < _rowSelectButtons.Length && _rowSelectButtons[i] != null)
                _rowSelectButtons[i].onClick.AddListener(() => SelectRow(idx));
        }
        if (_paletteButtons != null)
            for (int p = 0; p < _paletteButtons.Length; p++)
            {
                int pi = p;
                if (_paletteButtons[p] != null) _paletteButtons[p].onClick.AddListener(() => ApplyPalette(pi));
            }
        if (_copyButton  != null) _copyButton.onClick.AddListener(CopyActive);
        if (_pasteButton != null) _pasteButton.onClick.AddListener(PasteActive);
        if (_picker      != null) _picker.ColorChanged += OnPickerColor;

        _wired = true;
        RefreshAll();
        SelectRow(0);
    }

    // タブ再表示やリセット後の再読込で現在値に同期(リスナー配線後のみ)。
    void OnEnable()
    {
        if (_wired) { RefreshAll(); SelectRow(_activeRow); }
    }

    void RefreshAll()
    {
        _loading = true;
        for (int i = 0; i < Count; i++)
        {
            Color c = Get(i);
            SetSliders(i, c);
            UpdateRowVisual(i, c);
        }
        _loading = false;
    }

    void SetSliders(int i, Color c)
    {
        if (_rSliders[i] != null) _rSliders[i].SetValueWithoutNotify(Mathf.Round(c.r * 255f));
        if (_gSliders[i] != null) _gSliders[i].SetValueWithoutNotify(Mathf.Round(c.g * 255f));
        if (_bSliders[i] != null) _bSliders[i].SetValueWithoutNotify(Mathf.Round(c.b * 255f));
    }

    void OnChanged(int i)
    {
        if (_loading) return;
        var c = new Color(
            SliderVal(_rSliders, i) / 255f,
            SliderVal(_gSliders, i) / 255f,
            SliderVal(_bSliders, i) / 255f,
            1f);
        Set(i, c);
        UpdateRowVisual(i, c);
        if (i == _activeRow) UpdateActiveInfo();
    }

    // ── 選択中の行 ──────────────────────────────────────────────────────────────
    void SelectRow(int i)
    {
        _activeRow = Mathf.Clamp(i, 0, Mathf.Max(0, Count - 1));
        if (_rowBackgrounds != null)
            for (int k = 0; k < _rowBackgrounds.Length; k++)
                if (_rowBackgrounds[k] != null)
                    _rowBackgrounds[k].color = (k == _activeRow) ? RowActive : RowIdle;
        UpdateActiveInfo();
    }

    void UpdateActiveInfo()
    {
        Color c = Get(_activeRow);
        if (_activeSwatch != null) _activeSwatch.color = new Color(c.r, c.g, c.b, 1f);
        if (_activeNameLabel != null)
            _activeNameLabel.text = (_rowNames != null && _activeRow < _rowNames.Length)
                ? _rowNames[_activeRow] : ("行" + _activeRow);
        if (_pasteButton != null) _pasteButton.interactable = _hasCopy;
        // 行選択・スライダー編集など「ピッカー以外」発の変更時はピッカーへ反映。
        if (_picker != null && !_fromPicker) _picker.SetColor(c);
    }

    // 2D ピッカーのドラッグで選択中の行の色を更新する。
    void OnPickerColor(Color c)
    {
        if (_loading) return;
        _fromPicker = true;
        ApplyToActive(c);
        _fromPicker = false;
    }

    // ── パレット / コピー / 貼り付け ────────────────────────────────────────────
    void ApplyPalette(int pi)
    {
        if (_paletteSwatches == null || pi >= _paletteSwatches.Length || _paletteSwatches[pi] == null) return;
        Color c = _paletteSwatches[pi].color; c.a = 1f;
        ApplyToActive(c);
    }

    void CopyActive()
    {
        _copyBuffer = Get(_activeRow);
        _hasCopy = true;
        if (_pasteButton != null) _pasteButton.interactable = true;
    }

    void PasteActive()
    {
        if (!_hasCopy) return;
        Color c = _copyBuffer; c.a = 1f;
        ApplyToActive(c);
    }

    // 選択中の行へ色を適用し、スライダー・見本・保存値を更新する。
    void ApplyToActive(Color c)
    {
        int i = _activeRow;
        Set(i, c);
        _loading = true; SetSliders(i, c); _loading = false;
        UpdateRowVisual(i, c);
        UpdateActiveInfo();
    }

    void UpdateRowVisual(int i, Color c)
    {
        if (_swatches != null && i < _swatches.Length && _swatches[i] != null)
            _swatches[i].color = new Color(c.r, c.g, c.b, 1f);
        SetText(_rValues, i, c.r);
        SetText(_gValues, i, c.g);
        SetText(_bValues, i, c.b);
    }

    // ── 行インデックス ↔ GameColorSettings ──────────────────────────────────────
    static Color Get(int i)
        => i < NoteCount ? GameColorSettings.NoteColor(i)
         : i == NoteCount ? GameColorSettings.DividerColor
         : i == NoteCount + 1 ? GameColorSettings.JudgmentLineColor
         : GameColorSettings.ChordColor;   // 8 行目: 同時押しノーツ (K 指示 2026-07-30)

    static void Set(int i, Color c)
    {
        if (i < NoteCount)            GameColorSettings.SetNoteColor(i, c);
        else if (i == NoteCount)      GameColorSettings.DividerColor = c;
        else if (i == NoteCount + 1)  GameColorSettings.JudgmentLineColor = c;
        else                          GameColorSettings.ChordColor = c;
    }

    static float SliderVal(Slider[] arr, int i)
        => (arr != null && i < arr.Length && arr[i] != null) ? arr[i].value : 0f;

    static void SetText(TextMeshProUGUI[] arr, int i, float v01)
    {
        if (arr != null && i < arr.Length && arr[i] != null)
            arr[i].text = Mathf.RoundToInt(v01 * 255f).ToString();
    }
}
