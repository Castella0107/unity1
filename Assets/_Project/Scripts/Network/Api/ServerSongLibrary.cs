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

        /// <summary>テストソング (title が "Test Song" で始まる開発用データ) か。</summary>
        public static bool IsTestSong(string songId)
            => _songs.TryGetValue(songId, out var s) &&
               (s.Title ?? "").StartsWith("Test Song", System.StringComparison.OrdinalIgnoreCase);

        /// <summary>対戦モードのドラフト候補となる曲 ID 一覧 (K 指示 2026-07-30: テストソングを除外)。</summary>
        public static IEnumerable<string> PvpSongIds
        {
            get
            {
                foreach (var id in _songs.Keys)
                    if (!IsTestSong(id)) yield return id;
            }
        }

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

                // 同時実行数の上限 (サーバー負荷とソケット枯渇を避けつつ直列 N 往復を排除)。
                const int Batch = 8;

                // 1. 全曲の詳細を並列取得 (旧: 曲ごとに直列 await で 200 往復が順番待ち)。
                var details = new SongDetailDto[songList.Count];
                for (int start = 0; start < songList.Count; start += Batch)
                {
                    int end = System.Math.Min(start + Batch, songList.Count);
                    var tasks = new Task<ApiResult<SongDetailDto>>[end - start];
                    for (int i = start; i < end; i++)
                        tasks[i - start] = ApiClient.GetAsync<SongDetailDto>($"/songs/{songList[i].SongId}");
                    var results = await Task.WhenAll(tasks);
                    for (int i = start; i < end; i++)
                    {
                        var detail = results[i - start];
                        if (detail.Ok && detail.Data?.Charts != null) details[i] = detail.Data;
                        else Debug.LogWarning($"[ServerSongLibrary] {songList[i].SongId} 詳細取得失敗 ({detail.ErrorCode})");
                    }
                }

                // 2. 曲登録 + 譜面ジョブ収集 (共有 Dictionary/List への書込はメインスレッド逐次で安全)。
                var chartJobs = new List<(string songId, ChartInfoDto info)>();
                for (int i = 0; i < songList.Count; i++)
                {
                    var song = songList[i];
                    _songs[song.SongId] = song;
                    index.Songs.Add(song);
                    if (details[i]?.Charts == null) continue;
                    foreach (var info in details[i].Charts) chartJobs.Add((song.SongId, info));
                }

                // 3. 譜面を並列でキャッシュ確保 (DL 同時 + hash/disk は EnsureChartCachedAsync 内で Task.Run)。
                for (int start = 0; start < chartJobs.Count; start += Batch)
                {
                    int end = System.Math.Min(start + Batch, chartJobs.Count);
                    var tasks = new Task<bool>[end - start];
                    for (int i = start; i < end; i++)
                        tasks[i - start] = EnsureChartCachedAsync(chartJobs[i].songId, chartJobs[i].info);
                    var results = await Task.WhenAll(tasks);
                    for (int i = start; i < end; i++)
                    {
                        var info = chartJobs[i].info;
                        if (results[i - start])
                        {
                            okCharts++;
                            index.Charts.Add(new IndexEntry
                            {
                                SongId = chartJobs[i].songId, Difficulty = info.Difficulty,
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
            string chartHash  = info.ChartHash;
            string json = null;

            // ディスク読込 + SHA-256 照合はワーカースレッドへ (メインスレッドの同期IOブロックを排除)。
            // Sha256Hex は呼び出し毎に SHA256.Create() するためスレッドセーフ。cachePath は chartId+version で
            // 一意なので並列書込でも衝突しない。
            if (File.Exists(cachePath))
                json = await Task.Run(() =>
                {
                    var bytes = File.ReadAllBytes(cachePath);
                    return Sha256Hex(bytes) == chartHash
                        ? System.Text.Encoding.UTF8.GetString(bytes) : null;
                });

            if (json == null)
            {
                var dl = await ApiClient.DownloadAsync($"/charts/{info.ChartId}/download");
                if (!dl.Ok || dl.Data == null || dl.Data.Length == 0)
                {
                    Debug.LogWarning($"[ServerSongLibrary] {info.ChartId} DL失敗 ({dl.ErrorCode})");
                    return false;
                }

                var data = dl.Data;
                var (actualHash, text) = await Task.Run(() =>
                {
                    string h = Sha256Hex(data);
                    File.WriteAllBytes(cachePath, data);
                    return (h, System.Text.Encoding.UTF8.GetString(data));
                });
                if (actualHash != chartHash)
                    Debug.LogWarning($"[ServerSongLibrary] {info.ChartId} hash不一致: api={chartHash} actual={actualHash} (サーバー申告値を採用)");
                json = text;
            }

            return RegisterChart(songId, info.Difficulty, json, info.Level, info.ChartHash, info.ChartId);
        }

        static bool RegisterChart(string songId, string difficulty, string json, int level, string chartHash, string chartId)
        {
            try
            {
                var chart = ServerChartConverter.Convert(json, level, chartHash);
                _charts[Key(songId, difficulty)] = chart;
                _chartIds[Key(songId, difficulty)] = chartId;   // スコア提出 (score/validate) 用
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ServerSongLibrary] {chartId} 変換失敗: {e.Message}");
                return false;
            }
        }

        static readonly Dictionary<string, string> _chartIds = new Dictionary<string, string>();

        /// <summary>サーバー配信譜面の chart_id を返す (スコア提出用)。未同期/ローカル専用譜面は false。</summary>
        public static bool TryGetChartId(string songId, string difficulty, out string chartId)
            => _chartIds.TryGetValue(Key(songId, difficulty), out chartId);

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
