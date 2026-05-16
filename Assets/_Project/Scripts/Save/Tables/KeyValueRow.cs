#if SQLITE_NET_PCL
using SQLite;

/// <summary>
/// SQLite の app_kv テーブルに対応する汎用キーバリュー行クラス。アクティブプロファイル ID やマイグレーションフラグなどを格納する。
/// </summary>
// Generic key-value store (active profile ID, migration flags, etc.)
[Table("app_kv")]
public class KeyValueRow
{
    [PrimaryKey] public string Key   { get; set; }
    public string               Value { get; set; }
}
#endif
