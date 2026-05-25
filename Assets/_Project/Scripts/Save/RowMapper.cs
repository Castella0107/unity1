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
