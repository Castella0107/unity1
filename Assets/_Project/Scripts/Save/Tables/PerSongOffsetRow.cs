#if SQLITE_NET_PCL
using SQLite;

[Table("per_song_offsets")]
public class PerSongOffsetRow
{
    [PrimaryKey] public string SongId           { get; set; }
    public int                 JudgmentOffsetMs { get; set; }
    public long                UpdatedAtUnixMs  { get; set; }
}
#endif
