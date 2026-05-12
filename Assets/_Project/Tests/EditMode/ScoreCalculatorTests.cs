using NUnit.Framework;

// All arithmetic verified analytically in comments.
// N=1000: _xMicro = 1_000_000_000  (1_000_000_000_000 / 1000)
// N=100:  _xMicro = 10_000_000_000 (1_000_000_000_000 / 100)

[TestFixture]
public class ScoreCalculatorTests
{
    // ── Perfect scores ────────────────────────────────────────────────────

    [Test]
    public void AllPerfectPlus_GivesExactly1000000()
    {
        // 1000 × _xMicro(1B) = 1_000_000_000_000 micro → display 1_000_000
        var calc = new ScoreCalculator(1000);
        for (int i = 0; i < 1000; i++) calc.Add(Judgment.PerfectPlus);
        Assert.AreEqual(1_000_000, calc.CurrentScore);
    }

    [Test]
    public void AllPerfect_GivesExactly1000000()
    {
        // Perfect adds the same full _xMicro as PerfectPlus
        var calc = new ScoreCalculator(1000);
        for (int i = 0; i < 1000; i++) calc.Add(Judgment.Perfect);
        Assert.AreEqual(1_000_000, calc.CurrentScore);
    }

    // ── All Miss → zero ───────────────────────────────────────────────────

    [Test]
    public void AllMiss_GivesZero()
    {
        var calc = new ScoreCalculator(1000);
        for (int i = 0; i < 1000; i++) calc.Add(Judgment.Miss);
        Assert.AreEqual(0, calc.CurrentScore);
    }

    // ── Penalty equivalences ──────────────────────────────────────────────

    /// <summary>
    /// penalty(Good) = _xMicro/4, so 4 × penalty(Good) = _xMicro = penalty(Miss).
    /// Verified: (N-4) PP + 4 Good == (N-1) PP + 1 Miss.
    ///
    /// N=100, _xMicro=10B:
    ///   A: 96×10B + 4×7.5B = 960B+30B = 990B → score 990_000
    ///   B: 99×10B + 0      = 990B        → score 990_000  ✓
    /// </summary>
    [Test]
    public void FourGoods_EqualsOneMissPenalty()
    {
        const int N = 100;
        var calcA = new ScoreCalculator(N);
        var calcB = new ScoreCalculator(N);

        for (int i = 0; i < N - 4; i++) calcA.Add(Judgment.PerfectPlus);
        for (int i = 0; i < 4; i++)     calcA.Add(Judgment.Good);

        for (int i = 0; i < N - 1; i++) calcB.Add(Judgment.PerfectPlus);
        calcB.Add(Judgment.Miss);

        Assert.AreEqual(calcB.CurrentScore, calcA.CurrentScore,
            "4 Good notes should cost the same as 1 Miss");
    }

    /// <summary>
    /// penalty(Great) = _xMicro/200, penalty(Good) = _xMicro/4 = 50×(_xMicro/200).
    /// So 50 Greats == 1 Good in terms of lost score.
    /// Verified: (N-50) PP + 50 Great == (N-1) PP + 1 Good.
    ///
    /// N=100, _xMicro=10B:
    ///   A: 50×10B + 50×9.95B = 500B+497.5B = 997.5B → score 997_500
    ///   B: 99×10B + 7.5B    = 990B+7.5B   = 997.5B → score 997_500  ✓
    /// </summary>
    [Test]
    public void FiftyGreats_EqualsOneGoodPenalty()
    {
        const int N = 100;
        var calcA = new ScoreCalculator(N);
        var calcB = new ScoreCalculator(N);

        for (int i = 0; i < N - 50; i++) calcA.Add(Judgment.PerfectPlus);
        for (int i = 0; i < 50; i++)     calcA.Add(Judgment.Great);

        for (int i = 0; i < N - 1; i++) calcB.Add(Judgment.PerfectPlus);
        calcB.Add(Judgment.Good);

        Assert.AreEqual(calcB.CurrentScore, calcA.CurrentScore,
            "50 Great notes should cost the same as 1 Good");
    }

    // ── Rank boundary values ──────────────────────────────────────────────

    [Test]
    public void ComputeRank_BoundaryValues()
    {
        // S+ (≥ 997_000)
        Assert.AreEqual("S+", ScoreCalculator.ComputeRank(1_000_000), "1000000 → S+");
        Assert.AreEqual("S+", ScoreCalculator.ComputeRank(997_000),   "997000  → S+ lower bound");
        Assert.AreEqual("S",  ScoreCalculator.ComputeRank(996_999),   "996999  → S  (just below S+)");

        // S (≥ 990_000)
        Assert.AreEqual("S",  ScoreCalculator.ComputeRank(990_000),   "990000  → S  lower bound");
        Assert.AreEqual("A+", ScoreCalculator.ComputeRank(989_999),   "989999  → A+ (just below S)");

        // A+ (≥ 950_000)
        Assert.AreEqual("A+", ScoreCalculator.ComputeRank(950_000),   "950000  → A+ lower bound");
        Assert.AreEqual("A",  ScoreCalculator.ComputeRank(949_999),   "949999  → A  (just below A+)");

        // A (≥ 900_000)
        Assert.AreEqual("A",  ScoreCalculator.ComputeRank(900_000),   "900000  → A  lower bound");
        Assert.AreEqual("B",  ScoreCalculator.ComputeRank(899_999),   "899999  → B  (just below A)");

        // B (≥ 800_000)
        Assert.AreEqual("B",  ScoreCalculator.ComputeRank(800_000),   "800000  → B  lower bound");
        Assert.AreEqual("C",  ScoreCalculator.ComputeRank(799_999),   "799999  → C  (just below B)");

        // C (≥ 700_000)
        Assert.AreEqual("C",  ScoreCalculator.ComputeRank(700_000),   "700000  → C  lower bound");
        Assert.AreEqual("D",  ScoreCalculator.ComputeRank(699_999),   "699999  → D  (just below C)");
        Assert.AreEqual("D",  ScoreCalculator.ComputeRank(0),          "0       → D");
    }
}
