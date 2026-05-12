using System.Collections.Generic;

// Unity-independent. No UnityEngine references allowed in this assembly.
// Abstracts note lookup so JudgmentEngine works against both ChartData (headless)
// and the live visual pool (gameplay). Currently only ChartDataNoteSource exists;
// a PoolNoteSource could be added if the visual pool needs to drive lookup.
public interface INoteSource
{
    IReadOnlyList<NoteData> AllNotes { get; }

    NoteData FindNearestUnhitTap(LaneRef lane, double timeMs, double maxDeltaMs);
    NoteData FindNearestUnjudgedHold(LaneRef lane, double timeMs);

    // Used by ProcessTime for auto-miss sweep.
    IEnumerable<NoteData> EnumerateUnhitTapsExpiredAt(double currentMs, double goodMs);

    void MarkTapHit(int noteId);
    void MarkHoldHeadJudged(int noteId, Judgment j);
    void MarkHoldTailJudged(int noteId, Judgment j);
    bool IsTapHit(int noteId);
    bool IsHoldHeadJudged(int noteId);
}
