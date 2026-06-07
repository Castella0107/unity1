using System.Threading.Tasks;
using RhythmGame.Network;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// SongSelect のプレイヤーデータポップアップ (画面遷移図 2026-06-07 改訂 ③)。
/// プロフィールカードのクリックで開くモーダル。シーン配線不要の自己生成シングルトン（OnGUI 描画、ConfirmDialog と同方式）。
///
/// 表示: プレイヤー名 / ソロ総プレイ回数 (ローカルDB) / PVP 戦績 (GET /api/pvp/user/{id}/stats)。
/// 閉じる: ESC / 閉じるボタン / ボックス領域外クリック。
/// <see cref="IsOpen"/> が真の間、SongSelect 側は入力処理を抑止すること。
/// </summary>
public class PlayerDataPopup : MonoBehaviour
{
    static PlayerDataPopup _instance;

    /// <summary>
    /// ポップアップ表示中か。閉じたフレームも true を返す（閉じ入力を裏画面が同フレームで再拾いするのを防ぐ）。
    /// </summary>
    public static bool IsOpen => _instance != null
        && (_instance._open || Time.frameCount == _instance._closeFrame);

    bool _open;
    int  _openedFrame;
    int  _closeFrame = -1;

    // ── 表示データ（Show 時に非同期取得） ──────────────────────────────
    string _playerName   = "";
    string _soloLine     = "取得中...";
    string _pvpRating    = "--";
    string _pvpMatches   = "--";
    string _pvpWinLine   = "--";
    string _pvpRatioLine = "--";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject("[PlayerDataPopup]");
        _instance = go.AddComponent<PlayerDataPopup>();
        DontDestroyOnLoad(go);
    }

    /// <summary>ポップアップを開き、ローカルDB とサーバーからプレイヤーデータを取得する。</summary>
    public static void Show()
    {
        Bootstrap();
        _instance.Open();
    }

    void Open()
    {
        _playerName   = !string.IsNullOrEmpty(RhythmGame.Network.Api.AuthManager.DisplayName)
            ? RhythmGame.Network.Api.AuthManager.DisplayName
            : LocalIdentity.UserId;
        _soloLine     = "取得中...";
        _pvpRating    = "--";
        _pvpMatches   = "--";
        _pvpWinLine   = "--";
        _pvpRatioLine = "--";
        _open         = true;
        _openedFrame  = Time.frameCount;
        _ = LoadAsync();
    }

    async Task LoadAsync()
    {
        // ソロ: ローカルDB の総プレイ回数
        try
        {
            if (RepositoryService.Instance?.IsReady == true)
            {
                int plays = await RepositoryService.Instance.PlayRecords.GetTotalPlaysAsync();
                _soloLine = $"{plays:N0} PLAYS";
            }
            else
            {
                _soloLine = "-- (DB未接続)";
            }
        }
        catch { _soloLine = "--"; }

        // PVP: Go サーバー /users/me (M6 で結線変更)
        if (RhythmGame.Network.Api.AuthManager.OfflineMode) { SetPvpUnavailable(); return; }
        var r = await RhythmGame.Network.Api.ApiClient
            .GetAsync<RhythmGame.Network.Api.UserMeDto>("/users/me");
        if (!r.Ok || r.Data == null) { SetPvpUnavailable(); return; }

        int total = r.Data.WinCount + r.Data.LossCount + r.Data.DrawCount;
        _pvpRating    = $"{r.Data.Rating:F0}";
        _pvpMatches   = $"{total}";
        _pvpWinLine   = $"{r.Data.WinCount}W - {r.Data.LossCount}L - {r.Data.DrawCount}D";
        _pvpRatioLine = total > 0 ? $"{(double)r.Data.WinCount / total * 100.0:0.00}%" : "0.00%";
    }

    void SetPvpUnavailable()
    {
        _pvpRating    = "--";
        _pvpMatches   = "--";
        _pvpWinLine   = "-- (オフライン)";
        _pvpRatioLine = "--";
    }

    void Update()
    {
        if (!_open) return;
        if (Time.frameCount == _openedFrame) return;   // 開いたフレームの入力は無視

        var kb = Keyboard.current;
        bool closeKey = kb != null && (kb.escapeKey.wasPressedThisFrame
                                    || kb.spaceKey.wasPressedThisFrame
                                    || kb.enterKey.wasPressedThisFrame);

        var pad = Gamepad.current;
        if (pad != null && (RhythmGame.Input.GamepadLayout.BackPressed(pad)
                         || RhythmGame.Input.GamepadLayout.ConfirmPressed(pad)))
            closeKey = true;

        // 領域外クリックで閉じる
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            var pos = mouse.position.ReadValue();
            // OnGUI 座標系 (左上原点) に合わせる
            var guiPos = new Vector2(pos.x, Screen.height - pos.y);
            if (!BoxRect().Contains(guiPos)) closeKey = true;
        }

        if (closeKey) Close();
    }

    void Close()
    {
        _open       = false;
        _closeFrame = Time.frameCount;
    }

    Rect BoxRect()
    {
        const float w = 560f, h = 360f;
        return new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
    }

    Texture2D _dimTex;

    Texture2D DimTex()
    {
        if (_dimTex == null)
        {
            _dimTex = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            _dimTex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.65f));
            _dimTex.Apply();
        }
        return _dimTex;
    }

    void OnGUI()
    {
        if (!_open) return;

        // 背景の暗幕（クリック透過防止も兼ねる）
        GUI.depth = -1000;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), DimTex());

        var box = BoxRect();
        var boxStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.UpperCenter,
            fontSize  = 22,
            padding   = new RectOffset(24, 24, 20, 20),
        };
        GUI.Box(box, "PLAYER DATA", boxStyle);

        var label = new GUIStyle(GUI.skin.label) { fontSize = 17 };
        var value = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
        var head  = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
        head.normal.textColor = new Color(0.97f, 0.78f, 0.25f);   // gold (Lobby と統一)

        float x = box.x + 28f, w = box.width - 56f;
        float y = box.y + 60f;

        void Row(string k, string v)
        {
            GUI.Label(new Rect(x, y, w * 0.5f, 26f), k, label);
            GUI.Label(new Rect(x + w * 0.5f, y, w * 0.5f, 26f), v, value);
            y += 30f;
        }

        Row("NAME", _playerName);
        y += 6f;

        GUI.Label(new Rect(x, y, w, 24f), "── SOLO ──", head); y += 28f;
        Row("TOTAL PLAYS", _soloLine);
        y += 6f;

        GUI.Label(new Rect(x, y, w, 24f), "── PVP ──", head); y += 28f;
        Row("RATING",      _pvpRating);
        Row("MATCHES",     _pvpMatches);
        Row("WIN - LOSE",  _pvpWinLine);
        Row("WIN RATIO",   _pvpRatioLine);

        // 閉じるボタン
        var btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 16 };
        var btnRect  = new Rect(box.x + (box.width - 180f) * 0.5f, box.yMax - 56f, 180f, 40f);
        if (GUI.Button(btnRect, "閉じる (ESC)", btnStyle)) Close();
    }
}
