using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace RhythmGame.Server.Services
{
    /// <summary>
    /// ファイルシステム上の譜面フォルダから ChartData/SongMetadata を提供。
    /// 起動時に {root}/{song_id}/charts/{difficulty}.json を全スキャンし、
    /// 各譜面の SHA-256 をインデックス化する。
    /// </summary>
    public class FileSystemChartRepository : IChartRepository
    {
        private class ChartEntry
        {
            public string SongId = "";
            public string Difficulty = "";
            public string MetaPath = "";
            public string ChartPath = "";
        }

        private readonly Dictionary<string, ChartEntry> _byHash = new();
        private readonly ILogger<FileSystemChartRepository> _logger;

        public FileSystemChartRepository(string songsRoot, ILogger<FileSystemChartRepository> logger)
        {
            _logger = logger;

            if (!Directory.Exists(songsRoot))
            {
                _logger.LogWarning("[ChartRepo] Songs root not found: {Path}", songsRoot);
                return;
            }

            foreach (var songDir in Directory.GetDirectories(songsRoot))
            {
                var songId = Path.GetFileName(songDir);
                var metaPath = Path.Combine(songDir, "meta.json");
                if (!File.Exists(metaPath))
                {
                    _logger.LogDebug("[ChartRepo] Skip {SongId}: meta.json not found", songId);
                    continue;
                }

                var chartsDir = Path.Combine(songDir, "charts");
                if (!Directory.Exists(chartsDir))
                {
                    _logger.LogDebug("[ChartRepo] Skip {SongId}: charts/ not found", songId);
                    continue;
                }

                foreach (var chartFile in Directory.GetFiles(chartsDir, "*.json"))
                {
                    var difficulty = Path.GetFileNameWithoutExtension(chartFile);
                    try
                    {
                        var chartBytes = File.ReadAllBytes(chartFile);
                        var hashBytes = SHA256.HashData(chartBytes);
                        var hashHex = Convert.ToHexString(hashBytes);

                        _byHash[hashHex] = new ChartEntry
                        {
                            SongId = songId,
                            Difficulty = difficulty,
                            MetaPath = metaPath,
                            ChartPath = chartFile,
                        };

                        _logger.LogInformation(
                            "[ChartRepo] Indexed: {SongId}/{Difficulty} hash={Hash}",
                            songId, difficulty, hashHex.Substring(0, 16) + "...");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[ChartRepo] Failed to index: {Path}", chartFile);
                    }
                }
            }

            _logger.LogInformation("[ChartRepo] Total indexed: {Count} charts from {Path}",
                _byHash.Count, songsRoot);
        }

        public Task<(ChartData chart, SongMetadata meta)?> TryGetByHashAsync(string chartHashHex)
        {
            if (string.IsNullOrEmpty(chartHashHex))
                return Task.FromResult<(ChartData, SongMetadata)?>(null);

            if (!_byHash.TryGetValue(chartHashHex.ToUpperInvariant(), out var entry))
            {
                _logger.LogWarning("[ChartRepo] Chart not found for hash: {Hash}", chartHashHex);
                return Task.FromResult<(ChartData, SongMetadata)?>(null);
            }

            try
            {
                var metaJson = File.ReadAllText(entry.MetaPath);
                var chartJson = File.ReadAllText(entry.ChartPath);

                var meta = ChartParser.ParseMeta(metaJson);
                var chart = ChartParser.ParseChart(chartJson);

                return Task.FromResult<(ChartData, SongMetadata)?>((chart, meta));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ChartRepo] Failed to parse: {Hash}", chartHashHex);
                return Task.FromResult<(ChartData, SongMetadata)?>(null);
            }
        }
    }
}
