using System.Collections.Generic;

// Unity-independent. No UnityEngine references allowed in this assembly.
// Counts the total number of scoring events in a chart.
// This is the value that must be passed to ScoreCalculator as totalNotes so that
// all-perfect play yields exactly 1,000,000.
//
// Scoring events per note type:
//   Tap / FxTap : 1 (the tap itself)
//   Hold / FxHold : 1 (head) + N (body ticks) + 1 (tail)
//
// Tick count uses the same BpmTimeline.GetHoldTickIntervalMs loop as HoldJudgmentTracker
// (2 scoring events per measure of hold duration). The two MUST stay identical.
/// <summary>
/// チャート内のスコアリングイベント総数を算出する静的クラス。
/// Tap/FxTap は1イベント、Hold/FxHold はヘッド＋ティック数＋テールの合計としてカウントする。
/// </summary>
public static class ScoringEventCounter
{
    /// <summary>譜面の総スコアリングイベント数を数える。Tap/FxTap=1、Hold/FxHold=頭+ティック数+尾。ScoreCalculator の totalNotes に渡す値。</summary>
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

    /// <summary>ホールド内部のティック数(1 小節 2 ノーツ)を返す(HoldJudgmentTracker のティック計算と同一ロジック)。</summary>
    public static int CountHoldTicks(NoteData hold, BpmTimeline bpm)
    {
        double cursor = hold.TimeMs;
        double end    = hold.TimeMs + hold.DurationMs;
        int    count  = 0;
        while (true)
        {
            cursor += bpm.GetHoldTickIntervalMs(cursor);
            if (cursor >= end - BpmTimeline.HoldTailGuardMs) break;
            count++;
        }
        return count;
    }
}
