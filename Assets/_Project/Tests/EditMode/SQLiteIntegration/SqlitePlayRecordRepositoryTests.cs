#if SQLITE_NET_PCL
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;

/// <summary><see cref="SqlitePlayRecordRepository"/> の統合テスト(一時 SQLite DB を使用)。</summary>
public class SqlitePlayRecordRepositoryTests
{
    TempSqliteDb _temp;
    SqlitePlayRecordRepository _repo;

    [SetUp]
    public async Task SetUp()
    {
        _temp = new TempSqliteDb();
        _repo = new SqlitePlayRecordRepository();
        await _repo.InitializeAsync(_temp.FilePath);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _repo.CloseAsync();
        _temp?.Dispose();
    }

    // ── 初期化 ────────────────────────────────────────────────────────────────

    [Test]
    public async Task Initialize_CreatesTables()
    {
        var count = await _repo.GetTotalPlaysAsync();
        Assert.AreEqual(0, count);
    }

    // ── 保存 / 取得 ───────────────────────────────────────────────────────────

    [Test]
    public async Task Save_PersistsAcrossInstances()
    {
        var rec = MakeRecord("song1", "extra", 950_000);
        await _repo.SaveAsync(rec);
        await _repo.CloseAsync();

        var repo2 = new SqlitePlayRecordRepository();
        await repo2.InitializeAsync(_temp.FilePath);
        var got = await repo2.GetByIdAsync(rec.PlayId);
        await repo2.CloseAsync();

        Assert.IsNotNull(got);
        Assert.AreEqual(950_000, got.EffectiveScore);
    }

    [Test]
    public async Task Save_PreservesAllFields()
    {
        var rec = new PlayRecord
        {
            PlayId                = "test-uuid",
            SongId                = "song1",
            Difficulty            = "extra",
            PlayedAtUnixMs        = 1700000000000L,
            RawScore              = 985_000,
            EffectiveScore        = 985_000,
            Rank                  = "S",
            PerfectPlusCount      = 950,
            PerfectCount          = 30,
            GreatCount            = 15,
            GoodCount             = 4,
            MissCount             = 1,
            MaxCombo              = 980,
            FastCount             = 22,
            LateCount             = 18,
            TotalNotes            = 1000,
            SectorScores          = new[] { 200_000, 195_000, 200_000, 195_000, 195_000 },
            IsFullCombo           = false,
            IsAllPerfect          = false,
            IsAllPerfectPlus      = false,
            Modifiers             = new[] { "Mirror" },
            IsPvP                 = false,
            ChartHash             = "abcd1234",
            JudgmentEngineVersion = "1.0.0",
            ReplayPath            = "/tmp/test.replay",
        };

        await _repo.SaveAsync(rec);
        var got = await _repo.GetByIdAsync("test-uuid");

        Assert.AreEqual(rec.SongId,           got.SongId);
        Assert.AreEqual(rec.Difficulty,        got.Difficulty);
        Assert.AreEqual(rec.PlayedAtUnixMs,    got.PlayedAtUnixMs);
        Assert.AreEqual(rec.RawScore,          got.RawScore);
        Assert.AreEqual(rec.PerfectPlusCount,  got.PerfectPlusCount);
        Assert.AreEqual(rec.MissCount,         got.MissCount);
        Assert.AreEqual(rec.MaxCombo,          got.MaxCombo);
        CollectionAssert.AreEqual(rec.SectorScores, got.SectorScores);
        CollectionAssert.AreEqual(rec.Modifiers,    got.Modifiers);
        Assert.AreEqual(rec.ReplayPath,        got.ReplayPath);
    }

    // ── PersonalBest ──────────────────────────────────────────────────────────

    [Test]
    public async Task PersonalBest_UpdatedOnSave()
    {
        await _repo.SaveAsync(MakeRecord("song1", "extra", 800_000));
        await _repo.SaveAsync(MakeRecord("song1", "extra", 950_000));
        await _repo.SaveAsync(MakeRecord("song1", "extra", 900_000));

        var best = await _repo.GetBestAsync("song1", "extra");
        Assert.AreEqual(950_000, best.BestEffectiveScore);
        Assert.AreEqual(3, best.TotalPlays);
    }

    [Test]
    public async Task PersonalBest_AchievementsAccumulate()
    {
        await _repo.SaveAsync(MakeRecord("song1", "extra", 800_000, isFullCombo: true));
        await _repo.SaveAsync(MakeRecord("song1", "extra", 950_000));  // 高スコアだが FC なし

        var best = await _repo.GetBestAsync("song1", "extra");
        Assert.AreEqual(950_000, best.BestEffectiveScore);
        Assert.IsTrue(best.HasFullCombo);  // 過去に達成済み → 維持
    }

    [Test]
    public async Task GetBest_ReturnsNull_WhenNoPlays()
    {
        var best = await _repo.GetBestAsync("nonexistent", "extra");
        Assert.IsNull(best);
    }

    // ── 履歴 ──────────────────────────────────────────────────────────────────

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
    public async Task GetAllHistory_PaginationWorks()
    {
        for (int i = 0; i < 10; i++)
            await _repo.SaveAsync(MakeRecord("song" + i, "extra", 800_000 + i * 10_000, playedAt: 1000 + i));

        var page1 = await _repo.GetAllHistoryAsync(limit: 3, offset: 0);
        var page2 = await _repo.GetAllHistoryAsync(limit: 3, offset: 3);

        Assert.AreEqual(3, page1.Count);
        Assert.AreEqual(3, page2.Count);
        Assert.AreNotEqual(page1[0].PlayId, page2[0].PlayId);
    }

    // ── 削除 ──────────────────────────────────────────────────────────────────

    [Test]
    public async Task DeleteAll_RemovesPlaysAndBests()
    {
        await _repo.SaveAsync(MakeRecord("song1", "extra", 950_000));
        await _repo.SaveAsync(MakeRecord("song2", "extra", 850_000));

        await _repo.DeleteAllAsync();

        Assert.AreEqual(0, await _repo.GetTotalPlaysAsync());
        Assert.IsNull(await _repo.GetBestAsync("song1", "extra"));
    }

    // ── 並行 ──────────────────────────────────────────────────────────────────

    [Test]
    public async Task ConcurrentSaves_AllPersist()
    {
        var tasks = new List<Task>();
        for (int i = 0; i < 20; i++)
        {
            int idx = i;
            tasks.Add(Task.Run(async () =>
                await _repo.SaveAsync(MakeRecord("song" + idx, "extra", 800_000 + idx))));
        }
        await Task.WhenAll(tasks);

        Assert.AreEqual(20, await _repo.GetTotalPlaysAsync());
    }

    // ── GetAllBests ──────────────────────────────────────────────────────────

    [Test]
    public async Task GetAllBests_ReturnsAllSongDifficultyPairs()
    {
        await _repo.SaveAsync(MakeRecord("song1", "extra", 950_000));
        await _repo.SaveAsync(MakeRecord("song2", "extra", 850_000));
        await _repo.SaveAsync(MakeRecord("song1", "hard",  900_000));

        var bests = await _repo.GetAllBestsAsync();
        Assert.AreEqual(3, bests.Count);
        Assert.IsTrue(bests.Exists(b => b.SongId == "song1" && b.Difficulty == "extra"));
        Assert.IsTrue(bests.Exists(b => b.SongId == "song2" && b.Difficulty == "extra"));
        Assert.IsTrue(bests.Exists(b => b.SongId == "song1" && b.Difficulty == "hard"));
    }

    [Test]
    public async Task GetAllBests_Empty_ReturnsEmptyList()
    {
        var bests = await _repo.GetAllBestsAsync();
        Assert.AreEqual(0, bests.Count);
    }

    // ── ヘルパー ──────────────────────────────────────────────────────────────

    static PlayRecord MakeRecord(string songId, string diff, int effectiveScore,
                                  bool isFullCombo = false, long playedAt = 0)
    {
        return new PlayRecord
        {
            PlayId            = Guid.NewGuid().ToString(),
            SongId            = songId,
            Difficulty        = diff,
            PlayedAtUnixMs    = playedAt > 0 ? playedAt : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            RawScore          = effectiveScore,
            EffectiveScore    = effectiveScore,
            Rank              = "S",
            PerfectPlusCount  = 100,
            MaxCombo          = 100,
            TotalNotes        = 100,
            SectorScores      = new[] { 0, 0, 0, 0, 0 },
            IsFullCombo       = isFullCombo,
            IsAllPerfect      = effectiveScore >= 1_000_000,
            Modifiers         = new string[0],
        };
    }
}
#endif
