using System;
using System.IO;
using System.IO.Compression;
using System.Text;

// Unity-independent. No UnityEngine references allowed in this assembly.
/// <summary>
/// <see cref="ReplayData"/> をバイナリ形式にシリアライズし、CRC32を付加したうえで gzip 圧縮する静的クラス。
/// </summary>
public static class ReplayEncoder
{
    /// <summary>
    /// <see cref="ReplayData"/> をバイナリ([header][metadata][result][events][crc32])にシリアライズし gzip 圧縮して返す。
    /// </summary>
    public static byte[] Encode(ReplayData data)
    {
        using var raw = new MemoryStream();

        // Write structured sections
        using (var w = new BinaryWriter(raw, Encoding.UTF8, leaveOpen: true))
        {
            WriteHeader(w, data.Header);
            WriteMetadata(w, data.Metadata);
            WriteResult(w, data.Result);
            WriteEvents(w, data.InputEvents);
        }
        // raw.Position is now at end

        // Append CRC32 of all bytes written so far
        uint crc = Crc32.Compute(raw.ToArray());
        using (var w = new BinaryWriter(raw, Encoding.UTF8, leaveOpen: true))
            w.Write(crc);

        // Compress
        using var compressed = new MemoryStream();
        using (var gz = new GZipStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            raw.Position = 0;
            raw.CopyTo(gz);
        }
        return compressed.ToArray();
    }

    // ── Section writers ───────────────────────────────────────────────────────

    static void WriteHeader(BinaryWriter w, ReplayHeader h)
    {
        w.Write(ReplayHeader.Magic);               // 4 bytes
        w.Write(h.Version);                        // 2 bytes
        w.Write(h.Flags);                          // 2 bytes
        w.Write(h.PlayerUuid ?? new byte[16]);     // 16 bytes
    }

    static void WriteMetadata(BinaryWriter w, ReplayMetadata m)
    {
        WriteString(w, m.SongId);
        w.Write(DifficultyToByte(m.Difficulty));
        w.Write(m.ChartHash ?? new byte[32]);      // 32 bytes
        w.Write(m.PlayedAtUnixMs);
        w.Write(m.DurationMs);
        w.Write(m.Bpm);
        w.Write(m.AppJudgmentOffsetMs);
        w.Write(m.AppVisualOffsetMs);
        w.Write(m.PerSongOffsetMs);

        var mods = m.Modifiers ?? new string[0];
        w.Write((byte)Math.Min(mods.Length, 255));
        for (int i = 0; i < Math.Min(mods.Length, 255); i++)
            WriteString(w, mods[i]);

        WriteString(w, m.JudgmentEngineVersion ?? "1.0.0");
    }

    static void WriteResult(BinaryWriter w, ReplayResult r)
    {
        w.Write(r.RawScore);
        w.Write(r.EffectiveScore);
        WriteRank4(w, r.Rank);   // 4 bytes fixed
        w.Write(r.PerfectPlusCount);
        w.Write(r.PerfectCount);
        w.Write(r.GreatCount);
        w.Write(r.GoodCount);
        w.Write(r.MissCount);
        w.Write(r.MaxCombo);
        w.Write(r.FastCount);
        w.Write(r.LateCount);
        w.Write(r.TotalNotes);
    }

    static void WriteEvents(BinaryWriter w, System.Collections.Generic.List<ReplayInputEvent> events)
    {
        w.Write((uint)(events?.Count ?? 0));
        if (events == null) return;
        foreach (var e in events)
        {
            VarInt.WriteSignedVarInt(w, e.DeltaMsFromPrev);
            w.Write(e.Lane);
            w.Write(e.Action);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static void WriteString(BinaryWriter w, string s)
    {
        byte[] b = Encoding.UTF8.GetBytes(s ?? "");
        w.Write((byte)Math.Min(b.Length, 255));
        w.Write(b, 0, Math.Min(b.Length, 255));
    }

    static void WriteRank4(BinaryWriter w, string rank)
    {
        var buf = new byte[4];
        byte[] src = Encoding.UTF8.GetBytes(rank ?? "D");
        Array.Copy(src, 0, buf, 0, Math.Min(src.Length, 4));
        w.Write(buf);
    }

    static byte DifficultyToByte(string diff)
    {
        switch (diff)
        {
            case "easy":   return 0;
            case "normal": return 1;
            case "hard":   return 2;
            case "extra":  return 3;
            default:       return 1;
        }
    }
}
