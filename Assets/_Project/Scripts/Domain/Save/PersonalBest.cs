// Unity-independent. No UnityEngine references allowed in this assembly.
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
