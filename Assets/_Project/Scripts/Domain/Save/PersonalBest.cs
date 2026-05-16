// Unity-independent. No UnityEngine references allowed in this assembly.
/// <summary>
/// 楽曲・難易度ごとのパーソナルベスト記録。最高スコア・最大コンボ・
/// フルコンボ／オールパーフェクト達成フラグ・総プレイ回数などを保持する。
/// </summary>
public sealed class PersonalBest
{
    public string SongId               { get; set; }
    public string Difficulty           { get; set; }
    public string BestPlayId           { get; set; }
    public int    BestEffectiveScore   { get; set; }
    public string BestRank             { get; set; }
    public int    BestMaxCombo         { get; set; }
    public bool   HasFullCombo         { get; set; }
    public bool   HasAllPerfect        { get; set; }
    public bool   HasAllPerfectPlus    { get; set; }
    public int    TotalPlays           { get; set; }
    public long   FirstPlayedAt        { get; set; }
    public long   LastPlayedAt         { get; set; }
}
