using UnityEngine;

namespace RhythmGame.Network
{
    /// <summary>
    /// 認証なしの簡易ローカル識別。AccountTabController が PlayerPrefs に保存する
    /// "DisplayName" を sanitize/truncate して UserId として返す。
    /// 空 / 空白のみの場合は "anon" にフォールバック。
    /// 後段 Phase 4-B で本格認証 (Discord OAuth + JWT) に置き換える際、ここを差替えるだけで
    /// 既存の Auto-Submit / Leaderboard / DebugNetworkOverlay の参照を維持できる。
    /// </summary>
    public static class LocalIdentity
    {
        /// <summary>表示名を保存する PlayerPrefs キー(AccountTabController と共通)。</summary>
        public const string PrefKey  = "DisplayName";
        /// <summary>未設定時のフォールバックユーザー名。</summary>
        public const string Fallback = "anon";
        /// <summary>UserId の最大長(サーバー側 VARCHAR(64) の半分)。</summary>
        public const int    MaxLen   = 32;

        /// <summary>サーバー送信用に正規化された UserId。常に非空文字列を返す。</summary>
        public static string UserId
        {
            get
            {
                string raw = PlayerPrefs.GetString(PrefKey, "");
                return Sanitize(raw);
            }
        }

        /// <summary>表示名を正規化する(trim + 最大長切り詰め、空なら "anon")。</summary>
        public static string Sanitize(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return Fallback;
            string t = raw.Trim();
            if (t.Length == 0) return Fallback;
            if (t.Length > MaxLen) t = t.Substring(0, MaxLen);
            return t;
        }
    }
}
