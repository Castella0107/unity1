using System;
using System.Collections.Generic;

// Unity-independent. No UnityEngine references allowed in this assembly.
// IInputSource backed by a ReplayData.InputEvents list.
// Delta-encoded events are decoded to absolute timestamps in the constructor.
// Call Advance(songTimeMs) every frame from ReplayPlaybackController.Update.
/// <summary>
/// <see cref="ReplayData"/> のデルタエンコードされた入力イベントリストをもとに <see cref="IInputSource"/> を実装するクラス。
/// コンストラクタで絶対タイムスタンプへ変換し、Advance(timeMs) を呼ぶことで該当時刻までのイベントを発火する。
/// </summary>
public sealed class ReplayInputSource : IInputSource
{
    /// <inheritdoc/>
    public event Action<LaneRef, double> OnLaneDown;
    /// <inheritdoc/>
    public event Action<LaneRef, double> OnLaneUp;

    readonly List<AbsEvent> _events;
    int _cursor;

    /// <summary>全イベントを発火済みか。</summary>
    public bool IsFinished  => _cursor >= _events.Count;
    /// <summary>次に発火するイベントのインデックス。</summary>
    public int  CursorIndex => _cursor;
    /// <summary>総イベント数。</summary>
    public int  EventCount  => _events.Count;

    /// <summary>リプレイのデルタエンコード入力列を絶対時刻に展開して初期化する。</summary>
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
