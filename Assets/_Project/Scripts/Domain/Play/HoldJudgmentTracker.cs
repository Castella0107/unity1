using System.Collections.Generic;

// Unity-independent. No UnityEngine references allowed in this assembly.

public readonly struct TickResult
{
    public readonly int      TickIdx;
    public readonly Judgment Judgment;
    public readonly double   TickTimeMs;

    public TickResult(int tickIdx, Judgment judgment, double tickTimeMs)
    {
        TickIdx    = tickIdx;
        Judgment   = judgment;
        TickTimeMs = tickTimeMs;
    }
}

public class HoldJudgmentTracker
{
    public int                  NoteId    { get; }
    public LaneRef              Lane      { get; }
    public double               StartMs   { get; }
    public double               EndMs     { get; }
    public IReadOnlyList<double> TickTimes { get; }

    bool   _headJudged;
    bool   _tailJudged;
    bool   _isHeld;
    double _lastReleaseMs = -1;   // -1 = never released
    int    _nextTickIdx;
    bool   _abandoned;

    const double GUARD_MS = 50.0;

    public bool IsHeadJudged => _headJudged;
    public bool IsTailJudged => _tailJudged;
    public bool IsAbandoned  => _abandoned;
    public bool IsCompleted  => _tailJudged || _abandoned;

    public HoldJudgmentTracker(NoteData note, BpmTimeline bpm)
    {
        NoteId    = note.Id;
        Lane      = note.Lane;
        StartMs   = note.TimeMs;
        EndMs     = note.TimeMs + note.DurationMs;
        TickTimes = ComputeTickTimes(StartMs, EndMs, bpm);
    }

    static List<double> ComputeTickTimes(double startMs, double endMs, BpmTimeline bpm)
    {
        var ticks  = new List<double>();
        double cursor = startMs;
        while (true)
        {
            cursor += bpm.GetTickIntervalMs(cursor);
            if (cursor >= endMs) break;
            ticks.Add(cursor);
        }
        return ticks;
    }

    // ── Head ──────────────────────────────────────────────────────────────────

    public Judgment? OnHeadInput(double timeMs)
    {
        if (_headJudged) return null;
        double delta = timeMs - StartMs;
        if (System.Math.Abs(delta) > JudgmentWindow.GoodMs) return null;
        _headJudged = true;
        _isHeld     = true;
        return JudgmentWindow.FromDeltaMs(delta);
    }

    /// Returns true if the head just timed out (first call to exceed the window).
    public bool OnHeadMissed(double currentMs)
    {
        if (_headJudged || _abandoned) return false;
        if (currentMs - StartMs > JudgmentWindow.GoodMs)
        {
            _headJudged = true;
            _abandoned  = true;
            return true;
        }
        return false;
    }

    // ── Key state ─────────────────────────────────────────────────────────────

    /// Re-press during hold (guard period re-activation).
    public void OnPressed(double timeMs)
    {
        if (_abandoned || !_headJudged) return;
        _isHeld       = true;
        _lastReleaseMs = -1;
    }

    /// Key released — begins guard period countdown.
    public void OnReleased(double timeMs)
    {
        if (_abandoned || !_headJudged) return;
        _isHeld        = false;
        _lastReleaseMs = timeMs;
    }

    // ── Ticks ─────────────────────────────────────────────────────────────────

    /// Advance to currentMs, yielding results for each newly elapsed tick.
    /// Call every frame from JudgmentSystem.Update.
    public IEnumerable<TickResult> AdvanceTo(double currentMs)
    {
        if (_abandoned || !_headJudged) yield break;

        while (_nextTickIdx < TickTimes.Count && currentMs >= TickTimes[_nextTickIdx])
        {
            double   tickTime = TickTimes[_nextTickIdx];
            Judgment j;

            if (_isHeld)
            {
                j = Judgment.PerfectPlus;
            }
            else
            {
                // Guard period: forgive brief releases (< GUARD_MS)
                double sinceRelease = tickTime - (_lastReleaseMs >= 0 ? _lastReleaseMs : tickTime);
                if (sinceRelease <= GUARD_MS)
                {
                    j = Judgment.PerfectPlus;
                }
                else
                {
                    // Guard exceeded — abandon; drain remaining ticks as Miss
                    _abandoned = true;
                    yield return new TickResult(_nextTickIdx, Judgment.Miss, tickTime);
                    _nextTickIdx++;
                    while (_nextTickIdx < TickTimes.Count)
                    {
                        yield return new TickResult(_nextTickIdx, Judgment.Miss, TickTimes[_nextTickIdx]);
                        _nextTickIdx++;
                    }
                    yield break;
                }
            }

            yield return new TickResult(_nextTickIdx, j, tickTime);
            _nextTickIdx++;
        }
    }

    // ── Tail ──────────────────────────────────────────────────────────────────

    public Judgment? OnTailInput(double timeMs)
    {
        if (_tailJudged || _abandoned) return null;
        double delta = timeMs - EndMs;
        if (System.Math.Abs(delta) > JudgmentWindow.GoodMs) return null;
        _tailJudged = true;
        return JudgmentWindow.FromDeltaMs(delta);
    }

    /// Returns true if the tail window just expired (call every frame).
    public bool OnTailMissed(double currentMs)
    {
        if (_tailJudged || _abandoned) return false;
        if (currentMs - EndMs > JudgmentWindow.GoodMs)
        {
            _tailJudged = true;
            return true;
        }
        return false;
    }
}
