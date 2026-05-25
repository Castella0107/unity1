using NUnit.Framework;

/// <summary><see cref="AppOffsetSettings"/> のユニットテスト。</summary>
public class AppOffsetSettingsTests
{
    [Test]
    public void Default_IsZero()
    {
        var d = AppOffsetSettings.Default;
        Assert.AreEqual(0, d.JudgmentOffsetMs);
        Assert.AreEqual(0, d.VisualOffsetMs);
    }

    [Test]
    public void Clamped_RestrictsToMaxMs()
    {
        var s = new AppOffsetSettings { JudgmentOffsetMs = 500, VisualOffsetMs = 300 };
        var c = s.Clamped();
        Assert.AreEqual(AppOffsetSettings.MaxMs, c.JudgmentOffsetMs);
        Assert.AreEqual(AppOffsetSettings.MaxMs, c.VisualOffsetMs);
    }

    [Test]
    public void Clamped_RestrictsToMinMs()
    {
        var s = new AppOffsetSettings { JudgmentOffsetMs = -500, VisualOffsetMs = -300 };
        var c = s.Clamped();
        Assert.AreEqual(AppOffsetSettings.MinMs, c.JudgmentOffsetMs);
        Assert.AreEqual(AppOffsetSettings.MinMs, c.VisualOffsetMs);
    }

    [Test]
    public void Clamped_WithinRange_Unchanged()
    {
        var s = new AppOffsetSettings { JudgmentOffsetMs = -50, VisualOffsetMs = 100 };
        var c = s.Clamped();
        Assert.AreEqual(-50, c.JudgmentOffsetMs);
        Assert.AreEqual(100, c.VisualOffsetMs);
    }
}

/// <summary><see cref="PerSongOffset"/> のユニットテスト。</summary>
public class PerSongOffsetTests
{
    [Test]
    public void DefaultFor_IsZero()
    {
        var o = PerSongOffset.DefaultFor("song1");
        Assert.AreEqual("song1", o.SongId);
        Assert.AreEqual(0, o.JudgmentOffsetMs);
    }

    [Test]
    public void Clamped_RestrictsToMaxMs()
    {
        var o = new PerSongOffset { SongId = "x", JudgmentOffsetMs = 80 };
        Assert.AreEqual(50, o.Clamped().JudgmentOffsetMs);
    }

    [Test]
    public void Clamped_RestrictsToMinMs()
    {
        var o = new PerSongOffset { SongId = "x", JudgmentOffsetMs = -80 };
        Assert.AreEqual(-50, o.Clamped().JudgmentOffsetMs);
    }

    [Test]
    public void Clamped_WithinRange_Unchanged()
    {
        var o = new PerSongOffset { SongId = "x", JudgmentOffsetMs = 25 };
        Assert.AreEqual(25, o.Clamped().JudgmentOffsetMs);
    }

    [Test]
    public void Clamped_PreservesSongId()
    {
        var o = new PerSongOffset { SongId = "my_song", JudgmentOffsetMs = 100 };
        Assert.AreEqual("my_song", o.Clamped().SongId);
    }
}
