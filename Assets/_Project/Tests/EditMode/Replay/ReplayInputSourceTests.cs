using System.Collections.Generic;
using NUnit.Framework;

public class ReplayInputSourceTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    static ReplayData MakeReplay(params (int deltaMsFromPrev, byte lane, byte action)[] events)
    {
        var list = new List<ReplayInputEvent>();
        foreach (var (d, l, a) in events)
            list.Add(new ReplayInputEvent { DeltaMsFromPrev = d, Lane = l, Action = a });
        return new ReplayData
        {
            Header      = new ReplayHeader(),
            Metadata    = new ReplayMetadata { SongId = "test", Difficulty = "easy",
                                               ChartHash = new byte[32], Modifiers = new string[0] },
            Result      = new ReplayResult { Rank = "D" },
            InputEvents = list,
        };
    }

    // ── Construction ──────────────────────────────────────────────────────────

    [Test]
    public void EmptyReplay_IsFinishedImmediately()
    {
        var src = new ReplayInputSource(MakeReplay());
        Assert.IsTrue(src.IsFinished);
        Assert.AreEqual(0, src.EventCount);
    }

    [Test]
    public void EmptyReplay_Advance_DoesNotThrow()
    {
        var src = new ReplayInputSource(MakeReplay());
        Assert.DoesNotThrow(() => src.Advance(99999));
    }

    [Test]
    public void NullReplay_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() => new ReplayInputSource(null));
    }

    // ── Advance / firing ──────────────────────────────────────────────────────

    [Test]
    public void ThreeEvents_Advance150_FiresOneEvent()
    {
        // Absolute times: 100, 200, 300
        var replay = MakeReplay((100, 0, 0), (100, 0, 1), (100, 1, 0));
        var src    = new ReplayInputSource(replay);

        int fireCount = 0;
        src.OnLaneDown += (l, t) => fireCount++;
        src.OnLaneUp   += (l, t) => fireCount++;

        src.Advance(150);

        Assert.AreEqual(1, fireCount);
        Assert.AreEqual(1, src.CursorIndex);
    }

    [Test]
    public void ThreeEvents_Advance300_FiresAll()
    {
        var replay = MakeReplay((100, 0, 0), (100, 0, 1), (100, 1, 0));
        var src    = new ReplayInputSource(replay);

        int fireCount = 0;
        src.OnLaneDown += (l, t) => fireCount++;
        src.OnLaneUp   += (l, t) => fireCount++;

        src.Advance(150);  // fires 1
        src.Advance(300);  // fires remaining 2

        Assert.AreEqual(3, fireCount);
        Assert.IsTrue(src.IsFinished);
    }

    // ── Boundary: exactly at timestamp ───────────────────────────────────────

    [Test]
    public void Advance_ExactlyAtTimestamp_Fires()
    {
        var replay = MakeReplay((100, 0, 0));
        var src    = new ReplayInputSource(replay);

        int count = 0;
        src.OnLaneDown += (l, t) => count++;

        src.Advance(100.0);

        Assert.AreEqual(1, count);
    }

    [Test]
    public void Advance_OneMillisecondBefore_DoesNotFire()
    {
        var replay = MakeReplay((100, 0, 0));
        var src    = new ReplayInputSource(replay);

        int count = 0;
        src.OnLaneDown += (l, t) => count++;

        src.Advance(99.0);

        Assert.AreEqual(0, count);
    }

    // ── Simultaneous events (same timestamp) ──────────────────────────────────

    [Test]
    public void TwoEventsAtSameTime_BothFire()
    {
        // Both at absolute 100ms
        var replay = MakeReplay((100, 0, 0), (0, 1, 0));
        var src    = new ReplayInputSource(replay);

        int count = 0;
        src.OnLaneDown += (l, t) => count++;

        src.Advance(100);

        Assert.AreEqual(2, count);
    }

    // ── IsDown / IsUp routing ────────────────────────────────────────────────

    [Test]
    public void ActionZero_RoutesToOnLaneDown()
    {
        var replay = MakeReplay((500, 2, 0));  // Action=0 → Down
        var src    = new ReplayInputSource(replay);

        int downCount = 0;
        int upCount   = 0;
        src.OnLaneDown += (l, t) => { downCount++; Assert.AreEqual(LaneRef.Lane2, l); };
        src.OnLaneUp   += (l, t) => upCount++;

        src.Advance(500);

        Assert.AreEqual(1, downCount);
        Assert.AreEqual(0, upCount);
    }

    [Test]
    public void ActionOne_RoutesToOnLaneUp()
    {
        var replay = MakeReplay((500, 3, 1));  // Action=1 → Up
        var src    = new ReplayInputSource(replay);

        int downCount = 0;
        int upCount   = 0;
        src.OnLaneDown += (l, t) => downCount++;
        src.OnLaneUp   += (l, t) => { upCount++; Assert.AreEqual(LaneRef.Lane3, l); };

        src.Advance(500);

        Assert.AreEqual(0, downCount);
        Assert.AreEqual(1, upCount);
    }

    // ── Timestamp passed to handler is absolute ───────────────────────────────

    [Test]
    public void FiredTimestamp_IsAbsolute_NotDelta()
    {
        // Delta-encoded: 200ms + 300ms = absolute 500ms for second event
        var replay = MakeReplay((200, 0, 0), (300, 0, 1));
        var src    = new ReplayInputSource(replay);

        double firedAt = -1;
        src.OnLaneUp += (l, t) => firedAt = t;

        src.Advance(9999);  // fire all

        Assert.AreEqual(500.0, firedAt, 0.001);
    }
}
