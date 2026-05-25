using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// Export で出力された JSON を読み込み、ローカルデータを全置換で復元する。
/// </summary>
/// <remarks>
/// マージ戦略は採用せず、既存データを全て削除してから Import 内容で上書きする。
/// schemaVersion の互換性チェックを行い、不一致は失敗とする。
/// </remarks>
public static class ImportService
{
    /// <summary>インポート前のプレビュー結果(読み込み+検証のみ、書き込みはしない)。</summary>
    public readonly struct Preview
    {
        /// <summary>読み込み・検証に成功したか。</summary>
        public readonly bool         Success;
        /// <summary>対象ファイルのパス。</summary>
        public readonly string       FilePath;
        /// <summary>デコードされたスキーマ。</summary>
        public readonly ExportSchema Schema;
        /// <summary>失敗時の理由(成功時は null)。</summary>
        public readonly string       FailureReason;

        /// <summary>プレビュー結果を生成する。</summary>
        public Preview(bool success, string filePath, ExportSchema schema, string reason)
        {
            Success       = success;
            FilePath      = filePath;
            Schema        = schema;
            FailureReason = reason;
        }
    }

    /// <summary>インポート実行の結果。成否・失敗理由と各種別の取り込み件数を保持する。</summary>
    public readonly struct Result
    {
        /// <summary>成功したか。</summary>
        public readonly bool   Success;
        /// <summary>失敗時の理由(成功時は null)。</summary>
        public readonly string FailureReason;
        /// <summary>取り込んだプレイ記録数。</summary>
        public readonly int    ImportedPlayRecords;
        /// <summary>取り込んだデバイスプロファイル数。</summary>
        public readonly int    ImportedProfiles;
        /// <summary>取り込んだ曲別オフセット数。</summary>
        public readonly int    ImportedPerSongOffsets;
        /// <summary>取り込んだパーソナルベスト数。</summary>
        public readonly int    ImportedPersonalBests;
        /// <summary>取り込んだ PlayerPrefs エントリ数。</summary>
        public readonly int    ImportedPlayerPrefs;

        /// <summary>結果を生成する。</summary>
        public Result(bool success, string reason, int plays, int profiles, int perSong, int bests, int prefs)
        {
            Success                = success;
            FailureReason          = reason;
            ImportedPlayRecords    = plays;
            ImportedProfiles       = profiles;
            ImportedPerSongOffsets = perSong;
            ImportedPersonalBests  = bests;
            ImportedPlayerPrefs    = prefs;
        }
    }

    /// <summary>
    /// <c>persistentDataPath/exports/</c> 配下で最新の .json ファイルを返す(無ければ null)。
    /// </summary>
    public static string FindLatestExportFile()
    {
        string dir = Path.Combine(Application.persistentDataPath, "exports");
        if (!Directory.Exists(dir)) return null;

        var files = Directory.GetFiles(dir, "*.json");
        if (files.Length == 0) return null;

        return files
            .OrderByDescending(f => File.GetLastWriteTimeUtc(f))
            .First();
    }

    /// <summary>ファイルを読み込んで ExportSchema にデコードし、互換性チェックを行う。</summary>
    public static Preview LoadAndValidate(string filePath)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return new Preview(false, filePath, null, "file not found");

            string json = File.ReadAllText(filePath);
            var schema = JsonConvert.DeserializeObject<ExportSchema>(json);
            if (schema == null)
                return new Preview(false, filePath, null, "JSON deserialization returned null");

            if (schema.SchemaVersion != ExportSchema.CurrentVersion)
                return new Preview(false, filePath, schema,
                    "schemaVersion mismatch (file=" + schema.SchemaVersion +
                    ", expected=" + ExportSchema.CurrentVersion + ")");

            if (schema.DeviceProfiles == null || schema.DeviceProfiles.Count == 0)
                return new Preview(false, filePath, schema, "no device profiles in file");

            return new Preview(true, filePath, schema, null);
        }
        catch (Exception e)
        {
            return new Preview(false, filePath, null, e.GetType().Name + ": " + e.Message);
        }
    }

    /// <summary>
    /// 全置換 Import を実行する。Preview で検証済みのスキーマを渡す前提。
    /// </summary>
    public static async Task<Result> RunAsync(ExportSchema schema)
    {
        var repo = RepositoryService.Instance;
        if (repo == null || !repo.IsReady)
            return new Result(false, "RepositoryService not ready", 0, 0, 0, 0, 0);
        if (schema == null)
            return new Result(false, "schema is null", 0, 0, 0, 0, 0);

        try
        {
            // 1. 既存データの全削除(default プロファイル以外)
            await repo.PlayRecords.DeleteAllAsync();          // PlayRow + PersonalBestRow
            await repo.Offsets.DeleteAllPerSongOffsetsAsync();

            var existingProfiles = await repo.Offsets.GetAllProfilesAsync();
            foreach (var p in existingProfiles)
                if (p.ProfileId != "default")
                    await repo.Offsets.DeleteProfileAsync(p.ProfileId);

            // 2. Import 投入
            int plays = 0;
            foreach (var rec in schema.PlayRecords ?? new System.Collections.Generic.List<PlayRecord>())
            {
                if (await repo.PlayRecords.SaveAsync(rec)) plays++;
            }

            int profiles = 0;
            foreach (var profile in schema.DeviceProfiles)
            {
                if (await repo.Offsets.SaveProfileAsync(profile)) profiles++;
            }

            int perSong = 0;
            foreach (var off in schema.PerSongOffsets ?? new System.Collections.Generic.List<PerSongOffset>())
            {
                if (await repo.Offsets.SavePerSongOffsetAsync(off)) perSong++;
            }

            // PersonalBest は PlayRecord 保存時に派生更新される設計だが、明示的に持ってきた値もあるので参考まで(現状の Repo API には Save 単体が無いためカウントのみ)
            int bests = schema.PersonalBests?.Count ?? 0;

            // 3. ActiveProfile 切替
            if (!string.IsNullOrEmpty(schema.ActiveProfileId))
                await repo.SetActiveProfileAsync(schema.ActiveProfileId);

            // 4. PlayerPrefs 上書き
            int prefs = 0;
            if (schema.PlayerPrefs != null)
            {
                foreach (var kvp in schema.PlayerPrefs)
                {
                    if (TryApplyPlayerPref(kvp.Key, kvp.Value)) prefs++;
                }
                PlayerPrefs.Save();
            }

            Debug.Log("[Import] OK plays=" + plays + " profiles=" + profiles +
                      " perSong=" + perSong + " prefs=" + prefs);
            return new Result(true, null, plays, profiles, perSong, bests, prefs);
        }
        catch (Exception e)
        {
            Debug.LogError("[Import] FAILED: " + e.GetType().Name + " — " + e.Message);
            return new Result(false, e.Message, 0, 0, 0, 0, 0);
        }
    }

    static bool TryApplyPlayerPref(string key, object value)
    {
        try
        {
            // Newtonsoft.Json は int→long, float→double として JSON から復元する。
            switch (value)
            {
                case long l:   PlayerPrefs.SetInt(key,   (int)l);          return true;
                case int i:    PlayerPrefs.SetInt(key,   i);               return true;
                case double d: PlayerPrefs.SetFloat(key, (float)d);        return true;
                case float f:  PlayerPrefs.SetFloat(key, f);               return true;
                case string s: PlayerPrefs.SetString(key, s);              return true;
                case bool b:   PlayerPrefs.SetInt(key,   b ? 1 : 0);       return true;
                default:
                    Debug.LogWarning("[Import] Skipped PlayerPref " + key +
                                     " (unsupported type: " + value?.GetType().Name + ")");
                    return false;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Import] Failed to set PlayerPref " + key + ": " + e.Message);
            return false;
        }
    }
}
