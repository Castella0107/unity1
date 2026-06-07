using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace RhythmGame.Network.Api
{
    /// <summary>
    /// 試合中 WebSocket クライアント (docs/06_api_websocket.md v1.0 準拠、Go サーバー移行 M3)。
    ///   接続: wss://host/ws/match/{match_id} → 最初のメッセージで {"type":"auth","token"} (5秒以内)
    ///   受信: auth_ok / ready_check / ready_ack / start / opponent_progress / pong / error(auth_error)
    ///   送信: ready / progress(0.5秒毎・呼び出し側制御) / ping(15秒毎・自動)
    /// イベントはメインスレッドにディスパッチされる (MainThreadDispatcher)。
    /// 切断ポリシー (draft 30s グレース / play 中=表示専用) はサーバー側挙動。再接続は呼び出し側が
    /// 新しいインスタンスで ConnectAsync し直す (同じ match_id / JWT でルーム復帰可)。
    /// </summary>
    public class MatchSocketClient : IDisposable
    {
        /// <summary>ready_check 受信 (引数=deadline RFC3339)。</summary>
        public event Action<string> OnReadyCheck;
        /// <summary>ready_ack 受信 (引数=ready したプレイヤーの user_id)。両者にブロードキャストされる。</summary>
        public event Action<string> OnReadyAck;
        /// <summary>start 受信 (両者 ready 完了、pre_match → pick_song1)。</summary>
        public event Action OnStart;
        /// <summary>opponent_progress 受信 (song_order, percent_x1000, score)。</summary>
        public event Action<int, int, long> OnOpponentProgress;
        /// <summary>サーバーからの error / auth_error (code, message)。多くは直後に切断される。</summary>
        public event Action<string, string> OnSocketError;
        /// <summary>接続クローズ (正常・異常問わず受信ループ終了時に1回)。</summary>
        public event Action OnClosed;

        /// <summary>auth_ok で通知された現在フェーズ。</summary>
        public string PhaseAtConnect { get; private set; }
        /// <summary>接続中か。</summary>
        public bool IsConnected => _ws != null && _ws.State == WebSocketState.Open;

        const int AuthTimeoutMs   = 5000;
        const int PingIntervalMs  = 15000;

        ClientWebSocket _ws;
        CancellationTokenSource _cts;
        TaskCompletionSource<bool> _authTcs;
        readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        bool _closedNotified;

        // ── 接続 ─────────────────────────────────────────────────────────────

        /// <summary>接続+認証ハンドシェイクまで行う。成功で true。</summary>
        public async Task<bool> ConnectAsync(string matchId)
        {
            string token = await AuthManager.EnsureValidAccessTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                Debug.LogWarning("[MatchSocket] 未ログインのため接続不可");
                return false;
            }

            string baseUrl = ServerConfig.BaseUrl.TrimEnd('/');
            string wsUrl = baseUrl.StartsWith("https") ? "wss" + baseUrl.Substring(5)
                         : baseUrl.StartsWith("http")  ? "ws"  + baseUrl.Substring(4)
                         : baseUrl;
            wsUrl += "/ws/match/" + matchId;

            _ws  = new ClientWebSocket();
            _cts = new CancellationTokenSource();
            _authTcs = new TaskCompletionSource<bool>();

            try
            {
                await _ws.ConnectAsync(new Uri(wsUrl), _cts.Token);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MatchSocket] 接続失敗: {e.Message}");
                return false;
            }

            _ = Task.Run(ReceiveLoopAsync);

            // 認証 (最初のメッセージ・5秒以内)
            await SendJsonAsync(new JObject { ["type"] = "auth", ["token"] = token });

            var timeout = Task.Delay(AuthTimeoutMs);
            var done = await Task.WhenAny(_authTcs.Task, timeout);
            if (done == timeout || !_authTcs.Task.Result)
            {
                Debug.LogWarning("[MatchSocket] 認証失敗/タイムアウト");
                await CloseAsync();
                return false;
            }

            _ = Task.Run(PingLoopAsync);
            Debug.Log($"[MatchSocket] connected (phase={PhaseAtConnect})");
            return true;
        }

        // ── 送信 ─────────────────────────────────────────────────────────────

        /// <summary>準備完了を通知する。</summary>
        public Task SendReadyAsync()
            => SendJsonAsync(new JObject { ["type"] = "ready" });

        /// <summary>プレイ中の進捗を送信する (0.5秒間隔は呼び出し側が制御)。</summary>
        public Task SendProgressAsync(int songOrder, int percentX1000, long score)
            => SendJsonAsync(new JObject
            {
                ["type"]          = "progress",
                ["song_order"]    = songOrder,
                ["percent_x1000"] = percentX1000,
                ["score"]         = score,
            });

        async Task SendJsonAsync(JObject obj)
        {
            if (_ws == null || _ws.State != WebSocketState.Open) return;
            var bytes = Encoding.UTF8.GetBytes(obj.ToString(Newtonsoft.Json.Formatting.None));
            await _sendLock.WaitAsync();
            try
            {
                if (_ws.State == WebSocketState.Open)
                    await _ws.SendAsync(new ArraySegment<byte>(bytes),
                        WebSocketMessageType.Text, true, _cts.Token);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MatchSocket] 送信失敗: {e.Message}");
            }
            finally
            {
                _sendLock.Release();
            }
        }

        // ── 受信ループ ────────────────────────────────────────────────────────

        async Task ReceiveLoopAsync()
        {
            var buffer = new byte[16 * 1024];
            var message = new StringBuilder();

            try
            {
                while (_ws.State == WebSocketState.Open && !_cts.IsCancellationRequested)
                {
                    message.Clear();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            NotifyClosed();
                            return;
                        }
                        message.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    } while (!result.EndOfMessage);

                    HandleMessage(message.ToString());
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                if (!_cts.IsCancellationRequested)
                    Debug.LogWarning($"[MatchSocket] 受信ループ終了: {e.Message}");
            }
            NotifyClosed();
        }

        void HandleMessage(string json)
        {
            JObject msg;
            try { msg = JObject.Parse(json); }
            catch { Debug.LogWarning("[MatchSocket] 不正なメッセージ: " + json); return; }

            string type = msg.Value<string>("type") ?? "";
            switch (type)
            {
                case "auth_ok":
                    PhaseAtConnect = msg.Value<string>("phase");
                    _authTcs?.TrySetResult(true);
                    break;

                case "auth_error":   // K ガイド表記 (docs/06 では "error")
                case "error":
                {
                    string code = msg.Value<string>("code") ?? type;
                    string text = msg.Value<string>("message") ?? "";
                    if (_authTcs != null && !_authTcs.Task.IsCompleted)
                        _authTcs.TrySetResult(false);
                    Dispatch(() => OnSocketError?.Invoke(code, text));
                    break;
                }

                case "ready_check":
                {
                    string deadline = msg.Value<string>("deadline");
                    Dispatch(() => OnReadyCheck?.Invoke(deadline));
                    break;
                }

                case "ready_ack":
                {
                    string userId = msg.Value<string>("user_id");
                    Dispatch(() => OnReadyAck?.Invoke(userId));
                    break;
                }

                case "start":
                    Dispatch(() => OnStart?.Invoke());
                    break;

                case "opponent_progress":
                {
                    int  songOrder = msg.Value<int?>("song_order") ?? 0;
                    int  percent   = msg.Value<int?>("percent_x1000") ?? 0;
                    long score     = msg.Value<long?>("score") ?? 0;
                    Dispatch(() => OnOpponentProgress?.Invoke(songOrder, percent, score));
                    break;
                }

                case "pong":
                    break;

                default:
                    Debug.Log("[MatchSocket] 未知のメッセージ型: " + type);
                    break;
            }
        }

        async Task PingLoopAsync()
        {
            try
            {
                while (_ws != null && _ws.State == WebSocketState.Open && !_cts.IsCancellationRequested)
                {
                    await Task.Delay(PingIntervalMs, _cts.Token);
                    await SendJsonAsync(new JObject { ["type"] = "ping" });
                }
            }
            catch (OperationCanceledException) { }
        }

        // ── クローズ ──────────────────────────────────────────────────────────

        /// <summary>接続を閉じる (多重呼び出し安全)。</summary>
        public async Task CloseAsync()
        {
            try
            {
                if (_ws != null && (_ws.State == WebSocketState.Open || _ws.State == WebSocketState.CloseReceived))
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
            }
            catch { /* close中の例外は握る */ }
            _cts?.Cancel();
        }

        void NotifyClosed()
        {
            if (_closedNotified) return;
            _closedNotified = true;
            Dispatch(() => OnClosed?.Invoke());
        }

        // 受信スレッド → メインスレッドへ。直接実行は Unity API のスレッド制約で
        // 受信ループごと死ぬため、Dispatcher 不在時は破棄してエラーを出す (Bootstrap 済みなら起きない)。
        static void Dispatch(Action action)
        {
            var d = MainThreadDispatcher.Instance;
            if (d != null) d.Enqueue(action);
            else Debug.LogError("[MatchSocket] MainThreadDispatcher 不在 — イベントを破棄しました");
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _ws?.Dispose();
            _cts?.Dispose();
        }
    }
}
