using System.Collections.Generic;

// Unity-independent. No UnityEngine references allowed in this assembly.
namespace Domain.Pvp
{
    /// <summary>
    /// 1 つのセクター対戦結果。Player A と Player B のセクタースコアを比較した結果。
    /// </summary>
    public readonly struct SectorPair
    {
        public readonly string SongId;
        public readonly int    SectorIndex;   // 0..4 想定
        public readonly int    ScoreA;
        public readonly int    ScoreB;

        public SectorPair(string songId, int sectorIndex, int scoreA, int scoreB)
        {
            SongId      = songId;
            SectorIndex = sectorIndex;
            ScoreA      = scoreA;
            ScoreB      = scoreB;
        }
    }

    public enum SectorOutcome { Draw = 0, AWins = 1, BWins = 2 }

    public readonly struct SectorResult
    {
        public readonly SongId Song;
        public readonly int    SectorIndex;
        public readonly int    ScoreA;
        public readonly int    ScoreB;
        public readonly double PointsA;     // 1.0 / 0.5 / 0.0
        public readonly double PointsB;     // 0.0 / 0.5 / 1.0
        public readonly SectorOutcome Outcome;

        internal SectorResult(string songId, int sectorIndex, int scoreA, int scoreB,
                              double pointsA, double pointsB, SectorOutcome outcome)
        {
            Song        = new SongId(songId);
            SectorIndex = sectorIndex;
            ScoreA      = scoreA;
            ScoreB      = scoreB;
            PointsA     = pointsA;
            PointsB     = pointsB;
            Outcome     = outcome;
        }
    }

    /// <summary>軽量な songId ラッパー (struct で nullable に対応)。</summary>
    public readonly struct SongId
    {
        public readonly string Value;
        public SongId(string v) { Value = v ?? ""; }
        public override string ToString() => Value;
    }

    public enum MatchOutcomeKind { Draw = 0, AWins = 1, BWins = 2 }

    /// <summary>1 試合の集計結果。Sectors と TotalPoints, Outcome を保持。</summary>
    public sealed class MatchOutcome
    {
        public IReadOnlyList<SectorResult> Sectors { get; }
        public double                      TotalPointsA { get; }
        public double                      TotalPointsB { get; }
        public MatchOutcomeKind            Kind         { get; }

        public MatchOutcome(IReadOnlyList<SectorResult> sectors, double a, double b, MatchOutcomeKind kind)
        {
            Sectors     = sectors;
            TotalPointsA = a;
            TotalPointsB = b;
            Kind         = kind;
        }

        /// <summary>Glicko-2 用に各 sector を 1 試合とみなした結果列を構築する。</summary>
        public IEnumerable<Glicko2Result> ToGlicko2ResultsForA(double opponentRating, double opponentRD)
        {
            foreach (var s in Sectors)
                yield return new Glicko2Result(opponentRating, opponentRD, s.PointsA);
        }

        public IEnumerable<Glicko2Result> ToGlicko2ResultsForB(double opponentRating, double opponentRD)
        {
            foreach (var s in Sectors)
                yield return new Glicko2Result(opponentRating, opponentRD, s.PointsB);
        }
    }

    /// <summary>
    /// PVP マッチ採点。設計書: 1試合 = 3曲 × 5セクター = 最大15pt。
    /// セクター毎にスコアを比較し、勝者 +1pt / 同点 0.5pt ずつ。
    /// </summary>
    public static class MatchScoring
    {
        public static MatchOutcome Score(IReadOnlyList<SectorPair> sectors)
        {
            if (sectors == null) sectors = new SectorPair[0];

            var results = new List<SectorResult>(sectors.Count);
            double a = 0, b = 0;
            foreach (var sp in sectors)
            {
                double pa, pb;
                SectorOutcome o;
                if      (sp.ScoreA >  sp.ScoreB) { pa = 1.0; pb = 0.0; o = SectorOutcome.AWins; }
                else if (sp.ScoreA <  sp.ScoreB) { pa = 0.0; pb = 1.0; o = SectorOutcome.BWins; }
                else                              { pa = 0.5; pb = 0.5; o = SectorOutcome.Draw;  }

                a += pa;
                b += pb;
                results.Add(new SectorResult(sp.SongId, sp.SectorIndex, sp.ScoreA, sp.ScoreB, pa, pb, o));
            }

            MatchOutcomeKind kind;
            if      (a >  b) kind = MatchOutcomeKind.AWins;
            else if (a <  b) kind = MatchOutcomeKind.BWins;
            else             kind = MatchOutcomeKind.Draw;

            return new MatchOutcome(results, a, b, kind);
        }
    }
}
