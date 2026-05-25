using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// Unity-independent. No UnityEngine references allowed in this assembly.
// Used in tests and as a runtime fallback when SQLite is unavailable.
/// <summary>
/// <see cref="IPlayRecordRepository"/> のインメモリ実装。
/// プレイ記録とパーソナルベストをメモリ上で管理し、テストや SQLite 非利用時の
/// ランタイムフォールバックとして使用する。
/// </summary>
public class InMemoryPlayRecordRepository : IPlayRecordRepository
{
    readonly Dictionary<string, PlayRecord>   _records = new Dictionary<string, PlayRecord>();
    readonly Dictionary<string, PersonalBest> _bests   = new Dictionary<string, PersonalBest>();

    /// <inheritdoc/>
    public Task InitializeAsync(string dbPath) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task<bool> SaveAsync(PlayRecord record)
    {
        _records[record.PlayId] = record;
        UpdateBest(record);
        return Task.FromResult(true);
    }

    void UpdateBest(PlayRecord record)
    {
        string key = record.SongId + ":" + record.Difficulty;

        if (!_bests.TryGetValue(key, out var existing))
        {
            _bests[key] = new PersonalBest
            {
                SongId             = record.SongId,
                Difficulty         = record.Difficulty,
                BestPlayId         = record.PlayId,
                BestEffectiveScore = record.EffectiveScore,
                BestRank           = record.Rank,
                BestMaxCombo       = record.MaxCombo,
                HasFullCombo       = record.IsFullCombo,
                HasAllPerfect      = record.IsAllPerfect,
                HasAllPerfectPlus  = record.IsAllPerfectPlus,
                TotalPlays         = 1,
                FirstPlayedAt      = record.PlayedAtUnixMs,
                LastPlayedAt       = record.PlayedAtUnixMs,
            };
            return;
        }

        bool isBetter = record.EffectiveScore > existing.BestEffectiveScore;
        _bests[key] = new PersonalBest
        {
            SongId             = existing.SongId,
            Difficulty         = existing.Difficulty,
            BestPlayId         = isBetter ? record.PlayId         : existing.BestPlayId,
            BestEffectiveScore = isBetter ? record.EffectiveScore  : existing.BestEffectiveScore,
            BestRank           = isBetter ? record.Rank            : existing.BestRank,
            BestMaxCombo       = isBetter ? record.MaxCombo        : existing.BestMaxCombo,
            // Achievement flags accumulate — once achieved, always shown
            HasFullCombo       = existing.HasFullCombo      || record.IsFullCombo,
            HasAllPerfect      = existing.HasAllPerfect     || record.IsAllPerfect,
            HasAllPerfectPlus  = existing.HasAllPerfectPlus || record.IsAllPerfectPlus,
            TotalPlays         = existing.TotalPlays + 1,
            FirstPlayedAt      = existing.FirstPlayedAt,
            LastPlayedAt       = record.PlayedAtUnixMs,
        };
    }

    /// <inheritdoc/>
    public Task<PlayRecord> GetByIdAsync(string playId)
    {
        _records.TryGetValue(playId, out var r);
        return Task.FromResult(r);
    }

    /// <inheritdoc/>
    public Task<PersonalBest> GetBestAsync(string songId, string difficulty)
    {
        _bests.TryGetValue(songId + ":" + difficulty, out var b);
        return Task.FromResult(b);
    }

    /// <inheritdoc/>
    public Task<List<PersonalBest>> GetAllBestsAsync() =>
        Task.FromResult(_bests.Values.ToList());

    /// <inheritdoc/>
    public Task<List<PlayRecord>> GetHistoryAsync(string songId, string difficulty, int limit = 50) =>
        Task.FromResult(_records.Values
            .Where(r => r.SongId == songId && r.Difficulty == difficulty)
            .OrderByDescending(r => r.PlayedAtUnixMs)
            .Take(limit).ToList());

    /// <inheritdoc/>
    public Task<List<PlayRecord>> GetAllHistoryAsync(int limit = 50, int offset = 0) =>
        Task.FromResult(_records.Values
            .OrderByDescending(r => r.PlayedAtUnixMs)
            .Skip(offset).Take(limit).ToList());

    /// <inheritdoc/>
    public Task<int> GetTotalPlaysAsync() => Task.FromResult(_records.Count);

    /// <inheritdoc/>
    public Task<bool> DeleteAllAsync()
    {
        _records.Clear();
        _bests.Clear();
        return Task.FromResult(true);
    }
}
