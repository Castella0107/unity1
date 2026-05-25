using System.Collections.Generic;
using System.Linq;

// Unity-independent. No UnityEngine references allowed in this assembly.
/// <summary>
/// BPMの時系列変化を管理し、任意の時刻におけるBPMおよび1/16拍の間隔（ミリ秒）を返すクラス。
/// </summary>
public class BpmTimeline
{
    readonly List<(double timeMs, double bpm)> _changes;

    /// <summary>テンポイベントから BPM 変化点を時刻昇順で構築する。BPM イベントが無ければ 120 を既定とする。</summary>
    public BpmTimeline(IEnumerable<TempoEvent> events)
    {
        _changes = events
            .Where(e => e.Type == "bpm")
            .OrderBy(e => e.TimeMs)
            .Select(e => (e.TimeMs, e.Bpm))
            .ToList();
        if (_changes.Count == 0)
            _changes.Add((0, 120.0));
    }

    /// <summary>指定時刻に有効な BPM を返す。</summary>
    public double GetBpmAt(double timeMs)
    {
        double bpm = _changes[0].bpm;
        foreach (var (t, b) in _changes)
        {
            if (t > timeMs) break;
            bpm = b;
        }
        return bpm;
    }

    /// <summary>指定時刻における 1/16 拍の長さ(ms)を返す。</summary>
    public double GetTickIntervalMs(double timeMs) =>
        (60_000.0 / GetBpmAt(timeMs)) / 16.0;

    /// <summary>1 小節あたりの拍数(拍子情報が無いため 4/4 を既定とする)。</summary>
    public const int BeatsPerMeasure = 4;

    /// <summary>1 小節あたりのホールド判定ティック数(1 小節 2 ノーツ)。</summary>
    public const int HoldTicksPerMeasure = 2;

    /// <summary>
    /// ホールド末尾ガード(ms)。EndMs から HoldTailGuardMs 以内に来るボディティックは生成しない。
    /// 終端の小節区切りが尾(tail)と重複してコンボ/スコアが二重加算されるのを防ぐ(終端優先)。
    /// 浮動小数の累積誤差で end-ε にティックが紛れ込むケースも吸収する。
    /// </summary>
    public const double HoldTailGuardMs = 1.0;

    /// <summary>指定時刻における 1 拍の長さ(ms)を返す。</summary>
    public double GetBeatIntervalMs(double timeMs) =>
        60_000.0 / GetBpmAt(timeMs);

    /// <summary>指定時刻における 1 小節の長さ(ms)を返す(4/4 = 4 拍)。</summary>
    public double GetMeasureIntervalMs(double timeMs) =>
        GetBeatIntervalMs(timeMs) * BeatsPerMeasure;

    /// <summary>
    /// ホールドの判定ティック間隔(ms)を返す。1 小節 2 ノーツ = 小節長の 1/2(4/4 で 2 拍)。
    /// HoldJudgmentTracker と ScoringEventCounter が共有し、満点(1,000,000)整合を保つ。
    /// </summary>
    public double GetHoldTickIntervalMs(double timeMs) =>
        GetMeasureIntervalMs(timeMs) / HoldTicksPerMeasure;
}
