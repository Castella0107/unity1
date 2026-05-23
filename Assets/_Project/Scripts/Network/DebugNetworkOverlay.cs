using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RhythmGame.Network
{
    /// <summary>
    /// シーン非依存のデバッグ用ネットワークオーバーレイ。
    /// _Persistent.unity の GameObject にアタッチして使う。
    ///
    /// 操作:
    ///   - F9 でオーバーレイの表示/非表示を切替
    ///   - "Ping" ボタンで /api/ping 疎通確認
    ///   - "Validate Latest Replay" ボタンで RepositoryService から取得した
    ///     最新の PlayRecord のリプレイをサーバー側で検証
    ///
    /// 注意:
    ///   - デバッグ用途のため正規 UI ではない (OnGUI / IMGUI ベース)。
    ///   - 正規 UI への移行は Title 画面・History 詳細画面で別途実装する。
    /// </summary>
    public class DebugNetworkOverlay : MonoBehaviour
    {
        [SerializeField] bool _visibleOnStart = false;
        [SerializeField] Key  _toggleKey = Key.F9;

        bool   _visible;
        string _lastPingText = "(no ping yet)";
        string _lastValidateText = "(no validation yet)";
        string _lastLeaderboardText = "(no leaderboard yet)";
        string _lastPersonalText = "(no personal best yet)";
        string _lastPvpText = "(no pvp test yet)";
        bool   _busy;
        string _serverUrlEdit;
        string _userNameEdit;
        Vector2 _scroll;

        void Awake()
        {
            _visible = _visibleOnStart;
            _serverUrlEdit = ServerConfig.BaseUrl;
            _userNameEdit  = PlayerPrefs.GetString(LocalIdentity.PrefKey, "");
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb[_toggleKey].wasPressedThisFrame)
                _visible = !_visible;
        }

        void OnGUI()
        {
            if (!_visible) return;

            const float w = 520f;
            const float h = 540f;
            var rect = new Rect(Screen.width - w - 16f, 16f, w, h);

            GUI.Box(rect, "DebugNetworkOverlay  (F9 to toggle)");

            GUILayout.BeginArea(new Rect(rect.x + 8f, rect.y + 24f, rect.width - 16f, rect.height - 32f));

            GUILayout.Label("Server URL:");
            _serverUrlEdit = GUILayout.TextField(_serverUrlEdit ?? "");

            GUILayout.Label("User Name (effective UserId: " + LocalIdentity.UserId + ")");
            _userNameEdit = GUILayout.TextField(_userNameEdit ?? "");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply URL"))
            {
                ServerConfig.BaseUrl = _serverUrlEdit;
                Debug.Log("[DebugNetworkOverlay] BaseUrl updated: " + ServerConfig.BaseUrl);
            }
            if (GUILayout.Button("Apply Name"))
            {
                PlayerPrefs.SetString(LocalIdentity.PrefKey, _userNameEdit ?? "");
                PlayerPrefs.Save();
                Debug.Log("[DebugNetworkOverlay] UserId now: " + LocalIdentity.UserId);
            }
            GUI.enabled = !_busy;
            if (GUILayout.Button("Ping"))
                _ = DoPing();
            if (GUILayout.Button("Validate Latest"))
                _ = DoValidateLatest();
            if (GUILayout.Button("Leaderboard (latest song)"))
                _ = DoFetchLeaderboard();
            if (GUILayout.Button("My Best (latest song)"))
                _ = DoFetchPersonalBest();
            if (GUILayout.Button($"Flush Queue ({SubmissionQueue.Count()})"))
                _ = DoFlushQueue();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Test PVP Pipeline (alice vs bob, latest replay ×3)"))
                _ = DoTestPvpPipeline();
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(220f));
            GUILayout.Label("Ping: " + _lastPingText);
            GUILayout.Label("Validate: " + _lastValidateText);
            GUILayout.Label("Personal: " + _lastPersonalText);
            GUILayout.Label("Leaderboard:");
            GUILayout.Label(_lastLeaderboardText);
            GUILayout.Label("PVP:");
            GUILayout.Label(_lastPvpText);
            GUILayout.EndScrollView();
            if (_busy) GUILayout.Label("...busy...");

            GUILayout.EndArea();
        }

        // ── Actions ─────────────────────────────────────────────────────────────

        async Task DoPing()
        {
            if (NetworkClient.Instance == null)
            {
                _lastPingText = "NetworkClient.Instance is null";
                return;
            }
            _busy = true;
            try
            {
                var r = await NetworkClient.Instance.PingAsync();
                _lastPingText = r.Ok
                    ? $"OK rt={r.RoundtripMs}ms server={r.Body?.serverVersion} time={r.Body?.serverTimeUnixMs}"
                    : $"FAIL rt={r.RoundtripMs}ms err={r.Error}";
            }
            finally { _busy = false; }
        }

        async Task DoValidateLatest()
        {
            if (NetworkClient.Instance == null)
            {
                _lastValidateText = "NetworkClient.Instance is null";
                return;
            }
            if (RepositoryService.Instance == null || !RepositoryService.Instance.IsReady)
            {
                _lastValidateText = "RepositoryService not ready";
                return;
            }

            _busy = true;
            try
            {
                var history = await RepositoryService.Instance.PlayRecords.GetAllHistoryAsync(limit: 1);
                if (history == null || history.Count == 0)
                {
                    _lastValidateText = "No play records to validate";
                    return;
                }
                var rec = history[0];
                if (string.IsNullOrEmpty(rec.ReplayPath) || !File.Exists(rec.ReplayPath))
                {
                    _lastValidateText = $"Replay file not found: {rec.ReplayPath}";
                    return;
                }

                byte[] bytes = File.ReadAllBytes(rec.ReplayPath);
                var claim = new ResultClaimDto
                {
                    score       = rec.RawScore,
                    maxCombo    = rec.MaxCombo,
                    perfectPlus = rec.PerfectPlusCount,
                    perfect     = rec.PerfectCount,
                    great       = rec.GreatCount,
                    good        = rec.GoodCount,
                    miss        = rec.MissCount,
                    rank        = rec.Rank ?? "",
                };
                // サーバー永続化用メタ。VALID なら server 側 DB に Insert される。
                var meta = new ValidateRequestDto
                {
                    playId           = rec.PlayId,
                    songId           = rec.SongId,
                    difficulty       = rec.Difficulty,
                    userId           = LocalIdentity.UserId,
                    playedAtUnixMs   = rec.PlayedAtUnixMs,
                    totalNotes       = rec.TotalNotes,
                    isFullCombo      = rec.IsFullCombo,
                    isAllPerfect     = rec.IsAllPerfect,
                    isAllPerfectPlus = rec.IsAllPerfectPlus,
                };

                var r = await NetworkClient.Instance.ValidateReplayAsync(rec.ChartHash, bytes, claim, meta);
                if (!r.Ok)
                {
                    _lastValidateText = $"FAIL transport={r.TransportError} rt={r.RoundtripMs}ms";
                    return;
                }
                _lastValidateText = r.Body.isValid
                    ? $"VALID rt={r.RoundtripMs}ms score={r.Body.serverResult?.score}"
                    : $"INVALID rt={r.RoundtripMs}ms reason={r.Body.mismatchReason}";
            }
            catch (System.Exception e)
            {
                _lastValidateText = "EXCEPTION: " + e.Message;
            }
            finally { _busy = false; }
        }

        async Task DoTestPvpPipeline()
        {
            if (NetworkClient.Instance == null) { _lastPvpText = "NetworkClient null"; return; }
            if (RepositoryService.Instance == null || !RepositoryService.Instance.IsReady)
            { _lastPvpText = "RepositoryService not ready"; return; }

            _busy = true;
            try
            {
                // 最新リプレイ 1 件取得 (3 曲分の代わりに同じリプレイを 3 回使う)
                var history = await RepositoryService.Instance.PlayRecords.GetAllHistoryAsync(limit: 1);
                if (history == null || history.Count == 0)
                { _lastPvpText = "No play records (play a song first)"; return; }
                var rec = history[0];
                if (string.IsNullOrEmpty(rec.ReplayPath) || !File.Exists(rec.ReplayPath))
                { _lastPvpText = "Replay file missing"; return; }

                byte[] bytes = File.ReadAllBytes(rec.ReplayPath);
                string b64 = System.Convert.ToBase64String(bytes);

                // 同じ songId を 3 つ pool に入れる → match.songs が全て同じ曲になる
                var pool = new[] { rec.SongId, rec.SongId, rec.SongId };

                var create = await NetworkClient.Instance.CreateMatchAsync("alice", "bob", pool);
                if (!create.Ok) { _lastPvpText = "Create FAIL: " + create.Error; return; }
                string matchId = create.Body.matchId;
                _lastPvpText = $"Match created {matchId.Substring(0, 8)} (songs={create.Body.songs.Count})";

                // alice / bob とも同じ 3 リプレイで submit → 全 sector draw 想定
                var songsToSubmit = new System.Collections.Generic.List<SubmitMatchSongDto>();
                foreach (var s in create.Body.songs)
                    songsToSubmit.Add(new SubmitMatchSongDto { songId = s.songId, replayDataBase64 = b64 });

                var subA = await NetworkClient.Instance.SubmitMatchAsync(matchId, "alice", songsToSubmit);
                if (!subA.Ok) { _lastPvpText = "alice submit FAIL: " + subA.Error; return; }
                if (subA.Body.matchFinalized) { _lastPvpText = "ERROR: finalized after alice only?"; return; }

                var subB = await NetworkClient.Instance.SubmitMatchAsync(matchId, "bob", songsToSubmit);
                if (!subB.Ok) { _lastPvpText = "bob submit FAIL: " + subB.Error; return; }
                if (!subB.Body.matchFinalized || subB.Body.result == null)
                { _lastPvpText = "ERROR: not finalized after both submits"; return; }

                var r = subB.Body.result;
                string outcome = r.outcomeKind == 0 ? "DRAW" : (r.outcomeKind == 1 ? "alice WINS" : "bob WINS");
                _lastPvpText =
                    $"matchId={matchId.Substring(0, 8)}  songs={rec.SongId}×3\n" +
                    $"  pts: alice={r.totalPointsA}  bob={r.totalPointsB}  outcome={outcome}\n" +
                    $"  alice R: {r.ratingABefore:F1} → {r.ratingAAfter:F1}\n" +
                    $"  bob   R: {r.ratingBBefore:F1} → {r.ratingBAfter:F1}";
                Debug.Log("[DebugNetworkOverlay] " + _lastPvpText);
            }
            catch (System.Exception e) { _lastPvpText = "EXCEPTION: " + e.Message; }
            finally { _busy = false; }
        }

        async Task DoFlushQueue()
        {
            _busy = true;
            try
            {
                int n = await SubmissionQueue.FlushAsync();
                _lastValidateText = $"Queue flush: submitted {n}, remaining {SubmissionQueue.Count()}";
            }
            catch (System.Exception e) { _lastValidateText = "Flush EXCEPTION: " + e.Message; }
            finally { _busy = false; }
        }

        async Task DoFetchPersonalBest()
        {
            if (NetworkClient.Instance == null) { _lastPersonalText = "NetworkClient null"; return; }
            if (RepositoryService.Instance == null || !RepositoryService.Instance.IsReady)
            { _lastPersonalText = "RepositoryService not ready"; return; }

            _busy = true;
            try
            {
                var history = await RepositoryService.Instance.PlayRecords.GetAllHistoryAsync(limit: 1);
                if (history == null || history.Count == 0) { _lastPersonalText = "No play records"; return; }
                var rec = history[0];

                var r = await NetworkClient.Instance.FetchPersonalBestAsync(rec.SongId, rec.Difficulty, LocalIdentity.UserId);
                if (!r.Ok) { _lastPersonalText = $"FAIL rt={r.RoundtripMs}ms err={r.Error}"; return; }
                var b = r.Body;
                if (b == null || !b.hasRecord)
                {
                    _lastPersonalText = $"No record yet for {LocalIdentity.UserId} on {rec.SongId}/{rec.Difficulty} (totalUsers={b?.totalUsers ?? 0})";
                    return;
                }
                _lastPersonalText = $"{b.userId}  rank #{b.overallRank}/{b.totalUsers}  score={b.best.score} ({b.best.scoreRank})  combo={b.best.maxCombo}  rt={r.RoundtripMs}ms";
            }
            catch (System.Exception e) { _lastPersonalText = "EXCEPTION: " + e.Message; }
            finally { _busy = false; }
        }

        async Task DoFetchLeaderboard()
        {
            if (NetworkClient.Instance == null) { _lastLeaderboardText = "NetworkClient null"; return; }
            if (RepositoryService.Instance == null || !RepositoryService.Instance.IsReady)
            { _lastLeaderboardText = "RepositoryService not ready"; return; }

            _busy = true;
            try
            {
                var history = await RepositoryService.Instance.PlayRecords.GetAllHistoryAsync(limit: 1);
                if (history == null || history.Count == 0)
                { _lastLeaderboardText = "No play records (play a song first)"; return; }
                var rec = history[0];

                var r = await NetworkClient.Instance.FetchLeaderboardAsync(rec.SongId, rec.Difficulty, 10);
                if (!r.Ok) { _lastLeaderboardText = $"FAIL rt={r.RoundtripMs}ms err={r.Error}"; return; }

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"{r.Body.songId}/{r.Body.difficulty}  total={r.Body.total}  rt={r.RoundtripMs}ms");
                if (r.Body.entries != null)
                    foreach (var e in r.Body.entries)
                        sb.AppendLine($"  #{e.rank}  {e.userId}  {e.score} ({e.scoreRank})  combo={e.maxCombo}");
                _lastLeaderboardText = sb.ToString().TrimEnd();
            }
            catch (System.Exception e) { _lastLeaderboardText = "EXCEPTION: " + e.Message; }
            finally { _busy = false; }
        }
    }
}
