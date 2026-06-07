using System;
using System.Threading.Tasks;
using UnityEngine;

namespace RhythmGame.Network.Api
{
    /// <summary>
    /// Go サーバー (PVPharmonics) の JWT 認証セッション管理。
    ///   - アクセストークン(1h) + リフレッシュトークン(30日, Rotation) を PlayerPrefs に永続化
    ///   - <see cref="EnsureValidAccessTokenAsync"/> が期限前リフレッシュを面倒見る (ApiClient から呼ばれる)
    ///   - refresh は単一飛行 (並行呼び出しは同じ Task を待つ — Rotation のトークン競合防止)
    /// オフラインモード (<see cref="OfflineMode"/>) はセッション中のみ有効でソロプレイ用。
    /// </summary>
    public static class AuthManager
    {
        const string KeyAccess    = "Auth_AccessToken";
        const string KeyRefresh   = "Auth_RefreshToken";
        const string KeyUserId    = "Auth_UserId";
        const string KeyName      = "Auth_DisplayName";
        const string KeyEmail     = "Auth_Email";
        const string KeyExpiresAt = "Auth_ExpiresAtUnixMs";

        /// <summary>期限のこの秒数前になったらリフレッシュする。</summary>
        const int RefreshMarginSec = 60;

        static Task<bool> _refreshInFlight;

        /// <summary>「オフラインで続行」中か (セッション限り・非永続)。ソロのみプレイ可。</summary>
        public static bool OfflineMode { get; set; }

        public static string AccessToken  => PlayerPrefs.GetString(KeyAccess, "");
        public static string RefreshToken => PlayerPrefs.GetString(KeyRefresh, "");
        public static string UserId       => PlayerPrefs.GetString(KeyUserId, "");
        public static string DisplayName  => PlayerPrefs.GetString(KeyName, "");
        public static string Email        => PlayerPrefs.GetString(KeyEmail, "");

        static long ExpiresAtUnixMs
        {
            get => long.TryParse(PlayerPrefs.GetString(KeyExpiresAt, "0"), out var v) ? v : 0;
            set { PlayerPrefs.SetString(KeyExpiresAt, value.ToString()); }
        }

        /// <summary>保存済みセッション (リフレッシュトークン) があるか。起動時の自動ログイン判定に使う。</summary>
        public static bool HasSession => !string.IsNullOrEmpty(RefreshToken);

        /// <summary>アクセストークンが現在有効か (期限マージン込み)。</summary>
        public static bool HasValidAccessToken =>
            !string.IsNullOrEmpty(AccessToken) &&
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() < ExpiresAtUnixMs - RefreshMarginSec * 1000L;

        // ── 認証フロー ───────────────────────────────────────────────────────

        /// <summary>新規登録。成功時はセッションを保存してログイン状態になる。</summary>
        public static async Task<ApiResult<AuthResponseDto>> RegisterAsync(string email, string password, string displayName)
        {
            var r = await ApiClient.PostAsync<AuthResponseDto>("/auth/register",
                new RegisterRequestDto { Email = email, Password = password, DisplayName = displayName },
                auth: false);
            if (r.Ok && r.Data != null) StoreSession(r.Data, email);
            return r;
        }

        /// <summary>ログイン。成功時はセッションを保存する。</summary>
        public static async Task<ApiResult<AuthResponseDto>> LoginAsync(string email, string password)
        {
            var r = await ApiClient.PostAsync<AuthResponseDto>("/auth/login",
                new LoginRequestDto { Email = email, Password = password },
                auth: false);
            if (r.Ok && r.Data != null) StoreSession(r.Data, email);
            return r;
        }

        /// <summary>ログアウト。サーバーのリフレッシュトークンを失効させ、ローカルセッションを破棄する。</summary>
        public static async Task LogoutAsync()
        {
            var refresh = RefreshToken;
            if (!string.IsNullOrEmpty(refresh))
                await ApiClient.PostAsync<EmptyDto>("/auth/logout",
                    new LogoutRequestDto { RefreshToken = refresh }, auth: false);
            ClearSession();
        }

        /// <summary>
        /// リフレッシュトークンでアクセストークンを更新する (Rotation: 両トークン更新)。
        /// 並行呼び出しは同一 Task を共有する。リフレッシュ失敗 (失効) 時はセッションを破棄して false。
        /// </summary>
        public static Task<bool> TryRefreshAsync()
        {
            if (_refreshInFlight != null) return _refreshInFlight;
            _refreshInFlight = RefreshCoreAsync();
            return _refreshInFlight;
        }

        static async Task<bool> RefreshCoreAsync()
        {
            try
            {
                var refresh = RefreshToken;
                if (string.IsNullOrEmpty(refresh)) return false;

                var r = await ApiClient.PostAsync<AuthResponseDto>("/auth/refresh",
                    new RefreshRequestDto { RefreshToken = refresh }, auth: false);
                if (r.Ok && r.Data != null)
                {
                    StoreTokens(r.Data.AccessToken, r.Data.RefreshToken, r.Data.ExpiresIn);
                    Debug.Log("[AuthManager] token refreshed");
                    return true;
                }

                // 401 = リフレッシュトークン失効 → 要再ログイン。通信エラー時はセッション保持。
                if (r.Status == 401)
                {
                    Debug.LogWarning("[AuthManager] refresh token expired — session cleared");
                    ClearSession();
                }
                return false;
            }
            finally
            {
                _refreshInFlight = null;
            }
        }

        /// <summary>
        /// 有効なアクセストークンを返す (必要ならリフレッシュ)。取得不能なら null。
        /// ApiClient の認証付きリクエストから毎回呼ばれる。
        /// </summary>
        public static async Task<string> EnsureValidAccessTokenAsync()
        {
            if (OfflineMode) return null;
            if (HasValidAccessToken) return AccessToken;
            if (!HasSession) return null;
            return await TryRefreshAsync() ? AccessToken : null;
        }

        // ── セッション保存 ───────────────────────────────────────────────────

        static void StoreSession(AuthResponseDto data, string emailFallback)
        {
            PlayerPrefs.SetString(KeyUserId, data.UserId ?? "");
            PlayerPrefs.SetString(KeyName,   data.DisplayName ?? "");
            PlayerPrefs.SetString(KeyEmail,  string.IsNullOrEmpty(data.Email) ? (emailFallback ?? "") : data.Email);
            StoreTokens(data.AccessToken, data.RefreshToken, data.ExpiresIn);
            OfflineMode = false;
            Debug.Log($"[AuthManager] session stored user={data.UserId}");
        }

        static void StoreTokens(string access, string refresh, int expiresInSec)
        {
            PlayerPrefs.SetString(KeyAccess,  access ?? "");
            PlayerPrefs.SetString(KeyRefresh, refresh ?? "");
            ExpiresAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + (long)expiresInSec * 1000L;
            PlayerPrefs.Save();
        }

        /// <summary>ローカルセッションを破棄する。</summary>
        public static void ClearSession()
        {
            PlayerPrefs.DeleteKey(KeyAccess);
            PlayerPrefs.DeleteKey(KeyRefresh);
            PlayerPrefs.DeleteKey(KeyUserId);
            PlayerPrefs.DeleteKey(KeyName);
            PlayerPrefs.DeleteKey(KeyEmail);
            PlayerPrefs.DeleteKey(KeyExpiresAt);
            PlayerPrefs.Save();
        }
    }
}
