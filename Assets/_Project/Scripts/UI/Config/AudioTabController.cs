using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// コンフィグ画面のオーディオタブを管理するコントローラー。
/// 判定オフセット・映像オフセットのスライダー操作と DeviceProfile への保存、マスター/音楽/SE の音量設定を担当する。
/// </summary>
public class AudioTabController : MonoBehaviour
{
    [Header("Active Profile")]
    [SerializeField] TextMeshProUGUI _activeProfileNameLabel;
    [SerializeField] Button          _changeProfileButton;

    [Header("Judgment Offset")]
    [SerializeField] TextMeshProUGUI _judgmentOffsetValue;
    [SerializeField] Slider          _judgmentOffsetSlider;
    [SerializeField] Button          _judgmentDecreaseButton;
    [SerializeField] Button          _judgmentIncreaseButton;
    [SerializeField] Button          _judgmentResetButton;

    [Header("Visual Offset")]
    [SerializeField] TextMeshProUGUI _visualOffsetValue;
    [SerializeField] Slider          _visualOffsetSlider;
    [SerializeField] Button          _visualDecreaseButton;
    [SerializeField] Button          _visualIncreaseButton;
    [SerializeField] Button          _visualResetButton;

    [Header("Calibration")]
    [SerializeField] Button _calibrateButton;

    [Header("Volume")]
    [SerializeField] Slider          _masterVolumeSlider;
    [SerializeField] TextMeshProUGUI _masterVolumeValue;
    [SerializeField] Slider          _musicVolumeSlider;
    [SerializeField] TextMeshProUGUI _musicVolumeValue;
    [SerializeField] Slider          _sfxVolumeSlider;
    [SerializeField] TextMeshProUGUI _sfxVolumeValue;

    ConfigController  _configController;
    DeviceProfile     _currentActiveProfile;
    bool              _suppressSliderEvents;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _configController = FindObjectOfType<ConfigController>();
    }

    void Start()
    {
        SetupOffsetSliders();
        SetupVolumeSliders();
        SetupButtons();
    }

    void OnEnable()
    {
        _ = RefreshAsync();
        if (RepositoryService.Instance != null)
            RepositoryService.Instance.OnActiveProfileChanged += HandleProfileChanged;
    }

    void OnDisable()
    {
        if (RepositoryService.Instance != null)
            RepositoryService.Instance.OnActiveProfileChanged -= HandleProfileChanged;
    }

    // ── Setup ─────────────────────────────────────────────────────────────────

    void SetupOffsetSliders()
    {
        void ConfigureOffsetSlider(Slider s, Action<int> onChange)
        {
            s.minValue     = AppOffsetSettings.MinMs;
            s.maxValue     = AppOffsetSettings.MaxMs;
            s.wholeNumbers = true;
            s.onValueChanged.AddListener(v => { if (!_suppressSliderEvents) onChange((int)v); });
        }

        ConfigureOffsetSlider(_judgmentOffsetSlider, v =>
        {
            _judgmentOffsetValue.text = v + " ms";
            _ = SaveOffsetsAsync();
        });

        ConfigureOffsetSlider(_visualOffsetSlider, v =>
        {
            _visualOffsetValue.text = v + " ms";
            _ = SaveOffsetsAsync();
        });
    }

    void SetupVolumeSliders()
    {
        void Init(Slider s, TextMeshProUGUI label, string key, float def)
        {
            s.minValue = 0; s.maxValue = 100; s.wholeNumbers = true;
            s.value    = PlayerPrefs.GetFloat(key, def);
            label.text = (int)s.value + "%";
        }

        Init(_masterVolumeSlider, _masterVolumeValue, "Vol_Master", 80f);
        Init(_musicVolumeSlider,  _musicVolumeValue,  "Vol_Music",  90f);
        Init(_sfxVolumeSlider,    _sfxVolumeValue,    "Vol_Sfx",    70f);

        _masterVolumeSlider.onValueChanged.AddListener(v =>
        {
            _masterVolumeValue.text = (int)v + "%";
            PlayerPrefs.SetFloat("Vol_Master", v);
            PlayerPrefs.Save();
            AudioVolumeBinder.Instance?.SetMasterVolume(v);
        });
        _musicVolumeSlider.onValueChanged.AddListener(v =>
        {
            _musicVolumeValue.text = (int)v + "%";
            PlayerPrefs.SetFloat("Vol_Music", v);
            PlayerPrefs.Save();
            AudioVolumeBinder.Instance?.SetMusicVolume(v);
        });
        _sfxVolumeSlider.onValueChanged.AddListener(v =>
        {
            _sfxVolumeValue.text = (int)v + "%";
            PlayerPrefs.SetFloat("Vol_Sfx", v);
            PlayerPrefs.Save();
            AudioVolumeBinder.Instance?.SetSfxVolume(v);
        });
    }

    void SetupButtons()
    {
        _judgmentDecreaseButton.onClick.AddListener(
            () => _judgmentOffsetSlider.value = Mathf.Max(_judgmentOffsetSlider.value - 1, AppOffsetSettings.MinMs));
        _judgmentIncreaseButton.onClick.AddListener(
            () => _judgmentOffsetSlider.value = Mathf.Min(_judgmentOffsetSlider.value + 1, AppOffsetSettings.MaxMs));
        _judgmentResetButton.onClick.AddListener(
            () => _judgmentOffsetSlider.value = 0);

        _visualDecreaseButton.onClick.AddListener(
            () => _visualOffsetSlider.value = Mathf.Max(_visualOffsetSlider.value - 1, AppOffsetSettings.MinMs));
        _visualIncreaseButton.onClick.AddListener(
            () => _visualOffsetSlider.value = Mathf.Min(_visualOffsetSlider.value + 1, AppOffsetSettings.MaxMs));
        _visualResetButton.onClick.AddListener(
            () => _visualOffsetSlider.value = 0);

        _changeProfileButton.onClick.AddListener(
            () => _configController?.SwitchToTab("Devices"));

        _calibrateButton.onClick.AddListener(
            () => Debug.Log("[AudioTab] Calibration: Phase 2 で実装予定"));
    }

    // ── Data ─────────────────────────────────────────────────────────────────

    async Task RefreshAsync()
    {
        var repo = RepositoryService.Instance;
        if (repo == null || !repo.IsReady) return;

        _currentActiveProfile = repo.ActiveProfile;
        if (_currentActiveProfile == null) return;

        if (_activeProfileNameLabel != null)
            _activeProfileNameLabel.text = _currentActiveProfile.DisplayName;

        _suppressSliderEvents = true;
        if (_judgmentOffsetSlider != null)
            _judgmentOffsetSlider.SetValueWithoutNotify(_currentActiveProfile.Offsets.JudgmentOffsetMs);
        if (_visualOffsetSlider != null)
            _visualOffsetSlider.SetValueWithoutNotify(_currentActiveProfile.Offsets.VisualOffsetMs);
        _suppressSliderEvents = false;

        if (_judgmentOffsetValue != null)
            _judgmentOffsetValue.text = _currentActiveProfile.Offsets.JudgmentOffsetMs + " ms";
        if (_visualOffsetValue != null)
            _visualOffsetValue.text = _currentActiveProfile.Offsets.VisualOffsetMs + " ms";
    }

    void HandleProfileChanged(DeviceProfile profile) => _ = RefreshAsync();

    async Task SaveOffsetsAsync()
    {
        var repo = RepositoryService.Instance?.Offsets;
        if (repo == null || _currentActiveProfile == null) return;

        var updated = new DeviceProfile
        {
            ProfileId           = _currentActiveProfile.ProfileId,
            DisplayName         = _currentActiveProfile.DisplayName,
            OsDeviceName        = _currentActiveProfile.OsDeviceName,
            IsAutoSwitchEnabled = _currentActiveProfile.IsAutoSwitchEnabled,
            Offsets = new AppOffsetSettings
            {
                JudgmentOffsetMs = (int)_judgmentOffsetSlider.value,
                VisualOffsetMs   = (int)_visualOffsetSlider.value,
            },
            CreatedAtUnixMs = _currentActiveProfile.CreatedAtUnixMs,
            UpdatedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };

        bool ok = await repo.SaveProfileAsync(updated);
        if (ok)
        {
            _currentActiveProfile = updated;
            // Fire OnActiveProfileChanged so AudioConductor and other listeners update
            await RepositoryService.Instance.SetActiveProfileAsync(updated.ProfileId);
        }
    }
}
