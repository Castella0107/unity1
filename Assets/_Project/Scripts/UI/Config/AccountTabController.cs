using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AccountTabController : MonoBehaviour
{
    [Header("Profile")]
    [SerializeField] TextMeshProUGUI _playerNameValue;
    [SerializeField] TMP_InputField  _displayNameInput;
    [SerializeField] Button          _displayNameSaveButton;
    [SerializeField] TMP_InputField  _statusMessageInput;
    [SerializeField] Button          _statusMessageSaveButton;
    [SerializeField] TextMeshProUGUI _charCountText;

    [Header("Rank (Phase 4)")]
    [SerializeField] TextMeshProUGUI _currentRankValue;
    [SerializeField] TextMeshProUGUI _ratingValue;

    [Header("Linked Accounts (Phase 4)")]
    [SerializeField] TextMeshProUGUI _discordStatusText;
    [SerializeField] Button          _discordLinkButton;
    [SerializeField] TextMeshProUGUI _googleStatusText;
    [SerializeField] Button          _googleLinkButton;

    [Header("Notifications")]
    [SerializeField] Toggle _notificationsToggle;

    [Header("Sign Out (Phase 4)")]
    [SerializeField] Button _signOutButton;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        LoadSettings();
        SetupListeners();
    }

    void LoadSettings()
    {
        if (_playerNameValue   != null) _playerNameValue.text  = "Guest";
        if (_displayNameInput  != null) _displayNameInput.text = PlayerPrefs.GetString("DisplayName", "");
        if (_statusMessageInput != null)
        {
            _statusMessageInput.text = PlayerPrefs.GetString("StatusMessage", "");
            UpdateCharCount();
        }
        if (_currentRankValue != null) _currentRankValue.text = "未連携 (Phase 4)";
        if (_ratingValue      != null) _ratingValue.text      = "---";
        if (_discordStatusText != null) _discordStatusText.text = "Not Linked";
        if (_googleStatusText  != null) _googleStatusText.text  = "Not Linked";
        if (_discordLinkButton != null) _discordLinkButton.interactable = false;
        if (_googleLinkButton  != null) _googleLinkButton.interactable  = false;
        if (_notificationsToggle != null)
            _notificationsToggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt("NotificationsEnabled", 1) == 1);
        if (_signOutButton != null) _signOutButton.interactable = false;
    }

    void SetupListeners()
    {
        if (_displayNameInput != null && _displayNameSaveButton != null)
        {
            _displayNameInput.onValueChanged.AddListener(_ =>
                SetSaveButtonDirty(_displayNameSaveButton, true));
            _displayNameSaveButton.onClick.AddListener(SaveDisplayName);
            SetSaveButtonDirty(_displayNameSaveButton, false);
        }

        if (_statusMessageInput != null && _statusMessageSaveButton != null)
        {
            _statusMessageInput.onValueChanged.AddListener(_ =>
            {
                UpdateCharCount();
                SetSaveButtonDirty(_statusMessageSaveButton, true);
            });
            _statusMessageSaveButton.onClick.AddListener(SaveStatusMessage);
            SetSaveButtonDirty(_statusMessageSaveButton, false);
        }

        if (_notificationsToggle != null)
            _notificationsToggle.onValueChanged.AddListener(v =>
            {
                PlayerPrefs.SetInt("NotificationsEnabled", v ? 1 : 0);
                PlayerPrefs.Save();
            });

        if (_discordLinkButton != null)
            _discordLinkButton.onClick.AddListener(
                () => Debug.Log("[Account] Discord 連携: Phase 4 で実装"));
        if (_googleLinkButton != null)
            _googleLinkButton.onClick.AddListener(
                () => Debug.Log("[Account] Google 連携: Phase 4 で実装"));
        if (_signOutButton != null)
            _signOutButton.onClick.AddListener(
                () => Debug.Log("[Account] Sign Out: Phase 4 で実装"));
    }

    // ── Save helpers ──────────────────────────────────────────────────────────

    void SaveDisplayName()
    {
        string name = _displayNameInput.text?.Trim() ?? "";
        PlayerPrefs.SetString("DisplayName", name);
        PlayerPrefs.Save();
        FlashSaved(_displayNameSaveButton);
    }

    void SaveStatusMessage()
    {
        string msg = _statusMessageInput.text ?? "";
        PlayerPrefs.SetString("StatusMessage", msg);
        PlayerPrefs.Save();
        FlashSaved(_statusMessageSaveButton);
    }

    void UpdateCharCount()
    {
        if (_charCountText == null || _statusMessageInput == null) return;
        int len = _statusMessageInput.text?.Length ?? 0;
        _charCountText.text  = len + " / 200";
        _charCountText.color = len > 180
            ? new Color(1f, 0.6f, 0.3f)
            : new Color(1f, 1f, 1f, 0.5f);
    }

    void SetSaveButtonDirty(Button btn, bool dirty)
    {
        var label = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (label == null) return;
        if (dirty)
        {
            label.text = "SAVE"; label.color = Color.white;
            btn.interactable = true;
        }
        else
        {
            label.text = "SAVE"; label.color = new Color(1f, 1f, 1f, 0.35f);
            btn.interactable = false;
        }
    }

    void FlashSaved(Button btn)
    {
        var label = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null) { label.text = "SAVED"; label.color = new Color(0.4f, 1f, 0.4f); }
        btn.interactable = false;
        StartCoroutine(ResetButtonAfter(btn, 0.8f));
    }

    IEnumerator ResetButtonAfter(Button btn, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        SetSaveButtonDirty(btn, false);
    }
}
