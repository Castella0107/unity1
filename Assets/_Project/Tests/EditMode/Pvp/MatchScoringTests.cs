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
        static SectorPair PairT(string song, int idx, int a, int b, int tieA, int tieB) => new SectorPair(song, idx, a, b, null, tieA, tieB);

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

        // ── Tie-break (スコア同点 → Σ 2×PerfectPlus + Perfect が多い側の勝ち) ──

        [Test]
        public void Score_ScoreTie_BrokenByTieBreak_AWins()
        {
            // スコア同点だが A のタイブレーク値が上 → A が 1.0 を取り引分を解消する。
            var r = MatchScoring.Score(new[] { PairT("s", 0, 100, 100, tieA: 90, tieB: 80) });
            Assert.AreEqual(1.0, r.TotalPointsA, 1e-9);
            Assert.AreEqual(0.0, r.TotalPointsB, 1e-9);
            Assert.AreEqual(SectorOutcome.AWins, r.Sectors[0].Outcome);
            Assert.AreEqual(MatchOutcomeKind.AWins, r.Kind);
        }

        [Test]
        public void Score_ScoreTie_BrokenByTieBreak_BWins()
        {
            var r = MatchScoring.Score(new[] { PairT("s", 0, 100, 100, tieA: 70, tieB: 88) });
            Assert.AreEqual(0.0, r.TotalPointsA, 1e-9);
            Assert.AreEqual(1.0, r.TotalPointsB, 1e-9);
            Assert.AreEqual(SectorOutcome.BWins, r.Sectors[0].Outcome);
            // Glicko も素の勝敗で B 勝ち (タイブレーク後も相補性 1.0 を維持)
            var ga = r.ToGlicko2ResultsForA(1500, 200).ToList();
            var gb = r.ToGlicko2ResultsForB(1500, 200).ToList();
            Assert.AreEqual(0.0, ga[0].Score, 1e-9);
            Assert.AreEqual(1.0, gb[0].Score, 1e-9);
        }

        [Test]
        public void Score_ScoreTie_TieBreakAlsoEqual_StaysDraw()
        {
            // スコアもタイブレークも同点 → 真の引分 (0.5/0.5)。
            var r = MatchScoring.Score(new[] { PairT("s", 0, 100, 100, tieA: 80, tieB: 80) });
            Assert.AreEqual(0.5, r.TotalPointsA, 1e-9);
            Assert.AreEqual(0.5, r.TotalPointsB, 1e-9);
            Assert.AreEqual(SectorOutcome.Draw, r.Sectors[0].Outcome);
        }

        [Test]
        public void Score_TieBreakIgnoredWhenScoresDiffer()
        {
            // スコアで決着する場合はタイブレーク値を見ない (B のタイブレークが上でも A 勝ち)。
            var r = MatchScoring.Score(new[] { PairT("s", 0, 200, 100, tieA: 10, tieB: 999) });
            Assert.AreEqual(SectorOutcome.AWins, r.Sectors[0].Outcome);
        }

        [Test]
        public void Score_NoTieBreakData_ScoreTieStaysDraw_BackCompat()
        {
            // タイブレーク未指定 (0/0) の既存呼び出しは従来どおり引分 = 後方互換。
            var r = MatchScoring.Score(new[] { Pair("s", 0, 100, 100) });
            Assert.AreEqual(SectorOutcome.Draw, r.Sectors[0].Outcome);
        }

        // ── 難易度 = 実効スコアの重み(easy75 / normal80 / hard90 / extra100)。勝敗ptはフラット ──

        // A/B で異なる難易度を指定するヘルパ。
        static SectorPair PairAB(string song, int idx, int a, int b, string diffA, string diffB)
            => new SectorPair(song, idx, a, b, diffA, 0, 0, diffB);

        [Test]
        public void DifficultyMultiplier_KnownValues()
        {
            Assert.AreEqual(0.75, MatchScoring.DifficultyMultiplier("easy"),   1e-9);
            Assert.AreEqual(0.90, MatchScoring.DifficultyMultiplier("hard"),   1e-9);
            Assert.AreEqual(1.00, MatchScoring.DifficultyMultiplier("EXTRA"),  1e-9);  // case-insensitive
            Assert.AreEqual(75,  MatchScoring.MultiplierPercent("easy"));
            Assert.AreEqual(100, MatchScoring.MultiplierPercent("extra"));
            Assert.AreEqual(100, MatchScoring.MultiplierPercent(null));   // unknown → 100
        }

        [Test]
        public void Score_HigherDifficulty_WinsSectorAtEqualRawScore()
        {
            // ユーザー想定: 理論値 200000 どうしで A=easy / B=extra
            //   → 実効 150000 vs 200000 → B がセクター勝ち。ポイントはフラット (0 / 1)。
            var r = MatchScoring.Score(new[] { PairAB("s", 0, 200000, 200000, "easy", "extra") });
            Assert.AreEqual(SectorOutcome.BWins, r.Sectors[0].Outcome);
            Assert.AreEqual(0.0, r.TotalPointsA, 1e-9);
            Assert.AreEqual(1.0, r.TotalPointsB, 1e-9);   // 倍率を掛けないフラット
        }

        [Test]
        public void Score_LowerRawButHigherDifficulty_CanStillWin()
        {
            // A=extra 200000 (実効 200000) vs B=easy 250000 (実効 187500) → A 勝ち。
            var r = MatchScoring.Score(new[] { PairAB("s", 0, 200000, 250000, "extra", "easy") });
            Assert.AreEqual(SectorOutcome.AWins, r.Sectors[0].Outcome);
        }

        [Test]
        public void Score_PointsAreFlat_RegardlessOfDifficulty()
        {
            // 両者 hard・A が 5 セクター全勝(同倍率なので実効比較=生スコア比較)→ フラット 5.0。
            var sectors = new List<SectorPair>();
            for (int idx = 0; idx < 5; idx++) sectors.Add(PairD("hardSong", idx, 1000, 500, "hard"));
            var r = MatchScoring.Score(sectors);
            Assert.AreEqual(5.0, r.TotalPointsA, 1e-9);   // 倍率を掛けない
            Assert.AreEqual(0.0, r.TotalPointsB, 1e-9);
            Assert.AreEqual(MatchOutcomeKind.AWins, r.Kind);
        }

        [Test]
        public void Score_Draw_GivesFlatHalf()
        {
            var r = MatchScoring.Score(new[] { PairD("e", 0, 100, 100, "easy") });
            Assert.AreEqual(0.5, r.TotalPointsA, 1e-9);   // フラット 0.5(倍率なし)
            Assert.AreEqual(0.5, r.TotalPointsB, 1e-9);
            Assert.AreEqual(SectorOutcome.Draw, r.Sectors[0].Outcome);
        }

        [Test]
        public void Score_NoDifficulty_FlatWin()
        {
            var r = MatchScoring.Score(new[] { Pair("s", 0, 100, 50) });
            Assert.AreEqual(1.0, r.TotalPointsA, 1e-9);   // 難易度なし=×1.0、勝ちはフラット 1.0
            Assert.AreEqual(MatchOutcomeKind.AWins, r.Kind);
        }

        [Test]
        public void Score_FlatPoints_DoNotBiasGlicko()
        {
            // A 勝ち: ポイントはフラット 1.0、Glicko も素の勝敗 1.0/0.0(相補性維持)。
            var r = MatchScoring.Score(new[] { PairD("hard", 0, 100, 50, "hard") });
            Assert.AreEqual(1.0, r.Sectors[0].PointsA, 1e-9);
            var ga = r.ToGlicko2ResultsForA(1500, 200).ToList();
            var gb = r.ToGlicko2ResultsForB(1500, 200).ToList();
            Assert.AreEqual(1.0, ga[0].Score, 1e-9);
            Assert.AreEqual(0.0, gb[0].Score, 1e-9);
            Assert.AreEqual(1.0, ga[0].Score + gb[0].Score, 1e-9);
        }
    }
}
