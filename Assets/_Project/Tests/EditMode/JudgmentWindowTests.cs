using NUnit.Framework;

/// <summary><see cref="JudgmentWindow"/> のユニットテスト。</summary>
[TestFixture]
public class JudgmentWindowTests
{
    // ── Boundary: PerfectPlus ±16 ms ──────────────────────────────────────

    [Test]
    public void FromDeltaMs_AtZero_ReturnsPerfectPlus()
        => Assert.AreEqual(Judgment.PerfectPlus, JudgmentWindow.FromDeltaMs(0.0));

    [Test]
    public void FromDeltaMs_At16ms_ReturnsPerfectPlus()
        => Assert.AreEqual(Judgment.PerfectPlus, JudgmentWindow.FromDeltaMs(16.0));

    // ── Boundary: Perfect 17–33 ms ────────────────────────────────────────

    [Test]
    public void FromDeltaMs_At17ms_ReturnsPerfect()
        => Assert.AreEqual(Judgment.Perfect, JudgmentWindow.FromDeltaMs(17.0));

    [Test]
    public void FromDeltaMs_AtNegative33ms_ReturnsPerfect()
        => Assert.AreEqual(Judgment.Perfect, JudgmentWindow.FromDeltaMs(-33.0));

    // ── Boundary: Great 34–50 ms ──────────────────────────────────────────

    [Test]
    public void FromDeltaMs_At50ms_ReturnsGreat()
        => Assert.AreEqual(Judgment.Great, JudgmentWindow.FromDeltaMs(50.0));

    // ── Boundary: Good 51–83 ms ───────────────────────────────────────────

    [Test]
    public void FromDeltaMs_At51ms_ReturnsGood()
        => Assert.AreEqual(Judgment.Good, JudgmentWindow.FromDeltaMs(51.0));

    // ── Miss > 83 ms ─────────────────────────────────────────────────────

    [Test]
    public void FromDeltaMs_At84ms_ReturnsMiss()
        => Assert.AreEqual(Judgment.Miss, JudgmentWindow.FromDeltaMs(84.0));
}
