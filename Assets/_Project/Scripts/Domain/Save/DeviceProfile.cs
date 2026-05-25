using System;

// Unity-independent. No UnityEngine references allowed in this assembly.
/// <summary>
/// デバイスごとのオフセット設定プロファイル。OS デバイス名による自動切替や
/// 作成・更新日時の記録をサポートする。
/// </summary>
public sealed class DeviceProfile
{
    /// <summary>プロファイルの一意なID。</summary>
    public string            ProfileId           { get; set; }
    /// <summary>UI 表示名。</summary>
    public string            DisplayName         { get; set; }
    /// <summary>自動切替の紐付け対象 OS デバイス名(null = 紐付けなし)。</summary>
    public string            OsDeviceName        { get; set; }
    /// <summary>デバイス検出による自動切替が有効か。</summary>
    public bool              IsAutoSwitchEnabled { get; set; }
    /// <summary>このプロファイルのオフセット設定。</summary>
    public AppOffsetSettings Offsets             { get; set; }
    /// <summary>作成日時(Unix エポックからのミリ秒)。</summary>
    public long              CreatedAtUnixMs     { get; set; }
    /// <summary>更新日時(Unix エポックからのミリ秒)。</summary>
    public long              UpdatedAtUnixMs     { get; set; }

    /// <summary>既定のデバイスプロファイルを生成する。</summary>
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
