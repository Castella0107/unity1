using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Pvp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RhythmGame.Server.Data;

namespace RhythmGame.Server.Services
{
    /// <summary>
    /// PVP 試合の作成・リプレイ提出・確定・取得を司る REST エンドポイント群。
    /// 同期型 finalize: 両プレイヤー submit が揃った時点で結果計算 + レーティング更新を 1 トランザクションで行う。
    /// </summary>
    [ApiController]
    [Route("api/pvp")]
    public class PvpController : ControllerBase
    {
        private readonly ILogger<PvpController> _logger;
        private readonly AppDbContext _db;
        private readonly ActiveMatchStore _matches;
        private readonly ReplayValidationCore _validator;
        private readonly MatchmakingQueueService _queue;

        public PvpController(
            ILogger<PvpController> logger,
            AppDbContext db,
            ActiveMatchStore matches,
            ReplayValidationCore validator,
            MatchmakingQueueService queue)
        {
            _logger = logger;
            _db = db;
            _matches = matches;
            _validator = validator;
            _queue = queue;
        }

        // ── Queue ──────────────────────────────────────────────────────────────

        public class QueueRequestDto { public string UserId { get; set; } = ""; }

        public class QueueResponseDto
        {
            public string Status      { get; set; } = "";   // idle / queued / matched
            public string MatchId     { get; set; } = "";
            public string OpponentId  { get; set; } = "";
            public List<SongPickDto> Songs { get; set; } = new();
            public int    QueueDepth  { get; set; }
        }

        [HttpPost("queue/join")]
        public async Task<ActionResult<QueueResponseDto>> QueueJoin([FromBody] QueueRequestDto req)
        {
            if (req == null || string.IsNullOrEmpty(req.UserId)) return BadRequest("userId required");
            await GetOrCreateUserAsync(req.UserId);
            var s = _queue.Join(req.UserId);
            return BuildQueueDto(s);
        }

        [HttpPost("queue/leave")]
        public ActionResult<QueueResponseDto> QueueLeave([FromBody] QueueRequestDto req)
        {
            if (req == null || string.IsNullOrEmpty(req.UserId)) return BadRequest("userId required");
            var s = _queue.Leave(req.UserId);
            return BuildQueueDto(s);
        }

        [HttpGet("queue/status")]
        public ActionResult<QueueResponseDto> QueueStatus([FromQuery] string userId)
        {
            if (string.IsNullOrEmpty(userId)) return BadRequest("userId required");
            var s = _queue.GetStatus(userId);
            return BuildQueueDto(s);
        }

        private static QueueResponseDto BuildQueueDto(MatchmakingQueueService.Snapshot s) => new()
        {
            Status     = s.Status.ToString().ToLowerInvariant(),
            MatchId    = s.MatchId,
            OpponentId = s.OpponentId,
            Songs      = (s.Songs ?? new List<ActiveMatchStore.SongPick>())
                          .Select(p => new SongPickDto { SongId = p.SongId, Difficulty = p.Difficulty }).ToList(),
            QueueDepth = s.QueueDepth,
        };

        // ── Create ─────────────────────────────────────────────────────────────

        public class CreateRequestDto
        {
            public string UserIdA { get; set; } = "";
            public string UserIdB { get; set; } = "";
            public string[] PoolSongIds { get; set; }   // 任意。null なら MatchPool.CreateBootstrapPool() から
        }

        public class SongPickDto
        {
            public string SongId     { get; set; } = "";
            public string Difficulty { get; set; } = "";
        }

        public class CreateResponseDto
        {
            public string                 MatchId { get; set; } = "";
            public string                 UserIdA { get; set; } = "";
            public string                 UserIdB { get; set; } = "";
            public List<SongPickDto>      Songs   { get; set; } = new();
        }

        [HttpPost("match/create")]
        public async Task<ActionResult<CreateResponseDto>> Create([FromBody] CreateRequestDto req)
        {
            if (req == null || string.IsNullOrEmpty(req.UserIdA) || string.IsNullOrEmpty(req.UserIdB))
                return BadRequest("UserIdA / UserIdB required");
            if (req.UserIdA == req.UserIdB)
                return BadRequest("UserIdA == UserIdB not allowed");

            // 両 User を UPSERT (存在しなければ作成、Glicko2 初期値で)
            await GetOrCreateUserAsync(req.UserIdA);
            await GetOrCreateUserAsync(req.UserIdB);

            // 楽曲プールから 3 曲ランダム選択
            MatchPool pool;
            if (req.PoolSongIds != null && req.PoolSongIds.Length > 0)
            {
                var entries = req.PoolSongIds.Select(s => new MatchPoolEntry(s, "extra", 10)).ToList();
                pool = new MatchPool("custom", entries);
            }
            else
            {
                pool = MatchPool.CreateBootstrapPool();
            }
            if (pool.Entries.Count < 3)
                return BadRequest($"MatchPool has only {pool.Entries.Count} songs, need >= 3");

            var rng = new Random();
            var shuffled = pool.Entries.OrderBy(_ => rng.Next()).Take(3).ToList();
            var songs = shuffled.Select(e => new ActiveMatchStore.SongPick
            {
                SongId = e.SongId, Difficulty = e.Difficulty
            }).ToList();

            var active = _matches.Create(req.UserIdA, req.UserIdB, songs);

            _logger.LogInformation("[Pvp] Match created {MatchId}: {A} vs {B}, songs={Songs}",
                active.MatchId, req.UserIdA, req.UserIdB,
                string.Join(",", songs.Select(s => s.SongId)));

            return new CreateResponseDto
            {
                MatchId = active.MatchId,
                UserIdA = active.UserIdA,
                UserIdB = active.UserIdB,
                Songs   = songs.Select(s => new SongPickDto { SongId = s.SongId, Difficulty = s.Difficulty }).ToList(),
            };
        }

        // ── Submit ─────────────────────────────────────────────────────────────

        public class SubmitSongDto
        {
            public string SongId           { get; set; } = "";
            public string ReplayDataBase64 { get; set; } = "";
        }

        public class SubmitRequestDto
        {
            public string             UserId { get; set; } = "";
            public List<SubmitSongDto> Songs { get; set; } = new();
        }

        public class SubmitResponseDto
        {
            public bool   Accepted        { get; set; }
            public string Error           { get; set; } = "";
            public bool   MatchFinalized  { get; set; }
            public MatchResultDto Result  { get; set; }    // Finalized 時のみ非 null
        }

        [HttpPost("match/{matchId}/submit")]
        public async Task<ActionResult<SubmitResponseDto>> Submit(string matchId, [FromBody] SubmitRequestDto req)
        {
            if (req == null || string.IsNullOrEmpty(req.UserId))
                return BadRequest("userId required");

            var m = _matches.TryGet(matchId);
            if (m == null) return NotFound(new SubmitResponseDto { Accepted = false, Error = "match not found or already finalized" });
            if (m.Finalized) return new SubmitResponseDto { Accepted = false, Error = "match already finalized" };

            bool isA = req.UserId == m.UserIdA;
            bool isB = req.UserId == m.UserIdB;
            if (!isA && !isB) return Forbid();

            var mySub = isA ? m.SubmissionA : m.SubmissionB;
            if (mySub.Submitted) return new SubmitResponseDto { Accepted = false, Error = "already submitted" };

            if (req.Songs == null || req.Songs.Count != m.Songs.Count)
                return BadRequest($"songs.Count must be {m.Songs.Count}");

            // 各曲を順番に検証 (順序はマッチ作成時の m.Songs 順に合わせる必要がある)
            var sectorScores = new int[m.Songs.Count][];
            for (int i = 0; i < m.Songs.Count; i++)
            {
                var expected = m.Songs[i];
                var actual   = req.Songs[i];
                if (actual.SongId != expected.SongId)
                {
                    return BadRequest(new SubmitResponseDto { Accepted = false, Error = $"songs[{i}] mismatch: expected={expected.SongId}, got={actual.SongId}" });
                }

                byte[] bytes;
                try { bytes = Convert.FromBase64String(actual.ReplayDataBase64 ?? ""); }
                catch { return BadRequest(new SubmitResponseDto { Accepted = false, Error = $"songs[{i}] base64 decode failed" }); }

                // chartHash は replay 内部に格納されている → デコード後に取得 + 検証
                ReplayData replay;
                try { replay = ReplayDecoder.Decode(bytes); }
                catch (Exception ex) { return BadRequest(new SubmitResponseDto { Accepted = false, Error = $"songs[{i}] decode: {ex.Message}" }); }

                var chartHash = Convert.ToHexString(replay.Metadata.ChartHash ?? Array.Empty<byte>());
                var vr = await _validator.ValidateAsync(chartHash, bytes);
                if (!vr.Ok)
                {
                    return BadRequest(new SubmitResponseDto { Accepted = false, Error = $"songs[{i}] validate: {vr.Error}" });
                }

                // sector scores 5 件を取り出す (足りない場合は 0 詰め)
                var s = vr.Snapshot.SectorScores ?? new int[5];
                var copy = new int[5];
                for (int k = 0; k < 5 && k < s.Length; k++) copy[k] = s[k];
                sectorScores[i] = copy;
            }

            mySub.Submitted    = true;
            mySub.SectorScores = sectorScores;

            // 両者揃ったら finalize
            if (m.SubmissionA.Submitted && m.SubmissionB.Submitted)
            {
                var result = await FinalizeMatchAsync(m);
                return new SubmitResponseDto { Accepted = true, MatchFinalized = true, Result = result };
            }
            return new SubmitResponseDto { Accepted = true, MatchFinalized = false };
        }

        // ── Progress (in-match real-time) ──────────────────────────────────────

        public class ProgressUpdateDto
        {
            public string UserId       { get; set; } = "";
            public int    SongIndex    { get; set; }
            public int    PercentX1000 { get; set; }
            public int    Score        { get; set; }
        }

        public class ProgressSideDto
        {
            public string UserId          { get; set; } = "";
            public int    SongIndex       { get; set; }
            public int    PercentX1000    { get; set; }
            public int    Score           { get; set; }
            public long   UpdatedAtUnixMs { get; set; }
        }

        public class ProgressSnapshotDto
        {
            public string          MatchId { get; set; } = "";
            public ProgressSideDto A       { get; set; } = new();
            public ProgressSideDto B       { get; set; } = new();
            public bool            Finalized { get; set; }
        }

        [HttpPost("match/{matchId}/progress")]
        public ActionResult<ProgressSnapshotDto> PostProgress(string matchId, [FromBody] ProgressUpdateDto req)
        {
            if (req == null || string.IsNullOrEmpty(req.UserId)) return BadRequest("userId required");
            var m = _matches.TryGet(matchId);
            if (m == null) return NotFound();
            bool isA = req.UserId == m.UserIdA;
            bool isB = req.UserId == m.UserIdB;
            if (!isA && !isB) return Forbid();

            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var p = isA ? m.ProgressA : m.ProgressB;
            p.SongIndex       = req.SongIndex;
            p.PercentX1000    = System.Math.Max(0, System.Math.Min(100000, req.PercentX1000));
            p.Score           = req.Score;
            p.UpdatedAtUnixMs = nowMs;

            return BuildProgressSnapshot(m);
        }

        [HttpGet("match/{matchId}/progress")]
        public ActionResult<ProgressSnapshotDto> GetProgress(string matchId)
        {
            var m = _matches.TryGet(matchId);
            if (m != null) return BuildProgressSnapshot(m);
            // 既に finalize 済みなら 200 + Finalized=true (クライアント側 polling を止めるシグナル)
            return new ProgressSnapshotDto { MatchId = matchId, Finalized = true };
        }

        private static ProgressSnapshotDto BuildProgressSnapshot(ActiveMatchStore.ActiveMatch m)
        {
            return new ProgressSnapshotDto
            {
                MatchId = m.MatchId,
                Finalized = m.Finalized,
                A = new ProgressSideDto
                {
                    UserId          = m.UserIdA,
                    SongIndex       = m.ProgressA.SongIndex,
                    PercentX1000    = m.ProgressA.PercentX1000,
                    Score           = m.ProgressA.Score,
                    UpdatedAtUnixMs = m.ProgressA.UpdatedAtUnixMs,
                },
                B = new ProgressSideDto
                {
                    UserId          = m.UserIdB,
                    SongIndex       = m.ProgressB.SongIndex,
                    PercentX1000    = m.ProgressB.PercentX1000,
                    Score           = m.ProgressB.Score,
                    UpdatedAtUnixMs = m.ProgressB.UpdatedAtUnixMs,
                },
            };
        }

        // ── Get ────────────────────────────────────────────────────────────────

        public class MatchResultDto
        {
            public string  MatchId          { get; set; } = "";
            public string  UserIdA          { get; set; } = "";
            public string  UserIdB          { get; set; } = "";
            public List<SongPickDto> Songs  { get; set; } = new();
            public List<int> SectorScoresA  { get; set; } = new();   // 15 件
            public List<int> SectorScoresB  { get; set; } = new();
            public double TotalPointsA      { get; set; }
            public double TotalPointsB      { get; set; }
            public int    OutcomeKind       { get; set; }            // 0=Draw, 1=AWins, 2=BWins
            public double RatingABefore     { get; set; }
            public double RatingAAfter      { get; set; }
            public double RatingBBefore     { get; set; }
            public double RatingBAfter      { get; set; }
            public long   CompletedAtUnixMs { get; set; }
        }

        [HttpGet("match/{matchId}")]
        public async Task<ActionResult<MatchResultDto>> Get(string matchId)
        {
            // 確定済みなら DB を見る
            var saved = await _db.Matches.AsNoTracking().FirstOrDefaultAsync(x => x.MatchId == matchId);
            if (saved != null) return BuildResultDto(saved);

            // 進行中なら ActiveMatchStore (snapshot 用に部分情報のみ)
            var active = _matches.TryGet(matchId);
            if (active == null) return NotFound();

            return new MatchResultDto
            {
                MatchId = active.MatchId,
                UserIdA = active.UserIdA,
                UserIdB = active.UserIdB,
                Songs   = active.Songs.Select(s => new SongPickDto { SongId = s.SongId, Difficulty = s.Difficulty }).ToList(),
                OutcomeKind = -1,   // 未確定
            };
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private async Task<MatchResultDto> FinalizeMatchAsync(ActiveMatchStore.ActiveMatch m)
        {
            // SectorPair[15] を構築 (3 songs × 5 sectors)
            var pairs = new List<SectorPair>(15);
            for (int songIdx = 0; songIdx < m.Songs.Count; songIdx++)
            {
                var sA = m.SubmissionA.SectorScores[songIdx];
                var sB = m.SubmissionB.SectorScores[songIdx];
                for (int sec = 0; sec < 5; sec++)
                {
                    pairs.Add(new SectorPair(m.Songs[songIdx].SongId, sec, sA[sec], sB[sec]));
                }
            }

            var outcome = MatchScoring.Score(pairs);

            // 両ユーザーの BEFORE 状態でレーティング更新
            var userA = await _db.Users.FindAsync(m.UserIdA);
            var userB = await _db.Users.FindAsync(m.UserIdB);
            if (userA == null) userA = await GetOrCreateUserAsync(m.UserIdA);
            if (userB == null) userB = await GetOrCreateUserAsync(m.UserIdB);

            var pA = new Glicko2Player(userA.Rating, userA.RatingDeviation, userA.Volatility);
            var pB = new Glicko2Player(userB.Rating, userB.RatingDeviation, userB.Volatility);

            var newA = Glicko2Calculator.Update(pA, outcome.ToGlicko2ResultsForA(pB.Rating, pB.RatingDeviation).ToList());
            var newB = Glicko2Calculator.Update(pB, outcome.ToGlicko2ResultsForB(pA.Rating, pA.RatingDeviation).ToList());

            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var ratingABefore = userA.Rating;
            var ratingBBefore = userB.Rating;
            userA.Rating          = newA.Rating;
            userA.RatingDeviation = newA.RatingDeviation;
            userA.Volatility      = newA.Volatility;
            userA.LastRatedAtUnixMs = nowMs;
            userA.TotalPvpMatches++;
            userB.Rating          = newB.Rating;
            userB.RatingDeviation = newB.RatingDeviation;
            userB.Volatility      = newB.Volatility;
            userB.LastRatedAtUnixMs = nowMs;
            userB.TotalPvpMatches++;

            switch (outcome.Kind)
            {
                case MatchOutcomeKind.AWins: userA.PvpWins++;   userB.PvpLosses++; break;
                case MatchOutcomeKind.BWins: userA.PvpLosses++; userB.PvpWins++;   break;
                case MatchOutcomeKind.Draw:  userA.PvpDraws++;  userB.PvpDraws++;  break;
            }

            // MatchEntity 保存
            var entity = new MatchEntity
            {
                MatchId          = m.MatchId,
                UserIdA          = m.UserIdA,
                UserIdB          = m.UserIdB,
                SongIdsCsv       = string.Join(",", m.Songs.Select(s => s.SongId)),
                DifficultiesCsv  = string.Join(",", m.Songs.Select(s => s.Difficulty)),
                SectorScoresA    = string.Join(",", m.SubmissionA.SectorScores.SelectMany(a => a)),
                SectorScoresB    = string.Join(",", m.SubmissionB.SectorScores.SelectMany(a => a)),
                TotalPointsAx10  = (int)Math.Round(outcome.TotalPointsA * 10),
                TotalPointsBx10  = (int)Math.Round(outcome.TotalPointsB * 10),
                OutcomeKind      = (int)outcome.Kind,
                CreatedAtUnixMs  = m.CreatedAtUnixMs,
                CompletedAtUnixMs = nowMs,
                RatingABefore    = ratingABefore,
                RatingAAfter     = newA.Rating,
                RatingBBefore    = ratingBBefore,
                RatingBAfter     = newB.Rating,
            };
            _db.Matches.Add(entity);
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "[Pvp] Finalized {MatchId}: {A}({Ar:F1}→{Ar2:F1}) vs {B}({Br:F1}→{Br2:F1}), {Pa}-{Pb}",
                m.MatchId, m.UserIdA, ratingABefore, newA.Rating, m.UserIdB, ratingBBefore, newB.Rating,
                outcome.TotalPointsA, outcome.TotalPointsB);

            m.Finalized        = true;
            m.CompletedMatchId = m.MatchId;
            _matches.Remove(m.MatchId);

            return BuildResultDto(entity);
        }

        private async Task<UserEntity> GetOrCreateUserAsync(string userId)
        {
            var u = await _db.Users.FindAsync(userId);
            if (u == null)
            {
                u = new UserEntity
                {
                    UserId            = userId,
                    DisplayName       = userId,
                    FirstSeenUnixMs   = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    LastSeenUnixMs    = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Rating            = 1500.0,
                    RatingDeviation   = 350.0,
                    Volatility        = 0.06,
                };
                _db.Users.Add(u);
                await _db.SaveChangesAsync();
            }
            return u;
        }

        private static MatchResultDto BuildResultDto(MatchEntity e)
        {
            var songIds = (e.SongIdsCsv ?? "").Split(',');
            var diffs   = (e.DifficultiesCsv ?? "").Split(',');
            var songs   = new List<SongPickDto>();
            for (int i = 0; i < songIds.Length; i++)
            {
                songs.Add(new SongPickDto
                {
                    SongId     = songIds[i],
                    Difficulty = i < diffs.Length ? diffs[i] : "extra",
                });
            }
            return new MatchResultDto
            {
                MatchId       = e.MatchId,
                UserIdA       = e.UserIdA,
                UserIdB       = e.UserIdB,
                Songs         = songs,
                SectorScoresA = (e.SectorScoresA ?? "").Split(',').Where(s => !string.IsNullOrEmpty(s)).Select(int.Parse).ToList(),
                SectorScoresB = (e.SectorScoresB ?? "").Split(',').Where(s => !string.IsNullOrEmpty(s)).Select(int.Parse).ToList(),
                TotalPointsA  = e.TotalPointsAx10 / 10.0,
                TotalPointsB  = e.TotalPointsBx10 / 10.0,
                OutcomeKind   = e.OutcomeKind,
                RatingABefore = e.RatingABefore,
                RatingAAfter  = e.RatingAAfter,
                RatingBBefore = e.RatingBBefore,
                RatingBAfter  = e.RatingBAfter,
                CompletedAtUnixMs = e.CompletedAtUnixMs,
            };
        }
    }
}
