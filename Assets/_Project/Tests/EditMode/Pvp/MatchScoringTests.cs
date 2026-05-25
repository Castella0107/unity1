using NUnit.Framework;
using Domain.Pvp;
using System.Collections.Generic;
using System.Linq;

namespace Domain.Pvp.Tests
{
    /// <summary><see cref="MatchScoring"/> のユニットテスト。</summary>
    [TestFixture]
    public class MatchScoringTests
    {
        static SectorPair Pair(string song, int idx, int a, int b) => new SectorPair(song, idx, a, b);

        [Test]
        public void Score_AllAWins_GivesAFullPoints()
        {
            var sectors = new[]
            {
                Pair("s1", 0, 100, 50),
                Pair("s1", 1, 100, 50),
                Pair("s1", 2, 100, 50),
            };
            var r = MatchScoring.Score(sectors);
            Assert.AreEqual(3.0, r.TotalPointsA, 1e-9);
            Assert.AreEqual(0.0, r.TotalPointsB, 1e-9);
            Assert.AreEqual(MatchOutcomeKind.AWins, r.Kind);
            Assert.AreEqual(3, r.Sectors.Count);
        }

        [Test]
        public void Score_AllDraws_GivesHalfHalf()
        {
            var sectors = new[]
            {
                Pair("s1", 0, 100, 100),
                Pair("s1", 1, 200, 200),
            };
            var r = MatchScoring.Score(sectors);
            Assert.AreEqual(1.0, r.TotalPointsA, 1e-9);
            Assert.AreEqual(1.0, r.TotalPointsB, 1e-9);
            Assert.AreEqual(MatchOutcomeKind.Draw, r.Kind);
        }

        [Test]
        public void Score_MixedSectors_AccumulatesCorrectly()
        {
            // 3 sectors: A wins, B wins, draw
            var sectors = new[]
            {
                Pair("s1", 0, 100,  50),    // A
                Pair("s1", 1,  30, 100),    // B
                Pair("s1", 2,  80,  80),    // Draw
            };
            var r = MatchScoring.Score(sectors);
            Assert.AreEqual(1.5, r.TotalPointsA, 1e-9);
            Assert.AreEqual(1.5, r.TotalPointsB, 1e-9);
            Assert.AreEqual(MatchOutcomeKind.Draw, r.Kind);

            Assert.AreEqual(SectorOutcome.AWins, r.Sectors[0].Outcome);
            Assert.AreEqual(SectorOutcome.BWins, r.Sectors[1].Outcome);
            Assert.AreEqual(SectorOutcome.Draw,  r.Sectors[2].Outcome);
        }

        [Test]
        public void Score_FullMatch3Songs5Sectors_15PointsMax()
        {
            // 3曲 × 5セクター = 15 sector で A が全勝
            var sectors = new List<SectorPair>();
            for (int song = 0; song < 3; song++)
                for (int idx = 0; idx < 5; idx++)
                    sectors.Add(Pair("song_" + song, idx, 1000, 500));

            var r = MatchScoring.Score(sectors);
            Assert.AreEqual(15, r.Sectors.Count);
            Assert.AreEqual(15.0, r.TotalPointsA, 1e-9);
            Assert.AreEqual( 0.0, r.TotalPointsB, 1e-9);
            Assert.AreEqual(MatchOutcomeKind.AWins, r.Kind);
        }

        [Test]
        public void ToGlicko2ResultsForA_Yields15ResultsPerMatch()
        {
            var sectors = new List<SectorPair>();
            for (int i = 0; i < 15; i++) sectors.Add(Pair("s", i, 100, 50));
            var r = MatchScoring.Score(sectors);

            var glickoResults = r.ToGlicko2ResultsForA(opponentRating: 1500, opponentRD: 200).ToList();
            Assert.AreEqual(15, glickoResults.Count);
            Assert.IsTrue(glickoResults.TrueForAll(g => g.Score == 1.0));
        }
    }
}
