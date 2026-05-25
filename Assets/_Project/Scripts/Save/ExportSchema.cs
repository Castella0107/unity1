using System.Collections.Generic;

/// <summary>
/// ローカルデータのエクスポート/インポート用 JSON スキーマ DTO。
/// Schema バージョンは互換性チェックのため必須。
/// </summary>
public sealed class ExportSchema
{
    /// <summary>現在のスキーマバージョン。フィールド追加・型変更時にインクリメントする。</summary>
    public const int CurrentVersion = 1;

    /// <summary>このエクスポートのスキーマバージョン。</summary>
    public int                          SchemaVersion    { get; set; } = CurrentVersion;
    /// <summary>エクスポート日時(ISO 8601 UTC)。</summary>
    public string                       ExportedAt       { get; set; }
    /// <summary>エクスポート時のアプリバージョン。</summary>
    public string                       AppVersion       { get; set; }

    /// <summary>全プレイ記録。</summary>
    public List<PlayRecord>             PlayRecords      { get; set; } = new List<PlayRecord>();
    /// <summary>全デバイスプロファイル。</summary>
    public List<DeviceProfile>          DeviceProfiles   { get; set; } = new List<DeviceProfile>();
    /// <summary>アクティブだったプロファイルID。</summary>
    public string                       ActiveProfileId  { get; set; }
    /// <summary>全曲別オフセット。</summary>
    public List<PerSongOffset>          PerSongOffsets   { get; set; } = new List<PerSongOffset>();
    /// <summary>全パーソナルベスト。</summary>
    public List<PersonalBest>           PersonalBests    { get; set; } = new List<PersonalBest>();

    /// <summary>PlayerPrefs ホワイトリストの内容(キー → 値、値の型は int/float/string のいずれか)。</summary>
    public Dictionary<string, object>   PlayerPrefs      { get; set; } = new Dictionary<string, object>();
}
