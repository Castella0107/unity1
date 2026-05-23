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
                        // クライアント側 (ChartParser) は JSON 内の chartHash フィールドを優先するため、
                        // サーバーも同じ値でインデックスしないとクライアント/サーバーで hash が一致しない。
                        // フィールドが欠落 / 無効 hex の場合のみ JSON 文字列の SHA-256 にフォールバック。
                        var chartJson = File.ReadAllText(chartFile);
                        var hashHex = ExtractOrComputeChartHash(chartJson).ToUpperInvariant();

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

        /// <summary>
        /// JSON 内に `chartHash` フィールドが有効 hex 文字列で含まれていればそれを返す。
        /// それ以外の場合は、クライアント側 ChartParser のフォールバック仕様
        /// (JSON 文字列の SHA-256) と同じ計算を行う。
        /// </summary>
        private static string ExtractOrComputeChartHash(string chartJson)
        {
            // strip BOM (ChartParser と同じ前処理)
            if (chartJson.Length > 0 && chartJson[0] == '﻿')
                chartJson = chartJson.Substring(1);

            // 軽量な抽出: フル JSON パースは不要
            // pattern: "chartHash" : "<hex>"  ("chart_hash" や snake_case には対応しない)
            const string key = "\"chartHash\"";
            int idx = chartJson.IndexOf(key, StringComparison.Ordinal);
            if (idx >= 0)
            {
                int colon = chartJson.IndexOf(':', idx + key.Length);
                if (colon > 0)
                {
                    int q1 = chartJson.IndexOf('"', colon + 1);
                    if (q1 > 0)
                    {
                        int q2 = chartJson.IndexOf('"', q1 + 1);
                        if (q2 > q1)
                        {
                            var candidate = chartJson.Substring(q1 + 1, q2 - q1 - 1);
                            if (IsValidHexHash(candidate))
                                return candidate;
                        }
                    }
                }
            }

            // Fallback: SHA-256 of the JSON string (UTF-8). ChartParser.ComputeSha256Hex と同じ。
            var bytes = System.Text.Encoding.UTF8.GetBytes(chartJson);
            return Convert.ToHexString(SHA256.HashData(bytes));
        }

        private static bool IsValidHexHash(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length != 64) return false;
            foreach (var c in s)
            {
                bool ok = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!ok) return false;
            }
            return true;
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
