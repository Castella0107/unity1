using System.Collections.Generic;

// Unity-independent. No UnityEngine references allowed in this assembly.

/// <summary>
/// ホールドノートの各ティック判定結果を保持する読み取り専用構造体。
/// </summary>
public readonly struct TickResult
{
    /// <summary>ティックの連番インデックス。</summary>
    public readonly int      TickIdx;
    /// <summary>このティックの判定結果。</summary>
    public readonly Judgment Judgment;
    /// <summary>ティックの時刻(ms)。</summary>
    public readonly double   TickTimeMs;

    /// <summary>ティック結果を生成する。</summary>
    public TickResult(int tickIdx, Judgment judgment, double tickTimeMs)
    {
        TickIdx    = tickIdx;
        Judgment   = judgment;
        TickTimeMs = tickTimeMs;
    }
}

/// <summary>
/// ホールドノート1本分のヘッド・ティック・テール判定状態を追跡し、
/// 押下継続・ガード期間・ミス放棄を管理するクラス。
/// </summary>
public class HoldJudgmentTracker
{
    /// <summary>対象ホールドノーツのID。</summary>
    public int                  NoteId    { get; }
    /// <summary>対象レーン。</summary>
    public LaneRef              Lane      { get; }
    /// <summary>ホールド開始時刻(ms)。</summary>
    public double               StartMs   { get; }
    /// <summary>ホールド終了時刻(ms)。</summary>
    public double               EndMs     { get; }
    /// <summary>各ティックの時刻一覧(1 小節刻み)。</summary>
    public IReadOnlyList<double> TickTimes { get; }

    bool   _headJudged;
    bool   _tailJudged;
    bool   _isHeld;
    double _lastReleaseMs = -1;   // -1 = never released
    int    _nextTickIdx;
    bool   _abandoned;

    const double GUARD_MS = 50.0;

    /// <summary>ホールド頭が判定済みか。</summary>
    public bool IsHeadJudged => _headJudged;
    /// <summary>ホールド尾が判定済みか。</summary>
    public bool IsTailJudged => _tailJudged;
    /// <summary>ガード超過などで放棄されたか。</summary>
    public bool IsAbandoned  => _abandoned;
    /// <summary>尾判定済みまたは放棄済みで、追跡が完了しているか。</summary>
    public bool IsCompleted  => _tailJudged || _abandoned;

    /// <summary>ノーツと BPM タイムラインからホールド追跡を初期化し、ティック時刻を事前計算する。</summary>
    public HoldJudgmentTracker(NoteData note, BpmTimeline bpm)
    {
        NoteId    = note.Id;
        Lane      = note.Lane;
        StartMs   = note.TimeMs;
        EndMs     = note.TimeMs + note.DurationMs;
        TickTimes = ComputeTickTimes(StartMs, EndMs, bpm);
    }

    // Body ticks are placed strictly inside (startMs, endMs) at the hold-tick interval
    // (2 per measure), excluding any tick within HoldTailGuardMs of the end so the tail
    // takes priority (no double combo/score when a measure boundary lands on the end).
    // Must stay identical to ScoringEventCounter.CountHoldTicks so all-perfect totals 1,000,000.
    static List<double> ComputeTickTimes(double startMs, double endMs, BpmTimeline bpm)
    {
        var ticks  = new List<double>();
        double cursor = startMs;
        while (true)
        {
            cursor += bpm.GetHoldTickIntervalMs(cursor);
            if (cursor >= endMs - BpmTimeline.HoldTailGuardMs) break;
            ticks.Add(cursor);
        }
        return ticks;
    }

    // ── Head ──────────────────────────────────────────────────────────────────

    /// <summary>ホールド頭への押下を判定する。Good 窓外/判定済みなら null。</summary>
    public Judgment? OnHeadInput(double timeMs)
    {
        if (_headJudged) return null;
        double delta = timeMs - StartMs;
        if (System.Math.Abs(delta) > JudgmentWindow.GoodMs) return null;
        _headJudged = true;
        _isHeld     = true;
        return JudgmentWindow.FromDeltaMs(delta);
    }

    /// <summary>ホールド頭がタイムアウトした最初の呼び出しで true を返す(オートミス)。</summary>
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

    /// <summary>ホールド中の再押下(ガード期間の再アクティブ化)。</summary>
    public void OnPressed(double timeMs)
    {
        if (_abandoned || !_headJudged) return;
        _isHeld       = true;
        _lastReleaseMs = -1;
    }

    /// <summary>キー離上。ガード期間のカウントダウンを開始する。</summary>
    public void OnReleased(double timeMs)
    {
        if (_abandoned || !_headJudged) return;
        _isHeld        = false;
        _lastReleaseMs = timeMs;
    }

    // ── Ticks ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// currentMs まで進め、新たに経過した各ティックの判定結果を返す。毎フレーム呼ぶ。
    /// 押下中は PerfectPlus、ガード(50ms)内の短い離上は許容、超過すると放棄し残りを Miss で流す。
    /// </summary>
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

    /// <summary>
    /// 尾を解決する。currentMs が EndMs に達した最初の呼び出しで判定を返す。離上は不要で、
    /// 押下継続中(またはガード 50ms 内の離上)なら PerfectPlus、ガード超過の離上なら Miss。
    /// 判定済み/放棄済みなら null。毎フレーム呼ぶ。
    /// </summary>
    public Judgment? ResolveTail(double currentMs)
    {
        if (_tailJudged || _abandoned) return null;
        if (currentMs < EndMs)         return null;
        _tailJudged = true;
        if (_isHeld) return Judgment.PerfectPlus;
        double sinceRelease = EndMs - (_lastReleaseMs >= 0 ? _lastReleaseMs : EndMs);
        return sinceRelease <= GUARD_MS ? Judgment.PerfectPlus : Judgment.Miss;
    }
}
