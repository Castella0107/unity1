using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// コンフィグ画面「オーディオ」タブ (モック適用 2026-06-07 再編)。
/// 音量(全体/楽曲/効果音)、ウィンドウ切替ミュート、オーディオデバイスプロファイル(管理ボタン→モーダル)を担当する。
/// タイミング補正(判定/表示オフセット+キャリブレーション)はゲームプレイタブへ移動した。
/// </summary>
public class AudioTabController : MonoBehaviour
{
    [Header("Device Profile")]
    [SerializeField] TextMeshProUGUI _activeProfileNameLabel;
    [SerializeField] Button          _manageDevicesButton;
    [SerializeField] GameObject      _devicesPanelRoot;   // DevicesTabController が載ったモーダル

    [Header("Mute on Focus Loss")]
    [SerializeField] Toggle _muteOnFocusLossToggle;

    [Header("Volume")]
    [SerializeField] Slider          _masterVolumeSlider;
    [SerializeField] TextMeshProUGUI _masterVolumeValue;
    [SerializeField] Slider          _musicVolumeSlider;
    [SerializeField] TextMeshProUGUI _musicVolumeValue;
    [SerializeField] Slider          _sfxVolumeSlider;
    [SerializeField] TextMeshProUGUI _sfxVolumeValue;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        SetupVolumeSliders();

        if (_manageDevicesButton != null)
            _manageDevicesButton.onClick.AddListener(() =>
            {
                if (_devicesPanelRoot != null) _devicesPanelRoot.SetActive(true);
                else Debug.LogWarning("[AudioTab] DevicesPanel が未割り当て");
            });

        if (_muteOnFocusLossToggle != null)
        {
            _muteOnFocusLossToggle.SetIsOnWithoutNotify(MuteOnFocusLoss.Enabled);
            _muteOnFocusLossToggle.onValueChanged.AddListener(v => MuteOnFocusLoss.Enabled = v);
        }
    }

    void OnEnable()
    {
        RefreshProfileLabel();
        if (RepositoryService.Instance != null)
            RepositoryService.Instance.OnActiveProfileChanged += HandleProfileChanged;
    }

    void OnDisable()
    {
        if (RepositoryService.Instance != null)
            RepositoryService.Instance.OnActiveProfileChanged -= HandleProfileChanged;
    }

    void HandleProfileChanged(DeviceProfile profile) => RefreshProfileLabel();

    void RefreshProfileLabel()
    {
        if (_activeProfileNameLabel == null) return;
        var p = RepositoryService.Instance?.ActiveProfile;
        _activeProfileNameLabel.text = p != null ? p.DisplayName : "-";
    }

    // ── Volume ────────────────────────────────────────────────────────────────

    void SetupVolumeSliders()
    {
        void Init(Slider s, TextMeshProUGUI label, string key, float def)
        {
            if (s == null) return;
            s.minValue = 0; s.maxValue = 100; s.wholeNumbers = true;
            s.value    = PlayerPrefs.GetFloat(key, def);
            if (label != null) label.text = (int)s.value + "%";
        }

        Init(_masterVolumeSlider, _masterVolumeValue, "Vol_Master", 80f);
        Init(_musicVolumeSlider,  _musicVolumeValue,  "Vol_Music",  90f);
        Init(_sfxVolumeSlider,    _sfxVolumeValue,    "Vol_Sfx",    70f);

        _masterVolumeSlider?.onValueChanged.AddListener(v =>
        {
            if (_masterVolumeValue != null) _masterVolumeValue.text = (int)v + "%";
            PlayerPrefs.SetFloat("Vol_Master", v);
            PlayerPrefs.Save();
            AudioVolumeBinder.Instance?.SetMasterVolume(v);
        });
        _musicVolumeSlider?.onValueChanged.AddListener(v =>
        {
            if (_musicVolumeValue != null) _musicVolumeValue.text = (int)v + "%";
            PlayerPrefs.SetFloat("Vol_Music", v);
            PlayerPrefs.Save();
            AudioVolumeBinder.Instance?.SetMusicVolume(v);
        });
        _sfxVolumeSlider?.onValueChanged.AddListener(v =>
        {
            if (_sfxVolumeValue != null) _sfxVolumeValue.text = (int)v + "%";
            PlayerPrefs.SetFloat("Vol_Sfx", v);
            PlayerPrefs.Save();
            AudioVolumeBinder.Instance?.SetSfxVolume(v);
        });
    }
}
