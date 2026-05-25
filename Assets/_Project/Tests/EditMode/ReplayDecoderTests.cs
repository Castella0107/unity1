using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using NUnit.Framework;

/// <summary><see cref="ReplayDecoder"/> のユニットテスト。</summary>
public class ReplayDecoderTests
{
    // ── 基本デコード ──────────────────────────────────────────────────────────

    [Test]
    public void DecodeHeaderOnly_ReturnsCorrectVersion()
    {
        var bytes = ReplayEncoder.Encode(MakeSample());
        var h     = ReplayDecoder.DecodeHeaderOnly(bytes);
        Assert.AreEqual(ReplayHeader.CurrentVersion, h.Version);
    }

    [Test]
    public void Decode_AllSectionsNonNull()
    {
        var decoded = ReplayDecoder.Decode(ReplayEncoder.Encode(MakeSample()));
        Assert.IsNotNull(decoded.Header);
        Assert.IsNotNull(decoded.Metadata);
        Assert.IsNotNull(decoded.Result);
        Assert.IsNotNull(decoded.InputEvents);
    }

    // ── 往復テスト: Header ────────────────────────────────────────────────────

    [Test]
    public void RoundTrip_PlayerUuid()
    {
        var data = MakeSample();
        data.Header.PlayerUuid[0]  = 0xDE;
        data.Header.PlayerUuid[15] = 0xEF;

        var decoded = ReplayDecoder.Decode(ReplayEncoder.Encode(data));
        Assert.AreEqual(0xDE, decoded.Header.PlayerUuid[0]);
        Assert.AreEqual(0xEF, decoded.Header.PlayerUuid[15]);
    }

    // ── 往復テスト: Metadata ──────────────────────────────────────────────────

    [Test]
    public void RoundTrip_MetadataFields()
    {
        var meta = new ReplayMetadata
        {
            SongId                = "very_long_song_name_with_underscores_2026",
            Difficulty            = "hard",
            ChartHash             = MakeHash(),
            PlayedAtUnixMs        = 1735689600000L,
            DurationMs            = 245678,
            Bpm                   = 187.5f,
            AppJudgmentOffsetMs   = -28,
            AppVisualOffsetMs     = -12,
            PerSongOffsetMs       = 23,
            Modifiers             = new[] { "Mirror", "Random" },
            JudgmentEngineVersion = "1.0.0-beta",
        };
        var data    = MakeSample(meta: meta);
        var decoded = ReplayDecoder.Decode(ReplayEncoder.Encode(data));
        var m       = decoded.Metadata;

        Assert.AreEqual("very_long_song_name_with_underscores_2026", m.SongId);
        Assert.AreEqual("hard",          m.Difficulty);
        Assert.AreEqual(1735689600000L,  m.PlayedAtUnixMs);
        Assert.AreEqual(245678,          m.DurationMs);
        Assert.AreEqual(187.5f,          m.Bpm);
        Assert.AreEqual(-28,             m.AppJudgmentOffsetMs);
        Assert.AreEqual(-12,             m.AppVisualOffsetMs);
        Assert.AreEqual(23,              m.PerSongOffsetMs);
        CollectionAssert.AreEqual(new[] { "Mirror", "Random" }, m.Modifiers);
        Assert.AreEqual("1.0.0-beta",    m.JudgmentEngineVersion);
        CollectionAssert.AreEqual(MakeHash(), m.ChartHash);
    }

    [Test]
    public void RoundTrip_Difficulty_AllValues()
    {
        foreach (var diff in new[] { "easy", "normal", "hard", "extra" })
        {
            var meta    = new ReplayMetadata
            {
                SongId = "s", Difficulty = diff,
                ChartHash = new byte[32], Modifiers = new string[0],
                JudgmentEngineVersion = "1.0.0",
            };
            var decoded = ReplayDecoder.Decode(ReplayEncoder.Encode(MakeSample(meta: meta)));
            Assert.AreEqual(diff, decoded.Metadata.Difficulty, "Difficulty: " + diff);
        }
    }

    [Test]
    public void RoundTrip_NoModifiers()
    {
        var meta = new ReplayMetadata
        {
            SongId = "s", Difficulty = "extra",
            ChartHash = new byte[32], Modifiers = new string[0],
            JudgmentEngineVersion = "1.0.0",
        };
        var decoded = ReplayDecoder.Decode(ReplayEncoder.Encode(MakeSample(meta: meta)));
        Assert.AreEqual(0, decoded.Metadata.Modifiers.Length);
    }

    // ── 往復テスト: Result ────────────────────────────────────────────────────

    [Test]
    public void RoundTrip_ResultFields()
    {
        var result = new ReplayResult
        {
            RawScore = 987_654, EffectiveScore = 988_640, Rank = "S",
            PerfectPlusCount = 985, PerfectCount = 12, GreatCount = 2,
            GoodCount = 1, MissCount = 0, MaxCombo = 1000,
            FastCount = 110, LateCount = 95, TotalNotes = 1000,
        };
        var decoded = ReplayDecoder.Decode(ReplayEncoder.Encode(MakeSample(result: result)));
        var r       = decoded.Result;

        Assert.AreEqual(987_654,  r.RawScore);
        Assert.AreEqual(988_640,  r.EffectiveScore);
        Assert.AreEqual("S",      r.Rank);
        Assert.AreEqual(985,      r.PerfectPlusCount);
        Assert.AreEqual(12,       r.PerfectCount);
        Assert.AreEqual(2,        r.GreatCount);
        Assert.AreEqual(1,        r.GoodCount);
        Assert.AreEqual(0,        r.MissCount);
        Assert.AreEqual(1000,     r.MaxCombo);
        Assert.AreEqual(110,      r.FastCount);
        Assert.AreEqual(95,       r.LateCount);
        Assert.AreEqual(1000,     r.TotalNotes);
    }

    [Test]
    public void RoundTrip_RankPlus()
    {
        var result  = new ReplayResult { Rank = "S+", RawScore = 1_000_000, EffectiveScore = 1_000_000, TotalNotes = 1000 };
        var decoded = ReplayDecoder.Decode(ReplayEncoder.Encode(MakeSample(result: result)));
        Assert.AreEqual("S+", decoded.Result.Rank);
    }

    [Test]
    public void RoundTrip_RankAllVariants()
    {
        foreach (var rank in new[] { "S+", "S", "A+", "A", "B", "C", "D" })
        {
            var result  = new ReplayResult { Rank = rank };
            var decoded = ReplayDecoder.Decode(ReplayEncoder.Encode(MakeSample(result: result)));
            Assert.AreEqual(rank, decoded.Result.Rank, "Rank: " + rank);
        }
    }

    // ── 往復テスト: InputEvents ───────────────────────────────────────────────

    [Test]
    public void RoundTrip_InputEvents_PreservesOrder()
    {
        var events = new List<ReplayInputEvent>
        {
            new ReplayInputEvent { DeltaMsFromPrev = 1234,  Lane = 0, Action = 0 },
            new ReplayInputEvent { DeltaMsFromPrev = 56,    Lane = 0, Action = 1 },
            new ReplayInputEvent { DeltaMsFromPrev = 33,    Lane = 5, Action = 0 },
            new ReplayInputEvent { DeltaMsFromPrev = -10,   Lane = 5, Action = 1 },  // 負値
            new ReplayInputEvent { DeltaMsFromPrev = 200,   Lane = 4, Action = 0 },
        };
        var data = MakeSample();
        data.InputEvents = events;

        var decoded = ReplayDecoder.Decode(ReplayEncoder.Encode(data));
        Assert.AreEqual(events.Count, decoded.InputEvents.Count);
        for (int i = 0; i < events.Count; i++)
        {
            Assert.AreEqual(events[i].DeltaMsFromPrev, decoded.InputEvents[i].DeltaMsFromPrev, "DeltaMs[" + i + "]");
            Assert.AreEqual(events[i].Lane,            decoded.InputEvents[i].Lane,            "Lane["    + i + "]");
            Assert.AreEqual(events[i].Action,          decoded.InputEvents[i].Action,          "Action["  + i + "]");
        }
    }

    [Test]
    public void RoundTrip_EmptyInputEvents()
    {
        var data = MakeSample();
        data.InputEvents = new List<ReplayInputEvent>();
        var decoded = ReplayDecoder.Decode(ReplayEncoder.Encode(data));
        Assert.AreEqual(0, decoded.InputEvents.Count);
    }

    [Test]
    public void RoundTrip_LargeInputEventList()
    {
        var data = MakeSample(eventCount: 5000);
        var decoded = ReplayDecoder.Decode(ReplayEncoder.Encode(data));
        Assert.AreEqual(5000, decoded.InputEvents.Count);
    }

    [Test]
    public void RoundTrip_LargeNegativeDelta()
    {
        // VarInt で負の大きい値が正しく保存・復元されること
        var data = MakeSample();
        data.InputEvents = new List<ReplayInputEvent>
        {
            new ReplayInputEvent { DeltaMsFromPrev = -100_000, Lane = 0, Action = 0 },
        };
        var decoded = ReplayDecoder.Decode(ReplayEncoder.Encode(data));
        Assert.AreEqual(-100_000, decoded.InputEvents[0].DeltaMsFromPrev);
    }

    // ── 異常系 ────────────────────────────────────────────────────────────────

    [Test]
    public void Decode_InvalidMagic_Throws()
    {
        byte[] raw = Decompress(ReplayEncoder.Encode(MakeSample()));
        raw[0] = 0xFF;  // Magic の先頭バイトを破壊
        Assert.Throws<InvalidDataException>(() => ReplayDecoder.Decode(Recompress(raw)));
    }

    [Test]
    public void Decode_CorruptedCrc_ThrowsWithCrcMessage()
    {
        byte[] raw = Decompress(ReplayEncoder.Encode(MakeSample()));
        raw[raw.Length - 1] ^= 0xFF;  // CRC の最終バイトを反転
        var ex = Assert.Throws<InvalidDataException>(() => ReplayDecoder.Decode(Recompress(raw)));
        Assert.IsTrue(ex.Message.Contains("CRC32"));
    }

    [Test]
    public void Decode_TooShort_Throws()
    {
        byte[] raw    = { 0x01, 0x02, 0x03 };  // 4バイト未満
        Assert.Throws<InvalidDataException>(() => ReplayDecoder.Decode(Recompress(raw)));
    }

    [Test]
    public void DecodeHeaderOnly_InvalidMagic_Throws()
    {
        byte[] raw = Decompress(ReplayEncoder.Encode(MakeSample()));
        raw[0] = 0xAB;
        Assert.Throws<InvalidDataException>(() => ReplayDecoder.DecodeHeaderOnly(Recompress(raw)));
    }

    // ── ユーティリティ ────────────────────────────────────────────────────────

    static byte[] Decompress(byte[] compressed)
    {
        using var input  = new MemoryStream(compressed);
        using var gz     = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gz.CopyTo(output);
        return output.ToArray();
    }

    static byte[] Recompress(byte[] raw)
    {
        using var ms = new MemoryStream();
        using (var gz = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true))
            gz.Write(raw, 0, raw.Length);
        return ms.ToArray();
    }

    static ReplayData MakeSample(int eventCount = 5,
                                  ReplayMetadata meta   = null,
                                  ReplayResult   result = null)
    {
        var events = new List<ReplayInputEvent>(eventCount);
        for (int i = 0; i < eventCount; i++)
            events.Add(new ReplayInputEvent
            {
                DeltaMsFromPrev = 100 + i,
                Lane            = (byte)(i % 6),
                Action          = (byte)(i % 2),
            });

        return new ReplayData
        {
            Header = new ReplayHeader { PlayerUuid = new byte[16] },
            Metadata = meta ?? new ReplayMetadata
            {
                SongId                = "test_song",
                Difficulty            = "extra",
                ChartHash             = MakeHash(),
                PlayedAtUnixMs        = 1700000000000L,
                DurationMs            = 180_000,
                Bpm                   = 175.0f,
                Modifiers             = new[] { "Mirror" },
                JudgmentEngineVersion = "1.0.0",
            },
            Result = result ?? new ReplayResult
            {
                RawScore = 1_000_000, EffectiveScore = 1_000_000,
                Rank = "S+", PerfectPlusCount = 1000,
                MaxCombo = 1000, TotalNotes = 1000,
            },
            InputEvents = events,
        };
    }

    static byte[] MakeHash()
    {
        var h = new byte[32];
        for (int i = 0; i < 32; i++) h[i] = (byte)(i * 7 + 1);
        return h;
    }
}
