using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class HistoryController : MonoBehaviour
{
    [Header("Header")]
    [SerializeField] Button          _backButton;
    [SerializeField] TextMeshProUGUI _totalPlaysText;

    [Header("Filter")]
    [SerializeField] Toggle      _listModeToggle;
    [SerializeField] Toggle      _bestModeToggle;
    [SerializeField] TMP_Dropdown _difficultyDropdown;
    [SerializeField] TMP_Dropdown _rankDropdown;
    [SerializeField] TMP_Dropdown _sortDropdown;

    [Header("List")]
    [SerializeField] RectTransform _listContent;
    [SerializeField] ScrollRect    _scrollRect;
    [SerializeField] GameObject    _historyItemPrefab;
    [SerializeField] GameObject    _emptyState;

    [Header("Detail")]
    [SerializeField] GameObject        _detailEmptyState;
    [SerializeField] GameObject        _detailContent;
    [SerializeField] HistoryDetailView _detailView;

    [Header("Input")]
    [SerializeField] InputActionAsset _inputAsset;

    enum ViewMode { AllPlays, PersonalBests }
    ViewMode _mode = ViewMode.AllPlays;

    string _diffFilter = "all";
    string _rankFilter = "all";
    string _sortBy     = "recent";

    readonly List<PlayRecord>    _allRecords = new List<PlayRecord>();
    readonly List<PersonalBest>  _allBests   = new List<PersonalBest>();
    readonly List<HistoryItemView> _items     = new List<HistoryItemView>();
    int _selectedIndex = -1;

    InputAction _navigateAction;
    InputAction _submitAction;
    InputAction _cancelAction;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        var map        = _inputAsset.FindActionMap("UI", throwIfNotFound: true);
        _navigateAction = map.FindAction("Navigate", throwIfNotFound: true);
        _submitAction   = map.FindAction("Submit",   throwIfNotFound: true);
        _cancelAction   = map.FindAction("Cancel",   throwIfNotFound: true);
    }

    void OnEnable()
    {
        _navigateAction.performed += OnNavigate;
        _cancelAction.performed   += OnCancel;
        _navigateAction.Enable();
        _submitAction.Enable();
        _cancelAction.Enable();

        JacketBackgroundController.Instance?.SetFallback();
    }

    void OnDisable()
    {
        _navigateAction.performed -= OnNavigate;
        _cancelAction.performed   -= OnCancel;
        _navigateAction.Disable();
        _submitAction.Disable();
        _cancelAction.Disable();
    }

    async void Start()
    {
        SetupUI();
        await LoadData();
        ApplyFilters();
    }

    // ── Setup ─────────────────────────────────────────────────────────────────

    void SetupUI()
    {
        _backButton.onClick.AddListener(() => SceneRouter.Instance.GoTo(SceneId.Title));

        _listModeToggle.onValueChanged.AddListener(on =>
        {
            if (on) { _mode = ViewMode.AllPlays;      ApplyFilters(); }
        });
        _bestModeToggle.onValueChanged.AddListener(on =>
        {
            if (on) { _mode = ViewMode.PersonalBests; ApplyFilters(); }
        });
        _listModeToggle.SetIsOnWithoutNotify(true);

        _difficultyDropdown.ClearOptions();
        _difficultyDropdown.AddOptions(new List<string> { "All", "Easy", "Normal", "Hard", "Extra" });
        _difficultyDropdown.onValueChanged.AddListener(idx =>
        {
            switch (idx)
            {
                case 1:  _diffFilter = "easy";   break;
                case 2:  _diffFilter = "normal"; break;
                case 3:  _diffFilter = "hard";   break;
                case 4:  _diffFilter = "extra";  break;
                default: _diffFilter = "all";    break;
            }
            ApplyFilters();
        });

        _rankDropdown.ClearOptions();
        _rankDropdown.AddOptions(new List<string>
            { "All Ranks", "S+ Only", "S or Better", "A or Better", "B or Better" });
        _rankDropdown.onValueChanged.AddListener(idx =>
        {
            switch (idx)
            {
                case 1:  _rankFilter = "sp";           break;
                case 2:  _rankFilter = "s_or_better";  break;
                case 3:  _rankFilter = "a_or_better";  break;
                case 4:  _rankFilter = "b_or_better";  break;
                default: _rankFilter = "all";          break;
            }
            ApplyFilters();
        });

        _sortDropdown.ClearOptions();
        _sortDropdown.AddOptions(new List<string>
            { "Recent First", "Score: High→Low", "Score: Low→High", "Combo: High" });
        _sortDropdown.onValueChanged.AddListener(idx =>
        {
            switch (idx)
            {
                case 1:  _sortBy = "score_desc"; break;
                case 2:  _sortBy = "score_asc";  break;
                case 3:  _sortBy = "combo_desc"; break;
                default: _sortBy = "recent";     break;
            }
            ApplyFilters();
        });
    }

    // ── Data ──────────────────────────────────────────────────────────────────

    async Task LoadData()
    {
        var repo = RepositoryService.Instance?.PlayRecords;
        if (repo == null)
        {
            if (_totalPlaysText != null) _totalPlaysText.text = "No database";
            return;
        }

        var records = await repo.GetAllHistoryAsync(limit: 1000, offset: 0);
        _allRecords.Clear();
        _allRecords.AddRange(records);

        var bests = await repo.GetAllBestsAsync();
        _allBests.Clear();
        _allBests.AddRange(bests);

        int total = await repo.GetTotalPlaysAsync();
        if (_totalPlaysText != null)
            _totalPlaysText.text = string.Format("Total: {0} plays", total);
    }

    // ── Filtering & sorting ───────────────────────────────────────────────────

    void ApplyFilters()
    {
        IEnumerable<PlayRecord> filtered = _allRecords;

        if (_mode == ViewMode.PersonalBests)
        {
            var bestIds = new HashSet<string>(_allBests.Select(b => b.BestPlayId));
            filtered = filtered.Where(r => bestIds.Contains(r.PlayId));
        }

        if (_diffFilter != "all")
            filtered = filtered.Where(r => r.Difficulty == _diffFilter);

        filtered = FilterByRank(filtered, _rankFilter);
        filtered = SortRecords(filtered, _sortBy);

        BuildList(filtered.ToList());
    }

    static IEnumerable<PlayRecord> FilterByRank(IEnumerable<PlayRecord> src, string filter)
    {
        switch (filter)
        {
            case "sp":
                return src.Where(r => r.Rank == "S+");
            case "s_or_better":
                return src.Where(r => r.Rank == "S+" || r.Rank == "S");
            case "a_or_better":
                return src.Where(r => r.Rank == "S+" || r.Rank == "S"
                                   || r.Rank == "A+" || r.Rank == "A");
            case "b_or_better":
                return src.Where(r => r.Rank == "S+" || r.Rank == "S"
                                   || r.Rank == "A+" || r.Rank == "A"
                                   || r.Rank == "B");
            default:
                return src;
        }
    }

    static IEnumerable<PlayRecord> SortRecords(IEnumerable<PlayRecord> src, string sortBy)
    {
        switch (sortBy)
        {
            case "score_desc": return src.OrderByDescending(r => r.EffectiveScore);
            case "score_asc":  return src.OrderBy(r => r.EffectiveScore);
            case "combo_desc": return src.OrderByDescending(r => r.MaxCombo);
            default:           return src.OrderByDescending(r => r.PlayedAtUnixMs);
        }
    }

    // ── List building ─────────────────────────────────────────────────────────

    void BuildList(List<PlayRecord> records)
    {
        foreach (var v in _items) if (v.Root != null) Destroy(v.Root);
        _items.Clear();
        _selectedIndex = -1;

        bool empty = records.Count == 0;
        if (_emptyState != null) _emptyState.SetActive(empty);

        if (empty) { ShowDetail(null); return; }

        for (int i = 0; i < records.Count; i++)
        {
            var go   = Instantiate(_historyItemPrefab, _listContent);
            var view = new HistoryItemView(go, records[i]);
            int idx  = i;
            view.Button.onClick.AddListener(() => SelectIndex(idx));
            _items.Add(view);
        }

        SelectIndex(0);
    }

    void SelectIndex(int index)
    {
        if (index < 0 || index >= _items.Count) return;
        for (int i = 0; i < _items.Count; i++) _items[i].SetSelected(i == index);
        _selectedIndex = index;
        ShowDetail(_items[index].Record);
    }

    void ShowDetail(PlayRecord record)
    {
        bool hasRecord = record != null;
        if (_detailEmptyState != null) _detailEmptyState.SetActive(!hasRecord);
        if (_detailContent    != null) _detailContent.SetActive(hasRecord);
        if (hasRecord && _detailView != null) _detailView.Show(record);
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    void OnNavigate(InputAction.CallbackContext ctx)
    {
        if (_items.Count == 0) return;
        float y = ctx.ReadValue<Vector2>().y;
        if      (y >  0.5f) SelectAndScroll((_selectedIndex - 1 + _items.Count) % _items.Count);
        else if (y < -0.5f) SelectAndScroll((_selectedIndex + 1) % _items.Count);
    }

    void OnCancel(InputAction.CallbackContext ctx) => SceneRouter.Instance.GoTo(SceneId.Title);

    void SelectAndScroll(int index)
    {
        SelectIndex(index);
        if (_scrollRect == null || _selectedIndex < 0 || _selectedIndex >= _items.Count) return;

        var itemRT    = _items[_selectedIndex].Root.GetComponent<RectTransform>();
        float itemY   = Mathf.Abs(itemRT.anchoredPosition.y);
        float contentH = _listContent.rect.height;
        float viewH    = _scrollRect.viewport.rect.height;
        if (contentH <= viewH) return;
        _scrollRect.verticalNormalizedPosition = 1f - Mathf.Clamp01(itemY / (contentH - viewH));
    }
}

// ── HistoryItemView (non-MonoBehaviour view helper) ───────────────────────────

public class HistoryItemView
{
    public GameObject  Root   { get; }
    public Button      Button { get; }
    public PlayRecord  Record { get; }

    Image _bg;
    static readonly Color IdleColor     = new Color(1f, 1f, 1f, 0.04f);
    static readonly Color SelectedColor = new Color(0.17f, 0.35f, 0.63f, 0.5f);

    public HistoryItemView(GameObject go, PlayRecord rec)
    {
        Root   = go;
        Record = rec;
        Button = go.GetComponent<Button>();
        _bg    = FindComponent<Image>(go, "Background");

        var dt = DateTimeOffset.FromUnixTimeMilliseconds(rec.PlayedAtUnixMs).LocalDateTime;
        SetText(go, "Layout/DateBlock/DateText", dt.ToString("yyyy/MM/dd"));
        SetText(go, "Layout/DateBlock/TimeText", dt.ToString("HH:mm"));
        SetText(go, "Layout/SongBlock/TitleText", rec.SongId);
        SetText(go, "Layout/SongBlock/DiffText",  rec.Difficulty.ToUpper());
        SetColor(go, "Layout/SongBlock/DiffText",  DiffColor(rec.Difficulty));
        SetText(go, "Layout/ScoreBlock/ScoreText", rec.EffectiveScore.ToString("N0"));
        SetText(go, "Layout/ScoreBlock/RankText",  rec.Rank);
        SetColor(go, "Layout/ScoreBlock/RankText", RankColor(rec.Rank));

        SetActive(go, "Layout/BadgeBlock/FullComboBadge",      rec.IsFullCombo);
        SetActive(go, "Layout/BadgeBlock/AllPerfectBadge",     rec.IsAllPerfect && !rec.IsAllPerfectPlus);
        SetActive(go, "Layout/BadgeBlock/AllPerfectPlusBadge", rec.IsAllPerfectPlus);

        SetSelected(false);
    }

    public void SetSelected(bool on)
    {
        if (_bg != null) _bg.color = on ? SelectedColor : IdleColor;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static T FindComponent<T>(GameObject root, string path) where T : Component
    {
        var t = root.transform.Find(path);
        return t != null ? t.GetComponent<T>() : null;
    }

    static void SetText(GameObject root, string path, string text)
    {
        var tmp = FindComponent<TMPro.TextMeshProUGUI>(root, path);
        if (tmp != null) tmp.text = text;
    }

    static void SetColor(GameObject root, string path, Color color)
    {
        var tmp = FindComponent<TMPro.TextMeshProUGUI>(root, path);
        if (tmp != null) tmp.color = color;
    }

    static void SetActive(GameObject root, string path, bool active)
    {
        var t = root.transform.Find(path);
        if (t != null) t.gameObject.SetActive(active);
    }

    static Color DiffColor(string diff)
    {
        switch (diff)
        {
            case "easy":   return new Color(0.4f, 0.95f, 0.4f);
            case "normal": return new Color(0.4f, 0.7f,  1.0f);
            case "hard":   return new Color(1.0f, 0.7f,  0.3f);
            case "extra":  return new Color(1.0f, 0.3f,  0.3f);
            default:       return Color.white;
        }
    }

    static Color RankColor(string rank)
    {
        switch (rank)
        {
            case "S+": return new Color(1.0f, 0.85f, 0.3f);
            case "S":  return new Color(1.0f, 0.7f,  0.3f);
            case "A+": return new Color(0.4f, 0.95f, 0.5f);
            case "A":  return new Color(0.4f, 0.85f, 0.5f);
            case "B":  return new Color(0.4f, 0.7f,  1.0f);
            case "C":  return new Color(0.7f, 0.7f,  0.7f);
            default:   return new Color(0.5f, 0.5f,  0.5f);
        }
    }
}
