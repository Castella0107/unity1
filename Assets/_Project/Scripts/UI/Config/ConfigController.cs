using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// コンフィグ画面全体を管理するコントローラー。
/// タブバーの構築・タブ切り替え（Audio / Devices / Display / Input / Game / Account / Data）、およびキーボード入力によるナビゲーションを担当する。
/// </summary>
public class ConfigController : MonoBehaviour
{
    [Header("Tab Bar")]
    [SerializeField] RectTransform _tabBarContent;
    [SerializeField] GameObject    _tabButtonPrefab;

    [Header("Content Panels (Audio / Devices / Display / Input / Game / Account / Data)")]
    [SerializeField] GameObject _audioPanel;
    [SerializeField] GameObject _devicesPanel;
    [SerializeField] GameObject _displayPanel;
    [SerializeField] GameObject _inputPanel;
    [SerializeField] GameObject _gamePanel;
    [SerializeField] GameObject _accountPanel;
    [SerializeField] GameObject _dataPanel;

    [Header("Navigation")]
    [SerializeField] Button _backButton;

    [Header("Input")]
    [SerializeField] InputActionAsset _inputAsset;

    // ── Tab definitions ───────────────────────────────────────────────────────

    /// <summary>コンフィグ画面のタブ種別を表す列挙型。</summary>
    public enum ConfigTab { Audio = 0, Devices = 1, Display = 2, Input = 3, Game = 4, Account = 5, Data = 6 }

    static readonly (ConfigTab tab, string label)[] Tabs =
    {
        (ConfigTab.Audio,   "Audio"),
        (ConfigTab.Devices, "Devices"),
        (ConfigTab.Display, "Display"),
        (ConfigTab.Input,   "Input"),
        (ConfigTab.Game,    "Game"),
        (ConfigTab.Account, "Account"),
        (ConfigTab.Data,    "Data"),
    };

    readonly List<TabButtonView>           _tabButtons = new List<TabButtonView>();
    Dictionary<ConfigTab, GameObject>      _panelMap;
    ConfigTab                              _currentTab = ConfigTab.Audio;

    InputAction _navigateAction;
    InputAction _cancelAction;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        var map = _inputAsset.FindActionMap("UI", throwIfNotFound: true);
        _navigateAction = map.FindAction("Navigate", throwIfNotFound: true);
        _cancelAction   = map.FindAction("Cancel",   throwIfNotFound: true);
    }

    void OnEnable()
    {
        _navigateAction.Enable();
        _cancelAction.Enable();
        _navigateAction.performed += OnNavigate;
        _cancelAction.performed   += OnCancel;
    }

    void OnDisable()
    {
        _navigateAction.performed -= OnNavigate;
        _cancelAction.performed   -= OnCancel;
    }

    void Start()
    {
        _panelMap = new Dictionary<ConfigTab, GameObject>
        {
            { ConfigTab.Audio,   _audioPanel   },
            { ConfigTab.Devices, _devicesPanel },
            { ConfigTab.Display, _displayPanel },
            { ConfigTab.Input,   _inputPanel   },
            { ConfigTab.Game,    _gamePanel    },
            { ConfigTab.Account, _accountPanel },
            { ConfigTab.Data,    _dataPanel    },
        };

        JacketBackgroundController.Instance?.SetFallback();
        _backButton.onClick.AddListener(OnBack);
        BuildTabBar();
        SwitchTab(ConfigTab.Audio);

        RhythmGame.UI.Common.ShortcutHintOverlay.Set(
            "←→ / Tab / LB RB: タブ切替   ↑↓: 項目   Space: 決定   ESC: 戻る");
    }

    // ── Tab bar ───────────────────────────────────────────────────────────────

    void BuildTabBar()
    {
        foreach (Transform t in _tabBarContent) Destroy(t.gameObject);
        _tabButtons.Clear();

        for (int i = 0; i < Tabs.Length; i++)
        {
            var go   = Instantiate(_tabButtonPrefab, _tabBarContent);
            var view = new TabButtonView(go, Tabs[i].label);
            int idx  = i;
            view.Button.onClick.AddListener(() => SwitchTab((ConfigTab)idx));
            _tabButtons.Add(view);
        }
    }

    /// <summary>指定タブに切り替え、ボタンの選択状態とパネルの表示を更新する。</summary>
    public void SwitchTab(ConfigTab tab)
    {
        _currentTab = tab;

        for (int i = 0; i < _tabButtons.Count; i++)
            _tabButtons[i].SetSelected(i == (int)tab);

        foreach (var kvp in _panelMap)
            if (kvp.Value != null) kvp.Value.SetActive(kvp.Key == tab);

        FocusFirstSelectable(tab);
    }

    /// <summary>タブ名(文字列)でタブを切り替える。子コントローラーからのタブ間遷移に使う。</summary>
    public void SwitchToTab(string tabName)
    {
        if (System.Enum.TryParse<ConfigTab>(tabName, ignoreCase: true, out var tab))
            SwitchTab(tab);
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    void OnNavigate(InputAction.CallbackContext ctx)
    {
        var v = ctx.ReadValue<Vector2>();

        // 横方向はタブ切替。ただしスライダー等「←→で値が変わる項目」を選択中はそちらに譲る。
        if (Mathf.Abs(v.x) > 0.5f && Mathf.Abs(v.x) > Mathf.Abs(v.y))
        {
            if (FocusedConsumesHorizontal()) return;
            if (v.x > 0f) StepTab(+1);
            else          StepTab(-1);
        }
        // 縦方向（項目フォーカス移動）は EventSystem の Selectable ナビゲーションに任せる。
    }

    void StepTab(int dir)
    {
        SwitchTab((ConfigTab)(((int)_currentTab + dir + Tabs.Length) % Tabs.Length));
    }

    // 現在フォーカス中の UI が横入力を消費する（Slider 等）かどうか。
    static bool FocusedConsumesHorizontal()
    {
        var go = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (go == null) return false;
        return go.GetComponent<Slider>() != null || go.GetComponent<Scrollbar>() != null;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && (kb.tabKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame)) StepTab(+1);
        if (kb != null && kb.qKey.wasPressedThisFrame) StepTab(-1);

        var pad = Gamepad.current;
        if (pad != null)
        {
            if (RhythmGame.Input.GamepadLayout.NextTabPressed(pad)) StepTab(+1);
            if (RhythmGame.Input.GamepadLayout.PrevTabPressed(pad)) StepTab(-1);
        }
    }

    // タブ切替時、新パネル内の最初の操作可能項目へフォーカスを移す（キーボード/パッドで項目操作可に）。
    void FocusFirstSelectable(ConfigTab tab)
    {
        if (EventSystem.current == null) return;
        if (!_panelMap.TryGetValue(tab, out var panel) || panel == null) return;
        var sel = panel.GetComponentInChildren<Selectable>(false);
        EventSystem.current.SetSelectedGameObject(sel != null ? sel.gameObject : null);
    }

    void OnCancel(InputAction.CallbackContext ctx) => OnBack();

    void OnBack()
    {
        if (SceneRouter.Instance != null)
            SceneRouter.Instance.GoTo(SceneId.Title);
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene("Title");
    }
}

// ── Tab button view helper ────────────────────────────────────────────────────

/// <summary>
/// タブボタン1つの表示状態（選択中 / 非選択）を管理するビューヘルパークラス。
/// </summary>
public class TabButtonView
{
    /// <summary>このタブボタンのルート GameObject。</summary>
    public GameObject Root   { get; }
    /// <summary>クリック用ボタン。</summary>
    public Button     Button { get; }

    Image            _bg;
    TextMeshProUGUI  _label;

    static readonly Color ColIdle     = new Color(1f, 1f, 1f, 0.05f);
    static readonly Color ColSelected = new Color(0.17f, 0.35f, 0.63f, 0.80f);

    /// <summary>GameObject とラベル文字列からタブボタンの表示を構築する。</summary>
    public TabButtonView(GameObject go, string labelText)
    {
        Root   = go;
        Button = go.GetComponent<Button>();
        _bg    = go.transform.Find("Background")?.GetComponent<Image>();
        _label = go.GetComponentInChildren<TextMeshProUGUI>();

        if (_label != null) _label.text = labelText;
        SetSelected(false);
    }

    /// <summary>選択状態に応じて背景色とラベル色を切り替える。</summary>
    public void SetSelected(bool selected)
    {
        if (_bg != null) _bg.color = selected ? ColSelected : ColIdle;
        if (_label != null)
        {
            var c = _label.color;
            c.a = selected ? 1.0f : 0.55f;
            _label.color = c;
        }
    }
}
