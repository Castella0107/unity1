using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using RhythmGame.Network;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// リザルト画面を管理するコントローラー。
/// スコアカウントアップアニメーション・ランク表示・判定内訳・セクタースコア・バッジ表示、および再挑戦/曲選択/タイトルへのナビゲーションを担当する。
/// </summary>
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

    [Header("Server Result (optional — OnGUI fallback if unassigned)")]
    [SerializeField] TextMeshProUGUI _serverStatusText;

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
    string           _serverStatusFallback;   // shown via OnGUI when _serverStatusText is unassigned

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        Application.runInBackground = true;
        var map = _inputAsset.FindActionMap("UI", throwIfNotFound: true);
        _submitAction = map.FindAction("Submit", throwIfNotFound: true);
        _cancelAction = map.FindAction("Cancel", throwIfNotFound: true);
    }

    void OnEnable()
    {
        _submitAction.Enable();
        _cancelAction.Enable();
        _submitAction.performed += OnSubmitKey;
        _cancelAction.performed += OnCancelKey;
    }

    void OnDisable()
    {
        _submitAction.performed -= OnSubmitKey;
        _cancelAction.performed -= OnCancelKey;
    }

    void Start()
    {
        _retryButton.onClick.AddListener(OnRetry);
        _toSelectButton.onClick.AddListener(OnToSelect);
        _toTitleButton.onClick.AddListener(OnToTitle);

        _params = ParameterStore.GetPending<ResultParameters>();

        PlayResultView view = _params?.View ?? CreateDummyView();
        JacketBackgroundController.Instance?.SetJacket(view.Record?.SongId);
        if (_params == null)
            Debug.Log("[Result] No ResultParameters found — using dummy view");

        ApplyView(view);
        KickoffServerStatus(view.Record);
        if (IsTestPlayMode) WriteTestPlayResult(view.Record);
    }

    void OnSubmitKey(InputAction.CallbackContext _) => OnRetry();
    void OnCancelKey(InputAction.CallbackContext _) => OnToTitle();

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
        _difficultyText.color = RankColors.GetDifficultyColor(r.Difficulty);
        _songInfoText.text    = $"{v.SongTitle}  -  {v.SongArtist}";

        _rankText.text  = r.Rank;
        _rankText.color = RankColors.GetRankColor(r.Rank);

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

    // TestPlay (--chart 起動) では SongSelect/Title への戻りはアプリ終了に置き換える。
    // ChartEditor 側へリザルトを見せたあと、本体exeを閉じてエディタに戻す想定。
    static bool IsTestPlayMode => !string.IsNullOrEmpty(ChartLoader.OverrideBasePath);

    void OnRetry()
    {
        if (_params?.SourceGamePlayParameters != null && SceneRouter.Instance != null)
            SceneRouter.Instance.GoTo(SceneId.GamePlay, _params.SourceGamePlayParameters);
        else if (IsTestPlayMode)
            Application.Quit();
        else if (SceneRouter.Instance != null)
            SceneRouter.Instance.GoTo(SceneId.SongSelect);
        else
            SceneManager.LoadScene("SongSelect");
    }

    void OnToSelect()
    {
        if (IsTestPlayMode) { Application.Quit(); return; }
        if (SceneRouter.Instance != null)
            SceneRouter.Instance.GoTo(SceneId.SongSelect);
        else
            SceneManager.LoadScene("SongSelect");
    }

    void OnToTitle()
    {
        if (IsTestPlayMode) { Application.Quit(); return; }
        if (SceneRouter.Instance != null)
            SceneRouter.Instance.GoTo(SceneId.Title);
        else
            SceneManager.LoadScene("Title");
    }

    // ── Server validation + leaderboard rank ────────────────────────────────────
    // Reuses the submission GamePlayController already fired at song completion (shared via
    // ServerSubmissionTracker) so we never double-submit. Shows VALID/INVALID and, on VALID,
    // the player's leaderboard rank. Output goes to _serverStatusText when wired, otherwise an
    // OnGUI fallback panel — so it works without any scene editing.

    static bool IsServerEligible(ResultParameters p, PlayRecord rec)
        => p != null && rec != null && !rec.IsPvP && !IsTestPlayMode
           && ServerConfig.Enabled && NetworkClient.Instance != null;

    async void KickoffServerStatus(PlayRecord rec)
    {
        if (!IsServerEligible(_params, rec)) return;   // leave the panel hidden
        SetServerStatus("Server: checking...");

        // Wait briefly for THIS play's fire-and-forget submission to register its task.
        Task<NetworkClient.ValidateResult> task = null;
        float deadline = Time.realtimeSinceStartup + 1.5f;
        while (true)
        {
            if (ServerSubmissionTracker.PlayId == rec.PlayId && ServerSubmissionTracker.Task != null)
            { task = ServerSubmissionTracker.Task; break; }
            if (Time.realtimeSinceStartup >= deadline) break;
            await Task.Delay(50);
            if (this == null) return;   // scene unloaded while waiting
        }
        if (task == null) { SetServerStatus("Server: offline (not submitted)"); return; }

        NetworkClient.ValidateResult r;
        try { r = await task; }
        catch (Exception e) { SetServerStatus("Server: error - " + e.Message); return; }
        if (this == null) return;

        if (!r.Ok)           { SetServerStatus("Server: offline (queued for retry)"); return; }
        if (!r.Body.isValid) { SetServerStatus("Server: INVALID - " + r.Body.mismatchReason); return; }

        SetServerStatus("Server: VALID  (ranking...)");
        try
        {
            var pb = await NetworkClient.Instance.FetchPersonalBestAsync(rec.SongId, rec.Difficulty, LocalIdentity.UserId);
            if (this == null) return;
            if (pb.Ok && pb.Body != null && pb.Body.hasRecord)
                SetServerStatus($"Server: VALID   Rank #{pb.Body.overallRank} / {pb.Body.totalUsers}");
            else
                SetServerStatus("Server: VALID");
        }
        catch { SetServerStatus("Server: VALID"); }
    }

    void SetServerStatus(string text)
    {
        if (_serverStatusText != null) _serverStatusText.text = text;
        else                           _serverStatusFallback = text;
    }

    void OnGUI()
    {
        if (_serverStatusText != null || string.IsNullOrEmpty(_serverStatusFallback)) return;
        const float w = 440f, h = 30f;
        var style = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize  = 16,
            padding   = new RectOffset(10, 10, 4, 4),
        };
        GUI.Box(new Rect(16f, Screen.height - h - 16f, w, h), _serverStatusFallback, style);
    }

    // ── TestPlay result hand-off (ChartEditor round-trip) ───────────────────────
    // When launched from the ChartEditor with --testplay-result <path>, write the play
    // result there so the editor can read it on PVP exit and surface it to the charter.

    [Serializable]
    class TestPlayResultDto
    {
        public string songId;
        public string difficulty;
        public int    score;
        public int    rawScore;
        public string rank;
        public int    perfectPlus;
        public int    perfect;
        public int    great;
        public int    good;
        public int    miss;
        public int    maxCombo;
        public int    fastCount;
        public int    lateCount;
        public int    totalNotes;
        public bool   fullCombo;
        public bool   allPerfect;
        public bool   allPerfectPlus;
        public long   playedAtUnixMs;
    }

    static void WriteTestPlayResult(PlayRecord r)
    {
        string path = CommandLineArgs.Get("testplay-result");
        if (string.IsNullOrEmpty(path) || r == null) return;
        try
        {
            var dto = new TestPlayResultDto
            {
                songId         = r.SongId,
                difficulty     = r.Difficulty,
                score          = r.EffectiveScore,
                rawScore       = r.RawScore,
                rank           = r.Rank ?? "",
                perfectPlus    = r.PerfectPlusCount,
                perfect        = r.PerfectCount,
                great          = r.GreatCount,
                good           = r.GoodCount,
                miss           = r.MissCount,
                maxCombo       = r.MaxCombo,
                fastCount      = r.FastCount,
                lateCount      = r.LateCount,
                totalNotes     = r.TotalNotes,
                fullCombo      = r.IsFullCombo,
                allPerfect     = r.IsAllPerfect,
                allPerfectPlus = r.IsAllPerfectPlus,
                playedAtUnixMs = r.PlayedAtUnixMs,
            };
            File.WriteAllText(path, JsonUtility.ToJson(dto, true), new System.Text.UTF8Encoding(false));
            Debug.Log("[Result] TestPlay result written: " + path);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Result] TestPlay result write failed: " + e.Message);
        }
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
