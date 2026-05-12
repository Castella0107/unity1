using System;
using System.Threading.Tasks;
using UnityEngine;

// Migrates legacy PlayerPrefs offset settings to the SQLite offset repository.
// Runs once on first launch (guarded by a migration flag).
public static class PlayerPrefsOffsetMigrator
{
    const string MigrationFlagKey = "OffsetMigrationComplete_v1";

    public static async Task MigrateIfNeeded(IOffsetRepository repo)
    {
        if (PlayerPrefs.GetInt(MigrationFlagKey, 0) == 1) return;

        Debug.Log("[OffsetMigration] Starting PlayerPrefs → SQLite migration");

        // Keys written by SongSelectController and read by SimpleCalibration
        bool hasJudge  = PlayerPrefs.HasKey("JudgmentOffsetMs");
        bool hasVisual = PlayerPrefs.HasKey("VisualOffsetMs");

        if (hasJudge || hasVisual)
        {
            int judge  = PlayerPrefs.GetInt("JudgmentOffsetMs", 0);
            int visual = PlayerPrefs.GetInt("VisualOffsetMs",   0);

            var def = await repo.GetProfileByIdAsync("default");
            if (def != null)
            {
                def.Offsets             = new AppOffsetSettings { JudgmentOffsetMs = judge, VisualOffsetMs = visual };
                def.UpdatedAtUnixMs     = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                await repo.SaveProfileAsync(def);
                Debug.Log(string.Format("[OffsetMigration] Migrated J={0}, V={1} to default profile", judge, visual));
            }

            if (hasJudge)  PlayerPrefs.DeleteKey("JudgmentOffsetMs");
            if (hasVisual) PlayerPrefs.DeleteKey("VisualOffsetMs");
        }

        PlayerPrefs.SetInt(MigrationFlagKey, 1);
        PlayerPrefs.Save();
        Debug.Log("[OffsetMigration] Done");
    }
}
