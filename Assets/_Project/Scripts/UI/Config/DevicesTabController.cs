using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// コンフィグ画面のデバイスタブを管理するコントローラー。
/// デバイスプロファイルの一覧表示・選択・追加・削除・保存、および OS デバイス名のアタッチ／クリアを担当する。
/// </summary>
public class DevicesTabController : MonoBehaviour
{
    [Header("Current OS Device")]
    [SerializeField] TextMeshProUGUI _currentDeviceText;
    [SerializeField] Button          _attachToProfileButton;

    [Header("Profile List")]
    [SerializeField] RectTransform _profileListContent;
    [SerializeField] GameObject    _profileListItemPrefab;
    [SerializeField] Button        _addNewButton;

    [Header("Edit — Basic")]
    [SerializeField] TMP_InputField  _displayNameInput;
    [SerializeField] TextMeshProUGUI _osDeviceValueText;
    [SerializeField] Button          _clearOsDeviceButton;
    [SerializeField] Toggle          _autoSwitchToggle;
    [SerializeField] TextMeshProUGUI _offsetsInfoText;

    [Header("Edit — Actions")]
    [SerializeField] Button _setActiveButton;
    [SerializeField] Button _deleteButton;
    [SerializeField] Button _saveButton;

    readonly List<DeviceProfile>        _profiles  = new List<DeviceProfile>();
    readonly List<ProfileListItemView>  _itemViews = new List<ProfileListItemView>();
    DeviceProfile _selectedProfile;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        _addNewButton.onClick.AddListener(OnAddNew);
        _attachToProfileButton.onClick.AddListener(OnAttachToProfile);
        _clearOsDeviceButton.onClick.AddListener(OnClearOsDevice);
        _setActiveButton.onClick.AddListener(OnSetActive);
        _deleteButton.onClick.AddListener(OnDelete);
        _saveButton.onClick.AddListener(OnSave);
    }

    void OnEnable()
    {
        _ = RefreshAsync();
        if (RepositoryService.Instance != null)
            RepositoryService.Instance.OnActiveProfileChanged += OnActiveProfileChanged;
        InvokeRepeating(nameof(PollCurrentDevice), 0f, 1f);
    }

    void OnDisable()
    {
        if (RepositoryService.Instance != null)
            RepositoryService.Instance.OnActiveProfileChanged -= OnActiveProfileChanged;
        CancelInvoke(nameof(PollCurrentDevice));
    }

    void PollCurrentDevice()
    {
        if (_currentDeviceText == null) return;
        string name = DeviceProfileService.Instance?.CurrentOsDeviceName;
        _currentDeviceText.text = string.IsNullOrEmpty(name) ? "(no device detected)" : name;
    }

    // ── Refresh ───────────────────────────────────────────────────────────────

    async Task RefreshAsync()
    {
        var repo = RepositoryService.Instance?.Offsets;
        if (repo == null) return;

        var allProfiles = await repo.GetAllProfilesAsync();
        _profiles.Clear();
        _profiles.AddRange(allProfiles);

        BuildProfileList();

        // Re-select the previously selected profile (or active profile)
        string targetId = _selectedProfile?.ProfileId
                       ?? RepositoryService.Instance?.ActiveProfile?.ProfileId
                       ?? "default";

        var toSelect = _profiles.Find(p => p.ProfileId == targetId)
                    ?? (_profiles.Count > 0 ? _profiles[0] : null);

        if (toSelect != null) SelectProfile(toSelect);
        PollCurrentDevice();
    }

    void BuildProfileList()
    {
        foreach (var v in _itemViews) { if (v.Root != null) Destroy(v.Root); }
        _itemViews.Clear();

        string activeId = RepositoryService.Instance?.ActiveProfile?.ProfileId;

        foreach (var profile in _profiles)
        {
            var go   = Instantiate(_profileListItemPrefab, _profileListContent);
            var view = new ProfileListItemView(go, profile, profile.ProfileId == activeId);
            var p    = profile;
            view.Button.onClick.AddListener(() => SelectProfile(p));
            _itemViews.Add(view);
        }
    }

    void SelectProfile(DeviceProfile profile)
    {
        _selectedProfile = profile;

        foreach (var v in _itemViews)
            v.SetSelected(v.ProfileId == profile.ProfileId);

        if (_displayNameInput  != null) _displayNameInput.text = profile.DisplayName;
        if (_osDeviceValueText != null)
            _osDeviceValueText.text = string.IsNullOrEmpty(profile.OsDeviceName) ? "-" : profile.OsDeviceName;
        if (_autoSwitchToggle  != null) _autoSwitchToggle.SetIsOnWithoutNotify(profile.IsAutoSwitchEnabled);
        if (_offsetsInfoText   != null)
            _offsetsInfoText.text = string.Format("判定: {0} ms\n映像: {1} ms",
                profile.Offsets.JudgmentOffsetMs, profile.Offsets.VisualOffsetMs);

        string activeId = RepositoryService.Instance?.ActiveProfile?.ProfileId;
        if (_deleteButton    != null) _deleteButton.interactable    = profile.ProfileId != "default";
        if (_setActiveButton != null) _setActiveButton.interactable = profile.ProfileId != activeId;
    }

    void OnActiveProfileChanged(DeviceProfile profile) => _ = RefreshAsync();

    // ── Button handlers ───────────────────────────────────────────────────────

    async void OnAddNew()
    {
        var repo = RepositoryService.Instance?.Offsets;
        if (repo == null) return;

        var newProfile = new DeviceProfile
        {
            ProfileId           = "profile-" + Guid.NewGuid().ToString("N").Substring(0, 8),
            DisplayName         = "New Profile",
            OsDeviceName        = null,
            IsAutoSwitchEnabled = false,
            Offsets             = AppOffsetSettings.Default,
            CreatedAtUnixMs     = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            UpdatedAtUnixMs     = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };

        await repo.SaveProfileAsync(newProfile);
        _selectedProfile = newProfile;
        await RefreshAsync();
    }

    async void OnAttachToProfile()
    {
        if (_selectedProfile == null) return;
        var service = DeviceProfileService.Instance;
        if (service == null) { Debug.LogWarning("[DevicesTab] DeviceProfileService not available"); return; }

        bool ok = await service.AttachCurrentDeviceToProfileAsync(_selectedProfile.ProfileId);
        if (ok) await RefreshAsync();
        else Debug.LogWarning("[DevicesTab] Attach failed — no device detected or no matching profile");
    }

    async void OnClearOsDevice()
    {
        if (_selectedProfile == null) return;

        var updated = new DeviceProfile
        {
            ProfileId           = _selectedProfile.ProfileId,
            DisplayName         = _selectedProfile.DisplayName,
            OsDeviceName        = null,  // explicit clear
            IsAutoSwitchEnabled = _selectedProfile.IsAutoSwitchEnabled,
            Offsets             = _selectedProfile.Offsets,
            CreatedAtUnixMs     = _selectedProfile.CreatedAtUnixMs,
            UpdatedAtUnixMs     = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };

        await RepositoryService.Instance.Offsets.SaveProfileAsync(updated);
        await RefreshAsync();
    }

    async void OnSetActive()
    {
        if (_selectedProfile == null) return;
        await RepositoryService.Instance.SetActiveProfileAsync(_selectedProfile.ProfileId);
        await RefreshAsync();
    }

    async void OnDelete()
    {
        if (_selectedProfile == null || _selectedProfile.ProfileId == "default") return;

        // Switch to default before deleting to avoid dangling active profile
        await RepositoryService.Instance.SetActiveProfileAsync("default");
        await RepositoryService.Instance.Offsets.DeleteProfileAsync(_selectedProfile.ProfileId);
        _selectedProfile = null;
        await RefreshAsync();
    }

    async void OnSave()
    {
        if (_selectedProfile == null) return;

        var updated = new DeviceProfile
        {
            ProfileId           = _selectedProfile.ProfileId,
            DisplayName         = _displayNameInput != null ? _displayNameInput.text.Trim() : _selectedProfile.DisplayName,
            OsDeviceName        = _selectedProfile.OsDeviceName,  // preserve — use ClearOsDevice to remove
            IsAutoSwitchEnabled = _autoSwitchToggle != null && _autoSwitchToggle.isOn,
            Offsets             = _selectedProfile.Offsets,
            CreatedAtUnixMs     = _selectedProfile.CreatedAtUnixMs,
            UpdatedAtUnixMs     = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };

        await RepositoryService.Instance.Offsets.SaveProfileAsync(updated);
        _selectedProfile = updated;
        await RefreshAsync();
    }
}

// ── Profile list item view ────────────────────────────────────────────────────

/// <summary>
/// デバイスプロファイルリストの1アイテムの表示状態（選択中 / 非選択、アクティブ表示）を管理するビューヘルパークラス。
/// </summary>
public class ProfileListItemView
{
    /// <summary>このアイテムのルート GameObject。</summary>
    public GameObject Root      { get; }
    /// <summary>選択用ボタン。</summary>
    public Button     Button    { get; }
    /// <summary>このアイテムが表すプロファイルID。</summary>
    public string     ProfileId { get; }

    Image _bg;
    Image _activeIndicator;

    static readonly Color ColIdle     = new Color(1f, 1f, 1f, 0.05f);
    static readonly Color ColSelected = new Color(0.17f, 0.35f, 0.63f, 0.5f);

    /// <summary>GameObject・プロファイル・アクティブ状態からリストアイテムの表示を構築する。</summary>
    public ProfileListItemView(GameObject go, DeviceProfile profile, bool isActive)
    {
        Root      = go;
        ProfileId = profile.ProfileId;
        Button    = go.GetComponent<Button>();
        _bg               = go.transform.Find("Background")?.GetComponent<Image>();
        _activeIndicator  = go.transform.Find("ActiveIndicator")?.GetComponent<Image>();

        var nameText   = go.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
        var deviceText = go.transform.Find("DeviceText")?.GetComponent<TextMeshProUGUI>();

        if (nameText   != null) nameText.text = profile.DisplayName;
        if (deviceText != null)
            deviceText.text = string.IsNullOrEmpty(profile.OsDeviceName)
                ? "OS: -"
                : "OS: " + profile.OsDeviceName;

        if (_activeIndicator != null) _activeIndicator.gameObject.SetActive(isActive);
        SetSelected(false);
    }

    /// <summary>選択状態に応じて背景色を切り替える。</summary>
    public void SetSelected(bool selected)
    {
        if (_bg != null) _bg.color = selected ? ColSelected : ColIdle;
    }
}
