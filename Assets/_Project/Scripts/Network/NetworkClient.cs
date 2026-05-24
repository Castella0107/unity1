using System;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace RhythmGame.Network
{
    /// <summary>
    /// _Persistent.unity に常駐するサーバー通信クライアント (シングルトン)。
    /// REST JSON 経路で疎通する。後段で gRPC-Net.Client 直結に差し替え可能な抽象設計。
    ///
    /// 主要 API:
    ///   - PingAsync(): GET /api/ping
    ///   - ValidateReplayAsync(): POST /api/replay/validate
    /// </summary>
    public class NetworkClient : MonoBehaviour, INetworkClient
    {
        /// <summary>
        /// シングルトン instance。<see cref="INetworkClient"/> 型として公開し、
        /// 将来 WebSocket/S3 PUT 版実装への差し替えを透過化する。
        /// </summary>
        public static INetworkClient Instance { get; private set; }

        /// <summary>直近の Ping 成功時刻 (Unix ms)。0 はまだ成功していない。</summary>
        public long LastPingUnixMs { get; private set; }

        /// <summary>
        /// シーン編集なしで _Persistent と同等の常駐 GameObject を生成する。
        /// BeforeSceneLoad で実行されるので、最初のシーンの Awake より前に Instance が利用可能になる。
        /// 既に NetworkClient が存在する場合は何もしない (テスト・複数 Bootstrap 対策)。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("NetworkClient (auto)");
            go.AddComponent<NetworkClient>();
            go.AddComponent<DebugNetworkOverlay>();
            // Awake が走って DontDestroyOnLoad される
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        async void Start()
        {
            // 起動時にキューがあれば疎通確認 → 成功で自動 flush。
            // ない時は 0 ms で finish、トラフィック発生せず。
            if (!ServerConfig.Enabled) return;
            if (SubmissionQueue.Count() == 0) return;
            Debug.Log($"[NetworkClient] Boot ping (queue={SubmissionQueue.Count()})");
            await PingAsync();   // 成功時 AutoFlushOnPingSuccess で flush 発火
        }

        // ── Ping ────────────────────────────────────────────────────────────────

        public class PingResult
        {
            public bool   Ok;
            public string Error;
            public long   RoundtripMs;
            public PingResponseDto Body;
        }

        /// <summary>Ping 成功時にバックグラウンドで送信キューを flush するか。</summary>
        public bool AutoFlushOnPingSuccess { get; set; } = true;

        public async Task<PingResult> PingAsync()
        {
            if (!ServerConfig.Enabled)
                return new PingResult { Ok = false, Error = "Network disabled in ServerConfig" };

            string url = ServerConfig.BaseUrl.TrimEnd('/') + "/api/ping";
            var sw = System.Diagnostics.Stopwatch.StartNew();

            using (var req = UnityWebRequest.Get(url))
            {
                req.timeout = ServerConfig.TimeoutSeconds;
                await SendAsync(req);
                sw.Stop();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    string err = $"{req.result} ({req.responseCode}): {req.error}";
                    Debug.LogWarning("[NetworkClient] Ping failed: " + err);
                    return new PingResult { Ok = false, Error = err, RoundtripMs = sw.ElapsedMilliseconds };
                }

                try
                {
                    var body = JsonConvert.DeserializeObject<PingResponseDto>(req.downloadHandler.text);
                    LastPingUnixMs = body?.serverTimeUnixMs ?? 0;
                    Debug.Log($"[NetworkClient] Ping ok ({sw.ElapsedMilliseconds} ms): server={body?.serverVersion}");

                    // サーバー復活検知 → キューを flush
                    if (AutoFlushOnPingSuccess && SubmissionQueue.Count() > 0)
                        _ = FlushSubmissionQueueSafe();

                    return new PingResult
                    {
                        Ok          = true,
                        Body        = body,
                        RoundtripMs = sw.ElapsedMilliseconds,
                    };
                }
                catch (Exception e)
                {
                    string err = "Parse error: " + e.Message + " body=" + req.downloadHandler.text;
                    Debug.LogWarning("[NetworkClient] " + err);
                    return new PingResult { Ok = false, Error = err, RoundtripMs = sw.ElapsedMilliseconds };
                }
            }
        }

        // ── ValidateReplay ──────────────────────────────────────────────────────

        public class ValidateResult
        {
            public bool                  Ok;
            public string                TransportError;
            public long                  RoundtripMs;
            public ValidateResponseDto   Body;
        }

        /// <summary>
        /// リプレイをサーバーで検証する。
        /// metadata (playId/songId/difficulty 等) を渡すとサーバー側で永続化される。
        /// 空のままでも検証は通るが、サーバーに保存されない。
        /// </summary>
        public async Task<ValidateResult> ValidateReplayAsync(
            string chartHashHex,
            byte[] replayBytes,
            ResultClaimDto claim,
            ValidateRequestDto metadata = null)
        {
            if (!ServerConfig.Enabled)
                return new ValidateResult { Ok = false, TransportError = "Network disabled in ServerConfig" };

            if (replayBytes == null || replayBytes.Length == 0)
                return new ValidateResult { Ok = false, TransportError = "Replay bytes are empty" };

            var dto = metadata != null ? metadata : new ValidateRequestDto();
            dto.chartHash        = chartHashHex ?? string.Empty;
            dto.replayDataBase64 = Convert.ToBase64String(replayBytes);
            dto.claim            = claim ?? new ResultClaimDto();

            string json = JsonConvert.SerializeObject(dto);
            string url  = ServerConfig.BaseUrl.TrimEnd('/') + "/api/replay/validate";
            var sw = System.Diagnostics.Stopwatch.StartNew();

            using (var req = new UnityWebRequest(url, "POST"))
            {
                req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = ServerConfig.TimeoutSeconds;

                await SendAsync(req);
                sw.Stop();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    string err = $"{req.result} ({req.responseCode}): {req.error}";
                    Debug.LogWarning("[NetworkClient] ValidateReplay failed: " + err);
                    return new ValidateResult { Ok = false, TransportError = err, RoundtripMs = sw.ElapsedMilliseconds };
                }

                try
                {
                    var body = JsonConvert.DeserializeObject<ValidateResponseDto>(req.downloadHandler.text);
                    Debug.Log($"[NetworkClient] ValidateReplay rt={sw.ElapsedMilliseconds}ms isValid={body?.isValid} reason={body?.mismatchReason}");
                    return new ValidateResult
                    {
                        Ok          = true,
                        Body        = body,
                        RoundtripMs = sw.ElapsedMilliseconds,
                    };
                }
                catch (Exception e)
                {
                    string err = "Parse error: " + e.Message;
                    Debug.LogWarning("[NetworkClient] " + err);
                    return new ValidateResult { Ok = false, TransportError = err, RoundtripMs = sw.ElapsedMilliseconds };
                }
            }
        }

        // ── PVP API ─────────────────────────────────────────────────────────────

        public class PvpCreateResult
        {
            public bool                   Ok;
            public string                 Error;
            public long                   RoundtripMs;
            public CreateMatchResponseDto Body;
        }

        public async Task<PvpCreateResult> CreateMatchAsync(string userIdA, string userIdB, string[] poolSongIds = null)
        {
            if (!ServerConfig.Enabled) return new PvpCreateResult { Ok = false, Error = "Network disabled" };
            if (string.IsNullOrEmpty(userIdA) || string.IsNullOrEmpty(userIdB))
                return new PvpCreateResult { Ok = false, Error = "userIdA/userIdB required" };

            var dto = new CreateMatchRequestDto { userIdA = userIdA, userIdB = userIdB, poolSongIds = poolSongIds };
            string url = ServerConfig.BaseUrl.TrimEnd('/') + "/api/pvp/match/create";
            var (ok, err, body, rt) = await PostJsonAsync<CreateMatchResponseDto>(url, dto);
            return new PvpCreateResult { Ok = ok, Error = err, Body = body, RoundtripMs = rt };
        }

        public class PvpSubmitResult
        {
            public bool                   Ok;
            public string                 Error;
            public long                   RoundtripMs;
            public SubmitMatchResponseDto Body;
        }

        public async Task<PvpSubmitResult> SubmitMatchAsync(string matchId, string userId, System.Collections.Generic.List<SubmitMatchSongDto> songs)
        {
            if (!ServerConfig.Enabled) return new PvpSubmitResult { Ok = false, Error = "Network disabled" };
            var dto = new SubmitMatchRequestDto { userId = userId, songs = songs };
            string url = ServerConfig.BaseUrl.TrimEnd('/') + $"/api/pvp/match/{UnityWebRequest.EscapeURL(matchId)}/submit";
            var (ok, err, body, rt) = await PostJsonAsync<SubmitMatchResponseDto>(url, dto);
            return new PvpSubmitResult { Ok = ok, Error = err, Body = body, RoundtripMs = rt };
        }

        public class PvpFetchResult
        {
            public bool           Ok;
            public string         Error;
            public long           RoundtripMs;
            public MatchResultDto Body;
        }

        public async Task<PvpFetchResult> FetchMatchAsync(string matchId)
        {
            if (!ServerConfig.Enabled) return new PvpFetchResult { Ok = false, Error = "Network disabled" };
            string url = ServerConfig.BaseUrl.TrimEnd('/') + $"/api/pvp/match/{UnityWebRequest.EscapeURL(matchId)}";
            var (ok, err, body, rt) = await GetJsonAsync<MatchResultDto>(url);
            return new PvpFetchResult { Ok = ok, Error = err, Body = body, RoundtripMs = rt };
        }

        // ── PVP Progress (in-match real-time) ───────────────────────────────────

        public class PvpProgressResult
        {
            public bool                Ok;
            public string              Error;
            public long                RoundtripMs;
            public ProgressSnapshotDto Body;
        }

        public async Task<PvpProgressResult> SendPvpProgressAsync(
            string matchId, string userId, int songIndex, int percentX1000, int score)
        {
            if (!ServerConfig.Enabled) return new PvpProgressResult { Ok = false, Error = "Network disabled" };
            var dto = new ProgressUpdateDto
            {
                userId       = userId,
                songIndex    = songIndex,
                percentX1000 = percentX1000,
                score        = score,
            };
            string url = ServerConfig.BaseUrl.TrimEnd('/') + $"/api/pvp/match/{UnityWebRequest.EscapeURL(matchId)}/progress";
            var (ok, err, body, rt) = await PostJsonAsync<ProgressSnapshotDto>(url, dto);
            return new PvpProgressResult { Ok = ok, Error = err, Body = body, RoundtripMs = rt };
        }

        public async Task<PvpProgressResult> FetchPvpProgressAsync(string matchId)
        {
            if (!ServerConfig.Enabled) return new PvpProgressResult { Ok = false, Error = "Network disabled" };
            string url = ServerConfig.BaseUrl.TrimEnd('/') + $"/api/pvp/match/{UnityWebRequest.EscapeURL(matchId)}/progress";
            var (ok, err, body, rt) = await GetJsonAsync<ProgressSnapshotDto>(url);
            return new PvpProgressResult { Ok = ok, Error = err, Body = body, RoundtripMs = rt };
        }

        // ── PVP Queue ───────────────────────────────────────────────────────────

        public class QueueResult
        {
            public bool             Ok;
            public string           Error;
            public long             RoundtripMs;
            public QueueResponseDto Body;
        }

        public async Task<QueueResult> JoinQueueAsync(string userId)
        {
            if (!ServerConfig.Enabled) return new QueueResult { Ok = false, Error = "Network disabled" };
            string url = ServerConfig.BaseUrl.TrimEnd('/') + "/api/pvp/queue/join";
            var (ok, err, body, rt) = await PostJsonAsync<QueueResponseDto>(url, new QueueRequestDto { userId = userId });
            return new QueueResult { Ok = ok, Error = err, Body = body, RoundtripMs = rt };
        }

        public async Task<QueueResult> LeaveQueueAsync(string userId)
        {
            if (!ServerConfig.Enabled) return new QueueResult { Ok = false, Error = "Network disabled" };
            string url = ServerConfig.BaseUrl.TrimEnd('/') + "/api/pvp/queue/leave";
            var (ok, err, body, rt) = await PostJsonAsync<QueueResponseDto>(url, new QueueRequestDto { userId = userId });
            return new QueueResult { Ok = ok, Error = err, Body = body, RoundtripMs = rt };
        }

        public async Task<QueueResult> GetQueueStatusAsync(string userId)
        {
            if (!ServerConfig.Enabled) return new QueueResult { Ok = false, Error = "Network disabled" };
            string url = ServerConfig.BaseUrl.TrimEnd('/') + "/api/pvp/queue/status?userId=" + UnityWebRequest.EscapeURL(userId);
            var (ok, err, body, rt) = await GetJsonAsync<QueueResponseDto>(url);
            return new QueueResult { Ok = ok, Error = err, Body = body, RoundtripMs = rt };
        }

        // ── Generic JSON helpers ────────────────────────────────────────────────

        async Task<(bool ok, string err, T body, long rt)> PostJsonAsync<T>(string url, object request)
            where T : class
        {
            string json = JsonConvert.SerializeObject(request);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            using (var req = new UnityWebRequest(url, "POST"))
            {
                req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = ServerConfig.TimeoutSeconds;
                await SendAsync(req);
                sw.Stop();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    string body = req.downloadHandler?.text ?? "";
                    string err = $"{req.result} ({req.responseCode}): {req.error} body={body.Substring(0, System.Math.Min(200, body.Length))}";
                    return (false, err, null, sw.ElapsedMilliseconds);
                }
                try
                {
                    var body = JsonConvert.DeserializeObject<T>(req.downloadHandler.text);
                    return (true, null, body, sw.ElapsedMilliseconds);
                }
                catch (Exception e) { return (false, "Parse: " + e.Message, null, sw.ElapsedMilliseconds); }
            }
        }

        async Task<(bool ok, string err, T body, long rt)> GetJsonAsync<T>(string url)
            where T : class
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            using (var req = UnityWebRequest.Get(url))
            {
                req.timeout = ServerConfig.TimeoutSeconds;
                await SendAsync(req);
                sw.Stop();
                if (req.result != UnityWebRequest.Result.Success)
                    return (false, $"{req.result} ({req.responseCode}): {req.error}", null, sw.ElapsedMilliseconds);
                try
                {
                    var body = JsonConvert.DeserializeObject<T>(req.downloadHandler.text);
                    return (true, null, body, sw.ElapsedMilliseconds);
                }
                catch (Exception e) { return (false, "Parse: " + e.Message, null, sw.ElapsedMilliseconds); }
            }
        }

        // ── SubmissionQueue Flush ───────────────────────────────────────────────

        static async Task FlushSubmissionQueueSafe()
        {
            try
            {
                int n = await SubmissionQueue.FlushAsync();
                if (n > 0)
                    Debug.Log($"[NetworkClient] SubmissionQueue flushed {n} record(s). Remaining={SubmissionQueue.Count()}");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[NetworkClient] FlushSubmissionQueue failed: " + e.Message);
            }
        }

        // ── FetchLeaderboard ────────────────────────────────────────────────────

        public class LeaderboardResult
        {
            public bool                   Ok;
            public string                 Error;
            public long                   RoundtripMs;
            public LeaderboardResponseDto Body;
        }

        public async Task<LeaderboardResult> FetchLeaderboardAsync(string songId, string difficulty, int limit = 10)
        {
            if (!ServerConfig.Enabled)
                return new LeaderboardResult { Ok = false, Error = "Network disabled in ServerConfig" };
            if (string.IsNullOrEmpty(songId) || string.IsNullOrEmpty(difficulty))
                return new LeaderboardResult { Ok = false, Error = "songId/difficulty is empty" };

            string url = ServerConfig.BaseUrl.TrimEnd('/')
                       + $"/api/leaderboard/{UnityWebRequest.EscapeURL(songId)}/{UnityWebRequest.EscapeURL(difficulty)}?limit={limit}";
            var sw = System.Diagnostics.Stopwatch.StartNew();

            using (var req = UnityWebRequest.Get(url))
            {
                req.timeout = ServerConfig.TimeoutSeconds;
                await SendAsync(req);
                sw.Stop();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    string err = $"{req.result} ({req.responseCode}): {req.error}";
                    Debug.LogWarning("[NetworkClient] Leaderboard failed: " + err);
                    return new LeaderboardResult { Ok = false, Error = err, RoundtripMs = sw.ElapsedMilliseconds };
                }

                try
                {
                    var body = JsonConvert.DeserializeObject<LeaderboardResponseDto>(req.downloadHandler.text);
                    Debug.Log($"[NetworkClient] Leaderboard rt={sw.ElapsedMilliseconds}ms total={body?.total} entries={body?.entries?.Count ?? 0}");
                    return new LeaderboardResult { Ok = true, Body = body, RoundtripMs = sw.ElapsedMilliseconds };
                }
                catch (Exception e)
                {
                    return new LeaderboardResult { Ok = false, Error = "Parse error: " + e.Message, RoundtripMs = sw.ElapsedMilliseconds };
                }
            }
        }

        // ── FetchPersonalBest ───────────────────────────────────────────────────

        public class PersonalBestResult
        {
            public bool                    Ok;
            public string                  Error;
            public long                    RoundtripMs;
            public PersonalBestResponseDto Body;
        }

        public async Task<PersonalBestResult> FetchPersonalBestAsync(string songId, string difficulty, string userId)
        {
            if (!ServerConfig.Enabled)
                return new PersonalBestResult { Ok = false, Error = "Network disabled in ServerConfig" };
            if (string.IsNullOrEmpty(songId) || string.IsNullOrEmpty(difficulty) || string.IsNullOrEmpty(userId))
                return new PersonalBestResult { Ok = false, Error = "songId/difficulty/userId is empty" };

            string url = ServerConfig.BaseUrl.TrimEnd('/')
                       + $"/api/leaderboard/{UnityWebRequest.EscapeURL(songId)}/{UnityWebRequest.EscapeURL(difficulty)}/me?userId={UnityWebRequest.EscapeURL(userId)}";
            var sw = System.Diagnostics.Stopwatch.StartNew();

            using (var req = UnityWebRequest.Get(url))
            {
                req.timeout = ServerConfig.TimeoutSeconds;
                await SendAsync(req);
                sw.Stop();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    string err = $"{req.result} ({req.responseCode}): {req.error}";
                    return new PersonalBestResult { Ok = false, Error = err, RoundtripMs = sw.ElapsedMilliseconds };
                }
                try
                {
                    var body = JsonConvert.DeserializeObject<PersonalBestResponseDto>(req.downloadHandler.text);
                    return new PersonalBestResult { Ok = true, Body = body, RoundtripMs = sw.ElapsedMilliseconds };
                }
                catch (Exception e)
                {
                    return new PersonalBestResult { Ok = false, Error = "Parse error: " + e.Message, RoundtripMs = sw.ElapsedMilliseconds };
                }
            }
        }

        // ── UnityWebRequest → Task ──────────────────────────────────────────────

        static Task SendAsync(UnityWebRequest req)
        {
            var tcs = new TaskCompletionSource<bool>();
            var op  = req.SendWebRequest();
            op.completed += _ => tcs.TrySetResult(true);
            return tcs.Task;
        }
    }
}
