using System.Collections.Generic;
using UnityEngine;

// Shared visual-stage setup used by both GamePlayController (live) and
// ReplayPlaybackController (replay). Call BindStageVisuals after chart/meta
// are loaded, and UnbindStageVisuals when the session ends.
public static class StageInitializer
{
    public static void BindStageVisuals(
        AudioConductor conductor,
        ChartData      chart,
        SongMetadata   meta,
        NoteScroller   scroller,
        GameHud        hud)
    {
        // Hide the persistent jacket-background canvas so it does not occlude
        // the 3D camera output. Must be the first call here.
        JacketBackgroundController.Instance?.SetCanvasEnabled(false);

        var bpmTimeline = new BpmTimeline(chart.Events ?? new List<TempoEvent>());
        BeatGridController.Instance?.BindGamePlay(conductor, bpmTimeline);

        scroller?.Initialize(chart);
        hud?.Initialize(meta, chart, isPvP: false);
    }

    public static void UnbindStageVisuals()
    {
        BeatGridController.Instance?.Unbind();
        JacketBackgroundController.Instance?.SetCanvasEnabled(true);
    }
}
