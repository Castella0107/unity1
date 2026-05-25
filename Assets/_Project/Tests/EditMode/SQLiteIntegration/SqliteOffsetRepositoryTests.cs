#if SQLITE_NET_PCL
using System;
using System.Threading.Tasks;
using NUnit.Framework;

/// <summary><see cref="SqliteOffsetRepository"/> の統合テスト(一時 SQLite DB を使用)。</summary>
public class SqliteOffsetRepositoryTests
{
    TempSqliteDb _temp;
    SqliteOffsetRepository _repo;

    [SetUp]
    public async Task SetUp()
    {
        _temp = new TempSqliteDb();
        _repo = new SqliteOffsetRepository();
        await _repo.InitializeAsync(_temp.FilePath);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _repo.CloseAsync();
        _temp?.Dispose();
    }

    // ── 初期化 ────────────────────────────────────────────────────────────────

    [Test]
    public async Task Initialize_CreatesDefaultProfile()
    {
        var def = await _repo.GetProfileByIdAsync("default");
        Assert.IsNotNull(def);
        Assert.AreEqual("Default", def.DisplayName);
    }

    [Test]
    public async Task Initialize_SetsActiveToDefault()
    {
        var activeId = await _repo.GetActiveProfileIdAsync();
        Assert.AreEqual("default", activeId);
    }

    // ── プロファイル保存 / 取得 ───────────────────────────────────────────────

    [Test]
    public async Task SaveProfile_PersistsAcrossInstances()
    {
        var p = new DeviceProfile
        {
            ProfileId           = "bt-1",
            DisplayName         = "BT Headset",
            OsDeviceName        = "Sony WH-1000XM5",
            IsAutoSwitchEnabled = true,
            Offsets             = new AppOffsetSettings { JudgmentOffsetMs = -28, VisualOffsetMs = -12 },
            CreatedAtUnixMs     = 1700000000000L,
            UpdatedAtUnixMs     = 1700000000000L,
        };
        await _repo.SaveProfileAsync(p);
        await _repo.CloseAsync();

        var repo2 = new SqliteOffsetRepository();
        await repo2.InitializeAsync(_temp.FilePath);
        var got = await repo2.GetProfileByIdAsync("bt-1");
        await repo2.CloseAsync();

        Assert.IsNotNull(got);
        Assert.AreEqual("BT Headset",        got.DisplayName);
        Assert.AreEqual("Sony WH-1000XM5",   got.OsDeviceName);
        Assert.IsTrue(got.IsAutoSwitchEnabled);
        Assert.AreEqual(-28, got.Offsets.JudgmentOffsetMs);
        Assert.AreEqual(-12, got.Offsets.VisualOffsetMs);
    }

    [Test]
    public async Task InsertOrReplace_OverwritesExisting()
    {
        await _repo.SaveProfileAsync(new DeviceProfile
        {
            ProfileId = "x", DisplayName = "First",
            Offsets = new AppOffsetSettings { JudgmentOffsetMs = 10, VisualOffsetMs = 5 },
        });
        await _repo.SaveProfileAsync(new DeviceProfile
        {
            ProfileId = "x", DisplayName = "Updated",
            Offsets = new AppOffsetSettings { JudgmentOffsetMs = 20, VisualOffsetMs = 15 },
        });

        var got = await _repo.GetProfileByIdAsync("x");
        Assert.AreEqual("Updated", got.DisplayName);
        Assert.AreEqual(20, got.Offsets.JudgmentOffsetMs);
    }

    [Test]
    public async Task GetAllProfiles_IncludesDefault()
    {
        await _repo.SaveProfileAsync(new DeviceProfile
        {
            ProfileId = "extra", DisplayName = "Extra",
            Offsets = AppOffsetSettings.Default,
        });

        var all = await _repo.GetAllProfilesAsync();
        Assert.GreaterOrEqual(all.Count, 2);
        Assert.IsTrue(all.Exists(p => p.ProfileId == "default"));
        Assert.IsTrue(all.Exists(p => p.ProfileId == "extra"));
    }

    [Test]
    public async Task GetProfileByOsDeviceName_FindsMatch()
    {
        await _repo.SaveProfileAsync(new DeviceProfile
        {
            ProfileId = "speakers", DisplayName = "Speakers",
            OsDeviceName = "Speakers (Realtek)", Offsets = AppOffsetSettings.Default,
        });
        await _repo.SaveProfileAsync(new DeviceProfile
        {
            ProfileId = "bt", DisplayName = "BT",
            OsDeviceName = "Sony WH", Offsets = AppOffsetSettings.Default,
        });

        var found = await _repo.GetProfileByOsDeviceNameAsync("Sony WH");
        Assert.IsNotNull(found);
        Assert.AreEqual("bt", found.ProfileId);
    }

    [Test]
    public async Task GetProfileByOsDeviceName_ReturnsNull_WhenNoMatch()
    {
        var found = await _repo.GetProfileByOsDeviceNameAsync("NonExistentDevice");
        Assert.IsNull(found);
    }

    // ── プロファイル削除 ──────────────────────────────────────────────────────

    [Test]
    public async Task DeleteDefault_IsRejected()
    {
        bool ok = await _repo.DeleteProfileAsync("default");
        Assert.IsFalse(ok);
        Assert.IsNotNull(await _repo.GetProfileByIdAsync("default"));
    }

    [Test]
    public async Task DeleteNonDefault_Succeeds()
    {
        await _repo.SaveProfileAsync(new DeviceProfile
        {
            ProfileId = "temp", DisplayName = "Temp",
            Offsets = AppOffsetSettings.Default,
        });
        bool ok = await _repo.DeleteProfileAsync("temp");
        Assert.IsTrue(ok);
        Assert.IsNull(await _repo.GetProfileByIdAsync("temp"));
    }

    // ── ActiveProfileId ───────────────────────────────────────────────────────

    [Test]
    public async Task SetActiveProfileId_Persists()
    {
        await _repo.SaveProfileAsync(new DeviceProfile
        {
            ProfileId = "newone", DisplayName = "New",
            Offsets = AppOffsetSettings.Default,
        });
        await _repo.SetActiveProfileIdAsync("newone");
        await _repo.CloseAsync();

        var repo2 = new SqliteOffsetRepository();
        await repo2.InitializeAsync(_temp.FilePath);
        var activeId = await repo2.GetActiveProfileIdAsync();
        await repo2.CloseAsync();

        Assert.AreEqual("newone", activeId);
    }

    // ── PerSongOffset ─────────────────────────────────────────────────────────

    [Test]
    public async Task PerSongOffset_ClampedOnSave()
    {
        await _repo.SavePerSongOffsetAsync(new PerSongOffset
        {
            SongId = "song1", JudgmentOffsetMs = 100  // ±50 超過
        });

        var got = await _repo.GetPerSongOffsetAsync("song1");
        Assert.AreEqual(50, got.JudgmentOffsetMs);
    }

    [Test]
    public async Task GetPerSongOffset_ReturnsDefault_WhenNotSaved()
    {
        var offset = await _repo.GetPerSongOffsetAsync("unknown_song");
        Assert.AreEqual(0, offset.JudgmentOffsetMs);
        Assert.AreEqual("unknown_song", offset.SongId);
    }

    [Test]
    public async Task PerSongOffset_Overwrites_OnReSave()
    {
        await _repo.SavePerSongOffsetAsync(new PerSongOffset { SongId = "s", JudgmentOffsetMs = 10 });
        await _repo.SavePerSongOffsetAsync(new PerSongOffset { SongId = "s", JudgmentOffsetMs = -20 });

        var got = await _repo.GetPerSongOffsetAsync("s");
        Assert.AreEqual(-20, got.JudgmentOffsetMs);
    }
}
#endif
