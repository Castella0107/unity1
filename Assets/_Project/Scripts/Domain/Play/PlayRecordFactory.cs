using System;

// Unity-independent. No UnityEngine references allowed in this assembly.
// Pure factory — converts PlayProgressSnapshot into a PlayRecord.
/// <summary>
/// <see cref="PlayProgressSnapshot"/> から <see cref="PlayRecord"/> を生成する純粋ファクトリクラス。
/// 難易度倍率の適用・ランク計算・達成フラグの判定を行う。
/// </summary>
public static class PlayRecordFactory
{
    public const string EngineVersion = "1.0.0";

    public static PlayRecord Create(
        PlayProgressSnapshot snap,
        string   songId,
        string   difficulty,
        string   chartHash,
        int      totalNotes,
        string[] modifiers = null,
        bool     isPvP     = false,
        string   matchId   = null)
    {
        int    raw       = snap.CurrentScore;
        int    effective = ApplyDifficultyMultiplier(raw, difficulty);
        string rank      = ScoreCalculator.ComputeRank(effective);

        // Copy sector scores out of the snapshot
        int[] sectorScores = new int[5];
        for (int i = 0; i < 5 && i < snap.SectorScores.Length; i++)
            sectorScores[i] = snap.SectorScores[i];

        return new PlayRecord
        {
            PlayId               = Guid.NewGuid().ToString(),
            SongId               = songId,
            Difficulty           = difficulty,
            PlayedAtUnixMs       = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            RawScore             = raw,
            EffectiveScore       = effective,
            Rank                 = rank,
            PerfectPlusCount     = snap.PerfectPlusCount,
            PerfectCount         = snap.PerfectCount,
            GreatCount           = snap.GreatCount,
            GoodCount            = snap.GoodCount,
            MissCount            = snap.MissCount,
            MaxCombo             = snap.MaxCombo,
            FastCount            = snap.FastCount,
            LateCount            = snap.LateCount,
            TotalNotes           = totalNotes,
            SectorScores         = sectorScores,
            IsFullCombo          = snap.MissCount == 0,
            IsAllPerfect         = raw >= 1_000_000,
            IsAllPerfectPlus     = snap.PerfectPlusCount == totalNotes,
            Modifiers            = modifiers ?? new string[0],
            IsPvP                = isPvP,
            MatchId              = matchId,
            ChartHash            = chartHash,
            JudgmentEngineVersion = EngineVersion,
            ReplayPath           = null,
        };
    }

    // Integer arithmetic avoids float rounding errors (e.g. 1_000_000 * 0.90f = 899_999).
    public static int ApplyDifficultyMultiplier(int raw, string difficulty)
    {
        switch (difficulty != null ? difficulty.ToLower() : "")
        {
            case "easy":   return (int)((long)raw * 75 / 100);
            case "normal": return (int)((long)raw * 80 / 100);
            case "hard":   return (int)((long)raw * 90 / 100);
            default:       return raw;
        }
    }
}
