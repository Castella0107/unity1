using System.Collections.Generic;

// Unity-independent. No UnityEngine references allowed in this assembly.
// Counts the total number of scoring events in a chart.
// This is the value that must be passed to ScoreCalculator as totalNotes so that
// all-perfect play yields exactly 1,000,000.
//
// Scoring events per note type:
//   Tap / FxTap : 1 (the tap itself)
//   Hold / FxHold : 1 (head) + N (ticks) + 1 (tail)
//
// Tick count uses the same BpmTimeline.GetTickIntervalMs loop as HoldJudgmentTracker.
/// <summary>
/// チャート内のスコアリングイベント総数を算出する静的クラス。
/// Tap/FxTap は1イベント、Hold/FxHold はヘッド＋ティック数＋テールの合計としてカウントする。
/// </summary>
public static class ScoringEventCounter
{
    public static int Count(IEnumerable<NoteData> notes, BpmTimeline bpm)
    {
        if (notes == null) return 0;
        if (bpm == null)   bpm = new BpmTimeline(new List<TempoEvent>());

        int total = 0;
        foreach (var n in notes)
        {
            switch (n.Type)
            {
                case NoteType.Tap:
                case NoteType.FxTap:
                    total += 1;
                    break;
                case NoteType.Hold:
                case NoteType.FxHold:
                    total += 2 + CountHoldTicks(n, bpm);   // head + tail + ticks
                    break;
            }
        }
        return total;
    }

    // Returns the number of interior ticks (same loop as HoldJudgmentTracker.ComputeTickTimes).
    public static int CountHoldTicks(NoteData hold, BpmTimeline bpm)
    {
        double cursor = hold.TimeMs;
        double end    = hold.TimeMs + hold.DurationMs;
        int    count  = 0;
        while (true)
        {
            cursor += bpm.GetTickIntervalMs(cursor);
            if (cursor >= end) break;
            count++;
        }
        return count;
    }
}
