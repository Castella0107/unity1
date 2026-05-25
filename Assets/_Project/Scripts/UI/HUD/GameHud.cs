using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ゲームプレイ中の HUD を管理するコンポーネント。
/// 左上の楽曲情報ボックス、上中央の判定カウント、左の縦スコアゲージ＋総合 RATE、
/// および S1..S5 のセクター達成率(菱形マーカー)を表示する。PVP 用の次曲インジケーターも担当する。
/// </summary>
public class GameHud : MonoBehaviour
{
    [Header("Song Info Box (top-left)")]
    [SerializeField] RawImage        _jacket;
    [SerializeField] TextMeshProUGUI _songTitle;
    [SerializeField] TextMeshProUGUI _songArtist;
    [SerializeField] TextMeshProUGUI _difficulty;

    [Header("Judgment Counts (top-center)")]
    [SerializeField] TextMeshProUGUI _ppCount;
    [SerializeField] TextMeshProUGUI _pCount;
    [SerializeField] TextMeshProUGUI _grCount;
    [SerializeField] TextMeshProUGUI _gdCount;
    [SerializeField] TextMeshProUGUI _mCount;

    [Header("Score Panel (left)")]
    [SerializeField] Image           _scoreGauge;   // filled vertical, fillAmount = score/1,000,000
    [SerializeField] TextMeshProUGUI _scoreValue;
    [SerializeField] TextMeshProUGUI _rateValue;

    [Header("Sector Diamonds — index 0 = S1 (bottom) .. 4 = S5 (top)")]
    [SerializeField] Image[]           _sectorDiamonds = new Image[5];
    [SerializeField] TextMeshProUGUI[] _sectorPercents = new TextMeshProUGUI[5];

    [Header("Next Song Indicator (PVP Only)")]
    [SerializeField] GameObject      _nextIndicator;
    [SerializeField] RawImage        _nextJacket;
    [SerializeField] TextMeshProUGUI _nextSongTitle;

    [Header("Refs")]
    [SerializeField] JudgmentSystem  _judgment;
    [SerializeField] AudioConductor  _conductor;

    static readonly Color ColIdle   = new Color(0.35f, 0.35f, 0.35f, 1f);  // future sector
    static readonly Color ColJacketFallback = new Color(0.12f, 0.13f, 0.18f, 1f);

    SongMetadata _meta;
    ChartData    _chart;
    JacketLoader _jacketLoader;
    int          _shownSectorIdx = -1;   // highest sector index whose final value is locked in

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>楽曲メタ・譜面・PvP フラグから HUD(曲情報・スコア・セクターパネル等)を初期化する。</summary>
    public void Initialize(SongMetadata meta, ChartData chart, bool isPvP)
    {
        _meta  = meta;
        _chart = chart;

        if (_songTitle  != null) _songTitle.text  = meta.Title;
        if (_songArtist != null) _songArtist.text = meta.Artist;
        if (_difficulty != null)
        {
            _difficulty.text  = $"{DifficultyLabel(chart.Difficulty)} {chart.Level}";
            _difficulty.color = RankColors.GetDifficultyColor(chart.Difficulty);
        }

        LoadJacket(meta.SongId);

        if (_scoreValue != null) _scoreValue.text = "0";
        if (_rateValue  != null) _rateValue.text  = "0.00%";
        if (_scoreGauge != null) _scoreGauge.fillAmount = 0f;

        _shownSectorIdx = -1;
        for (int i = 0; i < _sectorDiamonds.Length; i++)
        {
            bool exists = _meta.Sectors != null && i < _meta.Sectors.Count;
            if (_sectorDiamonds[i] != null)
            {
                _sectorDiamonds[i].color = ColIdle;
                _sectorDiamonds[i].enabled = exists;
            }
            if (_sectorPercents[i] != null)
                _sectorPercents[i].text = exists ? "--" : "";
        }

        if (_nextIndicator != null) _nextIndicator.SetActive(isPvP);
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Update()
    {
        if (_judgment == null || _conductor == null || _meta == null) return;

        var agg = _judgment.Aggregator;
        if (agg == null) return;

        _ppCount.text = agg.Counts[(int)Judgment.PerfectPlus].ToString();
        _pCount.text  = agg.Counts[(int)Judgment.Perfect].ToString();
        _grCount.text = agg.Counts[(int)Judgment.Great].ToString();
        _gdCount.text = agg.Counts[(int)Judgment.Good].ToString();
        _mCount.text  = agg.Counts[(int)Judgment.Miss].ToString();

        int score = agg.CurrentScore;
        if (_scoreValue != null) _scoreValue.text = score.ToString("N0");
        if (_scoreGauge != null) _scoreGauge.fillAmount = score / 1_000_000f;

        // Overall RATE = accuracy = score / max-so-far. At song end == score/10000.
        if (_rateValue != null)
            _rateValue.text = Rate(score, agg.CurrentMaxScore).ToString("F2") + "%";

        UpdateSectorDiamonds(agg);
    }

    // ── Sector diamonds ─────────────────────────────────────────────────────────

    // Driven off the aggregator's CurrentSectorIdx so the display matches the exact
    // moment each sector's score delta is finalized (idx > i ⇒ sector i is locked in).
    void UpdateSectorDiamonds(PlayProgressAggregator agg)
    {
        int curIdx     = agg.CurrentSectorIdx;
        int sectorCount = _meta.Sectors != null ? _meta.Sectors.Count : 0;

        // Lock in newly-completed sectors with their final accuracy + rank color.
        while (_shownSectorIdx < curIdx - 1 && _shownSectorIdx < 4)
        {
            _shownSectorIdx++;
            int i = _shownSectorIdx;
            if (i < sectorCount)
                SetDiamond(i, Rate(agg.SectorScores[i], agg.SectorMaxScores[i]), final: true);
        }

        // Live update for the in-progress sector.
        if (curIdx < 5 && curIdx < sectorCount && agg.CurrentSectorMaxScore > 0)
            SetDiamond(curIdx, Rate(agg.CurrentSectorScore, agg.CurrentSectorMaxScore), final: false);
    }

    void SetDiamond(int i, float ratePct, bool final)
    {
        if (i < 0 || i >= _sectorDiamonds.Length) return;
        string rank = ScoreCalculator.ComputeRank(Mathf.RoundToInt(ratePct * 10000f));
        if (_sectorDiamonds[i] != null) _sectorDiamonds[i].color = RankColors.GetRankColor(rank);
        if (_sectorPercents[i] != null) _sectorPercents[i].text  = ratePct.ToString("F2") + "%";
    }

    // ── PVP helper ────────────────────────────────────────────────────────────

    /// <summary>次曲のタイトルとジャケットを表示する(PVP の曲間表示用)。</summary>
    public void SetNextSong(string title, Texture2D jacket)
    {
        if (_nextSongTitle != null) _nextSongTitle.text = title;
        if (_nextJacket    != null) _nextJacket.texture = jacket;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    static float Rate(int score, int maxScore)
        => maxScore > 0 ? score / (float)maxScore * 100f : 0f;

    static string DifficultyLabel(string diff)
    {
        switch (diff?.ToLower())
        {
            case "easy":   return "ESY";
            case "normal": return "NML";
            case "hard":   return "HRD";
            case "extra":  return "EX";
            default:       return string.IsNullOrEmpty(diff) ? "??" : diff.ToUpper();
        }
    }

    async void LoadJacket(string songId)
    {
        if (_jacket == null) return;
        _jacket.color = Color.white;
        _jacketLoader ??= new JacketLoader();
        var tex = await LoadJacketTexture(songId);
        if (_jacket == null) return;   // destroyed while awaiting
        if (tex != null) _jacket.texture = tex;
        else             _jacket.color   = ColJacketFallback;
    }

    System.Threading.Tasks.Task<Texture2D> LoadJacketTexture(string songId)
        => _jacketLoader.LoadAsync(songId);
}
