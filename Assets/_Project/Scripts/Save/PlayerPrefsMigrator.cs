using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// PlayerPrefs に保存されていた旧形式のベストスコアデータを SQLite リポジトリへ移行する静的クラス。
/// DB 初期化後の初回起動時に一度だけ実行される。
/// </summary>
// Migrates legacy PlayerPrefs best-score data to the SQLite repository.
// Runs once on first launch after DB initialization.
public static class PlayerPrefsMigrator
{
    const string MigrationFlagKey = "DBMigrationComplete_v1";

    public static async Task MigrateIfNeeded(IPlayRecordRepository repo)
    {
        if (PlayerPrefs.GetInt(MigrationFlagKey, 0) == 1) return;

        Debug.Log("[Migration] Starting PlayerPrefs → SQLite migration");
        int count = 0;

        var songsRoot = Path.Combine(Application.streamingAssetsPath, "Songs");
        if (!Directory.Exists(songsRoot))
        {
            Finalize();
            return;
        }

        string[] difficulties = { "easy", "normal", "hard", "extra" };
        foreach (var dir in Directory.GetDirectories(songsRoot))
        {
            string songId = Path.GetFileName(dir);
            foreach (string diff in difficulties)
            {
                string key = string.Format("Best_{0}_{1}", songId, diff);
                if (!PlayerPrefs.HasKey(key)) continue;

                int score = PlayerPrefs.GetInt(key, 0);
                if (score <= 0) continue;

                await repo.SaveAsync(MakeStubRecord(songId, diff, score));
                PlayerPrefs.DeleteKey(key);
                count++;
            }
        }

        Finalize();
        Debug.Log(string.Format("[Migration] Done — migrated {0} records", count));
    }

    static PlayRecord MakeStubRecord(string songId, string difficulty, int effectiveScore)
    {
        return new PlayRecord
        {
            PlayId               = "migrated_" + Guid.NewGuid().ToString(),
            SongId               = songId,
            Difficulty           = difficulty,
            PlayedAtUnixMs       = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            RawScore             = effectiveScore,
            EffectiveScore       = effectiveScore,
            Rank                 = ScoreCalculator.ComputeRank(effectiveScore),
            PerfectPlusCount     = 0,
            PerfectCount         = 0,
            GreatCount           = 0,
            GoodCount            = 0,
            MissCount            = 0,
            MaxCombo             = 0,
            FastCount            = 0,
            LateCount            = 0,
            TotalNotes           = 0,
            SectorScores         = new[] { 0, 0, 0, 0, 0 },
            IsFullCombo          = false,
            IsAllPerfect         = effectiveScore >= 1_000_000,
            IsAllPerfectPlus     = false,
            Modifiers            = new string[0],
            IsPvP                = false,
            ChartHash            = "migrated",
            JudgmentEngineVersion = "migrated",
        };
    }

    static void Finalize()
    {
        PlayerPrefs.SetInt(MigrationFlagKey, 1);
        PlayerPrefs.Save();
    }
}
