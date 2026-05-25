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
        /// <summary>楽曲の難易度("easy"/"normal"/"hard"/"extra")。獲得ポイントの倍率に使う。null/不明は extra (×1.0) 扱い。</summary>
        public readonly string Difficulty;

        /// <summary>楽曲ID・セクション・両者スコア・難易度を指定してセクター対戦ペアを生成する。</summary>
        public SectorPair(string songId, int sectorIndex, int scoreA, int scoreB, string difficulty = null)
        {
            SongId      = songId;
            SectorIndex = sectorIndex;
            ScoreA      = scoreA;
            ScoreB      = scoreB;
            Difficulty  = difficulty;
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
        /// <summary>Player A の獲得ポイント(勝1.0/分0.5/負0.0 × 難易度倍率)。</summary>
        public readonly double PointsA;
        /// <summary>Player B の獲得ポイント(勝1.0/分0.5/負0.0 × 難易度倍率)。</summary>
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

        // Rating uses the raw sector win/loss (1.0 / 0.5 / 0.0), NOT the difficulty-weighted
        // PointsA/B. The multiplier only scales the displayed 15pt match score; feeding a
        // weighted score into Glicko-2 would break its requirement that A's and B's scores
        // for the same sector sum to 1.0.
        /// <summary>Glicko-2 用に各 sector を 1 試合(素の勝敗)とみなした Player A 視点の結果列を構築する。</summary>
        public IEnumerable<Glicko2Result> ToGlicko2ResultsForA(double opponentRating, double opponentRD)
        {
            foreach (var s in Sectors)
                yield return new Glicko2Result(opponentRating, opponentRD, RawScore(s.Outcome, forA: true));
        }

        /// <summary>Glicko-2 用に各セクターを 1 試合(素の勝敗)とみなした Player B 視点の結果列を構築する。</summary>
        public IEnumerable<Glicko2Result> ToGlicko2ResultsForB(double opponentRating, double opponentRD)
        {
            foreach (var s in Sectors)
                yield return new Glicko2Result(opponentRating, opponentRD, RawScore(s.Outcome, forA: false));
        }

        static double RawScore(SectorOutcome o, bool forA)
        {
            if (o == SectorOutcome.Draw) return 0.5;
            bool win = forA ? o == SectorOutcome.AWins : o == SectorOutcome.BWins;
            return win ? 1.0 : 0.0;
        }
    }

    /// <summary>
    /// PVP マッチ採点。設計書: 1試合 = 3曲 × 5セクター = 最大15pt。
    /// セクター毎にスコアを比較し、勝者 +1pt / 同点 0.5pt ずつ。
    /// </summary>
    public static class MatchScoring
    {
        /// <summary>難易度倍率 (設計書: easy 0.75 / normal 0.80 / hard 0.90 / extra 1.00)。null/不明は 1.0。</summary>
        public static double DifficultyMultiplier(string difficulty)
        {
            switch (difficulty?.Trim().ToLowerInvariant())
            {
                case "easy":   return 0.75;
                case "normal": return 0.80;
                case "hard":   return 0.90;
                case "extra":  return 1.00;
                default:       return 1.00;   // null/未知は重み無し
            }
        }

        /// <summary>
        /// セクター対戦ペア列を採点する。各セクターの勝敗(勝ち1.0/引分0.5/負け0.0)に楽曲の
        /// 難易度倍率を掛けたものを獲得ポイントとし、合計と試合勝敗を集計する。
        /// 倍率は試合ポイント(最大15pt表示)にのみ効き、レーティング(Glicko-2)は素の勝敗を使う。
        /// </summary>
        public static MatchOutcome Score(IReadOnlyList<SectorPair> sectors)
        {
            if (sectors == null) sectors = new SectorPair[0];

            var results = new List<SectorResult>(sectors.Count);
            double a = 0, b = 0;
            foreach (var sp in sectors)
            {
                double rawA, rawB;
                SectorOutcome o;
                if      (sp.ScoreA >  sp.ScoreB) { rawA = 1.0; rawB = 0.0; o = SectorOutcome.AWins; }
                else if (sp.ScoreA <  sp.ScoreB) { rawA = 0.0; rawB = 1.0; o = SectorOutcome.BWins; }
                else                              { rawA = 0.5; rawB = 0.5; o = SectorOutcome.Draw;  }

                double mult = DifficultyMultiplier(sp.Difficulty);
                double pa = rawA * mult;
                double pb = rawB * mult;

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
