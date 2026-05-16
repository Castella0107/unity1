using System;

// Unity-independent. No UnityEngine references allowed in this assembly.
/// <summary>
/// デバイスごとのオフセット設定プロファイル。OS デバイス名による自動切替や
/// 作成・更新日時の記録をサポートする。
/// </summary>
public sealed class DeviceProfile
{
    public string            ProfileId           { get; set; }
    public string            DisplayName         { get; set; }
    public string            OsDeviceName        { get; set; }  // null = no auto-switch binding
    public bool              IsAutoSwitchEnabled { get; set; }
    public AppOffsetSettings Offsets             { get; set; }
    public long              CreatedAtUnixMs     { get; set; }
    public long              UpdatedAtUnixMs     { get; set; }

    public static DeviceProfile CreateDefault() => new DeviceProfile
    {
        ProfileId           = "default",
        DisplayName         = "Default",
        OsDeviceName        = null,
        IsAutoSwitchEnabled = false,
        Offsets             = AppOffsetSettings.Default,
        CreatedAtUnixMs     = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        UpdatedAtUnixMs     = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
    };
}
