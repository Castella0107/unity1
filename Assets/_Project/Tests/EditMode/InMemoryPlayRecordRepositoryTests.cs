using System;
using System.Threading.Tasks;
using NUnit.Framework;

/// <summary><see cref="InMemoryPlayRecordRepository"/> のユニットテスト。</summary>
public class InMemoryPlayRecordRepositoryTests
{
    IPlayRecordRepository _repo;

    [SetUp]
    public void Setup()
    {
        _repo = new InMemoryPlayRecordRepository();
        _repo.InitializeAsync(null).GetAwaiter().GetResult();
    }

    [Test]
    public async Task SaveAndGetById()
    {
        var rec = MakeRecord("song1", "extra", 950_000);
        await _repo.SaveAsync(rec);

        var got = await _repo.GetByIdAsync(rec.PlayId);
        Assert.IsNotNull(got);
        Assert.AreEqual(950_000, got.EffectiveScore);
        Assert.AreEqual("song1", got.SongId);
    }

    [Test]
    public async Task GetBest_NoRecord_ReturnsNull()
    {
        var best = await _repo.GetBestAsync("nonexistent", "extra");
        Assert.IsNull(best);
    }

    [Test]
    public async Task GetBest_MultipleRecords_ReturnsHighest()
    {
        await _repo.SaveAsync(MakeRecord("song1", "extra", 800_000));
        await _repo.SaveAsync(MakeRecord("song1", "extra", 950_000));
        await _repo.SaveAsync(MakeRecord("song1", "extra", 900_000));

        var best = await _repo.GetBestAsync("song1", "extra");
        Assert.AreEqual(950_000, best.BestEffectiveScore);
        Assert.AreEqual(3, best.TotalPlays);
    }

    [Test]
    public async Task GetBest_AchievementsAccumulate()
    {
        await _repo.SaveAsync(MakeRecord("song1", "extra", 800_000, isFullCombo: true));
        await _repo.SaveAsync(MakeRecord("song1", "extra", 900_000, isFullCombo: false));

        var best = await _repo.GetBestAsync("song1", "extra");
        Assert.IsTrue(best.HasFullCombo);   // retained from first play
    }

    [Test]
    public async Task GetBest_SeparatedByDifficulty()
    {
        await _repo.SaveAsync(MakeRecord("song1", "hard",  600_000));
        await _repo.SaveAsync(MakeRecord("song1", "extra", 950_000));

        var hardBest  = await _repo.GetBestAsync("song1", "hard");
        var extraBest = await _repo.GetBestAsync("song1", "extra");

        Assert.AreEqual(600_000, hardBest.BestEffectiveScore);
        Assert.AreEqual(950_000, extraBest.BestEffectiveScore);
    }

    [Test]
    public async Task GetHistory_OrderedByPlayedAtDesc()
    {
        await _repo.SaveAsync(MakeRecord("song1", "extra", 800_000, playedAt: 1000));
        await _repo.SaveAsync(MakeRecord("song1", "extra", 850_000, playedAt: 3000));
        await _repo.SaveAsync(MakeRecord("song1", "extra", 900_000, playedAt: 2000));

        var history = await _repo.GetHistoryAsync("song1", "extra");
        Assert.AreEqual(3, history.Count);
        Assert.AreEqual(3000, history[0].PlayedAtUnixMs);
        Assert.AreEqual(2000, history[1].PlayedAtUnixMs);
        Assert.AreEqual(1000, history[2].PlayedAtUnixMs);
    }

    [Test]
    public async Task DeleteAll_ClearsEverything()
    {
        await _repo.SaveAsync(MakeRecord("song1", "extra", 900_000));
        await _repo.DeleteAllAsync();

        Assert.AreEqual(0, await _repo.GetTotalPlaysAsync());
        Assert.IsNull(await _repo.GetBestAsync("song1", "extra"));
    }

    [Test]
    public async Task GetTotalPlays_CountsAllRecords()
    {
        await _repo.SaveAsync(MakeRecord("song1", "extra", 900_000));
        await _repo.SaveAsync(MakeRecord("song1", "hard",  800_000));
        await _repo.SaveAsync(MakeRecord("song2", "extra", 700_000));

        Assert.AreEqual(3, await _repo.GetTotalPlaysAsync());
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    static PlayRecord MakeRecord(
        string songId, string diff, int effectiveScore,
        bool isFullCombo = false, bool isAllPerfect = false,
        long playedAt = 0)
    {
        return new PlayRecord
        {
            PlayId           = Guid.NewGuid().ToString(),
            SongId           = songId,
            Difficulty       = diff,
            PlayedAtUnixMs   = playedAt > 0 ? playedAt : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            RawScore         = effectiveScore,
            EffectiveScore   = effectiveScore,
            Rank             = "S",
            PerfectPlusCount = 100,
            MaxCombo         = 100,
            TotalNotes       = 100,
            SectorScores     = new[] { 0, 0, 0, 0, 0 },
            IsFullCombo      = isFullCombo,
            IsAllPerfect     = isAllPerfect,
            Modifiers        = new string[0],
        };
    }
}
