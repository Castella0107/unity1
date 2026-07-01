using System;
using System.Collections.Generic;

// Unity-independent. No UnityEngine references allowed in this assembly.
// IInputSource that synthesises a perfect performance from the chart itself.
// For each note it emits a lane-down at the note's target time (delta 0 → PERFECT+);
// holds also emit a lane-up at TimeMs + DurationMs so the tail judges perfect too.
// Call Advance(songTimeMs) every frame from GamePlayController.Update (same as ReplayInputSource).
/// <summary>
/// 譜面そのものから「完璧な演奏」を合成して供給する <see cref="IInputSource"/> 実装。
/// 各ノーツのターゲット時刻ちょうどに OnLaneDown を発火する(delta 0 → PERFECT+)。
/// ホールドは TimeMs + DurationMs に OnLaneUp も発火し、終端も完璧判定になる。
/// ReplayInputSource と同様、毎フレーム Advance(songTimeMs) を呼ぶ。
/// </summary>
public sealed class AutoPlayInputSource : IInputSource
{
    /// <inheritdoc/>
    public event Action<LaneRef, double> OnLaneDown;
    /// <inheritdoc/>
    public event Action<LaneRef, double> OnLaneUp;

    readonly List<AbsEvent> _events;
    int _cursor;

    /// <summary>全イベントを発火済みか。</summary>
    public bool IsFinished => _cursor >= _events.Count;
    /// <summary>総イベント数。</summary>
    public int  EventCount => _events.Count;

    /// <summary>譜面のノーツ列から完璧な入力イベント列(押下/離上)を時刻順に構築する。</summary>
    public AutoPlayInputSource(ChartData chart)
    {
        if (chart == null) throw new ArgumentNullException("chart");
        var notes = chart.Notes;
        _events = new List<AbsEvent>(notes != null ? notes.Count * 2 : 0);

        if (notes != null)
        {
            foreach (var n in notes)
            {
                if (n == null) continue;
                _events.Add(new AbsEvent(n.TimeMs, n.Lane, true));
                if (n.Type == NoteType.Hold || n.Type == NoteType.FxHold)
                    _events.Add(new AbsEvent(n.TimeMs + n.DurationMs, n.Lane, false));
            }
        }

        // 時刻昇順。同時刻は Down を Up より先に出す(離して押し直しが逆転しないように)。
        _events.Sort((a, b) =>
        {
            int c = a.TimeMs.CompareTo(b.TimeMs);
            if (c != 0) return c;
            return (a.IsDown ? 0 : 1).CompareTo(b.IsDown ? 0 : 1);
        });
    }

    /// <summary>タイムスタンプが <paramref name="timeMs"/> 以下の未発火イベントをすべて発火する。</summary>
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
