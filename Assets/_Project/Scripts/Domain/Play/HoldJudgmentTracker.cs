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
/// 押下継続・ガード期間・復帰を管理するクラス。
/// HEAD_RECOVERY_SPEC (2026-07-30): 頭ミスでも放棄せず、再押下で途中から復帰できる
/// (Go サーバー engine/hold.go と同一意味論 — 変更は必ず両側同時に)。
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

    bool     _headJudged;
    bool     _headMissed;         // 頭をオートミスしたか (HEAD_RECOVERY_SPEC: 放棄はせず途中復帰可能)
    Judgment _headJudgment;       // 支点(ヘッド)の確定判定。実 tick が 1 つも無い場合の踏襲元フォールバック。
    Judgment _lastTickJudgment;   // 直前の「実 tick」(無敵ゾーンより手前のティック)の判定。終端 1/8 はこれを複製する。
    bool     _tailJudged;
    bool     _isHeld;
    double   _lastReleaseMs = -1;   // -1 = never released
    int      _nextTickIdx;
    bool     _recovering;           // ガード超過離上から再押下で復帰した直後の1判定だけ Great に落とす
    readonly bool _isShort;         // 1/8 以下の長さ(ボディティックが 1 本も無い)ホールドか

    const double GUARD_MS = 50.0;

    /// <summary>ホールド頭が判定済みか。</summary>
    public bool IsHeadJudged => _headJudged;
    /// <summary>ホールド尾が判定済みか。</summary>
    public bool IsTailJudged => _tailJudged;
    /// <summary>追跡が完了しているか (= 尾判定済み)。
    /// HEAD_RECOVERY_SPEC (2026-07-30): 頭ミスでも放棄しない — 途中の再押下で復帰できる。</summary>
    public bool IsCompleted  => _tailJudged;

    /// <summary>ノーツと BPM タイムラインからホールド追跡を初期化し、ティック時刻を事前計算する。</summary>
    public HoldJudgmentTracker(NoteData note, BpmTimeline bpm)
    {
        NoteId    = note.Id;
        Lane      = note.Lane;
        StartMs   = note.TimeMs;
        EndMs     = note.TimeMs + note.DurationMs;
        TickTimes = ComputeTickTimes(StartMs, EndMs, bpm);
        // ボディティックが 1 本も無い = 長さが 1/8(八分音符 = 小節/8)以下のホールド。
        // この場合は終端(尾)が唯一の無敵ゾーンで、支点(ヘッド)さえ通れば無条件 P+ で通す。
        _isShort  = TickTimes.Count == 0;
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
        _headJudged       = true;
        _isHeld           = true;
        _headJudgment     = JudgmentWindow.FromDeltaMs(delta);
        _lastTickJudgment = _headJudgment;   // 実 tick が来るまでの踏襲元フォールバック(ヘッド)
        return _headJudgment;
    }

    /// <summary>ホールド頭がタイムアウトした最初の呼び出しで true を返す(オートミス)。
    /// HEAD_RECOVERY_SPEC: 放棄はせず、以降のティックは Miss として進行する
    /// (再押下すれば OnPressed の復帰ロジックで途中から取れる)。</summary>
    public bool OnHeadMissed(double currentMs)
    {
        if (_headJudged) return false;
        if (currentMs - StartMs > JudgmentWindow.GoodMs)
        {
            _headJudged       = true;
            _headMissed       = true;
            _lastTickJudgment = Judgment.Miss;  // 無敵ゾーン/尾の複製元は Miss から開始
            _lastReleaseMs    = StartMs;        // 頭時刻を離上起点に = 復帰まで全ティック Miss
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
        if (!_headJudged) return;
        if (!_isHeld && _lastReleaseMs >= 0 && (timeMs - _lastReleaseMs) > GUARD_MS)
            _recovering = true;
        _isHeld        = true;
        _lastReleaseMs = -1;
    }

    /// <summary>キー離上。ガード期間のカウントダウンを開始する。</summary>
    public void OnReleased(double timeMs)
    {
        if (!_headJudged) return;
        _isHeld        = false;
        _lastReleaseMs = timeMs;
    }

    // ── Ticks ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// currentMs まで進め、新たに経過した各ティックの判定結果を返す。毎フレーム呼ぶ。
    /// 押下中は PerfectPlus(復帰直後の最初の1ティックのみ Great)、ガード(50ms)内の短い離上は許容。
    /// ガード超過の離上中に来たティックは Miss(コンボ断)になるが、放棄はしない:
    /// 再押下(OnPressed)すれば以降のティックは復帰して継続できる。
    /// 例外: 最終ボディティック(= 終端の 1/8 無敵ゾーン)では「離上に対する新たなペナルティ」を課さない:
    ///   ・押下中(再押下含む) → 通常どおり復帰判定に従う(復帰直後 Great → 以降 PerfectPlus)。
    ///     直前が Miss でも終端で押していれば復帰する。
    ///   ・離上中 → 直前の実ティック判定(_lastTickJudgment)を複製する
    ///     (手前でコンボが切れていた=直前 Miss ならそのまま Miss、押せていた=P+ なら救済)。
    /// </summary>
    public IEnumerable<TickResult> AdvanceTo(double currentMs)
    {
        if (!_headJudged) yield break;

        while (_nextTickIdx < TickTimes.Count && currentMs >= TickTimes[_nextTickIdx])
        {
            double   tickTime  = TickTimes[_nextTickIdx];
            // 最終ボディティックは終端の 1/8(八分音符)無敵ゾーン。ティック間隔は 1/8 なので
            // 「最後の 1/8」に入るボディティックは必ずこの 1 本だけ(浮動小数境界に依らず index で判定)。
            bool     inInvincibleZone = _nextTickIdx == TickTimes.Count - 1;
            Judgment j;

            if (_isHeld)
            {
                // 押下中(無敵ゾーンでも同じ): 復帰後の最初の1ティックだけ Great、以降 PerfectPlus。
                if (_recovering) { j = Judgment.Great; _recovering = false; }
                else             { j = Judgment.PerfectPlus; }
            }
            else if (inInvincibleZone)
            {
                // 無敵ゾーンかつ離上中: 新たな離上ペナルティを課さず直前の実ティック判定を複製。
                j = _lastTickJudgment;
            }
            else
            {
                // Guard period: forgive brief releases (< GUARD_MS)
                double sinceRelease = tickTime - (_lastReleaseMs >= 0 ? _lastReleaseMs : tickTime);
                // ガード超過の離上中に来たティックは Miss(コンボは断たれる)。放棄はせず、
                // 再押下があれば次以降のティックから復帰する。
                j = sinceRelease <= GUARD_MS ? Judgment.PerfectPlus : Judgment.Miss;
            }

            _lastTickJudgment = j;
            yield return new TickResult(_nextTickIdx, j, tickTime);
            _nextTickIdx++;
        }
    }

    // ── Tail ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// 尾を解決する。currentMs が EndMs に達した最初の呼び出しで判定を返す。
    /// 尾は終端の 1/8 無敵ゾーンに属するため「離上に対する新たなペナルティ」を課さない:
    ///   ・1/8 以下の短いホールド(ボディティック無し): 支点(ヘッド)を取っていれば無条件 P+。
    ///     (頭ミスの短ホールドは HEAD_RECOVERY_SPEC により通常規則: 押下中=復帰判定/離上中=Miss)
    ///   ・押下中(再押下含む): 通常どおり復帰判定に従う(復帰直後 Great → 以降 PerfectPlus)。
    ///     直前が Miss でも終端で押していれば復帰する。
    ///   ・離上中: 直前の実ティック判定(_lastTickJudgment)を複製する
    ///     (無敵ゾーン最終ティックと同値。手前で離してコンボが切れていれば Miss を複製)。
    /// 判定済み/放棄済みなら null。毎フレーム呼ぶ。
    /// </summary>
    public Judgment? ResolveTail(double currentMs)
    {
        if (_tailJudged)       return null;
        if (currentMs < EndMs) return null;
        _tailJudged = true;
        // 1/8 以下の短いホールドは終端が唯一の無敵ゾーン。支点(頭)を取っていれば無条件 P+。
        // 頭ミスの短ホールド (HEAD_RECOVERY_SPEC) は下の通常規則へ:
        // 押下中(=復帰) なら Great/P+、離上中なら Miss (複製元 _lastTickJudgment = Miss)。
        if (_isShort && !_headMissed) return Judgment.PerfectPlus;
        // 押下中の尾は復帰判定に従う(直前が Miss でも押していれば復帰)。
        if (_isHeld)
        {
            if (_recovering) { _recovering = false; return Judgment.Great; }
            return Judgment.PerfectPlus;
        }
        // 離上中の尾は新たなペナルティ無し。直前の実ティック判定を複製。
        return _lastTickJudgment;
    }
}
