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
    readonly List<LaneRef>                             _activeLaneScratch = new List<LaneRef>();  // ProcessTime のキー走査用に再利用

    /// <summary>判定が確定するたびに発火する(タップ/ホールド頭/ティック/尾、オートミス含む)。</summary>
    public event Action<JudgmentEvent> OnJudgment;

    /// <summary>現在のコンボ数。</summary>
    public int                    CurrentCombo => _progress.CurrentCombo;
    /// <summary>これまでの最大コンボ数。</summary>
    public int                    MaxCombo     => _progress.MaxCombo;
    /// <summary>スコア・コンボ・セクション集計を保持するアグリゲータ。</summary>
    public PlayProgressAggregator Progress     => _progress;

    /// <summary>譜面・ノーツソース・BPMタイムラインから判定エンジンを構築し、ホールドトラッカーを準備する。</summary>
    public JudgmentEngine(
        ChartData   chart,
        INoteSource notes,
        BpmTimeline bpm,
        int[]       sectorEndsMs = null,
        Judgment    comboBorder  = Judgment.Good)
    {
        _notes    = notes;
        var ends         = sectorEndsMs ?? new int[0];
        var sectorEvents = ScoringEventCounter.CountPerSector(notes.AllNotes, bpm, ends);
        _progress = new PlayProgressAggregator(chart.TotalNotes, ends, comboBorder, sectorEvents);

        foreach (var n in notes.AllNotes)
            if (n.Type == NoteType.Hold || n.Type == NoteType.FxHold)
                _holds[n.Id] = new HoldJudgmentTracker(n, bpm);
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    /// <summary>レーン押下を処理する。アクティブホールドの再押下、最近傍タップ、ホールド頭の順に判定を試みる。</summary>
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

    /// <summary>レーン離上を処理する。尾は時間経過(ProcessTime の ResolveTail)で自動解決するため、ここではリリース状態のみ記録する。</summary>
    public void ProcessLaneUp(LaneRef lane, double timeMs)
    {
        if (!_activeHoldByLane.TryGetValue(lane, out var tracker)) return;
        tracker.OnReleased(timeMs);
    }

    /// <summary>指定レーンで現在アクティブな (頭判定済み・尾未解決の) ホールドの NoteId。ビジュアル用で判定には影響しない。</summary>
    public int? GetActiveHoldNoteId(LaneRef lane)
        => _activeHoldByLane.TryGetValue(lane, out var t) ? t.NoteId : (int?)null;

    // ── Time advancement (auto-miss + hold ticks + hold tail) ─────────────────

    /// <summary>時刻を進め、タップ/ホールド頭のオートミス、ホールドのティック加算、ホールド尾のオートミスを処理する。</summary>
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
        // HEAD_RECOVERY_SPEC (2026-07-30): 頭ミスでもホールドは放棄せずアクティブ化する。
        // 以降のティックは Miss で進行し、再押下 (ProcessLaneDown → OnPressed) で途中復帰できる。
        foreach (var h in _holds.Values)
        {
            if (h.OnHeadMissed(timeMs))
            {
                _progress.ApplyMiss(h.StartMs);
                Fire(new JudgmentEvent(h.NoteId, NoteKind.HoldHead, h.Lane, Judgment.Miss, 0, h.StartMs,
                                       _progress.CurrentCombo, isAutoMiss: true));
                if (!_activeHoldByLane.ContainsKey(h.Lane))
                    _activeHoldByLane[h.Lane] = h;
            }
        }

        // 3. Ticks + tail auto-miss for active holds
        // アクティブホールドが無い区間はゼロコスト。走査用リストは再利用してフレーム毎の GC を避ける
        // (反復中に _activeHoldByLane を変更するため、キーのスナップショットは従来どおり取る)。
        if (_activeHoldByLane.Count == 0) return;
        _activeLaneScratch.Clear();
        _activeLaneScratch.AddRange(_activeHoldByLane.Keys);
        foreach (var lane in _activeLaneScratch)
        {
            if (!_activeHoldByLane.TryGetValue(lane, out var tracker)) continue;

            foreach (var tick in tracker.AdvanceTo(timeMs))
            {
                _progress.ApplyTick(tick.Judgment, tick.TickTimeMs);
                Fire(new JudgmentEvent(tracker.NoteId, NoteKind.HoldTick, lane,
                                       tick.Judgment, 0, tick.TickTimeMs, _progress.CurrentCombo));
            }

            // Tail auto-resolves at EndMs. 終端の 1/8 は無敵ゾーン(押下/離上を再評価しない):
            // 通常ホールドは直前の実ティック判定を複製(手前でコンボが切れていれば Miss を複製)、
            // 1/8 以下の短いホールドは(支点を取っていれば)P+。
            // 頭ミスのホールドも HEAD_RECOVERY_SPEC によりここまで進行する (復帰していれば Great/P+、
            // 死んだままなら Miss の複製)。
            Judgment? tailJ = tracker.ResolveTail(timeMs);
            if (tailJ.HasValue)
            {
                _notes.MarkHoldTailJudged(tracker.NoteId, tailJ.Value);
                if (tailJ.Value == Judgment.Miss) _progress.ApplyMiss(tracker.EndMs);
                else                              _progress.ApplyHit(tailJ.Value, 0, tracker.EndMs);
                _activeHoldByLane.Remove(lane);
                Fire(new JudgmentEvent(tracker.NoteId, NoteKind.HoldTail, lane, tailJ.Value, 0, tracker.EndMs,
                                       _progress.CurrentCombo, isAutoMiss: tailJ.Value == Judgment.Miss));
                continue;
            }

            if (tracker.IsCompleted)
                _activeHoldByLane.Remove(lane);
        }
    }

    // ── Result ────────────────────────────────────────────────────────────────

    /// <summary>最終セクションを確定してプレイ結果スナップショットを返す。冪等で複数回呼んでも安全。</summary>
    public PlayProgressSnapshot BuildResult()
    {
        _progress.FinalizeLastSector();
        return _progress.Snapshot();
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    void Fire(JudgmentEvent ev) => OnJudgment?.Invoke(ev);
}
