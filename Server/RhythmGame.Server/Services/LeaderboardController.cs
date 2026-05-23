using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RhythmGame.Server.Data;

namespace RhythmGame.Server.Services
{
    /// <summary>
    /// 楽曲+難易度別のトップスコア。/api/leaderboard/{songId}/{difficulty}?limit=N
    /// 同一ユーザーの複数提出はすべて表示 (個人ベストのみに絞るのは後段)。
    /// </summary>
    [ApiController]
    [Route("api/leaderboard")]
    public class LeaderboardController : ControllerBase
    {
        private readonly ILogger<LeaderboardController> _logger;
        private readonly AppDbContext _db;

        public LeaderboardController(ILogger<LeaderboardController> logger, AppDbContext db)
        {
            _logger = logger;
            _db = db;
        }

        public class EntryDto
        {
            public int    Rank           { get; set; }
            public string UserId         { get; set; } = "";
            public int    Score          { get; set; }
            public string ScoreRank      { get; set; } = "";   // S+/S/A+/A/B/C/D
            public int    MaxCombo       { get; set; }
            public bool   IsFullCombo    { get; set; }
            public bool   IsAllPerfectPlus { get; set; }
            public long   PlayedAtUnixMs { get; set; }
        }

        public class ResponseDto
        {
            public string         SongId     { get; set; } = "";
            public string         Difficulty { get; set; } = "";
            public int            Total      { get; set; }
            public List<EntryDto> Entries    { get; set; } = new();
        }

        [HttpGet("{songId}/{difficulty}")]
        public async Task<ActionResult<ResponseDto>> Get(string songId, string difficulty, [FromQuery] int limit = 10)
        {
            limit = limit < 1 ? 10 : (limit > 100 ? 100 : limit);

            // 各 UserId の最高スコアレコードのみに絞る。
            // SQLite では Group→Max→Join のクエリより、対象行を一度読んで in-memory で
            // GroupBy する方がインデックスを活かせて高速 (PlayRecords が数千件規模を想定)。
            var rawRows = await _db.PlayRecords
                .AsNoTracking()
                .Where(x => x.SongId == songId && x.Difficulty == difficulty)
                .ToListAsync();

            var bestPerUser = rawRows
                .GroupBy(x => x.UserId)
                .Select(g => g
                    .OrderByDescending(x => x.EffectiveScore)
                    .ThenBy(x => x.PlayedAtUnixMs)
                    .First())
                .OrderByDescending(x => x.EffectiveScore)
                .ThenBy(x => x.PlayedAtUnixMs)
                .ToList();

            int total = bestPerUser.Count;
            var rows = bestPerUser.Take(limit).ToList();

            var entries = new List<EntryDto>(rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                entries.Add(new EntryDto
                {
                    Rank             = i + 1,
                    UserId           = r.UserId,
                    Score            = r.EffectiveScore,
                    ScoreRank        = r.Rank,
                    MaxCombo         = r.MaxCombo,
                    IsFullCombo      = r.IsFullCombo,
                    IsAllPerfectPlus = r.IsAllPerfectPlus,
                    PlayedAtUnixMs   = r.PlayedAtUnixMs,
                });
            }

            return new ResponseDto
            {
                SongId     = songId,
                Difficulty = difficulty,
                Total      = total,
                Entries    = entries,
            };
        }

        /// <summary>個人ベスト + 全体での順位を返す。</summary>
        public class PersonalDto
        {
            public string  SongId         { get; set; } = "";
            public string  Difficulty     { get; set; } = "";
            public string  UserId         { get; set; } = "";
            public bool    HasRecord      { get; set; }
            public int     OverallRank    { get; set; }   // 自分の順位 (1 始まり)。HasRecord==false なら 0
            public int     TotalUsers     { get; set; }
            public EntryDto Best          { get; set; }
        }

        [HttpGet("{songId}/{difficulty}/me")]
        public async Task<ActionResult<PersonalDto>> GetPersonal(
            string songId, string difficulty, [FromQuery] string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return BadRequest(new PersonalDto { SongId = songId, Difficulty = difficulty, UserId = "" });

            var rawRows = await _db.PlayRecords
                .AsNoTracking()
                .Where(x => x.SongId == songId && x.Difficulty == difficulty)
                .ToListAsync();

            var bestPerUser = rawRows
                .GroupBy(x => x.UserId)
                .Select(g => g
                    .OrderByDescending(x => x.EffectiveScore)
                    .ThenBy(x => x.PlayedAtUnixMs)
                    .First())
                .OrderByDescending(x => x.EffectiveScore)
                .ThenBy(x => x.PlayedAtUnixMs)
                .ToList();

            int totalUsers = bestPerUser.Count;
            var idx = bestPerUser.FindIndex(x => x.UserId == userId);
            if (idx < 0)
            {
                return new PersonalDto
                {
                    SongId = songId, Difficulty = difficulty, UserId = userId,
                    HasRecord = false, OverallRank = 0, TotalUsers = totalUsers,
                };
            }
            var r = bestPerUser[idx];
            return new PersonalDto
            {
                SongId      = songId,
                Difficulty  = difficulty,
                UserId      = userId,
                HasRecord   = true,
                OverallRank = idx + 1,
                TotalUsers  = totalUsers,
                Best = new EntryDto
                {
                    Rank             = idx + 1,
                    UserId           = r.UserId,
                    Score            = r.EffectiveScore,
                    ScoreRank        = r.Rank,
                    MaxCombo         = r.MaxCombo,
                    IsFullCombo      = r.IsFullCombo,
                    IsAllPerfectPlus = r.IsAllPerfectPlus,
                    PlayedAtUnixMs   = r.PlayedAtUnixMs,
                },
            };
        }
    }
}
