// Requires sqlite-net-pcl. Enable by adding SQLITE_NET_PCL to Scripting Define Symbols
// after installing the package (NuGetForUnity → sqlite-net-pcl, or manual DLL placement).
#if SQLITE_NET_PCL
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SQLite;
using UnityEngine;

public class SqlitePlayRecordRepository : IPlayRecordRepository
{
    SQLiteAsyncConnection _db;

    public async Task InitializeAsync(string dbPath)
    {
        _db = new SQLiteAsyncConnection(dbPath);
        await _db.CreateTableAsync<PlayRow>();
        await _db.CreateTableAsync<PersonalBestRow>();
        Debug.Log("[Repo] SQLite initialized at " + dbPath);
    }

    public async Task<bool> SaveAsync(PlayRecord record)
    {
        if (_db == null) return false;
        try
        {
            await _db.InsertAsync(RowMapper.ToRow(record));
            await UpdatePersonalBestAsync(record);
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError("[Repo] SaveAsync failed: " + e.Message);
            return false;
        }
    }

    async Task UpdatePersonalBestAsync(PlayRecord record)
    {
        string key     = PersonalBestRow.MakeKey(record.SongId, record.Difficulty);
        var    existing = await _db.FindAsync<PersonalBestRow>(key);

        if (existing == null)
        {
            await _db.InsertAsync(new PersonalBestRow
            {
                CompositeKey        = key,
                SongId              = record.SongId,
                Difficulty          = record.Difficulty,
                BestPlayId          = record.PlayId,
                BestEffectiveScore  = record.EffectiveScore,
                BestRank            = record.Rank,
                BestMaxCombo        = record.MaxCombo,
                HasFullComboInt      = record.IsFullCombo      ? 1 : 0,
                HasAllPerfectInt     = record.IsAllPerfect     ? 1 : 0,
                HasAllPerfectPlusInt = record.IsAllPerfectPlus ? 1 : 0,
                TotalPlays          = 1,
                FirstPlayedAt       = record.PlayedAtUnixMs,
                LastPlayedAt        = record.PlayedAtUnixMs,
            });
            return;
        }

        existing.TotalPlays++;
        existing.LastPlayedAt = record.PlayedAtUnixMs;

        // Achievement flags accumulate
        if (record.IsFullCombo)      existing.HasFullComboInt      = 1;
        if (record.IsAllPerfect)     existing.HasAllPerfectInt     = 1;
        if (record.IsAllPerfectPlus) existing.HasAllPerfectPlusInt = 1;

        if (record.EffectiveScore > existing.BestEffectiveScore)
        {
            existing.BestPlayId         = record.PlayId;
            existing.BestEffectiveScore = record.EffectiveScore;
            existing.BestRank           = record.Rank;
            existing.BestMaxCombo       = record.MaxCombo;
        }

        await _db.UpdateAsync(existing);
    }

    public async Task<PlayRecord> GetByIdAsync(string playId)
    {
        if (_db == null) return null;
        return RowMapper.ToRecord(await _db.FindAsync<PlayRow>(playId));
    }

    public async Task<PersonalBest> GetBestAsync(string songId, string difficulty)
    {
        if (_db == null) return null;
        var key = PersonalBestRow.MakeKey(songId, difficulty);
        return RowMapper.ToBest(await _db.FindAsync<PersonalBestRow>(key));
    }

    public async Task<List<PersonalBest>> GetAllBestsAsync()
    {
        if (_db == null) return new List<PersonalBest>();
        var rows = await _db.Table<PersonalBestRow>().ToListAsync();
        return rows.Select(RowMapper.ToBest).ToList();
    }

    public async Task<List<PlayRecord>> GetHistoryAsync(string songId, string difficulty, int limit = 50)
    {
        if (_db == null) return new List<PlayRecord>();
        var rows = await _db.Table<PlayRow>()
            .Where(r => r.SongId == songId && r.Difficulty == difficulty)
            .OrderByDescending(r => r.PlayedAtUnixMs)
            .Take(limit)
            .ToListAsync();
        return rows.Select(RowMapper.ToRecord).ToList();
    }

    public async Task<List<PlayRecord>> GetAllHistoryAsync(int limit = 50, int offset = 0)
    {
        if (_db == null) return new List<PlayRecord>();
        var rows = await _db.QueryAsync<PlayRow>(
            "SELECT * FROM plays ORDER BY PlayedAtUnixMs DESC LIMIT ? OFFSET ?",
            limit, offset);
        return rows.Select(RowMapper.ToRecord).ToList();
    }

    public async Task<int> GetTotalPlaysAsync()
    {
        if (_db == null) return 0;
        return await _db.Table<PlayRow>().CountAsync();
    }

    public async Task<bool> DeleteAllAsync()
    {
        if (_db == null) return false;
        await _db.DeleteAllAsync<PlayRow>();
        await _db.DeleteAllAsync<PersonalBestRow>();
        return true;
    }

    public System.Threading.Tasks.Task CloseAsync()
        => _db?.CloseAsync() ?? System.Threading.Tasks.Task.CompletedTask;
}
#endif
