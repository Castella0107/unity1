using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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

    readonly List<TabButtonView> _tabButtons = new List<TabButtonView>();
    ConfigTab _currentTab = ConfigTab.Audio;

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
        _navigateAction.Disable();
        _cancelAction.Disable();
    }

    void Start()
    {
        JacketBackgroundController.Instance?.SetFallback();
        _backButton.onClick.AddListener(OnBack);
        BuildTabBar();
        SwitchTab(ConfigTab.Audio);
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

    public void SwitchTab(ConfigTab tab)
    {
        _currentTab = tab;

        for (int i = 0; i < _tabButtons.Count; i++)
            _tabButtons[i].SetSelected(i == (int)tab);

        if (_audioPanel   != null) _audioPanel.SetActive(tab   == ConfigTab.Audio);
        if (_devicesPanel != null) _devicesPanel.SetActive(tab == ConfigTab.Devices);
        if (_displayPanel != null) _displayPanel.SetActive(tab == ConfigTab.Display);
        if (_inputPanel   != null) _inputPanel.SetActive(tab   == ConfigTab.Input);
        if (_gamePanel    != null) _gamePanel.SetActive(tab    == ConfigTab.Game);
        if (_accountPanel != null) _accountPanel.SetActive(tab == ConfigTab.Account);
        if (_dataPanel    != null) _dataPanel.SetActive(tab    == ConfigTab.Data);
    }

    /// Switch to a tab by name (used by child controllers to navigate between tabs).
    public void SwitchToTab(string tabName)
    {
        switch (tabName.ToLower())
        {
            case "audio":   SwitchTab(ConfigTab.Audio);   break;
            case "devices": SwitchTab(ConfigTab.Devices); break;
            case "display": SwitchTab(ConfigTab.Display); break;
            case "input":   SwitchTab(ConfigTab.Input);   break;
            case "game":    SwitchTab(ConfigTab.Game);    break;
            case "account": SwitchTab(ConfigTab.Account); break;
            case "data":    SwitchTab(ConfigTab.Data);    break;
        }
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    void OnNavigate(InputAction.CallbackContext ctx)
    {
        var v = ctx.ReadValue<Vector2>();
        if      (v.y >  0.5f) SwitchTab((ConfigTab)(((int)_currentTab - 1 + Tabs.Length) % Tabs.Length));
        else if (v.y < -0.5f) SwitchTab((ConfigTab)(((int)_currentTab + 1)                % Tabs.Length));
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

public class TabButtonView
{
    public GameObject Root   { get; }
    public Button     Button { get; }

    Image            _bg;
    TextMeshProUGUI  _label;

    static readonly Color ColIdle     = new Color(1f, 1f, 1f, 0.05f);
    static readonly Color ColSelected = new Color(0.17f, 0.35f, 0.63f, 0.80f);

    public TabButtonView(GameObject go, string labelText)
    {
        Root   = go;
        Button = go.GetComponent<Button>();
        _bg    = go.transform.Find("Background")?.GetComponent<Image>();
        _label = go.GetComponentInChildren<TextMeshProUGUI>();

        if (_label != null) _label.text = labelText;
        SetSelected(false);
    }

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
