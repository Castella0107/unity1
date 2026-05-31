using NUnit.Framework;

/// <summary><see cref="PlayProgressAggregator"/> のユニットテスト。</summary>
public class PlayProgressAggregatorTests
{
    static int[] FourSectors => new[] { 1000, 2000, 3000, 4000 };

    [Test]
    public void AllPerfectPlus_GivesExactly1000000()
    {
        var agg = new PlayProgressAggregator(100, FourSectors, Judgment.Good);
        for (int i = 0; i < 100; i++)
            agg.ApplyHit(Judgment.PerfectPlus, 0, i * 50);
        agg.FinalizeLastSector();
        Assert.AreEqual(1_000_000, agg.CurrentScore);
    }

    [Test]
    public void AllMiss_GivesZero()
    {
        var agg = new PlayProgressAggregator(100, FourSectors, Judgment.Good);
        for (int i = 0; i < 100; i++)
            agg.ApplyMiss(i * 50);
        Assert.AreEqual(0, agg.CurrentScore);
        Assert.AreEqual(100, agg.Counts[(int)Judgment.Miss]);
    }

    [Test]
    public void ComboResetsOnMissWithDefaultBorder()
    {
        var agg = new PlayProgressAggregator(10, FourSectors, Judgment.Good);
        for (int i = 0; i < 5; i++)
            agg.ApplyHit(Judgment.PerfectPlus, 0, i * 10);
        Assert.AreEqual(5, agg.CurrentCombo);
        agg.ApplyMiss(60);
        Assert.AreEqual(0, agg.CurrentCombo);
        Assert.AreEqual(5, agg.MaxCombo);
    }

    [Test]
    public void GoodBreaksComboWhenBorderIsGreat()
    {
        var agg = new PlayProgressAggregator(10, FourSectors, Judgment.Great);
        agg.ApplyHit(Judgment.PerfectPlus, 0, 0);
        agg.ApplyHit(Judgment.Good, 80, 10);
        Assert.AreEqual(0, agg.CurrentCombo);
    }

    [Test]
    public void FastLateCountedCorrectly()
    {
        var agg = new PlayProgressAggregator(10, FourSectors, Judgment.Good);
        agg.ApplyHit(Judgment.Perfect, -20, 0);   // Fast
        agg.ApplyHit(Judgment.Perfect, +20, 10);  // Late
        agg.ApplyHit(Judgment.PerfectPlus, 0, 20); // Just — no count
        Assert.AreEqual(1, agg.FastCount);
        Assert.AreEqual(1, agg.LateCount);
    }

    [Test]
    public void SectorScoresSumEqualsTotal()
    {
        var agg = new PlayProgressAggregator(10, FourSectors, Judgment.Good);
        // Notes at 0,50,100,...450 → spread across all 5 sectors
        for (int i = 0; i < 10; i++)
            agg.ApplyHit(Judgment.PerfectPlus, 0, i * 50);
        agg.FinalizeLastSector();

        int sum = 0;
        for (int i = 0; i < 5; i++) sum += agg.SectorScores[i];
        Assert.AreEqual(agg.CurrentScore, sum);
    }

    [Test]
    public void SnapshotCounts_MatchAggregatorCounts()
    {
        var agg = new PlayProgressAggregator(5, FourSectors, Judgment.Good);
        agg.ApplyHit(Judgment.PerfectPlus, 0, 0);
        agg.ApplyHit(Judgment.Great,       5, 10);
        agg.ApplyMiss(20);
        var snap = agg.Snapshot();
        Assert.AreEqual(1, snap.PerfectPlusCount);
        Assert.AreEqual(1, snap.GreatCount);
        Assert.AreEqual(1, snap.MissCount);
    }

    // ── Per-sector tie-break (Σ 2×PerfectPlus + Perfect、セクター毎; 同点解決用) ──

    [Test]
    public void SectorTieBreaks_AccumulatePerSector_TwoPerPlusOnePerPerfect()
    {
        var agg = new PlayProgressAggregator(4, FourSectors, Judgment.Good);
        agg.ApplyHit(Judgment.PerfectPlus, 0, 500);   // sector0 → +2
        agg.ApplyHit(Judgment.Perfect,     0, 600);   // sector0 → +1  (計 3)
        agg.ApplyHit(Judgment.PerfectPlus, 0, 1500);  // sector1 → +2
        agg.ApplyHit(Judgment.Great,       0, 1600);  // sector1 → +0
        agg.FinalizeLastSector();

        Assert.AreEqual(3, agg.SectorTieBreaks[0]);
        Assert.AreEqual(2, agg.SectorTieBreaks[1]);
        Assert.AreEqual(0, agg.SectorTieBreaks[2]);
    }

    [Test]
    public void SectorTieBreaks_PropagatedToSnapshot()
    {
        var agg = new PlayProgressAggregator(2, FourSectors, Judgment.Good);
        agg.ApplyHit(Judgment.PerfectPlus, 0, 500);   // sector0 → +2
        agg.ApplyHit(Judgment.Perfect,     0, 1500);  // sector1 → +1
        agg.FinalizeLastSector();

        var snap = agg.Snapshot();
        Assert.AreEqual(5, snap.SectorTieBreaks.Length);
        Assert.AreEqual(2, snap.SectorTieBreaks[0]);
        Assert.AreEqual(1, snap.SectorTieBreaks[1]);
        // 加算的: タイブレーク追跡は既存出力 (スコア/カウント) に影響しない
        Assert.AreEqual(agg.CurrentScore, snap.CurrentScore);
        Assert.AreEqual(2, snap.PerfectPlusCount + snap.PerfectCount);
    }
}
