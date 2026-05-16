using System.Collections.Generic;

/// <summary>
/// ローカルデータのエクスポート/インポート用 JSON スキーマ DTO。
/// Schema バージョンは互換性チェックのため必須。
/// </summary>
public sealed class ExportSchema
{
    /// <summary>現在のスキーマバージョン。フィールド追加・型変更時にインクリメントする。</summary>
    public const int CurrentVersion = 1;

    public int                          SchemaVersion    { get; set; } = CurrentVersion;
    public string                       ExportedAt       { get; set; }     // ISO 8601 UTC
    public string                       AppVersion       { get; set; }

    public List<PlayRecord>             PlayRecords      { get; set; } = new List<PlayRecord>();
    public List<DeviceProfile>          DeviceProfiles   { get; set; } = new List<DeviceProfile>();
    public string                       ActiveProfileId  { get; set; }
    public List<PerSongOffset>          PerSongOffsets   { get; set; } = new List<PerSongOffset>();
    public List<PersonalBest>           PersonalBests    { get; set; } = new List<PersonalBest>();

    /// PlayerPrefs ホワイトリストの内容(キー → 値、値の型は int/float/string のいずれか)。
    public Dictionary<string, object>   PlayerPrefs      { get; set; } = new Dictionary<string, object>();
}
