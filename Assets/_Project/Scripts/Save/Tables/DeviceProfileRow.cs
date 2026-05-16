#if SQLITE_NET_PCL
using SQLite;

/// <summary>
/// SQLite の device_profiles テーブルに対応する行クラス。デバイスプロファイルとオフセット設定を保持する。
/// </summary>
[Table("device_profiles")]
public class DeviceProfileRow
{
    [PrimaryKey] public string ProfileId             { get; set; }
    public string DisplayName                        { get; set; }
    [Indexed]    public string OsDeviceName          { get; set; }
    public int   IsAutoSwitchEnabledInt              { get; set; }
    public int   JudgmentOffsetMs                    { get; set; }
    public int   VisualOffsetMs                      { get; set; }
    public long  CreatedAtUnixMs                     { get; set; }
    public long  UpdatedAtUnixMs                     { get; set; }
}
#endif
