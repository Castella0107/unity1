// Unity-independent. No UnityEngine references allowed in this assembly.
namespace Domain.Pvp
{
    /// <summary>
    /// Glicko-2 におけるプレイヤーのレーティング状態。
    /// 公式の Glicko-2 紙 (Glickman 2012) では rating r / rating deviation RD / volatility σ の 3 値で表現される。
    /// クライアント/サーバー間で bit-perfect 同期するため値型 (immutable) として扱う。
    /// </summary>
    public sealed class Glicko2Player
    {
        /// <summary>表示用レーティング (1500 が初期値)。</summary>
        public double Rating           { get; }
        /// <summary>RD: rating deviation。低いほど rating の信頼性が高い (350 が初期値)。</summary>
        public double RatingDeviation  { get; }
        /// <summary>σ: volatility。レーティング変動の不安定さ (0.06 が初期値、典型 0.03〜0.1)。</summary>
        public double Volatility       { get; }

        public Glicko2Player(double rating, double ratingDeviation, double volatility)
        {
            Rating           = rating;
            RatingDeviation  = ratingDeviation;
            Volatility       = volatility;
        }

        /// <summary>新規プレイヤー (Glicko-2 公式推奨の初期値)。</summary>
        public static Glicko2Player CreateDefault() => new Glicko2Player(1500.0, 350.0, 0.06);

        public override string ToString()
            => $"Glicko2Player(R={Rating:F2}, RD={RatingDeviation:F2}, σ={Volatility:F4})";
    }

    /// <summary>1 つの試合結果。score は 0 = 負け、0.5 = 引分け、1 = 勝ち。</summary>
    public readonly struct Glicko2Result
    {
        public readonly double OpponentRating;
        public readonly double OpponentRatingDeviation;
        public readonly double Score;   // 0, 0.5, 1

        public Glicko2Result(double opponentRating, double opponentRD, double score)
        {
            OpponentRating          = opponentRating;
            OpponentRatingDeviation = opponentRD;
            Score                   = score;
        }
    }
}
