// Unity-independent. No UnityEngine references allowed in this assembly.
/// <summary>
/// タイムライン座標系のヘルパー。
/// レーン番号 → X 座標、時刻 (ms) → Y 座標 (上 0 → 下 dur*ppms) 変換と逆変換を提供する。
/// </summary>
public static class TimelineLayout
{
    public const int LaneCount        = 6;
    public const float LaneWidthPx    = 64f;   // 1 レーンの幅
    public const float LaneGapPx      = 4f;    // レーン間の隙間
    public const float TimelineWidthPx = (LaneWidthPx + LaneGapPx) * LaneCount + LaneGapPx;

    public static float LaneToX(LaneRef lane)
    {
        int idx = LaneIndex(lane);
        // Centered: gap, lane, gap, lane, ... ; X relative to left edge
        return LaneGapPx + idx * (LaneWidthPx + LaneGapPx) + LaneWidthPx * 0.5f;
    }

    public static int LaneIndex(LaneRef lane)
    {
        switch (lane)
        {
            case LaneRef.Lane0: return 0;
            case LaneRef.Lane1: return 1;
            case LaneRef.Lane2: return 2;
            case LaneRef.Lane3: return 3;
            case LaneRef.FxL:   return 4;
            case LaneRef.FxR:   return 5;
            default: return 0;
        }
    }

    public static LaneRef IndexToLane(int idx)
    {
        switch (idx)
        {
            case 0: return LaneRef.Lane0;
            case 1: return LaneRef.Lane1;
            case 2: return LaneRef.Lane2;
            case 3: return LaneRef.Lane3;
            case 4: return LaneRef.FxL;
            case 5: return LaneRef.FxR;
            default: return LaneRef.Lane0;
        }
    }

    /// <summary>クリック位置 X (タイムライン左端からの相対) → レーン。範囲外は -1。</summary>
    public static int XToLaneIndex(float x)
    {
        for (int i = 0; i < LaneCount; i++)
        {
            float left  = LaneGapPx + i * (LaneWidthPx + LaneGapPx);
            float right = left + LaneWidthPx;
            if (x >= left && x <= right) return i;
        }
        return -1;
    }

    /// <summary>時刻 ms と pixelsPerMs から content 内 Y 座標 (上=高Y) を返す。</summary>
    public static float TimeToY(double timeMs, float pixelsPerMs, float contentHeightPx)
    {
        // Content top is at high Y in RectTransform local coords; we want time=0 at top.
        return contentHeightPx - (float)(timeMs * pixelsPerMs);
    }

    public static double YToTime(float y, float pixelsPerMs, float contentHeightPx)
    {
        if (pixelsPerMs <= 0f) return 0.0;
        return (contentHeightPx - y) / pixelsPerMs;
    }
}
