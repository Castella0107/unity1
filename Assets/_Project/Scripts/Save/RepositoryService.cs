using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

// Attach to a GameObject in _Persistent.unity.
// Initializes all storage repositories and exposes them as singletons.
public class RepositoryService : MonoBehaviour
{
    public static RepositoryService Instance { get; private set; }

    public IPlayRecordRepository PlayRecords    { get; private set; }
    public IOffsetRepository     Offsets        { get; private set; }
    public ReplayStorage         Replays        { get; private set; }
    public DeviceProfile         ActiveProfile  { get; private set; }
    public bool                  IsReady        { get; private set; }

    public event Action<DeviceProfile> OnActiveProfileChanged;

    async void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        await InitializeAsync();
    }

    async Task InitializeAsync()
    {
        try
        {
#if SQLITE_NET_PCL
            string dataDir  = Path.Combine(Application.persistentDataPath, "data");
            string playsDb  = Path.Combine(dataDir, "plays.db");
            string settingsDb = Path.Combine(dataDir, "settings.db");

            Debug.Log("[RepositoryService] persistentDataPath: " + Application.persistentDataPath);
            Debug.Log("[RepositoryService] plays.db path:    " + playsDb);
            Debug.Log("[RepositoryService] plays.db exists:  " + File.Exists(playsDb));
            Debug.Log("[RepositoryService] settings.db path: " + settingsDb);

            if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);

            // SQLitePCL must be initialized before first connection
            SQLitePCL.Batteries_V2.Init();

            var playRepo = new SqlitePlayRecordRepository();
            await playRepo.InitializeAsync(playsDb);
            PlayRecords = playRepo;

            var offsetRepo = new SqliteOffsetRepository();
            await offsetRepo.InitializeAsync(settingsDb);
            Offsets = offsetRepo;

            Debug.Log("[RepositoryService] SQLite repos initialized.");
#else
            Debug.LogWarning("[RepositoryService] SQLITE_NET_PCL not defined — using InMemory repositories. " +
                             "Add SQLITE_NET_PCL to Scripting Define Symbols (Project Settings > Player).");
            var playRepo = new InMemoryPlayRecordRepository();
            await playRepo.InitializeAsync(null);
            PlayRecords = playRepo;

            var offsetRepo = new InMemoryOffsetRepository();
            await offsetRepo.InitializeAsync(null);
            Offsets = offsetRepo;
#endif
            // One-time migrations
            await PlayerPrefsMigrator.MigrateIfNeeded(PlayRecords);
            await PlayerPrefsOffsetMigrator.MigrateIfNeeded(Offsets);

            // Replay file storage (no SQLite dependency)
            Replays = new ReplayStorage();
            Replays.Initialize();

            Debug.Log("[RepositoryService] ReplayStorage initialized. Count=" + Replays.GetReplayCount());

            // Restore active profile
            string activeId = await Offsets.GetActiveProfileIdAsync();
            ActiveProfile   = await Offsets.GetProfileByIdAsync(activeId)
                           ?? DeviceProfile.CreateDefault();

            IsReady = true;
            Debug.Log("[RepositoryService] Ready — profile: " + ActiveProfile.DisplayName
                      + "  replays: " + Replays.GetReplayCount());
        }
        catch (System.Exception e)
        {
            Debug.LogError("[RepositoryService] InitializeAsync FAILED: " + e.GetType().Name
                           + " — " + e.Message + "\n" + e.StackTrace);
            // Fallback to InMemory so the app stays functional
            if (PlayRecords == null)
            {
                var fallback = new InMemoryPlayRecordRepository();
                await fallback.InitializeAsync(null);
                PlayRecords = fallback;
            }
            if (Offsets == null)
            {
                var fallback = new InMemoryOffsetRepository();
                await fallback.InitializeAsync(null);
                Offsets = fallback;
            }
            if (Replays == null)
            {
                Replays = new ReplayStorage();
                Replays.Initialize();
            }
            IsReady = true;   // degraded mode — InMemory, not persisted
        }
    }

    /// Switch the active device profile (Phase 2 will also call this from auto-detection).
    public async Task<bool> SetActiveProfileAsync(string profileId)
    {
        var profile = await Offsets.GetProfileByIdAsync(profileId);
        if (profile == null) return false;
        await Offsets.SetActiveProfileIdAsync(profileId);
        ActiveProfile = profile;
        OnActiveProfileChanged?.Invoke(profile);
        return true;
    }
}
