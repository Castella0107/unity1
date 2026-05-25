using System.Collections.Generic;

// Unity-independent. No UnityEngine references allowed in this assembly.
// Abstracts note lookup so JudgmentEngine works against both ChartData (headless)
// and the live visual pool (gameplay). Currently only ChartDataNoteSource exists;
// a PoolNoteSource could be added if the visual pool needs to drive lookup.
/// <summary>
/// ノーツの検索・状態管理を抽象化するインターフェース。
/// JudgmentEngine がヘッドレス実行とゲームプレイの双方で同一ロジックを使用できるようにする。
/// </summary>
public interface INoteSource
{
    /// <summary>全ノーツの読み取り専用リスト。</summary>
    IReadOnlyList<NoteData> AllNotes { get; }

    /// <summary>指定レーンで <paramref name="timeMs"/> に最も近い未ヒットのタップを探す(許容差 <paramref name="maxDeltaMs"/>)。無ければ null。</summary>
    NoteData FindNearestUnhitTap(LaneRef lane, double timeMs, double maxDeltaMs);
    /// <summary>指定レーンで <paramref name="timeMs"/> に最も近い未判定のホールドを探す。無ければ null。</summary>
    NoteData FindNearestUnjudgedHold(LaneRef lane, double timeMs);

    /// <summary>現在時刻で Good 窓を過ぎてもヒットされていないタップを列挙する(オートミス掃引用)。</summary>
    IEnumerable<NoteData> EnumerateUnhitTapsExpiredAt(double currentMs, double goodMs);

    /// <summary>タップをヒット済みとして記録する。</summary>
    void MarkTapHit(int noteId);
    /// <summary>ホールド頭の判定を記録する。</summary>
    void MarkHoldHeadJudged(int noteId, Judgment j);
    /// <summary>ホールド尾の判定を記録する。</summary>
    void MarkHoldTailJudged(int noteId, Judgment j);
    /// <summary>タップがヒット済みか。</summary>
    bool IsTapHit(int noteId);
    /// <summary>ホールド頭が判定済みか。</summary>
    bool IsHoldHeadJudged(int noteId);
}
