using System;
using System.Linq;

// Unity-independent. No UnityEngine references allowed in this assembly.
public static class ReplayValidator
{
    // ReplayData.Metadata.ChartHash is byte[], ChartData.ChartHash is a hex string.
    public static bool MatchesChart(ReplayData replay, ChartData chart)
    {
        if (replay?.Metadata?.ChartHash == null) return false;
        if (string.IsNullOrEmpty(chart?.ChartHash))  return false;

        byte[] chartBytes = HexToBytes(chart.ChartHash);
        if (chartBytes == null) return false;

        return replay.Metadata.ChartHash.SequenceEqual(chartBytes);
    }

    static byte[] HexToBytes(string hex)
    {
        if (hex.Length % 2 != 0) return null;
        var bytes = new byte[hex.Length / 2];
        try
        {
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }
        catch
        {
            return null;
        }
        return bytes;
    }
}
