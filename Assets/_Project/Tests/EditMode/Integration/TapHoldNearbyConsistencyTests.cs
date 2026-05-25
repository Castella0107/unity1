using System.Collections.Generic;
using NUnit.Framework;

// JudgmentEngine と JudgmentRunner の結果が一致することを確認する統合テスト。
// 特に「同一レーンに Tap と Hold が近接」する難しいケースを重点的に検証する。
/// <summary>JudgmentEngine と JudgmentRunner の結果一致を検証する統合テスト(同一レーンの Tap/Hold 近接ケース重点)。</summary>
public class TapHoldNearbyConsistencyTests
{
    static PlayProgressSnapshot RunEngine(ChartData chart, IReadOnlyList<ReplayInputEvent> events)
    {
        var bpm    = new BpmTimeline(chart.Events ?? new List<TempoEvent>());
        var src    = new ChartDataNoteSource(chart);
        var engine = new JudgmentEngine(chart, src, bpm);

        double absTime = 0;
        if (events != null)
        {
            foreach (var e in events)
            {
                absTime += e.DeltaMsFromPrev;
                engine.ProcessTime(absTime);
                if (e.Action == 0) engine.ProcessLaneDown((LaneRef)e.Lane, absTime);
                else               engine.ProcessLaneUp  ((LaneRef)e.Lane, absTime);
            }
        }

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

    static PlayProgressSnapshot RunRunner(ChartData chart, IReadOnlyList<ReplayInputEvent> events)
        => new JudgmentRunner().Run(chart, events);

    static void AssertConsistent(PlayProgressSnapshot engine, PlayProgressSnapshot runner)
    {
        Assert.AreEqual(runner.CurrentScore,     engine.CurrentScore,     "Score");
        Assert.AreEqual(runner.PerfectPlusCount, engine.PerfectPlusCount, "PerfectPlus");
        Assert.AreEqual(runner.PerfectCount,     engine.PerfectCount,     "Perfect");
        Assert.AreEqual(runner.GreatCount,       engine.GreatCount,       "Great");
        Assert.AreEqual(runner.GoodCount,        engine.GoodCount,        "Good");
        Assert.AreEqual(runner.MissCount,        engine.MissCount,        "Miss");
        Assert.AreEqual(runner.MaxCombo,         engine.MaxCombo,         "MaxCombo");
    }

    // ── 同一レーン Tap → Hold 近接 ────────────────────────────────────────────

    [Test]
    public void SameLane_TapThenHold_HitBoth_Consistent()
    {
        // Tap at 1000ms, Hold head at 1200ms — user hits both perfectly.
        var chart = new ChartBuilder().WithBpm(120)
            .AddTap(LaneRef.Lane0,  1000)
            .AddHold(LaneRef.Lane0, 1200, 400)
            .Build();

        var events = ReplayBuilder.AllPerfectPlus(chart);
        AssertConsistent(RunEngine(chart, events), RunRunner(chart, events));
    }

    [Test]
    public void SameLane_TapThenHold_MissTap_HitHold_Consistent()
    {
        // Tap at 1000ms missed; Hold head at 1200ms hit perfectly.
        var chart = new ChartBuilder().WithBpm(120)
            .AddTap(LaneRef.Lane0,  1000)
            .AddHold(LaneRef.Lane0, 1200, 400)
            .Build();

        // Only down+up for the hold, no input for the tap.
        var events = new List<ReplayInputEvent>
        {
            new ReplayInputEvent { DeltaMsFromPrev = 1200, Lane = 0, Action = 0 },
            new ReplayInputEvent { DeltaMsFromPrev = 400,  Lane = 0, Action = 1 },
        };

        AssertConsistent(RunEngine(chart, events), RunRunner(chart, events));
    }

    [Test]
    public void SameLane_TapThenHold_BothMissed_Consistent()
    {
        var chart = new ChartBuilder().WithBpm(120)
            .AddTap(LaneRef.Lane0,  1000)
            .AddHold(LaneRef.Lane0, 1200, 400)
            .Build();

        AssertConsistent(RunEngine(chart, null), RunRunner(chart, null));
    }

    // ── 複数レーン混在 ────────────────────────────────────────────────────────

    [Test]
    public void MultiLane_TapAndHold_AllPerfectPlus_Consistent()
    {
        var chart = new ChartBuilder().WithBpm(120)
            .AddTap(LaneRef.Lane0,  1000)
            .AddHold(LaneRef.Lane1, 1000, 600)
            .AddTap(LaneRef.Lane2,  1500)
            .AddHold(LaneRef.Lane3, 1800, 400)
            .Build();

        var events = ReplayBuilder.AllPerfectPlus(chart);
        AssertConsistent(RunEngine(chart, events), RunRunner(chart, events));
    }

    // ── FxレーンHold + 通常Tap同時 ────────────────────────────────────────────

    [Test]
    public void FxHoldWithTapSimultaneous_Consistent()
    {
        var chart = new ChartBuilder().WithBpm(120)
            .AddHold(LaneRef.FxL,  1000, 800)
            .AddTap(LaneRef.Lane0, 1000)
            .AddTap(LaneRef.Lane1, 1400)
            .Build();

        var events = ReplayBuilder.AllPerfectPlus(chart);
        AssertConsistent(RunEngine(chart, events), RunRunner(chart, events));
    }

    // ── エンジンとランナーのスコアが完全一致 (全パターン) ───────────────────────

    [Test]
    public void AllOffset20ms_EngineMatchesRunner()
    {
        var chart = new ChartBuilder().WithBpm(120)
            .AddTap(LaneRef.Lane0, 1000).AddTap(LaneRef.Lane1, 1500)
            .AddTap(LaneRef.Lane2, 2000).AddTap(LaneRef.Lane3, 2500)
            .Build();

        var events = ReplayBuilder.AllWithOffset(chart, 20);
        AssertConsistent(RunEngine(chart, events), RunRunner(chart, events));
    }

    [Test]
    public void EncodeDecodeRun_EngineMatchesRunner()
    {
        var chart = new ChartBuilder().WithBpm(120)
            .AddTap(LaneRef.Lane0, 1000).AddHold(LaneRef.Lane1, 1500, 500)
            .AddTap(LaneRef.Lane2, 2500).Build();

        var originalEvents = ReplayBuilder.AllPerfectPlus(chart);

        // Roundtrip through binary encoding
        var replayData = new ReplayData
        {
            Header      = new ReplayHeader { PlayerUuid = new byte[16] },
            Metadata    = new ReplayMetadata
            {
                SongId = "test", Difficulty = "extra", ChartHash = new byte[32],
                Bpm = 120, Modifiers = new string[0], JudgmentEngineVersion = "1.0.0",
            },
            Result      = new ReplayResult { RawScore = 0, EffectiveScore = 0, Rank = "S+", TotalNotes = 3 },
            InputEvents = new List<ReplayInputEvent>(originalEvents),
        };

        var encoded = ReplayEncoder.Encode(replayData);
        var decoded = ReplayDecoder.Decode(encoded);

        AssertConsistent(RunEngine(chart, decoded.InputEvents), RunRunner(chart, decoded.InputEvents));
    }
}
