using System.Collections;
using System.Threading.Tasks;
using RhythmGame.Network;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// タイトル画面コントローラー (ユーザー提供モック準拠の縦メニュー形式、2026-06-07 リデザイン)。
/// レイアウト: 左上=タイトルロゴ / 左下=縦メニュー(選択中は拡大+エンブレム+説明文) / 右上=プレイヤーチップ。
/// 背景は BGA 未制作のため真っ暗(プレースホルダー)。
/// ↑↓(W/S)で項目移動、Space/Enter で決定、ESC で終了確認。
/// </summary>
public class TitleController : MonoBehaviour
{
    [Header("Menu (baked-in 5 items)")]
    [SerializeField] MenuOutlineLabel[] _itemLabels;   // 項目ラベル(中抜きグラデ文字)
    [SerializeField] TextMeshProUGUI[]  _itemDescs;    // 選択中のみ表示する説明文
    [SerializeField] GameObject[]       _itemIcons;    // 選択中エンブレム(◉+✦)
    [SerializeField] GameObject[]       _itemDots;     // 非選択の小ドット

    [Header("History 子メニュー (Ladder Match / Free Play)")]
    [SerializeField] GameObject         _childRoot;    // 子ボタン列のルート(初期非表示)
    [SerializeField] MenuOutlineLabel[] _childLabels;
    [SerializeField] TextMeshProUGUI[]  _childDescs;

    [Header("Player Chip (右上)")]
    [SerializeField] TextMeshProUGUI _playerNameText;
    [SerializeField] TextMeshProUGUI _playerRatingText;

    [Header("Input")]
    [SerializeField] InputActionAsset _inputAsset;

    // ── Menu items ─────────────────────────────────────────────────────────
    /// <summary>タイトルメニューの項目種別を表す列挙型。</summary>
    private enum MenuId { FreePlay, Online, Config, History, Exit }

    private static readonly (MenuId id, string label, string desc)[] _menus =
    {
        (MenuId.FreePlay, "Free Play", "ソロでお好きな曲をプレイするモードです。"),
        (MenuId.Online,   "Online",    "オンライン対戦(RANKED MATCH)のロビーへ進みます。"),
        (MenuId.Config,   "Config",    "ゲームの各種設定を変更します。"),
        (MenuId.History,  "History",   "戦績とリプレイを確認します。(→ で種類を選択)"),
        (MenuId.Exit,     "Exit",      "ゲームを終了します。"),
    };

    // History の子ボタン (モック準拠: 親の右に展開する子メニュー)。
    // mode は HistoryParameters.Mode に渡す ("Ladder" / "Free")。
    private static readonly (string label, string desc, string mode)[] _historyChildren =
    {
        ("Ladder Match", "ラダーマッチ(オンライン対戦)の履歴とリプレイを確認します。", "Ladder"),
        ("Free Play",    "フリープレイ(ソロ)のベスト記録とリプレイを確認します。",     "Free"),
    };

    const float SelectedFontSize   = 38f;
    const float UnselectedFontSize = 27f;

    // ── State ──────────────────────────────────────────────────────────────
    private int  _currentIndex;
    private bool _inSubmenu;
    private int  _childIndex;

    // ── Input Actions ──────────────────────────────────────────────────────
    private InputAction _navigateAction;
    private InputAction _submitAction;
    private InputAction _cancelAction;

    // ── Lifecycle ──────────────────────────────────────────────────────────

    private void Awake()
    {
        Application.runInBackground = true;

        var map = _inputAsset.FindActionMap("UI", throwIfNotFound: true);
        _navigateAction = map.FindAction("Navigate", throwIfNotFound: true);
        _submitAction   = map.FindAction("Submit",   throwIfNotFound: true);
        _cancelAction   = map.FindAction("Cancel",   throwIfNotFound: true);
    }

    private void OnEnable()
    {
        _navigateAction.Enable();
        _submitAction  .Enable();
        _cancelAction  .Enable();
        _navigateAction.performed += OnNavigate;
        _submitAction  .performed += OnSubmit;
        _cancelAction  .performed += OnCancel;
    }

    private void OnDisable()
    {
        _navigateAction.performed -= OnNavigate;
        _submitAction  .performed -= OnSubmit;
        _cancelAction  .performed -= OnCancel;
    }

    private void Start()
    {
        // BGA 未制作のため背景は真っ暗のまま (JacketBackground は出さない)
        JacketBackgroundController.Instance?.SetCanvasEnabled(false);

        // ラベル/説明文はコントローラーのデータが正本 (シーンは空テキストで焼かれる)
        for (int i = 0; i < _menus.Length; i++)
        {
            if (_itemLabels != null && i < _itemLabels.Length && _itemLabels[i] != null)
                _itemLabels[i].SetText(_menus[i].label);
            if (_itemDescs != null && i < _itemDescs.Length && _itemDescs[i] != null)
                _itemDescs[i].text = _menus[i].desc;
        }
        for (int i = 0; i < _historyChildren.Length; i++)
        {
            if (_childLabels != null && i < _childLabels.Length && _childLabels[i] != null)
                _childLabels[i].SetText(_historyChildren[i].label);
            if (_childDescs != null && i < _childDescs.Length && _childDescs[i] != null)
                _childDescs[i].text = _historyChildren[i].desc;
        }
        if (_childRoot != null) _childRoot.SetActive(false);

        if (_playerNameText != null) _playerNameText.text = LocalIdentity.UserId;
        if (_playerRatingText != null) _playerRatingText.text = "RATING ----";
        _ = LoadPlayerRatingAsync();

        _currentIndex = 0;
        RefreshSelection();

        RhythmGame.UI.Common.ShortcutHintOverlay.Set("↑↓: 項目   Space: 決定   ESC: 終了");
        StartCoroutine(PulseSelectedIcon());
    }

    async Task LoadPlayerRatingAsync()
    {
        var net = NetworkClient.Instance;
        if (net == null || _playerRatingText == null) return;
        var r = await net.FetchPvpUserStatsAsync(LocalIdentity.UserId);
        if (this == null || _playerRatingText == null) return;
        if (r.Ok && r.Body != null)
            _playerRatingText.text = $"RATING {r.Body.rating:F0}";
    }

    // ── Input callbacks ────────────────────────────────────────────────────

    private void OnNavigate(InputAction.CallbackContext ctx)
    {
        if (RhythmGame.UI.Common.ConfirmDialog.IsOpen) return;
        var v = ctx.ReadValue<Vector2>();

        if (_inSubmenu)
        {
            // 子メニュー内: ↑↓=子選択 / ←=親に戻る
            if      (v.y >  0.5f) MoveChild(-1);
            else if (v.y < -0.5f) MoveChild(+1);
            else if (v.x < -0.5f) CloseSubmenu();
            return;
        }

        if      (v.y >  0.5f) Move(-1);   // 上
        else if (v.y < -0.5f) Move(+1);   // 下
        else if (v.x >  0.5f && _menus[_currentIndex].id == MenuId.History)
            OpenSubmenu();                // → で子メニュー展開 (History のみ)
    }

    private void OnSubmit(InputAction.CallbackContext ctx)
    {
        if (RhythmGame.UI.Common.ConfirmDialog.IsOpen) return;
        if (_inSubmenu) { DecideChild(); return; }
        Decide();
    }

    private void OnCancel(InputAction.CallbackContext ctx)
    {
        if (RhythmGame.UI.Common.ConfirmDialog.IsOpen) return;
        if (_inSubmenu) { CloseSubmenu(); return; }
        ConfirmExit();
    }

    // ── Selection ──────────────────────────────────────────────────────────

    private void Move(int dir)
    {
        _currentIndex = (_currentIndex + dir + _menus.Length) % _menus.Length;
        RefreshSelection();
    }

    private void RefreshSelection()
    {
        for (int i = 0; i < _menus.Length; i++)
        {
            bool sel = i == _currentIndex;
            if (_itemLabels != null && i < _itemLabels.Length && _itemLabels[i] != null)
            {
                _itemLabels[i].SetFontSize(sel ? SelectedFontSize : UnselectedFontSize);
                _itemLabels[i].SetSelected(sel);
            }
            // 説明文は半透明黒帯(DescBg=親GO)ごと切り替える
            if (_itemDescs != null && i < _itemDescs.Length && _itemDescs[i] != null)
                _itemDescs[i].transform.parent.gameObject.SetActive(sel && !_inSubmenu);
            if (_itemIcons != null && i < _itemIcons.Length && _itemIcons[i] != null)
                _itemIcons[i].SetActive(sel);
            if (_itemDots != null && i < _itemDots.Length && _itemDots[i] != null)
                _itemDots[i].SetActive(!sel);
        }
    }

    // ── History 子メニュー ──────────────────────────────────────────────────

    private void OpenSubmenu()
    {
        _inSubmenu  = true;
        _childIndex = 0;
        if (_childRoot != null) _childRoot.SetActive(true);
        RefreshSelection();        // 親の説明文を隠す
        RefreshChildSelection();
        RhythmGame.UI.Common.ShortcutHintOverlay.Set("↑↓: 種類   Space: 決定   ESC / ←: 戻る");
    }

    private void CloseSubmenu()
    {
        _inSubmenu = false;
        if (_childRoot != null) _childRoot.SetActive(false);
        RefreshSelection();        // 親の説明文を戻す
        RhythmGame.UI.Common.ShortcutHintOverlay.Set("↑↓: 項目   Space: 決定   ESC: 終了");
    }

    private void MoveChild(int dir)
    {
        _childIndex = (_childIndex + dir + _historyChildren.Length) % _historyChildren.Length;
        RefreshChildSelection();
    }

    private void RefreshChildSelection()
    {
        for (int i = 0; i < _historyChildren.Length; i++)
        {
            bool sel = i == _childIndex;
            if (_childLabels != null && i < _childLabels.Length && _childLabels[i] != null)
            {
                _childLabels[i].SetFontSize(sel ? 30f : 23f);
                _childLabels[i].SetSelected(sel);
            }
            if (_childDescs != null && i < _childDescs.Length && _childDescs[i] != null)
                _childDescs[i].transform.parent.gameObject.SetActive(sel);
        }
    }

    private void DecideChild()
    {
        if (SceneRouter.Instance == null) return;
        SceneRouter.Instance.GoTo(SceneId.History, new HistoryParameters
        {
            Mode = _historyChildren[_childIndex].mode,
        });
    }

    // ── Decision ───────────────────────────────────────────────────────────

    private void Decide()
    {
        if (SceneRouter.Instance == null)
        {
            Debug.LogError("[TitleController] SceneRouter.Instance is null — Bootstrap not loaded?");
            return;
        }

        switch (_menus[_currentIndex].id)
        {
            case MenuId.FreePlay:
                SceneRouter.Instance.GoTo(SceneId.SongSelect);
                break;
            case MenuId.Online:
                // オンラインロビー (対戦待合) を経由してマッチングへ。
                SceneRouter.Instance.GoTo(SceneId.PVPLobby);
                break;
            case MenuId.Config:
                SceneRouter.Instance.GoTo(SceneId.Config);
                break;
            case MenuId.History:
                OpenSubmenu();   // 子ボタン (Ladder Match / Free Play) を展開
                break;
            case MenuId.Exit:
                ConfirmExit();
                break;
        }
    }

    private void ConfirmExit()
    {
        RhythmGame.UI.Common.ConfirmDialog.Show(
            "ゲームを終了しますか？", "終了する", "もどる",
            onConfirm: QuitGame);
    }

    private void QuitGame()
    {
        Debug.Log("[Title] EXIT");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ── Selected icon pulse ────────────────────────────────────────────────

    private IEnumerator PulseSelectedIcon()
    {
        while (true)
        {
            if (_itemIcons != null && _currentIndex < _itemIcons.Length && _itemIcons[_currentIndex] != null)
            {
                float alpha = Mathf.Lerp(0.55f, 1.0f, (Mathf.Sin(Time.time * 3f) + 1f) * 0.5f);
                foreach (var g in _itemIcons[_currentIndex].GetComponentsInChildren<UnityEngine.UI.Graphic>())
                {
                    var c = g.color;
                    c.a = alpha;
                    g.color = c;
                }
            }
            yield return null;
        }
    }
}
