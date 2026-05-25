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
        static SectorPair PairD(string song, int idx, int a, int b, string diff) => new SectorPair(song, idx, a, b, diff);

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

        // ── Difficulty multiplier (easy 0.75 / normal 0.80 / hard 0.90 / extra 1.00) ──

        [Test]
        public void DifficultyMultiplier_KnownValues()
        {
            Assert.AreEqual(0.75, MatchScoring.DifficultyMultiplier("easy"),   1e-9);
            Assert.AreEqual(0.80, MatchScoring.DifficultyMultiplier("normal"), 1e-9);
            Assert.AreEqual(0.90, MatchScoring.DifficultyMultiplier("hard"),   1e-9);
            Assert.AreEqual(1.00, MatchScoring.DifficultyMultiplier("extra"),  1e-9);
            Assert.AreEqual(1.00, MatchScoring.DifficultyMultiplier("EXTRA"),  1e-9);  // case-insensitive
            Assert.AreEqual(1.00, MatchScoring.DifficultyMultiplier(null),     1e-9);  // unknown → 1.0
        }

        [Test]
        public void Score_NoDifficulty_DefaultsToExtraMultiplier()
        {
            // 既存呼び出し (難易度なし) は ×1.0 のまま = 後方互換
            var r = MatchScoring.Score(new[] { Pair("s", 0, 100, 50) });
            Assert.AreEqual(1.0, r.TotalPointsA, 1e-9);
        }

        [Test]
        public void Score_DifficultyMultiplier_ScalesMatchPoints()
        {
            // A が hard 曲の 5 セクター全勝 → 5 × (1.0 × 0.9) = 4.5
            var sectors = new List<SectorPair>();
            for (int idx = 0; idx < 5; idx++) sectors.Add(PairD("hardSong", idx, 1000, 500, "hard"));
            var r = MatchScoring.Score(sectors);
            Assert.AreEqual(4.5, r.TotalPointsA, 1e-9);
            Assert.AreEqual(0.0, r.TotalPointsB, 1e-9);
            Assert.AreEqual(MatchOutcomeKind.AWins, r.Kind);
        }

        [Test]
        public void Score_EasyDraw_GivesWeightedHalf()
        {
            // easy 引分 → 各 0.5 × 0.75 = 0.375 (×1000 保存で 375 = 整数)
            var r = MatchScoring.Score(new[] { PairD("e", 0, 100, 100, "easy") });
            Assert.AreEqual(0.375, r.TotalPointsA, 1e-9);
            Assert.AreEqual(0.375, r.TotalPointsB, 1e-9);
            Assert.AreEqual(SectorOutcome.Draw, r.Sectors[0].Outcome);
        }

        [Test]
        public void Score_HarderSongOutweighsMoreEasierWins()
        {
            // A: hard 1 勝 (0.9) / B: easy 2 勝 (2 × 0.75 = 1.5) → 試合は B 勝ち (素の勝ち数は A1:B2)
            var sectors = new[]
            {
                PairD("hard", 0, 100,  50, "hard"),   // A → 0.9
                PairD("easy", 0,  50, 100, "easy"),   // B → 0.75
                PairD("easy", 1,  50, 100, "easy"),   // B → 0.75
            };
            var r = MatchScoring.Score(sectors);
            Assert.AreEqual(0.9, r.TotalPointsA, 1e-9);
            Assert.AreEqual(1.5, r.TotalPointsB, 1e-9);
            Assert.AreEqual(MatchOutcomeKind.BWins, r.Kind);
        }

        [Test]
        public void Score_DifficultyMultiplier_DoesNotBiasGlickoScore()
        {
            // hard で A 勝ち: 試合ポイントは重み付き (0.9) だが Glicko は素の勝敗 (A=1.0 / B=0.0)。
            var r = MatchScoring.Score(new[] { PairD("hard", 0, 100, 50, "hard") });
            Assert.AreEqual(0.9, r.Sectors[0].PointsA, 1e-9);   // 重み付き試合ポイント
            var ga = r.ToGlicko2ResultsForA(1500, 200).ToList();
            var gb = r.ToGlicko2ResultsForB(1500, 200).ToList();
            Assert.AreEqual(1.0, ga[0].Score, 1e-9);   // 素の勝ち
            Assert.AreEqual(0.0, gb[0].Score, 1e-9);   // 素の負け
            Assert.AreEqual(1.0, ga[0].Score + gb[0].Score, 1e-9);  // 相補性 (和=1) を維持
        }
    }
}
