using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace RhythmGame.Network.Api
{
    /// <summary>API 呼び出し結果。Ok=false のとき ErrorCode/ErrorMessage が入る。</summary>
    public class ApiResult<T>
    {
        public bool   Ok;
        public T      Data;
        public string ErrorCode;      // サーバーのエラーコード or クライアント側 "NETWORK_ERROR" 等
        public string ErrorMessage;   // 日本語メッセージ (サーバー返却 or クライアント生成)
        public long   Status;         // HTTP ステータス (通信不能時は 0)
        public long   RoundtripMs;

        public static ApiResult<T> Fail(string code, string message, long status = 0) =>
            new ApiResult<T> { Ok = false, ErrorCode = code, ErrorMessage = message, Status = status };
    }

    /// <summary>
    /// Go サーバー (PVPharmonics) /api/v1 共通クライアント (docs/05_api_rest.md §4 準拠)。
    ///   - ベース URL: <see cref="ServerConfig.BaseUrl"/> + "/api/v1"
    ///   - 封筒: 成功 {"data":...} / 失敗 {"error":{code,message}}
    ///   - 認証: Bearer JWT (<see cref="AuthManager"/>)、401 で refresh→1回だけ再試行
    ///   - 429: Retry-After をエラーメッセージに含めて返す (自動リトライはしない)
    /// メインスレッドから呼ぶこと (UnityWebRequest 制約)。
    /// </summary>
    public static class ApiClient
    {
        static string ApiBase => ServerConfig.BaseUrl.TrimEnd('/') + "/api/v1";

        // ── Public API ───────────────────────────────────────────────────────

        public static Task<ApiResult<T>> GetAsync<T>(string path, bool auth = true)
            => SendAsync<T>(UnityWebRequest.kHttpVerbGET, path, null, auth, allowAuthRetry: true);

        public static Task<ApiResult<TRes>> PostAsync<TRes>(string path, object body, bool auth = true)
            => SendAsync<TRes>(UnityWebRequest.kHttpVerbPOST, path, body, auth, allowAuthRetry: true);

        public static Task<ApiResult<TRes>> PatchAsync<TRes>(string path, object body, bool auth = true)
            => SendAsync<TRes>("PATCH", path, body, auth, allowAuthRetry: true);

        /// <summary>
        /// 生ファイルダウンロード (封筒なし、譜面ファイル配信用)。302 リダイレクトは UnityWebRequest が自動追従。
        /// </summary>
        public static async Task<ApiResult<byte[]>> DownloadAsync(string path, bool auth = true)
        {
            if (!ServerConfig.Enabled)
                return ApiResult<byte[]>.Fail("NETWORK_DISABLED", "ネットワーク機能が無効です");

            string token = null;
            if (auth)
            {
                token = await AuthManager.EnsureValidAccessTokenAsync();
                if (string.IsNullOrEmpty(token))
                    return ApiResult<byte[]>.Fail("NOT_AUTHENTICATED", "ログインが必要です");
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            using var req = UnityWebRequest.Get(ApiBase + path);
            req.timeout = ServerConfig.TimeoutSeconds * 3;   // ファイル配信は余裕を持つ
            if (auth) req.SetRequestHeader("Authorization", "Bearer " + token);

            await Send(req);
            sw.Stop();

            if (req.result == UnityWebRequest.Result.Success)
                return new ApiResult<byte[]>
                {
                    Ok = true, Data = req.downloadHandler.data,
                    Status = req.responseCode, RoundtripMs = sw.ElapsedMilliseconds,
                };

            Debug.LogWarning($"[ApiClient] GET {path} download failed ({req.responseCode}): {req.error}");
            return ApiResult<byte[]>.Fail("DOWNLOAD_ERROR",
                $"ダウンロードに失敗しました: {req.error}", req.responseCode);
        }

        // ── Core ─────────────────────────────────────────────────────────────

        static async Task<ApiResult<T>> SendAsync<T>(
            string method, string path, object body, bool auth, bool allowAuthRetry)
        {
            if (!ServerConfig.Enabled)
                return ApiResult<T>.Fail("NETWORK_DISABLED", "ネットワーク機能が無効です");

            string token = null;
            if (auth)
            {
                token = await AuthManager.EnsureValidAccessTokenAsync();
                if (string.IsNullOrEmpty(token))
                    return ApiResult<T>.Fail("NOT_AUTHENTICATED", "ログインが必要です");
            }

            string url = ApiBase + path;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            using var req = new UnityWebRequest(url, method);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.timeout = ServerConfig.TimeoutSeconds;
            req.SetRequestHeader("Accept", "application/json");
            if (auth) req.SetRequestHeader("Authorization", "Bearer " + token);
            if (body != null)
            {
                var json = JsonConvert.SerializeObject(body);
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
            }

            await Send(req);
            sw.Stop();

            long status = req.responseCode;

            // 通信レイヤーのエラー (接続不能・タイムアウト等、HTTP 応答なし)
            if (req.result != UnityWebRequest.Result.Success && status == 0)
            {
                Debug.LogWarning($"[ApiClient] {method} {path} network error: {req.error}");
                return ApiResult<T>.Fail("NETWORK_ERROR", "サーバーに接続できません: " + req.error);
            }

            // 401 → refresh して 1 回だけ再試行 (auth エンドポイント自身は対象外)
            if (status == 401 && auth && allowAuthRetry)
            {
                bool refreshed = await AuthManager.TryRefreshAsync();
                if (refreshed)
                    return await SendAsync<T>(method, path, body, auth, allowAuthRetry: false);
            }

            // 204 No Content
            if (status == 204)
                return new ApiResult<T> { Ok = true, Status = status, RoundtripMs = sw.ElapsedMilliseconds };

            string text = req.downloadHandler?.text ?? "";
            ApiEnvelope<T> envelope = null;
            try { envelope = JsonConvert.DeserializeObject<ApiEnvelope<T>>(text); }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ApiClient] {method} {path} parse error ({status}): {e.Message}");
                return ApiResult<T>.Fail("PARSE_ERROR", "サーバー応答の解析に失敗しました", status);
            }

            if (status >= 200 && status < 300 && envelope != null && envelope.Error == null)
            {
                return new ApiResult<T>
                {
                    Ok = true, Data = envelope.Data,
                    Status = status, RoundtripMs = sw.ElapsedMilliseconds,
                };
            }

            string code = envelope?.Error?.Code ?? $"HTTP_{status}";
            string msg  = envelope?.Error?.Message ?? "サーバーエラーが発生しました";
            if (status == 429)
            {
                var retryAfter = req.GetResponseHeader("Retry-After");
                if (!string.IsNullOrEmpty(retryAfter)) msg += $" ({retryAfter}秒後に再試行できます)";
            }
            Debug.LogWarning($"[ApiClient] {method} {path} → {status} {code}: {msg}");
            return new ApiResult<T>
            {
                Ok = false, ErrorCode = code, ErrorMessage = msg,
                Status = status, RoundtripMs = sw.ElapsedMilliseconds,
            };
        }

        static Task Send(UnityWebRequest req)
        {
            var tcs = new TaskCompletionSource<bool>();
            var op  = req.SendWebRequest();
            op.completed += _ => tcs.TrySetResult(true);
            return tcs.Task;
        }
    }
}
