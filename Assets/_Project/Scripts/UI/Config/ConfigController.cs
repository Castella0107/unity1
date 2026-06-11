using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// コンフィグ画面全体を管理するコントローラー (モック適用 2026-06-07 再編)。
/// 5タブ構成: ゲームプレイ / キー設定 / グラフィック / オーディオ / アカウント設定。
/// タブバー構築・タブ切替(←→/Tab/Q E/L R Shift/LB RB)・右側説明カード・F9リセット・ESC=呼び出し元復帰を担当する。
/// </summary>
public class ConfigController : MonoBehaviour
{
    [Header("Tab Bar")]
    [SerializeField] RectTransform _tabBarContent;
    [SerializeField] GameObject    _tabButtonPrefab;

    [Header("Content Panels (Gameplay / Keys / Graphics / Audio / Account)")]
    [SerializeField] GameObject _gameplayPanel;
    [SerializeField] GameObject _keysPanel;
    [SerializeField] GameObject _graphicsPanel;
    [SerializeField] GameObject _audioPanel;
    [SerializeField] GameObject _accountPanel;

    [Header("Description Card (右側)")]
    [SerializeField] TextMeshProUGUI _descTitleText;
    [SerializeField] TextMeshProUGUI _descBodyText;

    [Header("Navigation")]
    [SerializeField] Button _backButton;
    [SerializeField] Button _resetButton;
    [SerializeField] Button _saveButton;   // 保存ボタン(押下時に確定)。BuildConfigScene が配線。

    [Header("Input")]
    [SerializeField] InputActionAsset _inputAsset;

    // ── Tab definitions ───────────────────────────────────────────────────────

    /// <summary>コンフィグ画面のタブ種別を表す列挙型。</summary>
    public enum ConfigTab { Gameplay = 0, Keys = 1, Graphics = 2, Audio = 3, Account = 4 }

    static readonly (ConfigTab tab, string label, string descTitle, string descBody)[] Tabs =
    {
        (ConfigTab.Gameplay, "ゲームプレイ",   "ゲームプレイ",
            "ゲームプレイに関する\n一般的なオプションを設定できます。\n\nタイミング補正はアクティブな\nデバイスプロファイルに保存されます。"),
        (ConfigTab.Keys,     "キー設定",       "キー設定",
            "レーンキーの割り当てと\nゲームパッドの設定ができます。\n\nキー変更ボタンをクリックしてから、\n希望のキーを押してください。"),
        (ConfigTab.Graphics, "グラフィック",   "グラフィック",
            "画質およびグラフィック性能に\n関するオプションを設定できます。"),
        (ConfigTab.Audio,    "オーディオ",     "オーディオ",
            "音量およびサウンド出力に関する\nオプションを設定できます。"),
        (ConfigTab.Account,  "アカウント設定", "アカウント設定",
            "アカウント管理および\nローカルデータの管理機能を\n利用できます。"),
    };

    readonly List<TabButtonView>           _tabButtons = new List<TabButtonView>();
    Dictionary<ConfigTab, GameObject>      _panelMap;
    ConfigTab                              _currentTab = ConfigTab.Gameplay;

    // 入口は Title / SongSelect(F2) / PVPLobby(F2) の3箇所。ESC は呼び出し元へ戻る。
    SceneId          _returnScene      = SceneId.Title;
    ISceneParameters _returnParameters;

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
        // 遷移元(あれば)を控える。直接シーン起動やパラメータなし遷移は Title 扱い。
        var p = ParameterStore.GetPending<ConfigParameters>();
        ConfigTab initialTab = ConfigTab.Gameplay;
        if (p != null)
        {
            _returnScene      = p.ReturnScene;
            _returnParameters = p.ReturnParameters;
            if (!string.IsNullOrEmpty(p.InitialTab) &&
                System.Enum.TryParse<ConfigTab>(p.InitialTab, ignoreCase: true, out var t))
                initialTab = t;
        }

        _panelMap = new Dictionary<ConfigTab, GameObject>
        {
            { ConfigTab.Gameplay, _gameplayPanel },
            { ConfigTab.Keys,     _keysPanel     },
            { ConfigTab.Graphics, _graphicsPanel },
            { ConfigTab.Audio,    _audioPanel    },
            { ConfigTab.Account,  _accountPanel  },
        };

        JacketBackgroundController.Instance?.SetFallback();
        if (_backButton  != null) _backButton.onClick.AddListener(OnBack);
        if (_resetButton != null) _resetButton.onClick.AddListener(ConfirmResetAll);
        if (_saveButton  != null)
        {
            _saveButton.onClick.AddListener(OnSave);
            // 設定は「保存(F5)」で確定し、未保存のまま離脱すると破棄される。保存しないと次回起動に
            // 反映されないため、保存ボタンを紫アクセント+「保存 (F5)」表記で目立たせる。
            var saveImg = _saveButton.GetComponent<UnityEngine.UI.Image>();
            if (saveImg != null) saveImg.color = new Color(0.42f, 0.32f, 0.88f, 1f);
            var saveLbl = _saveButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (saveLbl != null) saveLbl.text = "保存 (F5)";
        }
        BuildTabBar();
        SwitchTab(initialTab);
        TakeGlobalSnapshot();   // 入場時の確定済みベースライン(保存ボタン押下まで巻き戻し対象)

        RhythmGame.UI.Common.ShortcutHintOverlay.Set(
            "L/R Shift・←→: タブ切替   ↑↓: 項目   Space: 決定   保存(F5)で確定※未保存で離脱すると破棄   ESC: 閉じる");
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
            view.Button.onClick.AddListener(() => TrySwitchTab((ConfigTab)idx));
            _tabButtons.Add(view);
        }
    }

    /// <summary>
    /// タブを切り替える。保存モデルがグローバル(保存ボタンで確定/離脱で破棄確認)になったため、
    /// タブ切替では保存確認を挟まずそのまま切り替える(変更は全タブ通して暫定、確定は保存ボタン)。
    /// </summary>
    public void TrySwitchTab(ConfigTab target)
    {
        if (target == _currentTab) return;
        if (RhythmGame.UI.Common.SaveChangesDialog.IsOpen) return;
        SwitchTab(target);
    }

    /// <summary>指定タブに切り替え、ボタンの選択状態・パネル表示・説明カードを更新し、スナップショットを取り直す。</summary>
    public void SwitchTab(ConfigTab tab)
    {
        _currentTab = tab;

        for (int i = 0; i < _tabButtons.Count; i++)
            _tabButtons[i].SetSelected(i == (int)tab);

        foreach (var kvp in _panelMap)
            if (kvp.Value != null) kvp.Value.SetActive(kvp.Key == tab);

        if (_descTitleText != null) _descTitleText.text = Tabs[(int)tab].descTitle;
        if (_descBodyText  != null) _descBodyText.text  = Tabs[(int)tab].descBody;

        FocusFirstSelectable(tab);
        // スナップショットはグローバル(入場時+保存時)で取るので、タブ切替では取り直さない。
    }

    /// <summary>タブ名(文字列)でタブを切り替える。子コントローラーからのタブ間遷移に使う。</summary>
    public void SwitchToTab(string tabName)
    {
        if (System.Enum.TryParse<ConfigTab>(tabName, ignoreCase: true, out var tab))
            TrySwitchTab(tab);
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    void OnNavigate(InputAction.CallbackContext ctx)
    {
        if (RhythmGame.UI.Common.ConfirmDialog.IsOpen) return;
        if (RhythmGame.UI.Common.SaveChangesDialog.IsOpen) return;
        var v = ctx.ReadValue<Vector2>();

        // 横方向はタブ切替。ただしスライダー等「←→で値が変わる項目」を選択中はそちらに譲る。
        if (Mathf.Abs(v.x) > 0.5f && Mathf.Abs(v.x) > Mathf.Abs(v.y))
        {
            if (FocusedConsumesHorizontal()) return;
            if (v.x > 0f) StepTab(+1);
            else          StepTab(-1);
        }
        // 縦方向(項目フォーカス移動)は EventSystem の Selectable ナビゲーションに任せる。
    }

    void StepTab(int dir)
    {
        TrySwitchTab((ConfigTab)(((int)_currentTab + dir + Tabs.Length) % Tabs.Length));
    }

    // 現在フォーカス中の UI が横入力を消費する(Slider 等)かどうか。
    static bool FocusedConsumesHorizontal()
    {
        var go = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (go == null) return false;
        return go.GetComponent<Slider>() != null || go.GetComponent<Scrollbar>() != null;
    }

    void Update()
    {
        if (RhythmGame.UI.Common.ConfirmDialog.IsOpen) return;
        if (RhythmGame.UI.Common.SaveChangesDialog.IsOpen) return;

        var kb = Keyboard.current;
        if (kb != null)
        {
            // モック準拠: L Shift / R Shift でタブ切替 (既存 Tab/Q/E も維持)
            if (kb.rightShiftKey.wasPressedThisFrame) StepTab(+1);
            if (kb.leftShiftKey.wasPressedThisFrame)  StepTab(-1);
            if (kb.tabKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame) StepTab(+1);
            if (kb.qKey.wasPressedThisFrame) StepTab(-1);

            // F5: 設定を保存(確定) / F9: 全設定リセット (確認ダイアログ)
            if (kb.f5Key.wasPressedThisFrame) OnSave();
            if (kb.f9Key.wasPressedThisFrame) ConfirmResetAll();
        }

        var pad = Gamepad.current;
        if (pad != null)
        {
            if (RhythmGame.Input.GamepadLayout.NextTabPressed(pad)) StepTab(+1);
            if (RhythmGame.Input.GamepadLayout.PrevTabPressed(pad)) StepTab(-1);
        }
    }

    // タブ切替時、新パネル内の最初の操作可能項目へフォーカスを移す(キーボード/パッドで項目操作可に)。
    void FocusFirstSelectable(ConfigTab tab)
    {
        if (EventSystem.current == null) return;
        if (!_panelMap.TryGetValue(tab, out var panel) || panel == null) return;
        var sel = panel.GetComponentInChildren<Selectable>(false);
        EventSystem.current.SetSelectedGameObject(sel != null ? sel.gameObject : null);
    }

    void OnCancel(InputAction.CallbackContext ctx)
    {
        if (RhythmGame.UI.Common.ConfirmDialog.IsOpen) return;
        if (RhythmGame.UI.Common.SaveChangesDialog.IsOpen) return;
        OnBack();
    }

    // 閉じる(ESC/ボタン)。保存していない変更があれば「保存して退出/破棄して退出/キャンセル」を確認。
    void OnBack()
    {
        if (RhythmGame.UI.Common.SaveChangesDialog.IsOpen) return;
        if (!IsAnyDirty()) { LeaveScene(); return; }

        RhythmGame.UI.Common.SaveChangesDialog.Show(
            "保存していない変更があります。",
            "「破棄して退出」すると変更は元に戻ります。",
            onApply:   () => { TakeGlobalSnapshot(); LeaveScene(); },   // 保存して退出(即時保存済を確定)
            onDiscard: RevertAllAndLeave,                                // 破棄して退出(全タブ巻き戻し)
            onCancel:  null);                                            // 留まる
    }

    // ── 保存ボタン: 現在値を確定(ベースラインを更新)し、フィードバック表示 ──
    void OnSave()
    {
        TakeGlobalSnapshot();
        RhythmGame.UI.Common.ShortcutHintOverlay.Set("設定を保存しました ✓");
    }

    void LeaveScene()
    {
        if (SceneRouter.Instance != null)
            SceneRouter.Instance.GoTo(_returnScene, _returnParameters);
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene("Title");
    }

    // ── 未保存変更の検出 / 巻き戻し (スナップショット方式) ─────────────────────
    //
    // 各設定は変更と同時に PlayerPrefs / DeviceProfile へ即時保存される(プレビューを兼ねる)。
    // タブ入場時に対象キーのスナップショットを取り、タブ移動/クローズ時に差分があれば
    // 「キャンセル / 保存しない / 適用」を確認する。
    //   適用       = 現在値を確定(即時保存済なので何もしない)
    //   保存しない = スナップショット値へ巻き戻し(必要なら画面を再読込して UI も戻す)
    //   キャンセル = その場に留まる

    enum PrefType { Int, Float, String }

    static readonly Dictionary<ConfigTab, (string key, PrefType type)[]> TabPrefKeys =
        new Dictionary<ConfigTab, (string, PrefType)[]>
    {
        { ConfigTab.Gameplay, new[]
            {
                ("HiSpeed", PrefType.Float), ("ComboBorderIdx", PrefType.Int), ("ShowFastLate", PrefType.Int),
                ("BgEffectsIntensity", PrefType.Float), ("JudgmentEffectStyleIdx", PrefType.Int),
            } },
        { ConfigTab.Keys, new[]
            {
                ("ControllerEnabled", PrefType.Int), ("GamepadLayout", PrefType.Int),
                ("InputBindings_Gameplay", PrefType.String),
            } },
        { ConfigTab.Graphics, new[]
            {
                ("ResolutionIdx", PrefType.Int), ("ScreenModeIdx", PrefType.Int), ("FpsLimitIdx", PrefType.Int),
                ("VSync", PrefType.Int), ("CameraAngleIdx", PrefType.Int), ("BloomLevelIdx", PrefType.Int),
                ("MotionEffects", PrefType.Int), ("ShowFps", PrefType.Int),
            } },
        { ConfigTab.Audio, new[]
            {
                ("Vol_Master", PrefType.Float), ("Vol_Music", PrefType.Float), ("Vol_Sfx", PrefType.Float),
                ("MuteOnFocusLoss", PrefType.Int),
            } },
        { ConfigTab.Account, new[]
            {
                ("DisplayName", PrefType.String), ("StatusMessage", PrefType.String),
                ("NotificationsEnabled", PrefType.Int),
            } },
    };

    // key → 値の文字列表現 (キー不存在は null)。型は TabPrefKeys 参照。
    readonly Dictionary<string, string> _snapshot = new Dictionary<string, string>();
    // ゲームプレイタブのみ: アクティブプロファイルのオフセットも対象
    bool _snapshotHasOffsets;
    int  _snapshotJudgmentMs, _snapshotVisualMs;

    static string ReadPref(string key, PrefType type)
    {
        if (!PlayerPrefs.HasKey(key)) return null;
        switch (type)
        {
            case PrefType.Int:    return PlayerPrefs.GetInt(key).ToString();
            case PrefType.Float:  return PlayerPrefs.GetFloat(key).ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            default:              return PlayerPrefs.GetString(key);
        }
    }

    // 全タブのスナップショットを取る(入場時+保存時)。_snapshot は pref key で一意なので全タブ同居可。
    void TakeGlobalSnapshot()
    {
        _snapshot.Clear();
        foreach (var kv in TabPrefKeys)
            foreach (var (key, type) in kv.Value)
                _snapshot[key] = ReadPref(key, type);

        _snapshotHasOffsets = false;
        var prof = RepositoryService.Instance?.ActiveProfile;
        if (prof != null)
        {
            _snapshotHasOffsets = true;
            _snapshotJudgmentMs = prof.Offsets.JudgmentOffsetMs;
            _snapshotVisualMs   = prof.Offsets.VisualOffsetMs;
        }
    }

    // 全タブのどれかに未保存(スナップショット比)の変更があるか。
    bool IsAnyDirty()
    {
        foreach (var kv in TabPrefKeys)
            foreach (var (key, type) in kv.Value)
            {
                _snapshot.TryGetValue(key, out var old);
                if (ReadPref(key, type) != old) return true;
            }

        if (_snapshotHasOffsets)
        {
            var prof = RepositoryService.Instance?.ActiveProfile;
            if (prof != null &&
                (prof.Offsets.JudgmentOffsetMs != _snapshotJudgmentMs ||
                 prof.Offsets.VisualOffsetMs   != _snapshotVisualMs))
                return true;
        }
        return false;
    }

    // 全タブをスナップショットへ巻き戻し(PlayerPrefs+ランタイム+オフセット)、退出する。
    // 退出するので UI 巻き戻し(シーン再読込)は不要。
    async void RevertAllAndLeave()
    {
        // 1. PlayerPrefs を全タブ書き戻す
        foreach (var kv in TabPrefKeys)
            foreach (var (key, type) in kv.Value)
            {
                _snapshot.TryGetValue(key, out var old);
                if (old == null) { PlayerPrefs.DeleteKey(key); continue; }
                switch (type)
                {
                    case PrefType.Int:    PlayerPrefs.SetInt(key, int.Parse(old)); break;
                    case PrefType.Float:  PlayerPrefs.SetFloat(key, float.Parse(old, System.Globalization.CultureInfo.InvariantCulture)); break;
                    default:              PlayerPrefs.SetString(key, old); break;
                }
            }
        PlayerPrefs.Save();

        // 2. 即時反映済みのランタイム状態を全タブ分巻き戻す
        ApplyRuntimeRevertAll();

        // 3. オフセット(DeviceProfile)を書き戻す
        await RevertOffsetsAsync();

        // 4. 退出
        LeaveScene();
    }

    // 全タブのランタイム状態を(巻き戻し済み PlayerPrefs から)再適用する。
    void ApplyRuntimeRevertAll()
    {
        DisplayTabController.ApplySettingsOnBoot();   // Graphics
        AudioVolumeBinder.Instance?.SetMasterVolume(PlayerPrefs.GetFloat("Vol_Master", 80f));
        AudioVolumeBinder.Instance?.SetMusicVolume(PlayerPrefs.GetFloat("Vol_Music", 90f));
        AudioVolumeBinder.Instance?.SetSfxVolume(PlayerPrefs.GetFloat("Vol_Sfx", 70f));
        float bg = PlayerPrefs.GetFloat("BgEffectsIntensity", 100f);   // Gameplay
        JacketBackgroundController.Instance?.SetBrightness((bg / 100f) * 0.5f);
        BeatGridController.Instance?.SetUserIntensity(bg / 100f);
        var map = _inputAsset != null ? _inputAsset.FindActionMap("Gameplay") : null;   // Keys
        if (map != null)
        {
            map.RemoveAllBindingOverrides();
            InputTabController.LoadBindingsFromPrefs(_inputAsset);
        }
    }

    async System.Threading.Tasks.Task RevertOffsetsAsync()
    {
        if (!_snapshotHasOffsets) return;
        var repo = RepositoryService.Instance;
        var prof = repo?.ActiveProfile;
        if (repo == null || prof == null) return;
        if (prof.Offsets.JudgmentOffsetMs == _snapshotJudgmentMs &&
            prof.Offsets.VisualOffsetMs   == _snapshotVisualMs) return;

        var reverted = new DeviceProfile
        {
            ProfileId           = prof.ProfileId,
            DisplayName         = prof.DisplayName,
            OsDeviceName        = prof.OsDeviceName,
            IsAutoSwitchEnabled = prof.IsAutoSwitchEnabled,
            Offsets = new AppOffsetSettings
            {
                JudgmentOffsetMs = _snapshotJudgmentMs,
                VisualOffsetMs   = _snapshotVisualMs,
            },
            CreatedAtUnixMs = prof.CreatedAtUnixMs,
            UpdatedAtUnixMs = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };
        await repo.Offsets.SaveProfileAsync(reverted);
        await repo.SetActiveProfileAsync(reverted.ProfileId);
    }

    // ── Reset (F9) ────────────────────────────────────────────────────────────

    // リセット対象の PlayerPrefs キー(コンフィグ画面で扱う設定のみ。
    // オフセットは DeviceProfile(DB) 管理なので対象外 = キャリブレーションでやり直す)。
    static readonly string[] ResetKeys =
    {
        "HiSpeed", "ComboBorderIdx", "ShowFastLate", "BgEffectsIntensity", "JudgmentEffectStyleIdx",
        "ResolutionIdx", "ScreenModeIdx", "FpsLimitIdx", "VSync",
        "CameraAngleIdx", "BloomLevelIdx", "MotionEffects", "ShowFps",
        "Vol_Master", "Vol_Music", "Vol_Sfx", "MuteOnFocusLoss",
        "ControllerEnabled", "GamepadLayout", "InputBindings_Gameplay",
        "NotificationsEnabled",
    };

    void ConfirmResetAll()
    {
        RhythmGame.UI.Common.ConfirmDialog.Show(
            "すべての設定を初期値に戻しますか?\n(キーバインド・音量・画質設定など。タイミング補正は対象外)",
            "リセット", "キャンセル",
            onConfirm: ResetAll);
    }

    void ResetAll()
    {
        foreach (var key in ResetKeys) PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();

        // 表示設定は即時反映(他はシーン再読込で各タブが既定値を読み直す)
        DisplayTabController.ApplySettingsOnBoot();

        // Config を開き直して全タブを既定値で再構築(呼び出し元情報・現在タブは引き継ぐ)
        SceneRouter.Instance?.GoTo(SceneId.Config, new ConfigParameters
        {
            ReturnScene      = _returnScene,
            ReturnParameters = _returnParameters,
            InitialTab       = _currentTab.ToString(),
        });
    }
}

// ── Tab button view helper ────────────────────────────────────────────────────

/// <summary>
/// タブボタン1つの表示状態(選択中 / 非選択)を管理するビューヘルパークラス。
/// </summary>
public class TabButtonView
{
    /// <summary>このタブボタンのルート GameObject。</summary>
    public GameObject Root   { get; }
    /// <summary>クリック用ボタン。</summary>
    public Button     Button { get; }

    Image            _bg;
    TextMeshProUGUI  _label;

    static readonly Color ColIdle     = new Color(1f, 1f, 1f, 0.06f);
    static readonly Color ColSelected = new Color(0.42f, 0.32f, 0.85f, 0.95f);   // モックの紫アクセントをダーク変換

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
