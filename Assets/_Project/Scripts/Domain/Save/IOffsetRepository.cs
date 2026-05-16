using System.Collections.Generic;
using System.Threading.Tasks;

// Unity-independent. No UnityEngine references allowed in this assembly.
/// <summary>
/// デバイスプロファイルおよび楽曲ごとのオフセット設定を永続化するリポジトリの抽象インターフェース。
/// </summary>
public interface IOffsetRepository
{
    Task InitializeAsync(string dbPath);

    // DeviceProfile CRUD
    Task<List<DeviceProfile>> GetAllProfilesAsync();
    Task<DeviceProfile>       GetProfileByIdAsync(string profileId);
    Task<DeviceProfile>       GetProfileByOsDeviceNameAsync(string osDeviceName);
    Task<bool>                SaveProfileAsync(DeviceProfile profile);
    Task<bool>                DeleteProfileAsync(string profileId);

    // Active profile tracking
    Task<string> GetActiveProfileIdAsync();
    Task<bool>   SetActiveProfileIdAsync(string profileId);

    // Per-song offsets
    Task<PerSongOffset>       GetPerSongOffsetAsync(string songId);
    Task<List<PerSongOffset>> GetAllPerSongOffsetsAsync();
    Task<bool>                SavePerSongOffsetAsync(PerSongOffset offset);
    Task<bool>                DeleteAllPerSongOffsetsAsync();
}
