// Requires sqlite-net-pcl. Enable by adding SQLITE_NET_PCL to Scripting Define Symbols.
#if SQLITE_NET_PCL
using SQLite;

[Table("plays")]
public class PlayRow
{
    [PrimaryKey] public string PlayId    { get; set; }
    [Indexed]    public string SongId    { get; set; }
    [Indexed]    public string Difficulty { get; set; }
    [Indexed]    public long   PlayedAtUnixMs { get; set; }

    public int    RawScore    { get; set; }
    public int    EffectiveScore { get; set; }
    public string Rank        { get; set; }

    public int PpCount    { get; set; }
    public int PCount     { get; set; }
    public int GreatCount { get; set; }
    public int GoodCount  { get; set; }
    public int MissCount  { get; set; }
    public int MaxCombo   { get; set; }
    public int FastCount  { get; set; }
    public int LateCount  { get; set; }
    public int TotalNotes { get; set; }

    public int Sec1Score { get; set; }
    public int Sec2Score { get; set; }
    public int Sec3Score { get; set; }
    public int Sec4Score { get; set; }
    public int Sec5Score { get; set; }

    public int IsFullComboInt       { get; set; }
    public int IsAllPerfectInt      { get; set; }
    public int IsAllPerfectPlusInt  { get; set; }

    // Modifiers stored as comma-separated string (e.g. "Mirror,Random")
    public string ModifiersCsv           { get; set; }
    public int    IsPvpInt               { get; set; }
    public string MatchId                { get; set; }
    public string ChartHash              { get; set; }
    public string JudgmentEngineVersion  { get; set; }
    public string ReplayPath             { get; set; }
}
#endif
