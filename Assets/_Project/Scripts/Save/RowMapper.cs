using System;

/// <summary>
/// SQLite 行オブジェクトとドメインモデルを相互変換する静的クラス。
/// SQLite 行に関するメソッドは #if SQLITE_NET_PCL でガードされている。
/// </summary>
// Row ↔ Domain conversion. SQLite-row methods are guarded by #if SQLITE_NET_PCL.
public static class RowMapper
{
#if SQLITE_NET_PCL
    /// <summary>プレイ記録を SQLite 行オブジェクトに変換する。</summary>
    public static PlayRow ToRow(PlayRecord r)
    {
        return new PlayRow
        {
            PlayId               = r.PlayId,
            SongId               = r.SongId,
            Difficulty           = r.Difficulty,
            PlayedAtUnixMs       = r.PlayedAtUnixMs,
            RawScore             = r.RawScore,
            EffectiveScore       = r.EffectiveScore,
            Rank                 = r.Rank,
            PpCount              = r.PerfectPlusCount,
            PCount               = r.PerfectCount,
            GreatCount           = r.GreatCount,
            GoodCount            = r.GoodCount,
            MissCount            = r.MissCount,
            MaxCombo             = r.MaxCombo,
            FastCount            = r.FastCount,
            LateCount            = r.LateCount,
            TotalNotes           = r.TotalNotes,
            Sec1Score = r.SectorScores != null && r.SectorScores.Length > 0 ? r.SectorScores[0] : 0,
            Sec2Score = r.SectorScores != null && r.SectorScores.Length > 1 ? r.SectorScores[1] : 0,
            Sec3Score = r.SectorScores != null && r.SectorScores.Length > 2 ? r.SectorScores[2] : 0,
            Sec4Score = r.SectorScores != null && r.SectorScores.Length > 3 ? r.SectorScores[3] : 0,
            Sec5Score = r.SectorScores != null && r.SectorScores.Length > 4 ? r.SectorScores[4] : 0,
            IsFullComboInt      = r.IsFullCombo      ? 1 : 0,
            IsAllPerfectInt     = r.IsAllPerfect     ? 1 : 0,
            IsAllPerfectPlusInt = r.IsAllPerfectPlus ? 1 : 0,
            // Modifiers serialized as comma-separated (values never contain commas)
            ModifiersCsv        = r.Modifiers != null ? string.Join(",", r.Modifiers) : "",
            IsPvpInt            = r.IsPvP ? 1 : 0,
            MatchId             = r.MatchId,
            ChartHash           = r.ChartHash,
            JudgmentEngineVersion = r.JudgmentEngineVersion,
            ReplayPath          = r.ReplayPath,
        };
    }

    /// <summary>SQLite 行オブジェクトをプレイ記録に変換する(null は null)。</summary>
    public static PlayRecord ToRecord(PlayRow row)
    {
        if (row == null) return null;
        return new PlayRecord
        {
            PlayId           = row.PlayId,
            SongId           = row.SongId,
            Difficulty       = row.Difficulty,
            PlayedAtUnixMs   = row.PlayedAtUnixMs,
            RawScore         = row.RawScore,
            EffectiveScore   = row.EffectiveScore,
            Rank             = row.Rank,
            PerfectPlusCount = row.PpCount,
            PerfectCount     = row.PCount,
            GreatCount       = row.GreatCount,
            GoodCount        = row.GoodCount,
            MissCount        = row.MissCount,
            MaxCombo         = row.MaxCombo,
            FastCount        = row.FastCount,
            LateCount        = row.LateCount,
            TotalNotes       = row.TotalNotes,
            SectorScores     = new[] {
                row.Sec1Score, row.Sec2Score, row.Sec3Score, row.Sec4Score, row.Sec5Score },
            IsFullCombo      = row.IsFullComboInt      != 0,
            IsAllPerfect     = row.IsAllPerfectInt     != 0,
            IsAllPerfectPlus = row.IsAllPerfectPlusInt != 0,
            Modifiers        = string.IsNullOrEmpty(row.ModifiersCsv)
                ? new string[0]
                : row.ModifiersCsv.Split(','),
            IsPvP            = row.IsPvpInt  != 0,
            MatchId          = row.MatchId,
            ChartHash        = row.ChartHash,
            JudgmentEngineVersion = row.JudgmentEngineVersion,
            ReplayPath       = row.ReplayPath,
        };
    }

    /// <summary>PVP マッチ記録を SQLite 行オブジェクトに変換する。</summary>
    public static PvpMatchRow ToPvpRow(PvpMatchRecord m)
    {
        return new PvpMatchRow
        {
            MatchId                 = m.MatchId,
            SelfUserId              = m.SelfUserId,
            OpponentId              = m.OpponentId,
            ResultKind              = m.ResultKind,
            SelfPoints              = m.SelfPoints,
            OpponentPoints          = m.OpponentPoints,
            SelfRatingBefore        = m.SelfRatingBefore,
            SelfRatingAfter         = m.SelfRatingAfter,
            OpponentRatingBefore    = m.OpponentRatingBefore,
            OpponentRatingAfter     = m.OpponentRatingAfter,
            SongIdsCsv              = JoinCsv(m.SongIds),
            DifficultiesCsv         = JoinCsv(m.Difficulties),
            SelfSectorScoresCsv     = JoinInts(m.SelfSectorScores),
            OpponentSectorScoresCsv = JoinInts(m.OpponentSectorScores),
            SelfReplayPathsBar      = m.SelfReplayPaths != null ? string.Join("|", m.SelfReplayPaths) : "",
            CompletedAtUnixMs       = m.CompletedAtUnixMs,
        };
    }

    /// <summary>SQLite 行オブジェクトを PVP マッチ記録に変換する(null は null)。</summary>
    public static PvpMatchRecord ToPvpRecord(PvpMatchRow row)
    {
        if (row == null) return null;
        return new PvpMatchRecord
        {
            MatchId               = row.MatchId,
            SelfUserId            = row.SelfUserId,
            OpponentId            = row.OpponentId,
            ResultKind            = row.ResultKind,
            SelfPoints            = row.SelfPoints,
            OpponentPoints        = row.OpponentPoints,
            SelfRatingBefore      = row.SelfRatingBefore,
            SelfRatingAfter       = row.SelfRatingAfter,
            OpponentRatingBefore  = row.OpponentRatingBefore,
            OpponentRatingAfter   = row.OpponentRatingAfter,
            SongIds               = SplitCsv(row.SongIdsCsv),
            Difficulties          = SplitCsv(row.DifficultiesCsv),
            SelfSectorScores      = SplitInts(row.SelfSectorScoresCsv),
            OpponentSectorScores  = SplitInts(row.OpponentSectorScoresCsv),
            SelfReplayPaths       = string.IsNullOrEmpty(row.SelfReplayPathsBar)
                ? new string[0] : row.SelfReplayPathsBar.Split('|'),
            CompletedAtUnixMs     = row.CompletedAtUnixMs,
        };
    }

    static string JoinCsv(string[] a)  => a != null ? string.Join(",", a) : "";
    static string[] SplitCsv(string s) => string.IsNullOrEmpty(s) ? new string[0] : s.Split(',');

    static string JoinInts(int[] a)
    {
        if (a == null || a.Length == 0) return "";
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < a.Length; i++) { if (i > 0) sb.Append(','); sb.Append(a[i]); }
        return sb.ToString();
    }

    static int[] SplitInts(string s)
    {
        if (string.IsNullOrEmpty(s)) return new int[0];
        var parts  = s.Split(',');
        var result = new int[parts.Length];
        for (int i = 0; i < parts.Length; i++) int.TryParse(parts[i], out result[i]);
        return result;
    }

    /// <summary>SQLite 行オブジェクトをパーソナルベストに変換する(null は null)。</summary>
    public static PersonalBest ToBest(PersonalBestRow row)
    {
        if (row == null) return null;
        return new PersonalBest
        {
            SongId             = row.SongId,
            Difficulty         = row.Difficulty,
            BestPlayId         = row.BestPlayId,
            BestEffectiveScore = row.BestEffectiveScore,
            BestRank           = row.BestRank,
            BestMaxCombo       = row.BestMaxCombo,
            HasFullCombo       = row.HasFullComboInt       != 0,
            HasAllPerfect      = row.HasAllPerfectInt      != 0,
            HasAllPerfectPlus  = row.HasAllPerfectPlusInt  != 0,
            TotalPlays         = row.TotalPlays,
            FirstPlayedAt      = row.FirstPlayedAt,
            LastPlayedAt       = row.LastPlayedAt,
        };
    }
#endif
}
