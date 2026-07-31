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

    [Header("Note Sound")]
    [SerializeField] TMP_Dropdown _noteSoundDropdown;

    [Header("Hit Sounds")]
    [SerializeField] Toggle _judgmentSoundToggle;   // 判定ガイド音 (判定効果音) の ON/OFF

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        SetupVolumeSliders();
        SetupNoteSound();

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

        if (_judgmentSoundToggle != null)
        {
            _judgmentSoundToggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt("HitSoundJudgment", 1) == 1);
            _judgmentSoundToggle.onValueChanged.AddListener(v =>
            {
                // PLAY OPTIONS と同じ経路: シングルトン経由で反映し、未生成時は PlayerPrefs へ直接保存
                if (HitSoundPlayer.Instance != null) HitSoundPlayer.Instance.SetJudgmentSoundsEnabled(v);
                else { PlayerPrefs.SetInt("HitSoundJudgment", v ? 1 : 0); PlayerPrefs.Save(); }
            });
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

        // 数値クリックで直接入力 (K 指示 2026-07-31)
        void Editable(Slider s, TextMeshProUGUI label)
        {
            if (s == null || label == null) return;
            RhythmGame.UI.Common.ClickToEditValue.Attach(label, 0f, 100f, integer: true,
                get:    () => s.value,
                commit: v  => s.value = v);
        }
        Editable(_masterVolumeSlider, _masterVolumeValue);
        Editable(_musicVolumeSlider,  _musicVolumeValue);
        Editable(_sfxVolumeSlider,    _sfxVolumeValue);

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

    // ── ノーツ音 (タップ時の効果音) の選択 ────────────────────────────────────
    // 0 = スタンダード: air-chart (譜面エディタ) の試聴と同一サンプル (既定)
    // 1 = クリック: 従来の HitSoundLibrary シンセ音
    void SetupNoteSound()
    {
        if (_noteSoundDropdown == null) return;
        _noteSoundDropdown.ClearOptions();
        _noteSoundDropdown.AddOptions(new System.Collections.Generic.List<string>
        {
            "スタンダード (エディタと同じ)", "クリック (シンセ)"
        });
        _noteSoundDropdown.SetValueWithoutNotify(Mathf.Clamp(HitSoundPlayer.NoteSoundIdx, 0, 1));
        _noteSoundDropdown.RefreshShownValue();
        _noteSoundDropdown.onValueChanged.AddListener(idx =>
        {
            HitSoundPlayer.NoteSoundIdx = idx;
            HitSoundPlayer.Instance?.PreviewNoteSound(idx);   // 変更を即試聴
        });
    }
}
