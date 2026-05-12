using System.Collections.Generic;
using System.Linq;

// Test helper: generates synthetic ReplayInputEvent lists for judgment engine tests.
public static class ReplayBuilder
{
    /// Each note hit at exactly its time (delta = 0 → Perfect+).
    public static List<ReplayInputEvent> AllPerfectPlus(ChartData chart)
    {
        var abs = new List<(double timeMs, byte lane, byte action)>();
        foreach (var n in chart.Notes)
        {
            byte lane = (byte)n.Lane;
            switch (n.Type)
            {
                case NoteType.Tap:
                case NoteType.FxTap:
                    abs.Add((n.TimeMs,      lane, 0));  // Down
                    abs.Add((n.TimeMs + 30, lane, 1));  // Up (post-judgment)
                    break;
                case NoteType.Hold:
                case NoteType.FxHold:
                    abs.Add((n.TimeMs,                 lane, 0));  // Down at head
                    abs.Add((n.TimeMs + n.DurationMs,  lane, 1));  // Up at tail
                    break;
            }
        }
        return ToReplayEvents(abs);
    }

    /// No input — all notes auto-miss.
    public static List<ReplayInputEvent> AllMiss()
    {
        return new List<ReplayInputEvent>();
    }

    /// All tap notes hit with a fixed time offset (positive = late, negative = fast).
    /// Hold notes are skipped (complex tail timing with offset).
    public static List<ReplayInputEvent> AllWithOffset(ChartData chart, double offsetMs)
    {
        var abs = new List<(double timeMs, byte lane, byte action)>();
        foreach (var n in chart.Notes)
        {
            if (n.Type != NoteType.Tap && n.Type != NoteType.FxTap) continue;
            byte lane = (byte)n.Lane;
            abs.Add((n.TimeMs + offsetMs,      lane, 0));
            abs.Add((n.TimeMs + offsetMs + 30, lane, 1));
        }
        return ToReplayEvents(abs);
    }

    // Convert absolute (timeMs, lane, action) list → delta-encoded ReplayInputEvents
    static List<ReplayInputEvent> ToReplayEvents(
        List<(double timeMs, byte lane, byte action)> abs)
    {
        var sorted = abs.OrderBy(e => e.timeMs).ToList();
        var result = new List<ReplayInputEvent>(sorted.Count);
        double last = 0;
        foreach (var item in sorted)
        {
            int delta = (int)System.Math.Round(item.timeMs - last);
            result.Add(new ReplayInputEvent
            {
                DeltaMsFromPrev = delta,
                Lane            = item.lane,
                Action          = item.action,
            });
            last = item.timeMs;
        }
        return result;
    }
}
