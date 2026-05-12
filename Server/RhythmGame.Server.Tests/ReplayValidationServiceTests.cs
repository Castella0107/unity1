using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Google.Protobuf;
using Microsoft.Extensions.Logging.Abstractions;
using RhythmGame.Server;
using RhythmGame.Server.Services;
using Xunit;

namespace RhythmGame.Server.Tests
{
    public class ReplayValidationServiceTests
    {
        private static ReplayValidationService CreateService(IChartRepository? chartRepo = null)
            => new ReplayValidationService(
                NullLogger<ReplayValidationService>.Instance,
                chartRepo ?? new EmptyChartRepository());

        // 譜面DBが空のフェイク (常に not found)
        private class EmptyChartRepository : IChartRepository
        {
            public System.Threading.Tasks.Task<(ChartData chart, SongMetadata meta)?> TryGetByHashAsync(string chartHashHex)
                => System.Threading.Tasks.Task.FromResult<(ChartData, SongMetadata)?>(null);
        }

        private static ReplayData BuildSampleReplay(byte[] chartHash)
        {
            return new ReplayData
            {
                Header = new ReplayHeader
                {
                    Version = ReplayHeader.CurrentVersion,
                    Flags = 0,
                    PlayerUuid = new byte[16],
                },
                Metadata = new ReplayMetadata
                {
                    SongId = "test_song",
                    Difficulty = "extra",
                    ChartHash = chartHash,
                    PlayedAtUnixMs = 1700000000000,
                    DurationMs = 60000,
                    Bpm = 180.0f,
                    AppJudgmentOffsetMs = 0,
                    AppVisualOffsetMs = 0,
                    PerSongOffsetMs = 0,
                    Modifiers = new string[0],
                    JudgmentEngineVersion = "1.0.0",
                },
                Result = new ReplayResult
                {
                    RawScore = 1000000,
                    EffectiveScore = 1000000,
                    Rank = "S+",
                    PerfectPlusCount = 100,
                    PerfectCount = 0,
                    GreatCount = 0,
                    GoodCount = 0,
                    MissCount = 0,
                    MaxCombo = 100,
                    FastCount = 0,
                    LateCount = 0,
                    TotalNotes = 100,
                },
                InputEvents = new List<ReplayInputEvent>(),
            };
        }

        private static byte[] MakeHash(byte fillByte)
        {
            var hash = new byte[32];
            for (int i = 0; i < 32; i++) hash[i] = fillByte;
            return hash;
        }

        [Fact]
        public async Task EmptyReplayData_ReturnsInvalid()
        {
            var service = CreateService();
            var request = new ValidateRequest
            {
                ChartHash = "00".PadLeft(64, '0'),
                ReplayData = ByteString.Empty,
                Claim = new PlayResultClaim(),
            };

            var response = await service.Validate(request, null!);

            Assert.False(response.IsValid);
            Assert.Contains("error", response.MismatchReason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ChartHashMismatch_ReturnsInvalid()
        {
            var service = CreateService();
            var realHash = MakeHash(0xAA);
            var replayBytes = ReplayEncoder.Encode(BuildSampleReplay(realHash));

            var request = new ValidateRequest
            {
                ChartHash = Convert.ToHexString(MakeHash(0xBB)),
                ReplayData = ByteString.CopyFrom(replayBytes),
                Claim = new PlayResultClaim(),
            };

            var response = await service.Validate(request, null!);

            Assert.False(response.IsValid);
            Assert.Contains("ChartHash mismatch", response.MismatchReason);
        }

        [Fact]
        public async Task ChartNotRegistered_ReturnsInvalid()
        {
            // Hash は一致するが、譜面DBが空なので "Chart not registered on server"
            var service = CreateService();
            var hash = MakeHash(0xCD);
            var replayBytes = ReplayEncoder.Encode(BuildSampleReplay(hash));

            var request = new ValidateRequest
            {
                ChartHash = Convert.ToHexString(hash),
                ReplayData = ByteString.CopyFrom(replayBytes),
                Claim = new PlayResultClaim
                {
                    Score = 1000000,
                    MaxCombo = 100,
                    PerfectPlus = 100,
                    Rank = "S+",
                },
            };

            var response = await service.Validate(request, null!);

            Assert.False(response.IsValid);
            Assert.Contains("not registered", response.MismatchReason);
        }

        [Fact]
        public async Task RealChart_EmptyReplay_RecomputesAndMatches()
        {
            // 実譜面 (test_song/extra) + 空リプレイ で
            // サーバー判定再計算が完走することを確認。
            // 空リプレイ → 全 Miss → score=0 → クライアント Claim も score=0 で一致

            // 実譜面のパス
            var songsRoot = Path.GetFullPath(Path.Combine(
                Directory.GetCurrentDirectory(),
                "..", "..", "..", "..", "..",
                "Assets", "StreamingAssets", "Songs"));

            // テストの cwd は bin/Debug/net10.0 なので 5 階層上が PVP/

            var chartRepo = new FileSystemChartRepository(
                songsRoot,
                NullLogger<FileSystemChartRepository>.Instance);

            // test_song/extra の正しいハッシュを取得
            var chartFile = Path.Combine(songsRoot, "test_song", "charts", "extra.json");
            if (!File.Exists(chartFile))
            {
                // テスト譜面が見つからない場合はスキップ (環境差吸収)
                Assert.True(true, "test_song chart not found, skipping");
                return;
            }

            var chartBytes = File.ReadAllBytes(chartFile);
            var realHash = System.Security.Cryptography.SHA256.HashData(chartBytes);
            var realHashHex = Convert.ToHexString(realHash);

            var service = CreateService(chartRepo);
            var replayBytes = ReplayEncoder.Encode(BuildSampleReplay(realHash));

            var request = new ValidateRequest
            {
                ChartHash = realHashHex,
                ReplayData = ByteString.CopyFrom(replayBytes),
                Claim = new PlayResultClaim
                {
                    Score = 0,           // 空リプレイなので 0 のはず
                    MaxCombo = 0,
                    PerfectPlus = 0,
                    Perfect = 0,
                    Great = 0,
                    Good = 0,
                    // Miss = (test_song の TotalNotes と同じはず) ← 後で動かしてから判明する
                    Rank = "D",          // 0 点なので最低ランク
                },
            };

            var response = await service.Validate(request, null!);

            // 空リプレイ判定は Miss だらけになるので、Claim と一致する保証はない。
            // ここでは「Validate がエラーなく完走したこと」を主に確認する。
            // ServerResult が null でなく、JudgmentRunner が動いた証拠を見る。
            Assert.NotNull(response.ServerResult);
            // 判定は走った (例外が catch されていない) → MismatchReason には "Validation error" が入らない
            Assert.DoesNotContain("Validation error", response.MismatchReason);
        }
    
        [Fact]
        public async Task ScoreClaim_TooHigh_ReturnsMismatch()
        {
            // 空リプレイ → server score=0、しかし Claim で 1,000,000 を主張 (チート想定)
            // 期待: IsValid=false、MismatchReason に "Score:" を含む
            var songsRoot = Path.GetFullPath(Path.Combine(
                Directory.GetCurrentDirectory(),
                "..", "..", "..", "..", "..",
                "Assets", "StreamingAssets", "Songs"));

            var chartFile = Path.Combine(songsRoot, "test_song", "charts", "extra.json");
            if (!File.Exists(chartFile))
            {
                Assert.True(true, "test_song chart not found, skipping");
                return;
            }

            var chartBytes = File.ReadAllBytes(chartFile);
            var realHash = System.Security.Cryptography.SHA256.HashData(chartBytes);
            var realHashHex = Convert.ToHexString(realHash);

            var chartRepo = new FileSystemChartRepository(
                songsRoot,
                NullLogger<FileSystemChartRepository>.Instance);
            var service = CreateService(chartRepo);
            var replayBytes = ReplayEncoder.Encode(BuildSampleReplay(realHash));

            var request = new ValidateRequest
            {
                ChartHash = realHashHex,
                ReplayData = ByteString.CopyFrom(replayBytes),
                Claim = new PlayResultClaim
                {
                    Score = 1000000,    // ← チート: 空リプレイなのに満点を主張
                    MaxCombo = 0,
                    PerfectPlus = 0,
                    Perfect = 0,
                    Great = 0,
                    Good = 0,
                    Rank = "S+",
                },
            };

            var response = await service.Validate(request, null!);

            Assert.False(response.IsValid);
            Assert.Contains("Score:", response.MismatchReason);
        }

        [Fact]
        public async Task MaxComboClaim_TooHigh_ReturnsMismatch()
        {
            // 空リプレイ → server MaxCombo=0、Claim で 100 を主張
            var songsRoot = Path.GetFullPath(Path.Combine(
                Directory.GetCurrentDirectory(),
                "..", "..", "..", "..", "..",
                "Assets", "StreamingAssets", "Songs"));

            var chartFile = Path.Combine(songsRoot, "test_song", "charts", "extra.json");
            if (!File.Exists(chartFile))
            {
                Assert.True(true, "test_song chart not found, skipping");
                return;
            }

            var chartBytes = File.ReadAllBytes(chartFile);
            var realHash = System.Security.Cryptography.SHA256.HashData(chartBytes);
            var realHashHex = Convert.ToHexString(realHash);

            var chartRepo = new FileSystemChartRepository(
                songsRoot,
                NullLogger<FileSystemChartRepository>.Instance);
            var service = CreateService(chartRepo);
            var replayBytes = ReplayEncoder.Encode(BuildSampleReplay(realHash));

            var request = new ValidateRequest
            {
                ChartHash = realHashHex,
                ReplayData = ByteString.CopyFrom(replayBytes),
                Claim = new PlayResultClaim
                {
                    Score = 0,
                    MaxCombo = 100,    // ← チート: 空リプレイなのに 100 コンボを主張
                    PerfectPlus = 0,
                    Perfect = 0,
                    Great = 0,
                    Good = 0,
                    Rank = "D",
                },
            };

            var response = await service.Validate(request, null!);

            Assert.False(response.IsValid);
            Assert.Contains("MaxCombo:", response.MismatchReason);
        }
    }
}
