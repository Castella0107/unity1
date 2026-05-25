using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Thin Unity host for JudgmentEngine.
// Bridges IInputSource events → JudgmentEngine, and JudgmentEngine.OnJudgment → visual/audio effects.
// All judgment logic lives in JudgmentEngine (Domain layer).

/// <summary>
/// JudgmentEngine の Unity ホストクラス。
/// IInputSource（レーン Down / Up）のイベントを JudgmentEngine に転送し、
/// 判定結果を JudgmentEffectsController や NoteScroller へ通知する薄いブリッジ層。
/// 判定ロジックの実体はドメイン層の JudgmentEngine に集約されている。
/// </summary>
public class JudgmentSystem : MonoBehaviour
{
    [SerializeField] AudioConductor            _conductor;
    [SerializeField] NoteScroller              _scroller;
    [SerializeField] JudgmentEffectsController _effects;

    JudgmentEngine _engine;
    IInputSource   _inputSource;

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>内部の判定エンジン。</summary>
    public JudgmentEngine      Engine       => _engine;
    /// <summary>記録中のリプレイ入力バッファ。</summary>
    public ReplayInputBuffer   ReplayBuffer { get; private set; }

    /// <summary>判定確定時に発火する旧来イベント(JudgmentEffectsController 用)。引数: 判定, 時刻ms。</summary>
    public event Action<Judgment, double> OnJudged;

    /// <summary>スコア・コンボ集計アグリゲータ(後方互換アクセサ)。</summary>
    public PlayProgressAggregator Aggregator => _engine?.Progress;

    /// <summary>譜面・メタ・入力ソースから判定システムを初期化する。再初期化時は前の入力購読を解除する。</summary>
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

    // ReplayInputBuffer は DeltaMsFromPrev を int 丸めで永続化する。
    // JudgmentRunner (サーバー側) は累積した int から ProcessLaneDown/Up を呼ぶため、
    // クライアント側もここで int 丸めしてから engine に渡さないと bit-perfect 同一動作にならない。
    void HandleLaneDown(LaneRef lane, double timeMs)
    {
        double t = System.Math.Round(timeMs);
        ReplayBuffer?.Add((int)lane, true, t);
        _engine?.ProcessLaneDown(lane, t);
    }

    void HandleLaneUp(LaneRef lane, double timeMs)
    {
        double t = System.Math.Round(timeMs);
        ReplayBuffer?.Add((int)lane, false, t);
        _engine?.ProcessLaneUp(lane, t);
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

    /// <summary>最終結果のプレイ進行スナップショットを構築して返す。</summary>
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
