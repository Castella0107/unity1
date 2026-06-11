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
    /// <summary>各ティックの時刻一覧(1 小節 8 ノーツ刻み = 4/4 で八分音符ごと)。</summary>
    public IReadOnlyList<double> TickTimes { get; }

    bool   _headJudged;
    bool   _tailJudged;
    bool   _isHeld;
    double _lastReleaseMs = -1;   // -1 = never released
    int    _nextTickIdx;
    bool   _abandoned;
    bool   _recovering;           // ガード超過離上から再押下で復帰した直後の1判定だけ Great に落とす

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
    // (8 per measure), excluding any tick within HoldTailGuardMs of the end so the tail
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

    /// <summary>
    /// ホールド中の再押下。ガード(50ms)を超える離上からの再押下は「復帰」とみなし、
    /// 復帰後の最初の1判定(ティックまたは尾)を Great に落とす(_recovering)。
    /// ガード内の素早い再押下は離上扱いせず、ペナルティ無しで継続(復帰フラグも立てない)。
    /// </summary>
    public void OnPressed(double timeMs)
    {
        if (_abandoned || !_headJudged) return;
        if (!_isHeld && _lastReleaseMs >= 0 && (timeMs - _lastReleaseMs) > GUARD_MS)
            _recovering = true;
        _isHeld        = true;
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
    /// 押下中は PerfectPlus(復帰直後の最初の1ティックのみ Great)、ガード(50ms)内の短い離上は許容。
    /// ガード超過の離上中に来たティックは Miss(コンボ断)になるが、放棄はしない:
    /// 再押下(OnPressed)すれば以降のティックは復帰して継続できる。
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
                // 復帰後の最初の1ティックだけ Great に落とし、以降は PerfectPlus に戻す。
                if (_recovering) { j = Judgment.Great; _recovering = false; }
                else             { j = Judgment.PerfectPlus; }
            }
            else
            {
                // Guard period: forgive brief releases (< GUARD_MS)
                double sinceRelease = tickTime - (_lastReleaseMs >= 0 ? _lastReleaseMs : tickTime);
                // ガード超過の離上中に来たティックは Miss(コンボは断たれる)。放棄はせず、
                // 再押下があれば次以降のティックから復帰する。
                j = sinceRelease <= GUARD_MS ? Judgment.PerfectPlus : Judgment.Miss;
            }

            yield return new TickResult(_nextTickIdx, j, tickTime);
            _nextTickIdx++;
        }
    }

    // ── Tail ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// 尾を解決する。currentMs が EndMs に達した最初の呼び出しで判定を返す。離上は不要で、
    /// 押下継続中(またはガード 50ms 内の離上)なら PerfectPlus、ガード超過の離上なら Miss。
    /// 尾が復帰後の最初の1判定(間にティックが無い)になる場合は Great。
    /// 判定済み/放棄済みなら null。毎フレーム呼ぶ。
    /// </summary>
    public Judgment? ResolveTail(double currentMs)
    {
        if (_tailJudged || _abandoned) return null;
        if (currentMs < EndMs)         return null;
        _tailJudged = true;
        if (_isHeld)
        {
            if (_recovering) { _recovering = false; return Judgment.Great; }
            return Judgment.PerfectPlus;
        }
        double sinceRelease = EndMs - (_lastReleaseMs >= 0 ? _lastReleaseMs : EndMs);
        return sinceRelease <= GUARD_MS ? Judgment.PerfectPlus : Judgment.Miss;
    }
}
