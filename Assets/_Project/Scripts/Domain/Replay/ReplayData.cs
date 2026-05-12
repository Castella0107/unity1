using System.Collections.Generic;

// Unity-independent. No UnityEngine references allowed in this assembly.

public sealed class ReplayHeader
{
    public const uint   Magic          = 0x52504C31;  // "RPL1"
    public const ushort CurrentVersion = 1;

    public ushort Version   { get; set; } = CurrentVersion;
    public ushort Flags     { get; set; } = 0;
    public byte[] PlayerUuid { get; set; } = new byte[16];  // zero-padded until Phase 4
}

public sealed class ReplayMetadata
{
    public string   SongId                { get; set; }
    public string   Difficulty            { get; set; }    // easy/normal/hard/extra
    public byte[]   ChartHash             { get; set; } = new byte[32];  // SHA-256 (32 bytes)
    public long     PlayedAtUnixMs        { get; set; }
    public int      DurationMs            { get; set; }
    public float    Bpm                   { get; set; }
    public short    AppJudgmentOffsetMs   { get; set; }
    public short    AppVisualOffsetMs     { get; set; }
    public short    PerSongOffsetMs       { get; set; }
    public string[] Modifiers             { get; set; } = new string[0];
    public string   JudgmentEngineVersion { get; set; }
}

public sealed class ReplayResult
{
    public int    RawScore         { get; set; }
    public int    EffectiveScore   { get; set; }
    public string Rank             { get; set; }   // 4-byte fixed in binary ("S+", etc.)
    public int    PerfectPlusCount { get; set; }
    public int    PerfectCount     { get; set; }
    public int    GreatCount       { get; set; }
    public int    GoodCount        { get; set; }
    public int    MissCount        { get; set; }
    public int    MaxCombo         { get; set; }
    public int    FastCount        { get; set; }
    public int    LateCount        { get; set; }
    public int    TotalNotes       { get; set; }
}

public sealed class ReplayInputEvent
{
    public int  DeltaMsFromPrev { get; set; }  // ms from previous event (ZigZag VLQ)
    public byte Lane            { get; set; }  // 0=Lane0, 1=Lane1, 2=Lane2, 3=Lane3, 4=FxL, 5=FxR
    public byte Action          { get; set; }  // 0=Down, 1=Up
}

public sealed class ReplayData
{
    public ReplayHeader             Header      { get; set; } = new ReplayHeader();
    public ReplayMetadata           Metadata    { get; set; }
    public ReplayResult             Result      { get; set; }
    public List<ReplayInputEvent>   InputEvents { get; set; } = new List<ReplayInputEvent>();
}
