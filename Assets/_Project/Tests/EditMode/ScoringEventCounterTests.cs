using System.Collections.Generic;
using NUnit.Framework;

public class ScoringEventCounterTests
{
    static BpmTimeline Bpm120() => new BpmTimeline(new List<TempoEvent>
        { new TempoEvent { Type = "bpm", TimeMs = 0, Bpm = 120 } });

    static BpmTimeline Bpm150() => new BpmTimeline(new List<TempoEvent>
        { new TempoEvent { Type = "bpm", TimeMs = 0, Bpm = 150 } });

    // ── Null / empty ───────────────────────────────────────────────────────────

    [Test]
    public void NullNotes_ReturnsZero()
    {
        Assert.AreEqual(0, ScoringEventCounter.Count(null, Bpm120()));
    }

    [Test]
    public void EmptyNotes_ReturnsZero()
    {
        Assert.AreEqual(0, ScoringEventCounter.Count(new List<NoteData>(), Bpm120()));
    }

    // ── Tap / FxTap ───────────────────────────────────────────────────────────

    [Test]
    public void TapOnly_CountEqualsNoteCount()
    {
        var notes = new List<NoteData>
        {
            new NoteData { Id = 1, Type = NoteType.Tap,   Lane = LaneRef.Lane0, TimeMs = 1000 },
            new NoteData { Id = 2, Type = NoteType.Tap,   Lane = LaneRef.Lane1, TimeMs = 1500 },
            new NoteData { Id = 3, Type = NoteType.FxTap, Lane = LaneRef.FxL,   TimeMs = 2000 },
        };
        Assert.AreEqual(3, ScoringEventCounter.Count(notes, Bpm120()));
    }

    // ── Hold ticks ───────────────────────────────────────────────────────────

    [Test]
    public void Hold120Bpm500ms_TotalIs17()
    {
        // 120 BPM → tick = 31.25ms; ticks in [1000, 1500): 15 ticks
        // total = head(1) + 15 ticks + tail(1) = 17
        var notes = new List<NoteData>
        {
            new NoteData { Id = 1, Type = NoteType.Hold, Lane = LaneRef.Lane0,
                           TimeMs = 1000, DurationMs = 500 }
        };
        Assert.AreEqual(17, ScoringEventCounter.Count(notes, Bpm120()));
    }

    [Test]
    public void FxHold_CountsSameAsHold()
    {
        var hold = new List<NoteData>
        {
            new NoteData { Id = 1, Type = NoteType.Hold,   Lane = LaneRef.Lane0,
                           TimeMs = 1000, DurationMs = 500 }
        };
        var fxHold = new List<NoteData>
        {
            new NoteData { Id = 1, Type = NoteType.FxHold, Lane = LaneRef.FxL,
                           TimeMs = 1000, DurationMs = 500 }
        };
        Assert.AreEqual(
            ScoringEventCounter.Count(hold, Bpm120()),
            ScoringEventCounter.Count(fxHold, Bpm120()));
    }

    // ── Mixed ─────────────────────────────────────────────────────────────────

    [Test]
    public void MixedTapAndHold_SumIsCorrect()
    {
        // Tap(1) + Hold(17) + Tap(1) = 19
        var notes = new List<NoteData>
        {
            new NoteData { Id = 1, Type = NoteType.Tap,  Lane = LaneRef.Lane0, TimeMs = 0 },
            new NoteData { Id = 2, Type = NoteType.Hold, Lane = LaneRef.Lane1,
                           TimeMs = 1000, DurationMs = 500 },
            new NoteData { Id = 3, Type = NoteType.Tap,  Lane = LaneRef.Lane2, TimeMs = 2000 },
        };
        Assert.AreEqual(19, ScoringEventCounter.Count(notes, Bpm120()));
    }

    // ── Matches HoldJudgmentTracker ────────────────────────────────────────────

    [Test]
    public void CountHoldTicks_MatchesHoldJudgmentTracker_120Bpm()
    {
        var note = new NoteData
        {
            Id = 1, Type = NoteType.Hold, Lane = LaneRef.Lane0,
            TimeMs = 1000, DurationMs = 500
        };
        var bpm     = Bpm120();
        var tracker = new HoldJudgmentTracker(note, bpm);

        Assert.AreEqual(tracker.TickTimes.Count, ScoringEventCounter.CountHoldTicks(note, bpm));
    }

    [Test]
    public void CountHoldTicks_MatchesHoldJudgmentTracker_150Bpm()
    {
        // 150 BPM → tick = 25ms; 1000ms hold → 39 ticks
        var note = new NoteData
        {
            Id = 1, Type = NoteType.Hold, Lane = LaneRef.Lane0,
            TimeMs = 1000, DurationMs = 1000
        };
        var bpm     = Bpm150();
        var tracker = new HoldJudgmentTracker(note, bpm);

        Assert.AreEqual(tracker.TickTimes.Count, ScoringEventCounter.CountHoldTicks(note, bpm));
    }

    // ── All-perfect yields 1,000,000 ──────────────────────────────────────────

    [Test]
    public void TotalNotes_EnablesExactMillionScore()
    {
        // Verify that using ScoringEventCounter.Count as TotalNotes gives
        // exactly 1,000,000 for all-perfect on a mixed chart.
        var notes = new List<NoteData>
        {
            new NoteData { Id = 1, Type = NoteType.Tap,  Lane = LaneRef.Lane0, TimeMs = 1000 },
            new NoteData { Id = 2, Type = NoteType.Hold, Lane = LaneRef.Lane1,
                           TimeMs = 2000, DurationMs = 500 },
        };
        var bpm   = Bpm120();
        int total = ScoringEventCounter.Count(notes, bpm);  // 1 + 17 = 18

        var calc = new ScoreCalculator(total);
        // Apply all scoring events as PerfectPlus
        // 1 tap + (1 head + 15 ticks + 1 tail) = 18
        for (int i = 0; i < total; i++)
            calc.Add(Judgment.PerfectPlus);

        Assert.AreEqual(1_000_000, calc.CurrentScore);
    }
}
