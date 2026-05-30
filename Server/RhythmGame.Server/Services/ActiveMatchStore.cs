using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Domain.Pvp;

namespace RhythmGame.Server.Services
{
    /// <summary>
    /// 進行中の PVP 試合をプロセス内に保持する singleton。
    /// 試合が確定 (両プレイヤー submit 完了) すると DB の MatchEntity に永続化し、
    /// ここからは除去される。プロセス再起動でロストする (短命データのため OK)。
    /// </summary>
    public class ActiveMatchStore
    {
        public class SongPick
        {
            public string SongId     { get; set; } = "";
            public string Difficulty { get; set; } = "";
        }

        public class PlayerSubmission
        {
            public string  UserId         { get; set; } = "";
            public bool    Submitted      { get; set; }
            public int[][] SectorScores   { get; set; } // [songIndex][sectorIndex 0..4]
            public string  Error          { get; set; } = "";
        }

        public class PlayerProgress
        {
            public int  SongIndex       { get; set; }  // 0..N
            public int  PercentX1000    { get; set; }  // 0..100000 (パーセント×1000で整数化)
            public int  Score           { get; set; }
            public long UpdatedAtUnixMs { get; set; }
        }

        public class ActiveMatch
        {
            public string         MatchId         { get; set; } = "";
            public string         UserIdA         { get; set; } = "";
            public string         UserIdB         { get; set; } = "";
            public List<SongPick> Songs           { get; set; } = new();
            public PlayerSubmission SubmissionA   { get; set; } = new();
            public PlayerSubmission SubmissionB   { get; set; } = new();
            public PlayerProgress   ProgressA     { get; set; } = new();
            public PlayerProgress   ProgressB     { get; set; } = new();
            public long           CreatedAtUnixMs { get; set; }
            public bool           Finalized       { get; set; }
            public string         CompletedMatchId{ get; set; } = ""; // DB に保存した MatchEntity の MatchId (= MatchId と同一)

            // ── Draft (PICK/BAN) state ──────────────────────────────────────────
            // ルール: A/B が各自プールから1曲ブラインドPICK → 残プールから3曲をBAN候補抽選 →
            //         A/B が候補から各自1曲ブラインドBAN → 残った1曲(BAN重複時は残2曲からランダム)が3曲目。
            //         試合3曲 = [PickA, PickB, 3曲目]。DraftDone までは Songs は空。
            public string         PickA           { get; set; } = "";
            public string         PickB           { get; set; } = "";
            public List<string>   BanCandidates   { get; set; } = new();   // 両PICK後に確定 (3件)
            public string         BanA            { get; set; } = "";
            public string         BanB            { get; set; } = "";
            public bool           DraftDone       { get; set; }            // Songs 確定済みなら true
        }

        private readonly ConcurrentDictionary<string, ActiveMatch> _matches = new();
        private readonly object _draftLock = new();

        /// <summary>ドラフトのフェーズ。</summary>
        public enum DraftPhase { Pick, Ban, Done }

        public ActiveMatch Create(string userIdA, string userIdB, List<SongPick> songs)
        {
            var m = new ActiveMatch
            {
                MatchId         = Guid.NewGuid().ToString(),
                UserIdA         = userIdA,
                UserIdB         = userIdB,
                Songs           = songs ?? new List<SongPick>(),
                SubmissionA     = new PlayerSubmission { UserId = userIdA },
                SubmissionB     = new PlayerSubmission { UserId = userIdB },
                CreatedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                // 曲を渡して作成 = ドラフト省略 (debug create)。空で作成 = ドラフト開始 (queue)。
                DraftDone       = songs != null && songs.Count > 0,
            };
            _matches[m.MatchId] = m;
            return m;
        }

        // ── Draft 操作 (per-match の競合は _draftLock で直列化) ────────────────────

        public static DraftPhase GetPhase(ActiveMatch m)
        {
            if (m.DraftDone) return DraftPhase.Done;
            if (string.IsNullOrEmpty(m.PickA) || string.IsNullOrEmpty(m.PickB)) return DraftPhase.Pick;
            return DraftPhase.Ban;
        }

        /// <summary>PICK を適用。両者PICK完了で BAN 候補3曲を抽選する。</summary>
        public (bool ok, string err) ApplyPick(ActiveMatch m, string userId, string songId,
                                               IReadOnlyList<MatchPoolEntry> pool)
        {
            lock (_draftLock)
            {
                if (m.DraftDone) return (false, "draft already done");
                if (!pool.Any(e => e.SongId == songId)) return (false, "songId not in pool");
                if (!string.IsNullOrEmpty(m.PickA) && !string.IsNullOrEmpty(m.PickB)) return (false, "pick phase over");

                if (userId == m.UserIdA) { if (!string.IsNullOrEmpty(m.PickA)) return (false, "already picked"); m.PickA = songId; }
                else if (userId == m.UserIdB) { if (!string.IsNullOrEmpty(m.PickB)) return (false, "already picked"); m.PickB = songId; }
                else return (false, "user not in match");

                // 両PICK完了 → BAN候補抽選 (PICK済みを除いた残プールから3曲)
                if (!string.IsNullOrEmpty(m.PickA) && !string.IsNullOrEmpty(m.PickB) && m.BanCandidates.Count == 0)
                {
                    var picked = new HashSet<string> { m.PickA, m.PickB };
                    var remaining = pool.Select(e => e.SongId).Where(s => !picked.Contains(s)).Distinct().ToList();
                    var rng = new Random();
                    m.BanCandidates = remaining.OrderBy(_ => rng.Next()).Take(3).ToList();
                }
                return (true, "");
            }
        }

        /// <summary>BAN を適用。両者BAN完了で3曲目を解決し Songs を確定する。</summary>
        public (bool ok, string err) ApplyBan(ActiveMatch m, string userId, string songId,
                                              IReadOnlyList<MatchPoolEntry> pool)
        {
            lock (_draftLock)
            {
                if (m.DraftDone) return (false, "draft already done");
                if (m.BanCandidates.Count < 3) return (false, "ban phase not ready (picks incomplete)");
                if (!m.BanCandidates.Contains(songId)) return (false, "songId not a ban candidate");

                if (userId == m.UserIdA) { if (!string.IsNullOrEmpty(m.BanA)) return (false, "already banned"); m.BanA = songId; }
                else if (userId == m.UserIdB) { if (!string.IsNullOrEmpty(m.BanB)) return (false, "already banned"); m.BanB = songId; }
                else return (false, "user not in match");

                // 両BAN完了 → 3曲目を解決
                if (!string.IsNullOrEmpty(m.BanA) && !string.IsNullOrEmpty(m.BanB))
                {
                    var survivors = m.BanCandidates.Where(s => s != m.BanA && s != m.BanB).ToList();
                    string third;
                    if (survivors.Count == 1) third = survivors[0];            // BAN が割れた
                    else                                                        // BAN かぶり → 残2曲からランダム
                    {
                        var rng = new Random();
                        third = survivors[rng.Next(survivors.Count)];
                    }
                    string Diff(string sid) => pool.FirstOrDefault(e => e.SongId == sid).Difficulty ?? "extra";
                    m.Songs = new List<SongPick>
                    {
                        new SongPick { SongId = m.PickA, Difficulty = Diff(m.PickA) },
                        new SongPick { SongId = m.PickB, Difficulty = Diff(m.PickB) },
                        new SongPick { SongId = third,   Difficulty = Diff(third)   },
                    };
                    m.DraftDone = true;
                }
                return (true, "");
            }
        }

        public ActiveMatch TryGet(string matchId)
        {
            if (string.IsNullOrEmpty(matchId)) return null;
            _matches.TryGetValue(matchId, out var m);
            return m;
        }

        public bool Remove(string matchId)
        {
            return _matches.TryRemove(matchId, out _);
        }
    }
}
