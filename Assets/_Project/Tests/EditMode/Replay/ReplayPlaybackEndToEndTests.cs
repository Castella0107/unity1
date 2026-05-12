using System.Collections.Generic;
using NUnit.Framework;

// Verifies that JudgmentRunner (headless) and ReplayInputSource+JudgmentEngine
// (the path taken during actual replay playback) produce identical scores.
public class ReplayPlaybackEndToEndTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    static ReplayData BuildReplayData(ChartData chart)
    {
        var events = ReplayBuilder.AllPerfectPlus(chart);
        return new ReplayData
        {
            Header      = new ReplayHeader { PlayerUuid = new byte[16] },
            Metadata    = new ReplayMetadata
            {
                SongId = "test", Difficulty = "extra",
                ChartHash = new byte[32], Bpm = 120,
                Modifiers = new string[0], JudgmentEngineVersion = "1.0.0",
            },
            Result      = new ReplayResult { Rank = "S+", TotalNotes = chart.TotalNotes },
            InputEvents = events,
        };
    }

    static PlayProgressSnapshot RunViaEngine(ChartData chart, ReplayData replay)
    {
        var bpm    = new BpmTimeline(chart.Events ?? new List<TempoEvent>());
        var src    = new ChartDataNoteSource(chart);
        var engine = new JudgmentEngine(chart, src, bpm);

        // Wire ReplayInputSource events directly into engine (mirrors JudgmentSystem.HandleLaneDown/Up)
        var inputSrc = new ReplayInputSource(replay);
        inputSrc.OnLaneDown += (lane, t) => engine.ProcessLaneDown(lane, t);
        inputSrc.OnLaneUp   += (lane, t) => engine.ProcessLaneUp(lane, t);

        // Advance through all events + flush auto-miss
        double maxTime = 0;
        foreach (var n in chart.Notes)
        {
            double end = (n.Type == NoteType.Hold || n.Type == NoteType.FxHold)
                ? n.TimeMs + n.DurationMs : n.TimeMs;
            if (end > maxTime) maxTime = end;
        }
        double endTime = maxTime + JudgmentWindow.GoodMs + 200.0;

        inputSrc.Advance(endTime);
        engine.ProcessTime(endTime);

        return engine.BuildResult();
    }

    static PlayProgressSnapshot RunViaRunner(ChartData chart, ReplayData replay)
        => new JudgmentRunner().Run(chart, replay.InputEvents);

    static void AssertEqual(PlayProgressSnapshot a, PlayProgressSnapshot b)
    {
        Assert.AreEqual(b.CurrentScore,     a.CurrentScore,     "Score");
        Assert.AreEqual(b.PerfectPlusCount, a.PerfectPlusCount, "PerfectPlus");
        Assert.AreEqual(b.PerfectCount,     a.PerfectCount,     "Perfect");
        Assert.AreEqual(b.GreatCount,       a.GreatCount,       "Great");
        Assert.AreEqual(b.GoodCount,        a.GoodCount,        "Good");
        Assert.AreEqual(b.MissCount,        a.MissCount,        "Miss");
        Assert.AreEqual(b.MaxCombo,         a.MaxCombo,         "MaxCombo");
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Test]
    public void AllPerfectPlus_TapOnly_EngineMatchesRunner()
    {
        var chart  = new ChartBuilder().WithBpm(120)
            .AddTap(LaneRef.Lane0, 1000).AddTap(LaneRef.Lane1, 1500)
            .AddTap(LaneRef.Lane2, 2000).AddTap(LaneRef.Lane3, 2500)
            .Build();
        var replay = BuildReplayData(chart);

        AssertEqual(RunViaEngine(chart, replay), RunViaRunner(chart, replay));
        Assert.AreEqual(1_000_000, RunViaRunner(chart, replay).CurrentScore);
    }

    [Test]
    public void AllPerfectPlus_Mixed_EngineMatchesRunner()
    {
        var chart  = new ChartBuilder().WithBpm(120)
            .AddTap(LaneRef.Lane0,  1000)
            .AddHold(LaneRef.Lane1, 1500, 600)
            .AddTap(LaneRef.Lane2,  2500)
            .AddHold(LaneRef.FxL,   3000, 400)
            .Build();
        var replay = BuildReplayData(chart);

        AssertEqual(RunViaEngine(chart, replay), RunViaRunner(chart, replay));
    }

    [Test]
    public void AllMiss_EngineMatchesRunner()
    {
        var chart  = new ChartBuilder().WithBpm(120)
            .AddTap(LaneRef.Lane0, 1000).AddTap(LaneRef.Lane1, 1500).Build();
        var replay = new ReplayData
        {
            Header      = new ReplayHeader { PlayerUuid = new byte[16] },
            Metadata    = new ReplayMetadata { SongId = "t", Difficulty = "easy",
                                               ChartHash = new byte[32], Modifiers = new string[0] },
            Result      = new ReplayResult { Rank = "D" },
            InputEvents = new List<ReplayInputEvent>(),
        };

        AssertEqual(RunViaEngine(chart, replay), RunViaRunner(chart, replay));
        Assert.AreEqual(2, RunViaRunner(chart, replay).MissCount);
    }

    [Test]
    public void EncodeDecodeRoundTrip_EngineMatchesRunner()
    {
        var chart  = new ChartBuilder().WithBpm(150)
            .AddTap(LaneRef.Lane0, 1000).AddHold(LaneRef.Lane1, 1500, 500)
            .AddTap(LaneRef.Lane2, 2500).Build();
        var replay = BuildReplayData(chart);

        var encoded  = ReplayEncoder.Encode(replay);
        var decoded  = ReplayDecoder.Decode(encoded);

        AssertEqual(RunViaEngine(chart, decoded), RunViaRunner(chart, decoded));
    }

    [Test]
    public void ReplayInputSource_EventTimestamps_MatchOriginalDeltas()
    {
        // Verify that delta decoding in ReplayInputSource is identical to JudgmentRunner's decoding.
        var events = new List<ReplayInputEvent>
        {
            new ReplayInputEvent { DeltaMsFromPrev = 1000, Lane = 0, Action = 0 },
            new ReplayInputEvent { DeltaMsFromPrev =   30, Lane = 0, Action = 1 },
            new ReplayInputEvent { DeltaMsFromPrev =  470, Lane = 1, Action = 0 },
        };

        var firedTimes = new List<double>();
        var replay = new ReplayData
        {
            Header      = new ReplayHeader { PlayerUuid = new byte[16] },
            Metadata    = new ReplayMetadata { SongId = "t", Difficulty = "easy",
                                               ChartHash = new byte[32], Modifiers = new string[0] },
            Result      = new ReplayResult { Rank = "D" },
            InputEvents = events,
        };

        var src = new ReplayInputSource(replay);
        src.OnLaneDown += (l, t) => firedTimes.Add(t);
        src.OnLaneUp   += (l, t) => firedTimes.Add(t);
        src.Advance(99999);

        Assert.AreEqual(3, firedTimes.Count);
        Assert.AreEqual(1000.0, firedTimes[0], 0.001);
        Assert.AreEqual(1030.0, firedTimes[1], 0.001);
        Assert.AreEqual(1500.0, firedTimes[2], 0.001);
    }
}
