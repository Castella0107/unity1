using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// コンフィグ画面のデータタブを管理するコントローラー。
/// ストレージ使用量の表示、プレイ履歴の全削除・全データ消去（Danger Zone）、同期モード設定を担当する。
/// </summary>
public class DataTabController : MonoBehaviour
{
    [Header("Storage")]
    [SerializeField] TextMeshProUGUI _dataDirText;
    [SerializeField] TextMeshProUGUI _dbSizeText;
    [SerializeField] TextMeshProUGUI _songsSizeText;
    [SerializeField] TextMeshProUGUI _replaysSizeText;
    [SerializeField] Button          _refreshButton;

    [Header("Songs Library (Phase 2)")]
    [SerializeField] Button             _manageSongsButton;
    [SerializeField] Button             _reDownloadButton;
    [SerializeField] ManageSongsPanel   _manageSongsPanel;

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
        if (_manageSongsButton  != null) _manageSongsButton.onClick.AddListener(() =>
        {
            if (_manageSongsPanel != null) _manageSongsPanel.Open();
            else Debug.LogWarning("[Data] ManageSongsPanel が未割り当て");
        });
        if (_reDownloadButton   != null) _reDownloadButton.onClick.AddListener(
            () => Debug.Log("[Data] Re-download: Phase 4"));
        if (_exportButton       != null) _exportButton.onClick.AddListener(() => _ = ExportAsync());
        if (_importButton       != null) _importButton.onClick.AddListener(() => _ = ImportAsync());

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

    // ── Import ────────────────────────────────────────────────────────────────

    // 2 段階確認: 1回目で検証/プレビュー、5秒以内の 2 回目で実行
    const float ImportConfirmWindowSec = 5f;
    ExportSchema _pendingImportSchema;
    float        _pendingImportDeadline;

    async Task ImportAsync()
    {
        // 確認待ち中なら実行に進む
        if (_pendingImportSchema != null && Time.unscaledTime <= _pendingImportDeadline)
        {
            var schema = _pendingImportSchema;
            _pendingImportSchema = null;
            if (_importButton != null) _importButton.interactable = false;
            try
            {
                var r = await ImportService.RunAsync(schema);
                if (r.Success)
                    Debug.Log("[Data] Import OK plays=" + r.ImportedPlayRecords +
                              " profiles=" + r.ImportedProfiles +
                              " perSong=" + r.ImportedPerSongOffsets +
                              " prefs=" + r.ImportedPlayerPrefs);
                else
                    Debug.LogWarning("[Data] Import FAILED: " + r.FailureReason);
                await RefreshStats();
            }
            finally
            {
                if (_importButton != null) _importButton.interactable = true;
            }
            return;
        }

        // 1 回目: ファイル検出 + バリデーション
        string filePath = ImportService.FindLatestExportFile();
        if (filePath == null)
        {
            Debug.LogWarning("[Data] No export JSON found in " +
                System.IO.Path.Combine(Application.persistentDataPath, "exports"));
            return;
        }

        var preview = ImportService.LoadAndValidate(filePath);
        if (!preview.Success)
        {
            Debug.LogWarning("[Data] Import preview failed: " + preview.FailureReason);
            return;
        }

        _pendingImportSchema   = preview.Schema;
        _pendingImportDeadline = Time.unscaledTime + ImportConfirmWindowSec;
        Debug.LogWarning("[Data] Import preview — file: " + System.IO.Path.GetFileName(filePath) +
                         "  plays=" + (preview.Schema.PlayRecords?.Count ?? 0) +
                         "  profiles=" + (preview.Schema.DeviceProfiles?.Count ?? 0) +
                         "  perSong=" + (preview.Schema.PerSongOffsets?.Count ?? 0) +
                         "  bests=" + (preview.Schema.PersonalBests?.Count ?? 0) +
                         "  prefs=" + (preview.Schema.PlayerPrefs?.Count ?? 0) +
                         "  — click Import again within " + ImportConfirmWindowSec + "s to apply (REPLACES existing data)");
        await Task.CompletedTask;
    }

    // ── Export ────────────────────────────────────────────────────────────────

    async Task ExportAsync()
    {
        if (_exportButton != null) _exportButton.interactable = false;
        try
        {
            var result = await ExportService.RunAsync();
            if (result.Success)
            {
                Debug.Log("[Data] Export OK: " + result.FilePath);
                ExportService.RevealInFileExplorer(result.FilePath);
            }
            else
            {
                Debug.LogWarning("[Data] Export FAILED: " + result.FailureReason);
            }
        }
        finally
        {
            if (_exportButton != null) _exportButton.interactable = true;
        }
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
