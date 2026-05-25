using System.Collections.Generic;

// Unity-independent. No UnityEngine references allowed in this assembly.

/// <summary>
/// リプレイファイルの先頭に記録されるヘッダー情報。マジックナンバー・バージョン・フラグ・プレイヤーUUIDを保持する。
/// </summary>
public sealed class ReplayHeader
{
    /// <summary>ファイル識別用マジックナンバー ("RPL1")。</summary>
    public const uint   Magic          = 0x52504C31;
    /// <summary>現行リプレイフォーマットのバージョン。</summary>
    public const ushort CurrentVersion = 1;

    /// <summary>このリプレイのフォーマットバージョン。</summary>
    public ushort Version   { get; set; } = CurrentVersion;
    /// <summary>フォーマットフラグ(予約)。</summary>
    public ushort Flags     { get; set; } = 0;
    /// <summary>プレイヤーUUID(16バイト、Phase 4 まではゼロ埋め)。</summary>
    public byte[] PlayerUuid { get; set; } = new byte[16];
}

/// <summary>
/// リプレイに紐づく楽曲・難易度・オフセット設定・モディファイアなどのメタデータ。
/// </summary>
public sealed class ReplayMetadata
{
    /// <summary>楽曲ID。</summary>
    public string   SongId                { get; set; }
    /// <summary>難易度 (easy/normal/hard/extra)。</summary>
    public string   Difficulty            { get; set; }
    /// <summary>譜面の SHA-256 ハッシュ(32バイト)。</summary>
    public byte[]   ChartHash             { get; set; } = new byte[32];
    /// <summary>プレイ日時(Unix エポックからのミリ秒)。</summary>
    public long     PlayedAtUnixMs        { get; set; }
    /// <summary>プレイ長(ms)。</summary>
    public int      DurationMs            { get; set; }
    /// <summary>基準 BPM。</summary>
    public float    Bpm                   { get; set; }
    /// <summary>適用された判定オフセット(ms)。</summary>
    public short    AppJudgmentOffsetMs   { get; set; }
    /// <summary>適用された表示オフセット(ms)。</summary>
    public short    AppVisualOffsetMs     { get; set; }
    /// <summary>曲別オフセット(ms)。</summary>
    public short    PerSongOffsetMs       { get; set; }
    /// <summary>適用モディファイア。</summary>
    public string[] Modifiers             { get; set; } = new string[0];
    /// <summary>判定エンジンのバージョン。</summary>
    public string   JudgmentEngineVersion { get; set; }
}

/// <summary>
/// リプレイに記録されたプレイ結果（スコア・ランク・判定内訳・コンボ数など）を保持するクラス。
/// </summary>
public sealed class ReplayResult
{
    /// <summary>素点(0〜1,000,000)。</summary>
    public int    RawScore         { get; set; }
    /// <summary>難易度補正後の実効スコア。</summary>
    public int    EffectiveScore   { get; set; }
    /// <summary>ランク(バイナリでは4バイト固定、"S+" 等)。</summary>
    public string Rank             { get; set; }
    /// <summary>PerfectPlus 判定数。</summary>
    public int    PerfectPlusCount { get; set; }
    /// <summary>Perfect 判定数。</summary>
    public int    PerfectCount     { get; set; }
    /// <summary>Great 判定数。</summary>
    public int    GreatCount       { get; set; }
    /// <summary>Good 判定数。</summary>
    public int    GoodCount        { get; set; }
    /// <summary>Miss 判定数。</summary>
    public int    MissCount        { get; set; }
    /// <summary>最大コンボ数。</summary>
    public int    MaxCombo         { get; set; }
    /// <summary>早押し回数。</summary>
    public int    FastCount        { get; set; }
    /// <summary>遅押し回数。</summary>
    public int    LateCount        { get; set; }
    /// <summary>総スコアリングイベント数。</summary>
    public int    TotalNotes       { get; set; }
}

/// <summary>
/// 1つのレーン入力イベント（押下または離し）を表すクラス。直前イベントからの経過時間・レーン番号・アクションを保持する。
/// </summary>
public sealed class ReplayInputEvent
{
    /// <summary>直前イベントからの経過時間(ms、ZigZag VLQ で符号化)。</summary>
    public int  DeltaMsFromPrev { get; set; }
    /// <summary>レーン番号(0-3=Lane0-3, 4=FxL, 5=FxR)。</summary>
    public byte Lane            { get; set; }
    /// <summary>アクション(0=Down, 1=Up)。</summary>
    public byte Action          { get; set; }
}

/// <summary>
/// リプレイファイル全体を表すルートクラス。ヘッダー・メタデータ・結果・入力イベント列を統合して保持する。
/// </summary>
public sealed class ReplayData
{
    /// <summary>ファイルヘッダー。</summary>
    public ReplayHeader             Header      { get; set; } = new ReplayHeader();
    /// <summary>楽曲・難易度・オフセット等のメタデータ。</summary>
    public ReplayMetadata           Metadata    { get; set; }
    /// <summary>プレイ結果。</summary>
    public ReplayResult             Result      { get; set; }
    /// <summary>入力イベント列(デルタエンコード)。</summary>
    public List<ReplayInputEvent>   InputEvents { get; set; } = new List<ReplayInputEvent>();
}
