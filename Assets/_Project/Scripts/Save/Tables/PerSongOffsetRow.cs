#if SQLITE_NET_PCL
using SQLite;

/// <summary>
/// SQLite の per_song_offsets テーブルに対応する行クラス。楽曲ごとのジャッジメントオフセット値を保持する。
/// </summary>
[Table("per_song_offsets")]
public class PerSongOffsetRow
{
    [PrimaryKey] public string SongId           { get; set; }
    public int                 JudgmentOffsetMs { get; set; }
    public long                UpdatedAtUnixMs  { get; set; }
}
#endif
