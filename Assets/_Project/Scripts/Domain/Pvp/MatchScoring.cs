using System.Collections.Generic;

// Unity-independent. No UnityEngine references allowed in this assembly.
namespace Domain.Pvp
{
    /// <summary>
    /// 1 つのセクター対戦結果。Player A と Player B のセクタースコアを比較した結果。
    /// </summary>
    public readonly struct SectorPair
    {
        /// <summary>対象楽曲ID。</summary>
        public readonly string SongId;
        /// <summary>セクションインデックス(0〜4 想定)。</summary>
        public readonly int    SectorIndex;
        /// <summary>Player A のセクタースコア。</summary>
        public readonly int    ScoreA;
        /// <summary>Player B のセクタースコア。</summary>
        public readonly int    ScoreB;

        /// <summary>楽曲ID・セクション・両者スコアを指定してセクター対戦ペアを生成する。</summary>
        public SectorPair(string songId, int sectorIndex, int scoreA, int scoreB)
        {
            SongId      = songId;
            SectorIndex = sectorIndex;
            ScoreA      = scoreA;
            ScoreB      = scoreB;
        }
    }

    /// <summary>セクター単位の勝敗(引分け/A勝ち/B勝ち)。</summary>
    public enum SectorOutcome { Draw = 0, AWins = 1, BWins = 2 }

    /// <summary>1 セクターの採点結果。両者スコア・獲得ポイント・勝敗を保持する。</summary>
    public readonly struct SectorResult
    {
        /// <summary>対象楽曲。</summary>
        public readonly SongId Song;
        /// <summary>セクションインデックス。</summary>
        public readonly int    SectorIndex;
        /// <summary>Player A のスコア。</summary>
        public readonly int    ScoreA;
        /// <summary>Player B のスコア。</summary>
        public readonly int    ScoreB;
        /// <summary>Player A の獲得ポイント(1.0 / 0.5 / 0.0)。</summary>
        public readonly double PointsA;
        /// <summary>Player B の獲得ポイント(0.0 / 0.5 / 1.0)。</summary>
        public readonly double PointsB;
        /// <summary>このセクターの勝敗。</summary>
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
        /// <summary>楽曲ID 文字列(null は空文字に正規化)。</summary>
        public readonly string Value;
        /// <summary>文字列から SongId を生成する(null は空文字)。</summary>
        public SongId(string v) { Value = v ?? ""; }
        /// <inheritdoc/>
        public override string ToString() => Value;
    }

    /// <summary>試合全体の勝敗(引分け/A勝ち/B勝ち)。</summary>
    public enum MatchOutcomeKind { Draw = 0, AWins = 1, BWins = 2 }

    /// <summary>1 試合の集計結果。Sectors と TotalPoints, Outcome を保持。</summary>
    public sealed class MatchOutcome
    {
        /// <summary>セクター別の採点結果一覧。</summary>
        public IReadOnlyList<SectorResult> Sectors { get; }
        /// <summary>Player A の合計ポイント。</summary>
        public double                      TotalPointsA { get; }
        /// <summary>Player B の合計ポイント。</summary>
        public double                      TotalPointsB { get; }
        /// <summary>試合全体の勝敗。</summary>
        public MatchOutcomeKind            Kind         { get; }

        /// <summary>セクター結果と合計ポイント・勝敗から試合結果を生成する。</summary>
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

        /// <summary>Glicko-2 用に各セクターを 1 試合とみなした Player B 視点の結果列を構築する。</summary>
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
        /// <summary>セクター対戦ペア列を採点し、各セクターの勝敗(勝ち+1 / 引分け+0.5)を集計した試合結果を返す。</summary>
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
