#if SQLITE_NET_PCL
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SQLite;
using UnityEngine;

/// <summary>
/// SQLite を永続化バックエンドとして使用する IOffsetRepository の実装。
/// デバイスプロファイル、楽曲ごとのオフセット、アクティブプロファイル ID を管理する。
/// </summary>
public class SqliteOffsetRepository : IOffsetRepository
{
    SQLiteAsyncConnection _db;
    const string ActiveProfileKey = "active_profile_id";

    public async Task InitializeAsync(string dbPath)
    {
        _db = new SQLiteAsyncConnection(dbPath);
        await _db.CreateTableAsync<DeviceProfileRow>();
        await _db.CreateTableAsync<PerSongOffsetRow>();
        await _db.CreateTableAsync<KeyValueRow>();

        // Seed default profile on first run
        if (await _db.FindAsync<DeviceProfileRow>("default") == null)
        {
            await _db.InsertAsync(ToRow(DeviceProfile.CreateDefault()));
            await SetActiveProfileIdAsync("default");
        }

        Debug.Log("[OffsetRepo] SQLite initialized at " + dbPath);
    }

    public async Task<List<DeviceProfile>> GetAllProfilesAsync()
    {
        var rows = await _db.Table<DeviceProfileRow>().ToListAsync();
        return rows.Select(ToProfile).ToList();
    }

    public async Task<DeviceProfile> GetProfileByIdAsync(string profileId)
    {
        return ToProfile(await _db.FindAsync<DeviceProfileRow>(profileId));
    }

    public async Task<DeviceProfile> GetProfileByOsDeviceNameAsync(string osDeviceName)
    {
        if (string.IsNullOrEmpty(osDeviceName)) return null;
        var row = await _db.Table<DeviceProfileRow>()
            .Where(r => r.OsDeviceName == osDeviceName)
            .FirstOrDefaultAsync();
        return ToProfile(row);
    }

    public async Task<bool> SaveProfileAsync(DeviceProfile profile)
    {
        try
        {
            var row = ToRow(profile);
            row.UpdatedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await _db.InsertOrReplaceAsync(row);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("[OffsetRepo] SaveProfile failed: " + e.Message);
            return false;
        }
    }

    public async Task<bool> DeleteProfileAsync(string profileId)
    {
        if (profileId == "default")
        {
            Debug.LogWarning("[OffsetRepo] Cannot delete default profile");
            return false;
        }
        int deleted = await _db.DeleteAsync<DeviceProfileRow>(profileId);
        return deleted > 0;
    }

    public async Task<string> GetActiveProfileIdAsync()
    {
        var row = await _db.FindAsync<KeyValueRow>(ActiveProfileKey);
        return row?.Value ?? "default";
    }

    public async Task<bool> SetActiveProfileIdAsync(string profileId)
    {
        try
        {
            await _db.InsertOrReplaceAsync(new KeyValueRow { Key = ActiveProfileKey, Value = profileId });
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("[OffsetRepo] SetActiveProfileId failed: " + e.Message);
            return false;
        }
    }

    public async Task<PerSongOffset> GetPerSongOffsetAsync(string songId)
    {
        var row = await _db.FindAsync<PerSongOffsetRow>(songId);
        if (row == null) return PerSongOffset.DefaultFor(songId);
        return new PerSongOffset
        {
            SongId           = row.SongId,
            JudgmentOffsetMs = row.JudgmentOffsetMs,
            UpdatedAtUnixMs  = row.UpdatedAtUnixMs,
        };
    }

    public async Task<bool> SavePerSongOffsetAsync(PerSongOffset offset)
    {
        try
        {
            var clamped = offset.Clamped();
            await _db.InsertOrReplaceAsync(new PerSongOffsetRow
            {
                SongId           = clamped.SongId,
                JudgmentOffsetMs = clamped.JudgmentOffsetMs,
                UpdatedAtUnixMs  = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            });
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("[OffsetRepo] SavePerSongOffset failed: " + e.Message);
            return false;
        }
    }

    // ── Row ↔ Domain ─────────────────────────────────────────────────────────

    static DeviceProfile ToProfile(DeviceProfileRow row)
    {
        if (row == null) return null;
        return new DeviceProfile
        {
            ProfileId           = row.ProfileId,
            DisplayName         = row.DisplayName,
            OsDeviceName        = row.OsDeviceName,
            IsAutoSwitchEnabled = row.IsAutoSwitchEnabledInt != 0,
            Offsets = new AppOffsetSettings
            {
                JudgmentOffsetMs = row.JudgmentOffsetMs,
                VisualOffsetMs   = row.VisualOffsetMs,
            },
            CreatedAtUnixMs = row.CreatedAtUnixMs,
            UpdatedAtUnixMs = row.UpdatedAtUnixMs,
        };
    }

    static DeviceProfileRow ToRow(DeviceProfile p)
    {
        var clamped = (p.Offsets ?? AppOffsetSettings.Default).Clamped();
        return new DeviceProfileRow
        {
            ProfileId              = p.ProfileId,
            DisplayName            = p.DisplayName,
            OsDeviceName           = p.OsDeviceName,
            IsAutoSwitchEnabledInt = p.IsAutoSwitchEnabled ? 1 : 0,
            JudgmentOffsetMs       = clamped.JudgmentOffsetMs,
            VisualOffsetMs         = clamped.VisualOffsetMs,
            CreatedAtUnixMs        = p.CreatedAtUnixMs,
            UpdatedAtUnixMs        = p.UpdatedAtUnixMs,
        };
    }

    public System.Threading.Tasks.Task CloseAsync()
        => _db?.CloseAsync() ?? System.Threading.Tasks.Task.CompletedTask;
}
#endif
