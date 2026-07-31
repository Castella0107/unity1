using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// コンフィグ画面全体を管理するコントローラー (モック適用 2026-06-07 再編)。
/// 6タブ構成: ゲームプレイ / キー設定 / グラフィック / 色 / オーディオ / アカウント設定。
/// タブバー構築・右側説明カード・F9リセット・ESC=呼び出し元復帰と、2階層フォーカス
/// (タブレベル: ←→/Tab/Q E/L R Shift/LB RB=タブ切替・↓=項目へ / 項目レベル: ↑↓=行移動・←→=値変更または行内移動・最上行で↑=タブへ) を担当する。
/// </summary>
public class ConfigController : MonoBehaviour
{
    [Header("Tab Bar")]
    [SerializeField] RectTransform _tabBarContent;
    [SerializeField] GameObject    _tabButtonPrefab;

    [Header("Content Panels (Gameplay / Keys / Graphics / Colors / Audio / Account)")]
    [SerializeField] GameObject _gameplayPanel;
    [SerializeField] GameObject _keysPanel;
    [SerializeField] GameObject _graphicsPanel;
    [SerializeField] GameObject _colorsPanel;
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
    public enum ConfigTab { Gameplay = 0, Keys = 1, Graphics = 2, Colors = 3, Audio = 4, Account = 5 }

    static readonly (ConfigTab tab, string label, string descTitle, string descBody)[] Tabs =
    {
        (ConfigTab.Gameplay, "ゲームプレイ",   "ゲームプレイ",
            "ゲームプレイに関する\n一般的なオプションを設定できます。\n\nタイミング補正はアクティブな\nデバイスプロファイルに保存されます。"),
        (ConfigTab.Keys,     "キー設定",       "キー設定",
            "レーンキーの割り当てと\nゲームパッドの設定ができます。\n\nキー変更ボタンをクリックしてから、\n希望のキーを押してください。"),
        (ConfigTab.Graphics, "グラフィック",   "グラフィック",
            "画質およびグラフィック性能に\n関するオプションを設定できます。"),
        (ConfigTab.Colors,   "色",             "色",
            "ノーツ(レーン別)・レーン仕切り線・\n判定線の色を RGB で設定できます。\n\nノーツの色は各レーンごとに\n個別に変更できます。"),
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

    // ── 2階層フォーカス (タブレベル / 項目レベル) ─────────────────────────────
    //
    // タブレベル: ←→=タブ切替 / ↓=項目レベルへ。EventSystem の選択を空にしておき、
    //             ←→ がスライダー等の項目に奪われないようにする(Colors タブで右に進めなくなる不具合の解消)。
    // 項目レベル: ↑↓=項目移動(現在項目の行をタブと同じ紫でハイライト) / ←→=値変更 / 最上項目で↑=タブレベルへ。
    //             項目の Selectable ナビゲーションは None にして、移動は本クラスが管理する
    //             (Slider の←→値変更は Slider.OnMove が Navigation 非依存で処理するためそのまま生きる)。

    /// <summary>タブボタンの選択色と同一 (TabButtonView.ColSelected)。項目ハイライトも同じ見せ方に揃える。</summary>
    static readonly Color ItemHighlightCol = new Color(0.42f, 0.32f, 0.85f, 0.95f);

    bool _itemLevel;                                        // false=タブレベル / true=項目レベル
    readonly List<Selectable> _items = new List<Selectable>();  // 現在パネルの操作可能項目(表示順)
    readonly List<int> _itemRows = new List<int>();         // 各項目の行番号 (パネル直下コンテナ単位)
    int        _rowCount;
    int        _itemIndex;
    Image      _highlightImg;                               // 紫ハイライト中の行背景
    Color      _highlightPrev;                              // ハイライト解除時の復元色
    GameObject _lastSelected;                               // マウスクリック等での選択変化の同期用

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
            { ConfigTab.Colors,   _colorsPanel   },
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

        UpdateHint();
    }

    // 階層に応じたショートカットヒント (タブレベル / 項目レベル)。
    void UpdateHint()
    {
        RhythmGame.UI.Common.ShortcutHintOverlay.Set(_itemLevel
            ? "↑↓: 行移動(最上で↑: タブへ)   ←→: 値変更・行内移動   Space: 決定・入力開始   F5: 保存   ESC: 閉じる"
            : "←→・L/R Shift: タブ切替   ↓: 項目へ   保存(F5)で確定※未保存で離脱すると破棄   ESC: 閉じる");
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
            // タブ移動は ←→(OnNavigate) が担うため、EventSystem のナビゲーションは切る
            var nav = view.Button.navigation; nav.mode = Navigation.Mode.None; view.Button.navigation = nav;
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

        // 2階層フォーカス: タブレベル中は選択を空のまま(←→をタブ切替専用に)、
        // 項目レベル中のタブ切替は新パネルの先頭項目へフォーカスを引き継ぐ。
        ClearItemHighlight();
        if (_itemLevel) EnterItemLevel();
        else if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
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
        if (RhythmGame.UI.Common.ClickToEditValue.IsEditing) return;   // 数値直接入力中
        var v = ctx.ReadValue<Vector2>();
        bool horizontal = Mathf.Abs(v.x) > 0.5f && Mathf.Abs(v.x) > Mathf.Abs(v.y);
        bool vertical   = Mathf.Abs(v.y) > 0.5f && Mathf.Abs(v.y) >= Mathf.Abs(v.x);

        // タブレベル: ←→=タブ切替(項目には渡さない) / ↓=項目レベルへ。
        if (!_itemLevel)
        {
            if      (horizontal)          StepTab(v.x > 0f ? +1 : -1);
            else if (vertical && v.y < 0f) EnterItemLevel();
            return;
        }

        // 項目レベル
        var cur = (_itemIndex >= 0 && _itemIndex < _items.Count) ? _items[_itemIndex] : null;
        if (cur == null) { ExitItemLevel(); return; }

        // ドロップダウン展開中など、選択が現在項目以外(リスト内トグル等)にある間は EventSystem に任せる。
        var selGO = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selGO != cur.gameObject) return;

        // 文字入力中はカーソル移動を奪わない。
        var input = cur as TMP_InputField;
        if (input != null && input.isFocused) return;

        if (vertical)
        {
            if (v.y > 0f && _itemRows[_itemIndex] == 0) { ExitItemLevel(); return; }   // 最上行で↑=タブレベルへ
            MoveRow(v.y > 0f ? -1 : +1);
        }
        else if (horizontal)
        {
            // 同一行に複数項目がある場合 (キー設定の 6 キー等) は ←→ = 行内移動。
            // Slider は行構成に関わらず ←→ = 値変更 (Slider.OnMove が処理)。
            bool multiItemRow = !(cur is Slider) &&
                ((_itemIndex > 0 && _itemRows[_itemIndex - 1] == _itemRows[_itemIndex]) ||
                 (_itemIndex + 1 < _items.Count && _itemRows[_itemIndex + 1] == _itemRows[_itemIndex]));
            if (multiItemRow) MoveInRow(v.x > 0f ? +1 : -1);
            else              StepItemValue(cur, v.x > 0f ? +1 : -1);
        }
    }

    void StepTab(int dir)
    {
        TrySwitchTab((ConfigTab)(((int)_currentTab + dir + Tabs.Length) % Tabs.Length));
    }

    // ── 項目レベルの管理 ──────────────────────────────────────────────────────

    /// <summary>項目レベルへ降りる。項目が無いタブではタブレベルに留まる。</summary>
    void EnterItemLevel()
    {
        BuildItems();
        if (_items.Count == 0) { ExitItemLevel(); return; }
        _itemLevel = true;
        SelectItem(0);   // ↓で降りたとき・タブ切替の引き継ぎは常に最上項目から
        UpdateHint();
    }

    /// <summary>タブレベルへ戻る(選択を空にして←→をタブ切替に返す)。</summary>
    void ExitItemLevel()
    {
        _itemLevel = false;
        _itemIndex = 0;
        ClearItemHighlight();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        UpdateHint();
    }

    /// <summary>↑↓: 行単位で移動し、移動先の行の先頭項目を選択する。</summary>
    void MoveRow(int dir)
    {
        if (_items.Count == 0) { ExitItemLevel(); return; }
        int row = Mathf.Clamp(_itemRows[_itemIndex] + dir, 0, _rowCount - 1);
        if (row == _itemRows[_itemIndex]) return;
        SelectItem(_itemRows.IndexOf(row));
    }

    /// <summary>←→: 同一行内で隣の項目へ移動する (行端では留まる)。</summary>
    void MoveInRow(int dir)
    {
        int target = _itemIndex + dir;
        if (target < 0 || target >= _items.Count) return;
        if (_itemRows[target] != _itemRows[_itemIndex]) return;
        SelectItem(target);
    }

    /// <summary>Space: スライダーを選択中で同一行に複数項目がある場合、行内の次の項目へ進む
    /// (行末なら行頭へ循環)。色タブの R→G→B を Space で順に編集するための動線。</summary>
    void AdvanceInRowOnSubmit()
    {
        if (!_itemLevel || _itemIndex < 0 || _itemIndex >= _items.Count) return;
        var cur = _items[_itemIndex];
        if (cur == null) return;

        // 入力欄は Space/Enter で編集開始 (選択しただけでは開始しない — BuildItems 参照)。
        // 編集中は Enter/Esc で TMP 側が終了し、↑↓ ナビゲーションが戻る。
        if (cur is TMP_InputField tif)
        {
            if (!tif.isFocused) tif.ActivateInputField();
            return;
        }

        if (!(cur is Slider)) return;
        var selGO = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selGO != cur.gameObject) return;   // ドロップダウン展開中などは奪わない

        int row  = _itemRows[_itemIndex];
        int next = _itemIndex + 1;
        if (next >= _items.Count || _itemRows[next] != row)
        {
            int first = _itemRows.IndexOf(row);
            if (first == _itemIndex) return;   // 行にスライダー 1 個だけなら何もしない
            next = first;                      // 行末 → 行頭へ循環
        }
        SelectItem(next);
    }

    void SelectItem(int idx)
    {
        if (idx < 0 || idx >= _items.Count) return;
        var sel = _items[idx];
        if (sel == null) { BuildItems(); if (_items.Count == 0) { ExitItemLevel(); return; } idx = Mathf.Clamp(idx, 0, _items.Count - 1); sel = _items[idx]; }
        _itemIndex = idx;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(sel.gameObject);
        _lastSelected = sel.gameObject;
        HighlightRow(sel);
    }

    // 現在パネルの操作可能項目を表示順に列挙し、Selectable ナビゲーションを None にする
    // (↑↓/←→ の解釈は本クラスが階層で切り替えるため。Slider の←→値変更は OnMove が処理するので影響なし)。
    void BuildItems()
    {
        _items.Clear();
        _itemRows.Clear();
        _rowCount = 0;
        if (!_panelMap.TryGetValue(_currentTab, out var panel) || panel == null) return;

        Transform lastContainer = null;
        foreach (var s in panel.GetComponentsInChildren<Selectable>(false))
        {
            var nav = s.navigation; nav.mode = Navigation.Mode.None; s.navigation = nav;

            // 入力欄は「選択された瞬間に編集開始」しないようにする。
            // 既定の TMP_InputField は OnSelect で ActivateInputField() まで行うため、
            // ↓でハイライトが乗っただけで矢印キーがカーソル移動に吸われ、
            // その行から下へ進めなくなっていた (K 報告 2026-07-31)。
            // 編集は Space/Enter で明示的に開始する。
            if (s is TMP_InputField tif) tif.shouldActivateOnSelect = false;

            if (!s.interactable || s is Scrollbar) continue;
            var n = s.gameObject.name;
            // スライダー横の◁▷ステッパーと色見本(マウス専用)は↑↓の停留対象にしない
            if (n.StartsWith("Step") || n == "Swatch") continue;
            // 行 = パネル直下の祖先コンテナ (Row_/CRow_/LaneBlock 等)。同一コンテナ内の
            // 項目は 1 行として扱う (キー設定の 6 キーが 1 行になる等)。
            var container = s.transform;
            while (container.parent != null && container.parent.gameObject != panel)
                container = container.parent;
            if (container != lastContainer) { _rowCount++; lastContainer = container; }
            _items.Add(s);
            _itemRows.Add(_rowCount - 1);
        }
    }

    /// <summary>現在項目の行背景を、タブ選択と同じ紫でハイライトする。</summary>
    void HighlightRow(Selectable sel)
    {
        ClearItemHighlight();
        var img = FindRowImage(sel.transform);
        if (img == null) return;
        _highlightImg  = img;
        _highlightPrev = img.color;
        img.color = ItemHighlightCol;
    }

    void ClearItemHighlight()
    {
        if (_highlightImg != null) _highlightImg.color = _highlightPrev;
        _highlightImg = null;
    }

    // 項目の属する行(Row_/CRow_)の背景 Image。行コンテナが無い項目は自身の Image を返す。
    Image FindRowImage(Transform t)
    {
        _panelMap.TryGetValue(_currentTab, out var panel);
        for (var p = t; p != null && (panel == null || p.gameObject != panel); p = p.parent)
            if (p.name.StartsWith("Row_") || p.name.StartsWith("CRow_"))
                return p.GetComponent<Image>();
        return t.GetComponent<Image>();
    }

    // 項目レベルの ←→: 値変更。Slider は EventSystem の Move(Slider.OnMove)が値を動かすため何もしない。
    static void StepItemValue(Selectable sel, int dir)
    {
        if (sel is Slider) return;

        var tgl = sel as Toggle;
        if (tgl != null) { tgl.isOn = !tgl.isOn; return; }

        var dd = sel as TMP_Dropdown;
        if (dd != null && dd.options.Count > 0)
        {
            dd.value = (dd.value + dir + dd.options.Count) % dd.options.Count;
            dd.RefreshShownValue();
        }
        // Button / InputField は ←→ での値変更なし (Space/Enter で決定)
    }

    // マウスクリック等で EventSystem の選択が変わった場合に、2階層状態(レベル・項目番号・ハイライト)を同期する。
    void SyncSelectionState()
    {
        var selGO = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selGO == _lastSelected) return;
        _lastSelected = selGO;
        if (selGO == null) return;
        if (!_panelMap.TryGetValue(_currentTab, out var panel) || panel == null) return;
        if (!selGO.transform.IsChildOf(panel.transform)) return;   // タブボタン・フッター・ダイアログは対象外
        var sel = selGO.GetComponent<Selectable>();
        if (sel == null) return;

        if (!_itemLevel)
        {
            BuildItems();
            int i = _items.IndexOf(sel);
            if (i >= 0) { _itemLevel = true; SelectItem(i); UpdateHint(); }
        }
        else
        {
            int i = _items.IndexOf(sel);
            if (i >= 0) SelectItem(i);
        }
    }

    void Update()
    {
        if (RhythmGame.UI.Common.ConfirmDialog.IsOpen) return;
        if (RhythmGame.UI.Common.SaveChangesDialog.IsOpen) return;
        if (RhythmGame.UI.Common.ClickToEditValue.IsEditing) return;   // 数値直接入力中 (Tab/Q/E/F5 等を止める)

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

            // Space: 同一行に複数項目がありスライダーを選択中なら「決定して行内の次へ」
            // (色タブの R→G→B→R 循環。←→は値変更に使うため行内移動は Space が担う)。
            // ボタン/トグル等は従来どおり UI モジュールの Submit が処理する。
            if (kb.spaceKey.wasPressedThisFrame) AdvanceInRowOnSubmit();
        }

        var pad = Gamepad.current;
        if (pad != null)
        {
            if (RhythmGame.Input.GamepadLayout.NextTabPressed(pad)) StepTab(+1);
            if (RhythmGame.Input.GamepadLayout.PrevTabPressed(pad)) StepTab(-1);
        }

        SyncSelectionState();
    }

    void OnCancel(InputAction.CallbackContext ctx)
    {
        if (RhythmGame.UI.Common.ConfirmDialog.IsOpen) return;
        if (RhythmGame.UI.Common.SaveChangesDialog.IsOpen) return;
        if (RhythmGame.UI.Common.ClickToEditValue.IsEditing) return;   // ESC は入力キャンセルに使う
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
                ("HiSpeed", PrefType.Float), ("LaneLength", PrefType.Float),
                ("ComboBorderIdx", PrefType.Int), ("ShowFastLate", PrefType.Int),
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
        { ConfigTab.Colors, new[]
            {
                ("NoteColor0", PrefType.String), ("NoteColor1", PrefType.String), ("NoteColor2", PrefType.String),
                ("NoteColor3", PrefType.String), ("NoteColor4", PrefType.String), ("NoteColor5", PrefType.String),
                ("DividerColor", PrefType.String), ("JudgmentLineColor", PrefType.String),
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
        "HiSpeed", "ComboBorderIdx", "ShowFastLate", "ComboShow", "ComboPosIdx",
        "BgEffectsIntensity", "JudgmentEffectStyleIdx", "NoteSoundIdx",
        "ResolutionIdx", "ScreenModeIdx", "FpsLimitIdx", "VSync",
        "CameraAngleIdx", "BloomLevelIdx", "MotionEffects", "ShowFps",
        "Vol_Master", "Vol_Music", "Vol_Sfx", "MuteOnFocusLoss",
        "ControllerEnabled", "GamepadLayout", "InputBindings_Gameplay",
        "NotificationsEnabled",
        "NoteColor0", "NoteColor1", "NoteColor2", "NoteColor3", "NoteColor4", "NoteColor5",
        "DividerColor", "JudgmentLineColor", "ChordColor",
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
