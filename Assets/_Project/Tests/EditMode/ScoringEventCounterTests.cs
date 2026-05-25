using System.Collections.Generic;
using NUnit.Framework;

/// <summary><see cref="ScoringEventCounter"/> のユニットテスト。</summary>
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

    // ── Hold ticks (per-measure) ────────────────────────────────────────────────

    [Test]
    public void Hold120Bpm500ms_TotalIs2()
    {
        // 120 BPM → measure = 2000 ms; a 500 ms hold spans < 1 measure → 0 body ticks
        // total = head(1) + 0 ticks + tail(1) = 2
        var notes = new List<NoteData>
        {
            new NoteData { Id = 1, Type = NoteType.Hold, Lane = LaneRef.Lane0,
                           TimeMs = 1000, DurationMs = 500 }
        };
        Assert.AreEqual(2, ScoringEventCounter.Count(notes, Bpm120()));
    }

    [Test]
    public void Hold120Bpm5000ms_TotalIs6()
    {
        // 120 BPM → hold tick = 1000 ms; ticks at 2000,3000,4000,5000 (within [1000,6000)) = 4
        // total = head(1) + 4 ticks + tail(1) = 6
        var notes = new List<NoteData>
        {
            new NoteData { Id = 1, Type = NoteType.Hold, Lane = LaneRef.Lane0,
                           TimeMs = 1000, DurationMs = 5000 }
        };
        Assert.AreEqual(6, ScoringEventCounter.Count(notes, Bpm120()));
    }

    [Test]
    public void FxHold_CountsSameAsHold()
    {
        var hold = new List<NoteData>
        {
            new NoteData { Id = 1, Type = NoteType.Hold,   Lane = LaneRef.Lane0,
                           TimeMs = 1000, DurationMs = 5000 }
        };
        var fxHold = new List<NoteData>
        {
            new NoteData { Id = 1, Type = NoteType.FxHold, Lane = LaneRef.FxL,
                           TimeMs = 1000, DurationMs = 5000 }
        };
        Assert.AreEqual(
            ScoringEventCounter.Count(hold, Bpm120()),
            ScoringEventCounter.Count(fxHold, Bpm120()));
    }

    // ── Mixed ─────────────────────────────────────────────────────────────────

    [Test]
    public void MixedTapAndHold_SumIsCorrect()
    {
        // Tap(1) + Hold(head+0ticks+tail = 2) + Tap(1) = 4
        var notes = new List<NoteData>
        {
            new NoteData { Id = 1, Type = NoteType.Tap,  Lane = LaneRef.Lane0, TimeMs = 0 },
            new NoteData { Id = 2, Type = NoteType.Hold, Lane = LaneRef.Lane1,
                           TimeMs = 1000, DurationMs = 500 },
            new NoteData { Id = 3, Type = NoteType.Tap,  Lane = LaneRef.Lane2, TimeMs = 2000 },
        };
        Assert.AreEqual(4, ScoringEventCounter.Count(notes, Bpm120()));
    }

    // ── Matches HoldJudgmentTracker ────────────────────────────────────────────

    [Test]
    public void CountHoldTicks_MatchesHoldJudgmentTracker_120Bpm()
    {
        var note = new NoteData
        {
            Id = 1, Type = NoteType.Hold, Lane = LaneRef.Lane0,
            TimeMs = 1000, DurationMs = 5000
        };
        var bpm     = Bpm120();
        var tracker = new HoldJudgmentTracker(note, bpm);

        Assert.AreEqual(tracker.TickTimes.Count, ScoringEventCounter.CountHoldTicks(note, bpm));
    }

    [Test]
    public void CountHoldTicks_MatchesHoldJudgmentTracker_150Bpm()
    {
        // 150 BPM → measure = 1600 ms; verifies counter and tracker agree on a non-zero count.
        var note = new NoteData
        {
            Id = 1, Type = NoteType.Hold, Lane = LaneRef.Lane0,
            TimeMs = 1000, DurationMs = 5000
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
        int total = ScoringEventCounter.Count(notes, bpm);  // 1 tap + (head + 0 ticks + tail) = 3

        var calc = new ScoreCalculator(total);
        for (int i = 0; i < total; i++)
            calc.Add(Judgment.PerfectPlus);

        Assert.AreEqual(1_000_000, calc.CurrentScore);
    }
}
