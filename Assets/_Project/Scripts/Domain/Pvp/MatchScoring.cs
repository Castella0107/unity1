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
        /// <summary>Player A の難易度("easy"/"normal"/"hard"/"extra")。実効スコア = スコア × 難易度倍率 の比較に使う。null/不明は extra (×1.0)。</summary>
        public readonly string DifficultyA;
        /// <summary>Player B の難易度。各プレイヤーが自分の難易度を選べる(相手と異なり得る)。</summary>
        public readonly string DifficultyB;
        /// <summary>Player A のタイブレーク値(Σ 2×PerfectPlus + Perfect)。実効達成率同点時に優劣を決める。</summary>
        public readonly int    TieA;
        /// <summary>Player B のタイブレーク値(Σ 2×PerfectPlus + Perfect)。</summary>
        public readonly int    TieB;
        /// <summary>Player A のセクター理論満点(達成率% の分母)。0/未指定なら実効スコア比較にフォールバック。</summary>
        public readonly int    MaxA;
        /// <summary>Player B のセクター理論満点。各自の譜面(難易度)に依存し A/B で異なり得る。</summary>
        public readonly int    MaxB;

        /// <summary>
        /// 楽曲ID・セクション・両者スコア・難易度・(任意)タイブレーク値・(任意)セクター満点を指定する。
        /// <paramref name="difficultyB"/> 省略時は <paramref name="difficultyA"/> を両者に適用(後方互換)。
        /// <paramref name="maxA"/>/<paramref name="maxB"/> を与えると達成率%(スコア/満点)× 倍率で比較、未指定なら実効スコア比較。
        /// </summary>
        public SectorPair(string songId, int sectorIndex, int scoreA, int scoreB,
                          string difficultyA = null, int tieA = 0, int tieB = 0, string difficultyB = null,
                          int maxA = 0, int maxB = 0)
        {
            SongId      = songId;
            SectorIndex = sectorIndex;
            ScoreA      = scoreA;
            ScoreB      = scoreB;
            DifficultyA = difficultyA;
            DifficultyB = difficultyB ?? difficultyA;
            TieA        = tieA;
            TieB        = tieB;
            MaxA        = maxA;
            MaxB        = maxB;
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
        /// <summary>難易度倍率 (設計書: easy 0.75 / normal 0.80 / hard 0.90 / extra 1.00)。表示用。null/不明は 1.0。</summary>
        public static double DifficultyMultiplier(string difficulty) => MultiplierPercent(difficulty) / 100.0;

        /// <summary>難易度倍率を百分率の整数で返す (easy75 / normal80 / hard90 / extra100)。実効スコア比較に使う(浮動小数回避)。</summary>
        public static int MultiplierPercent(string difficulty)
        {
            switch (difficulty?.Trim().ToLowerInvariant())
            {
                case "easy":   return 75;
                case "normal": return 80;
                case "hard":   return 90;
                case "extra":  return 100;
                default:       return 100;   // null/未知は重み無し
            }
        }

        /// <summary>
        /// セクター対戦ペア列を採点する。各プレイヤーの「実効達成率 = (セクタースコア / セクター満点) × 自分の難易度倍率」を
        /// 比較してセクター勝敗を決め、勝敗ポイントは <b>フラット</b>(勝ち1.0/引分0.5/負け0.0、倍率を掛けない)。
        /// 満点(MaxA/MaxB)が無い場合は実効スコア比較(スコア × 倍率)にフォールバック(後方互換)。
        /// 同点はタイブレーク(Σ 2×PerfectPlus + Perfect、実効=各自の倍率を掛けて比較)で決着、なお同点なら真の引分。
        /// 合計ポイントの多い側が試合勝者。レーティング(Glicko-2)は素の勝敗を使う。
        /// 例: easy 900000/満点900000(=100%) vs extra 500000/満点500000(=100%) → 100%×0.75 &lt; 100%×1.0 → extra 勝ち
        ///     (達成率比較なので、ノーツ分布が偏って一方のセクター満点が大きくても不公平にならない)。
        /// </summary>
        public static MatchOutcome Score(IReadOnlyList<SectorPair> sectors)
        {
            if (sectors == null) sectors = new SectorPair[0];

            var results = new List<SectorResult>(sectors.Count);
            double a = 0, b = 0;
            foreach (var sp in sectors)
            {
                int multA = MultiplierPercent(sp.DifficultyA);
                int multB = MultiplierPercent(sp.DifficultyB);

                // 比較値: 満点があれば「達成率%(スコア/満点)× 倍率」を交差比較(割り算回避)、
                //         無ければ「実効スコア(スコア × 倍率)」にフォールバック(後方互換)。
                //   達成率比較: (ScoreA/MaxA × multA) vs (ScoreB/MaxB × multB)
                //            ⇔ ScoreA × multA × MaxB  vs  ScoreB × multB × MaxA
                long cmpA, cmpB;
                if (sp.MaxA > 0 && sp.MaxB > 0)
                {
                    cmpA = (long)sp.ScoreA * multA * sp.MaxB;
                    cmpB = (long)sp.ScoreB * multB * sp.MaxA;
                }
                else
                {
                    cmpA = (long)sp.ScoreA * multA;
                    cmpB = (long)sp.ScoreB * multB;
                }

                double rawA, rawB;
                SectorOutcome o;
                if      (cmpA >  cmpB) { rawA = 1.0; rawB = 0.0; o = SectorOutcome.AWins; }
                else if (cmpA <  cmpB) { rawA = 0.0; rawB = 1.0; o = SectorOutcome.BWins; }
                else
                {
                    // 同点 → タイブレーク (Σ 2×P+ + P を実効化して比較)。
                    long tieA = (long)sp.TieA * multA;
                    long tieB = (long)sp.TieB * multB;
                    if      (tieA > tieB) { rawA = 1.0; rawB = 0.0; o = SectorOutcome.AWins; }
                    else if (tieA < tieB) { rawA = 0.0; rawB = 1.0; o = SectorOutcome.BWins; }
                    else                  { rawA = 0.5; rawB = 0.5; o = SectorOutcome.Draw;  }
                }

                // 勝敗ポイントはフラット(難易度倍率は実効スコア比較で既に反映済み)。
                double pa = rawA;
                double pb = rawB;

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
