#if SQLITE_NET_PCL
using SQLite;

// Generic key-value store (active profile ID, migration flags, etc.)
[Table("app_kv")]
public class KeyValueRow
{
    [PrimaryKey] public string Key   { get; set; }
    public string               Value { get; set; }
}
#endif
