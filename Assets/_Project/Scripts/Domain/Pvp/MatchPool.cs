using System.Collections.Generic;

// Unity-independent. No UnityEngine references allowed in this assembly.
namespace Domain.Pvp
{
    /// <summary>
    /// PVP マッチで使用される 1 曲のエントリ。
    /// 設計書: 「マッチングプール: 20曲固定」「セクター動的選曲」
    /// </summary>
    public readonly struct MatchPoolEntry
    {
        public readonly string SongId;
        public readonly string Difficulty;     // easy / normal / hard / extra
        public readonly int    Level;          // 表示譜面レベル (UI 並び替え用)

        public MatchPoolEntry(string songId, string difficulty, int level)
        {
            SongId     = songId;
            Difficulty = difficulty;
            Level      = level;
        }
    }

    /// <summary>
    /// 現在シーズンのマッチプール。
    /// 初期実装は固定リスト (test_song 系)。後段でサーバー JSON からロードに切替予定。
    /// </summary>
    public sealed class MatchPool
    {
        public string                       SeasonId { get; }
        public IReadOnlyList<MatchPoolEntry> Entries  { get; }

        public MatchPool(string seasonId, IReadOnlyList<MatchPoolEntry> entries)
        {
            SeasonId = seasonId ?? "";
            Entries  = entries  ?? new MatchPoolEntry[0];
        }

        /// <summary>暫定の固定プール (Phase 5 初期検証用、test_song 系 4 曲のみ)。</summary>
        public static MatchPool CreateBootstrapPool()
        {
            var list = new List<MatchPoolEntry>
            {
                new MatchPoolEntry("test_song",   "extra", 10),
                new MatchPoolEntry("test_song_1", "extra", 10),
                new MatchPoolEntry("test_song_2", "extra", 10),
                new MatchPoolEntry("test_song_3", "extra", 10),
            };
            return new MatchPool("bootstrap_2026Q2", list);
        }
    }
}
