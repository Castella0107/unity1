using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// コンフィグ画面の入力タブを管理するコントローラー。
/// レーン・FX キーのリバインド操作、キー表示の更新、コントローラー有効/無効切り替え、およびテストエリアのハイライト表示を担当する。
/// </summary>
public class InputTabController : MonoBehaviour
{
    [Header("Key Bindings")]
    [SerializeField] TextMeshProUGUI[] _keyDisplays;   // [Lane0..3, FxL, FxR]
    [SerializeField] Button[]          _changeButtons;  // same order
    [SerializeField] Button            _defaultsButton;

    [Header("Controller")]
    [SerializeField] Toggle _controllerEnabledToggle;

    [Header("Test Area")]
    [SerializeField] Image[] _testKeyHighlights;   // 6 images, same lane order

    [Header("Refs")]
    [SerializeField] InputActionAsset _inputAsset;

    static readonly string[] LaneActionNames = { "Lane0", "Lane1", "Lane2", "Lane3", "FxL", "FxR" };

    InputActionRebindingExtensions.RebindingOperation _rebindOp;
    int _rebindingIndex = -1;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        LoadSettings();
        SetupButtons();
        RefreshAllDisplays();
        SetupTestArea();
    }

    void OnDisable()
    {
        CleanupTestSubscriptions();
        _rebindOp?.Cancel();
    }

    // ── Setup ─────────────────────────────────────────────────────────────────

    void LoadSettings()
    {
        _controllerEnabledToggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt("ControllerEnabled", 1) == 1);
        _controllerEnabledToggle.onValueChanged.AddListener(v =>
        {
            PlayerPrefs.SetInt("ControllerEnabled", v ? 1 : 0);
            PlayerPrefs.Save();
        });
    }

    void SetupButtons()
    {
        for (int i = 0; i < _changeButtons.Length && i < LaneActionNames.Length; i++)
        {
            int idx = i;
            _changeButtons[i].onClick.AddListener(() => StartRebind(idx));
        }
        if (_defaultsButton != null)
            _defaultsButton.onClick.AddListener(RestoreDefaults);
    }

    void SetupTestArea()
    {
        if (_inputAsset == null) return;
        var map = _inputAsset.FindActionMap("Gameplay");
        if (map == null) return;

        for (int i = 0; i < LaneActionNames.Length; i++)
        {
            var action = map.FindAction(LaneActionNames[i]);
            if (action == null) continue;
            int idx = i;
            action.performed += _ => OnTestKeyDown(idx);
            action.canceled  += _ => OnTestKeyUp(idx);
        }
        map.Enable();
    }

    void CleanupTestSubscriptions()
    {
        if (_inputAsset == null) return;
        var map = _inputAsset.FindActionMap("Gameplay");
        if (map == null) return;

        for (int i = 0; i < LaneActionNames.Length; i++)
        {
            var action = map.FindAction(LaneActionNames[i]);
            if (action == null) continue;
            int idx = i;
            action.performed -= _ => OnTestKeyDown(idx);
            action.canceled  -= _ => OnTestKeyUp(idx);
        }
        map.Disable();
    }

    // ── Test area highlight ───────────────────────────────────────────────────

    void OnTestKeyDown(int idx)
    {
        if (idx < _testKeyHighlights.Length && _testKeyHighlights[idx] != null)
            _testKeyHighlights[idx].color = new Color(0.3f, 0.9f, 1f, 1f);
    }

    void OnTestKeyUp(int idx)
    {
        if (idx < _testKeyHighlights.Length && _testKeyHighlights[idx] != null)
            _testKeyHighlights[idx].color = new Color(1f, 1f, 1f, 0.25f);
    }

    // ── Key display ───────────────────────────────────────────────────────────

    void RefreshAllDisplays()
    {
        if (_inputAsset == null) return;
        var map = _inputAsset.FindActionMap("Gameplay");
        if (map == null) return;

        for (int i = 0; i < LaneActionNames.Length && i < _keyDisplays.Length; i++)
        {
            if (_keyDisplays[i] == null) continue;
            var action = map.FindAction(LaneActionNames[i]);
            if (action == null) { _keyDisplays[i].text = "?"; continue; }

            string display = "?";
            if (action.bindings.Count > 0)
            {
                var path = action.bindings[0].effectivePath;
                if (!string.IsNullOrEmpty(path))
                    display = InputControlPath.ToHumanReadableString(path,
                        InputControlPath.HumanReadableStringOptions.OmitDevice);
            }
            _keyDisplays[i].text = display.ToUpper();
        }
    }

    // ── Rebinding ─────────────────────────────────────────────────────────────

    void StartRebind(int idx)
    {
        if (_rebindingIndex >= 0 || _inputAsset == null) return;
        _rebindingIndex = idx;
        if (idx < _keyDisplays.Length && _keyDisplays[idx] != null)
            _keyDisplays[idx].text = "...";

        var map    = _inputAsset.FindActionMap("Gameplay");
        var action = map?.FindAction(LaneActionNames[idx]);
        if (action == null) { _rebindingIndex = -1; return; }

        action.Disable();
        _rebindOp = action.PerformInteractiveRebinding()
            .WithControlsExcluding("<Mouse>/leftButton")
            .WithControlsExcluding("<Mouse>/rightButton")
            .OnComplete(op =>
            {
                action.Enable();
                _rebindingIndex = -1;
                RefreshAllDisplays();
                SaveBindings();
                op.Dispose();
                _rebindOp = null;
            })
            .OnCancel(op =>
            {
                action.Enable();
                _rebindingIndex = -1;
                RefreshAllDisplays();
                op.Dispose();
                _rebindOp = null;
            })
            .Start();
    }

    void RestoreDefaults()
    {
        if (_inputAsset == null) return;
        var map = _inputAsset.FindActionMap("Gameplay");
        if (map == null) return;
        foreach (string name in LaneActionNames)
            map.FindAction(name)?.RemoveAllBindingOverrides();
        RefreshAllDisplays();
        SaveBindings();
    }

    void SaveBindings()
    {
        var map = _inputAsset?.FindActionMap("Gameplay");
        if (map == null) return;
        PlayerPrefs.SetString("InputBindings_Gameplay", map.SaveBindingOverridesAsJson());
        PlayerPrefs.Save();
    }

    // ── Static boot helper ────────────────────────────────────────────────────

    /// <summary>保存済みのキーバインド上書きを復元する。アプリ起動時に1回呼ぶ。</summary>
    public static void LoadBindingsFromPrefs(InputActionAsset asset)
    {
        string json = PlayerPrefs.GetString("InputBindings_Gameplay", "");
        if (string.IsNullOrEmpty(json)) return;
        var map = asset?.FindActionMap("Gameplay");
        if (map == null) return;
        map.LoadBindingOverridesFromJson(json);
        Debug.Log("[InputTab] Binding overrides restored from PlayerPrefs");
    }
}
