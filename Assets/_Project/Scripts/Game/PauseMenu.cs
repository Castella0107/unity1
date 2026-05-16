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

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        _buttons = new[] { _resumeButton, _restartButton, _quitButton };

        if (_panel            != null) _panel.SetActive(false);
        if (_countdownOverlay != null) _countdownOverlay.SetActive(false);

        if (_resumeButton  != null) _resumeButton.onClick.AddListener(OnResume);
        if (_restartButton != null) _restartButton.onClick.AddListener(OnRestart);
        if (_quitButton    != null) _quitButton.onClick.AddListener(OnQuit);
    }

    void Update()
    {
        if (Keyboard.current == null) return;

        // Toggle pause
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

        if (SceneRouter.Instance != null)
            SceneRouter.Instance.GoTo(SceneId.SongSelect);
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene("SongSelect");
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
