using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DisplayTabController : MonoBehaviour
{
    [Header("Resolution")]
    [SerializeField] TMP_Dropdown _resolutionDropdown;
    [SerializeField] TMP_Dropdown _screenModeDropdown;
    [SerializeField] TMP_Dropdown _fpsLimitDropdown;
    [SerializeField] Toggle       _vsyncToggle;

    [Header("Camera & Effects")]
    [SerializeField] TMP_Dropdown _cameraAngleDropdown;
    [SerializeField] TMP_Dropdown _bloomLevelDropdown;
    [SerializeField] Toggle       _motionEffectsToggle;
    [SerializeField] Toggle       _showFpsToggle;

    static readonly (int w, int h)[] Resolutions =
    {
        (1920, 1080), (2560, 1440), (3840, 2160), (1280, 720)
    };
    static readonly int[] FpsValues = { 60, 120, 144, 240, -1 };   // -1 = Unlimited

    void Start()
    {
        SetupDropdowns();
        LoadSettings();
        ApplyAllSettings();
    }

    void SetupDropdowns()
    {
        _resolutionDropdown.ClearOptions();
        _resolutionDropdown.AddOptions(new List<string> { "1920x1080", "2560x1440", "3840x2160", "1280x720" });
        _resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);

        _screenModeDropdown.ClearOptions();
        _screenModeDropdown.AddOptions(new List<string> { "Fullscreen", "Borderless Window", "Windowed" });
        _screenModeDropdown.onValueChanged.AddListener(OnScreenModeChanged);

        _fpsLimitDropdown.ClearOptions();
        _fpsLimitDropdown.AddOptions(new List<string> { "60", "120", "144", "240", "Unlimited" });
        _fpsLimitDropdown.onValueChanged.AddListener(OnFpsLimitChanged);

        _vsyncToggle.onValueChanged.AddListener(OnVsyncChanged);

        _cameraAngleDropdown.ClearOptions();
        _cameraAngleDropdown.AddOptions(new List<string> { "0° (flat)", "18° (mild)", "32° (steep)" });
        _cameraAngleDropdown.onValueChanged.AddListener(idx =>
        {
            PlayerPrefs.SetInt("CameraAngleIdx", idx);
            PlayerPrefs.Save();
        });

        _bloomLevelDropdown.ClearOptions();
        _bloomLevelDropdown.AddOptions(new List<string> { "Off", "Low", "Medium", "High" });
        _bloomLevelDropdown.onValueChanged.AddListener(OnBloomChanged);

        _motionEffectsToggle.onValueChanged.AddListener(v =>
        {
            PlayerPrefs.SetInt("MotionEffects", v ? 1 : 0);
            PlayerPrefs.Save();
        });

        _showFpsToggle.onValueChanged.AddListener(v =>
        {
            PlayerPrefs.SetInt("ShowFps", v ? 1 : 0);
            PlayerPrefs.Save();
            ApplyShowFps(v);
        });
    }

    void LoadSettings()
    {
        _resolutionDropdown.SetValueWithoutNotify(PlayerPrefs.GetInt("ResolutionIdx", 0));
        _screenModeDropdown.SetValueWithoutNotify(PlayerPrefs.GetInt("ScreenModeIdx", 1));   // Borderless
        _fpsLimitDropdown.SetValueWithoutNotify(PlayerPrefs.GetInt("FpsLimitIdx", 4));       // Unlimited
        _vsyncToggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt("VSync", 0) == 1);
        _cameraAngleDropdown.SetValueWithoutNotify(PlayerPrefs.GetInt("CameraAngleIdx", 2)); // 32°
        _bloomLevelDropdown.SetValueWithoutNotify(PlayerPrefs.GetInt("BloomLevelIdx", 2));   // Medium
        _motionEffectsToggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt("MotionEffects", 1) == 1);
        _showFpsToggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt("ShowFps", 0) == 1);
    }

    void ApplyAllSettings()
    {
        ApplyResolution(_resolutionDropdown.value, _screenModeDropdown.value);
        ApplyFpsAndVSync(_fpsLimitDropdown.value, _vsyncToggle.isOn);
        ApplyShowFps(_showFpsToggle.isOn);
    }

    void OnResolutionChanged(int idx)
    {
        PlayerPrefs.SetInt("ResolutionIdx", idx);
        PlayerPrefs.Save();
        ApplyResolution(idx, _screenModeDropdown.value);
    }

    void OnScreenModeChanged(int idx)
    {
        PlayerPrefs.SetInt("ScreenModeIdx", idx);
        PlayerPrefs.Save();
        ApplyResolution(_resolutionDropdown.value, idx);
    }

    static void ApplyResolution(int resIdx, int modeIdx)
    {
        if (resIdx < 0 || resIdx >= Resolutions.Length) return;
        var (w, h) = Resolutions[resIdx];
        FullScreenMode mode;
        switch (modeIdx)
        {
            case 0:  mode = FullScreenMode.ExclusiveFullScreen; break;
            case 1:  mode = FullScreenMode.FullScreenWindow;    break;
            case 2:  mode = FullScreenMode.Windowed;            break;
            default: mode = FullScreenMode.FullScreenWindow;    break;
        }
        Screen.SetResolution(w, h, mode);
    }

    void OnFpsLimitChanged(int idx)
    {
        PlayerPrefs.SetInt("FpsLimitIdx", idx);
        PlayerPrefs.Save();
        ApplyFpsAndVSync(idx, _vsyncToggle.isOn);
    }

    void OnVsyncChanged(bool enabled)
    {
        PlayerPrefs.SetInt("VSync", enabled ? 1 : 0);
        PlayerPrefs.Save();
        ApplyFpsAndVSync(_fpsLimitDropdown.value, enabled);
    }

    static void ApplyFpsAndVSync(int fpsIdx, bool vsync)
    {
        QualitySettings.vSyncCount = vsync ? 1 : 0;
        Application.targetFrameRate = vsync
            ? -1
            : (fpsIdx >= 0 && fpsIdx < FpsValues.Length ? FpsValues[fpsIdx] : 240);
    }

    void OnBloomChanged(int idx)
    {
        PlayerPrefs.SetInt("BloomLevelIdx", idx);
        PlayerPrefs.Save();
        // TODO Phase 2: Volume Profile から Bloom.intensity を設定
        Debug.Log("[Display] Bloom level → " + idx + " (実適用は Phase 2)");
    }

    static void ApplyShowFps(bool enabled)
    {
        var counter = FindObjectOfType<FpsCounter>();
        if (counter != null) counter.gameObject.SetActive(enabled);
    }

    /// Called from BootstrapController on app start (no MonoBehaviour needed).
    public static void ApplySettingsOnBoot()
    {
        int resIdx  = PlayerPrefs.GetInt("ResolutionIdx",  0);
        int modeIdx = PlayerPrefs.GetInt("ScreenModeIdx",  1);
        int fpsIdx  = PlayerPrefs.GetInt("FpsLimitIdx",    4);
        bool vsync  = PlayerPrefs.GetInt("VSync",          0) == 1;
        ApplyResolution(resIdx, modeIdx);
        ApplyFpsAndVSync(fpsIdx, vsync);
    }
}
