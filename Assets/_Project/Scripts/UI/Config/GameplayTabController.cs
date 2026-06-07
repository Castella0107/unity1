using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// コンフィグ画面「ゲームプレイ」タブ (モック適用 2026-06-07 再編)。
/// 旧 Game タブ(ハイスピード/コンボ境界/FAST-SLOW/エフェクト)と
/// 旧 Audio タブのタイミング補正(判定/表示オフセット+キャリブレーション)を統合する。
/// オフセットは アクティブ DeviceProfile に保存(プロファイル切替はオーディオタブのデバイス管理から)。
/// </summary>
public class GameplayTabController : MonoBehaviour
{
    [Header("Hi-Speed")]
    [SerializeField] Slider          _hiSpeedSlider;
    [SerializeField] TextMeshProUGUI _hiSpeedValue;

    [Header("Timing — Judgment Offset (音声再生タイミング補正相当)")]
    [SerializeField] Slider          _judgmentOffsetSlider;
    [SerializeField] TextMeshProUGUI _judgmentOffsetValue;

    [Header("Timing — Visual Offset (ノート生成タイミング補正相当)")]
    [SerializeField] Slider          _visualOffsetSlider;
    [SerializeField] TextMeshProUGUI _visualOffsetValue;

    [Header("Calibration")]
    [SerializeField] Button           _calibrateButton;
    [SerializeField] CalibrationPanel _calibrationPanel;

    [Header("Judgement / Display")]
    [SerializeField] TMP_Dropdown _comboBorderDropdown;
    [SerializeField] Toggle       _fastLateToggle;

    [Header("Effects")]
    [SerializeField] Slider          _backgroundEffectsSlider;
    [SerializeField] TextMeshProUGUI _backgroundEffectsValue;
    [SerializeField] TMP_Dropdown    _judgmentEffectDropdown;

    DeviceProfile _currentActiveProfile;
    bool          _suppressSliderEvents;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        SetupHiSpeed();
        SetupOffsets();
        SetupJudgementRows();
        SetupEffects();
        LoadPrefs();
    }

    void OnEnable()
    {
        _ = RefreshOffsetsAsync();
        if (RepositoryService.Instance != null)
            RepositoryService.Instance.OnActiveProfileChanged += HandleProfileChanged;
    }

    void OnDisable()
    {
        if (RepositoryService.Instance != null)
            RepositoryService.Instance.OnActiveProfileChanged -= HandleProfileChanged;
    }

    // ── Setup ─────────────────────────────────────────────────────────────────

    void SetupHiSpeed()
    {
        if (_hiSpeedSlider == null) return;
        _hiSpeedSlider.minValue = 0.5f;
        _hiSpeedSlider.maxValue = 20.0f;
        _hiSpeedSlider.onValueChanged.AddListener(v =>
        {
            if (_hiSpeedValue != null) _hiSpeedValue.text = v.ToString("F1");
            PlayerPrefs.SetFloat("HiSpeed", v);
            PlayerPrefs.Save();
        });
    }

    void SetupOffsets()
    {
        void Configure(Slider s, TextMeshProUGUI label)
        {
            if (s == null) return;
            s.minValue     = AppOffsetSettings.MinMs;
            s.maxValue     = AppOffsetSettings.MaxMs;
            s.wholeNumbers = true;
            s.onValueChanged.AddListener(v =>
            {
                if (label != null) label.text = (int)v + " ms";
                if (!_suppressSliderEvents) _ = SaveOffsetsAsync();
            });
        }
        Configure(_judgmentOffsetSlider, _judgmentOffsetValue);
        Configure(_visualOffsetSlider,   _visualOffsetValue);

        if (_calibrateButton != null)
            _calibrateButton.onClick.AddListener(() =>
            {
                if (_calibrationPanel != null) _calibrationPanel.Open();
                else Debug.LogWarning("[GameplayTab] CalibrationPanel が未割り当て");
            });
    }

    void SetupJudgementRows()
    {
        if (_comboBorderDropdown != null)
        {
            _comboBorderDropdown.ClearOptions();
            _comboBorderDropdown.AddOptions(new List<string>
            {
                "Good or better", "Great or better", "Perfect or better", "Perfect+ only"
            });
            _comboBorderDropdown.onValueChanged.AddListener(idx =>
            {
                PlayerPrefs.SetInt("ComboBorderIdx", idx);
                PlayerPrefs.Save();
            });
        }

        if (_fastLateToggle != null)
            _fastLateToggle.onValueChanged.AddListener(v =>
            {
                PlayerPrefs.SetInt("ShowFastLate", v ? 1 : 0);
                PlayerPrefs.Save();
            });
    }

    void SetupEffects()
    {
        if (_backgroundEffectsSlider != null)
        {
            _backgroundEffectsSlider.minValue     = 0;
            _backgroundEffectsSlider.maxValue     = 100;
            _backgroundEffectsSlider.wholeNumbers = true;
            _backgroundEffectsSlider.onValueChanged.AddListener(v =>
            {
                if (_backgroundEffectsValue != null) _backgroundEffectsValue.text = (int)v + "%";
                PlayerPrefs.SetFloat("BgEffectsIntensity", v);
                PlayerPrefs.Save();
                JacketBackgroundController.Instance?.SetBrightness((v / 100f) * 0.5f);
                BeatGridController.Instance?.SetUserIntensity(v / 100f);
            });
        }

        if (_judgmentEffectDropdown != null)
        {
            _judgmentEffectDropdown.ClearOptions();
            _judgmentEffectDropdown.AddOptions(new List<string> { "Subtle", "Normal", "Bold" });
            _judgmentEffectDropdown.onValueChanged.AddListener(idx =>
            {
                PlayerPrefs.SetInt("JudgmentEffectStyleIdx", idx);
                PlayerPrefs.Save();
            });
        }
    }

    void LoadPrefs()
    {
        if (_hiSpeedSlider != null)
        {
            _hiSpeedSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("HiSpeed", 4.5f));
            if (_hiSpeedValue != null) _hiSpeedValue.text = _hiSpeedSlider.value.ToString("F1");
        }

        if (_comboBorderDropdown != null)
            _comboBorderDropdown.SetValueWithoutNotify(PlayerPrefs.GetInt("ComboBorderIdx", 0));
        if (_fastLateToggle != null)
            _fastLateToggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt("ShowFastLate", 1) == 1);

        if (_backgroundEffectsSlider != null)
        {
            _backgroundEffectsSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("BgEffectsIntensity", 100f));
            if (_backgroundEffectsValue != null)
                _backgroundEffectsValue.text = (int)_backgroundEffectsSlider.value + "%";
        }

        if (_judgmentEffectDropdown != null)
            _judgmentEffectDropdown.SetValueWithoutNotify(PlayerPrefs.GetInt("JudgmentEffectStyleIdx", 1));
    }

    // ── Offsets (DeviceProfile 保存) ──────────────────────────────────────────

    async Task RefreshOffsetsAsync()
    {
        var repo = RepositoryService.Instance;
        if (repo == null || !repo.IsReady) return;

        _currentActiveProfile = repo.ActiveProfile;
        if (_currentActiveProfile == null) return;

        _suppressSliderEvents = true;
        _judgmentOffsetSlider?.SetValueWithoutNotify(_currentActiveProfile.Offsets.JudgmentOffsetMs);
        _visualOffsetSlider?.SetValueWithoutNotify(_currentActiveProfile.Offsets.VisualOffsetMs);
        _suppressSliderEvents = false;

        if (_judgmentOffsetValue != null)
            _judgmentOffsetValue.text = _currentActiveProfile.Offsets.JudgmentOffsetMs + " ms";
        if (_visualOffsetValue != null)
            _visualOffsetValue.text = _currentActiveProfile.Offsets.VisualOffsetMs + " ms";

        await Task.CompletedTask;
    }

    void HandleProfileChanged(DeviceProfile profile) => _ = RefreshOffsetsAsync();

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
                JudgmentOffsetMs = _judgmentOffsetSlider != null ? (int)_judgmentOffsetSlider.value : _currentActiveProfile.Offsets.JudgmentOffsetMs,
                VisualOffsetMs   = _visualOffsetSlider   != null ? (int)_visualOffsetSlider.value   : _currentActiveProfile.Offsets.VisualOffsetMs,
            },
            CreatedAtUnixMs = _currentActiveProfile.CreatedAtUnixMs,
            UpdatedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };

        bool ok = await repo.SaveProfileAsync(updated);
        if (ok)
        {
            _currentActiveProfile = updated;
            // AudioConductor 等のリスナーに反映させる
            await RepositoryService.Instance.SetActiveProfileAsync(updated.ProfileId);
        }
    }

    // ── Static helper (旧 GameTabController から移設) ─────────────────────────

    /// <summary>保存済みのコンボ継続境界となる判定値を返す。GamePlayController から JudgmentSystem.Initialize に渡す。</summary>
    public static Judgment GetSavedComboBorder()
    {
        switch (PlayerPrefs.GetInt("ComboBorderIdx", 0))
        {
            case 0:  return Judgment.Good;
            case 1:  return Judgment.Great;
            case 2:  return Judgment.Perfect;
            case 3:  return Judgment.PerfectPlus;
            default: return Judgment.Good;
        }
    }
}
