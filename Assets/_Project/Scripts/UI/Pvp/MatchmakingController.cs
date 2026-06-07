using System.Threading.Tasks;
using RhythmGame.Network.Api;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RhythmGame.UI.Pvp
{
    /// <summary>
    /// Matchmaking.unity に付ける MonoBehaviour (Go サーバー移行 M3 で全面書き換え)。
    /// PvpApi.JoinQueueAsync → GetQueueStatusAsync を 1.5 秒 poll (poll 毎にサーバーが再マッチ試行)
    /// → matched で PvpMatchContext をセットして PVPPrematch (READY 画面) へ。
    /// Inspector に Text/Button を割り当てれば正規 UI、未割当でも OnGUI フォールバックで動く。
    /// </summary>
    public class MatchmakingController : MonoBehaviour
    {
        [Header("Optional UI (auto fallback to OnGUI if status text is null)")]
        [SerializeField] TextMeshProUGUI _statusText;
        [SerializeField] TextMeshProUGUI _youNameText;
        [SerializeField] TextMeshProUGUI _opponentNameText;
        [SerializeField] TextMeshProUGUI _timerText;
        [SerializeField] TextMeshProUGUI _songsText;
        [SerializeField] Button          _cancelButton;
        [SerializeField] float           _pollIntervalSec = 1.5f;

        const string SearchBase = "SEARCHING FOR OPPONENT";

        bool   _canceled;
        bool   _animateSearch;   // true while waiting → drives Update timer + dots
        float  _elapsed;

        string _statusLine   = "(idle)";
        string _opponentName = "???";
        string _songsLine    = "";

        void Start()
        {
            // MCP/ウィンドウ非フォーカス時もループを止めないために必須。
            Application.runInBackground = true;
            JacketBackgroundController.Instance?.SetCanvasEnabled(true);
            JacketBackgroundController.Instance?.SetFallback();

            if (_cancelButton != null)
                _cancelButton.onClick.AddListener(ShowCancelConfirm);

            RhythmGame.UI.Common.ShortcutHintOverlay.Set("ESC: マッチング検索をキャンセル");

            if (_youNameText != null)
                _youNameText.text = string.IsNullOrEmpty(AuthManager.DisplayName) ? "YOU" : AuthManager.DisplayName;
            if (_opponentNameText != null) _opponentNameText.text = _opponentName;
            if (_timerText != null)        _timerText.text        = "00:00";

            if (AuthManager.OfflineMode || !AuthManager.HasSession)
            {
                _statusLine = "オンライン対戦にはログインが必要です";
                UpdateUi();
                return;
            }

            // 進行中マッチが残っている場合は Prematch に戻す
            if (!string.IsNullOrEmpty(PvpMatchContext.MatchId))
            {
                Debug.LogWarning("[Matchmaking] 進行中マッチあり → Prematch へ");
                SceneRouter.Instance?.GoTo(SceneId.PVPPrematch);
                return;
            }

            _ = RunMatchmakingLoop();
        }

        void OnDisable()
        {
            _canceled = true;
        }

        void Update()
        {
            // ESC: 検索キャンセル確認ダイアログ
            if (!RhythmGame.UI.Common.ConfirmDialog.IsOpen)
            {
                var kb = Keyboard.current;
                if (kb != null && kb.escapeKey.wasPressedThisFrame) ShowCancelConfirm();
                var pad = Gamepad.current;
                if (pad != null && RhythmGame.Input.GamepadLayout.BackPressed(pad)) ShowCancelConfirm();
            }

            if (!_animateSearch) return;

            _elapsed += Time.unscaledDeltaTime;

            if (_timerText != null)
            {
                int sec = (int)_elapsed;
                _timerText.text = string.Format("{0:00}:{1:00}", sec / 60, sec % 60);
            }
            if (_statusText != null)
            {
                int dots = ((int)(_elapsed * 2f)) % 4;   // ~2回/秒でドットが増減
                _statusText.text = SearchBase + new string('.', dots);
            }
        }

        // 検索キャンセルは誤操作防止のため確認ダイアログを挟む。
        void ShowCancelConfirm()
        {
            RhythmGame.UI.Common.ConfirmDialog.Show(
                "マッチング検索をやめますか？", "やめる", "つづける",
                onConfirm: OnCancelPressed);
        }

        void OnCancelPressed()
        {
            _canceled = true;
            _animateSearch = false;
            _statusLine = "Canceling...";
            UpdateUi();
            _ = CancelAndReturn();
        }

        async Task CancelAndReturn()
        {
            try { await PvpApi.LeaveQueueAsync(); }
            catch { }
            if (SceneRouter.Instance != null)
                SceneRouter.Instance.GoTo(SceneId.PVPLobby);
        }

        async Task RunMatchmakingLoop()
        {
            _statusLine = "Joining queue...";
            UpdateUi();

            var join = await PvpApi.JoinQueueAsync();
            if (_canceled) return;
            if (!join.Ok && join.ErrorCode != "ALREADY_IN_QUEUE")
            {
                // ALREADY_IN_MATCH: 残存マッチがある (タイムアウト放置等)。状態を出して中断。
                _statusLine = "参加失敗: " + join.ErrorMessage;
                UpdateUi();
                return;
            }

            // 即マッチング成立した場合
            if (join.Ok && join.Data.Status == "matched")
            {
                StartMatchFromQueueResponse(join.Data);
                return;
            }

            // 待機開始: Update がタイマーとドットアニメを駆動する
            _animateSearch = true;
            _statusLine = SearchBase;
            UpdateUi();

            while (!_canceled)
            {
                await Task.Delay((int)(_pollIntervalSec * 1000));
                if (_canceled) break;

                var s = await PvpApi.GetQueueStatusAsync();
                if (_canceled) break;   // await 中に Cancel された応答は捨てる(遷移と競合させない)
                if (!s.Ok) continue;    // 一時的なポーリングエラーは UI を乱さず再試行

                if (s.Data.Status == "matched")
                {
                    StartMatchFromQueueResponse(s.Data);
                    return;
                }
                if (s.Data.Status == "idle")
                {
                    // キューから落ちていたら再参加
                    var rejoin = await PvpApi.JoinQueueAsync();
                    if (_canceled) break;
                    if (rejoin.Ok && rejoin.Data.Status == "matched")
                    {
                        StartMatchFromQueueResponse(rejoin.Data);
                        return;
                    }
                }
            }
        }

        void StartMatchFromQueueResponse(QueueStatusDto body)
        {
            if (_canceled) return;   // Cancel 後に紛れ込んだ matched 応答は無視
            _animateSearch = false;
            _statusLine   = "MATCH FOUND";
            _opponentName = string.IsNullOrEmpty(body.OpponentId) ? "OPPONENT" : body.OpponentId;
            _songsLine    = "楽曲はドラフト (PICK/BAN) で決定";
            UpdateUi();
            Debug.Log($"[Matchmaking] MATCH FOUND vs {body.OpponentId} match={body.MatchId}");

            PvpMatchContext.StartMatch(body.MatchId, body.OpponentId);

            // 相手の表示名を取得 (MATCH FOUND 表示+以降の画面用にコンテキストへ)
            _ = ResolveOpponentNameAsync(body.OpponentId);

            // 即マッチ成立時は Lobby→Matchmaking の遷移がまだ進行中のことがあり、
            // 通常の GoTo だと _isTransitioning ガードに握り潰される(後から入った側が固まる実バグ)。
            SceneRouter.Instance?.GoToWhenIdle(SceneId.PVPPrematch);
        }

        async Task ResolveOpponentNameAsync(string opponentId)
        {
            if (string.IsNullOrEmpty(opponentId)) return;
            var r = await PvpApi.GetUserAsync(opponentId);
            if (r.Ok && r.Data != null && !string.IsNullOrEmpty(r.Data.DisplayName))
            {
                PvpMatchContext.OpponentDisplayName = r.Data.DisplayName;
                _opponentName = r.Data.DisplayName;
                UpdateUi();
            }
        }

        void UpdateUi()
        {
            if (_statusText != null)        _statusText.text       = _statusLine;
            if (_opponentNameText != null)  _opponentNameText.text = _opponentName;
            if (_songsText != null)         _songsText.text        = _songsLine;
        }

        // ── Fallback OnGUI (シーン UI 未組込みでも操作可) ──────────────────────
        void OnGUI()
        {
            // 正規 UI があれば描画しない
            if (_statusText != null) return;

            const float w = 420f;
            const float h = 200f;
            var rect = new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h);
            GUI.Box(rect, "Matchmaking");
            GUILayout.BeginArea(new Rect(rect.x + 16, rect.y + 28, rect.width - 32, rect.height - 36));
            GUILayout.Label("Status: " + _statusLine);
            GUILayout.Label("Opponent: " + _opponentName);
            GUILayout.Space(6);
            GUILayout.Label(_songsLine);
            GUILayout.Space(12);
            if (GUILayout.Button("Cancel"))
                ShowCancelConfirm();
            GUILayout.EndArea();
        }
    }
}
