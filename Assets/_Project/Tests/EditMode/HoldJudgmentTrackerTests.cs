using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary><see cref="HoldJudgmentTracker"/> のユニットテスト。</summary>
public class HoldJudgmentTrackerTests
{
    static BpmTimeline Bpm120 => new BpmTimeline(new[]
    {
        new TempoEvent { Type = "bpm", TimeMs = 0, Bpm = 120 }
    });

    static HoldJudgmentTracker MakeTracker(double startMs, double durationMs) =>
        new HoldJudgmentTracker(
            new NoteData { Id = 1, Type = NoteType.Hold, Lane = LaneRef.Lane0,
                           TimeMs = startMs, DurationMs = durationMs },
            Bpm120);

    [Test]
    public void HoldTickInterval_120Bpm_Is1000ms()
    {
        // 120 BPM → measure 2000 ms; 2 ticks per measure → tick interval 1000 ms (2 beats)
        Assert.AreEqual(1000.0, Bpm120.GetHoldTickIntervalMs(0), 0.01);
    }

    [Test]
    public void TickTimes_AreHoldTickSpaced()
    {
        // 5000 ms hold at 120 BPM (tick = 1000 ms) → ticks at 1000,2000,3000,4000 = 4 ticks
        var tracker = MakeTracker(0, 5000);
        Assert.AreEqual(4, tracker.TickTimes.Count);
        Assert.AreEqual(1000.0, tracker.TickTimes[0], 0.01);
        Assert.AreEqual(4000.0, tracker.TickTimes[3], 0.01);
    }

    [Test]
    public void TickTimes_EndOnBoundary_ExcludesEndTick_TailWins()
    {
        // 2000 ms hold at 120 BPM = exactly 2 tick intervals. The boundary tick at 2000
        // (= end) is dropped so the tail owns the end (no double combo). Only 1000 remains.
        var tracker = MakeTracker(0, 2000);
        Assert.AreEqual(1, tracker.TickTimes.Count);
        Assert.AreEqual(1000.0, tracker.TickTimes[0], 0.01);
    }

    [Test]
    public void OnHeadInput_WithinWindow_ReturnsJudgment()
    {
        var tracker = MakeTracker(1000, 500);
        var j = tracker.OnHeadInput(1000);
        Assert.AreEqual(Judgment.PerfectPlus, j);
        Assert.IsTrue(tracker.IsHeadJudged);
    }

    [Test]
    public void OnHeadInput_OutsideWindow_ReturnsNull()
    {
        var tracker = MakeTracker(1000, 500);
        var j = tracker.OnHeadInput(1000 + JudgmentWindow.GoodMs + 1);
        Assert.IsNull(j);
        Assert.IsFalse(tracker.IsHeadJudged);
    }

    [Test]
    public void AdvanceTo_AllHeld_ReturnsAllPerfectPlus()
    {
        var tracker = MakeTracker(0, 5000);   // ticks at 2000, 4000
        tracker.OnHeadInput(0);
        var ticks = tracker.AdvanceTo(5000).ToList();
        Assert.IsTrue(ticks.Count > 0);
        Assert.IsTrue(ticks.All(t => t.Judgment == Judgment.PerfectPlus));
    }

    [Test]
    public void HeadNotJudged_AdvanceTo_YieldsNothing()
    {
        var tracker = MakeTracker(0, 5000);
        // head NOT judged — AdvanceTo must yield nothing even though ticks exist
        var ticks = tracker.AdvanceTo(5000).ToList();
        Assert.AreEqual(0, ticks.Count);
    }

    [Test]
    public void GuardWindow_AllowsBriefRelease()
    {
        var tracker = MakeTracker(0, 2500);   // single tick at 2000
        tracker.OnHeadInput(0);
        tracker.OnReleased(1970);             // release 30 ms before the tick (within 50 ms guard)
        var ticks = tracker.AdvanceTo(2100).ToList();
        Assert.IsTrue(ticks.Count > 0);
        Assert.IsTrue(ticks.All(t => t.Judgment == Judgment.PerfectPlus));
        Assert.IsFalse(tracker.IsAbandoned);
    }

    [Test]
    public void GuardExceeded_RemainingTicksAllMiss()
    {
        var tracker = MakeTracker(0, 5000);   // ticks at 1000,2000,3000,4000
        tracker.OnHeadInput(0);
        tracker.OnReleased(500);              // released well before the first tick (1000)
        var ticks = tracker.AdvanceTo(4500).ToList();
        Assert.IsTrue(ticks.Any(t => t.Judgment == Judgment.Miss));
        Assert.IsTrue(tracker.IsAbandoned);
    }

    [Test]
    public void ResolveTail_HeldThrough_ReturnsPerfectPlus()
    {
        // No release at all — holding through to the end yields a Perfect+ tail.
        var tracker = MakeTracker(0, 5000);
        tracker.OnHeadInput(0);
        var j = tracker.ResolveTail(5000);
        Assert.AreEqual(Judgment.PerfectPlus, j);
        Assert.IsTrue(tracker.IsTailJudged);
    }

    [Test]
    public void ResolveTail_ReleasedNearEnd_ReturnsPerfectPlus()
    {
        // Releasing within the 50 ms guard of the end still counts as a held tail.
        var tracker = MakeTracker(0, 1000);   // no body ticks (< 1 measure)
        tracker.OnHeadInput(0);
        tracker.OnReleased(980);              // 20 ms before end
        var j = tracker.ResolveTail(1000);
        Assert.AreEqual(Judgment.PerfectPlus, j);
        Assert.IsTrue(tracker.IsTailJudged);
    }

    [Test]
    public void ResolveTail_ReleasedEarly_ReturnsMiss()
    {
        var tracker = MakeTracker(0, 1000);   // no body ticks (< 1 measure)
        tracker.OnHeadInput(0);
        tracker.OnReleased(100);              // released far too early
        var j = tracker.ResolveTail(1000);
        Assert.AreEqual(Judgment.Miss, j);
        Assert.IsTrue(tracker.IsCompleted);
    }

    [Test]
    public void ResolveTail_BeforeEnd_ReturnsNull()
    {
        var tracker = MakeTracker(0, 5000);
        tracker.OnHeadInput(0);
        Assert.IsNull(tracker.ResolveTail(4999));
        Assert.IsFalse(tracker.IsTailJudged);
    }

    [Test]
    public void HeadMissed_SetsAbandoned()
    {
        var tracker = MakeTracker(1000, 500);
        bool missed = tracker.OnHeadMissed(1000 + JudgmentWindow.GoodMs + 1);
        Assert.IsTrue(missed);
        Assert.IsTrue(tracker.IsAbandoned);
        Assert.IsTrue(tracker.IsHeadJudged);
    }
}
