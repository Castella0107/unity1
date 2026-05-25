using NUnit.Framework;

/// <summary><see cref="PlayRecordFactory"/> のユニットテスト。</summary>
public class PlayRecordFactoryTests
{
    [Test]
    public void Create_AllPerfectPlus_SetsAllPerfectPlusFlag()
    {
        var snap = MakeSnapshot(1_000_000, new[] { 100, 0, 0, 0, 0 },
                                new[] { 200000, 200000, 200000, 200000, 200000 });

        var rec = PlayRecordFactory.Create(snap, "song", "extra", "hash", 100);

        Assert.IsTrue(rec.IsAllPerfectPlus);
        Assert.IsTrue(rec.IsAllPerfect);
        Assert.IsTrue(rec.IsFullCombo);
        Assert.AreEqual("S+", rec.Rank);
    }

    [Test]
    public void Create_AllPerfectMixed_SetsAllPerfectButNotPlus()
    {
        var snap = MakeSnapshot(1_000_000, new[] { 50, 50, 0, 0, 0 }, null);

        var rec = PlayRecordFactory.Create(snap, "song", "extra", "hash", 100);

        Assert.IsFalse(rec.IsAllPerfectPlus);
        Assert.IsTrue(rec.IsAllPerfect);
        Assert.IsTrue(rec.IsFullCombo);
    }

    [Test]
    public void Create_OneMiss_BreaksFullCombo()
    {
        var snap = MakeSnapshot(990_000, new[] { 99, 0, 0, 0, 1 }, null);

        var rec = PlayRecordFactory.Create(snap, "song", "extra", "hash", 100);

        Assert.IsFalse(rec.IsFullCombo);
        Assert.IsFalse(rec.IsAllPerfect);
    }

    [Test]
    public void DifficultyMultiplier_Applied()
    {
        Assert.AreEqual(750_000,   PlayRecordFactory.ApplyDifficultyMultiplier(1_000_000, "easy"));
        Assert.AreEqual(800_000,   PlayRecordFactory.ApplyDifficultyMultiplier(1_000_000, "normal"));
        Assert.AreEqual(900_000,   PlayRecordFactory.ApplyDifficultyMultiplier(1_000_000, "hard"));
        Assert.AreEqual(1_000_000, PlayRecordFactory.ApplyDifficultyMultiplier(1_000_000, "extra"));
    }

    [Test]
    public void Create_SectorScoresCopied()
    {
        var sectors = new[] { 100_000, 150_000, 200_000, 250_000, 300_000 };
        var snap = MakeSnapshot(1_000_000, new[] { 100, 0, 0, 0, 0 }, sectors);

        var rec = PlayRecordFactory.Create(snap, "song", "extra", "hash", 100);

        Assert.AreEqual(5, rec.SectorScores.Length);
        for (int i = 0; i < 5; i++)
            Assert.AreEqual(sectors[i], rec.SectorScores[i]);
    }

    [Test]
    public void Create_PlayId_IsNotEmpty()
    {
        var snap = MakeSnapshot(500_000, new[] { 0, 0, 0, 50, 50 }, null);
        var rec  = PlayRecordFactory.Create(snap, "song", "extra", "hash", 100);
        Assert.IsFalse(string.IsNullOrEmpty(rec.PlayId));
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    static PlayProgressSnapshot MakeSnapshot(int score, int[] counts, int[] sectorScores)
    {
        var sectors = sectorScores
            ?? new[] { score / 5, score / 5, score / 5, score / 5, score / 5 };
        return new PlayProgressSnapshot(score, 0, 0, 0, 0, counts, sectors, 5);
    }
}
