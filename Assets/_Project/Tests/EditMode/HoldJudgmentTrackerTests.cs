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
    public void HoldTickInterval_120Bpm_Is250ms()
    {
        // 120 BPM → measure 2000 ms; 8 ticks per measure → tick interval 250 ms (1 eighth)
        Assert.AreEqual(250.0, Bpm120.GetHoldTickIntervalMs(0), 0.01);
    }

    [Test]
    public void TickTimes_AreHoldTickSpaced()
    {
        // 5000 ms hold at 120 BPM (tick = 250 ms) → ticks at 250,500,...,4750 = 19 ticks
        var tracker = MakeTracker(0, 5000);
        Assert.AreEqual(19, tracker.TickTimes.Count);
        Assert.AreEqual(250.0,  tracker.TickTimes[0],  0.01);
        Assert.AreEqual(4750.0, tracker.TickTimes[18], 0.01);
    }

    [Test]
    public void TickTimes_EndOnBoundary_ExcludesEndTick_TailWins()
    {
        // 2000 ms hold at 120 BPM = exactly 8 tick intervals. The boundary tick at 2000
        // (= end) is dropped so the tail owns the end (no double combo). 250..1750 remain.
        var tracker = MakeTracker(0, 2000);
        Assert.AreEqual(7, tracker.TickTimes.Count);
        Assert.AreEqual(250.0,  tracker.TickTimes[0], 0.01);
        Assert.AreEqual(1750.0, tracker.TickTimes[6], 0.01);
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
        var tracker = MakeTracker(0, 5000);   // ticks at 250,500,...,4750
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
        var tracker = MakeTracker(0, 2500);   // ticks every 250 ms (250,500,...,2250)
        tracker.OnHeadInput(0);
        tracker.OnReleased(1970);             // release 30 ms before the 2000 tick (within 50 ms guard)
        var ticks = tracker.AdvanceTo(2100).ToList();
        Assert.IsTrue(ticks.Count > 0);
        Assert.IsTrue(ticks.All(t => t.Judgment == Judgment.PerfectPlus));
        Assert.IsFalse(tracker.IsAbandoned);
    }

    [Test]
    public void GuardExceeded_TicksMiss_ButNotAbandoned()
    {
        // Released and never re-pressed: ticks during the drop are Miss, but the hold is
        // NOT abandoned (it stays recoverable). Only re-pressing would resume it.
        var tracker = MakeTracker(0, 5000);   // ticks at 250,500,...,4750
        tracker.OnHeadInput(0);
        tracker.OnReleased(520);              // released just after the 500 tick, before 750
        var ticks = tracker.AdvanceTo(4500).ToList();
        Assert.IsTrue(ticks.Any(t => t.Judgment == Judgment.Miss));
        Assert.IsFalse(tracker.IsAbandoned);
    }

    [Test]
    public void Recovery_RepressAfterDrop_FirstTickGreatThenPerfectPlus()
    {
        // Drop the hold past the guard, then re-press: the first tick after recovery is
        // Great, and the tick after that returns to PerfectPlus.
        var tracker = MakeTracker(0, 5000);   // ticks at 250,500,750,1000,...
        tracker.OnHeadInput(0);

        Assert.AreEqual(Judgment.PerfectPlus, tracker.AdvanceTo(300).Single().Judgment);   // 250: held

        tracker.OnReleased(300);
        Assert.AreEqual(Judgment.Miss, tracker.AdvanceTo(600).Single().Judgment);          // 500: dropped

        tracker.OnPressed(620);               // re-press 320 ms after release → recovery
        Assert.AreEqual(Judgment.Great,       tracker.AdvanceTo(800).Single().Judgment);   // 750: first after recovery
        Assert.AreEqual(Judgment.PerfectPlus, tracker.AdvanceTo(1050).Single().Judgment);  // 1000: back to P+
        Assert.IsFalse(tracker.IsAbandoned);
    }

    [Test]
    public void Recovery_RepressBeforeTail_TailIsGreat()
    {
        // Re-press near the end with no body tick in between → the tail is the first
        // judgment after recovery, so it resolves as Great.
        var tracker = MakeTracker(0, 1000);   // ticks 250,500,750 (body ticks not advanced here)
        tracker.OnHeadInput(0);
        tracker.OnReleased(200);              // dropped past guard
        tracker.OnPressed(900);               // re-press 700 ms later → recovery
        var j = tracker.ResolveTail(1000);
        Assert.AreEqual(Judgment.Great, j);
        Assert.IsTrue(tracker.IsTailJudged);
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
        var tracker = MakeTracker(0, 1000);   // tail-only check (body ticks not advanced)
        tracker.OnHeadInput(0);
        tracker.OnReleased(980);              // 20 ms before end
        var j = tracker.ResolveTail(1000);
        Assert.AreEqual(Judgment.PerfectPlus, j);
        Assert.IsTrue(tracker.IsTailJudged);
    }

    [Test]
    public void ResolveTail_ReleasedEarly_ReturnsMiss()
    {
        var tracker = MakeTracker(0, 1000);   // tail-only check (body ticks not advanced)
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
