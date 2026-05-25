using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// Unity-independent. No UnityEngine references allowed in this assembly.
// Used in tests and as a fallback when SQLite is unavailable.
/// <summary>
/// <see cref="IOffsetRepository"/> のインメモリ実装。
/// テスト用途や SQLite が利用できない環境でのフォールバックとして使用する。
/// </summary>
public class InMemoryOffsetRepository : IOffsetRepository
{
    readonly Dictionary<string, DeviceProfile> _profiles = new Dictionary<string, DeviceProfile>();
    readonly Dictionary<string, PerSongOffset> _perSong  = new Dictionary<string, PerSongOffset>();
    string _activeProfileId = "default";

    /// <inheritdoc/>
    public Task InitializeAsync(string dbPath)
    {
        if (!_profiles.ContainsKey("default"))
            _profiles["default"] = DeviceProfile.CreateDefault();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<List<DeviceProfile>> GetAllProfilesAsync() =>
        Task.FromResult(_profiles.Values.ToList());

    /// <inheritdoc/>
    public Task<DeviceProfile> GetProfileByIdAsync(string profileId)
    {
        _profiles.TryGetValue(profileId, out var p);
        return Task.FromResult(p);
    }

    /// <inheritdoc/>
    public Task<DeviceProfile> GetProfileByOsDeviceNameAsync(string osDeviceName)
    {
        if (string.IsNullOrEmpty(osDeviceName))
            return Task.FromResult<DeviceProfile>(null);
        var match = _profiles.Values.FirstOrDefault(p => p.OsDeviceName == osDeviceName);
        return Task.FromResult(match);
    }

    /// <inheritdoc/>
    public Task<bool> SaveProfileAsync(DeviceProfile profile)
    {
        _profiles[profile.ProfileId] = profile;
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task<bool> DeleteProfileAsync(string profileId)
    {
        if (profileId == "default") return Task.FromResult(false);
        return Task.FromResult(_profiles.Remove(profileId));
    }

    /// <inheritdoc/>
    public Task<string> GetActiveProfileIdAsync() => Task.FromResult(_activeProfileId);

    /// <inheritdoc/>
    public Task<bool> SetActiveProfileIdAsync(string profileId)
    {
        _activeProfileId = profileId;
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task<PerSongOffset> GetPerSongOffsetAsync(string songId)
    {
        _perSong.TryGetValue(songId, out var o);
        return Task.FromResult(o ?? PerSongOffset.DefaultFor(songId));
    }

    /// <inheritdoc/>
    public Task<List<PerSongOffset>> GetAllPerSongOffsetsAsync() =>
        Task.FromResult(_perSong.Values.ToList());

    /// <inheritdoc/>
    public Task<bool> SavePerSongOffsetAsync(PerSongOffset offset)
    {
        _perSong[offset.SongId] = offset.Clamped();
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task<bool> DeleteAllPerSongOffsetsAsync()
    {
        _perSong.Clear();
        return Task.FromResult(true);
    }
}
