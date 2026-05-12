using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Thin Unity host for JudgmentEngine.
// Bridges IInputSource events → JudgmentEngine, and JudgmentEngine.OnJudgment → visual/audio effects.
// All judgment logic lives in JudgmentEngine (Domain layer).
public class JudgmentSystem : MonoBehaviour
{
    [SerializeField] AudioConductor            _conductor;
    [SerializeField] NoteScroller              _scroller;
    [SerializeField] JudgmentEffectsController _effects;

    JudgmentEngine _engine;
    IInputSource   _inputSource;

    // ── Public API ─────────────────────────────────────────────────────────────

    public JudgmentEngine      Engine       => _engine;
    public ReplayInputBuffer   ReplayBuffer { get; private set; }

    // Legacy event kept for JudgmentEffectsController.
    public event Action<Judgment, double> OnJudged;

    // Backward-compat accessor used by JudgmentEffectsController.Update.
    public PlayProgressAggregator Aggregator => _engine?.Progress;

    public void Initialize(
        ChartData    chart,
        SongMetadata meta,
        IInputSource input,
        Judgment     comboBorder = Judgment.Good)
    {
        if (chart == null) throw new ArgumentNullException("chart");
        if (meta  == null) throw new ArgumentNullException("meta");
        if (input == null) throw new ArgumentNullException("input",
            "IInputSource is null. Assign GameInputController to GamePlayController._input in Inspector.");

        // Unsubscribe previous source if re-initialized
        if (_inputSource != null)
        {
            _inputSource.OnLaneDown -= HandleLaneDown;
            _inputSource.OnLaneUp   -= HandleLaneUp;
        }

        int[] sectorEnds = meta?.Sectors != null
            ? meta.Sectors.Take(4).Select(s => s.EndMs).ToArray()
            : new int[0];

        var bpm = new BpmTimeline(chart.Events ?? new List<TempoEvent>());
        var src = new ChartDataNoteSource(chart);
        _engine = new JudgmentEngine(chart, src, bpm, sectorEnds, comboBorder);
        _engine.OnJudgment += HandleJudgment;

        ReplayBuffer = new ReplayInputBuffer();

        _inputSource = input;
        _inputSource.OnLaneDown += HandleLaneDown;
        _inputSource.OnLaneUp   += HandleLaneUp;
    }

    // ── Input bridge ───────────────────────────────────────────────────────────

    void HandleLaneDown(LaneRef lane, double timeMs)
    {
        ReplayBuffer?.Add((int)lane, true, timeMs);
        _engine?.ProcessLaneDown(lane, timeMs);
    }

    void HandleLaneUp(LaneRef lane, double timeMs)
    {
        ReplayBuffer?.Add((int)lane, false, timeMs);
        _engine?.ProcessLaneUp(lane, timeMs);
    }

    // ── Frame update ───────────────────────────────────────────────────────────

    void Update()
    {
        if (_engine == null || _conductor == null) return;
        if (!_conductor.IsPlaying) return;
        if (_conductor.SongTimeMs < 0) return;

        _engine.ProcessTime(_conductor.JudgmentTimeMs);
    }

    // ── Judgment event handler ─────────────────────────────────────────────────

    void HandleJudgment(JudgmentEvent ev)
    {
        switch (ev.Kind)
        {
            case NoteKind.Tap:
                if (ev.Judgment == Judgment.Miss) _scroller?.NotifyMiss(ev.NoteId);
                else                              _scroller?.NotifyHit(ev.NoteId, ev.Judgment);
                _effects?.NotifyLane(ev.Lane);
                break;

            case NoteKind.HoldHead:
                if (ev.Judgment != Judgment.Miss) _scroller?.NotifyHitHead(ev.NoteId);
                _effects?.NotifyLane(ev.Lane);
                break;

            case NoteKind.HoldTick:
                // No particle, no note visual; sound handled via OnJudged (throttled in effects).
                break;

            case NoteKind.HoldTail:
                // Note despawns naturally via NoteScroller time-based threshold.
                break;
        }

        OnJudged?.Invoke(ev.Judgment, ev.DeltaMs);
    }

    // ── Result ────────────────────────────────────────────────────────────────

    public PlayProgressSnapshot SnapshotForResult() => _engine?.BuildResult();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void OnDestroy()
    {
        if (_inputSource != null)
        {
            _inputSource.OnLaneDown -= HandleLaneDown;
            _inputSource.OnLaneUp   -= HandleLaneUp;
        }
    }
}
