using NUnit.Framework;

/// <summary>
/// リーダーボード提出条件 (docs/design_doc/leaderboard_client.md §3) のテスト。
/// </summary>
public class ScoreSubmitPolicyTests
{
    [Test]
    public void AllConditionsMet_Submits()
    {
        Assert.IsTrue(ScoreSubmitPolicy.ShouldSubmit(
            isPvp: false, isAutoPlay: false, loggedIn: true, hasChartId: true, hasReplayPath: true));
    }

    [Test]
    public void Pvp_DoesNotSubmit()
    {
        // PVP は /matches/submit 経由で別管理 — score/validate へは送らない
        Assert.IsFalse(ScoreSubmitPolicy.ShouldSubmit(true, false, true, true, true));
    }

    [Test]
    public void AutoPlay_DoesNotSubmit()
    {
        Assert.IsFalse(ScoreSubmitPolicy.ShouldSubmit(false, true, true, true, true));
    }

    [Test]
    public void NotLoggedIn_DoesNotSubmit()
    {
        Assert.IsFalse(ScoreSubmitPolicy.ShouldSubmit(false, false, false, true, true));
    }

    [Test]
    public void LocalOnlyChart_DoesNotSubmit()
    {
        // サーバーに存在しない譜面 (chart_id 不明) は提出しない
        Assert.IsFalse(ScoreSubmitPolicy.ShouldSubmit(false, false, true, false, true));
    }

    [Test]
    public void NoReplaySaved_DoesNotSubmit()
    {
        Assert.IsFalse(ScoreSubmitPolicy.ShouldSubmit(false, false, true, true, false));
    }
}
