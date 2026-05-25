using System.Threading.Tasks;
using NUnit.Framework;

/// <summary><see cref="InMemoryOffsetRepository"/> のユニットテスト。</summary>
public class InMemoryOffsetRepositoryTests
{
    IOffsetRepository _repo;

    [SetUp]
    public void Setup()
    {
        _repo = new InMemoryOffsetRepository();
        _repo.InitializeAsync(null).GetAwaiter().GetResult();
    }

    [Test]
    public async Task Initialize_CreatesDefaultProfile()
    {
        var def = await _repo.GetProfileByIdAsync("default");
        Assert.IsNotNull(def);
        Assert.AreEqual("Default", def.DisplayName);
        Assert.AreEqual(0, def.Offsets.JudgmentOffsetMs);
    }

    [Test]
    public async Task SaveAndGetProfile()
    {
        var p = new DeviceProfile
        {
            ProfileId   = "test1",
            DisplayName = "Test Profile",
            OsDeviceName = "BT-Test",
            Offsets     = new AppOffsetSettings { JudgmentOffsetMs = -28, VisualOffsetMs = -12 },
        };
        await _repo.SaveProfileAsync(p);

        var got = await _repo.GetProfileByIdAsync("test1");
        Assert.AreEqual("Test Profile", got.DisplayName);
        Assert.AreEqual(-28, got.Offsets.JudgmentOffsetMs);
        Assert.AreEqual(-12, got.Offsets.VisualOffsetMs);
    }

    [Test]
    public async Task GetProfileByOsDeviceName_ReturnsCorrectProfile()
    {
        await _repo.SaveProfileAsync(MakeProfile("p1", "Speakers"));
        await _repo.SaveProfileAsync(MakeProfile("p2", "BT-XYZ"));

        var got = await _repo.GetProfileByOsDeviceNameAsync("BT-XYZ");
        Assert.AreEqual("p2", got.ProfileId);
    }

    [Test]
    public async Task GetProfileByOsDeviceName_NoMatch_ReturnsNull()
    {
        var got = await _repo.GetProfileByOsDeviceNameAsync("nonexistent");
        Assert.IsNull(got);
    }

    [Test]
    public async Task DeleteDefaultProfile_Rejected()
    {
        bool ok  = await _repo.DeleteProfileAsync("default");
        var  def = await _repo.GetProfileByIdAsync("default");
        Assert.IsFalse(ok);
        Assert.IsNotNull(def);
    }

    [Test]
    public async Task DeleteNonDefaultProfile_Succeeds()
    {
        await _repo.SaveProfileAsync(MakeProfile("removable", null));
        bool ok  = await _repo.DeleteProfileAsync("removable");
        var  got = await _repo.GetProfileByIdAsync("removable");
        Assert.IsTrue(ok);
        Assert.IsNull(got);
    }

    [Test]
    public async Task ActiveProfileId_DefaultIsDefault()
    {
        string id = await _repo.GetActiveProfileIdAsync();
        Assert.AreEqual("default", id);
    }

    [Test]
    public async Task SetActiveProfileId_Persists()
    {
        await _repo.SaveProfileAsync(MakeProfile("newone", null));
        await _repo.SetActiveProfileIdAsync("newone");
        string id = await _repo.GetActiveProfileIdAsync();
        Assert.AreEqual("newone", id);
    }

    [Test]
    public async Task PerSongOffset_DefaultIsZero()
    {
        var o = await _repo.GetPerSongOffsetAsync("song1");
        Assert.AreEqual(0, o.JudgmentOffsetMs);
        Assert.AreEqual("song1", o.SongId);
    }

    [Test]
    public async Task PerSongOffset_SaveAndRetrieve()
    {
        await _repo.SavePerSongOffsetAsync(new PerSongOffset { SongId = "song1", JudgmentOffsetMs = 23 });
        var o = await _repo.GetPerSongOffsetAsync("song1");
        Assert.AreEqual(23, o.JudgmentOffsetMs);
    }

    [Test]
    public async Task PerSongOffset_ClampedToMaxMs()
    {
        await _repo.SavePerSongOffsetAsync(new PerSongOffset { SongId = "song1", JudgmentOffsetMs = 100 });
        var o = await _repo.GetPerSongOffsetAsync("song1");
        Assert.AreEqual(PerSongOffset.MaxMs, o.JudgmentOffsetMs);   // 50
    }

    [Test]
    public async Task PerSongOffset_ClampedToMinMs()
    {
        await _repo.SavePerSongOffsetAsync(new PerSongOffset { SongId = "song1", JudgmentOffsetMs = -100 });
        var o = await _repo.GetPerSongOffsetAsync("song1");
        Assert.AreEqual(PerSongOffset.MinMs, o.JudgmentOffsetMs);   // -50
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    static DeviceProfile MakeProfile(string id, string osName) => new DeviceProfile
    {
        ProfileId    = id,
        DisplayName  = id,
        OsDeviceName = osName,
        Offsets      = AppOffsetSettings.Default,
    };
}
