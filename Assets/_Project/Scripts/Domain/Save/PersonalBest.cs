// Unity-independent. No UnityEngine references allowed in this assembly.
/// <summary>
/// 楽曲・難易度ごとのパーソナルベスト記録。最高スコア・最大コンボ・
/// フルコンボ／オールパーフェクト達成フラグ・総プレイ回数などを保持する。
/// </summary>
public sealed class PersonalBest
{
    /// <summary>楽曲ID。</summary>
    public string SongId               { get; set; }
    /// <summary>難易度。</summary>
    public string Difficulty           { get; set; }
    /// <summary>ベストスコアを記録したプレイのID。</summary>
    public string BestPlayId           { get; set; }
    /// <summary>最高実効スコア。</summary>
    public int    BestEffectiveScore   { get; set; }
    /// <summary>最高スコア時のランク。</summary>
    public string BestRank             { get; set; }
    /// <summary>最大コンボ記録。</summary>
    public int    BestMaxCombo         { get; set; }
    /// <summary>フルコンボ達成歴があるか。</summary>
    public bool   HasFullCombo         { get; set; }
    /// <summary>オールパーフェクト達成歴があるか。</summary>
    public bool   HasAllPerfect        { get; set; }
    /// <summary>オール PerfectPlus 達成歴があるか。</summary>
    public bool   HasAllPerfectPlus    { get; set; }
    /// <summary>総プレイ回数。</summary>
    public int    TotalPlays           { get; set; }
    /// <summary>初回プレイ日時(Unix エポックからのミリ秒)。</summary>
    public long   FirstPlayedAt        { get; set; }
    /// <summary>最終プレイ日時(Unix エポックからのミリ秒)。</summary>
    public long   LastPlayedAt         { get; set; }
}
