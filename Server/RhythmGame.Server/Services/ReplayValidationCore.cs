using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace RhythmGame.Server.Services
{
    /// <summary>
    /// gRPC / REST / PVP の各エンドポイントから呼ばれる共通検証ロジック。
    /// 与えられたリプレイバイト列をデコード → chartHash 一致確認 → JudgmentRunner 実行
    /// → PlayProgressSnapshot を返す。永続化 (DB Insert) は呼び出し側が扱う。
    /// </summary>
    public class ReplayValidationCore
    {
        private readonly ILogger<ReplayValidationCore> _logger;
        private readonly IChartRepository _chartRepo;

        public ReplayValidationCore(ILogger<ReplayValidationCore> logger, IChartRepository chartRepo)
        {
            _logger = logger;
            _chartRepo = chartRepo;
        }

        public class Result
        {
            public bool   Ok;
            public string Error;             // 失敗時の理由
            public string CanonicalChartHash; // 採用された chartHash (HEX 大文字)
            public ChartData             Chart;
            public SongMetadata          Meta;
            public PlayProgressSnapshot  Snapshot;
        }

        public async Task<Result> ValidateAsync(string chartHashHex, byte[] replayBytes)
        {
            if (replayBytes == null || replayBytes.Length == 0)
                return new Result { Ok = false, Error = "replay bytes empty" };

            ReplayData replayData;
            try { replayData = ReplayDecoder.Decode(replayBytes); }
            catch (Exception ex)
            {
                return new Result { Ok = false, Error = "decode failed: " + ex.Message };
            }

            var requestHashHex = (chartHashHex ?? "").ToUpperInvariant();
            var replayHashHex  = Convert.ToHexString(replayData.Metadata.ChartHash ?? Array.Empty<byte>());
            if (!string.Equals(requestHashHex, replayHashHex, StringComparison.OrdinalIgnoreCase))
            {
                return new Result
                {
                    Ok = false,
                    Error = $"chartHash mismatch: request={requestHashHex}, replay={replayHashHex}",
                };
            }

            var lookup = await _chartRepo.TryGetByHashAsync(requestHashHex);
            if (lookup is null)
            {
                return new Result { Ok = false, Error = $"chart not registered: {requestHashHex}" };
            }
            var (chart, meta) = lookup.Value;

            var runner = new JudgmentRunner();
            var snapshot = runner.Run(chart, meta, replayData);

            return new Result
            {
                Ok                 = true,
                CanonicalChartHash = (chart.ChartHash ?? "").ToUpperInvariant(),
                Chart              = chart,
                Meta               = meta,
                Snapshot           = snapshot,
            };
        }
    }
}
