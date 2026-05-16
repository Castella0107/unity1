using System.Collections.Generic;
using System.Linq;

// Unity-independent. No UnityEngine references allowed in this assembly.
// Thin wrapper: delegates all judgment logic to JudgmentEngine.
// Kept as an instance class so existing tests (new JudgmentRunner().Run(...)) compile unchanged.
/// <summary>
/// <see cref="JudgmentEngine"/> への薄いラッパー。リプレイイベントをチャートに対して再生し、
/// <see cref="PlayProgressSnapshot"/> を返す。既存テストとの互換性のためインスタンスクラスとして維持される。
/// </summary>
public class JudgmentRunner
{
    // Primary API — used by the 18 existing EditMode tests.
    public PlayProgressSnapshot Run(
        ChartData                      chart,
        IReadOnlyList<ReplayInputEvent> replayEvents,
        int[]                          sectorEnds  = null,
        Judgment                       comboBorder = Judgment.Good)
    {
        var bpm    = new BpmTimeline(chart.Events ?? new List<TempoEvent>());
        var src    = new ChartDataNoteSource(chart);
        var engine = new JudgmentEngine(chart, src, bpm, sectorEnds, comboBorder);

        if (replayEvents != null)
        {
            double absTime = 0;
            foreach (var e in replayEvents)
            {
                absTime += e.DeltaMsFromPrev;
                engine.ProcessTime(absTime);
                if (e.Action == 0) engine.ProcessLaneDown((LaneRef)e.Lane, absTime);
                else               engine.ProcessLaneUp  ((LaneRef)e.Lane, absTime);
            }
        }

        engine.ProcessTime(ComputeEndTime(chart));
        return engine.BuildResult();
    }

    // Full-replay API — includes sector ends extracted from SongMetadata.
    public PlayProgressSnapshot Run(ChartData chart, SongMetadata meta, ReplayData replay)
    {
        int[] sectorEnds = meta?.Sectors != null
            ? meta.Sectors.Take(4).Select(s => s.EndMs).ToArray()
            : null;

        return Run(chart, replay.InputEvents, sectorEnds);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    static double ComputeEndTime(ChartData chart)
    {
        double max = 0;
        foreach (var n in chart.Notes)
        {
            double end = (n.Type == NoteType.Hold || n.Type == NoteType.FxHold)
                ? n.TimeMs + n.DurationMs
                : n.TimeMs;
            if (end > max) max = end;
        }
        return max + JudgmentWindow.GoodMs + 200.0;
    }
}
