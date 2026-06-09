using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// ESC toggles pause. ↑↓ navigate buttons, Enter confirms.
// Resume plays a 3-second countdown before AudioConductor.Resume().

/// <summary>
/// ゲームプレイ中のポーズメニューを制御するクラス。
/// ESC キーでポーズ／再開を切り替え、↑↓（W/S）キーでボタン選択、Enter で確定する。
/// 再開時は設定秒数のカウントダウンを表示した後に AudioConductor.Resume() を呼び出す。
/// </summary>
public class PauseMenu : MonoBehaviour
{
    [SerializeField] GameObject     _panel;
    [SerializeField] Button         _resumeButton;
    [SerializeField] Button         _restartButton;
    [SerializeField] Button         _quitButton;
    [SerializeField] AudioConductor _conductor;

    [Header("Countdown")]
    [SerializeField] GameObject        _countdownOverlay;
    [SerializeField] TextMeshProUGUI   _countdownText;
    [SerializeField] float             _countdownSec = 3f;

    bool  _isPaused;
    int   _selectedIndex;
    Button[] _buttons;

    // PVP 対戦中はポーズ不可。ESC 長押し（6秒）でリタイア（不戦敗）。
    bool        _isPvpMatch;   // ライブ PVP 対戦 (リプレイは除く)。リタイア対象。
    bool        _isReplay;     // リプレイ再生中。Quit は History へ戻す。
    string      _returnMode;   // リプレイ Quit 時の History タブ ("Ladder"/"Free")。
    float       _retireHold;
    const float RetireHoldSec = 6f;
    bool        _retiring;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        if (_panel            != null) _panel.SetActive(false);
        if (_countdownOverlay != null) _countdownOverlay.SetActive(false);

        if (_resumeButton  != null) _resumeButton.onClick.AddListener(OnResume);
        if (_restartButton != null) _restartButton.onClick.AddListener(OnRestart);
        if (_quitButton    != null) _quitButton.onClick.AddListener(OnQuit);

        var prm = ParameterStore.GetCurrent<GamePlayParameters>();
        _isReplay   = prm != null && prm.IsReplay;
        _isPvpMatch = !_isReplay && prm != null && prm.IsPvp;   // リプレイはライブ対戦扱いにしない
        _returnMode = (prm != null && prm.IsPvp) ? "Ladder" : "Free";

        SetupNavButtons();   // Quit ラベル整理 +「タイトルへ」ボタンを追加

        RhythmGame.UI.Common.ShortcutHintOverlay.Set(
            _isPvpMatch ? "ESC（長押し6秒）: リタイア（不戦敗）" : "ESC: ポーズ");
    }

    // 戻る系ボタンを整える。ポーズパネルは手組みシーンで「タイトルへ」ボタンが無いため、
    // Quit ボタンを複製して runtime で追加する(シーン編集不要)。
    //   - Quit: リプレイ=「履歴へ」(History) / ソロ=「選曲へ」(SongSelect)
    //   - 追加: 「タイトルへ」→ Title
    void SetupNavButtons()
    {
        SetButtonLabel(_quitButton, _isReplay ? "履歴へ" : "選曲へ");

        Button titleBtn = null;
        if (_quitButton != null && _quitButton.transform.parent != null)
        {
            var go = Instantiate(_quitButton.gameObject, _quitButton.transform.parent);
            go.name = "TitleBtn";
            go.transform.SetSiblingIndex(_quitButton.transform.GetSiblingIndex() + 1);
            titleBtn = go.GetComponent<Button>();
            if (titleBtn != null)
            {
                titleBtn.onClick.RemoveAllListeners();
                titleBtn.onClick.AddListener(OnTitle);
            }
            SetButtonLabel(titleBtn, "タイトルへ");
        }

        _buttons = titleBtn != null
            ? new[] { _resumeButton, _restartButton, _quitButton, titleBtn }
            : new[] { _resumeButton, _restartButton, _quitButton };
    }

    static void SetButtonLabel(Button b, string text)
    {
        if (b == null) return;
        var lbl = b.GetComponentInChildren<TextMeshProUGUI>(true);
        if (lbl != null) lbl.text = text;
    }

    void Update()
    {
        if (Keyboard.current == null) return;

        // ライブ PVP 対戦: ポーズ不可。ESC を 6 秒長押しでリタイア。
        if (_isPvpMatch)
        {
            HandlePvpRetire();
            return;
        }

        // Toggle pause (solo)
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (_isPaused) OnResume();
            else if (_conductor != null && _conductor.IsPlaying) OpenPause();
        }

        if (!_isPaused) return;

        // ↑↓ navigation
        if (Keyboard.current.upArrowKey.wasPressedThisFrame   ||
            Keyboard.current.wKey.wasPressedThisFrame)
        {
            _selectedIndex = (_selectedIndex - 1 + _buttons.Length) % _buttons.Length;
            UpdateHighlight();
        }
        else if (Keyboard.current.downArrowKey.wasPressedThisFrame ||
                 Keyboard.current.sKey.wasPressedThisFrame)
        {
            _selectedIndex = (_selectedIndex + 1) % _buttons.Length;
            UpdateHighlight();
        }

        // Confirm
        if (Keyboard.current.enterKey.wasPressedThisFrame ||
            Keyboard.current.numpadEnterKey.wasPressedThisFrame)
        {
            _buttons[_selectedIndex].onClick.Invoke();
        }
    }

    // ── PVP リタイア（ESC 長押し）────────────────────────────────────────────
    void HandlePvpRetire()
    {
        if (_retiring) return;

        if (Keyboard.current.escapeKey.isPressed)
        {
            _retireHold += Time.unscaledDeltaTime;
            int remain = Mathf.Max(1, Mathf.CeilToInt(RetireHoldSec - _retireHold));
            RhythmGame.UI.Common.ShortcutHintOverlay.Set($"リタイア中… ESC を押し続ける（{remain}）");

            if (_retireHold >= RetireHoldSec)
            {
                _retiring = true;
                _conductor?.Stop();
                RhythmGame.UI.Common.ShortcutHintOverlay.Clear();
                // 離脱で WS が切れ、サーバーの submit タイムアウト後に forfeit (不戦敗) 扱い。
                _ = RhythmGame.Network.Api.PvpMatchContext.ClearAsync();
                SceneRouter.Instance?.GoTo(SceneId.PVPLobby);
            }
        }
        else if (_retireHold > 0f)
        {
            _retireHold = 0f;
            RhythmGame.UI.Common.ShortcutHintOverlay.Set("ESC（長押し6秒）: リタイア（不戦敗）");
        }
    }

    // ── Pause / Resume ────────────────────────────────────────────────────────

    void OpenPause()
    {
        _isPaused = true;
        _conductor?.Pause();
        _selectedIndex = 0;
        if (_panel != null) _panel.SetActive(true);
        UpdateHighlight();
    }

    void ClosePanelUI()
    {
        if (_panel != null) _panel.SetActive(false);
    }

    void OnResume()
    {
        if (!_isPaused) return;
        ClosePanelUI();
        StartCoroutine(CountdownThenResume());
    }

    IEnumerator CountdownThenResume()
    {
        if (_countdownOverlay != null) _countdownOverlay.SetActive(true);

        for (int i = Mathf.RoundToInt(_countdownSec); i >= 1; i--)
        {
            if (_countdownText != null) _countdownText.text = i.ToString();
            yield return new WaitForSecondsRealtime(1.0f);
        }

        if (_countdownText != null) _countdownText.text = "GO!";
        yield return new WaitForSecondsRealtime(0.3f);

        if (_countdownOverlay != null) _countdownOverlay.SetActive(false);
        _conductor?.Resume(prerollSec: 0.0);
        _isPaused = false;
    }

    // ── Buttons ───────────────────────────────────────────────────────────────

    void OnRestart()
    {
        _isPaused = false;
        _conductor?.Stop();
        if (_panel != null) _panel.SetActive(false);

        // Re-use the same GamePlayParameters that started this session
        var parameters = ParameterStore.GetCurrent<GamePlayParameters>();
        if (SceneRouter.Instance != null)
            SceneRouter.Instance.GoTo(SceneId.GamePlay, parameters);
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene("GamePlay");
    }

    void OnQuit()
    {
        _isPaused = false;
        _conductor?.Stop();
        if (_panel != null) _panel.SetActive(false);

        // リプレイ再生中のポーズ離脱は History へ戻す(PVP=Ladder / ソロ=Free タブ)。
        if (_isReplay)
        {
            var hp = new HistoryParameters { Mode = _returnMode };
            if (SceneRouter.Instance != null) SceneRouter.Instance.GoTo(SceneId.History, hp);
            else { ParameterStore.SetPending(hp); UnityEngine.SceneManagement.SceneManager.LoadScene("History"); }
            return;
        }

        if (SceneRouter.Instance != null)
            SceneRouter.Instance.GoTo(SceneId.SongSelect);
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene("SongSelect");
    }

    void OnTitle()
    {
        _isPaused = false;
        _conductor?.Stop();
        if (_panel != null) _panel.SetActive(false);

        if (SceneRouter.Instance != null)
            SceneRouter.Instance.GoTo(SceneId.Title);
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene("Title");
    }

    // ── Selection highlight ───────────────────────────────────────────────────

    void UpdateHighlight()
    {
        for (int i = 0; i < _buttons.Length; i++)
        {
            if (_buttons[i] == null) continue;
            var img = _buttons[i].GetComponent<Image>();
            if (img == null) continue;
            var c = img.color;
            img.color = (i == _selectedIndex)
                ? new Color(c.r, c.g, c.b, 0.8f)
                : new Color(c.r, c.g, c.b, 0.3f);
        }
    }
}
