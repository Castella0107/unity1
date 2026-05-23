using NUnit.Framework;
using Domain.Pvp;

namespace Domain.Pvp.Tests
{
    /// <summary>
    /// Glickman 2012 "Example of the Glicko-2 system" (Section 6) の reference 値に対する検証。
    /// </summary>
    [TestFixture]
    public class Glicko2CalculatorTests
    {
        const double Tolerance = 0.01;

        [Test]
        public void Update_GlickmanExample_MatchesPaperReferenceValues()
        {
            // Section 6: 初期 r=1500, RD=200, σ=0.06 が 3 試合で次の値になる
            var player = new Glicko2Player(1500.0, 200.0, 0.06);
            var results = new[]
            {
                new Glicko2Result(1400.0,  30.0, 1.0),   // win vs lower
                new Glicko2Result(1550.0, 100.0, 0.0),   // loss vs higher
                new Glicko2Result(1700.0, 300.0, 0.0),   // loss vs much higher
            };

            var result = Glicko2Calculator.Update(player, results);

            // 論文記載の reference: r ≈ 1464.06, RD ≈ 151.52, σ ≈ 0.05999
            Assert.AreEqual(1464.06, result.Rating,          Tolerance, "Rating should match paper");
            Assert.AreEqual( 151.52, result.RatingDeviation, Tolerance, "RD should match paper");
            Assert.AreEqual(0.05999, result.Volatility,    1e-4,        "σ should match paper");
        }

        [Test]
        public void Update_NoGames_LeavesRatingUnchangedAndIncreasesRd()
        {
            var p = new Glicko2Player(1500.0, 200.0, 0.06);
            var p2 = Glicko2Calculator.Update(p, new Glicko2Result[0]);
            Assert.AreEqual(1500.0, p2.Rating, 1e-9);
            Assert.Greater(p2.RatingDeviation, p.RatingDeviation, "RD should grow during inactivity");
            Assert.AreEqual(p.Volatility, p2.Volatility, 1e-9);
        }

        [Test]
        public void Decay_OnlyExpandsRd()
        {
            var p  = new Glicko2Player(1500.0, 200.0, 0.06);
            var p2 = Glicko2Calculator.Decay(p);
            Assert.AreEqual(p.Rating, p2.Rating, 1e-9);
            Assert.Greater(p2.RatingDeviation, p.RatingDeviation);
            Assert.AreEqual(p.Volatility, p2.Volatility, 1e-9);
        }

        [Test]
        public void SeasonDecay_PullsRatingHalfwayTo1500()
        {
            var p  = new Glicko2Player(1700.0, 80.0, 0.05);
            var p2 = Glicko2Calculator.SeasonDecay(p);
            Assert.AreEqual(1600.0, p2.Rating, 1e-9, "1700 → 1500 + (1700-1500)*0.5 = 1600");
            Assert.AreEqual( 100.0, p2.RatingDeviation, 1e-9, "RD floor = 100 が適用される");
            Assert.AreEqual(p.Volatility, p2.Volatility, 1e-9);
        }

        [Test]
        public void SeasonDecay_BelowFloorRd_NotShrunk()
        {
            var p  = new Glicko2Player(1500.0, 50.0, 0.06);  // RD=50 は floor=100 より下
            var p2 = Glicko2Calculator.SeasonDecay(p);
            Assert.AreEqual(100.0, p2.RatingDeviation, 1e-9, "RD は floor 値まで持ち上げられる");
        }

        [Test]
        public void Update_DefaultPlayer_WinAgainstWeak_RatingGoesUp()
        {
            var p  = Glicko2Player.CreateDefault();   // 1500/350/0.06
            var p2 = Glicko2Calculator.Update(p, new[] { new Glicko2Result(1400.0, 30.0, 1.0) });
            Assert.Greater(p2.Rating, p.Rating, "Winning against lower-rated should increase rating");
            Assert.Less(p2.RatingDeviation, p.RatingDeviation, "RD should decrease after a played game");
        }

        [Test]
        public void Update_DefaultPlayer_LossAgainstStrong_RatingGoesDown()
        {
            var p  = Glicko2Player.CreateDefault();
            var p2 = Glicko2Calculator.Update(p, new[] { new Glicko2Result(1700.0, 30.0, 0.0) });
            Assert.Less(p2.Rating, p.Rating);
        }
    }
}
