using System.Collections.Generic;

// Unity-independent. No UnityEngine references allowed in this assembly.
// Accumulates input events in chronological order.
// Consumed by GamePlayController at song-end to build ReplayData.
/// <summary>
/// プレイ中のレーン入力イベントを時系列順に蓄積するバッファクラス。
/// 曲終了時に GamePlayController が <see cref="ReplayData"/> を構築するために使用する。
/// </summary>
public class ReplayInputBuffer
{
    readonly List<ReplayInputEvent> _events = new List<ReplayInputEvent>();
    double _lastTimeMs;

    /// <summary>蓄積済みの入力イベント列(時系列順)。</summary>
    public IReadOnlyList<ReplayInputEvent> Events => _events;

    /// <summary>レーン入力イベントを追加する。直前イベントからの差分時刻として記録する。</summary>
    public void Add(int laneIdx, bool isDown, double timeMs)
    {
        int delta = (int)System.Math.Round(timeMs - _lastTimeMs);
        _events.Add(new ReplayInputEvent
        {
            DeltaMsFromPrev = delta,
            Lane   = (byte)laneIdx,
            Action = (byte)(isDown ? 0 : 1),
        });
        _lastTimeMs = timeMs;
    }

    /// <summary>蓄積したイベントと内部時刻をリセットする。</summary>
    public void Clear()
    {
        _events.Clear();
        _lastTimeMs = 0.0;
    }
}
