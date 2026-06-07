using System.Threading.Tasks;
using RhythmGame.Network.Api;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 起動時のログイン/新規登録画面 (Go サーバー移行 M1)。Bootstrap → Login → Title。
///   - 保存済みセッションがあれば自動リフレッシュして Title へスキップ
///   - LOGIN / REGISTER のモード切替 (REGISTER 時のみ表示名フィールド)
///   - 「オフラインで続行」= AuthManager.OfflineMode (ソロのみプレイ可)
/// エラー文言はサーバーの error.message (日本語) をそのまま表示する。
/// </summary>
public class LoginController : MonoBehaviour
{
    [Header("Inputs")]
    [SerializeField] TMP_InputField _emailInput;
    [SerializeField] TMP_InputField _passwordInput;
    [SerializeField] TMP_InputField _displayNameInput;   // REGISTER モードのみ表示
    [SerializeField] GameObject     _displayNameRow;

    [Header("Buttons")]
    [SerializeField] Button          _submitButton;       // LOGIN / REGISTER 実行
    [SerializeField] TextMeshProUGUI _submitLabel;
    [SerializeField] Button          _modeToggleButton;   // モード切替
    [SerializeField] TextMeshProUGUI _modeToggleLabel;
    [SerializeField] Button          _offlineButton;

    [Header("Status")]
    [SerializeField] TextMeshProUGUI _statusText;
    [SerializeField] TextMeshProUGUI _serverText;         // 接続先表示

    bool _registerMode;
    bool _busy;

    void Start()
    {
        Application.runInBackground = true;
        JacketBackgroundController.Instance?.SetCanvasEnabled(false);

        if (_submitButton     != null) _submitButton.onClick.AddListener(() => _ = SubmitAsync());
        if (_modeToggleButton != null) _modeToggleButton.onClick.AddListener(ToggleMode);
        if (_offlineButton    != null) _offlineButton.onClick.AddListener(ContinueOffline);

        if (_serverText != null) _serverText.text = ServerConfig.BaseUrl;
        if (_emailInput != null) _emailInput.text = AuthManager.Email;

        ApplyMode();
        RhythmGame.UI.Common.ShortcutHintOverlay.Set("Tab: 入力欄移動   Enter: 決定");

        _ = AutoLoginAsync();
    }

    // 保存済みセッションがあれば自動ログイン (リフレッシュ成功で Title へ)
    async Task AutoLoginAsync()
    {
        if (!AuthManager.HasSession) { SetStatus("", false); return; }

        SetBusy(true);
        SetStatus("自動ログイン中...", false);
        bool ok = AuthManager.HasValidAccessToken || await AuthManager.TryRefreshAsync();
        if (ok)
        {
            SetStatus($"ログイン: {AuthManager.DisplayName}", false);
            GoToTitle();
            return;
        }
        SetBusy(false);
        SetStatus(AuthManager.HasSession
            ? "自動ログインに失敗しました(サーバー未接続?)。再ログインかオフライン続行を選んでください"
            : "セッションの有効期限が切れました。再ログインしてください", true);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || _busy) return;
        if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
            _ = SubmitAsync();
    }

    // ── Actions ──────────────────────────────────────────────────────────────

    async Task SubmitAsync()
    {
        if (_busy) return;
        string email = _emailInput    != null ? _emailInput.text.Trim() : "";
        string pw    = _passwordInput != null ? _passwordInput.text : "";

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(pw))
        {
            SetStatus("メールアドレスとパスワードを入力してください", true);
            return;
        }

        SetBusy(true);
        if (_registerMode)
        {
            string name = _displayNameInput != null ? _displayNameInput.text.Trim() : "";
            if (string.IsNullOrEmpty(name))
            {
                SetStatus("表示名を入力してください", true);
                SetBusy(false);
                return;
            }
            SetStatus("登録中...", false);
            var r = await AuthManager.RegisterAsync(email, pw, name);
            if (r.Ok) { SetStatus("登録完了", false); GoToTitle(); return; }
            SetStatus(r.ErrorMessage, true);
        }
        else
        {
            SetStatus("ログイン中...", false);
            var r = await AuthManager.LoginAsync(email, pw);
            if (r.Ok) { SetStatus("", false); GoToTitle(); return; }
            SetStatus(r.ErrorMessage, true);
        }
        SetBusy(false);
    }

    void ToggleMode()
    {
        _registerMode = !_registerMode;
        ApplyMode();
        SetStatus("", false);
    }

    void ApplyMode()
    {
        if (_displayNameRow   != null) _displayNameRow.SetActive(_registerMode);
        if (_submitLabel      != null) _submitLabel.text      = _registerMode ? "登録する" : "ログイン";
        if (_modeToggleLabel  != null) _modeToggleLabel.text  = _registerMode ? "ログインへ" : "新規登録へ";
    }

    void ContinueOffline()
    {
        AuthManager.OfflineMode = true;
        Debug.Log("[Login] continue offline (solo only)");
        GoToTitle();
    }

    async void GoToTitle()
    {
        // ログイン確定後にサーバー楽曲同期をバックグラウンド開始 (選曲画面が EnsureSyncedAsync で待ち合わせる)
        if (!AuthManager.OfflineMode) _ = ServerSongLibrary.SyncAsync();

        // 自動ログインは Bootstrap→Login の遷移中に同期的へ完了しうる。
        // その時点の GoTo は SceneRouter の _isTransitioning ガードに握り潰されるため、遷移完了を待つ。
        while (SceneRouter.Instance != null && SceneRouter.Instance.IsTransitioning)
            await Task.Yield();
        SceneRouter.Instance?.GoTo(SceneId.Title);
    }

    // ── UI helpers ───────────────────────────────────────────────────────────

    void SetBusy(bool busy)
    {
        _busy = busy;
        if (_submitButton     != null) _submitButton.interactable     = !busy;
        if (_modeToggleButton != null) _modeToggleButton.interactable = !busy;
        if (_offlineButton    != null) _offlineButton.interactable    = !busy;
    }

    void SetStatus(string message, bool isError)
    {
        if (_statusText == null) return;
        _statusText.text  = message ?? "";
        _statusText.color = isError ? new Color(0.96f, 0.45f, 0.55f) : new Color(1f, 1f, 1f, 0.8f);
    }
}
