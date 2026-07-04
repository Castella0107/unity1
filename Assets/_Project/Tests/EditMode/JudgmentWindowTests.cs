using NUnit.Framework;

/// <summary><see cref="JudgmentWindow"/> のユニットテスト。
/// 標準 (P+±16 / P±33 / GREAT±66.67 / GOOD±100) とワイド (P+±25 / P±50 / GREAT±75 / GOOD±100) の両プロファイルを検証する。</summary>
[TestFixture]
public class JudgmentWindowTests
{
    [SetUp]
    public void SetUp() => JudgmentWindow.WideActive = false;

    [TearDown]
    public void TearDown() => JudgmentWindow.WideActive = false;

    // ── Standard: PerfectPlus ±16 ms ──────────────────────────────────────

    [Test]
    public void FromDeltaMs_AtZero_ReturnsPerfectPlus()
        => Assert.AreEqual(Judgment.PerfectPlus, JudgmentWindow.FromDeltaMs(0.0));

    [Test]
    public void FromDeltaMs_At16ms_ReturnsPerfectPlus()
        => Assert.AreEqual(Judgment.PerfectPlus, JudgmentWindow.FromDeltaMs(16.0));

    // ── Standard: Perfect 17–33 ms ────────────────────────────────────────

    [Test]
    public void FromDeltaMs_At17ms_ReturnsPerfect()
        => Assert.AreEqual(Judgment.Perfect, JudgmentWindow.FromDeltaMs(17.0));

    [Test]
    public void FromDeltaMs_AtNegative33ms_ReturnsPerfect()
        => Assert.AreEqual(Judgment.Perfect, JudgmentWindow.FromDeltaMs(-33.0));

    // ── Standard: Great 34–66.67 ms (4 frames) ────────────────────────────

    [Test]
    public void FromDeltaMs_At34ms_ReturnsGreat()
        => Assert.AreEqual(Judgment.Great, JudgmentWindow.FromDeltaMs(34.0));

    [Test]
    public void FromDeltaMs_At66ms_ReturnsGreat()
        => Assert.AreEqual(Judgment.Great, JudgmentWindow.FromDeltaMs(66.0));

    // ── Standard: Good 66.67–100 ms (6 frames) ────────────────────────────

    [Test]
    public void FromDeltaMs_At67ms_ReturnsGood()
        => Assert.AreEqual(Judgment.Good, JudgmentWindow.FromDeltaMs(67.0));

    [Test]
    public void FromDeltaMs_At100ms_ReturnsGood()
        => Assert.AreEqual(Judgment.Good, JudgmentWindow.FromDeltaMs(100.0));

    // ── Standard: Miss > 100 ms ───────────────────────────────────────────

    [Test]
    public void FromDeltaMs_At101ms_ReturnsMiss()
        => Assert.AreEqual(Judgment.Miss, JudgmentWindow.FromDeltaMs(101.0));

    // ── Wide profile: P+±25 / P±50 / GREAT±75 / GOOD±100 ─────────────────

    [Test]
    public void Wide_At25ms_ReturnsPerfectPlus()
    {
        JudgmentWindow.WideActive = true;
        Assert.AreEqual(Judgment.PerfectPlus, JudgmentWindow.FromDeltaMs(25.0));
    }

    [Test]
    public void Wide_At26ms_ReturnsPerfect()
    {
        JudgmentWindow.WideActive = true;
        Assert.AreEqual(Judgment.Perfect, JudgmentWindow.FromDeltaMs(26.0));
    }

    [Test]
    public void Wide_AtNegative50ms_ReturnsPerfect()
    {
        JudgmentWindow.WideActive = true;
        Assert.AreEqual(Judgment.Perfect, JudgmentWindow.FromDeltaMs(-50.0));
    }

    [Test]
    public void Wide_At51ms_ReturnsGreat()
    {
        JudgmentWindow.WideActive = true;
        Assert.AreEqual(Judgment.Great, JudgmentWindow.FromDeltaMs(51.0));
    }

    [Test]
    public void Wide_At75ms_ReturnsGreat()
    {
        JudgmentWindow.WideActive = true;
        Assert.AreEqual(Judgment.Great, JudgmentWindow.FromDeltaMs(75.0));
    }

    [Test]
    public void Wide_At76ms_ReturnsGood()
    {
        JudgmentWindow.WideActive = true;
        Assert.AreEqual(Judgment.Good, JudgmentWindow.FromDeltaMs(76.0));
    }

    [Test]
    public void Wide_At100ms_ReturnsGood()
    {
        JudgmentWindow.WideActive = true;
        Assert.AreEqual(Judgment.Good, JudgmentWindow.FromDeltaMs(100.0));
    }

    [Test]
    public void Wide_At101ms_ReturnsMiss()
    {
        JudgmentWindow.WideActive = true;
        Assert.AreEqual(Judgment.Miss, JudgmentWindow.FromDeltaMs(101.0));
    }

    // ── Good/Miss 境界は両プロファイルで同一 (コンボ/ミス数がプロファイル非依存であることの根拠) ──

    [Test]
    public void GoodBoundary_IsIdenticalAcrossProfiles()
        => Assert.AreEqual(100.0, JudgmentWindow.GoodMs, 1e-9);
}
