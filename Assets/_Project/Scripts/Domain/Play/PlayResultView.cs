// Unity-independent. No UnityEngine references allowed in this assembly.
// Display-only view: wraps PlayRecord and adds song-master / best-score context.
public sealed class PlayResultView
{
    public PlayRecord Record                  { get; set; }

    // From song metadata (not stored in PlayRecord)
    public string SongTitle                   { get; set; }
    public string SongArtist                  { get; set; }
    public int    Level                       { get; set; }

    // Best-score context
    public int    BestEffectiveScoreBefore    { get; set; }
    public bool   IsNewBest                   { get; set; }

    // Convenience accessors — delegate to Record
    public string SongId          => Record.SongId;
    public string Difficulty      => Record.Difficulty;
    public int    EffectiveScore  => Record.EffectiveScore;
    public string Rank            => Record.Rank;
    public bool   IsPvP           => Record.IsPvP;
    public bool   IsFullCombo     => Record.IsFullCombo;
    public bool   IsAllPerfect    => Record.IsAllPerfect;
    public bool   IsAllPerfectPlus => Record.IsAllPerfectPlus;
}
