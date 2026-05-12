using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DataTabController : MonoBehaviour
{
    [Header("Storage")]
    [SerializeField] TextMeshProUGUI _dataDirText;
    [SerializeField] TextMeshProUGUI _dbSizeText;
    [SerializeField] TextMeshProUGUI _songsSizeText;
    [SerializeField] TextMeshProUGUI _replaysSizeText;
    [SerializeField] Button          _refreshButton;

    [Header("Songs Library (Phase 2)")]
    [SerializeField] Button _manageSongsButton;
    [SerializeField] Button _reDownloadButton;

    [Header("Backup (Phase 2)")]
    [SerializeField] Button _exportButton;
    [SerializeField] Button _importButton;

    [Header("Danger Zone — Clear History")]
    [SerializeField] Button          _clearHistoryButton;
    [SerializeField] TMP_InputField  _clearHistoryConfirmInput;
    [SerializeField] Button          _clearHistoryConfirmButton;

    [Header("Danger Zone — Clear All")]
    [SerializeField] Button          _clearAllButton;
    [SerializeField] TMP_InputField  _clearAllConfirmInput;
    [SerializeField] Button          _clearAllConfirmButton;

    [Header("Sync (Phase 4)")]
    [SerializeField] TMP_Dropdown _syncModeDropdown;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void OnEnable() => _ = RefreshStats();

    void Start()
    {
        SetupButtons();
        SetupSync();
    }

    void SetupButtons()
    {
        if (_refreshButton      != null) _refreshButton.onClick.AddListener(() => _ = RefreshStats());
        if (_manageSongsButton  != null) _manageSongsButton.onClick.AddListener(
            () => Debug.Log("[Data] Manage Songs: Phase 2"));
        if (_reDownloadButton   != null) _reDownloadButton.onClick.AddListener(
            () => Debug.Log("[Data] Re-download: Phase 4"));
        if (_exportButton       != null) _exportButton.onClick.AddListener(
            () => Debug.Log("[Data] Export: Phase 2"));
        if (_importButton       != null) _importButton.onClick.AddListener(
            () => Debug.Log("[Data] Import: Phase 2"));

        SetupDangerButton(_clearHistoryButton, _clearHistoryConfirmInput, _clearHistoryConfirmButton,
            "DELETE", ClearHistory);
        SetupDangerButton(_clearAllButton, _clearAllConfirmInput, _clearAllConfirmButton,
            "NUKE", ClearAll);
    }

    void SetupDangerButton(Button mainBtn, TMP_InputField confirmInput,
                           Button confirmBtn, string keyword, System.Func<Task> action)
    {
        if (mainBtn == null || confirmInput == null || confirmBtn == null) return;

        confirmInput.gameObject.SetActive(false);
        confirmBtn.gameObject.SetActive(false);

        mainBtn.onClick.AddListener(() =>
        {
            confirmInput.gameObject.SetActive(true);
            confirmBtn.gameObject.SetActive(true);
            confirmInput.text = "";
            var ph = confirmInput.placeholder?.GetComponent<TextMeshProUGUI>();
            if (ph != null) ph.text = "Type '" + keyword + "' to confirm";
            confirmBtn.interactable = false;
        });

        confirmInput.onValueChanged.AddListener(v =>
        {
            if (confirmBtn != null)
                confirmBtn.interactable = (v?.Trim().ToUpper() == keyword);
        });

        confirmBtn.onClick.AddListener(async () =>
        {
            await action();
            if (confirmInput != null)  { confirmInput.gameObject.SetActive(false); confirmInput.text = ""; }
            if (confirmBtn  != null)     confirmBtn.gameObject.SetActive(false);
            await RefreshStats();
        });
    }

    void SetupSync()
    {
        if (_syncModeDropdown == null) return;
        _syncModeDropdown.ClearOptions();
        _syncModeDropdown.AddOptions(new List<string> { "Automatic", "Manual" });
        _syncModeDropdown.SetValueWithoutNotify(PlayerPrefs.GetInt("SyncModeIdx", 0));
        _syncModeDropdown.onValueChanged.AddListener(idx =>
        {
            PlayerPrefs.SetInt("SyncModeIdx", idx); PlayerPrefs.Save();
        });
        _syncModeDropdown.interactable = false;  // Phase 4
    }

    // ── Stats ─────────────────────────────────────────────────────────────────

    async Task RefreshStats()
    {
        string dataDir = Path.Combine(Application.persistentDataPath, "data");
        if (_dataDirText != null) _dataDirText.text = dataDir;

        long dbSize = GetDirSize(dataDir);
        if (_dbSizeText != null) _dbSizeText.text = "Database Size: " + FormatBytes(dbSize);

        string songsRoot = Path.Combine(Application.streamingAssetsPath, "Songs");
        long songsSize = 0; int songsCount = 0;
        if (Directory.Exists(songsRoot))
        {
            songsSize  = GetDirSize(songsRoot);
            songsCount = Directory.GetDirectories(songsRoot).Length;
        }
        if (_songsSizeText != null)
            _songsSizeText.text = "Songs Library: " + FormatBytes(songsSize) + " / " + songsCount + " songs";

        long replaysSize = 0; int replaysCount = 0;
        var replaySvc = RepositoryService.Instance?.Replays;
        if (replaySvc != null)
        {
            replaysCount = replaySvc.GetReplayCount();
            replaysSize  = replaySvc.GetTotalSize();
        }
        if (_replaysSizeText != null)
            _replaysSizeText.text = "Replays: " + FormatBytes(replaysSize) +
                                    " / " + replaysCount + " files";

        await Task.CompletedTask;
    }

    // ── Danger zone actions ───────────────────────────────────────────────────

    async Task ClearHistory()
    {
        var repo = RepositoryService.Instance?.PlayRecords;
        if (repo == null) return;
        bool ok = await repo.DeleteAllAsync();
        Debug.Log("[Data] Clear history: " + (ok ? "success" : "failed"));
    }

    async Task ClearAll()
    {
        var playRepo   = RepositoryService.Instance?.PlayRecords;
        var offsetRepo = RepositoryService.Instance?.Offsets;

        if (playRepo != null) await playRepo.DeleteAllAsync();

        if (offsetRepo != null)
        {
            var profiles = await offsetRepo.GetAllProfilesAsync();
            foreach (var p in profiles)
                if (p.ProfileId != "default")
                    await offsetRepo.DeleteProfileAsync(p.ProfileId);
            await offsetRepo.SaveProfileAsync(DeviceProfile.CreateDefault());
            await offsetRepo.SetActiveProfileIdAsync("default");
        }

        ClearPlayerPrefsExceptMigrationFlags();

        Debug.LogWarning("[Data] All local data cleared (NUKE) — アプリ再起動を推奨");
    }

    static void ClearPlayerPrefsExceptMigrationFlags()
    {
        bool dbMig     = PlayerPrefs.GetInt("DBMigrationComplete_v1", 0)     == 1;
        bool offsetMig = PlayerPrefs.GetInt("OffsetMigrationComplete_v1", 0) == 1;
        PlayerPrefs.DeleteAll();
        if (dbMig)     PlayerPrefs.SetInt("DBMigrationComplete_v1",     1);
        if (offsetMig) PlayerPrefs.SetInt("OffsetMigrationComplete_v1", 1);
        PlayerPrefs.Save();
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    static long GetDirSize(string path)
    {
        if (!Directory.Exists(path)) return 0;
        try
        {
            long total = 0;
            foreach (string f in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                total += new FileInfo(f).Length;
            return total;
        }
        catch { return 0; }
    }

    static string FormatBytes(long bytes)
    {
        if (bytes < 1024)             return bytes + " B";
        if (bytes < 1024 * 1024)      return (bytes / 1024.0).ToString("F1") + " KB";
        if (bytes < 1024L * 1024 * 1024) return (bytes / (1024.0 * 1024.0)).ToString("F1") + " MB";
        return (bytes / (1024.0 * 1024.0 * 1024.0)).ToString("F2") + " GB";
    }
}
