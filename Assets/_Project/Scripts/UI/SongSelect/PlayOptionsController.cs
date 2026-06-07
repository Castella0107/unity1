using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 選曲画面の簡易コンフィグ「PLAY OPTIONS」ポップアップ (ユーザー提供モック準拠・ダーク版)。
/// 左=プレイ設定6項目(◀▶で変更) / 右=プレイ画面プレビュー(レーン構成は GamePlay 準拠の静的表示)+GUIDE。
///
/// 項目はすべて即時反映:
///   ノーツスピード/ノーツ配置 は SongSelect 詳細ペインの既存コントロール(Slider/Dropdown)を直接駆動して同期。
///   ヒット音/判定音 は HitSoundPlayer + PlayerPrefs、FAST-SLOW/判定エフェクト は PlayerPrefs。
///
/// 開閉: SongSelect で O キー (ポップアップ中は O/ESC/閉じるボタンで閉じる)。
/// シーン配線は SongSelectSceneBuilder.BuildPlayOptionsPanel が baked-in で行う。
/// </summary>
public class PlayOptionsController : MonoBehaviour
{
    [Header("Window (初期非表示)")]
    [SerializeField] GameObject _window;
    [SerializeField] Button     _closeButton;

    [Header("Rows (6項目: 速度/配置/タップ音/判定音/FAST-SLOW/判定エフェクト)")]
    [SerializeField] Image[]           _rowBgs;
    [SerializeField] Button[]          _rowButtons;
    [SerializeField] TextMeshProUGUI[] _valueTexts;
    [SerializeField] Button[]          _prevButtons;
    [SerializeField] Button[]          _nextButtons;

    [Header("Guide")]
    [SerializeField] TextMeshProUGUI _guideText;

    // ── 共有設定値 (PlayerPrefs 一元管理) ─────────────────────────────────────
    // SongSelect 詳細ペインの SPEED/MODIFIER コントロール撤去(2026-06-07)に伴い、
    // ノーツスピードとノーツ配置の正本はここ。SongSelect は表示と OnPlay で参照する。

    /// <summary>ノーツ配置モディファイアの選択肢 (GamePlayParameters.Modifier に渡す表記)。</summary>
    public static readonly string[] ModifierNames = { "None", "Mirror", "Random" };

    /// <summary>ノーツスピード (0.5〜20、PlayerPrefs "HiSpeed")。</summary>
    public static float HiSpeed
    {
        get => PlayerPrefs.GetFloat("HiSpeed", 4.5f);
        set
        {
            PlayerPrefs.SetFloat("HiSpeed", Mathf.Clamp(value, 0.5f, 20f));
            PlayerPrefs.Save();
        }
    }

    /// <summary>ノーツ配置インデックス (0=None/1=Mirror/2=Random、PlayerPrefs "ModifierIdx")。</summary>
    public static int ModifierIdx
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt("ModifierIdx", 0), 0, ModifierNames.Length - 1);
        set
        {
            int n = ModifierNames.Length;
            PlayerPrefs.SetInt("ModifierIdx", (value % n + n) % n);
            PlayerPrefs.Save();
        }
    }

    /// <summary>現在のノーツ配置名 ("None"/"Mirror"/"Random")。</summary>
    public static string ModifierName => ModifierNames[ModifierIdx];

    static PlayOptionsController _instance;
    int _closeFrame  = -1;
    int _openedFrame = -1;
    int _selected;

    static readonly Color RowIdle     = new Color(1f, 1f, 1f, 0.06f);
    static readonly Color RowSelected = new Color(0.42f, 0.32f, 0.85f, 0.45f);

    const int RowCount = 6;

    static readonly string[] GuideTexts =
    {
        "ノーツの落下速度を変更します。\n選曲画面の [ ] キーでも変更できます。",
        "ノーツ配置のモディファイアを変更します。\nMIRROR=左右反転 / RANDOM=レーンシャッフル。",
        "演奏中にキーを押したときに鳴るヒット音を切り替えます。",
        "判定確定時(PERFECT等)の効果音を切り替えます。",
        "判定時の FAST/SLOW 表示を切り替えます。",
        "判定エフェクトの強さを変更します。",
    };

    /// <summary>ポップアップ表示中か。閉じたフレームも true (閉じ入力の同フレーム再拾い防止)。</summary>
    public static bool IsOpen => _instance != null
        && ((_instance._window != null && _instance._window.activeSelf)
            || Time.frameCount == _instance._closeFrame);

    /// <summary>開閉をトグルする (SongSelect の O キーから)。</summary>
    public static void Toggle()
    {
        if (_instance == null) return;
        if (_instance._window != null && _instance._window.activeSelf) _instance.Close();
        else _instance.Open();
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _instance = this;
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    void Start()
    {
        if (_closeButton != null) _closeButton.onClick.AddListener(Close);

        for (int i = 0; i < RowCount; i++)
        {
            int idx = i;
            if (_rowButtons  != null && i < _rowButtons.Length  && _rowButtons[i]  != null)
                _rowButtons[i].onClick.AddListener(() => Select(idx));
            if (_prevButtons != null && i < _prevButtons.Length && _prevButtons[i] != null)
                _prevButtons[i].onClick.AddListener(() => { Select(idx); Step(idx, -1); });
            if (_nextButtons != null && i < _nextButtons.Length && _nextButtons[i] != null)
                _nextButtons[i].onClick.AddListener(() => { Select(idx); Step(idx, +1); });
        }
    }

    void Open()
    {
        if (_window == null) return;
        _window.SetActive(true);
        _openedFrame = Time.frameCount;   // 開いたフレームの O/ESC を再拾いして即閉じるのを防ぐ
        Select(0);
        RefreshAllValues();
    }

    void Close()
    {
        if (_window == null) return;
        _window.SetActive(false);
        _closeFrame = Time.frameCount;
    }

    void Update()
    {
        if (_window == null || !_window.activeSelf) return;
        if (Time.frameCount == _openedFrame) return;   // 開いたフレームの入力は無視

        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.escapeKey.wasPressedThisFrame || kb.oKey.wasPressedThisFrame) { Close(); return; }

            if (kb.upArrowKey.wasPressedThisFrame   || kb.wKey.wasPressedThisFrame) Select((_selected + RowCount - 1) % RowCount);
            if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) Select((_selected + 1) % RowCount);
            if (kb.leftArrowKey.wasPressedThisFrame  || kb.aKey.wasPressedThisFrame) Step(_selected, -1);
            if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) Step(_selected, +1);
        }

        var pad = Gamepad.current;
        if (pad != null)
        {
            if (RhythmGame.Input.GamepadLayout.BackPressed(pad)) { Close(); return; }
            if (pad.dpad.up.wasPressedThisFrame)    Select((_selected + RowCount - 1) % RowCount);
            if (pad.dpad.down.wasPressedThisFrame)  Select((_selected + 1) % RowCount);
            if (pad.dpad.left.wasPressedThisFrame)  Step(_selected, -1);
            if (pad.dpad.right.wasPressedThisFrame) Step(_selected, +1);
        }
    }

    // ── Rows ──────────────────────────────────────────────────────────────────

    void Select(int idx)
    {
        _selected = idx;
        if (_rowBgs != null)
            for (int i = 0; i < _rowBgs.Length; i++)
                if (_rowBgs[i] != null) _rowBgs[i].color = (i == idx) ? RowSelected : RowIdle;
        if (_guideText != null && idx < GuideTexts.Length) _guideText.text = GuideTexts[idx];
    }

    void Step(int idx, int dir)
    {
        switch (idx)
        {
            case 0:   // ノーツスピード
                HiSpeed += dir * 0.5f;
                break;
            case 1:   // ノーツ配置
                ModifierIdx += dir;
                break;
            case 2:   // タップヒット音 ON/OFF
            {
                bool v = PlayerPrefs.GetInt("HitSoundTap", 1) != 1;   // トグル
                HitSoundPlayer.Instance?.SetTapClickEnabled(v);
                if (HitSoundPlayer.Instance == null) { PlayerPrefs.SetInt("HitSoundTap", v ? 1 : 0); PlayerPrefs.Save(); }
                break;
            }
            case 3:   // 判定音 ON/OFF
            {
                bool v = PlayerPrefs.GetInt("HitSoundJudgment", 1) != 1;
                HitSoundPlayer.Instance?.SetJudgmentSoundsEnabled(v);
                if (HitSoundPlayer.Instance == null) { PlayerPrefs.SetInt("HitSoundJudgment", v ? 1 : 0); PlayerPrefs.Save(); }
                break;
            }
            case 4:   // FAST/SLOW 表示 ON/OFF
                PlayerPrefs.SetInt("ShowFastLate", PlayerPrefs.GetInt("ShowFastLate", 1) == 1 ? 0 : 1);
                PlayerPrefs.Save();
                break;
            case 5:   // 判定エフェクト Subtle/Normal/Bold
            {
                int v = (PlayerPrefs.GetInt("JudgmentEffectStyleIdx", 1) + dir + 3) % 3;
                PlayerPrefs.SetInt("JudgmentEffectStyleIdx", v);
                PlayerPrefs.Save();
                break;
            }
        }
        RefreshAllValues();
    }

    void RefreshAllValues()
    {
        if (_valueTexts == null) return;
        void Set(int i, string text)
        {
            if (i < _valueTexts.Length && _valueTexts[i] != null) _valueTexts[i].text = text;
        }

        Set(0, HiSpeed.ToString("F1"));
        Set(1, ModifierIdx == 0 ? "OFF" : ModifierName.ToUpperInvariant());
        Set(2, PlayerPrefs.GetInt("HitSoundTap", 1) == 1 ? "ON" : "OFF");
        Set(3, PlayerPrefs.GetInt("HitSoundJudgment", 1) == 1 ? "ON" : "OFF");
        Set(4, PlayerPrefs.GetInt("ShowFastLate", 1) == 1 ? "ON" : "OFF");
        Set(5, new[] { "SUBTLE", "NORMAL", "BOLD" }[Mathf.Clamp(PlayerPrefs.GetInt("JudgmentEffectStyleIdx", 1), 0, 2)]);
    }
}
