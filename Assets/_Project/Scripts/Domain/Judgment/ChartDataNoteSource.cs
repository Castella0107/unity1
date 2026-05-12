using System;
using System.Collections.Generic;

// Unity-independent. No UnityEngine references allowed in this assembly.
// INoteSource backed by ChartData. Lane-bucketed dictionaries give O(n/6) average
// lookup instead of a full O(n) scan; safe for charts up to ~100k notes.
public sealed class ChartDataNoteSource : INoteSource
{
    readonly IReadOnlyList<NoteData>              _allNotes;
    readonly Dictionary<LaneRef, List<NoteData>> _tapsByLane  = new Dictionary<LaneRef, List<NoteData>>();
    readonly Dictionary<LaneRef, List<NoteData>> _holdsByLane = new Dictionary<LaneRef, List<NoteData>>();
    readonly HashSet<int>                         _hitTaps     = new HashSet<int>();
    readonly HashSet<int>                         _judgedHeads = new HashSet<int>();
    readonly HashSet<int>                         _judgedTails = new HashSet<int>();

    public IReadOnlyList<NoteData> AllNotes => _allNotes;

    public ChartDataNoteSource(ChartData chart)
    {
        _allNotes = chart.Notes;

        foreach (LaneRef lane in Enum.GetValues(typeof(LaneRef)))
        {
            _tapsByLane[lane]  = new List<NoteData>();
            _holdsByLane[lane] = new List<NoteData>();
        }

        foreach (var n in chart.Notes)
        {
            if (n.Type == NoteType.Tap || n.Type == NoteType.FxTap)
                _tapsByLane[n.Lane].Add(n);
            else if (n.Type == NoteType.Hold || n.Type == NoteType.FxHold)
                _holdsByLane[n.Lane].Add(n);
        }
    }

    public NoteData FindNearestUnhitTap(LaneRef lane, double timeMs, double maxDeltaMs)
    {
        if (!_tapsByLane.TryGetValue(lane, out var list)) return null;
        NoteData best     = null;
        double   bestDist = double.MaxValue;
        foreach (var n in list)
        {
            if (_hitTaps.Contains(n.Id)) continue;
            double d = Math.Abs(n.TimeMs - timeMs);
            if (d > maxDeltaMs || d >= bestDist) continue;
            bestDist = d;
            best     = n;
        }
        return best;
    }

    public NoteData FindNearestUnjudgedHold(LaneRef lane, double timeMs)
    {
        if (!_holdsByLane.TryGetValue(lane, out var list)) return null;
        NoteData best     = null;
        double   bestDist = double.MaxValue;
        foreach (var n in list)
        {
            if (_judgedHeads.Contains(n.Id)) continue;
            double d = Math.Abs(n.TimeMs - timeMs);
            if (d > JudgmentWindow.GoodMs || d >= bestDist) continue;
            bestDist = d;
            best     = n;
        }
        return best;
    }

    public IEnumerable<NoteData> EnumerateUnhitTapsExpiredAt(double currentMs, double goodMs)
    {
        foreach (var list in _tapsByLane.Values)
            foreach (var n in list)
                if (!_hitTaps.Contains(n.Id) && currentMs - n.TimeMs > goodMs)
                    yield return n;
    }

    public void MarkTapHit(int noteId)                      => _hitTaps.Add(noteId);
    public void MarkHoldHeadJudged(int noteId, Judgment j)  => _judgedHeads.Add(noteId);
    public void MarkHoldTailJudged(int noteId, Judgment j)  => _judgedTails.Add(noteId);
    public bool IsTapHit(int noteId)                        => _hitTaps.Contains(noteId);
    public bool IsHoldHeadJudged(int noteId)                => _judgedHeads.Contains(noteId);
}
