using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using RhythmGame.Network;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 楽曲選択画面を管理するコントローラー。
/// 楽曲リストの読み込み・並び替え・選択、難易度切り替え、★難易度表記、
/// パーソナルベスト(スコア/達成率/コンボ/セクター)表示、ハイスピード・曲別オフセット設定、
/// プロフィール表示、およびゲームプレイへのシーン遷移を担当する。
/// </summary>
public class SongSelectController : MonoBehaviour
{
    [Header("Song List")]
    [SerializeField] RectTransform      _listContent;
    [SerializeField] GameObject         _songItemPrefab;
    [SerializeField] ScrollRect         _scrollRect;

    [Header("Sort / Filter")]
    [SerializeField] Button             _sortButton;
    [SerializeField] TextMeshProUGUI    _sortLabel;

    [Header("Right Pane — Song Info")]
    [SerializeField] RawImage           _jacketImage;
    [SerializeField] TextMeshProUGUI    _titleText;
    [SerializeField] TextMeshProUGUI    _artistText;
    [SerializeField] TextMeshProUGUI    _bpmDurationText;

    [Header("Best Stats")]
    [SerializeField] TextMeshProUGUI    _statsText;     // SCORE / RATE / COMBO を1行で
    [SerializeField] TextMeshProUGUI    _bestRankText;
    [SerializeField] Image[]            _sectorIcons;   // 5 セクター

    [Header("Difficulty Buttons")]
    [SerializeField] Button             _btnEasy;
    [SerializeField] Button             _btnNormal;
    [SerializeField] Button             _btnHard;
    [SerializeField] Button             _btnExtra;
    [SerializeField] TextMeshProUGUI[]  _diffLevelTexts;

    [Header("Settings — HiSpeed")]
    [SerializeField] Slider             _hiSpeedSlider;
    [SerializeField] TextMeshProUGUI    _hiSpeedValue;

    [Header("Settings — Per-Song Offset")]
    [SerializeField] Slider             _perSongOffsetSlider;
    [SerializeField] TextMeshProUGUI    _perSongOffsetValue;
    [SerializeField] Button             _perSongOffsetSaveButton;
    [SerializeField] TextMeshProUGUI    _saveButtonLabel;

    [Header("Settings — Modifier")]
    [SerializeField] TMP_Dropdown       _modifierDropdown;

    [Header("Profile")]
    [SerializeField] TextMeshProUGUI    _profileName;
    [SerializeField] TextMeshProUGUI    _profileSub;

    [Header("Navigation")]
    [SerializeField] Button             _backButton;

    [Header("Input")]
    [SerializeField] InputActionAsset   _inputAsset;

    // ── Internal state ──────────────────────────────────────────────────────

    /// <summary>楽曲選択画面で使用する難易度種別を表す列挙型。</summary>
    enum Difficulty { Easy = 0, Normal = 1, Hard = 2, Extra = 3 }
    static readonly string[] DiffNames = { "easy", "normal", "hard", "extra" };
    static readonly string[] DiffShort = { "EZ", "NM", "HD", "EX" };

    /// <summary>リスト並び替えモード。</summary>
    enum SortMode { TitleAsc = 0, TitleDesc = 1, BpmAsc = 2, BpmDesc = 3 }
    static readonly string[] SortLabels =
        { "TITLE (A to Z)", "TITLE (Z to A)", "BPM (LOW)", "BPM (HIGH)" };

    readonly List<SongMetadata> _songs     = new List<SongMetadata>();
    readonly List<GameObject>   _itemViews = new List<GameObject>();
    int        _selectedIndex;
    Difficulty _selectedDiff = Difficulty.Extra;
    SortMode   _sortMode     = SortMode.TitleAsc;

    PerSongOffset _currentPerSongOffset;
    bool          _perSongOffsetDirty;

    InputAction _navigateAction;
    InputAction _submitAction;
    InputAction _cancelAction;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        Application.runInBackground = true;
        var map = _inputAsset.FindActionMap("UI", throwIfNotFound: true);
        _navigateAction = map.FindAction("Navigate", throwIfNotFound: true);
        _submitAction   = map.FindAction("Submit",   throwIfNotFound: true);
        _cancelAction   = map.FindAction("Cancel",   throwIfNotFound: true);
    }

    void OnEnable()
    {
        _navigateAction.Enable();
        _submitAction.Enable();
        _cancelAction.Enable();
        _navigateAction.performed += OnNavigate;
        _submitAction.performed   += OnSubmit;
        _cancelAction.performed   += OnCancel;
    }

    void OnDisable()
    {
        _navigateAction.performed -= OnNavigate;
        _submitAction.performed   -= OnSubmit;
        _cancelAction.performed   -= OnCancel;
    }

    void Update()
    {
        // F4: 並び替え切替 (Input System)
        var kb = Keyboard.current;
        if (kb != null && kb.f4Key.wasPressedThisFrame) CycleSort();
    }

    async void Start()
    {
        _btnEasy.onClick.AddListener(()   => SetDifficulty(Difficulty.Easy));
        _btnNormal.onClick.AddListener(() => SetDifficulty(Difficulty.Normal));
        _btnHard.onClick.AddListener(()   => SetDifficulty(Difficulty.Hard));
        _btnExtra.onClick.AddListener(()  => SetDifficulty(Difficulty.Extra));
        _backButton.onClick.AddListener(OnBack);
        if (_sortButton != null) _sortButton.onClick.AddListener(CycleSort);

        // HiSpeed (0.5〜20: 低速は縛りプレイ用に残す)
        _hiSpeedSlider.minValue = 0.5f;
        _hiSpeedSlider.maxValue = 20f;
        _hiSpeedSlider.onValueChanged.AddListener(v => _hiSpeedValue.text = v.ToString("F1"));
        _hiSpeedSlider.value = PlayerPrefs.GetFloat("HiSpeed", 4.5f);

        // Per-song offset
        _perSongOffsetSlider.minValue     = PerSongOffset.MinMs;
        _perSongOffsetSlider.maxValue     = PerSongOffset.MaxMs;
        _perSongOffsetSlider.wholeNumbers = true;
        _perSongOffsetSlider.onValueChanged.AddListener(OnPerSongOffsetChanged);
        _perSongOffsetSaveButton.onClick.AddListener(OnSavePerSongOffset);
        _currentPerSongOffset = PerSongOffset.DefaultFor("");
        UpdateSaveButtonAppearance();

        _modifierDropdown.ClearOptions();
        _modifierDropdown.AddOptions(new List<string> { "None", "Mirror", "Random" });
        _modifierDropdown.value = 0;

        UpdateProfile();
        UpdateSortLabel();

        await LoadSongList();

        if (_songs.Count > 0)
        {
            SelectSong(0);
            SetDifficulty(Difficulty.Extra);
        }
    }

    // ── Profile ──────────────────────────────────────────────────────────────

    void UpdateProfile()
    {
        if (_profileName != null) _profileName.text = LocalIdentity.UserId;
        if (_profileSub  != null) _profileSub.text  = "FREE PLAY";
    }

    // ── Song List ────────────────────────────────────────────────────────────

    async Task LoadSongList()
    {
        var songsRoot = Path.Combine(Application.streamingAssetsPath, "Songs");
        if (!Directory.Exists(songsRoot)) return;

        _songs.Clear();
        foreach (var dir in Directory.GetDirectories(songsRoot))
        {
            var songId = Path.GetFileName(dir);
            try   { _songs.Add(await ChartLoader.LoadMetaAsync(songId)); }
            catch (System.Exception e)
                  { Debug.LogWarning($"[SongSelect] Skip {songId}: {e.Message}"); }
        }

        ApplySort();
        RebuildSongViews();
    }

    void RebuildSongViews()
    {
        foreach (var item in _itemViews) Destroy(item);
        _itemViews.Clear();

        for (int i = 0; i < _songs.Count; i++)
        {
            var view = Instantiate(_songItemPrefab, _listContent);
            int idx  = i;
            view.GetComponentInChildren<Button>().onClick.AddListener(() => SelectSong(idx));

            var texts = view.GetComponentsInChildren<TextMeshProUGUI>(true);
            if (texts.Length >= 2)
            {
                texts[0].text = _songs[i].Title;
                texts[1].text = _songs[i].Artist;
            }
            _itemViews.Add(view);
        }
    }

    // ── Sorting ──────────────────────────────────────────────────────────────

    void CycleSort()
    {
        _sortMode = (SortMode)(((int)_sortMode + 1) % SortLabels.Length);
        UpdateSortLabel();

        if (_songs.Count == 0) return;
        var keepId = _songs[_selectedIndex].SongId;
        ApplySort();
        RebuildSongViews();

        int restore = _songs.FindIndex(s => s.SongId == keepId);
        SelectSong(restore < 0 ? 0 : restore);
    }

    void ApplySort()
    {
        switch (_sortMode)
        {
            case SortMode.TitleAsc:  _songs.Sort((a, b) => string.Compare(a.Title, b.Title, System.StringComparison.OrdinalIgnoreCase)); break;
            case SortMode.TitleDesc: _songs.Sort((a, b) => string.Compare(b.Title, a.Title, System.StringComparison.OrdinalIgnoreCase)); break;
            case SortMode.BpmAsc:    _songs.Sort((a, b) => a.Bpm.CompareTo(b.Bpm)); break;
            case SortMode.BpmDesc:   _songs.Sort((a, b) => b.Bpm.CompareTo(a.Bpm)); break;
        }
    }

    void UpdateSortLabel()
    {
        if (_sortLabel != null) _sortLabel.text = SortLabels[(int)_sortMode];
    }

    // ── Input ────────────────────────────────────────────────────────────────

    void OnNavigate(InputAction.CallbackContext ctx)
    {
        var v = ctx.ReadValue<Vector2>();
        if      (v.y >  0.5f) SelectSong(_selectedIndex - 1);
        else if (v.y < -0.5f) SelectSong(_selectedIndex + 1);
        else if (v.x >  0.5f) SetDifficulty((Difficulty)Mathf.Min(3, (int)_selectedDiff + 1));
        else if (v.x < -0.5f) SetDifficulty((Difficulty)Mathf.Max(0, (int)_selectedDiff - 1));
    }

    void OnSubmit(InputAction.CallbackContext ctx) => OnPlay();
    void OnCancel(InputAction.CallbackContext ctx) => OnBack();

    // ── Selection ────────────────────────────────────────────────────────────

    async void SelectSong(int index)
    {
        if (_songs.Count == 0) return;
        _selectedIndex = (index + _songs.Count) % _songs.Count;
        int captured = _selectedIndex;

        for (int i = 0; i < _itemViews.Count; i++)
        {
            var bg = _itemViews[i].transform.Find("Background")?.GetComponent<Image>();
            if (bg != null)
                bg.color = (i == _selectedIndex)
                    ? new Color(0.3f, 0.5f, 0.9f, 0.5f)
                    : new Color(1f, 1f, 1f, 0.05f);
        }

        ScrollToItem(_selectedIndex);

        var m = _songs[_selectedIndex];
        _titleText.text       = m.Title;
        _artistText.text      = m.Artist;
        int totalSec          = m.DurationMs / 1000;
        _bpmDurationText.text = $"BPM {m.Bpm:F0}   Length {totalSec / 60}:{totalSec % 60:D2}";

        ResetBestStats();

        JacketBackgroundController.Instance?.SetJacket(m.SongId);
        StartCoroutine(LoadJacket(m.SongId, m.JacketFile));
        StartCoroutine(LoadDifficultyLevels(m.SongId, captured));

        await LoadBestAsync(m.SongId, captured);

        // Async: per-song offset
        await LoadPerSongOffsetAsync(m.SongId, captured);
    }

    void ResetBestStats()
    {
        if (_statsText    != null) _statsText.text    = "SCORE 0     RATE 0.00%     COMBO 0";
        if (_bestRankText != null) _bestRankText.text = "-";
        SetSectorFill(null);
    }

    async Task LoadBestAsync(string songId, int captured)
    {
        if (RepositoryService.Instance?.IsReady != true) return;

        var diffStr = DiffNames[(int)_selectedDiff];
        var best    = await RepositoryService.Instance.PlayRecords.GetBestAsync(songId, diffStr);
        if (_selectedIndex != captured) return;
        if (best == null) return;

        // 達成率とセクターは本体記録(RawScore/SectorScores)から
        double rate = -1;
        if (!string.IsNullOrEmpty(best.BestPlayId))
        {
            var rec = await RepositoryService.Instance.PlayRecords.GetByIdAsync(best.BestPlayId);
            if (_selectedIndex != captured) return;
            if (rec != null)
            {
                rate = rec.RawScore / 10000.0;
                SetSectorFill(rec.SectorScores);
            }
        }

        if (_bestRankText != null) _bestRankText.text = best.BestRank;
        if (_statsText != null)
        {
            string rateStr = rate >= 0 ? rate.ToString("F2") : "--";
            _statsText.text =
                $"SCORE {best.BestEffectiveScore:N0}     RATE {rateStr}%     COMBO {best.BestMaxCombo:N0}";
        }
    }

    void SetSectorFill(int[] sectorScores)
    {
        if (_sectorIcons == null) return;
        for (int i = 0; i < _sectorIcons.Length; i++)
        {
            if (_sectorIcons[i] == null) continue;
            bool filled = sectorScores != null && i < sectorScores.Length && sectorScores[i] > 0;
            _sectorIcons[i].color = filled
                ? new Color(0.31f, 0.76f, 0.97f, 1f)   // cyan
                : new Color(1f, 1f, 1f, 0.12f);
        }
    }

    void ScrollToItem(int index)
    {
        if (_itemViews.Count == 0 || _scrollRect == null) return;
        float t = _songs.Count > 1 ? (float)index / (_songs.Count - 1) : 0f;
        _scrollRect.verticalNormalizedPosition = 1f - t;
    }

    void SetDifficulty(Difficulty d)
    {
        _selectedDiff = d;
        var btns = new[] { _btnEasy, _btnNormal, _btnHard, _btnExtra };
        for (int i = 0; i < 4; i++)
        {
            var img = btns[i].GetComponent<Image>();
            img.color = ((int)d == i)
                ? new Color(0.3f, 0.5f, 0.9f, 1f)
                : new Color(1f, 1f, 1f, 0.15f);
        }
        if (_songs.Count > 0) SelectSong(_selectedIndex);
    }

    // ── Per-Song Offset ───────────────────────────────────────────────────────

    async Task LoadPerSongOffsetAsync(string songId, int capturedIndex)
    {
        var repo = RepositoryService.Instance?.Offsets;
        _currentPerSongOffset = repo != null
            ? await repo.GetPerSongOffsetAsync(songId)
            : PerSongOffset.DefaultFor(songId);

        if (_selectedIndex != capturedIndex) return;

        _perSongOffsetSlider.SetValueWithoutNotify(_currentPerSongOffset.JudgmentOffsetMs);
        _perSongOffsetValue.text = $"{_currentPerSongOffset.JudgmentOffsetMs} ms";
        _perSongOffsetDirty      = false;
        UpdateSaveButtonAppearance();
    }

    void OnPerSongOffsetChanged(float v)
    {
        _perSongOffsetValue.text = $"{(int)v} ms";
        _perSongOffsetDirty      = (int)v != _currentPerSongOffset.JudgmentOffsetMs;
        UpdateSaveButtonAppearance();
    }

    async void OnSavePerSongOffset()
    {
        if (!_perSongOffsetDirty || _songs.Count == 0) return;

        var newOffset = new PerSongOffset
        {
            SongId           = _songs[_selectedIndex].SongId,
            JudgmentOffsetMs = (int)_perSongOffsetSlider.value,
        };

        var repo = RepositoryService.Instance?.Offsets;
        if (repo == null) return;

        bool ok = await repo.SavePerSongOffsetAsync(newOffset);
        if (ok)
        {
            _currentPerSongOffset = newOffset;
            _perSongOffsetDirty   = false;
            UpdateSaveButtonAppearance(saved: true);
            StartCoroutine(ResetSaveButtonAfterDelay(0.8f));
        }
    }

    void UpdateSaveButtonAppearance(bool saved = false)
    {
        if (_saveButtonLabel == null || _perSongOffsetSaveButton == null) return;

        if (saved)
        {
            _saveButtonLabel.text         = "SAVED";
            _saveButtonLabel.color        = new Color(0.4f, 1f, 0.4f);
            _perSongOffsetSaveButton.interactable = false;
        }
        else if (_perSongOffsetDirty)
        {
            _saveButtonLabel.text         = "SAVE";
            _saveButtonLabel.color        = Color.white;
            _perSongOffsetSaveButton.interactable = true;
        }
        else
        {
            _saveButtonLabel.text         = "SAVE";
            _saveButtonLabel.color        = new Color(1f, 1f, 1f, 0.35f);
            _perSongOffsetSaveButton.interactable = false;
        }
    }

    IEnumerator ResetSaveButtonAfterDelay(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        UpdateSaveButtonAppearance();
    }

    // ── Async loaders ────────────────────────────────────────────────────────

    IEnumerator LoadJacket(string songId, string jacketFile)
    {
        var fileName = string.IsNullOrEmpty(jacketFile) ? "jacket.png" : jacketFile;
        var path     = Path.Combine(Application.streamingAssetsPath, "Songs", songId, fileName)
                           .Replace("\\", "/");
        var url = (path.StartsWith("jar:") || path.StartsWith("http"))
            ? path : "file:///" + path.TrimStart('/');

        using var req = UnityWebRequestTexture.GetTexture(url);
        yield return req.SendWebRequest();

        _jacketImage.texture = req.result == UnityWebRequest.Result.Success
            ? ((DownloadHandlerTexture)req.downloadHandler).texture
            : null;
    }

    IEnumerator LoadDifficultyLevels(string songId, int captured)
    {
        for (int i = 0; i < 4; i++)
        {
            var task = ChartLoader.LoadChartAsync(songId, DiffNames[i]);
            while (!task.IsCompleted) yield return null;
            if (_selectedIndex != captured) yield break;

            int lvl = (task.IsCompleted && !task.IsFaulted && task.Result != null)
                ? task.Result.Level : -1;

            if (_diffLevelTexts != null && i < _diffLevelTexts.Length)
                _diffLevelTexts[i].text = $"{DiffShort[i]} {(lvl >= 0 ? lvl.ToString() : "-")}";
        }
    }

    // ── Play / Back ──────────────────────────────────────────────────────────

    void OnPlay()
    {
        if (_songs.Count == 0) return;

        if (_perSongOffsetDirty)
            Debug.LogWarning("[SongSelect] Unsaved per-song offset — will not affect this play");

        PlayerPrefs.SetFloat("HiSpeed", _hiSpeedSlider.value);
        PlayerPrefs.Save();

        var meta = _songs[_selectedIndex];
        var parameters = new GamePlayParameters
        {
            SongId       = meta.SongId,
            Difficulty   = DiffNames[(int)_selectedDiff],
            HiSpeed      = _hiSpeedSlider.value,
            JudgeOffset  = 0,   // offsets now come from DeviceProfile via RepositoryService
            VisualOffset = 0,
            Modifier     = _modifierDropdown.options[_modifierDropdown.value].text,
        };

        SceneRouter.Instance.GoTo(SceneId.GamePlay, parameters);
    }

    void OnBack() => SceneRouter.Instance.GoTo(SceneId.Title);
}
