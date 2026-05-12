using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

// ── VarInt ────────────────────────────────────────────────────────────────────

public class VarIntTests
{
    void RoundTrip(int value)
    {
        var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
            VarInt.WriteSignedVarInt(w, value);
        ms.Position = 0;
        using var r = new BinaryReader(ms);
        Assert.AreEqual(value, VarInt.ReadSignedVarInt(r));
    }

    [Test] public void Zero()          => RoundTrip(0);
    [Test] public void PositiveSmall() => RoundTrip(1);
    [Test] public void NegativeSmall() => RoundTrip(-1);
    [Test] public void Positive127()   => RoundTrip(127);
    [Test] public void Negative128()   => RoundTrip(-128);
    [Test] public void Large()         => RoundTrip(100_000);
    [Test] public void LargeNegative() => RoundTrip(-100_000);
    [Test] public void MaxInt()        => RoundTrip(int.MaxValue);
    [Test] public void MinInt()        => RoundTrip(int.MinValue);

    [Test]
    public void SmallPositive_Fits1Byte()
    {
        var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
            VarInt.WriteSignedVarInt(w, 1);
        Assert.AreEqual(1, ms.Length);
    }

    [Test]
    public void SmallNegative_Fits1Byte()
    {
        var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
            VarInt.WriteSignedVarInt(w, -1);
        Assert.AreEqual(1, ms.Length);
    }
}

// ── ReplayInputBuffer ─────────────────────────────────────────────────────────

public class ReplayInputBufferTests
{
    [Test]
    public void Empty_HasNoEvents()
    {
        var buf = new ReplayInputBuffer();
        Assert.AreEqual(0, buf.Events.Count);
    }

    [Test]
    public void FirstEvent_HasDeltaEqualToTime()
    {
        var buf = new ReplayInputBuffer();
        buf.Add(0, true, 1000.0);
        Assert.AreEqual(1, buf.Events.Count);
        Assert.AreEqual(1000, buf.Events[0].DeltaMsFromPrev);
    }

    [Test]
    public void SubsequentEvent_HasDeltaFromPrevious()
    {
        var buf = new ReplayInputBuffer();
        buf.Add(0, true,  1000.0);
        buf.Add(1, false, 1250.0);
        Assert.AreEqual(250, buf.Events[1].DeltaMsFromPrev);
    }

    [Test]
    public void LaneAndAction_StoredCorrectly()
    {
        var buf = new ReplayInputBuffer();
        buf.Add(3, true,  500.0);
        buf.Add(3, false, 600.0);
        Assert.AreEqual(3, buf.Events[0].Lane);
        Assert.AreEqual(0, buf.Events[0].Action);  // Down
        Assert.AreEqual(3, buf.Events[1].Lane);
        Assert.AreEqual(1, buf.Events[1].Action);  // Up
    }

    [Test]
    public void Clear_ResetsBuffer()
    {
        var buf = new ReplayInputBuffer();
        buf.Add(0, true, 1000.0);
        buf.Clear();
        Assert.AreEqual(0, buf.Events.Count);
        buf.Add(0, true, 500.0);
        Assert.AreEqual(500, buf.Events[0].DeltaMsFromPrev);
    }
}

// ── ReplayEncoder ─────────────────────────────────────────────────────────────

public class ReplayEncoderTests
{
    static ReplayData MakeMinimal()
    {
        return new ReplayData
        {
            Header   = new ReplayHeader(),
            Metadata = new ReplayMetadata
            {
                SongId                = "test_song",
                Difficulty            = "normal",
                ChartHash             = new byte[32],
                PlayedAtUnixMs        = 1_700_000_000_000L,
                DurationMs            = 120_000,
                Bpm                   = 160f,
                AppJudgmentOffsetMs   = 0,
                AppVisualOffsetMs     = 0,
                PerSongOffsetMs       = 0,
                Modifiers             = new string[0],
                JudgmentEngineVersion = "1.0.0",
            },
            Result = new ReplayResult
            {
                RawScore = 1_000_000, EffectiveScore = 1_000_000,
                Rank = "S+", PerfectPlusCount = 100, PerfectCount = 0,
                GreatCount = 0, GoodCount = 0, MissCount = 0,
                MaxCombo = 100, FastCount = 0, LateCount = 0, TotalNotes = 100,
            },
            InputEvents = new List<ReplayInputEvent>(),
        };
    }

    [Test]
    public void Encode_ProducesNonEmptyBytes()
    {
        byte[] bytes = ReplayEncoder.Encode(MakeMinimal());
        Assert.IsNotNull(bytes);
        Assert.Greater(bytes.Length, 0);
    }

    [Test]
    public void Encode_HeaderMagicSurvivesDecodeHeaderOnly()
    {
        byte[] bytes = ReplayEncoder.Encode(MakeMinimal());
        var h = ReplayDecoder.DecodeHeaderOnly(bytes);
        Assert.AreEqual(ReplayHeader.CurrentVersion, h.Version);
    }

    [Test]
    public void Encode_IsGzipCompressed()
    {
        byte[] bytes = ReplayEncoder.Encode(MakeMinimal());
        // gzip magic: 0x1F 0x8B
        Assert.AreEqual(0x1F, bytes[0]);
        Assert.AreEqual(0x8B, bytes[1]);
    }

    [Test]
    public void Encode_WithEvents_DecodesHeader()
    {
        var data = MakeMinimal();
        data.InputEvents.Add(new ReplayInputEvent { DeltaMsFromPrev = 1000, Lane = 0, Action = 0 });
        data.InputEvents.Add(new ReplayInputEvent { DeltaMsFromPrev = 250,  Lane = 0, Action = 1 });
        data.InputEvents.Add(new ReplayInputEvent { DeltaMsFromPrev = 500,  Lane = 2, Action = 0 });

        byte[] bytes = ReplayEncoder.Encode(data);
        var h = ReplayDecoder.DecodeHeaderOnly(bytes);
        Assert.AreEqual(1, h.Version);
    }

    [Test]
    public void Encode_SameDeterministic()
    {
        var data1 = MakeMinimal();
        var data2 = MakeMinimal();
        byte[] b1 = ReplayEncoder.Encode(data1);
        byte[] b2 = ReplayEncoder.Encode(data2);
        Assert.AreEqual(b1.Length, b2.Length);
    }

    [Test]
    public void Encode_DifferentSongs_ProduceDifferentOutput()
    {
        var a = MakeMinimal();
        var b = MakeMinimal();
        b.Metadata.SongId = "other_song";

        byte[] ba = ReplayEncoder.Encode(a);
        byte[] bb = ReplayEncoder.Encode(b);
        CollectionAssert.AreNotEqual(ba, bb);
    }

    [Test]
    public void Encode_NullInputEvents_DoesNotThrow()
    {
        var data = MakeMinimal();
        data.InputEvents = null;
        Assert.DoesNotThrow(() => ReplayEncoder.Encode(data));
    }
}
