using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RhythmGame.Server.Data;

namespace RhythmGame.Server.Services
{
    /// <summary>
    /// Unity / その他クライアント向けの REST 入口。
    /// 内部的には Domain 層 (JudgmentRunner, ReplayDecoder) を直呼び出しする。
    /// gRPC ReplayValidationService と同等のロジックを JSON で公開。
    /// </summary>
    [ApiController]
    [Route("api")]
    public class ReplayRestController : ControllerBase
    {
        private readonly ILogger<ReplayRestController> _logger;
        private readonly IChartRepository _chartRepo;
        private readonly AppDbContext _db;

        public ReplayRestController(
            ILogger<ReplayRestController> logger,
            IChartRepository chartRepo,
            AppDbContext db)
        {
            _logger = logger;
            _chartRepo = chartRepo;
            _db = db;
        }

        public class PingResponse
        {
            public string Status { get; set; } = "ok";
            public string ServerVersion { get; set; } = "0.1.0";
            public long ServerTimeUnixMs { get; set; }
        }

        [HttpGet("ping")]
        public PingResponse Ping()
        {
            return new PingResponse
            {
                Status = "ok",
                ServerVersion = "0.1.0",
                ServerTimeUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };
        }

        public class ValidateRequestDto
        {
            public string ChartHash { get; set; } = "";
            public string ReplayDataBase64 { get; set; } = "";
            public ResultDto Claim { get; set; } = new ResultDto();

            // 永続化用 (任意 — クライアントから送らなければ匿名で保存される)
            public string PlayId         { get; set; } = "";
            public string SongId         { get; set; } = "";
            public string Difficulty     { get; set; } = "";
            public string UserId         { get; set; } = "anon";
            public long   PlayedAtUnixMs { get; set; }
            public int    TotalNotes     { get; set; }
            public bool   IsFullCombo    { get; set; }
            public bool   IsAllPerfect   { get; set; }
            public bool   IsAllPerfectPlus { get; set; }
        }

        public class ResultDto
        {
            public long Score { get; set; }
            public int MaxCombo { get; set; }
            public int PerfectPlus { get; set; }
            public int Perfect { get; set; }
            public int Great { get; set; }
            public int Good { get; set; }
            public int Miss { get; set; }
            public string Rank { get; set; } = "";
        }

        public class ValidateResponseDto
        {
            public bool IsValid { get; set; }
            public ResultDto ServerResult { get; set; } = new ResultDto();
            public string MismatchReason { get; set; } = "";
        }

        [HttpPost("replay/validate")]
        public async Task<ActionResult<ValidateResponseDto>> Validate([FromBody] ValidateRequestDto req)
        {
            if (req is null)
                return BadRequest(new ValidateResponseDto { IsValid = false, MismatchReason = "request body is null" });

            byte[] replayBytes;
            try
            {
                replayBytes = Convert.FromBase64String(req.ReplayDataBase64 ?? "");
            }
            catch (FormatException)
            {
                return BadRequest(new ValidateResponseDto { IsValid = false, MismatchReason = "replayDataBase64 is not valid base64" });
            }

            try
            {
                var replayData = ReplayDecoder.Decode(replayBytes);

                var requestHashHex = (req.ChartHash ?? "").ToUpperInvariant();
                var replayHashHex = Convert.ToHexString(replayData.Metadata.ChartHash ?? Array.Empty<byte>());

                if (!string.Equals(requestHashHex, replayHashHex, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("[REST/Validate] ChartHash mismatch (request vs replay)");
                    return new ValidateResponseDto
                    {
                        IsValid = false,
                        ServerResult = req.Claim,
                        MismatchReason = $"ChartHash mismatch: request={requestHashHex}, replay={replayHashHex}",
                    };
                }

                var lookup = await _chartRepo.TryGetByHashAsync(requestHashHex);
                if (lookup is null)
                {
                    _logger.LogWarning("[REST/Validate] Chart not registered: hash={Hash}", requestHashHex);
                    return new ValidateResponseDto
                    {
                        IsValid = false,
                        ServerResult = req.Claim,
                        MismatchReason = $"Chart not registered on server: hash={requestHashHex}",
                    };
                }
                var (chart, meta) = lookup.Value;

                var runner = new JudgmentRunner();
                var snapshot = runner.Run(chart, meta, replayData);

                var serverResult = new ResultDto
                {
                    Score        = snapshot.CurrentScore,
                    MaxCombo     = snapshot.MaxCombo,
                    PerfectPlus  = snapshot.PerfectPlusCount,
                    Perfect      = snapshot.PerfectCount,
                    Great        = snapshot.GreatCount,
                    Good         = snapshot.GoodCount,
                    Miss         = snapshot.MissCount,
                    Rank         = ScoreCalculator.ComputeRank(snapshot.CurrentScore),
                };

                _logger.LogInformation(
                    "[REST/Validate] score={S}, maxCombo={MC}, pp={PP}, p={P}, gr={G}, gd={GD}, m={M}, rank={R}",
                    serverResult.Score, serverResult.MaxCombo, serverResult.PerfectPlus, serverResult.Perfect,
                    serverResult.Great, serverResult.Good, serverResult.Miss, serverResult.Rank);

                var mismatch = CompareClaims(req.Claim, serverResult);
                bool isValid = string.IsNullOrEmpty(mismatch);

                // 検証成功時のみ DB に保存。SongId/Difficulty 等メタが欠落していたら保存をスキップ。
                if (isValid && !string.IsNullOrEmpty(req.SongId) && !string.IsNullOrEmpty(req.Difficulty))
                {
                    try
                    {
                        await SavePlayRecordAsync(req, serverResult, chart.ChartHash);
                    }
                    catch (Exception saveEx)
                    {
                        // 保存失敗は検証結果の整合性を崩さないように warning のみ
                        _logger.LogWarning(saveEx, "[REST/Validate] Failed to save PlayRecord");
                    }
                }

                return new ValidateResponseDto
                {
                    IsValid = isValid,
                    ServerResult = serverResult,
                    MismatchReason = mismatch ?? "",
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[REST/Validate] Validation failed");
                return new ValidateResponseDto
                {
                    IsValid = false,
                    ServerResult = req.Claim,
                    MismatchReason = $"Validation error: {ex.Message}",
                };
            }
        }

        // 検証済みプレイ記録の保存。PlayId が一致する既存レコードがあれば上書きしない (idempotent)。
        // 副作用: UserId に対応する Users レコードを UPSERT (FirstSeen/LastSeen/TotalPlays)。
        private async System.Threading.Tasks.Task SavePlayRecordAsync(
            ValidateRequestDto req, ResultDto serverResult, string canonicalChartHash)
        {
            var playId = !string.IsNullOrEmpty(req.PlayId) ? req.PlayId : Guid.NewGuid().ToString();
            var existing = await _db.PlayRecords.FindAsync(playId);
            if (existing != null) return;

            // ── Users UPSERT ────────────────────────────────────────────────
            var nowMs  = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var userId = string.IsNullOrEmpty(req.UserId) ? "anon" : req.UserId;
            var user = await _db.Users.FindAsync(userId);
            if (user == null)
            {
                user = new UserEntity
                {
                    UserId          = userId,
                    DisplayName     = userId,
                    FirstSeenUnixMs = nowMs,
                    LastSeenUnixMs  = nowMs,
                    TotalPlays      = 1,
                };
                _db.Users.Add(user);
            }
            else
            {
                user.DisplayName    = userId;        // 現状 UserId == DisplayName。OAuth 後は分離
                user.LastSeenUnixMs = nowMs;
                user.TotalPlays    += 1;
            }

            var entity = new PlayRecordEntity
            {
                PlayId            = playId,
                UserId            = userId,
                SongId            = req.SongId,
                Difficulty        = req.Difficulty,
                PlayedAtUnixMs    = req.PlayedAtUnixMs,
                SubmittedAtUnixMs = nowMs,
                RawScore          = (int)serverResult.Score,
                EffectiveScore    = (int)serverResult.Score,   // 難易度倍率は後段で適用 (現状は Score をそのまま)
                Rank              = serverResult.Rank ?? "",
                PerfectPlus       = serverResult.PerfectPlus,
                Perfect           = serverResult.Perfect,
                Great             = serverResult.Great,
                Good              = serverResult.Good,
                Miss              = serverResult.Miss,
                MaxCombo          = serverResult.MaxCombo,
                TotalNotes        = req.TotalNotes,
                ChartHash         = canonicalChartHash ?? "",
                IsFullCombo       = req.IsFullCombo,
                IsAllPerfect      = req.IsAllPerfect,
                IsAllPerfectPlus  = req.IsAllPerfectPlus,
            };
            _db.PlayRecords.Add(entity);
            await _db.SaveChangesAsync();
            _logger.LogInformation("[REST/Validate] Saved PlayRecord {PlayId} song={Song}/{Diff} score={Score}",
                playId, req.SongId, req.Difficulty, serverResult.Score);
        }

        private static string CompareClaims(ResultDto client, ResultDto server)
        {
            if (client == null) return "Client claim is null";

            var diffs = new System.Collections.Generic.List<string>();
            if (client.Score != server.Score) diffs.Add($"Score: client={client.Score}, server={server.Score}");
            if (client.MaxCombo != server.MaxCombo) diffs.Add($"MaxCombo: client={client.MaxCombo}, server={server.MaxCombo}");
            if (client.PerfectPlus != server.PerfectPlus) diffs.Add($"PerfectPlus: client={client.PerfectPlus}, server={server.PerfectPlus}");
            if (client.Perfect != server.Perfect) diffs.Add($"Perfect: client={client.Perfect}, server={server.Perfect}");
            if (client.Great != server.Great) diffs.Add($"Great: client={client.Great}, server={server.Great}");
            if (client.Good != server.Good) diffs.Add($"Good: client={client.Good}, server={server.Good}");
            if (client.Miss != server.Miss) diffs.Add($"Miss: client={client.Miss}, server={server.Miss}");
            if (!string.Equals(client.Rank ?? "", server.Rank ?? "", StringComparison.Ordinal))
                diffs.Add($"Rank: client={client.Rank}, server={server.Rank}");

            return diffs.Count == 0 ? null : string.Join("; ", diffs);
        }
    }
}
