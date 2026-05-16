using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

// Unity-independent. No UnityEngine references allowed in this assembly.
/// <summary>
/// gzip圧縮されたリプレイバイナリを解凍・CRC32検証し、<see cref="ReplayData"/> へデコードする静的クラス。
/// </summary>
public static class ReplayDecoder
{
    /// Read and validate the header without fully decompressing/parsing.
    public static ReplayHeader DecodeHeaderOnly(byte[] compressed)
    {
        using var input  = new MemoryStream(compressed);
        using var gz     = new GZipStream(input, CompressionMode.Decompress);
        using var reader = new BinaryReader(gz, Encoding.UTF8);
        return ReadHeader(reader);
    }

    /// Full decode with CRC32 verification.
    public static ReplayData Decode(byte[] compressed)
    {
        // 1. Decompress entirely into memory
        byte[] raw;
        using (var input  = new MemoryStream(compressed))
        using (var gz     = new GZipStream(input, CompressionMode.Decompress))
        using (var output = new MemoryStream())
        {
            gz.CopyTo(output);
            raw = output.ToArray();
        }

        if (raw.Length < 4)
            throw new InvalidDataException("Replay data too short");

        // 2. Verify CRC32 — last 4 bytes are the checksum
        int  payloadLen  = raw.Length - 4;
        uint expectedCrc = BitConverter.ToUInt32(raw, payloadLen);
        var  payload     = new byte[payloadLen];
        Array.Copy(raw, 0, payload, 0, payloadLen);
        uint actualCrc = Crc32.Compute(payload);
        if (actualCrc != expectedCrc)
            throw new InvalidDataException(
                string.Format("CRC32 mismatch: expected 0x{0:X8}, got 0x{1:X8}",
                              expectedCrc, actualCrc));

        // 3. Parse sections
        using var ms     = new MemoryStream(payload);
        using var reader = new BinaryReader(ms, Encoding.UTF8);

        var header = ReadHeader(reader);
        if (header.Version != ReplayHeader.CurrentVersion)
            throw new NotSupportedException(
                string.Format("Replay version {0} not supported (current: {1})",
                              header.Version, ReplayHeader.CurrentVersion));

        return new ReplayData
        {
            Header      = header,
            Metadata    = ReadMetadata(reader),
            Result      = ReadResult(reader),
            InputEvents = ReadInputEvents(reader),
        };
    }

    // ── Section readers ───────────────────────────────────────────────────────

    static ReplayHeader ReadHeader(BinaryReader r)
    {
        uint magic = r.ReadUInt32();
        if (magic != ReplayHeader.Magic)
            throw new InvalidDataException(
                string.Format("Invalid replay magic: 0x{0:X8} (expected 0x{1:X8})",
                              magic, ReplayHeader.Magic));

        ushort version = r.ReadUInt16();
        ushort flags   = r.ReadUInt16();
        byte[] uuid    = r.ReadBytes(16);
        if (uuid.Length != 16)
            throw new InvalidDataException("PlayerUuid truncated");

        return new ReplayHeader { Version = version, Flags = flags, PlayerUuid = uuid };
    }

    static ReplayMetadata ReadMetadata(BinaryReader r)
    {
        string songId       = ReadString(r);
        byte   diffByte     = r.ReadByte();
        byte[] chartHash    = r.ReadBytes(32);
        if (chartHash.Length != 32)
            throw new InvalidDataException("ChartHash truncated");

        long  playedAt        = r.ReadInt64();
        int   durationMs      = r.ReadInt32();
        float bpm             = r.ReadSingle();
        short appJudgeOffset  = r.ReadInt16();
        short appVisualOffset = r.ReadInt16();
        short perSongOffset   = r.ReadInt16();

        byte     modCount = r.ReadByte();
        string[] mods     = new string[modCount];
        for (int i = 0; i < modCount; i++)
            mods[i] = ReadString(r);

        string engineVer = ReadString(r);

        return new ReplayMetadata
        {
            SongId                = songId,
            Difficulty            = ByteToDifficulty(diffByte),
            ChartHash             = chartHash,
            PlayedAtUnixMs        = playedAt,
            DurationMs            = durationMs,
            Bpm                   = bpm,
            AppJudgmentOffsetMs   = appJudgeOffset,
            AppVisualOffsetMs     = appVisualOffset,
            PerSongOffsetMs       = perSongOffset,
            Modifiers             = mods,
            JudgmentEngineVersion = engineVer,
        };
    }

    static ReplayResult ReadResult(BinaryReader r)
    {
        return new ReplayResult
        {
            RawScore         = r.ReadInt32(),
            EffectiveScore   = r.ReadInt32(),
            Rank             = ReadRank4(r),
            PerfectPlusCount = r.ReadInt32(),
            PerfectCount     = r.ReadInt32(),
            GreatCount       = r.ReadInt32(),
            GoodCount        = r.ReadInt32(),
            MissCount        = r.ReadInt32(),
            MaxCombo         = r.ReadInt32(),
            FastCount        = r.ReadInt32(),
            LateCount        = r.ReadInt32(),
            TotalNotes       = r.ReadInt32(),
        };
    }

    static List<ReplayInputEvent> ReadInputEvents(BinaryReader r)
    {
        uint count = r.ReadUInt32();
        if (count > 100_000)
            throw new InvalidDataException(
                string.Format("Too many input events: {0}", count));

        var list = new List<ReplayInputEvent>((int)count);
        for (int i = 0; i < (int)count; i++)
        {
            list.Add(new ReplayInputEvent
            {
                DeltaMsFromPrev = VarInt.ReadSignedVarInt(r),
                Lane            = r.ReadByte(),
                Action          = r.ReadByte(),
            });
        }
        return list;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static string ReadString(BinaryReader r)
    {
        byte len = r.ReadByte();
        if (len == 0) return "";
        byte[] bytes = r.ReadBytes(len);
        if (bytes.Length != len)
            throw new InvalidDataException("String truncated");
        return Encoding.UTF8.GetString(bytes);
    }

    static string ReadRank4(BinaryReader r)
    {
        byte[] bytes = r.ReadBytes(4);
        if (bytes.Length != 4)
            throw new InvalidDataException("Rank field truncated");
        return Encoding.UTF8.GetString(bytes).TrimEnd('\0', ' ');
    }

    static string ByteToDifficulty(byte b)
    {
        switch (b)
        {
            case 0:  return "easy";
            case 1:  return "normal";
            case 2:  return "hard";
            case 3:  return "extra";
            default: return "normal";
        }
    }
}
