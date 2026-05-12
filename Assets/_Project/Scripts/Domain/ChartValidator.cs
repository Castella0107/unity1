using System;
using System.Collections.Generic;

// Unity-independent. Part of Domain assembly.

public class ValidationIssue
{
    public enum SeverityLevel { Critical, Warning, Info }
    public SeverityLevel Severity;
    public string        Message;
}

public static class ChartValidator
{
    public static List<ValidationIssue> Validate(ChartData chart, SongMetadata song)
    {
        if (chart == null) throw new ArgumentNullException("chart");
        if (song  == null) throw new ArgumentNullException("song");

        var issues = new List<ValidationIssue>();
        var notes  = chart.Notes ?? new List<NoteData>();

        // ── CRITICAL: duplicate note (same lane + same time) ──────────────
        var seen = new HashSet<string>();
        foreach (var note in notes)
        {
            string key = note.Lane.ToString() + "@" + note.TimeMs.ToString("F3");
            if (!seen.Add(key))
                issues.Add(Crit($"Duplicate note: lane={note.Lane} timeMs={note.TimeMs}"));
        }

        foreach (var note in notes)
        {
            // CRITICAL: hold duration ≤ 0
            if ((note.Type == NoteType.Hold || note.Type == NoteType.FxHold)
                && note.DurationMs <= 0)
                issues.Add(Crit($"Note {note.Id}: hold DurationMs <= 0 ({note.DurationMs})"));

            // CRITICAL: note exceeds song duration
            if (note.TimeMs > song.DurationMs)
                issues.Add(Crit($"Note {note.Id} at {note.TimeMs}ms exceeds song duration {song.DurationMs}ms"));

            // WARNING: very short hold (< 50 ms, but > 0 so not Critical)
            if ((note.Type == NoteType.Hold || note.Type == NoteType.FxHold)
                && note.DurationMs > 0 && note.DurationMs < 50)
                issues.Add(Warn($"Note {note.Id}: hold DurationMs < 50ms ({note.DurationMs})"));
        }

        // ── Sector checks ──────────────────────────────────────────────────
        var sectors = song.Sectors;
        if (sectors != null && sectors.Count > 0)
        {
            int n = sectors.Count;
            var counts = new int[n];

            foreach (var note in notes)
            {
                int prevEnd = 0;
                for (int i = 0; i < n; i++)
                {
                    if (note.TimeMs > prevEnd && note.TimeMs <= sectors[i].EndMs)
                    { counts[i]++; break; }
                    prevEnd = sectors[i].EndMs;
                }
            }

            // WARNING: sector with 0 notes
            for (int i = 0; i < n; i++)
                if (counts[i] == 0)
                    issues.Add(Warn($"Sector '{sectors[i].Name}' has 0 notes"));

            // WARNING: max:min note ratio > 3.0 (only among non-zero sectors)
            int max = 0, min = int.MaxValue;
            bool any = false;
            for (int i = 0; i < n; i++)
                if (counts[i] > 0)
                { any = true; if (counts[i] > max) max = counts[i]; if (counts[i] < min) min = counts[i]; }
            if (any && min > 0 && (double)max / min > 3.0)
                issues.Add(Warn($"Sector distribution uneven: max={max} min={min} ratio={(double)max/min:F2}"));
        }

        return issues;
    }

    private static ValidationIssue Crit(string msg)
        => new ValidationIssue { Severity = ValidationIssue.SeverityLevel.Critical, Message = msg };
    private static ValidationIssue Warn(string msg)
        => new ValidationIssue { Severity = ValidationIssue.SeverityLevel.Warning,  Message = msg };
}
