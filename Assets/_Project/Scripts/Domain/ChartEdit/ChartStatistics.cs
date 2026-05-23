using System.Collections.Generic;

// Unity-independent. Part of Domain assembly.

/// <summary>
/// 譜面統計値の集計結果。
/// </summary>
public sealed class ChartStatistics
{
    public int    TotalNotes;
    public int    TapCount;
    public int    HoldCount;
    public int    FxTapCount;
    public int    FxHoldCount;
    public int    MaxCombo;            // approximation: notes + hold ticks (simple count of notes for v1)
    public double TotalDurationMs;
    public double NotesPerSecond;
    public double HoldOccupancyRatio;  // sum(hold durations) / song duration
    public int[]  LaneDistribution = new int[6];   // counts per lane
    public List<SectorStat> Sectors = new List<SectorStat>();
}

/// <summary>セクター単位の統計。</summary>
public sealed class SectorStat
{
    public int    Id;
    public string Name;
    public int    StartMs;
    public int    EndMs;
    public int    NoteCount;
    public double DurationMs;
    public double NotesPerSecond;
}

/// <summary>譜面から統計値を計算する静的ヘルパー。</summary>
public static class ChartStatisticsCalculator
{
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
