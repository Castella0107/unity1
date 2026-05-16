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
    readonly int[]           _sectorEndsMs;   // S1..S4 end times (ms); S5 = song end
    readonly Judgment        _comboBorder;
    readonly int[]           _counts       = new int[5];
    readonly int[]           _sectorScores = new int[5];

    int _currentCombo;
    int _maxCombo;
    int _fastCount;
    int _lateCount;
    int _scoreAtLastSectorEnd;
    int _currentSectorIdx;

    public IReadOnlyList<int> Counts        => _counts;
    public IReadOnlyList<int> SectorScores  => _sectorScores;
    public int CurrentCombo   => _currentCombo;
    public int MaxCombo       => _maxCombo;
    public int FastCount      => _fastCount;
    public int LateCount      => _lateCount;
    public int CurrentScore   => _score.CurrentScore;
    public int CurrentSectorIdx => _currentSectorIdx;

    public PlayProgressAggregator(int totalNotes, int[] sectorEndsMs, Judgment comboBorder)
    {
        _score        = new ScoreCalculator(totalNotes);
        _sectorEndsMs = sectorEndsMs ?? new int[0];
        _comboBorder  = comboBorder;
    }

    /// Hit from Tap, FxTap, Hold head, or Hold tail.
    public void ApplyHit(Judgment j, double deltaMs, double noteTimeMs)
    {
        if (j == Judgment.Miss) { ApplyMiss(noteTimeMs); return; }
        UpdateSectorIfNeeded(noteTimeMs);
        _counts[(int)j]++;
        _score.Add(j);
        UpdateCombo(j);
        UpdateFastLate(j, deltaMs);
    }

    /// Hold tick (PerfectPlus or Miss only).
    public void ApplyTick(Judgment j, double tickTimeMs)
    {
        UpdateSectorIfNeeded(tickTimeMs);
        _counts[(int)j]++;
        _score.Add(j);
        UpdateCombo(j);
    }

    public void ApplyMiss(double noteTimeMs)
    {
        UpdateSectorIfNeeded(noteTimeMs);
        _counts[(int)Judgment.Miss]++;
        _score.Add(Judgment.Miss);
        _currentCombo = 0;
    }

    /// Call once at song end. Finalises S5 (and any sectors not yet advanced through).
    public void FinalizeLastSector()
    {
        while (_currentSectorIdx < 5)
        {
            _sectorScores[_currentSectorIdx] = _score.CurrentScore - _scoreAtLastSectorEnd;
            _scoreAtLastSectorEnd = _score.CurrentScore;
            _currentSectorIdx++;
        }
    }

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
            _sectorScores[_currentSectorIdx] = _score.CurrentScore - _scoreAtLastSectorEnd;
            _scoreAtLastSectorEnd = _score.CurrentScore;
            _currentSectorIdx++;
        }
    }
}
