using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

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
        }

        private readonly ConcurrentDictionary<string, ActiveMatch> _matches = new();

        public ActiveMatch Create(string userIdA, string userIdB, List<SongPick> songs)
        {
            var m = new ActiveMatch
            {
                MatchId         = Guid.NewGuid().ToString(),
                UserIdA         = userIdA,
                UserIdB         = userIdB,
                Songs           = songs,
                SubmissionA     = new PlayerSubmission { UserId = userIdA },
                SubmissionB     = new PlayerSubmission { UserId = userIdB },
                CreatedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };
            _matches[m.MatchId] = m;
            return m;
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
