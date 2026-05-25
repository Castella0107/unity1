using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// コンフィグ画面のゲームタブを管理するコントローラー。
/// ハイスピード・コンボボーダー・FAST/LATE 表示・ノートスキン・背景エフェクト強度・判定エフェクトスタイルの設定を担当する。
/// </summary>
public class GameTabController : MonoBehaviour
{
    [Header("Hi-Speed")]
    [SerializeField] Slider          _hiSpeedSlider;
    [SerializeField] TextMeshProUGUI _hiSpeedValue;

    [Header("Combo")]
    [SerializeField] TMP_Dropdown _comboBorderDropdown;
    [SerializeField] Toggle       _fastLateToggle;

    [Header("Skins (Phase 2 — locked)")]
    [SerializeField] TMP_Dropdown _noteSkinDropdown;
    [SerializeField] TMP_Dropdown _laneSkinDropdown;

    [Header("Effects")]
    [SerializeField] Slider          _backgroundEffectsSlider;
    [SerializeField] TextMeshProUGUI _backgroundEffectsValue;
    [SerializeField] TMP_Dropdown    _judgmentEffectDropdown;

    void Start()
    {
        SetupAll();
        LoadSettings();
    }

    void SetupAll()
    {
        _hiSpeedSlider.minValue = 0.5f;
        _hiSpeedSlider.maxValue = 10.0f;
        _hiSpeedSlider.onValueChanged.AddListener(v =>
        {
            _hiSpeedValue.text = v.ToString("F1");
            PlayerPrefs.SetFloat("HiSpeed", v);
            PlayerPrefs.Save();
        });

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

        _fastLateToggle.onValueChanged.AddListener(v =>
        {
            PlayerPrefs.SetInt("ShowFastLate", v ? 1 : 0);
            PlayerPrefs.Save();
        });

        _noteSkinDropdown.ClearOptions();
        _noteSkinDropdown.AddOptions(new List<string> { "Default" });
        _noteSkinDropdown.interactable = false;

        _laneSkinDropdown.ClearOptions();
        _laneSkinDropdown.AddOptions(new List<string> { "Default" });
        _laneSkinDropdown.interactable = false;

        _backgroundEffectsSlider.minValue     = 0;
        _backgroundEffectsSlider.maxValue     = 100;
        _backgroundEffectsSlider.wholeNumbers = true;
        _backgroundEffectsSlider.onValueChanged.AddListener(v =>
        {
            _backgroundEffectsValue.text = (int)v + "%";
            PlayerPrefs.SetFloat("BgEffectsIntensity", v);
            PlayerPrefs.Save();
            JacketBackgroundController.Instance?.SetBrightness((v / 100f) * 0.5f);
            BeatGridController.Instance?.SetUserIntensity(v / 100f);
        });

        _judgmentEffectDropdown.ClearOptions();
        _judgmentEffectDropdown.AddOptions(new List<string> { "Subtle", "Normal", "Bold" });
        _judgmentEffectDropdown.onValueChanged.AddListener(idx =>
        {
            PlayerPrefs.SetInt("JudgmentEffectStyleIdx", idx);
            PlayerPrefs.Save();
        });
    }

    void LoadSettings()
    {
        _hiSpeedSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("HiSpeed", 4.5f));
        _hiSpeedValue.text = _hiSpeedSlider.value.ToString("F1");

        _comboBorderDropdown.SetValueWithoutNotify(PlayerPrefs.GetInt("ComboBorderIdx", 0));
        _fastLateToggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt("ShowFastLate", 1) == 1);

        _backgroundEffectsSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("BgEffectsIntensity", 100f));
        _backgroundEffectsValue.text = (int)_backgroundEffectsSlider.value + "%";
        JacketBackgroundController.Instance?.SetBrightness((_backgroundEffectsSlider.value / 100f) * 0.5f);
        BeatGridController.Instance?.SetUserIntensity(_backgroundEffectsSlider.value / 100f);

        _judgmentEffectDropdown.SetValueWithoutNotify(PlayerPrefs.GetInt("JudgmentEffectStyleIdx", 1));
    }

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
