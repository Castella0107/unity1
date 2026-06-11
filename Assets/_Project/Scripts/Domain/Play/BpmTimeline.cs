using System.Collections.Generic;
using System.Linq;

// Unity-independent. No UnityEngine references allowed in this assembly.
/// <summary>
/// BPMの時系列変化を管理し、任意の時刻におけるBPMおよび1/16拍の間隔（ミリ秒）を返すクラス。
/// </summary>
public class BpmTimeline
{
    readonly List<(double timeMs, double bpm)> _changes;
    // 拍子変化点 (時刻昇順)。timesig イベントが無ければ空 = 全編 4/4 既定 (後方互換)。
    readonly List<(double timeMs, int num, int den)> _sigs;

    /// <summary>
    /// テンポイベントから BPM 変化点と拍子変化点を時刻昇順で構築する。
    /// BPM イベントが無ければ 120、timesig が無ければ全編 4/4 を既定とする。
    /// </summary>
    public BpmTimeline(IEnumerable<TempoEvent> events)
    {
        var list = events as IList<TempoEvent> ?? events.ToList();
        _changes = list
            .Where(e => e.Type == "bpm")
            .OrderBy(e => e.TimeMs)
            .Select(e => (e.TimeMs, e.Bpm))
            .ToList();
        if (_changes.Count == 0)
            _changes.Add((0, 120.0));

        _sigs = list
            .Where(e => e.Type == "timesig" && e.Numerator > 0 && e.Denominator > 0)
            .OrderBy(e => e.TimeMs)
            .Select(e => (e.TimeMs, e.Numerator, e.Denominator))
            .ToList();
    }

    /// <summary>指定時刻に有効な拍子 (分子, 分母) を返す。timesig イベントが無い区間は 4/4。</summary>
    public (int num, int den) GetTimeSignatureAt(double timeMs)
    {
        int num = 4, den = 4;
        foreach (var (t, n, d) in _sigs)
        {
            if (t > timeMs + 0.001) break;
            num = n; den = d;
        }
        return (num, den);
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

    /// <summary>[レガシー] 既定の1小節あたりの拍数(4/4)。拍子対応後は GetTimeSignatureAt / GetMeasureIntervalMs を使う。</summary>
    public const int BeatsPerMeasure = 4;

    /// <summary>1 小節あたりのホールド判定ティック数(1 小節 8 ノーツ = 4/4 で八分音符刻み)。</summary>
    public const int HoldTicksPerMeasure = 8;

    /// <summary>
    /// ホールド末尾ガード(ms)。EndMs から HoldTailGuardMs 以内に来るボディティックは生成しない。
    /// 終端の小節区切りが尾(tail)と重複してコンボ/スコアが二重加算されるのを防ぐ(終端優先)。
    /// 浮動小数の累積誤差で end-ε にティックが紛れ込むケースも吸収する。
    /// </summary>
    public const double HoldTailGuardMs = 1.0;

    /// <summary>指定時刻における 1 拍の長さ(ms)を返す。</summary>
    public double GetBeatIntervalMs(double timeMs) =>
        60_000.0 / GetBpmAt(timeMs);

    /// <summary>
    /// 指定時刻における 1 小節の長さ(ms)を返す。拍子に追従する:
    /// 小節長 = 四分音符長(60000/BPM) × 分子 × (4 / 分母)。
    /// 例 4/4=4拍 / 3/4=3拍 / 6/8=3拍 / 7/8=3.5拍ぶんの四分音符長。
    /// </summary>
    public double GetMeasureIntervalMs(double timeMs)
    {
        var (num, den) = GetTimeSignatureAt(timeMs);
        return GetBeatIntervalMs(timeMs) * num * (4.0 / den);
    }

    /// <summary>
    /// ホールドの判定ティック間隔(ms)を返す。1 小節 8 ノーツ = 小節長の 1/8(4/4 で八分音符)。
    /// HoldJudgmentTracker と ScoringEventCounter が共有し、満点(1,000,000)整合を保つ。
    /// </summary>
    public double GetHoldTickIntervalMs(double timeMs) =>
        GetMeasureIntervalMs(timeMs) / HoldTicksPerMeasure;
}
