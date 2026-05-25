// Unity-independent. No UnityEngine references allowed in this assembly.
/// <summary>
/// ある時点のプレイ進行状態（スコア・コンボ・判定内訳・セクタースコアなど）を
/// 不変のスナップショットとして保持するクラス。
/// </summary>
public sealed class PlayProgressSnapshot
{
    /// <summary>表示スコア(0〜1,000,000)。</summary>
    public int   CurrentScore    { get; }
    /// <summary>スナップショット時点のコンボ数。</summary>
    public int   CurrentCombo    { get; }
    /// <summary>最大コンボ数。</summary>
    public int   MaxCombo        { get; }
    /// <summary>早押し回数。</summary>
    public int   FastCount       { get; }
    /// <summary>遅押し回数。</summary>
    public int   LateCount       { get; }
    /// <summary>判定別カウント [PerfectPlus, Perfect, Great, Good, Miss]。</summary>
    public int[] Counts          { get; }
    /// <summary>セクション別スコア(5要素)。</summary>
    public int[] SectorScores    { get; }
    /// <summary>スナップショット時点のセクションインデックス。</summary>
    public int   CurrentSectorIdx { get; }

    /// <summary>全フィールドを指定してスナップショットを生成する。</summary>
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

    /// <summary>PerfectPlus 判定数。</summary>
    public int PerfectPlusCount => Counts[(int)Judgment.PerfectPlus];
    /// <summary>Perfect 判定数。</summary>
    public int PerfectCount     => Counts[(int)Judgment.Perfect];
    /// <summary>Great 判定数。</summary>
    public int GreatCount       => Counts[(int)Judgment.Great];
    /// <summary>Good 判定数。</summary>
    public int GoodCount        => Counts[(int)Judgment.Good];
    /// <summary>Miss 判定数。</summary>
    public int MissCount        => Counts[(int)Judgment.Miss];
}
