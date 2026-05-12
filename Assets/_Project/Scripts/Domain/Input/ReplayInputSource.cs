using System;
using System.Collections.Generic;

// Unity-independent. No UnityEngine references allowed in this assembly.
// IInputSource backed by a ReplayData.InputEvents list.
// Delta-encoded events are decoded to absolute timestamps in the constructor.
// Call Advance(songTimeMs) every frame from ReplayPlaybackController.Update.
public sealed class ReplayInputSource : IInputSource
{
    public event Action<LaneRef, double> OnLaneDown;
    public event Action<LaneRef, double> OnLaneUp;

    readonly List<AbsEvent> _events;
    int _cursor;

    public bool IsFinished  => _cursor >= _events.Count;
    public int  CursorIndex => _cursor;
    public int  EventCount  => _events.Count;

    public ReplayInputSource(ReplayData replay)
    {
        if (replay == null) throw new ArgumentNullException("replay");
        var src = replay.InputEvents;
        _events = new List<AbsEvent>(src != null ? src.Count : 0);

        if (src != null)
        {
            double absTime = 0;
            foreach (var e in src)
            {
                absTime += e.DeltaMsFromPrev;
                _events.Add(new AbsEvent(absTime, (LaneRef)e.Lane, e.Action == 0));
            }
        }
    }

    // Fires all events whose timestamp is <= timeMs.
    public void Advance(double timeMs)
    {
        while (_cursor < _events.Count && _events[_cursor].TimeMs <= timeMs)
        {
            var ev = _events[_cursor++];
            if (ev.IsDown) OnLaneDown?.Invoke(ev.Lane, ev.TimeMs);
            else           OnLaneUp?.Invoke(ev.Lane, ev.TimeMs);
        }
    }

    readonly struct AbsEvent
    {
        public readonly double  TimeMs;
        public readonly LaneRef Lane;
        public readonly bool    IsDown;

        public AbsEvent(double t, LaneRef l, bool d) { TimeMs = t; Lane = l; IsDown = d; }
    }
}
