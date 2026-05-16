using System;

// Unity-independent. No UnityEngine references allowed in this assembly.
// Persistent play record — used for score DB, leaderboards, replay storage.
/// <summary>
/// スコアDB・リーダーボード・リプレイ保存に使用する永続化プレイ記録クラス。
/// 識別子・スコア・判定内訳・達成フラグ・メタ情報を格納する。
/// </summary>
public sealed class PlayRecord
{
    // Identifier
    public string PlayId                  { get; set; }
    public string SongId                  { get; set; }
    public string Difficulty              { get; set; }   // easy/normal/hard/extra
    public long   PlayedAtUnixMs          { get; set; }

    // Score
    public int    RawScore                { get; set; }   // 0-1,000,000
    public int    EffectiveScore          { get; set; }   // after difficulty multiplier
    public string Rank                    { get; set; }   // S+/S/A+/A/B/C/D

    // Judgment breakdown
    public int    PerfectPlusCount        { get; set; }
    public int    PerfectCount            { get; set; }
    public int    GreatCount              { get; set; }
    public int    GoodCount               { get; set; }
    public int    MissCount               { get; set; }
    public int    MaxCombo                { get; set; }
    public int    FastCount               { get; set; }
    public int    LateCount               { get; set; }
    public int    TotalNotes              { get; set; }

    // Sector scores (5 elements)
    public int[]  SectorScores            { get; set; }

    // Achievement flags (stored alongside the record for offline display)
    public bool   IsFullCombo             { get; set; }
    public bool   IsAllPerfect            { get; set; }
    public bool   IsAllPerfectPlus        { get; set; }

    // Meta
    public string[] Modifiers             { get; set; }   // e.g. ["Mirror"]
    public bool   IsPvP                   { get; set; }
    public string MatchId                 { get; set; }   // PvP only
    public string ChartHash               { get; set; }
    public string JudgmentEngineVersion   { get; set; }
    public string ReplayPath              { get; set; }   // null if not saved
}
