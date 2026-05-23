using System.Collections;
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
        [Header("Optional UI (auto fallback to OnGUI if null)")]
        [SerializeField] TextMeshProUGUI _statusText;
        [SerializeField] TextMeshProUGUI _opponentText;
        [SerializeField] Button          _cancelButton;
        [SerializeField] float           _pollIntervalSec = 1.5f;

        bool   _running;
        bool   _canceled;
        string _statusLine = "(idle)";
        string _opponentLine = "";

        void Start()
        {
            Application.runInBackground = true;
            JacketBackgroundController.Instance?.SetCanvasEnabled(true);
            JacketBackgroundController.Instance?.SetFallback();

            if (_cancelButton != null)
                _cancelButton.onClick.AddListener(OnCancelPressed);

            // 試合中に直接 Matchmaking に戻ってきた場合は何もしない
            var pvp = PvpFlowController.Instance;
            if (pvp != null && pvp.IsActive)
            {
                _statusLine = "PVP match already active — returning to game";
                Debug.LogWarning("[Matchmaking] " + _statusLine);
                return;
            }

            _ = RunMatchmakingLoop();
        }

        void OnDisable()
        {
            _canceled = true;
        }

        void OnCancelPressed()
        {
            _canceled = true;
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
            _running = true;
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

            _statusLine = "Waiting for opponent...  depth=" + join.Body.queueDepth;
            UpdateUi();

            while (!_canceled)
            {
                await Task.Delay((int)(_pollIntervalSec * 1000));
                if (_canceled) break;

                var s = await net.GetQueueStatusAsync(LocalIdentity.UserId);
                if (!s.Ok)
                {
                    _statusLine = "Poll error: " + s.Error;
                    UpdateUi();
                    continue;
                }
                if (s.Body.status == "matched")
                {
                    StartMatchFromQueueResponse(s.Body);
                    return;
                }
                if (s.Body.status == "idle")
                {
                    _statusLine = "Queue dropped — rejoining";
                    UpdateUi();
                    var rejoin = await net.JoinQueueAsync(LocalIdentity.UserId);
                    if (rejoin.Ok && rejoin.Body.status == "matched")
                    {
                        StartMatchFromQueueResponse(rejoin.Body);
                        return;
                    }
                    continue;
                }
                _statusLine = "Waiting...  depth=" + s.Body.queueDepth;
                UpdateUi();
            }
            _running = false;
        }

        void StartMatchFromQueueResponse(QueueResponseDto body)
        {
            _statusLine = "Match found: " + body.opponentId;
            _opponentLine = string.Format("Songs: {0}",
                body.songs != null ? string.Join(", ", body.songs.ConvertAll(s => s.songId)) : "(none)");
            UpdateUi();
            Debug.Log("[Matchmaking] " + _statusLine + " / " + _opponentLine);

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
            if (_statusText != null)   _statusText.text   = _statusLine;
            if (_opponentText != null) _opponentText.text = _opponentLine;
        }

        // ── Fallback OnGUI (シーン UI 未組込みでも操作可) ──────────────────────
        void OnGUI()
        {
            // 正規 UI があれば描画しない
            if (_statusText != null) return;

            const float w = 420f;
            const float h = 180f;
            var rect = new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h);
            GUI.Box(rect, "Matchmaking");
            GUILayout.BeginArea(new Rect(rect.x + 16, rect.y + 28, rect.width - 32, rect.height - 36));
            GUILayout.Label("Status:");
            GUILayout.Label(_statusLine);
            GUILayout.Space(6);
            GUILayout.Label(_opponentLine);
            GUILayout.Space(12);
            if (GUILayout.Button("Cancel"))
                OnCancelPressed();
            GUILayout.EndArea();
        }
    }
}
