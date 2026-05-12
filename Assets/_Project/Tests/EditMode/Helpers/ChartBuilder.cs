using System.Collections.Generic;

// Test helper: builds small ChartData objects for judgment engine tests.
// TotalNotes is computed as the total number of scoring events
// (tap = 1, hold = head + ticks + tail) so that all-perfect always gives 1,000,000.
public class ChartBuilder
{
    int _nextId = 1;
    readonly List<NoteData>   _notes  = new List<NoteData>();
    readonly List<TempoEvent> _tempos = new List<TempoEvent>();

    public ChartBuilder WithBpm(double bpm, double timeMs = 0)
    {
        _tempos.Add(new TempoEvent { Type = "bpm", TimeMs = timeMs, Bpm = bpm });
        return this;
    }

    public ChartBuilder AddTap(LaneRef lane, double timeMs)
    {
        _notes.Add(new NoteData
        {
            Id         = _nextId++,
            Type       = (lane == LaneRef.FxL || lane == LaneRef.FxR)
                         ? NoteType.FxTap : NoteType.Tap,
            Lane       = lane,
            TimeMs     = timeMs,
            DurationMs = 0,
        });
        return this;
    }

    public ChartBuilder AddHold(LaneRef lane, double startMs, double durationMs)
    {
        _notes.Add(new NoteData
        {
            Id         = _nextId++,
            Type       = (lane == LaneRef.FxL || lane == LaneRef.FxR)
                         ? NoteType.FxHold : NoteType.Hold,
            Lane       = lane,
            TimeMs     = startMs,
            DurationMs = durationMs,
        });
        return this;
    }

    public ChartData Build()
    {
        if (_tempos.Count == 0) WithBpm(120);
        return new ChartData
        {
            Notes      = _notes,
            Events     = _tempos,
            TotalNotes = ComputeTotalScoringEvents(),
            ChartHash  = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            Level      = 18,
        };
    }

    int ComputeTotalScoringEvents()
    {
        var bpmTl = new BpmTimeline(_tempos);
        int total = ScoringEventCounter.Count(_notes, bpmTl);
        return total > 0 ? total : 1;
    }
}
