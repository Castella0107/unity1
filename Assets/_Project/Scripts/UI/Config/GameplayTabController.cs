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

    [Header("Lane Length (FAR_WALL_SPEC: 透明遮蔽壁の位置 25〜100%)")]
    [SerializeField] Slider          _laneLengthSlider;
    [SerializeField] TextMeshProUGUI _laneLengthValue;

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
    [SerializeField] Toggle       _comboShowToggle;    // コンボ表示 ON/OFF (K 指示 2026-07-30)
    [SerializeField] TMP_Dropdown _comboPosDropdown;   // コンボ表示位置
    [SerializeField] Toggle       _maxScoreShowToggle; // 最大スコア (理論値) 表示 ON/OFF (K 指示 2026-08-01)
    [SerializeField] TMP_Dropdown _maxScorePosDropdown;// 最大スコア表示位置

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
        SetupLaneLength();
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

    // ハイスピード: 0.1 刻みで操作できるよう、スライダーは 10 倍整数 (0.5〜20.0 → 5〜200) で持つ。
    // wholeNumbers=true にすると Slider.OnMove の←→キー 1 ステップが「1」= 0.1 になる
    // (wholeNumbers=false だとレンジの 10% = 約 2.0 も動いてしまう)。保存値は従来どおり 0.5〜20 の float。
    void SetupHiSpeed()
    {
        if (_hiSpeedSlider == null) return;
        _hiSpeedSlider.minValue     = 5f;     // = 0.5
        _hiSpeedSlider.maxValue     = 200f;   // = 20.0
        _hiSpeedSlider.wholeNumbers = true;
        _hiSpeedSlider.onValueChanged.AddListener(v =>
        {
            float speed = v / 10f;
            if (_hiSpeedValue != null) _hiSpeedValue.text = speed.ToString("F1");
            PlayerPrefs.SetFloat("HiSpeed", speed);
            PlayerPrefs.Save();
        });
        // シーン焼き込みの ◁▷ ステッパー (±0.5) も 10 倍整数スケールの ±1 (= 0.1) に合わせる
        foreach (var st in _hiSpeedSlider.transform.parent
                     .GetComponentsInChildren<RhythmGame.UI.Common.SliderStepper>(true))
            st.SetDeltaMagnitude(1f);

        // 数値クリックで直接入力 (K 指示 2026-07-31)。確定値はスライダー経由で反映=保存経路は従来どおり
        RhythmGame.UI.Common.ClickToEditValue.Attach(_hiSpeedValue, 0.5f, 20f, integer: false,
            get:    () => _hiSpeedSlider.value / 10f,
            commit: v  => _hiSpeedSlider.value = Mathf.Round(v * 10f));
    }

    // レーン長 (laneLength): スライダーは 25〜200 (%)、保存は 0.25〜2.00 の float
    // (上限 200% へ拡張 — K 指示 2026-07-30)。
    // 変更は即時 PlayerPrefs へ (グローバル保存モデル: F5 で確定・未保存離脱で巻き戻し)。
    void SetupLaneLength()
    {
        if (_laneLengthSlider == null) return;
        _laneLengthSlider.minValue     = 25f;
        _laneLengthSlider.maxValue     = 200f;
        _laneLengthSlider.wholeNumbers = true;
        _laneLengthSlider.onValueChanged.AddListener(v =>
        {
            if (_laneLengthValue != null) _laneLengthValue.text = (int)v + "%";
            PlayerPrefs.SetFloat("LaneLength", v / 100f);
            PlayerPrefs.Save();
            FxSectorGeometry.RefreshLaneLength();   // プレイフィールドの壁位置へ即時反映
        });

        RhythmGame.UI.Common.ClickToEditValue.Attach(_laneLengthValue, 25f, 200f, integer: true,
            get:    () => _laneLengthSlider.value,
            commit: v  => _laneLengthSlider.value = v);
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
            RhythmGame.UI.Common.ClickToEditValue.Attach(label,
                AppOffsetSettings.MinMs, AppOffsetSettings.MaxMs, integer: true,
                get:    () => s.value,
                commit: v  => s.value = v);
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

        if (_comboShowToggle != null)
            _comboShowToggle.onValueChanged.AddListener(v =>
            {
                PlayerPrefs.SetInt("ComboShow", v ? 1 : 0);
                PlayerPrefs.Save();
            });

        if (_comboPosDropdown != null)
        {
            _comboPosDropdown.ClearOptions();
            _comboPosDropdown.AddOptions(new List<string> { "レーン中央 (溶け込み)", "上部中央 (消失点の上)", "手元 (溶け込み)" });
            _comboPosDropdown.onValueChanged.AddListener(idx =>
            {
                PlayerPrefs.SetInt("ComboPosIdx", idx);
                PlayerPrefs.Save();
            });
        }

        if (_maxScoreShowToggle != null)
            _maxScoreShowToggle.onValueChanged.AddListener(v =>
            {
                PlayerPrefs.SetInt("ShowMaxScore", v ? 1 : 0);
                PlayerPrefs.Save();
            });

        if (_maxScorePosDropdown != null)
        {
            _maxScorePosDropdown.ClearOptions();
            _maxScorePosDropdown.AddOptions(new List<string> { "レーン中央 (溶け込み)", "上部中央 (消失点の上)", "手元 (溶け込み)" });
            _maxScorePosDropdown.onValueChanged.AddListener(idx =>
            {
                PlayerPrefs.SetInt("MaxScorePosIdx", idx);
                PlayerPrefs.Save();
            });
        }
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
                // ジャケット背景の明るさ上限: 旧 0.5 は「薄暗い」と K 指摘 (2026-07-30) → 0.85 に引き上げ
                JacketBackgroundController.Instance?.SetBrightness((v / 100f) * 0.85f);
                BeatGridController.Instance?.SetUserIntensity(v / 100f);
            });

            RhythmGame.UI.Common.ClickToEditValue.Attach(_backgroundEffectsValue, 0f, 100f, integer: true,
                get:    () => _backgroundEffectsSlider.value,
                commit: v  => _backgroundEffectsSlider.value = v);
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
            // スライダーは 10 倍整数スケール (SetupHiSpeed 参照)
            _hiSpeedSlider.SetValueWithoutNotify(
                Mathf.Round(Mathf.Clamp(PlayerPrefs.GetFloat("HiSpeed", 4.5f), 0.5f, 20f) * 10f));
            if (_hiSpeedValue != null) _hiSpeedValue.text = (_hiSpeedSlider.value / 10f).ToString("F1");
        }

        if (_laneLengthSlider != null)
        {
            _laneLengthSlider.SetValueWithoutNotify(
                Mathf.Round(Mathf.Clamp(PlayerPrefs.GetFloat("LaneLength", 1f), 0.25f, 2f) * 100f));
            if (_laneLengthValue != null) _laneLengthValue.text = (int)_laneLengthSlider.value + "%";
        }

        if (_comboBorderDropdown != null)
            _comboBorderDropdown.SetValueWithoutNotify(PlayerPrefs.GetInt("ComboBorderIdx", 0));
        if (_fastLateToggle != null)
            _fastLateToggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt("ShowFastLate", 1) == 1);
        if (_comboShowToggle != null)
            _comboShowToggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt("ComboShow", 1) == 1);
        if (_comboPosDropdown != null)
            _comboPosDropdown.SetValueWithoutNotify(Mathf.Clamp(PlayerPrefs.GetInt("ComboPosIdx", 0), 0, 2));
        if (_maxScoreShowToggle != null)
            _maxScoreShowToggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt("ShowMaxScore", 1) == 1);
        if (_maxScorePosDropdown != null)
            _maxScorePosDropdown.SetValueWithoutNotify(Mathf.Clamp(PlayerPrefs.GetInt("MaxScorePosIdx", 0), 0, 2));

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
