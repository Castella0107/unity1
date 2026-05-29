using System.Threading.Tasks;
using RhythmGame.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmGame.UI.Pvp
{
    /// <summary>
    /// Matchmaking.unity に付ける MonoBehaviour。
    /// JoinQueueAsync → GetQueueStatusAsync を周期 poll → matched で PvpFlowController.StartMatch。
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
                _cancelButton.onClick.AddListener(OnCancelPressed);

            if (_youNameText != null)     _youNameText.text     = LocalIdentity.UserId;
            if (_opponentNameText != null) _opponentNameText.text = _opponentName;
            if (_timerText != null)       _timerText.text       = "00:00";

            // 試合中に直接 Matchmaking に戻ってきた場合は何もしない
            var pvp = PvpFlowController.Instance;
            if (pvp != null && pvp.IsActive)
            {
                _statusLine = "PVP match already active — returning to game";
                Debug.LogWarning("[Matchmaking] " + _statusLine);
                UpdateUi();
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
            try
            {
                if (NetworkClient.Instance != null)
                    await NetworkClient.Instance.LeaveQueueAsync(LocalIdentity.UserId);
            }
            catch { }
            if (SceneRouter.Instance != null)
                SceneRouter.Instance.GoTo(SceneId.Title);
        }

        async Task RunMatchmakingLoop()
        {
            _statusLine = "Joining queue...";
            UpdateUi();

            var net = NetworkClient.Instance;
            if (net == null)
            {
                _statusLine = "NetworkClient not available";
                UpdateUi();
                return;
            }

            var join = await net.JoinQueueAsync(LocalIdentity.UserId);
            if (!join.Ok)
            {
                _statusLine = "Join FAIL: " + join.Error;
                UpdateUi();
                return;
            }

            // 即マッチング成立した場合
            if (join.Body.status == "matched")
            {
                StartMatchFromQueueResponse(join.Body);
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

                var s = await net.GetQueueStatusAsync(LocalIdentity.UserId);
                if (!s.Ok) continue;   // 一時的なポーリングエラーは UI を乱さず再試行

                if (s.Body.status == "matched")
                {
                    StartMatchFromQueueResponse(s.Body);
                    return;
                }
                if (s.Body.status == "idle")
                {
                    // キューから落ちていたら再参加
                    var rejoin = await net.JoinQueueAsync(LocalIdentity.UserId);
                    if (rejoin.Ok && rejoin.Body.status == "matched")
                    {
                        StartMatchFromQueueResponse(rejoin.Body);
                        return;
                    }
                }
            }
        }

        void StartMatchFromQueueResponse(QueueResponseDto body)
        {
            _animateSearch = false;
            _statusLine   = "MATCH FOUND";
            _opponentName = string.IsNullOrEmpty(body.opponentId) ? "OPPONENT" : body.opponentId;
            _songsLine    = body.songs != null
                ? "♪ " + string.Join("    ♪ ", body.songs.ConvertAll(s => s.songId))
                : "";
            UpdateUi();
            Debug.Log("[Matchmaking] MATCH FOUND vs " + _opponentName + " / " + _songsLine);

            var pvp = PvpFlowController.Instance;
            if (pvp == null)
            {
                Debug.LogError("[Matchmaking] PvpFlowController.Instance is null");
                return;
            }
            pvp.StartMatch(body.matchId, body.opponentId, body.songs);
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
                OnCancelPressed();
            GUILayout.EndArea();
        }
    }
}
