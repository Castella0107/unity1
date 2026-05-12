using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameHud : MonoBehaviour
{
    [Header("Top Bar — Judgment Counts")]
    [SerializeField] TextMeshProUGUI _ppCount;
    [SerializeField] TextMeshProUGUI _pCount;
    [SerializeField] TextMeshProUGUI _grCount;
    [SerializeField] TextMeshProUGUI _gdCount;
    [SerializeField] TextMeshProUGUI _mCount;
    [SerializeField] TextMeshProUGUI _comboValue;

    [Header("Sector Panel")]
    [SerializeField] RectTransform _sectorPanelContent;
    [SerializeField] GameObject    _sectorItemPrefab;

    [Header("Bottom Bar")]
    [SerializeField] TextMeshProUGUI _scoreText;
    [SerializeField] TextMeshProUGUI _songInfoText;

    [Header("Next Song Indicator (PVP Only)")]
    [SerializeField] GameObject      _nextIndicator;
    [SerializeField] RawImage        _nextJacket;
    [SerializeField] TextMeshProUGUI _nextSongTitle;

    [Header("Refs")]
    [SerializeField] JudgmentSystem  _judgment;
    [SerializeField] AudioConductor  _conductor;

    SongMetadata          _meta;
    ChartData             _chart;
    List<SectorItemView>  _sectorItems = new();
    int                   _currentSectorIdx;

    // ── Public API ────────────────────────────────────────────────────────────

    public void Initialize(SongMetadata meta, ChartData chart, bool isPvP)
    {
        _meta  = meta;
        _chart = chart;

        _scoreText.text    = "SCORE: 0,000,000";
        _songInfoText.text = $"{meta.Title}  -  {meta.Artist}  [Lv.{chart.Level}]";

        // Sector panel
        foreach (Transform t in _sectorPanelContent) Destroy(t.gameObject);
        _sectorItems.Clear();

        for (int i = 0; i < meta.Sectors.Count; i++)
        {
            var go   = Instantiate(_sectorItemPrefab, _sectorPanelContent);
            var view = new SectorItemView(go, meta.Sectors[i].Name);
            _sectorItems.Add(view);
        }

        _currentSectorIdx = 0;
        if (_sectorItems.Count > 0) _sectorItems[0].SetInProgress();

        _nextIndicator.SetActive(isPvP);
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void OnDisable()
    {
        // No event subscriptions — polling in Update
    }

    void Update()
    {
        if (_judgment == null || _conductor == null || _meta == null) return;

        var agg = _judgment.Aggregator;
        if (agg == null) return;

        _ppCount.text    = agg.Counts[(int)Judgment.PerfectPlus].ToString();
        _pCount.text     = agg.Counts[(int)Judgment.Perfect].ToString();
        _grCount.text    = agg.Counts[(int)Judgment.Great].ToString();
        _gdCount.text    = agg.Counts[(int)Judgment.Good].ToString();
        _mCount.text     = agg.Counts[(int)Judgment.Miss].ToString();
        _comboValue.text = agg.CurrentCombo.ToString();

        _scoreText.text = $"SCORE: {agg.CurrentScore:N0}";

        UpdateSectorProgress();
    }

    // ── Sector logic ──────────────────────────────────────────────────────────

    void UpdateSectorProgress()
    {
        if (_meta.Sectors == null || _meta.Sectors.Count == 0) return;

        double now = _conductor.SongTimeMs;

        // Advance past completed sectors
        while (_currentSectorIdx < _meta.Sectors.Count
               && now >= _meta.Sectors[_currentSectorIdx].EndMs)
        {
            if (_currentSectorIdx < _sectorItems.Count)
            {
                string rank = _judgment.Aggregator != null
                    ? ScoreCalculator.ComputeRank(_judgment.Aggregator.CurrentScore)
                    : "--";
                _sectorItems[_currentSectorIdx].SetCompleted(rank);
            }
            _currentSectorIdx++;
            if (_currentSectorIdx < _sectorItems.Count)
                _sectorItems[_currentSectorIdx].SetInProgress();
        }

        // Progress bar for current sector
        if (_currentSectorIdx < _sectorItems.Count
            && _currentSectorIdx < _meta.Sectors.Count)
        {
            int prevEnd = _currentSectorIdx == 0
                ? 0 : _meta.Sectors[_currentSectorIdx - 1].EndMs;
            int curEnd  = _meta.Sectors[_currentSectorIdx].EndMs;
            float prog  = Mathf.Clamp01(
                (float)((now - prevEnd) / System.Math.Max(1, curEnd - prevEnd)));
            _sectorItems[_currentSectorIdx].SetProgress(prog);
        }
    }

    // ── PVP helper ────────────────────────────────────────────────────────────

    public void SetNextSong(string title, Texture2D jacket)
    {
        if (_nextSongTitle != null) _nextSongTitle.text = title;
        if (_nextJacket    != null) _nextJacket.texture = jacket;
    }
}

// ── SectorItemView ────────────────────────────────────────────────────────────
// Controls one entry in the sector panel. Kept in same file to avoid extra prefab script.

public class SectorItemView
{
    Image            _bg;
    TextMeshProUGUI  _label;
    TextMeshProUGUI  _rank;
    Image            _progressBar;

    static readonly Color ColIdle       = new Color(1f, 1f, 1f, 0.08f);
    static readonly Color ColInProgress = new Color(0.17f, 0.35f, 0.63f, 0.50f);
    static readonly Color ColDone       = new Color(0.17f, 0.35f, 0.63f, 0.25f);

    public SectorItemView(GameObject go, string sectorName)
    {
        _bg          = go.transform.Find("Background")?.GetComponent<Image>();
        _progressBar = go.transform.Find("ProgressBar")?.GetComponent<Image>();

        var tmps = go.GetComponentsInChildren<TextMeshProUGUI>(true);
        if (tmps.Length >= 1) _label = tmps[0];
        if (tmps.Length >= 2) _rank  = tmps[1];

        if (_label != null) _label.text = sectorName;
        if (_rank  != null) { _rank.text = "--"; _rank.color = new Color(.5f,.5f,.5f,1f); }
        if (_bg    != null) _bg.color = ColIdle;
        if (_progressBar != null) _progressBar.fillAmount = 0f;
    }

    public void SetInProgress()
    {
        if (_bg   != null) _bg.color = ColInProgress;
        if (_rank != null) { _rank.text = "..."; _rank.color = new Color(.8f,.8f,.8f,1f); }
    }

    public void SetProgress(float p)
    {
        if (_progressBar != null) _progressBar.fillAmount = p;
    }

    public void SetCompleted(string rank)
    {
        if (_bg   != null) _bg.color = ColDone;
        if (_rank != null) { _rank.text = rank; _rank.color = RankColor(rank); }
        if (_progressBar != null) _progressBar.fillAmount = 1f;
    }

    static Color RankColor(string rank)
    {
        switch (rank)
        {
            case "S+": return new Color(1.00f, 0.84f, 0.00f);
            case "S":  return new Color(1.00f, 0.95f, 0.40f);
            case "A+": return new Color(0.40f, 1.00f, 0.40f);
            case "A":  return new Color(0.40f, 0.85f, 1.00f);
            case "B":  return new Color(0.85f, 0.85f, 0.85f);
            case "C":  return new Color(0.95f, 0.60f, 0.30f);
            case "D":  return new Color(1.00f, 0.40f, 0.40f);
            default:   return Color.white;
        }
    }
}
