using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

// Manages the link between OS audio devices and DeviceProfiles.
// Starts polling after RepositoryService is ready.
// Place in _Persistent.unity alongside RepositoryService and MainThreadDispatcher.
/// <summary>
/// OS のオーディオデバイスと DeviceProfile の紐付けを管理する DontDestroyOnLoad シングルトン。
/// RepositoryService の準備完了後にデバイス監視を開始し、デバイス変更時に自動プロファイル切り替えを行う。
/// </summary>
public class DeviceProfileService : MonoBehaviour
{
    public static DeviceProfileService Instance { get; private set; }

    IAudioDeviceMonitor _monitor;

    public string CurrentOsDeviceName => _monitor?.CurrentDeviceName;
    public bool   IsMonitoring        { get; private set; }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        StartCoroutine(WaitAndStart());
    }

    IEnumerator WaitAndStart()
    {
        while (RepositoryService.Instance == null || !RepositoryService.Instance.IsReady)
            yield return null;

        StartMonitoring();
    }

    void StartMonitoring()
    {
        if (IsMonitoring) return;

        // Platform-conditional monitor selection
#if UNITY_STANDALONE_WIN && NAUDIO
        _monitor = new WindowsAudioDeviceMonitor();
#else
        _monitor = new NoOpAudioDeviceMonitor();
#endif

        if (!_monitor.IsAvailable)
        {
            Debug.Log("[DeviceProfileService] Device monitoring not available on this platform");
            return;
        }

        _monitor.OnDeviceChanged += OnDeviceChanged;
        _monitor.Start();
        IsMonitoring = true;

        // Trigger initial profile match without waiting
        _ = HandleDeviceChangedAsync(_monitor.CurrentDeviceName);
    }

    void OnDestroy()
    {
        if (_monitor == null) return;
        _monitor.OnDeviceChanged -= OnDeviceChanged;
        _monitor.Stop();
    }

    // ── Event handler (called on main thread via MainThreadDispatcher) ─────────

    async void OnDeviceChanged(string newDeviceName)
    {
        Debug.Log("[DeviceProfileService] Device changed to: " + newDeviceName);
        await HandleDeviceChangedAsync(newDeviceName);
    }

    async Task HandleDeviceChangedAsync(string osDeviceName)
    {
        if (string.IsNullOrEmpty(osDeviceName)) return;

        var repo   = RepositoryService.Instance?.Offsets;
        var active = RepositoryService.Instance?.ActiveProfile;
        if (repo == null || active == null) return;

        // Already on the right profile
        if (active.OsDeviceName == osDeviceName) return;

        // Find a profile bound to this device with auto-switch enabled
        var matched = await repo.GetProfileByOsDeviceNameAsync(osDeviceName);
        if (matched != null && matched.IsAutoSwitchEnabled)
        {
            await RepositoryService.Instance.SetActiveProfileAsync(matched.ProfileId);
            Debug.Log("[DeviceProfileService] Auto-switched to profile: " + matched.DisplayName);
        }
        else
        {
            Debug.Log("[DeviceProfileService] No auto-switch profile for: " + osDeviceName);
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// Bind the currently active OS device to the given profile (call from Settings UI).
    public async Task<bool> AttachCurrentDeviceToProfileAsync(string profileId)
    {
        string current = CurrentOsDeviceName;
        if (string.IsNullOrEmpty(current)) return false;

        var repo    = RepositoryService.Instance?.Offsets;
        var profile = await repo?.GetProfileByIdAsync(profileId);
        if (profile == null) return false;

        profile.OsDeviceName    = current;
        profile.UpdatedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return await repo.SaveProfileAsync(profile);
    }
}
