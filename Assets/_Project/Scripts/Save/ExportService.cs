using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// ローカルデータ(SQLite + PlayerPrefs ホワイトリスト)を JSON 1ファイルにエクスポートする。
/// </summary>
/// <remarks>
/// Songs ライブラリとリプレイバイナリは対象外。Import 復元時の互換性確保のため schemaVersion を埋める。
/// </remarks>
public static class ExportService
{
    /// <summary>エクスポート対象とする PlayerPrefs キー(型情報付き)。</summary>
    static readonly (string Key, PrefType Type)[] ExportedPrefs =
    {
        // Display
        ("ResolutionIdx",        PrefType.Int),
        ("ScreenModeIdx",        PrefType.Int),
        ("FpsLimitIdx",          PrefType.Int),
        ("VSync",                PrefType.Int),
        ("CameraAngleIdx",       PrefType.Int),
        ("BloomLevelIdx",        PrefType.Int),
        ("MotionEffects",        PrefType.Int),
        ("ShowFps",              PrefType.Int),
        // Audio volumes
        ("Vol_Master",           PrefType.Float),
        ("Vol_Music",            PrefType.Float),
        ("Vol_Sfx",              PrefType.Float),
        // Game
        ("HiSpeed",              PrefType.Float),
        ("ComboBorderIdx",       PrefType.Int),
        ("ShowFastLate",         PrefType.Int),
        ("BgEffectsIntensity",   PrefType.Float),
        ("JudgmentEffectStyleIdx", PrefType.Int),
        // Account
        ("DisplayName",          PrefType.String),
        ("StatusMessage",        PrefType.String),
        ("NotificationsEnabled", PrefType.Int),
        // Sync mode
        ("SyncModeIdx",          PrefType.Int),
    };

    enum PrefType { Int, Float, String }

    public readonly struct Result
    {
        public readonly bool   Success;
        public readonly string FilePath;
        public readonly string FailureReason;

        public Result(bool success, string filePath, string reason)
        {
            Success       = success;
            FilePath      = filePath;
            FailureReason = reason;
        }
    }

    /// <summary>
    /// すべてのローカルデータを収集し、persistentDataPath/exports/ 配下に JSON ファイルとして保存する。
    /// </summary>
    public static async Task<Result> RunAsync()
    {
        try
        {
            var repo = RepositoryService.Instance;
            if (repo == null || !repo.IsReady)
                return new Result(false, null, "RepositoryService not ready");

            var schema = new ExportSchema
            {
                ExportedAt = DateTimeOffset.UtcNow.ToString("o"),
                AppVersion = Application.version,
            };

            schema.PlayRecords    = await repo.PlayRecords.GetAllHistoryAsync(limit: int.MaxValue, offset: 0);
            schema.DeviceProfiles = await repo.Offsets.GetAllProfilesAsync();
            schema.ActiveProfileId = await repo.Offsets.GetActiveProfileIdAsync();
            schema.PerSongOffsets = await repo.Offsets.GetAllPerSongOffsetsAsync();
            schema.PersonalBests  = await repo.PlayRecords.GetAllBestsAsync();
            schema.PlayerPrefs    = CollectPlayerPrefs();

            string exportsDir = Path.Combine(Application.persistentDataPath, "exports");
            if (!Directory.Exists(exportsDir)) Directory.CreateDirectory(exportsDir);

            string fileName = "pvp_backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json";
            string filePath = Path.Combine(exportsDir, fileName);

            string json = JsonConvert.SerializeObject(schema, Formatting.Indented);
            File.WriteAllText(filePath, json);

            Debug.Log("[Export] Saved to " + filePath);
            return new Result(true, filePath, null);
        }
        catch (Exception e)
        {
            Debug.LogError("[Export] FAILED: " + e.GetType().Name + " — " + e.Message);
            return new Result(false, null, e.Message);
        }
    }

    /// <summary>エクスプローラー(または OS 既定のファイルマネージャ)で保存先フォルダを開く。</summary>
    public static void RevealInFileExplorer(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return;
        try
        {
            string folder = Path.GetDirectoryName(filePath);
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            // Windows: explorer /select で対象ファイルを選択状態で開く
            System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + filePath.Replace("/", "\\") + "\"");
#else
            Application.OpenURL("file://" + folder);
#endif
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Export] Failed to reveal file: " + e.Message);
        }
    }

    static Dictionary<string, object> CollectPlayerPrefs()
    {
        var dict = new Dictionary<string, object>();
        foreach (var (key, type) in ExportedPrefs)
        {
            if (!PlayerPrefs.HasKey(key)) continue;
            switch (type)
            {
                case PrefType.Int:    dict[key] = PlayerPrefs.GetInt(key);    break;
                case PrefType.Float:  dict[key] = PlayerPrefs.GetFloat(key);  break;
                case PrefType.String: dict[key] = PlayerPrefs.GetString(key); break;
            }
        }
        return dict;
    }
}
