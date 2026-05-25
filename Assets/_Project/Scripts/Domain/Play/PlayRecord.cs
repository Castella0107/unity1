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
    /// <summary>このプレイの一意なID。</summary>
    public string PlayId                  { get; set; }
    /// <summary>プレイした楽曲ID。</summary>
    public string SongId                  { get; set; }
    /// <summary>難易度 (easy/normal/hard/extra)。</summary>
    public string Difficulty              { get; set; }
    /// <summary>プレイ日時(Unix エポックからのミリ秒)。</summary>
    public long   PlayedAtUnixMs          { get; set; }

    // Score
    /// <summary>素点(0〜1,000,000)。</summary>
    public int    RawScore                { get; set; }
    /// <summary>難易度補正後の実効スコア。</summary>
    public int    EffectiveScore          { get; set; }
    /// <summary>ランク (S+/S/A+/A/B/C/D)。</summary>
    public string Rank                    { get; set; }

    // Judgment breakdown
    /// <summary>PerfectPlus 判定数。</summary>
    public int    PerfectPlusCount        { get; set; }
    /// <summary>Perfect 判定数。</summary>
    public int    PerfectCount            { get; set; }
    /// <summary>Great 判定数。</summary>
    public int    GreatCount              { get; set; }
    /// <summary>Good 判定数。</summary>
    public int    GoodCount               { get; set; }
    /// <summary>Miss 判定数。</summary>
    public int    MissCount               { get; set; }
    /// <summary>最大コンボ数。</summary>
    public int    MaxCombo                { get; set; }
    /// <summary>早押し回数。</summary>
    public int    FastCount               { get; set; }
    /// <summary>遅押し回数。</summary>
    public int    LateCount               { get; set; }
    /// <summary>総スコアリングイベント数。</summary>
    public int    TotalNotes              { get; set; }

    /// <summary>セクション別スコア(5要素)。</summary>
    public int[]  SectorScores            { get; set; }

    // Achievement flags (stored alongside the record for offline display)
    /// <summary>フルコンボ達成か。</summary>
    public bool   IsFullCombo             { get; set; }
    /// <summary>全ノーツ Perfect 以上か。</summary>
    public bool   IsAllPerfect            { get; set; }
    /// <summary>全ノーツ PerfectPlus か。</summary>
    public bool   IsAllPerfectPlus        { get; set; }

    // Meta
    /// <summary>適用モディファイア(例: ["Mirror"])。</summary>
    public string[] Modifiers             { get; set; }
    /// <summary>PvP マッチでのプレイか。</summary>
    public bool   IsPvP                   { get; set; }
    /// <summary>マッチID(PvP のみ)。</summary>
    public string MatchId                 { get; set; }
    /// <summary>譜面ハッシュ。</summary>
    public string ChartHash               { get; set; }
    /// <summary>判定エンジンのバージョン。</summary>
    public string JudgmentEngineVersion   { get; set; }
    /// <summary>リプレイファイルのパス(未保存なら null)。</summary>
    public string ReplayPath              { get; set; }
}
