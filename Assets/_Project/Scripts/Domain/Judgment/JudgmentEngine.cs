using System;
using System.Collections.Generic;

// Unity-independent. No UnityEngine references allowed in this assembly.
// Single source of truth for all judgment logic.
// JudgmentSystem (MonoBehaviour) and JudgmentRunner (headless) both delegate here.
/// <summary>
/// 全判定ロジックの単一責務クラス。タップ・ホールドのヒット処理、自動ミス判定、ホールドティック処理を行い、
/// JudgmentEvent をイベントとして通知する。MonoBehaviour 版とヘッドレス版の両方から利用される。
/// </summary>
public sealed class JudgmentEngine
{
    readonly INoteSource                               _notes;
    readonly PlayProgressAggregator                    _progress;
    readonly Dictionary<int,      HoldJudgmentTracker> _holds            = new Dictionary<int, HoldJudgmentTracker>();
    readonly Dictionary<LaneRef, HoldJudgmentTracker>  _activeHoldByLane = new Dictionary<LaneRef, HoldJudgmentTracker>();

    public event Action<JudgmentEvent> OnJudgment;

    public int                    CurrentCombo => _progress.CurrentCombo;
    public int                    MaxCombo     => _progress.MaxCombo;
    public PlayProgressAggregator Progress     => _progress;

    public JudgmentEngine(
        ChartData   chart,
        INoteSource notes,
        BpmTimeline bpm,
        int[]       sectorEndsMs = null,
        Judgment    comboBorder  = Judgment.Good)
    {
        _notes    = notes;
        _progress = new PlayProgressAggregator(chart.TotalNotes, sectorEndsMs ?? new int[0], comboBorder);

        foreach (var n in notes.AllNotes)
            if (n.Type == NoteType.Hold || n.Type == NoteType.FxHold)
                _holds[n.Id] = new HoldJudgmentTracker(n, bpm);
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    public void ProcessLaneDown(LaneRef lane, double timeMs)
    {
        // Re-press while a hold is active on this lane → guard recovery
        if (_activeHoldByLane.TryGetValue(lane, out var activeHold))
        {
            activeHold.OnPressed(timeMs);
            return;
        }

        // Try nearest unhit tap within the good window
        var tap = _notes.FindNearestUnhitTap(lane, timeMs, JudgmentWindow.GoodMs);
        if (tap != null)
        {
            _notes.MarkTapHit(tap.Id);
            double   delta = timeMs - tap.TimeMs;
            Judgment j     = JudgmentWindow.FromDeltaMs(delta);
            _progress.ApplyHit(j, delta, tap.TimeMs);
            Fire(new JudgmentEvent(tap.Id, NoteKind.Tap, lane, j, delta, timeMs, _progress.CurrentCombo));
            return;
        }

        // Try nearest unjudged hold head
        var holdNote = _notes.FindNearestUnjudgedHold(lane, timeMs);
        if (holdNote != null && _holds.TryGetValue(holdNote.Id, out var tracker))
        {
            Judgment? j = tracker.OnHeadInput(timeMs);
            if (j.HasValue)
            {
                _notes.MarkHoldHeadJudged(holdNote.Id, j.Value);
                double delta = timeMs - tracker.StartMs;
                _progress.ApplyHit(j.Value, delta, tracker.StartMs);
                _activeHoldByLane[lane] = tracker;
                Fire(new JudgmentEvent(holdNote.Id, NoteKind.HoldHead, lane, j.Value, delta, timeMs, _progress.CurrentCombo));
            }
        }
    }

    public void ProcessLaneUp(LaneRef lane, double timeMs)
    {
        if (!_activeHoldByLane.TryGetValue(lane, out var tracker)) return;

        if (Math.Abs(timeMs - tracker.EndMs) <= JudgmentWindow.GoodMs)
        {
            Judgment? j = tracker.OnTailInput(timeMs);
            if (j.HasValue)
            {
                _notes.MarkHoldTailJudged(tracker.NoteId, j.Value);
                double delta = timeMs - tracker.EndMs;
                _progress.ApplyHit(j.Value, delta, tracker.EndMs);
                _activeHoldByLane.Remove(lane);
                Fire(new JudgmentEvent(tracker.NoteId, NoteKind.HoldTail, lane, j.Value, delta, timeMs, _progress.CurrentCombo));
                return;
            }
        }

        tracker.OnReleased(timeMs);
    }

    // ── Time advancement (auto-miss + hold ticks + hold tail) ─────────────────

    public void ProcessTime(double timeMs)
    {
        // 1. Tap / FxTap auto-miss — collect first to avoid modifying during iteration
        List<NoteData> expired = null;
        foreach (var n in _notes.EnumerateUnhitTapsExpiredAt(timeMs, JudgmentWindow.GoodMs))
        {
            if (expired == null) expired = new List<NoteData>();
            expired.Add(n);
        }
        if (expired != null)
        {
            foreach (var n in expired)
            {
                _notes.MarkTapHit(n.Id);
                _progress.ApplyMiss(n.TimeMs);
                Fire(new JudgmentEvent(n.Id, NoteKind.Tap, n.Lane, Judgment.Miss, 0, n.TimeMs,
                                       _progress.CurrentCombo, isAutoMiss: true));
            }
        }

        // 2. Hold head auto-miss
        foreach (var h in _holds.Values)
        {
            if (h.OnHeadMissed(timeMs))
            {
                _progress.ApplyMiss(h.StartMs);
                Fire(new JudgmentEvent(h.NoteId, NoteKind.HoldHead, h.Lane, Judgment.Miss, 0, h.StartMs,
                                       _progress.CurrentCombo, isAutoMiss: true));
            }
        }

        // 3. Ticks + tail auto-miss for active holds
        var lanes = new List<LaneRef>(_activeHoldByLane.Keys);
        foreach (var lane in lanes)
        {
            if (!_activeHoldByLane.TryGetValue(lane, out var tracker)) continue;

            foreach (var tick in tracker.AdvanceTo(timeMs))
            {
                _progress.ApplyTick(tick.Judgment, tick.TickTimeMs);
                Fire(new JudgmentEvent(tracker.NoteId, NoteKind.HoldTick, lane,
                                       tick.Judgment, 0, tick.TickTimeMs, _progress.CurrentCombo));
            }

            if (tracker.OnTailMissed(timeMs))
            {
                _notes.MarkHoldTailJudged(tracker.NoteId, Judgment.Miss);
                _progress.ApplyMiss(tracker.EndMs);
                _activeHoldByLane.Remove(lane);
                Fire(new JudgmentEvent(tracker.NoteId, NoteKind.HoldTail, lane, Judgment.Miss, 0, tracker.EndMs,
                                       _progress.CurrentCombo, isAutoMiss: true));
                continue;
            }

            if (tracker.IsCompleted)
                _activeHoldByLane.Remove(lane);
        }
    }

    // ── Result ────────────────────────────────────────────────────────────────

    // Idempotent: FinalizeLastSector advances sectorIdx to 5 and is safe to call repeatedly.
    public PlayProgressSnapshot BuildResult()
    {
        _progress.FinalizeLastSector();
        return _progress.Snapshot();
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    void Fire(JudgmentEvent ev) => OnJudgment?.Invoke(ev);
}
