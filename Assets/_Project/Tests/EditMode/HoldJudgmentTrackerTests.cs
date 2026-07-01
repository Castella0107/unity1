using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary><see cref="HoldJudgmentTracker"/> のユニットテスト。</summary>
public class HoldJudgmentTrackerTests
{
    static BpmTimeline Bpm120 => new BpmTimeline(new[]
    {
        new TempoEvent { Type = "bpm", TimeMs = 0, Bpm = 120 }
    });

    static HoldJudgmentTracker MakeTracker(double startMs, double durationMs) =>
        new HoldJudgmentTracker(
            new NoteData { Id = 1, Type = NoteType.Hold, Lane = LaneRef.Lane0,
                           TimeMs = startMs, DurationMs = durationMs },
            Bpm120);

    [Test]
    public void HoldTickInterval_120Bpm_Is250ms()
    {
        // 120 BPM → measure 2000 ms; 8 ticks per measure → tick interval 250 ms (1 eighth)
        Assert.AreEqual(250.0, Bpm120.GetHoldTickIntervalMs(0), 0.01);
    }

    [Test]
    public void TickTimes_AreHoldTickSpaced()
    {
        // 5000 ms hold at 120 BPM (tick = 250 ms) → ticks at 250,500,...,4750 = 19 ticks
        var tracker = MakeTracker(0, 5000);
        Assert.AreEqual(19, tracker.TickTimes.Count);
        Assert.AreEqual(250.0,  tracker.TickTimes[0],  0.01);
        Assert.AreEqual(4750.0, tracker.TickTimes[18], 0.01);
    }

    [Test]
    public void TickTimes_EndOnBoundary_ExcludesEndTick_TailWins()
    {
        // 2000 ms hold at 120 BPM = exactly 8 tick intervals. The boundary tick at 2000
        // (= end) is dropped so the tail owns the end (no double combo). 250..1750 remain.
        var tracker = MakeTracker(0, 2000);
        Assert.AreEqual(7, tracker.TickTimes.Count);
        Assert.AreEqual(250.0,  tracker.TickTimes[0], 0.01);
        Assert.AreEqual(1750.0, tracker.TickTimes[6], 0.01);
    }

    [Test]
    public void OnHeadInput_WithinWindow_ReturnsJudgment()
    {
        var tracker = MakeTracker(1000, 500);
        var j = tracker.OnHeadInput(1000);
        Assert.AreEqual(Judgment.PerfectPlus, j);
        Assert.IsTrue(tracker.IsHeadJudged);
    }

    [Test]
    public void OnHeadInput_OutsideWindow_ReturnsNull()
    {
        var tracker = MakeTracker(1000, 500);
        var j = tracker.OnHeadInput(1000 + JudgmentWindow.GoodMs + 1);
        Assert.IsNull(j);
        Assert.IsFalse(tracker.IsHeadJudged);
    }

    [Test]
    public void AdvanceTo_AllHeld_ReturnsAllPerfectPlus()
    {
        var tracker = MakeTracker(0, 5000);   // ticks at 250,500,...,4750
        tracker.OnHeadInput(0);
        var ticks = tracker.AdvanceTo(5000).ToList();
        Assert.IsTrue(ticks.Count > 0);
        Assert.IsTrue(ticks.All(t => t.Judgment == Judgment.PerfectPlus));
    }

    [Test]
    public void HeadNotJudged_AdvanceTo_YieldsNothing()
    {
        var tracker = MakeTracker(0, 5000);
        // head NOT judged — AdvanceTo must yield nothing even though ticks exist
        var ticks = tracker.AdvanceTo(5000).ToList();
        Assert.AreEqual(0, ticks.Count);
    }

    [Test]
    public void GuardWindow_AllowsBriefRelease()
    {
        var tracker = MakeTracker(0, 2500);   // ticks every 250 ms (250,500,...,2250)
        tracker.OnHeadInput(0);
        tracker.OnReleased(1970);             // release 30 ms before the 2000 tick (within 50 ms guard)
        var ticks = tracker.AdvanceTo(2100).ToList();
        Assert.IsTrue(ticks.Count > 0);
        Assert.IsTrue(ticks.All(t => t.Judgment == Judgment.PerfectPlus));
        Assert.IsFalse(tracker.IsAbandoned);
    }

    [Test]
    public void GuardExceeded_TicksMiss_ButNotAbandoned()
    {
        // Released and never re-pressed: ticks during the drop are Miss, but the hold is
        // NOT abandoned (it stays recoverable). Only re-pressing would resume it.
        var tracker = MakeTracker(0, 5000);   // ticks at 250,500,...,4750
        tracker.OnHeadInput(0);
        tracker.OnReleased(520);              // released just after the 500 tick, before 750
        var ticks = tracker.AdvanceTo(4500).ToList();
        Assert.IsTrue(ticks.Any(t => t.Judgment == Judgment.Miss));
        Assert.IsFalse(tracker.IsAbandoned);
    }

    [Test]
    public void Recovery_RepressAfterDrop_FirstTickGreatThenPerfectPlus()
    {
        // Drop the hold past the guard, then re-press: the first tick after recovery is
        // Great, and the tick after that returns to PerfectPlus.
        var tracker = MakeTracker(0, 5000);   // ticks at 250,500,750,1000,...
        tracker.OnHeadInput(0);

        Assert.AreEqual(Judgment.PerfectPlus, tracker.AdvanceTo(300).Single().Judgment);   // 250: held

        tracker.OnReleased(300);
        Assert.AreEqual(Judgment.Miss, tracker.AdvanceTo(600).Single().Judgment);          // 500: dropped

        tracker.OnPressed(620);               // re-press 320 ms after release → recovery
        Assert.AreEqual(Judgment.Great,       tracker.AdvanceTo(800).Single().Judgment);   // 750: first after recovery
        Assert.AreEqual(Judgment.PerfectPlus, tracker.AdvanceTo(1050).Single().Judgment);  // 1000: back to P+
        Assert.IsFalse(tracker.IsAbandoned);
    }

    [Test]
    public void ResolveTail_HeldThrough_ReturnsPerfectPlus()
    {
        // No release at all — holding through to the end: real ticks are P+, so the tail
        // copies P+.
        var tracker = MakeTracker(0, 5000);
        tracker.OnHeadInput(0);
        tracker.AdvanceTo(5000).ToList();     // real ticks all P+
        var j = tracker.ResolveTail(5000);
        Assert.AreEqual(Judgment.PerfectPlus, j);
        Assert.IsTrue(tracker.IsTailJudged);
    }

    [Test]
    public void ResolveTail_ReleasedInZone_CopiesLastRealTick_PerfectPlus()
    {
        // 最後の実ティックまで押下 → 無敵ゾーンで離す。尾は直前の実ティック(P+)を複製する。
        var tracker = MakeTracker(0, 5000);   // real ticks 250..4500, invincible tick 4750
        tracker.OnHeadInput(0);
        tracker.AdvanceTo(4600).ToList();     // held through all real ticks (P+)
        tracker.OnReleased(4600);             // release inside the final 1/8 zone
        var j = tracker.ResolveTail(5000);
        Assert.AreEqual(Judgment.PerfectPlus, j);
    }

    [Test]
    public void ResolveTail_ReleasedEarlyStayReleased_CopiesMiss()
    {
        // ゾーン手前で離してコンボが切れていた(直前の実ティック=Miss)場合、尾も Miss を複製する。
        var tracker = MakeTracker(0, 5000);
        tracker.OnHeadInput(0);
        tracker.OnReleased(100);              // released far too early, never re-pressed
        tracker.AdvanceTo(4600).ToList();     // real ticks 250..4500 → Miss; _lastTickJudgment = Miss
        var j = tracker.ResolveTail(5000);
        Assert.AreEqual(Judgment.Miss, j);
        Assert.IsTrue(tracker.IsCompleted);
    }

    [Test]
    public void AdvanceTo_LastTick_ReleasedInZone_CopiesLastRealTick()
    {
        // 最終ボディティック(無敵)は押下/離上を再評価せず、直前の実ティック(P+)を複製する。
        var tracker = MakeTracker(0, 5000);   // real ticks 250..4500 (idx0..17), invincible idx18@4750
        tracker.OnHeadInput(0);
        tracker.AdvanceTo(4600).ToList();     // held through real ticks (P+)
        tracker.OnReleased(4600);             // release inside the zone
        var last = tracker.AdvanceTo(5000).Single();
        Assert.AreEqual(4750.0, last.TickTimeMs, 0.01);
        Assert.AreEqual(Judgment.PerfectPlus, last.Judgment);   // copies P+ despite release
    }

    [Test]
    public void AdvanceTo_LastTick_ReleasedEarly_CopiesMiss()
    {
        // ゾーン手前で離していれば、最終ボディティックも直前の Miss を複製する(コンボ断のまま)。
        var tracker = MakeTracker(0, 5000);   // real ticks 250..4500 (idx0..17), invincible idx18@4750
        tracker.OnHeadInput(0);
        tracker.OnReleased(100);              // released early, never re-pressed
        var ticks = tracker.AdvanceTo(5000).ToList();
        Assert.AreEqual(4750.0, ticks.Last().TickTimeMs, 0.01);
        Assert.IsTrue(ticks.All(t => t.Judgment == Judgment.Miss));   // invincible tick copies Miss
    }

    [Test]
    public void TerminalHold_RecoversFromMiss_LastTickIsGreat()
    {
        // 直前が Miss(手前でコンボ断)でも、終端の無敵ゾーンで押していれば復帰判定どおり復帰する:
        // 最終ボディティックは復帰直後なので Great、尾は P+ に戻る。
        var tracker = MakeTracker(0, 1250);   // ticks 250,500,750,1000 (idx0..3); invincible idx3@1000
        tracker.OnHeadInput(0);
        tracker.AdvanceTo(300).ToList();      // 250: P+
        tracker.OnReleased(300);
        tracker.AdvanceTo(950).ToList();      // 500,750: Miss (last real tick = Miss)
        tracker.OnPressed(970);               // re-press inside the zone → recovery
        var last = tracker.AdvanceTo(1050).Single();   // 1000: held → recovers → Great
        Assert.AreEqual(Judgment.Great, last.Judgment);
        Assert.AreEqual(Judgment.PerfectPlus, tracker.ResolveTail(1250));   // tail held → back to P+
    }

    [Test]
    public void TerminalHold_RecoversFromMiss_TailIsGreat()
    {
        // 最終ボディティックまで離していて(Miss複製)、尾の直前で押し直した場合、
        // 尾が復帰後の最初の判定になるので Great で復帰する。
        var tracker = MakeTracker(0, 1250);   // ticks 250,500,750,1000; invincible idx3@1000
        tracker.OnHeadInput(0);
        tracker.OnReleased(100);              // dropped early
        var ticks = tracker.AdvanceTo(1050).ToList();   // 250..1000 all Miss (idx3 zone copies Miss)
        Assert.AreEqual(Judgment.Miss, ticks.Last().Judgment);
        tracker.OnPressed(1100);             // re-press between final tick and tail → recovery
        Assert.AreEqual(Judgment.Great, tracker.ResolveTail(1250));
    }

    [Test]
    public void SingleTickHold_InvincibleTick_FallsBackToHead()
    {
        // ボディティックが 1 本だけ(それ自身が無敵)のホールドは、複製元の実ティックが無いので
        // ヘッド判定にフォールバックする(ここでは Perfect ヘッド → Perfect)。
        var tracker = MakeTracker(1000, 400);   // tick 1250 only (idx0 == invincible); end 1400
        Assert.AreEqual(1, tracker.TickTimes.Count);
        tracker.OnHeadInput(1020);              // delta 20ms → Perfect head
        tracker.OnReleased(1050);               // released early
        var last = tracker.AdvanceTo(1300).Single();
        Assert.AreEqual(Judgment.Perfect, last.Judgment);          // falls back to head
        Assert.AreEqual(Judgment.Perfect, tracker.ResolveTail(1400));
    }

    [Test]
    public void ShortHold_HeadHit_TailIsPerfectPlus_EvenWhenReleased()
    {
        // 1/8 以下(ボディティック無し)のホールド: 支点が通っていれば離上しても尾は無条件 P+。
        var tracker = MakeTracker(0, 250);    // 250 ms @120bpm = 1/8 → 0 body ticks
        Assert.AreEqual(0, tracker.TickTimes.Count);
        tracker.OnHeadInput(0);               // head P+
        tracker.OnReleased(50);               // released early
        var j = tracker.ResolveTail(250);
        Assert.AreEqual(Judgment.PerfectPlus, j);
    }

    [Test]
    public void ShortHold_PerfectHead_TailIsFlatPerfectPlus()
    {
        // 短いホールドは支点が P でも尾は P+ 固定(通常ホールドの「踏襲」とは別扱い)。
        var tracker = MakeTracker(1000, 200);  // 0 body ticks
        Assert.AreEqual(0, tracker.TickTimes.Count);
        tracker.OnHeadInput(1020);             // Perfect head
        var j = tracker.ResolveTail(1200);
        Assert.AreEqual(Judgment.PerfectPlus, j);
    }

    [Test]
    public void ShortHold_HeadMissed_TailIsNull()
    {
        // 支点が MISS の短いホールドは放棄され、尾は解決しない(頭のオートミスで MISS 済み)。
        var tracker = MakeTracker(1000, 200);
        tracker.OnHeadMissed(1000 + JudgmentWindow.GoodMs + 1);
        Assert.IsTrue(tracker.IsAbandoned);
        Assert.IsNull(tracker.ResolveTail(1200));
    }

    [Test]
    public void ResolveTail_BeforeEnd_ReturnsNull()
    {
        var tracker = MakeTracker(0, 5000);
        tracker.OnHeadInput(0);
        Assert.IsNull(tracker.ResolveTail(4999));
        Assert.IsFalse(tracker.IsTailJudged);
    }

    [Test]
    public void HeadMissed_SetsAbandoned()
    {
        var tracker = MakeTracker(1000, 500);
        bool missed = tracker.OnHeadMissed(1000 + JudgmentWindow.GoodMs + 1);
        Assert.IsTrue(missed);
        Assert.IsTrue(tracker.IsAbandoned);
        Assert.IsTrue(tracker.IsHeadJudged);
    }
}
