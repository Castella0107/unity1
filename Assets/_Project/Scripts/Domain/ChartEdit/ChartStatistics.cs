using System.Collections.Generic;

// Unity-independent. Part of Domain assembly.

/// <summary>
/// 譜面統計値の集計結果。
/// </summary>
public sealed class ChartStatistics
{
    /// <summary>総ノーツ数。</summary>
    public int    TotalNotes;
    /// <summary>Tap ノーツ数。</summary>
    public int    TapCount;
    /// <summary>Hold ノーツ数。</summary>
    public int    HoldCount;
    /// <summary>FxTap ノーツ数。</summary>
    public int    FxTapCount;
    /// <summary>FxHold ノーツ数。</summary>
    public int    FxHoldCount;
    /// <summary>最大コンボ概算(v1 では総ノーツ数)。</summary>
    public int    MaxCombo;
    /// <summary>集計対象の総時間(ms)。</summary>
    public double TotalDurationMs;
    /// <summary>毎秒ノーツ数(NPS)。</summary>
    public double NotesPerSecond;
    /// <summary>ホールド占有率(ホールド長合計 / 楽曲長)。</summary>
    public double HoldOccupancyRatio;
    /// <summary>レーン別ノーツ数(インデックス 0-5)。</summary>
    public int[]  LaneDistribution = new int[6];
    /// <summary>セクション別統計。</summary>
    public List<SectorStat> Sectors = new List<SectorStat>();
}

/// <summary>セクター単位の統計。</summary>
public sealed class SectorStat
{
    /// <summary>セクションID。</summary>
    public int    Id;
    /// <summary>セクション名。</summary>
    public string Name;
    /// <summary>セクション開始時刻(ms)。</summary>
    public int    StartMs;
    /// <summary>セクション終了時刻(ms)。</summary>
    public int    EndMs;
    /// <summary>このセクション内のノーツ数。</summary>
    public int    NoteCount;
    /// <summary>セクション長(ms)。</summary>
    public double DurationMs;
    /// <summary>このセクションの毎秒ノーツ数(NPS)。</summary>
    public double NotesPerSecond;
}

/// <summary>譜面から統計値を計算する静的ヘルパー。</summary>
public static class ChartStatisticsCalculator
{
    /// <summary>譜面とメタデータから統計値(種別別カウント・NPS・ホールド占有率・レーン分布・セクション統計)を計算する。</summary>
    public static ChartStatistics Compute(ChartData chart, SongMetadata meta, double audioDurationMs)
    {
        var st = new ChartStatistics
        {
            TotalDurationMs = audioDurationMs > 0 ? audioDurationMs : (meta != null ? meta.DurationMs : 0),
        };
        if (chart?.Notes == null) return st;

        double holdSumMs = 0;
        for (int i = 0; i < chart.Notes.Count; i++)
        {
            var n = chart.Notes[i];
            switch (n.Type)
            {
                case NoteType.Tap:    st.TapCount++;    break;
                case NoteType.Hold:   st.HoldCount++;   holdSumMs += n.DurationMs; break;
                case NoteType.FxTap:  st.FxTapCount++;  break;
                case NoteType.FxHold: st.FxHoldCount++; holdSumMs += n.DurationMs; break;
            }
            int li = TimelineLayout.LaneIndex(n.Lane);
            if (li >= 0 && li < st.LaneDistribution.Length) st.LaneDistribution[li]++;
        }
        st.TotalNotes = chart.Notes.Count;
        st.MaxCombo   = st.TotalNotes;
        st.NotesPerSecond = st.TotalDurationMs > 0 ? st.TotalNotes / (st.TotalDurationMs / 1000.0) : 0;
        st.HoldOccupancyRatio = st.TotalDurationMs > 0 ? holdSumMs / st.TotalDurationMs : 0;

        // Sectors
        var sectors = meta?.Sectors;
        if (sectors != null && sectors.Count > 0)
        {
            int prevEnd = 0;
            for (int i = 0; i < sectors.Count; i++)
            {
                var ss = new SectorStat
                {
                    Id = sectors[i].Id, Name = sectors[i].Name,
                    StartMs = prevEnd, EndMs = sectors[i].EndMs,
                    DurationMs = sectors[i].EndMs - prevEnd,
                };
                for (int j = 0; j < chart.Notes.Count; j++)
                {
                    var n = chart.Notes[j];
                    if (n.TimeMs > prevEnd && n.TimeMs <= sectors[i].EndMs) ss.NoteCount++;
                }
                ss.NotesPerSecond = ss.DurationMs > 0 ? ss.NoteCount / (ss.DurationMs / 1000.0) : 0;
                st.Sectors.Add(ss);
                prevEnd = sectors[i].EndMs;
            }
        }

        return st;
    }
}
