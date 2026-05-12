// Requires sqlite-net-pcl. Enable by adding SQLITE_NET_PCL to Scripting Define Symbols.
#if SQLITE_NET_PCL
using SQLite;

[Table("personal_bests")]
public class PersonalBestRow
{
    [PrimaryKey] public string CompositeKey       { get; set; }  // "{songId}:{difficulty}"
    public string SongId                          { get; set; }
    public string Difficulty                      { get; set; }
    public string BestPlayId                      { get; set; }
    public int    BestEffectiveScore              { get; set; }
    public string BestRank                        { get; set; }
    public int    BestMaxCombo                    { get; set; }
    public int    HasFullComboInt                 { get; set; }
    public int    HasAllPerfectInt                { get; set; }
    public int    HasAllPerfectPlusInt            { get; set; }
    public int    TotalPlays                      { get; set; }
    public long   FirstPlayedAt                   { get; set; }
    public long   LastPlayedAt                    { get; set; }

    public static string MakeKey(string songId, string difficulty) =>
        songId + ":" + difficulty;
}
#endif
