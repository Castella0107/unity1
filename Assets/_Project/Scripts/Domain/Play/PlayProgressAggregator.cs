using System.Collections.Generic;

// Unity-independent. No UnityEngine references allowed in this assembly.
//
// Sector score uses the "score delta" method:
//   sectorScores[i] = score_at_end_of_sector_i - score_at_end_of_sector_i-1
// This guarantees sum(sectorScores) == CurrentScore regardless of int rounding.
/// <summary>
/// プレイ中のスコア・コンボ・セクタースコア・Fast/Late カウントをリアルタイムで集計するクラス。
/// スコアデルタ方式により、セクタースコアの総和が常に CurrentScore と一致することを保証する。
/// </summary>
public class PlayProgressAggregator
{
    readonly ScoreCalculator _score;
    // Parallel "ideal" calculator: every scoring event scored as PerfectPlus.
    // Yields the maximum attainable score per sector, enabling accuracy-rate (達成率)
    // display without re-walking the chart. Purely additive — does NOT affect _score,
    // Counts, SectorScores, CurrentScore or Snapshot, so server bit-perfect is preserved.
    readonly ScoreCalculator _maxScore;
    readonly int[]           _sectorEndsMs;   // S1..S4 end times (ms); S5 = song end
    readonly Judgment        _comboBorder;
    readonly int[]           _counts          = new int[5];
    readonly int[]           _sectorScores    = new int[5];
    readonly int[]           _sectorMaxScores = new int[5];

    int _currentCombo;
    int _maxCombo;
    int _fastCount;
    int _lateCount;
    int _scoreAtLastSectorEnd;
    int _maxScoreAtLastSectorEnd;
    int _currentSectorIdx;

    /// <summary>判定別カウント [PerfectPlus, Perfect, Great, Good, Miss]。</summary>
    public IReadOnlyList<int> Counts        => _counts;
    /// <summary>セクション別スコア(スコアデルタ方式、総和は CurrentScore に一致)。</summary>
    public IReadOnlyList<int> SectorScores  => _sectorScores;
    /// <summary>セクション別の理論満点(全 PerfectPlus 時のスコアデルタ)。達成率の分母。</summary>
    public IReadOnlyList<int> SectorMaxScores => _sectorMaxScores;
    /// <summary>現在までに通過したノーツの理論満点(全 PerfectPlus 時の表示スコア)。総合達成率の分母。</summary>
    public int CurrentMaxScore   => _maxScore.CurrentScore;
    /// <summary>進行中セクションの暫定スコア(直前セクション確定以降の増分)。</summary>
    public int CurrentSectorScore    => CurrentScore - _scoreAtLastSectorEnd;
    /// <summary>進行中セクションの暫定満点(直前セクション確定以降の理論満点増分)。</summary>
    public int CurrentSectorMaxScore => CurrentMaxScore - _maxScoreAtLastSectorEnd;
    /// <summary>現在のコンボ数。</summary>
    public int CurrentCombo   => _currentCombo;
    /// <summary>最大コンボ数。</summary>
    public int MaxCombo       => _maxCombo;
    /// <summary>早押し(Fast)回数。</summary>
    public int FastCount      => _fastCount;
    /// <summary>遅押し(Late)回数。</summary>
    public int LateCount      => _lateCount;
    /// <summary>現在の表示スコア(0〜1,000,000)。</summary>
    public int CurrentScore   => _score.CurrentScore;
    /// <summary>現在処理中のセクションインデックス(0〜5)。</summary>
    public int CurrentSectorIdx => _currentSectorIdx;

    /// <summary>総ノーツ数・セクション終了時刻・コンボ継続境界判定を指定して集計器を初期化する。</summary>
    public PlayProgressAggregator(int totalNotes, int[] sectorEndsMs, Judgment comboBorder)
    {
        _score        = new ScoreCalculator(totalNotes);
        _maxScore     = new ScoreCalculator(totalNotes);
        _sectorEndsMs = sectorEndsMs ?? new int[0];
        _comboBorder  = comboBorder;
    }

    /// <summary>タップ/FxTap/ホールド頭/ホールド尾のヒットを反映する。Miss なら <see cref="ApplyMiss"/> に委譲。</summary>
    public void ApplyHit(Judgment j, double deltaMs, double noteTimeMs)
    {
        if (j == Judgment.Miss) { ApplyMiss(noteTimeMs); return; }
        UpdateSectorIfNeeded(noteTimeMs);
        _counts[(int)j]++;
        _score.Add(j);
        _maxScore.Add(Judgment.PerfectPlus);
        UpdateCombo(j);
        UpdateFastLate(j, deltaMs);
    }

    /// <summary>ホールドティック(PerfectPlus または Miss のみ)を反映する。</summary>
    public void ApplyTick(Judgment j, double tickTimeMs)
    {
        UpdateSectorIfNeeded(tickTimeMs);
        _counts[(int)j]++;
        _score.Add(j);
        _maxScore.Add(Judgment.PerfectPlus);
        UpdateCombo(j);
    }

    /// <summary>ミスを反映する(コンボリセット)。</summary>
    public void ApplyMiss(double noteTimeMs)
    {
        UpdateSectorIfNeeded(noteTimeMs);
        _counts[(int)Judgment.Miss]++;
        _score.Add(Judgment.Miss);
        _maxScore.Add(Judgment.PerfectPlus);
        _currentCombo = 0;
    }

    /// <summary>曲終端で 1 回呼ぶ。未確定のセクション(S5 含む)のスコアを確定する。冪等。</summary>
    public void FinalizeLastSector()
    {
        while (_currentSectorIdx < 5)
        {
            _sectorScores[_currentSectorIdx]    = _score.CurrentScore    - _scoreAtLastSectorEnd;
            _sectorMaxScores[_currentSectorIdx] = _maxScore.CurrentScore - _maxScoreAtLastSectorEnd;
            _scoreAtLastSectorEnd    = _score.CurrentScore;
            _maxScoreAtLastSectorEnd = _maxScore.CurrentScore;
            _currentSectorIdx++;
        }
    }

    /// <summary>現在の集計状態を不変のスナップショットとして取得する(配列は複製)。</summary>
    public PlayProgressSnapshot Snapshot()
    {
        return new PlayProgressSnapshot(
            _score.CurrentScore,
            _currentCombo,
            _maxCombo,
            _fastCount,
            _lateCount,
            (int[])_counts.Clone(),
            (int[])_sectorScores.Clone(),
            _currentSectorIdx);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    void UpdateCombo(Judgment j)
    {
        if ((int)j <= (int)_comboBorder)
        {
            _currentCombo++;
            if (_currentCombo > _maxCombo) _maxCombo = _currentCombo;
        }
        else
        {
            _currentCombo = 0;
        }
    }

    void UpdateFastLate(Judgment j, double deltaMs)
    {
        if (j == Judgment.PerfectPlus) return;   // Just = no Fast/Late attribution
        if (deltaMs < -2.0) _fastCount++;
        else if (deltaMs > 2.0) _lateCount++;
    }

    void UpdateSectorIfNeeded(double timeMs)
    {
        while (_currentSectorIdx < _sectorEndsMs.Length
               && timeMs >= _sectorEndsMs[_currentSectorIdx])
        {
            _sectorScores[_currentSectorIdx]    = _score.CurrentScore    - _scoreAtLastSectorEnd;
            _sectorMaxScores[_currentSectorIdx] = _maxScore.CurrentScore - _maxScoreAtLastSectorEnd;
            _scoreAtLastSectorEnd    = _score.CurrentScore;
            _maxScoreAtLastSectorEnd = _maxScore.CurrentScore;
            _currentSectorIdx++;
        }
    }
}
