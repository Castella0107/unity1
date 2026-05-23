using System.Threading.Tasks;
using UnityEngine;

namespace RhythmGame.Network
{
    /// <summary>
    /// PVP モードの GamePlay 中だけアクティブな OnGUI オーバーレイ。
    /// 自分の進捗を 0.5秒間隔で POST、相手の進捗を同時にスナップショット取得し画面右上に表示する。
    ///
    /// 自動 spawn される常駐シングルトン。PvpFlowController.IsActive かつ GamePlay シーン中のみ動作。
    /// </summary>
    public class PvpProgressOverlay : MonoBehaviour
    {
        public static PvpProgressOverlay Instance { get; private set; }

        public const float PollIntervalSec = 0.5f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("PvpProgressOverlay (auto)");
            go.AddComponent<PvpProgressOverlay>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // 外から GamePlay 側が UpdateLocalProgress で呼び出す。
        // 内部で per-frame 値を保持しておき、コルーチンが 0.5 秒ごとに POST する。
        float _localPercent;
        int   _localScore;
        int   _localSongIndex;
        bool  _localValid;

        ProgressSnapshotDto _snapshot;
        float _lastPostTime;
        bool  _polling;

        public void UpdateLocalProgress(int songIndex, float percent0to1, int score)
        {
            _localSongIndex = songIndex;
            _localPercent   = Mathf.Clamp01(percent0to1);
            _localScore     = score;
            _localValid     = true;
        }

        public void ClearLocalProgress()
        {
            _localValid = false;
            _snapshot   = null;
        }

        void Update()
        {
            var pvp = PvpFlowController.Instance;
            if (pvp == null || !pvp.IsActive) { _polling = false; return; }
            if (!_localValid) return;

            // 1 回ずつ flight in-flight 制限 (前回の post が完了してから次へ)
            if (_polling) return;
            if (Time.unscaledTime - _lastPostTime < PollIntervalSec) return;

            _polling = true;
            _ = TickAsync(pvp);
        }

        async Task TickAsync(PvpFlowController pvp)
        {
            try
            {
                var net = NetworkClient.Instance;
                if (net == null) return;
                var r = await net.SendPvpProgressAsync(
                    pvp.MatchId, pvp.SelfUserId,
                    _localSongIndex, Mathf.RoundToInt(_localPercent * 100000f), _localScore);
                if (r.Ok && r.Body != null)
                    _snapshot = r.Body;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[PvpProgressOverlay] tick exception: " + e.Message);
            }
            finally
            {
                _lastPostTime = Time.unscaledTime;
                _polling = false;
            }
        }

        // ── Display ────────────────────────────────────────────────────────────

        void OnGUI()
        {
            var pvp = PvpFlowController.Instance;
            if (pvp == null || !pvp.IsActive) return;
            if (_snapshot == null) return;

            const float w = 280f;
            const float h = 110f;
            var rect = new Rect(Screen.width - w - 16f, 16f, w, h);
            GUI.Box(rect, "PVP — vs " + (pvp.OpponentId ?? ""));

            GUILayout.BeginArea(new Rect(rect.x + 12f, rect.y + 24f, rect.width - 24f, rect.height - 32f));

            bool selfIsA = pvp.SelfUserId == _snapshot.a?.userId;
            var selfSide = selfIsA ? _snapshot.a : _snapshot.b;
            var oppSide  = selfIsA ? _snapshot.b : _snapshot.a;

            DrawSide("YOU", selfSide);
            GUILayout.Space(4f);
            DrawSide("OPP", oppSide);

            GUILayout.EndArea();
        }

        static void DrawSide(string label, ProgressSideDto p)
        {
            if (p == null) { GUILayout.Label($"{label}: (no data)"); return; }
            float pct = p.percentX1000 / 1000f;
            GUILayout.Label($"{label} song={p.songIndex + 1}  {pct:F1}%  score={p.score}");
        }
    }
}
