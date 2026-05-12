using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace RhythmGame.Server.Services
{
    public class ReplayValidationService : ReplayValidation.ReplayValidationBase
    {
        private readonly ILogger<ReplayValidationService> _logger;
        private readonly IChartRepository _chartRepo;

        public ReplayValidationService(
            ILogger<ReplayValidationService> logger,
            IChartRepository chartRepo)
        {
            _logger = logger;
            _chartRepo = chartRepo;
        }

        public override async Task<ValidateResponse> Validate(
            ValidateRequest request,
            ServerCallContext context)
        {
            _logger.LogInformation(
                "[ReplayValidation] Received: chartHash={Hash}, replayDataSize={Size} bytes, claimedScore={Score}",
                request.ChartHash,
                request.ReplayData?.Length ?? 0,
                request.Claim?.Score ?? 0);

            try
            {
                var replayBytes = request.ReplayData.ToByteArray();
                var replayData = ReplayDecoder.Decode(replayBytes);

                var requestHashHex = request.ChartHash?.ToUpperInvariant() ?? string.Empty;
                var replayHashHex = Convert.ToHexString(replayData.Metadata.ChartHash ?? Array.Empty<byte>());

                if (!string.Equals(requestHashHex, replayHashHex, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("[ReplayValidation] ChartHash mismatch (request vs replay)");
                    return new ValidateResponse
                    {
                        IsValid = false,
                        ServerResult = request.Claim,
                        MismatchReason = $"ChartHash mismatch: request={requestHashHex}, replay={replayHashHex}",
                    };
                }

                var lookup = await _chartRepo.TryGetByHashAsync(requestHashHex);
                if (lookup is null)
                {
                    _logger.LogWarning("[ReplayValidation] Chart not registered: hash={Hash}", requestHashHex);
                    return new ValidateResponse
                    {
                        IsValid = false,
                        ServerResult = request.Claim,
                        MismatchReason = $"Chart not registered on server: hash={requestHashHex}",
                    };
                }
                var (chart, meta) = lookup.Value;

                var runner = new JudgmentRunner();
                var snapshot = runner.Run(chart, meta, replayData);

                var serverClaim = new PlayResultClaim
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
                    "[ReplayValidation] Server result: score={S}, maxCombo={MC}, pp={PP}, p={P}, gr={G}, gd={GD}, m={M}, rank={R}",
                    serverClaim.Score, serverClaim.MaxCombo, serverClaim.PerfectPlus, serverClaim.Perfect,
                    serverClaim.Great, serverClaim.Good, serverClaim.Miss, serverClaim.Rank);

                var mismatch = CompareClaims(request.Claim, serverClaim);
                return new ValidateResponse
                {
                    IsValid = string.IsNullOrEmpty(mismatch),
                    ServerResult = serverClaim,
                    MismatchReason = mismatch ?? string.Empty,
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ReplayValidation] Validation failed");
                return new ValidateResponse
                {
                    IsValid = false,
                    ServerResult = request.Claim,
                    MismatchReason = $"Validation error: {ex.Message}",
                };
            }
        }

        private static string CompareClaims(PlayResultClaim client, PlayResultClaim server)
        {
            if (client == null) return "Client claim is null";

            var diffs = new List<string>();
            if (client.Score != server.Score) diffs.Add($"Score: client={client.Score}, server={server.Score}");
            if (client.MaxCombo != server.MaxCombo) diffs.Add($"MaxCombo: client={client.MaxCombo}, server={server.MaxCombo}");
            if (client.PerfectPlus != server.PerfectPlus) diffs.Add($"PerfectPlus: client={client.PerfectPlus}, server={server.PerfectPlus}");
            if (client.Perfect != server.Perfect) diffs.Add($"Perfect: client={client.Perfect}, server={server.Perfect}");
            if (client.Great != server.Great) diffs.Add($"Great: client={client.Great}, server={server.Great}");
            if (client.Good != server.Good) diffs.Add($"Good: client={client.Good}, server={server.Good}");
            if (client.Miss != server.Miss) diffs.Add($"Miss: client={client.Miss}, server={server.Miss}");
            if (!string.Equals(client.Rank, server.Rank, StringComparison.Ordinal))
                diffs.Add($"Rank: client={client.Rank}, server={server.Rank}");

            return diffs.Count == 0 ? null : string.Join("; ", diffs);
        }
    }
}
