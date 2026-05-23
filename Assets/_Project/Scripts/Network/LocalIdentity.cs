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
        public const string PrefKey  = "DisplayName";       // AccountTabController と共通
        public const string Fallback = "anon";
        public const int    MaxLen   = 32;                  // サーバー PlayRecordEntity.UserId VARCHAR(64) の半分

        /// <summary>サーバー送信用に正規化された UserId。常に非空文字列を返す。</summary>
        public static string UserId
        {
            get
            {
                string raw = PlayerPrefs.GetString(PrefKey, "");
                return Sanitize(raw);
            }
        }

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
