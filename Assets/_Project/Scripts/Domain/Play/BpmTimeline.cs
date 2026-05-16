using System.Collections.Generic;
using System.Linq;

// Unity-independent. No UnityEngine references allowed in this assembly.
/// <summary>
/// BPMの時系列変化を管理し、任意の時刻におけるBPMおよび1/16拍の間隔（ミリ秒）を返すクラス。
/// </summary>
public class BpmTimeline
{
    readonly List<(double timeMs, double bpm)> _changes;

    public BpmTimeline(IEnumerable<TempoEvent> events)
    {
        _changes = events
            .Where(e => e.Type == "bpm")
            .OrderBy(e => e.TimeMs)
            .Select(e => (e.TimeMs, e.Bpm))
            .ToList();
        if (_changes.Count == 0)
            _changes.Add((0, 120.0));
    }

    public double GetBpmAt(double timeMs)
    {
        double bpm = _changes[0].bpm;
        foreach (var (t, b) in _changes)
        {
            if (t > timeMs) break;
            bpm = b;
        }
        return bpm;
    }

    /// Returns the duration of one 1/16th beat (in ms) at the given time.
    public double GetTickIntervalMs(double timeMs) =>
        (60_000.0 / GetBpmAt(timeMs)) / 16.0;
}
