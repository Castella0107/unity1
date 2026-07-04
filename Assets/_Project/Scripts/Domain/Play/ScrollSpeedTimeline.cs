using System.Collections.Generic;
using System.Linq;

// Unity-independent. No UnityEngine references allowed in this assembly.
/// <summary>
/// "speed" テンポイベント（スクロール速度倍率）の時系列を管理し、任意区間の
/// 「視覚距離(ms 換算)」= 速度倍率を時間で積分した値を返すクラス。
///
/// 視覚位置 VisualPos(t) = ∫₀ᵗ mult(s) ds（mult は区分定数のステップ関数。最初の
/// speed イベント前は 1.0 既定）。ノートの判定線までの描画距離は
/// scrollSpeed × (VisualPos(noteTime) − VisualPos(visualMs)) / 1000 で求める。
///
/// スクロール演出のみに影響し、判定時刻・スコアには一切影響しない（視覚専用）。
/// speed イベントが無い譜面では VisualPos(t)=t となり、従来の等速挙動と完全に一致する。
/// </summary>
public class ScrollSpeedTimeline
{
    // 速度変化点(時刻昇順)。_mults[i] は _times[i] 以降に有効な倍率。
    readonly double[] _times;
    readonly double[] _mults;
    // _cum[i] = VisualPos(_times[i])(時刻 0 からの積分値)。区間距離を O(log n) で引くための前計算。
    readonly double[] _cum;

    /// <summary>テンポイベント列から speed イベントだけを抜き出して時刻昇順に構築する。</summary>
    public ScrollSpeedTimeline(IEnumerable<TempoEvent> events)
    {
        var changes = (events ?? Enumerable.Empty<TempoEvent>())
            .Where(e => e != null && e.Type == "speed" && e.Multiplier > 0.0)
            .OrderBy(e => e.TimeMs)
            .ToList();

        int n  = changes.Count;
        _times = new double[n];
        _mults = new double[n];
        _cum   = new double[n];

        for (int i = 0; i < n; i++)
        {
            _times[i] = changes[i].TimeMs;
            _mults[i] = changes[i].Multiplier;
        }
        for (int i = 0; i < n; i++)
        {
            // 0 〜 _times[0] は倍率 1.0(既定)。_times[0] が負でも符号込みで整合する。
            if (i == 0) _cum[0] = _times[0];
            else        _cum[i] = _cum[i - 1] + _mults[i - 1] * (_times[i] - _times[i - 1]);
        }
    }

    /// <summary>speed イベントが 1 件でもあるか(無ければ全編等速)。</summary>
    public bool HasChanges => _times.Length > 0;

    // 直近呼び出しの1要素メモ。構築後この積分は不変(純関数)なので常に正しい。描画は毎フレーム
    // 同一 visualMs を基点に全ノーツぶん VisualDistanceMs(visualMs, …) を呼ぶため、基点側の
    // VisualPos(visualMs) の二分探索が memo ヒットで省ける (NaN は何とも一致せず初回は必ずミス)。
    double _memoArg = double.NaN;
    double _memoVal;

    /// <summary>時刻 t における視覚位置(時刻 0 からの倍率積分, ms 換算)。</summary>
    public double VisualPos(double t)
    {
        if (t == _memoArg) return _memoVal;

        int n = _times.Length;
        double r;
        if (n == 0 || t < _times[0])         // speed イベント無し/最初の変化前は等速(従来挙動)
        {
            r = t;
        }
        else
        {
            // _times[i] <= t < _times[i+1] となる最大 i を二分探索。
            int lo = 0, hi = n;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (_times[mid] <= t) lo = mid + 1;
                else                  hi = mid;
            }
            int i = lo - 1;
            r = _cum[i] + _mults[i] * (t - _times[i]);
        }

        _memoArg = t; _memoVal = r;
        return r;
    }

    /// <summary>区間 [fromMs, toMs] の視覚距離(ms 換算)。to &gt; from で正。</summary>
    public double VisualDistanceMs(double fromMs, double toMs) => VisualPos(toMs) - VisualPos(fromMs);

    /// <summary>時刻 t に有効な速度倍率。</summary>
    public double GetMultiplierAt(double timeMs)
    {
        int n = _times.Length;
        if (n == 0 || timeMs < _times[0]) return 1.0;
        int lo = 0, hi = n;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            if (_times[mid] <= timeMs) lo = mid + 1;
            else                       hi = mid;
        }
        return _mults[lo - 1];
    }
}
