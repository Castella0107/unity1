// Requires sqlite-net-pcl. Enable by adding SQLITE_NET_PCL to Scripting Define Symbols
// after installing the package (NuGetForUnity → sqlite-net-pcl, or manual DLL placement).
#if SQLITE_NET_PCL
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SQLite;
using UnityEngine;

/// <summary>
/// SQLite を永続化バックエンドとして使用する IPlayRecordRepository の実装。
/// プレイ記録の保存・取得と自己ベストの自動更新を担当する。
/// </summary>
public class SqlitePlayRecordRepository : IPlayRecordRepository
{
    SQLiteAsyncConnection _db;

    /// <inheritdoc/>
    public async Task InitializeAsync(string dbPath)
    {
        _db = new SQLiteAsyncConnection(dbPath);
        await _db.CreateTableAsync<PlayRow>();
        await _db.CreateTableAsync<PersonalBestRow>();
        await _db.CreateTableAsync<PvpMatchRow>();
        Debug.Log("[Repo] SQLite initialized at " + dbPath);
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public async Task<PlayRecord> GetByIdAsync(string playId)
    {
        if (_db == null) return null;
        return RowMapper.ToRecord(await _db.FindAsync<PlayRow>(playId));
    }

    /// <inheritdoc/>
    public async Task<PersonalBest> GetBestAsync(string songId, string difficulty)
    {
        if (_db == null) return null;
        var key = PersonalBestRow.MakeKey(songId, difficulty);
        return RowMapper.ToBest(await _db.FindAsync<PersonalBestRow>(key));
    }

    /// <inheritdoc/>
    public async Task<List<PersonalBest>> GetAllBestsAsync()
    {
        if (_db == null) return new List<PersonalBest>();
        var rows = await _db.Table<PersonalBestRow>().ToListAsync();
        return rows.Select(RowMapper.ToBest).ToList();
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public async Task<List<PlayRecord>> GetAllHistoryAsync(int limit = 50, int offset = 0)
    {
        if (_db == null) return new List<PlayRecord>();
        var rows = await _db.QueryAsync<PlayRow>(
            "SELECT * FROM plays ORDER BY PlayedAtUnixMs DESC LIMIT ? OFFSET ?",
            limit, offset);
        return rows.Select(RowMapper.ToRecord).ToList();
    }

    /// <inheritdoc/>
    public async Task<int> GetTotalPlaysAsync()
    {
        if (_db == null) return 0;
        return await _db.Table<PlayRow>().CountAsync();
    }

    /// <inheritdoc/>
    public async Task ClearReplayPathAsync(string playId)
    {
        if (_db == null) return;
        var row = await _db.FindAsync<PlayRow>(playId);
        if (row != null) { row.ReplayPath = null; await _db.UpdateAsync(row); }
    }

    // ── PVP ローカル履歴 ──────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task SavePvpMatchAsync(PvpMatchRecord match)
    {
        if (_db == null || match == null) return;
        await _db.InsertOrReplaceAsync(RowMapper.ToPvpRow(match));
    }

    /// <inheritdoc/>
    public async Task<List<PvpMatchRecord>> GetRecentPvpMatchesAsync(int limit = 10)
    {
        if (_db == null) return new List<PvpMatchRecord>();
        var rows = await _db.QueryAsync<PvpMatchRow>(
            "SELECT * FROM pvp_matches ORDER BY CompletedAtUnixMs DESC LIMIT ?", limit);
        return rows.Select(RowMapper.ToPvpRecord).ToList();
    }

    /// <inheritdoc/>
    public async Task<List<PvpMatchRecord>> GetStalePvpMatchesAsync(int keep)
    {
        if (_db == null) return new List<PvpMatchRecord>();
        // LIMIT -1 = 無制限。新しい順に keep 件をスキップした残りが刈り取り対象。
        var rows = await _db.QueryAsync<PvpMatchRow>(
            "SELECT * FROM pvp_matches ORDER BY CompletedAtUnixMs DESC LIMIT -1 OFFSET ?", keep);
        return rows.Select(RowMapper.ToPvpRecord).ToList();
    }

    /// <inheritdoc/>
    public async Task DeletePvpMatchAsync(string matchId)
    {
        if (_db == null) return;
        await _db.DeleteAsync<PvpMatchRow>(matchId);
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAllAsync()
    {
        if (_db == null) return false;
        await _db.DeleteAllAsync<PlayRow>();
        await _db.DeleteAllAsync<PersonalBestRow>();
        await _db.DeleteAllAsync<PvpMatchRow>();
        return true;
    }

    /// <summary>SQLite 接続を閉じる。</summary>
    public System.Threading.Tasks.Task CloseAsync()
        => _db?.CloseAsync() ?? System.Threading.Tasks.Task.CompletedTask;
}
#endif
