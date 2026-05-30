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
        /// <summary>楽曲ID。</summary>
        public readonly string SongId;
        /// <summary>難易度 (easy / normal / hard / extra)。</summary>
        public readonly string Difficulty;
        /// <summary>表示譜面レベル(UI 並び替え用)。</summary>
        public readonly int    Level;

        /// <summary>楽曲ID・難易度・レベルを指定してプールエントリを生成する。</summary>
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
        /// <summary>シーズン識別子。</summary>
        public string                       SeasonId { get; }
        /// <summary>プール内の楽曲エントリ一覧。</summary>
        public IReadOnlyList<MatchPoolEntry> Entries  { get; }

        /// <summary>シーズンIDとエントリ一覧からマッチプールを生成する(null は空に正規化)。</summary>
        public MatchPool(string seasonId, IReadOnlyList<MatchPoolEntry> entries)
        {
            SeasonId = seasonId ?? "";
            Entries  = entries  ?? new MatchPoolEntry[0];
        }

        /// <summary>
        /// 暫定の固定プール (Phase 5 検証用、サンプル 20 曲)。
        /// sample_01..sample_20 は StreamingAssets/Songs に test_song を複製した同一譜面 (chartHash 共通)。
        /// 設計書の「マッチングプール: 20曲固定」を満たすための仮データ。本コンテンツ整備時に差替予定。
        /// </summary>
        public static MatchPool CreateBootstrapPool()
        {
            var list = new List<MatchPoolEntry>(20);
            for (int i = 1; i <= 20; i++)
                list.Add(new MatchPoolEntry($"sample_{i:00}", "extra", 10));
            return new MatchPool("bootstrap_2026Q2", list);
        }
    }
}
