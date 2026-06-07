using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace RhythmGame.Network.Api
{
    /// <summary>
    /// Go サーバーの楽曲・譜面ライブラリ (M2: 楽曲同期+譜面DL+chart_hash整合)。
    ///
    ///   - <see cref="SyncAsync"/>: GET /songs → 各曲の譜面情報 → 未キャッシュ分を /charts/{id}/download
    ///   - 生 JSON を persistentDataPath/server_charts/ にキャッシュ (SHA-256 を chart_hash と照合)
    ///   - <see cref="ServerChartConverter"/> で ChartData 化してメモリ登録 → ChartLoader が参照
    ///   - オフライン/同期失敗時は index.json から前回キャッシュを復元
    ///
    /// 音源・ジャケットは API 対象外のため、同 song_id の StreamingAssets/Songs/{song_id}/ を併用する
    /// (譜面のみサーバーが正、chart_hash はリプレイ検証に直結)。
    /// </summary>
    public static class ServerSongLibrary
    {
        static readonly Dictionary<string, SongListItemDto> _songs  = new Dictionary<string, SongListItemDto>();
        static readonly Dictionary<string, ChartData>       _charts = new Dictionary<string, ChartData>();   // key: songId + "/" + difficulty
        static Task<bool> _syncInFlight;

        /// <summary>同期済みか (ネットワーク or ディスクキャッシュ)。</summary>
        public static bool IsSynced { get; private set; }

        static string CacheDir  => Path.Combine(Application.persistentDataPath, "server_charts");
        static string IndexPath => Path.Combine(CacheDir, "index.json");

        // ── index.json (オフライン復元用) ─────────────────────────────────────

        class IndexEntry
        {
            public string SongId;
            public string Difficulty;
            public string ChartId;
            public int    Version;
            public string ChartHash;
            public int    Level;
        }

        class IndexFile
        {
            public List<SongListItemDto> Songs  = new List<SongListItemDto>();
            public List<IndexEntry>      Charts = new List<IndexEntry>();
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>未同期なら同期する (多重呼び出しは同一 Task を共有)。</summary>
        public static Task<bool> EnsureSyncedAsync()
        {
            if (IsSynced) return Task.FromResult(true);
            if (_syncInFlight != null) return _syncInFlight;
            _syncInFlight = SyncCoreAsync();
            return _syncInFlight;
        }

        /// <summary>強制再同期。</summary>
        public static Task<bool> SyncAsync()
        {
            if (_syncInFlight != null) return _syncInFlight;
            _syncInFlight = SyncCoreAsync();
            return _syncInFlight;
        }

        /// <summary>サーバー譜面を取得する (同期済みのもののみ)。</summary>
        public static bool TryGetChart(string songId, string difficulty, out ChartData chart)
            => _charts.TryGetValue(Key(songId, difficulty), out chart);

        /// <summary>サーバー曲のメタデータを合成して返す (未知の song_id は null)。</summary>
        public static SongMetadata GetMetaOrNull(string songId)
        {
            if (!_songs.TryGetValue(songId, out var s)) return null;
            return new SongMetadata
            {
                SongId     = s.SongId,
                Title      = s.Title,
                Artist     = s.Artist,
                Bpm        = s.Bpm,
                DurationMs = s.DurationSeconds * 1000,
                AudioFile  = "audio.wav",
                JacketFile = "",
                Sectors    = new List<SectorDef>(),
            };
        }

        /// <summary>同期済みのサーバー曲 ID 一覧。</summary>
        public static IEnumerable<string> SongIds => _songs.Keys;

        // ── Sync core ────────────────────────────────────────────────────────

        static async Task<bool> SyncCoreAsync()
        {
            try
            {
                Directory.CreateDirectory(CacheDir);

                // 実装は data=直接配列、設計書は data={songs:[...]} — 両形状を許容する
                // (差分は K に報告済み。確定したら片方に絞る)
                var list = await ApiClient.GetAsync<Newtonsoft.Json.Linq.JToken>("/songs?limit=200");
                if (!list.Ok || list.Data == null)
                {
                    Debug.LogWarning($"[ServerSongLibrary] /songs 取得失敗 ({list.ErrorCode}) — キャッシュから復元します");
                    return LoadFromDiskCache();
                }

                var songsArr = (list.Data as Newtonsoft.Json.Linq.JArray)
                            ?? list.Data["songs"] as Newtonsoft.Json.Linq.JArray;
                var songList = songsArr?.ToObject<List<SongListItemDto>>() ?? new List<SongListItemDto>();
                if (songList.Count == 0)
                {
                    Debug.LogWarning("[ServerSongLibrary] サーバーに楽曲が0件 (シード未投入?) — キャッシュから復元します");
                    return LoadFromDiskCache();
                }

                var index = new IndexFile();
                int okCharts = 0, ngCharts = 0;

                foreach (var song in songList)
                {
                    _songs[song.SongId] = song;
                    index.Songs.Add(song);

                    var detail = await ApiClient.GetAsync<SongDetailDto>($"/songs/{song.SongId}");
                    if (!detail.Ok || detail.Data?.Charts == null)
                    {
                        Debug.LogWarning($"[ServerSongLibrary] {song.SongId} 詳細取得失敗 ({detail.ErrorCode})");
                        continue;
                    }

                    foreach (var info in detail.Data.Charts)
                    {
                        bool ok = await EnsureChartCachedAsync(song.SongId, info);
                        if (ok)
                        {
                            okCharts++;
                            index.Charts.Add(new IndexEntry
                            {
                                SongId = song.SongId, Difficulty = info.Difficulty,
                                ChartId = info.ChartId, Version = info.Version,
                                ChartHash = info.ChartHash, Level = info.Level,
                            });
                        }
                        else ngCharts++;
                    }
                }

                File.WriteAllText(IndexPath, JsonConvert.SerializeObject(index));
                IsSynced = okCharts > 0;
                Debug.Log($"[ServerSongLibrary] sync 完了: songs={_songs.Count} charts={okCharts} (失敗 {ngCharts})");
                return IsSynced;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ServerSongLibrary] sync 例外: {e.Message} — キャッシュから復元します");
                return LoadFromDiskCache();
            }
            finally
            {
                _syncInFlight = null;
            }
        }

        // 譜面1枚をキャッシュ確保 (ディスクにハッシュ一致ファイルがあれば再利用、なければDL) → ChartData 登録
        static async Task<bool> EnsureChartCachedAsync(string songId, ChartInfoDto info)
        {
            string cachePath = ChartCachePath(info.ChartId, info.Version);
            string json = null;

            if (File.Exists(cachePath))
            {
                var bytes = File.ReadAllBytes(cachePath);
                if (Sha256Hex(bytes) == info.ChartHash)
                    json = System.Text.Encoding.UTF8.GetString(bytes);
            }

            if (json == null)
            {
                var dl = await ApiClient.DownloadAsync($"/charts/{info.ChartId}/download");
                if (!dl.Ok || dl.Data == null || dl.Data.Length == 0)
                {
                    Debug.LogWarning($"[ServerSongLibrary] {info.ChartId} DL失敗 ({dl.ErrorCode})");
                    return false;
                }

                string actualHash = Sha256Hex(dl.Data);
                if (actualHash != info.ChartHash)
                    Debug.LogWarning($"[ServerSongLibrary] {info.ChartId} hash不一致: api={info.ChartHash} actual={actualHash} (サーバー申告値を採用)");

                File.WriteAllBytes(cachePath, dl.Data);
                json = System.Text.Encoding.UTF8.GetString(dl.Data);
            }

            return RegisterChart(songId, info.Difficulty, json, info.Level, info.ChartHash, info.ChartId);
        }

        static bool RegisterChart(string songId, string difficulty, string json, int level, string chartHash, string chartId)
        {
            try
            {
                var chart = ServerChartConverter.Convert(json, level, chartHash);
                _charts[Key(songId, difficulty)] = chart;
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ServerSongLibrary] {chartId} 変換失敗: {e.Message}");
                return false;
            }
        }

        // オフライン時: 前回同期の index.json + キャッシュファイルから復元
        static bool LoadFromDiskCache()
        {
            try
            {
                if (!File.Exists(IndexPath)) return false;
                var index = JsonConvert.DeserializeObject<IndexFile>(File.ReadAllText(IndexPath));
                if (index == null) return false;

                foreach (var s in index.Songs ?? new List<SongListItemDto>())
                    _songs[s.SongId] = s;

                int ok = 0;
                foreach (var e in index.Charts ?? new List<IndexEntry>())
                {
                    string path = ChartCachePath(e.ChartId, e.Version);
                    if (!File.Exists(path)) continue;
                    if (RegisterChart(e.SongId, e.Difficulty, File.ReadAllText(path), e.Level, e.ChartHash, e.ChartId))
                        ok++;
                }

                IsSynced = ok > 0;
                Debug.Log($"[ServerSongLibrary] ディスクキャッシュ復元: songs={_songs.Count} charts={ok}");
                return IsSynced;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ServerSongLibrary] キャッシュ復元失敗: {e.Message}");
                return false;
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        static string Key(string songId, string difficulty) => songId + "/" + difficulty;

        static string ChartCachePath(string chartId, int version)
            => Path.Combine(CacheDir, $"{chartId}_v{version}.json");

        static string Sha256Hex(byte[] bytes)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(bytes);
            return System.BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
}
