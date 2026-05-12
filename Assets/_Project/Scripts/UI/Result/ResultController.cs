using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ResultController : MonoBehaviour
{
    [Header("Header")]
    [SerializeField] TextMeshProUGUI _modeText;
    [SerializeField] TextMeshProUGUI _difficultyText;
    [SerializeField] TextMeshProUGUI _songInfoText;

    [Header("Sector Scores")]
    [SerializeField] RectTransform _sectorListContent;
    [SerializeField] GameObject    _sectorItemPrefab;

    [Header("Center — Rank")]
    [SerializeField] TextMeshProUGUI _rankText;

    [Header("Center — Score")]
    [SerializeField] TextMeshProUGUI _currentScoreText;
    [SerializeField] TextMeshProUGUI _bestScoreText;
    [SerializeField] GameObject      _newBestBadge;

    [Header("Center — Achievements")]
    [SerializeField] GameObject _fullComboBadge;
    [SerializeField] GameObject _allPerfectBadge;
    [SerializeField] GameObject _allPerfectPlusBadge;

    [Header("Right — Judgment")]
    [SerializeField] TextMeshProUGUI _ppCount;
    [SerializeField] TextMeshProUGUI _pCount;
    [SerializeField] TextMeshProUGUI _grCount;
    [SerializeField] TextMeshProUGUI _gdCount;
    [SerializeField] TextMeshProUGUI _mCount;
    [SerializeField] TextMeshProUGUI _maxComboText;

    [Header("Fast / Late")]
    [SerializeField] TextMeshProUGUI _fastCountText;
    [SerializeField] TextMeshProUGUI _lateCountText;

    [Header("Buttons")]
    [SerializeField] Button _retryButton;
    [SerializeField] Button _toSelectButton;
    [SerializeField] Button _toTitleButton;

    [Header("Input")]
    [SerializeField] InputActionAsset _inputAsset;

    [Header("Animation")]
    [SerializeField] float _countupDuration = 1.5f;

    InputAction      _submitAction;
    InputAction      _cancelAction;
    ResultParameters _params;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        Application.runInBackground = true;
        var map = _inputAsset.FindActionMap("UI", throwIfNotFound: true);
        _submitAction = map.FindAction("Submit", throwIfNotFound: true);
        _cancelAction = map.FindAction("Cancel", throwIfNotFound: true);
    }

    void Start()
    {
        _submitAction.Enable();
        _cancelAction.Enable();
        _submitAction.performed += _ => OnRetry();
        _cancelAction.performed += _ => OnToTitle();

        _retryButton.onClick.AddListener(OnRetry);
        _toSelectButton.onClick.AddListener(OnToSelect);
        _toTitleButton.onClick.AddListener(OnToTitle);

        _params = ParameterStore.GetPending<ResultParameters>();

        PlayResultView view = _params?.View ?? CreateDummyView();
        JacketBackgroundController.Instance?.SetJacket(view.Record?.SongId);
        if (_params == null)
            Debug.Log("[Result] No ResultParameters found — using dummy view");

        ApplyView(view);
    }

    void OnDisable()
    {
        _submitAction?.Disable();
        _cancelAction?.Disable();
    }

    void Update()
    {
        if (Keyboard.current == null) return;
        if (Keyboard.current.rKey.wasPressedThisFrame) OnRetry();
        if (Keyboard.current.sKey.wasPressedThisFrame) OnToSelect();
        if (Keyboard.current.tKey.wasPressedThisFrame) OnToTitle();
    }

    // ── Apply view ────────────────────────────────────────────────────────────

    void ApplyView(PlayResultView v)
    {
        var r = v.Record;

        _modeText.text        = r.IsPvP ? "PVP" : "Single";
        _difficultyText.text  = $"{r.Difficulty.ToUpper()}  Lv.{v.Level}";
        _difficultyText.color = GetDifficultyColor(r.Difficulty);
        _songInfoText.text    = $"{v.SongTitle}  -  {v.SongArtist}";

        _rankText.text  = r.Rank;
        _rankText.color = GetRankColor(r.Rank);

        StartCoroutine(CountUp(_currentScoreText, 0, r.EffectiveScore, _countupDuration));
        int displayBest = Mathf.Max(v.BestEffectiveScoreBefore, r.EffectiveScore);
        _bestScoreText.text = $"BEST  {displayBest:N0}";

        _newBestBadge.SetActive(v.IsNewBest);
        if (v.IsNewBest) StartCoroutine(BlinkBadge(_newBestBadge));

        _fullComboBadge.SetActive(r.IsFullCombo);
        _allPerfectBadge.SetActive(r.IsAllPerfect && !r.IsAllPerfectPlus);
        _allPerfectPlusBadge.SetActive(r.IsAllPerfectPlus);

        _ppCount.text      = r.PerfectPlusCount.ToString();
        _pCount.text       = r.PerfectCount.ToString();
        _grCount.text      = r.GreatCount.ToString();
        _gdCount.text      = r.GoodCount.ToString();
        _mCount.text       = r.MissCount.ToString();
        _maxComboText.text = r.MaxCombo.ToString();

        _fastCountText.text = r.FastCount.ToString();
        _lateCountText.text = r.LateCount.ToString();

        if (_sectorListContent != null && _sectorItemPrefab != null && r.SectorScores != null)
        {
            foreach (Transform t in _sectorListContent) Destroy(t.gameObject);
            for (int i = 0; i < r.SectorScores.Length; i++)
            {
                var go    = Instantiate(_sectorItemPrefab, _sectorListContent);
                var texts = go.GetComponentsInChildren<TextMeshProUGUI>(true);
                if (texts.Length >= 1) texts[0].text = $"S{i + 1}";
                if (texts.Length >= 2) texts[1].text = r.SectorScores[i].ToString("N0");

                var accent = go.transform.Find("AccentBar")?.GetComponent<Image>();
                if (accent != null)
                {
                    float ratio = Mathf.Clamp01(r.SectorScores[i] / 200_000f);
                    accent.color = Color.Lerp(
                        new Color(1f, .3f, .3f, 1f),
                        new Color(.3f, .9f, .3f, 1f), ratio);
                }
            }
        }
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    void OnRetry()
    {
        if (_params?.SourceGamePlayParameters != null && SceneRouter.Instance != null)
            SceneRouter.Instance.GoTo(SceneId.GamePlay, _params.SourceGamePlayParameters);
        else if (SceneRouter.Instance != null)
            SceneRouter.Instance.GoTo(SceneId.SongSelect);
        else
            SceneManager.LoadScene("SongSelect");
    }

    void OnToSelect()
    {
        if (SceneRouter.Instance != null)
            SceneRouter.Instance.GoTo(SceneId.SongSelect);
        else
            SceneManager.LoadScene("SongSelect");
    }

    void OnToTitle()
    {
        if (SceneRouter.Instance != null)
            SceneRouter.Instance.GoTo(SceneId.Title);
        else
            SceneManager.LoadScene("Title");
    }

    // ── Animations ────────────────────────────────────────────────────────────

    IEnumerator CountUp(TextMeshProUGUI target, int from, int to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            target.text = Mathf.RoundToInt(Mathf.Lerp(from, to, t)).ToString("N0");
            yield return null;
        }
        target.text = to.ToString("N0");
    }

    IEnumerator BlinkBadge(GameObject badge)
    {
        var tmp = badge.GetComponentInChildren<TextMeshProUGUI>(true);
        while (badge != null && badge.activeSelf)
        {
            if (tmp != null)
            {
                var c = tmp.color;
                c.a = Mathf.Lerp(.5f, 1f, (Mathf.Sin(Time.time * 4f) + 1f) * .5f);
                tmp.color = c;
            }
            yield return null;
        }
    }

    // ── Color helpers ─────────────────────────────────────────────────────────

    static Color GetRankColor(string rank)
    {
        switch (rank)
        {
            case "S+": return new Color(1.00f, 0.84f, 0.00f);
            case "S":  return new Color(1.00f, 0.95f, 0.40f);
            case "A+": return new Color(0.40f, 1.00f, 0.40f);
            case "A":  return new Color(0.40f, 0.85f, 1.00f);
            case "B":  return new Color(0.85f, 0.85f, 0.85f);
            case "C":  return new Color(0.95f, 0.60f, 0.30f);
            default:   return new Color(1.00f, 0.40f, 0.40f);
        }
    }

    static Color GetDifficultyColor(string diff)
    {
        switch (diff?.ToLower())
        {
            case "easy":   return new Color(0.40f, 1.00f, 0.40f);
            case "normal": return new Color(0.40f, 0.85f, 1.00f);
            case "hard":   return new Color(1.00f, 0.60f, 0.20f);
            case "extra":  return new Color(1.00f, 0.35f, 0.35f);
            default:       return Color.white;
        }
    }

    // ── Dummy data for direct-play in editor ─────────────────────────────────

    static PlayResultView CreateDummyView()
    {
        var record = new PlayRecord
        {
            PlayId               = "dummy",
            SongId               = "test_song",
            Difficulty           = "extra",
            RawScore             = 1_000_000,
            EffectiveScore       = 1_000_000,
            Rank                 = "S+",
            PerfectPlusCount     = 1000,
            PerfectCount         = 0,
            GreatCount           = 0,
            GoodCount            = 0,
            MissCount            = 0,
            MaxCombo             = 1000,
            FastCount            = 222,
            LateCount            = 222,
            TotalNotes           = 1000,
            SectorScores         = new[] { 200_000, 200_000, 200_000, 200_000, 200_000 },
            IsFullCombo          = true,
            IsAllPerfect         = true,
            IsAllPerfectPlus     = true,
            Modifiers            = new string[0],
            JudgmentEngineVersion = PlayRecordFactory.EngineVersion,
        };
        return new PlayResultView
        {
            Record                   = record,
            SongTitle                = "Test Song",
            SongArtist               = "Test Artist",
            Level                    = 18,
            BestEffectiveScoreBefore = 985_000,
            IsNewBest                = true,
        };
    }
}
