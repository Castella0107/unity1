using System.Collections.Generic;

// Unity-independent. Part of Domain assembly.

/// <summary>
/// 譜面の Notes-Per-Second (NPS) ヒストグラムを計算する純粋ヘルパー。
/// 楽曲を bucketMs ごとに区切り、その区間に入るノーツ数を notes/sec に換算した配列を返す。
/// Overview などの可視化に使用する。
/// </summary>
public static class NpsHistogram
{
    /// <summary>
    /// notes を bucketMs 幅のビンに振り分け、各ビンの NPS (notes/sec) 配列を返す。
    /// </summary>
    /// <param name="notes">対象ノーツ列。null/空でも安全。</param>
    /// <param name="durationMs">楽曲長 (ms)。0 以下なら空配列。</param>
    /// <param name="bucketMs">ビン幅 (ms)。0 以下なら空配列。</param>
    public static float[] Compute(IList<NoteData> notes, double durationMs, double bucketMs)
    {
        if (durationMs <= 0.0 || bucketMs <= 0.0) return System.Array.Empty<float>();
        int n = (int)System.Math.Ceiling(durationMs / bucketMs);
        if (n < 1) n = 1;
        var counts = new int[n];
        if (notes != null)
        {
            for (int i = 0; i < notes.Count; i++)
            {
                double t = notes[i].TimeMs;
                if (t < 0.0) continue;
                int idx = (int)(t / bucketMs);
                if (idx < 0) idx = 0;
                if (idx >= n) idx = n - 1;
                counts[idx]++;
            }
        }
        var nps = new float[n];
        double sec = bucketMs / 1000.0;
        if (sec <= 0.0) return nps;
        for (int i = 0; i < n; i++) nps[i] = (float)(counts[i] / sec);
        return nps;
    }

    /// <summary>配列内の最大値を返す。空または null は 0。</summary>
    public static float Max(float[] values)
    {
        if (values == null) return 0f;
        float m = 0f;
        for (int i = 0; i < values.Length; i++) if (values[i] > m) m = values[i];
        return m;
    }

    /// <summary>
    /// pixel 幅から bucketMs を逆算する補助関数。1 ピクセルが 1 ビンとなる粗さを目安に、
    /// 下限 minBucketMs でクランプする。
    /// </summary>
    public static double SuggestBucketMs(double durationMs, int widthPx, double minBucketMs = 250.0)
    {
        if (widthPx < 1 || durationMs <= 0.0) return minBucketMs;
        double b = durationMs / widthPx;
        return b < minBucketMs ? minBucketMs : b;
    }
}
