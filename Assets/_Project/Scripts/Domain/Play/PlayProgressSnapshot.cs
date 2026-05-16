// Unity-independent. No UnityEngine references allowed in this assembly.
/// <summary>
/// ある時点のプレイ進行状態（スコア・コンボ・判定内訳・セクタースコアなど）を
/// 不変のスナップショットとして保持するクラス。
/// </summary>
public sealed class PlayProgressSnapshot
{
    public int   CurrentScore    { get; }
    public int   CurrentCombo    { get; }
    public int   MaxCombo        { get; }
    public int   FastCount       { get; }
    public int   LateCount       { get; }
    public int[] Counts          { get; }   // [pp, p, gr, gd, m]
    public int[] SectorScores    { get; }   // 5 elements
    public int   CurrentSectorIdx { get; }

    public PlayProgressSnapshot(
        int currentScore, int currentCombo, int maxCombo,
        int fastCount, int lateCount,
        int[] counts, int[] sectorScores, int currentSectorIdx)
    {
        CurrentScore     = currentScore;
        CurrentCombo     = currentCombo;
        MaxCombo         = maxCombo;
        FastCount        = fastCount;
        LateCount        = lateCount;
        Counts           = counts;
        SectorScores     = sectorScores;
        CurrentSectorIdx = currentSectorIdx;
    }

    public int PerfectPlusCount => Counts[(int)Judgment.PerfectPlus];
    public int PerfectCount     => Counts[(int)Judgment.Perfect];
    public int GreatCount       => Counts[(int)Judgment.Great];
    public int GoodCount        => Counts[(int)Judgment.Good];
    public int MissCount        => Counts[(int)Judgment.Miss];
}
