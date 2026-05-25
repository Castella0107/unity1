using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary><see cref="JudgmentRunner"/> のユニットテスト。</summary>
public class JudgmentRunnerTests
{
    // ── 全 Perfect+ : Tap ────────────────────────────────────────────────────

    [Test]
    public void AllPerfectPlus_TapOnly_GivesExactly1000000()
    {
        var chart = new ChartBuilder()
            .WithBpm(120)
            .AddTap(LaneRef.Lane0, 1000)
            .AddTap(LaneRef.Lane1, 1500)
            .AddTap(LaneRef.Lane2, 2000)
            .AddTap(LaneRef.Lane3, 2500)
            .AddTap(LaneRef.FxL,   3000)
            .AddTap(LaneRef.FxR,   3500)
            .Build();

        var snap = new JudgmentRunner().Run(chart, ReplayBuilder.AllPerfectPlus(chart));

        Assert.AreEqual(1_000_000, snap.CurrentScore);
        Assert.AreEqual(6, snap.PerfectPlusCount);
        Assert.AreEqual(0, snap.MissCount);
        Assert.AreEqual(6, snap.MaxCombo);
    }

    [Test]
    public void AllPerfectPlus_LargeChart_GivesExactly1000000()
    {
        var builder = new ChartBuilder().WithBpm(150);
        for (int i = 0; i < 100; i++)
            builder.AddTap((LaneRef)(i % 4), 1000 + i * 200.0);
        var chart = builder.Build();

        var snap = new JudgmentRunner().Run(chart, ReplayBuilder.AllPerfectPlus(chart));

        Assert.AreEqual(1_000_000, snap.CurrentScore);
        Assert.AreEqual(100, snap.PerfectPlusCount);
        Assert.AreEqual(100, snap.MaxCombo);
        Assert.AreEqual(0,   snap.MissCount);
    }

    // ── 全 Perfect+ : Hold ───────────────────────────────────────────────────

    [Test]
    public void AllPerfectPlus_HoldOnly_GivesExactly1000000()
    {
        var chart = new ChartBuilder()
            .WithBpm(120)
            .AddHold(LaneRef.Lane0, 1000, 500)
            .AddHold(LaneRef.Lane1, 2000, 500)
            .Build();

        var snap = new JudgmentRunner().Run(chart, ReplayBuilder.AllPerfectPlus(chart));

        Assert.AreEqual(1_000_000, snap.CurrentScore);
        Assert.AreEqual(0, snap.MissCount);
    }

    [Test]
    public void AllPerfectPlus_MixedTapAndHold_GivesExactly1000000()
    {
        var chart = new ChartBuilder()
            .WithBpm(120)
            .AddTap(LaneRef.Lane0,  1000)
            .AddHold(LaneRef.Lane1, 1500, 400)
            .AddTap(LaneRef.Lane2,  2500)
            .Build();

        var snap = new JudgmentRunner().Run(chart, ReplayBuilder.AllPerfectPlus(chart));

        Assert.AreEqual(1_000_000, snap.CurrentScore);
        Assert.AreEqual(0, snap.MissCount);
    }

    // ── 全 Miss ───────────────────────────────────────────────────────────────

    [Test]
    public void AllMiss_TapOnly_GivesZero()
    {
        var chart = new ChartBuilder()
            .WithBpm(120)
            .AddTap(LaneRef.Lane0, 1000)
            .AddTap(LaneRef.Lane1, 1500)
            .AddTap(LaneRef.Lane2, 2000)
            .Build();

        var snap = new JudgmentRunner().Run(chart, ReplayBuilder.AllMiss());

        Assert.AreEqual(0, snap.CurrentScore);
        Assert.AreEqual(3, snap.MissCount);
        Assert.AreEqual(0, snap.MaxCombo);
    }

    [Test]
    public void AllMiss_HoldOnly_HeadCountedAsMiss()
    {
        var chart = new ChartBuilder()
            .WithBpm(120)
            .AddHold(LaneRef.Lane0, 1000, 500)
            .Build();

        var snap = new JudgmentRunner().Run(chart, ReplayBuilder.AllMiss());

        Assert.AreEqual(0,   snap.CurrentScore);
        Assert.Greater(snap.MissCount, 0);
    }

    // ── 判定種別 ─────────────────────────────────────────────────────────────

    [Test]
    public void Offset20ms_GivesPerfect()
    {
        // |20| ≤ 33 → Perfect (not PerfectPlus since |20| > 16)
        var chart = new ChartBuilder()
            .WithBpm(120)
            .AddTap(LaneRef.Lane0, 1000)
            .AddTap(LaneRef.Lane1, 1500)
            .Build();

        var snap = new JudgmentRunner().Run(chart, ReplayBuilder.AllWithOffset(chart, 20));

        Assert.AreEqual(2, snap.PerfectCount);
        Assert.AreEqual(0, snap.PerfectPlusCount);
        Assert.AreEqual(0, snap.MissCount);
    }

    [Test]
    public void Offset40ms_GivesGreat()
    {
        // |40| > 33, |40| ≤ 50 → Great
        var chart = new ChartBuilder()
            .WithBpm(120)
            .AddTap(LaneRef.Lane0, 1000)
            .AddTap(LaneRef.Lane1, 1500)
            .Build();

        var snap = new JudgmentRunner().Run(chart, ReplayBuilder.AllWithOffset(chart, 40));

        Assert.AreEqual(2, snap.GreatCount);
        Assert.AreEqual(0, snap.MissCount);
    }

    [Test]
    public void Offset70ms_GivesGood()
    {
        // |70| > 50, |70| ≤ 83 → Good
        var chart = new ChartBuilder()
            .WithBpm(120)
            .AddTap(LaneRef.Lane0, 1000)
            .AddTap(LaneRef.Lane1, 1500)
            .Build();

        var snap = new JudgmentRunner().Run(chart, ReplayBuilder.AllWithOffset(chart, 70));

        Assert.AreEqual(2, snap.GoodCount);
        Assert.AreEqual(0, snap.MissCount);
    }

    [Test]
    public void Offset100ms_GivesMiss()
    {
        // |100| > 83 → outside Good window → not matched → auto-miss
        var chart = new ChartBuilder()
            .WithBpm(120)
            .AddTap(LaneRef.Lane0, 1000)
            .Build();

        var snap = new JudgmentRunner().Run(chart, ReplayBuilder.AllWithOffset(chart, 100));

        Assert.AreEqual(1, snap.MissCount);
    }

    // ── Fast / Late ───────────────────────────────────────────────────────────

    [Test]
    public void NegativeOffset_CountsAsFast()
    {
        var chart = new ChartBuilder()
            .WithBpm(120)
            .AddTap(LaneRef.Lane0, 1000)
            .AddTap(LaneRef.Lane1, 1500)
            .Build();

        var snap = new JudgmentRunner().Run(chart, ReplayBuilder.AllWithOffset(chart, -20));

        Assert.AreEqual(2, snap.FastCount);
        Assert.AreEqual(0, snap.LateCount);
    }

    [Test]
    public void PositiveOffset_CountsAsLate()
    {
        var chart = new ChartBuilder()
            .WithBpm(120)
            .AddTap(LaneRef.Lane0, 1000)
            .AddTap(LaneRef.Lane1, 1500)
            .Build();

        var snap = new JudgmentRunner().Run(chart, ReplayBuilder.AllWithOffset(chart, 20));

        Assert.AreEqual(0, snap.FastCount);
        Assert.AreEqual(2, snap.LateCount);
    }

    // ── セクタースコア ────────────────────────────────────────────────────────

    [Test]
    public void SectorScores_SumEqualsCurrentScore()
    {
        var builder = new ChartBuilder().WithBpm(120);
        for (int i = 0; i < 50; i++)
            builder.AddTap((LaneRef)(i % 4), 1000 + i * 100.0);
        var chart = builder.Build();

        var sectorEnds = new int[] { 1500, 2500, 3500, 4500 };
        var snap = new JudgmentRunner().Run(
            chart, ReplayBuilder.AllPerfectPlus(chart), sectorEnds);

        Assert.AreEqual(snap.CurrentScore, snap.SectorScores.Sum());
    }

    // ── 完全往復テスト ─────────────────────────────────────────────────────────

    [Test]
    public void FullRoundTrip_EncodeDecodeRun_GivesConsistentScore()
    {
        var chart = new ChartBuilder()
            .WithBpm(120)
            .AddTap(LaneRef.Lane0, 1000)
            .AddTap(LaneRef.Lane1, 1500)
            .AddTap(LaneRef.Lane2, 2000)
            .Build();

        var originalEvents = ReplayBuilder.AllPerfectPlus(chart);

        var replayData = new ReplayData
        {
            Header   = new ReplayHeader { PlayerUuid = new byte[16] },
            Metadata = new ReplayMetadata
            {
                SongId                = "test",
                Difficulty            = "extra",
                ChartHash             = new byte[32],
                Bpm                   = 120,
                Modifiers             = new string[0],
                JudgmentEngineVersion = "1.0.0",
            },
            Result = new ReplayResult
            {
                RawScore = 1_000_000, EffectiveScore = 1_000_000,
                Rank = "S+", TotalNotes = 3,
            },
            InputEvents = originalEvents,
        };

        var encoded = ReplayEncoder.Encode(replayData);
        var decoded = ReplayDecoder.Decode(encoded);
        var snap    = new JudgmentRunner().Run(chart, decoded.InputEvents);

        Assert.AreEqual(1_000_000, snap.CurrentScore);
        Assert.AreEqual(3, snap.PerfectPlusCount);
        Assert.AreEqual(0, snap.MissCount);
    }

    // ── 決定性 ────────────────────────────────────────────────────────────────

    [Test]
    public void Run_IsDeterministic()
    {
        var chart = new ChartBuilder()
            .WithBpm(150)
            .AddTap(LaneRef.Lane0,  1000)
            .AddTap(LaneRef.Lane1,  1100)
            .AddHold(LaneRef.Lane2, 1500, 800)
            .AddTap(LaneRef.Lane3,  2500)
            .Build();

        var replay  = ReplayBuilder.AllPerfectPlus(chart);
        var runner  = new JudgmentRunner();
        var snap1   = runner.Run(chart, replay);
        var snap2   = runner.Run(chart, replay);

        Assert.AreEqual(snap1.CurrentScore,    snap2.CurrentScore);
        Assert.AreEqual(snap1.PerfectPlusCount, snap2.PerfectPlusCount);
        Assert.AreEqual(snap1.MaxCombo,         snap2.MaxCombo);
        CollectionAssert.AreEqual(snap1.SectorScores, snap2.SectorScores);
    }

    // ── 空入力 / ヌル安全 ─────────────────────────────────────────────────────

    [Test]
    public void NullReplayEvents_TreatedAsAllMiss()
    {
        var chart = new ChartBuilder()
            .WithBpm(120)
            .AddTap(LaneRef.Lane0, 1000)
            .Build();

        var snap = new JudgmentRunner().Run(chart, null);

        Assert.AreEqual(0, snap.CurrentScore);
        Assert.AreEqual(1, snap.MissCount);
    }

    [Test]
    public void EmptyReplayEvents_TreatedAsAllMiss()
    {
        var chart = new ChartBuilder()
            .WithBpm(120)
            .AddTap(LaneRef.Lane0, 1000)
            .Build();

        var snap = new JudgmentRunner().Run(chart, new List<ReplayInputEvent>());

        Assert.AreEqual(0, snap.CurrentScore);
        Assert.AreEqual(1, snap.MissCount);
    }

    // ── 現実的な混在譜面 ──────────────────────────────────────────────────────

    [Test]
    public void RealisticChart_HoldHeavy_AllPerfectPlusGives1000000()
    {
        var chart = new ChartBuilder()
            .WithBpm(150)
            .AddTap(LaneRef.Lane0,   1000)
            .AddHold(LaneRef.Lane1,  1500,  800)
            .AddTap(LaneRef.Lane2,   2500)
            .AddHold(LaneRef.Lane3,  3000, 1200)
            .AddTap(LaneRef.FxL,     4500)
            .AddHold(LaneRef.FxR,    5000,  600)
            .Build();

        var snap = new JudgmentRunner().Run(chart, ReplayBuilder.AllPerfectPlus(chart));

        Assert.AreEqual(1_000_000, snap.CurrentScore);
        Assert.AreEqual(0, snap.MissCount);
    }
}
