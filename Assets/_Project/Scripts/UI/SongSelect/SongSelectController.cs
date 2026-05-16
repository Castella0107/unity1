using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 楽曲選択画面を管理するコントローラー。
/// 楽曲リストの読み込み・選択、難易度切り替え、ハイスピード・曲別オフセット設定、
/// パーソナルベスト表示、およびゲームプレイへのシーン遷移を担当する。
/// </summary>
public class SongSelectController : MonoBehaviour
{
    [Header("Song List")]
    [SerializeField] RectTransform      _listContent;
    [SerializeField] GameObject         _songItemPrefab;
    [SerializeField] ScrollRect         _scrollRect;

    [Header("Right Pane")]
    [SerializeField] RawImage           _jacketImage;
    [SerializeField] TextMeshProUGUI    _titleText;
    [SerializeField] TextMeshProUGUI    _artistText;
    [SerializeField] TextMeshProUGUI    _bpmDurationText;
    [SerializeField] TextMeshProUGUI    _bestScoreText;
    [SerializeField] TextMeshProUGUI    _bestRankText;

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

    [Header("Navigation")]
    [SerializeField] Button             _playButton;
    [SerializeField] Button             _backButton;

    [Header("Input")]
    [SerializeField] InputActionAsset   _inputAsset;

    // ── Internal state ──────────────────────────────────────────────────────

    /// <summary>楽曲選択画面で使用する難易度種別を表す列挙型。</summary>
    enum Difficulty { Easy = 0, Normal = 1, Hard = 2, Extra = 3 }
    static readonly string[] DiffNames = { "easy", "normal", "hard", "extra" };

    readonly List<SongMetadata> _songs     = new List<SongMetadata>();
    readonly List<GameObject>   _itemViews = new List<GameObject>();
    int        _selectedIndex;
    Difficulty _selectedDiff = Difficulty.Extra;

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

    async void Start()
    {
        _btnEasy.onClick.AddListener(()   => SetDifficulty(Difficulty.Easy));
        _btnNormal.onClick.AddListener(() => SetDifficulty(Difficulty.Normal));
        _btnHard.onClick.AddListener(()   => SetDifficulty(Difficulty.Hard));
        _btnExtra.onClick.AddListener(()  => SetDifficulty(Difficulty.Extra));
        _playButton.onClick.AddListener(OnPlay);
        _backButton.onClick.AddListener(OnBack);

        // HiSpeed
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

        await LoadSongList();

        if (_songs.Count > 0)
        {
            SelectSong(0);
            SetDifficulty(Difficulty.Extra);
        }
    }

    // ── Song List ────────────────────────────────────────────────────────────

    async Task LoadSongList()
    {
        var songsRoot = Path.Combine(Application.streamingAssetsPath, "Songs");
        if (!Directory.Exists(songsRoot)) return;

        foreach (var item in _itemViews) Destroy(item);
        _itemViews.Clear();
        _songs.Clear();

        foreach (var dir in Directory.GetDirectories(songsRoot))
        {
            var songId = Path.GetFileName(dir);
            try   { _songs.Add(await ChartLoader.LoadMetaAsync(songId)); }
            catch (System.Exception e)
                  { Debug.LogWarning($"[SongSelect] Skip {songId}: {e.Message}"); }
        }

        for (int i = 0; i < _songs.Count; i++)
        {
            var view = Instantiate(_songItemPrefab, _listContent);
            int idx  = i;
            view.GetComponentInChildren<Button>().onClick.AddListener(() => SelectSong(idx));

            var texts = view.GetComponentsInChildren<TextMeshProUGUI>(true);
            if (texts.Length >= 3)
            {
                texts[0].text = _songs[i].Title;
                texts[1].text = _songs[i].Artist;
                texts[2].text = "Lv.--";
            }
            _itemViews.Add(view);
        }
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
        _bestScoreText.text   = "---";
        _bestRankText.text    = "-";

        JacketBackgroundController.Instance?.SetJacket(m.SongId);
        StartCoroutine(LoadJacket(m.SongId, m.JacketFile));
        StartCoroutine(LoadDifficultyLevels(m.SongId));

        // Async: personal best
        if (RepositoryService.Instance?.IsReady == true)
        {
            var diffStr = DiffNames[(int)_selectedDiff];
            var best    = await RepositoryService.Instance.PlayRecords.GetBestAsync(m.SongId, diffStr);
            if (_selectedIndex != captured) return;
            if (best != null)
            {
                _bestScoreText.text = $"BEST: {best.BestEffectiveScore:N0}";
                _bestRankText.text  = best.BestRank;
            }
        }

        // Async: per-song offset
        await LoadPerSongOffsetAsync(m.SongId, captured);
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

    IEnumerator LoadDifficultyLevels(string songId)
    {
        for (int i = 0; i < 4; i++)
        {
            if (_diffLevelTexts == null || i >= _diffLevelTexts.Length) yield break;
            var task = ChartLoader.LoadChartAsync(songId, DiffNames[i]);
            while (!task.IsCompleted) yield return null;
            _diffLevelTexts[i].text = (task.IsCompleted && !task.IsFaulted && task.Result != null)
                ? task.Result.Level.ToString() : "-";
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
