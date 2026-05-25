using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

// JudgmentEngine 単体テスト。
// JudgmentRunnerTests と同型 — Engine が同じ結果を返すことを保証する。
/// <summary><see cref="JudgmentEngine"/> のユニットテスト。</summary>
public class JudgmentEngineTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    static JudgmentEngine BuildEngine(ChartData chart, Judgment comboBorder = Judgment.Good)
    {
        var bpm = new BpmTimeline(chart.Events ?? new List<TempoEvent>());
        var src = new ChartDataNoteSource(chart);
        return new JudgmentEngine(chart, src, bpm, null, comboBorder);
    }

    static void FeedEvents(JudgmentEngine engine, IReadOnlyList<ReplayInputEvent> events)
    {
        if (events == null) return;
        double absTime = 0;
        foreach (var e in events)
        {
            absTime += e.DeltaMsFromPrev;
            engine.ProcessTime(absTime);
            if (e.Action == 0) engine.ProcessLaneDown((LaneRef)e.Lane, absTime);
            else               engine.ProcessLaneUp  ((LaneRef)e.Lane, absTime);
        }
    }

    static PlayProgressSnapshot RunToEnd(ChartData chart, IReadOnlyList<ReplayInputEvent> events,
                                          Judgment comboBorder = Judgment.Good)
    {
        var engine = BuildEngine(chart, comboBorder);
        FeedEvents(engine, events);

        double max = 0;
        foreach (var n in chart.Notes)
        {
            double end = (n.Type == NoteType.Hold || n.Type == NoteType.FxHold)
                ? n.TimeMs + n.DurationMs : n.TimeMs;
            if (end > max) max = end;
        }
        engine.ProcessTime(max + JudgmentWindow.GoodMs + 200.0);
        return engine.BuildResult();
    }

    // ── 全 Perfect+ : Tap ────────────────────────────────────────────────────

    [Test]
    public void AllPerfectPlus_TapOnly_GivesExactly1000000()
    {
        var chart = new ChartBuilder()
            .WithBpm(120)
            .AddTap(LaneRef.Lane0, 1000).AddTap(LaneRef.Lane1, 1500)
            .AddTap(LaneRef.Lane2, 2000).AddTap(LaneRef.Lane3, 2500)
            .AddTap(LaneRef.FxL,   3000).AddTap(LaneRef.FxR,   3500)
            .Build();

        var snap = RunToEnd(chart, ReplayBuilder.AllPerfectPlus(chart));

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

        var snap = RunToEnd(chart, ReplayBuilder.AllPerfectPlus(chart));

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

        var snap = RunToEnd(chart, ReplayBuilder.AllPerfectPlus(chart));

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

        var snap = RunToEnd(chart, ReplayBuilder.AllPerfectPlus(chart));

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

        var snap = RunToEnd(chart, ReplayBuilder.AllMiss());

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

        var snap = RunToEnd(chart, ReplayBuilder.AllMiss());

        Assert.AreEqual(0, snap.CurrentScore);
        Assert.Greater(snap.MissCount, 0);
    }

    // ── 判定種別 ─────────────────────────────────────────────────────────────

    [Test]
    public void Offset20ms_GivesPerfect()
    {
        var chart = new ChartBuilder().WithBpm(120)
            .AddTap(LaneRef.Lane0, 1000).AddTap(LaneRef.Lane1, 1500).Build();

        var snap = RunToEnd(chart, ReplayBuilder.AllWithOffset(chart, 20));

        Assert.AreEqual(2, snap.PerfectCount);
        Assert.AreEqual(0, snap.PerfectPlusCount);
        Assert.AreEqual(0, snap.MissCount);
    }

    [Test]
    public void Offset40ms_GivesGreat()
    {
        var chart = new ChartBuilder().WithBpm(120)
            .AddTap(LaneRef.Lane0, 1000).AddTap(LaneRef.Lane1, 1500).Build();

        var snap = RunToEnd(chart, ReplayBuilder.AllWithOffset(chart, 40));

        Assert.AreEqual(2, snap.GreatCount);
        Assert.AreEqual(0, snap.MissCount);
    }

    [Test]
    public void Offset70ms_GivesGood()
    {
        var chart = new ChartBuilder().WithBpm(120)
            .AddTap(LaneRef.Lane0, 1000).AddTap(LaneRef.Lane1, 1500).Build();

        var snap = RunToEnd(chart, ReplayBuilder.AllWithOffset(chart, 70));

        Assert.AreEqual(2, snap.GoodCount);
        Assert.AreEqual(0, snap.MissCount);
    }

    [Test]
    public void Offset100ms_GivesMiss()
    {
        var chart = new ChartBuilder().WithBpm(120).AddTap(LaneRef.Lane0, 1000).Build();

        var snap = RunToEnd(chart, ReplayBuilder.AllWithOffset(chart, 100));

        Assert.AreEqual(1, snap.MissCount);
    }

    // ── Fast / Late ───────────────────────────────────────────────────────────

    [Test]
    public void NegativeOffset_CountsAsFast()
    {
        var chart = new ChartBuilder().WithBpm(120)
            .AddTap(LaneRef.Lane0, 1000).AddTap(LaneRef.Lane1, 1500).Build();

        var snap = RunToEnd(chart, ReplayBuilder.AllWithOffset(chart, -20));

        Assert.AreEqual(2, snap.FastCount);
        Assert.AreEqual(0, snap.LateCount);
    }

    [Test]
    public void PositiveOffset_CountsAsLate()
    {
        var chart = new ChartBuilder().WithBpm(120)
            .AddTap(LaneRef.Lane0, 1000).AddTap(LaneRef.Lane1, 1500).Build();

        var snap = RunToEnd(chart, ReplayBuilder.AllWithOffset(chart, 20));

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

        var engine = BuildEngine(chart);
        // Inject sector ends via new JudgmentEngine overload
        var bpm  = new BpmTimeline(chart.Events ?? new List<TempoEvent>());
        var src  = new ChartDataNoteSource(chart);
        var eng2 = new JudgmentEngine(chart, src, bpm, new int[] { 1500, 2500, 3500, 4500 });

        FeedEvents(eng2, ReplayBuilder.AllPerfectPlus(chart));
        eng2.ProcessTime(6000 + JudgmentWindow.GoodMs + 200);
        var snap = eng2.BuildResult();

        Assert.AreEqual(snap.CurrentScore, snap.SectorScores.Sum());
    }

    // ── 決定性 ────────────────────────────────────────────────────────────────

    [Test]
    public void Run_IsDeterministic()
    {
        var chart = new ChartBuilder().WithBpm(150)
            .AddTap(LaneRef.Lane0,  1000).AddTap(LaneRef.Lane1,  1100)
            .AddHold(LaneRef.Lane2, 1500, 800).AddTap(LaneRef.Lane3, 2500)
            .Build();

        var replay = ReplayBuilder.AllPerfectPlus(chart);
        var snap1  = RunToEnd(chart, replay);
        var snap2  = RunToEnd(chart, replay);

        Assert.AreEqual(snap1.CurrentScore,     snap2.CurrentScore);
        Assert.AreEqual(snap1.PerfectPlusCount, snap2.PerfectPlusCount);
        Assert.AreEqual(snap1.MaxCombo,         snap2.MaxCombo);
        CollectionAssert.AreEqual(snap1.SectorScores, snap2.SectorScores);
    }

    // ── null / 空入力 ─────────────────────────────────────────────────────────

    [Test]
    public void NullEvents_TreatedAsAllMiss()
    {
        var chart = new ChartBuilder().WithBpm(120).AddTap(LaneRef.Lane0, 1000).Build();
        var snap  = RunToEnd(chart, null);
        Assert.AreEqual(0, snap.CurrentScore);
        Assert.AreEqual(1, snap.MissCount);
    }

    [Test]
    public void EmptyEvents_TreatedAsAllMiss()
    {
        var chart = new ChartBuilder().WithBpm(120).AddTap(LaneRef.Lane0, 1000).Build();
        var snap  = RunToEnd(chart, new List<ReplayInputEvent>());
        Assert.AreEqual(0, snap.CurrentScore);
        Assert.AreEqual(1, snap.MissCount);
    }

    // ── OnJudgment イベント ───────────────────────────────────────────────────

    [Test]
    public void OnJudgment_FiredForEachNote()
    {
        var chart = new ChartBuilder().WithBpm(120)
            .AddTap(LaneRef.Lane0, 1000).AddTap(LaneRef.Lane1, 1500).Build();

        var engine = BuildEngine(chart);
        var events = new List<JudgmentEvent>();
        engine.OnJudgment += ev => events.Add(ev);

        FeedEvents(engine, ReplayBuilder.AllPerfectPlus(chart));
        engine.ProcessTime(2000 + JudgmentWindow.GoodMs + 200);

        Assert.AreEqual(2, events.Count);
        Assert.IsTrue(events.All(e => e.Kind == NoteKind.Tap));
        Assert.IsTrue(events.All(e => e.Judgment == Judgment.PerfectPlus));
    }

    [Test]
    public void OnJudgment_ComboIncreasesPerEvent()
    {
        var chart = new ChartBuilder().WithBpm(120)
            .AddTap(LaneRef.Lane0, 1000).AddTap(LaneRef.Lane1, 1500).AddTap(LaneRef.Lane2, 2000).Build();

        var engine = BuildEngine(chart);
        var combos = new List<int>();
        engine.OnJudgment += ev => combos.Add(ev.Combo);

        FeedEvents(engine, ReplayBuilder.AllPerfectPlus(chart));
        engine.ProcessTime(3000 + JudgmentWindow.GoodMs + 200);

        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, combos);
    }

    // ── 現実的な Hold 重譜面 ──────────────────────────────────────────────────

    [Test]
    public void RealisticChart_HoldHeavy_AllPerfectPlusGives1000000()
    {
        var chart = new ChartBuilder().WithBpm(150)
            .AddTap(LaneRef.Lane0,   1000)
            .AddHold(LaneRef.Lane1,  1500,  800)
            .AddTap(LaneRef.Lane2,   2500)
            .AddHold(LaneRef.Lane3,  3000, 1200)
            .AddTap(LaneRef.FxL,     4500)
            .AddHold(LaneRef.FxR,    5000,  600)
            .Build();

        var snap = RunToEnd(chart, ReplayBuilder.AllPerfectPlus(chart));

        Assert.AreEqual(1_000_000, snap.CurrentScore);
        Assert.AreEqual(0, snap.MissCount);
    }
}
