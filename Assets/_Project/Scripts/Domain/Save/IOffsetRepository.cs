using System.Collections.Generic;
using System.Threading.Tasks;

// Unity-independent. No UnityEngine references allowed in this assembly.
/// <summary>
/// デバイスプロファイルおよび楽曲ごとのオフセット設定を永続化するリポジトリの抽象インターフェース。
/// </summary>
public interface IOffsetRepository
{
    /// <summary>指定パスのデータストアを初期化する。</summary>
    Task InitializeAsync(string dbPath);

    /// <summary>全デバイスプロファイルを取得する。</summary>
    Task<List<DeviceProfile>> GetAllProfilesAsync();
    /// <summary>ID でデバイスプロファイルを取得する(無ければ null)。</summary>
    Task<DeviceProfile>       GetProfileByIdAsync(string profileId);
    /// <summary>OS デバイス名に紐付くプロファイルを取得する(自動切替用、無ければ null)。</summary>
    Task<DeviceProfile>       GetProfileByOsDeviceNameAsync(string osDeviceName);
    /// <summary>デバイスプロファイルを保存(新規/更新)する。</summary>
    Task<bool>                SaveProfileAsync(DeviceProfile profile);
    /// <summary>指定IDのデバイスプロファイルを削除する。</summary>
    Task<bool>                DeleteProfileAsync(string profileId);

    /// <summary>現在アクティブなプロファイルIDを取得する。</summary>
    Task<string> GetActiveProfileIdAsync();
    /// <summary>アクティブなプロファイルIDを設定する。</summary>
    Task<bool>   SetActiveProfileIdAsync(string profileId);

    /// <summary>指定楽曲の曲別オフセットを取得する。</summary>
    Task<PerSongOffset>       GetPerSongOffsetAsync(string songId);
    /// <summary>全楽曲の曲別オフセットを取得する。</summary>
    Task<List<PerSongOffset>> GetAllPerSongOffsetsAsync();
    /// <summary>曲別オフセットを保存する。</summary>
    Task<bool>                SavePerSongOffsetAsync(PerSongOffset offset);
    /// <summary>全ての曲別オフセットを削除する。</summary>
    Task<bool>                DeleteAllPerSongOffsetsAsync();
}
