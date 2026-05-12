using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

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
    public void TickInterval_120Bpm_Is31_25ms()
    {
        // 120 BPM → 1 beat = 500 ms → 1/16 beat = 31.25 ms
        Assert.AreEqual(31.25, Bpm120.GetTickIntervalMs(0), 0.01);
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
        var tracker = MakeTracker(0, 200);
        tracker.OnHeadInput(0);
        var ticks = tracker.AdvanceTo(250).ToList();
        Assert.IsTrue(ticks.Count > 0);
        Assert.IsTrue(ticks.All(t => t.Judgment == Judgment.PerfectPlus));
    }

    [Test]
    public void HeadNotJudged_AdvanceTo_YieldsNothing()
    {
        var tracker = MakeTracker(0, 200);
        // head NOT judged — AdvanceTo must yield nothing
        var ticks = tracker.AdvanceTo(250).ToList();
        Assert.AreEqual(0, ticks.Count);
    }

    [Test]
    public void GuardWindow_AllowsBriefRelease()
    {
        var tracker = MakeTracker(0, 200);
        tracker.OnHeadInput(0);
        tracker.OnReleased(50);   // release at 50 ms
        tracker.OnPressed(80);    // re-press 30 ms later (within 50 ms guard)
        var ticks = tracker.AdvanceTo(150).ToList();
        Assert.IsTrue(ticks.All(t => t.Judgment == Judgment.PerfectPlus));
    }

    [Test]
    public void GuardExceeded_RemainingTicksAllMiss()
    {
        var tracker = MakeTracker(0, 500);
        tracker.OnHeadInput(0);
        tracker.OnReleased(50);
        // No re-press; advance well past the guard window
        var ticks = tracker.AdvanceTo(400).ToList();
        Assert.IsTrue(ticks.Any(t => t.Judgment == Judgment.Miss));
        Assert.IsTrue(tracker.IsAbandoned);
    }

    [Test]
    public void OnTailInput_WithinWindow_ReturnsJudgment()
    {
        var tracker = MakeTracker(0, 500);
        tracker.OnHeadInput(0);
        var j = tracker.OnTailInput(500);
        Assert.AreEqual(Judgment.PerfectPlus, j);
        Assert.IsTrue(tracker.IsTailJudged);
    }

    [Test]
    public void OnTailMissed_PastWindow_SetsCompleted()
    {
        var tracker = MakeTracker(0, 500);
        tracker.OnHeadInput(0);
        bool missed = tracker.OnTailMissed(500 + JudgmentWindow.GoodMs + 1);
        Assert.IsTrue(missed);
        Assert.IsTrue(tracker.IsCompleted);
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
