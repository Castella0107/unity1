using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// プレイ履歴画面。2モード構成:
///   - Free play (ソロ): 各楽曲×難易度のベスト記録を、検索/難易度/ソートで絞り、行内アコーディオン展開。
///                       選択行を再クリック or Enter でそのベストリプレイを再生。
///   - Ladder match (PVP): 直近10戦のローカル記録。行クリックで展開し、各曲ジャケットでリプレイ再生。
/// 行のサマリ/詳細はシーンビルダーで baked-in 済みプレハブを使う(ランタイム生成しない)。
/// </summary>
public class HistoryController : MonoBehaviour
{
    [Header("Header")]
    [SerializeField] Button _backButton;

    [Header("Mode Tabs")]
    [SerializeField] Button _ladderTab;
    [SerializeField] Image  _ladderTabBg;
    [SerializeField] Button _freeTab;
    [SerializeField] Image  _freeTabBg;

    [Header("Free play - Filters")]
    [SerializeField] GameObject     _freeFilterBar;   // 検索/ソート/難易度の行 (Free モードのみ表示)
    [SerializeField] TMP_InputField _searchField;
    [SerializeField] TMP_Dropdown   _sortDropdown;
    [SerializeField] Button[]       _diffButtons;     // 4: easy/normal/hard/extra
    [SerializeField] Image[]        _diffButtonBgs;   // 4: 選択ハイライト

    [Header("List")]
    [SerializeField] RectTransform _listContent;
    [SerializeField] ScrollRect    _scrollRect;
    [SerializeField] GameObject    _soloItemPrefab;
    [SerializeField] GameObject    _pvpItemPrefab;
    [SerializeField] GameObject    _emptyState;
    [SerializeField] TextMeshProUGUI _emptyStateText;

    [Header("Input")]
    [SerializeField] InputActionAsset _inputAsset;

    enum Mode { Ladder, Free }
    Mode _mode = Mode.Free;

    static readonly string[] DiffOrder = { "easy", "normal", "hard", "extra" };
    string _diffFilter = "extra";
    string _searchText = "";
    int    _sortIndex  = 0;            // 0=曲名 1=日付 2=スコア 3=コンボ

    // Solo data: 各 (曲×難易度) のベスト記録 (全難易度ぶん読み込み、表示時に難易度で絞る)
    readonly List<PlayRecord>          _soloBests = new List<PlayRecord>();
    readonly Dictionary<string,string> _titles    = new Dictionary<string,string>();
    readonly List<HistorySoloRowView>  _soloViews = new List<HistorySoloRowView>();

    // PVP data: 直近10戦
    readonly List<PvpMatchRecord>      _pvpMatches = new List<PvpMatchRecord>();
    readonly List<HistoryPvpRowView>   _pvpViews   = new List<HistoryPvpRowView>();

    readonly JacketLoader _jackets = new JacketLoader();

    int _selectedIndex = -1;
    int _pvpSongCursor = 0;

    InputAction _navigateAction;
    InputAction _submitAction;
    InputAction _cancelAction;

    int RowCount => _mode == Mode.Free ? _soloViews.Count : _pvpViews.Count;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        var map         = _inputAsset.FindActionMap("UI", throwIfNotFound: true);
        _navigateAction = map.FindAction("Navigate", throwIfNotFound: true);
        _submitAction   = map.FindAction("Submit",   throwIfNotFound: true);
        _cancelAction   = map.FindAction("Cancel",   throwIfNotFound: true);
    }

    void OnEnable()
    {
        _navigateAction.performed += OnNavigate;
        _submitAction.performed   += OnSubmit;
        _cancelAction.performed   += OnCancel;
        _navigateAction.Enable();
        _submitAction.Enable();
        _cancelAction.Enable();

        JacketBackgroundController.Instance?.SetFallback();
    }

    void OnDisable()
    {
        _navigateAction.performed -= OnNavigate;
        _submitAction.performed   -= OnSubmit;
        _cancelAction.performed   -= OnCancel;
    }

    async void Start()
    {
        // タイトルの子ボタンから固定モードで起動 (独立画面化、2026-06-07)。
        // 画面内の Ladder/Free 切替は廃止 — タブボタンは隠し、Tab キー/パッド切替も無効。
        var p = ParameterStore.GetPending<HistoryParameters>();
        var initialMode = p?.Mode == "Ladder" ? Mode.Ladder : Mode.Free;

        SetupUI();
        if (_ladderTab != null) _ladderTab.gameObject.SetActive(false);
        if (_freeTab   != null) _freeTab.gameObject.SetActive(false);

        RhythmGame.UI.Common.ShortcutHintOverlay.Set(initialMode == Mode.Free
            ? "↑↓: 行   1-4: 難易度   Space: リプレイ   ESC: 戻る"
            : "↑↓: 行   ←→: 曲カーソル   Space: リプレイ   ESC: 戻る");

        await LoadAllData();
        SwitchMode(initialMode);
    }

    // ── Setup ─────────────────────────────────────────────────────────────────

    void SetupUI()
    {
        if (_backButton != null)
            _backButton.onClick.AddListener(() => SceneRouter.Instance.GoTo(SceneId.Title));

        if (_ladderTab != null) _ladderTab.onClick.AddListener(() => SwitchMode(Mode.Ladder));
        if (_freeTab   != null) _freeTab.onClick.AddListener(()   => SwitchMode(Mode.Free));

        if (_searchField != null)
            _searchField.onValueChanged.AddListener(t => { _searchText = t ?? ""; RebuildFreeList(); });

        if (_sortDropdown != null)
        {
            _sortDropdown.ClearOptions();
            _sortDropdown.AddOptions(new List<string> { "ソート：曲名", "ソート：日付", "ソート：スコア", "ソート：コンボ" });
            _sortDropdown.onValueChanged.AddListener(i => { _sortIndex = i; RebuildFreeList(); });
        }

        if (_diffButtons != null)
        {
            for (int i = 0; i < _diffButtons.Length && i < DiffOrder.Length; i++)
            {
                int idx = i;
                if (_diffButtons[i] != null)
                    _diffButtons[i].onClick.AddListener(() => SelectDifficulty(DiffOrder[idx]));
            }
        }
        UpdateDifficultyHighlight();
    }

    // ── Data ──────────────────────────────────────────────────────────────────

    async Task LoadAllData()
    {
        var repo = RepositoryService.Instance?.PlayRecords;
        if (repo == null) return;

        // Solo: 全難易度のベスト記録を読み込む
        _soloBests.Clear();
        var bests = await repo.GetAllBestsAsync();
        foreach (var b in bests)
        {
            if (string.IsNullOrEmpty(b.BestPlayId)) continue;
            var rec = await repo.GetByIdAsync(b.BestPlayId);
            if (rec != null) _soloBests.Add(rec);
        }

        // PVP: サーバー履歴(正本・端末跨ぎで残る)とローカル記録(リプレイ+セクター詳細)を統合
        await LoadPvpMatchesMergedAsync(repo);

        // 曲名の事前解決 (検索/ソートを同期処理にするため)
        var ids = new HashSet<string>();
        foreach (var r in _soloBests) ids.Add(r.SongId);
        foreach (var m in _pvpMatches) if (m.SongIds != null) foreach (var s in m.SongIds) ids.Add(s);
        foreach (var id in ids)
        {
            if (string.IsNullOrEmpty(id) || _titles.ContainsKey(id)) continue;
            try
            {
                var meta = await ChartLoader.LoadMetaAsync(id);
                _titles[id] = !string.IsNullOrEmpty(meta?.Title) ? meta.Title : id;
            }
            catch { _titles[id] = id; }
        }
    }

    /// <summary>
    /// PVP 対戦履歴を「サーバー優先 + ローカル統合」で読み込む。
    ///   - サーバー /users/me/matches を正本(全期間・端末跨ぎで残る)とする。
    ///   - match_id がローカルにもある試合はローカル(リプレイ再生 + セクター内訳付き)を採用。
    ///   - サーバーのみの試合は曲スコアから合成(リプレイ無し・曲単位の勝敗+正答率%)。
    ///   - サーバー未取得(オフライン/失敗)時はローカルのみにフォールバック。
    /// </summary>
    async Task LoadPvpMatchesMergedAsync(IPlayRecordRepository repo)
    {
        _pvpMatches.Clear();

        var local = await repo.GetRecentPvpMatchesAsync(50);
        var localById = new Dictionary<string, PvpMatchRecord>();
        foreach (var r in local)
            if (!string.IsNullOrEmpty(r.MatchId) && !localById.ContainsKey(r.MatchId))
                localById[r.MatchId] = r;

        var merged   = new List<PvpMatchRecord>();
        var consumed = new HashSet<string>();

        if (!RhythmGame.Network.Api.AuthManager.OfflineMode)
        {
            var resp = await RhythmGame.Network.Api.PvpApi.GetMyMatchesAsync(50, 0);
            if (resp.Ok && resp.Data?.Matches != null)
            {
                foreach (var sm in resp.Data.Matches)
                {
                    if (sm == null) continue;
                    if (!string.IsNullOrEmpty(sm.MatchId) && localById.TryGetValue(sm.MatchId, out var localRec))
                        merged.Add(localRec);                  // ローカル: リプレイ + セクター詳細
                    else
                        merged.Add(SynthesizeFromServer(sm));  // サーバーのみ: 合成
                    if (!string.IsNullOrEmpty(sm.MatchId)) consumed.Add(sm.MatchId);
                }
            }
        }

        // サーバーに無いローカル記録(オフライン保存/サーバー反映前の直近戦)は欠落させない
        foreach (var r in local)
            if (string.IsNullOrEmpty(r.MatchId) || !consumed.Contains(r.MatchId))
                merged.Add(r);

        merged.Sort((a, b) => b.CompletedAtUnixMs.CompareTo(a.CompletedAtUnixMs));   // 新しい順
        _pvpMatches.AddRange(merged);
    }

    // サーバー履歴 DTO をローカル記録形式に合成する(リプレイ無し)。セクター内訳は持たないため、
    // 曲スコアを5等分して「曲単位の勝敗ダイヤ + 正答率%」を表現(余りはS1に寄せ Σ=my_score を厳密化)。
    static PvpMatchRecord SynthesizeFromServer(RhythmGame.Network.Api.UserMatchDto sm)
    {
        int n = sm.Songs?.Count ?? 0;
        var songIds = new string[n];
        var diffs   = new string[n];
        var selfSec = new int[n * 5];
        var oppSec  = new int[n * 5];
        for (int j = 0; j < n; j++)
        {
            var s = sm.Songs[j];
            songIds[j] = s.SongId;
            diffs[j]   = s.Difficulty;
            int sb = s.MyScore / 5, ob = s.OpponentScore / 5;
            for (int k = 0; k < 5; k++) { selfSec[j * 5 + k] = sb; oppSec[j * 5 + k] = ob; }
            selfSec[j * 5] = s.MyScore - sb * 4;            // 余りを S1 に寄せ Σ=my_score
            oppSec[j * 5]  = s.OpponentScore - ob * 4;
        }
        return new PvpMatchRecord
        {
            MatchId              = sm.MatchId,
            SelfUserId           = string.IsNullOrEmpty(RhythmGame.Network.Api.AuthManager.DisplayName)
                                       ? RhythmGame.Network.Api.AuthManager.UserId
                                       : RhythmGame.Network.Api.AuthManager.DisplayName,
            OpponentId           = sm.Opponent != null
                                       ? (string.IsNullOrEmpty(sm.Opponent.DisplayName) ? sm.Opponent.UserId : sm.Opponent.DisplayName)
                                       : null,
            ResultKind           = sm.Outcome == "win" ? 1 : (sm.Outcome == "lose" ? 2 : 0),
            SelfPoints           = sm.MyPoints / 1000.0,
            OpponentPoints       = sm.OpponentPoints / 1000.0,
            SelfRatingBefore     = sm.RatingBefore,
            SelfRatingAfter      = sm.RatingAfter,
            SongIds              = songIds,
            Difficulties         = diffs,
            SelfSectorScores     = selfSec,
            OpponentSectorScores = oppSec,
            SelfReplayPaths      = null,    // サーバーのみ=ローカルリプレイ無し
            CompletedAtUnixMs    = ParseIsoMs(sm.PlayedAt),
        };
    }

    static long ParseIsoMs(string iso)
    {
        if (!string.IsNullOrEmpty(iso) &&
            DateTimeOffset.TryParse(iso, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var dto))
            return dto.ToUnixTimeMilliseconds();
        return 0;
    }

    string TitleOf(string songId) =>
        songId != null && _titles.TryGetValue(songId, out var t) ? t : songId;

    // ── Mode switching ──────────────────────────────────────────────────────────

    void SwitchMode(Mode mode)
    {
        _mode = mode;
        if (_freeFilterBar != null) _freeFilterBar.SetActive(mode == Mode.Free);
        if (_ladderTabBg != null) _ladderTabBg.color = TabColor(mode == Mode.Ladder);
        if (_freeTabBg   != null) _freeTabBg.color   = TabColor(mode == Mode.Free);

        if (mode == Mode.Free) RebuildFreeList();
        else                   RebuildPvpList();
    }

    static Color TabColor(bool selected) =>
        selected ? new Color(1f, 1f, 1f, 0.22f) : new Color(1f, 1f, 1f, 0.06f);

    void SelectDifficulty(string diff)
    {
        _diffFilter = diff;
        UpdateDifficultyHighlight();
        RebuildFreeList();
    }

    void UpdateDifficultyHighlight()
    {
        if (_diffButtonBgs == null) return;
        for (int i = 0; i < _diffButtonBgs.Length && i < DiffOrder.Length; i++)
            if (_diffButtonBgs[i] != null)
                _diffButtonBgs[i].color = DiffOrder[i] == _diffFilter
                    ? new Color(1f, 1f, 1f, 0.25f) : new Color(1f, 1f, 1f, 0f);
    }

    // ── Free play list ──────────────────────────────────────────────────────────

    void RebuildFreeList()
    {
        if (_mode != Mode.Free) return;
        ClearRows();

        if (_soloItemPrefab == null)
        {
            Debug.LogError("[History] _soloItemPrefab is not wired — run menu 'Tools/Build History Scene + Prefab' to regenerate the scene.");
            ShowEmpty(true, "Scene not rebuilt");
            return;
        }

        IEnumerable<PlayRecord> q = _soloBests.Where(r => r.Difficulty == _diffFilter);

        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            string needle = _searchText.Trim().ToLowerInvariant();
            q = q.Where(r => (TitleOf(r.SongId) ?? "").ToLowerInvariant().Contains(needle));
        }

        switch (_sortIndex)
        {
            case 1:  q = q.OrderByDescending(r => r.PlayedAtUnixMs);                 break; // 日付
            case 2:  q = q.OrderByDescending(r => r.EffectiveScore);                 break; // スコア
            case 3:  q = q.OrderByDescending(r => r.MaxCombo);                       break; // コンボ
            default: q = q.OrderBy(r => TitleOf(r.SongId), StringComparer.Ordinal);  break; // 曲名
        }

        var list = q.ToList();
        ShowEmpty(list.Count == 0, "No plays yet");

        foreach (var rec in list)
        {
            var go   = Instantiate(_soloItemPrefab, _listContent);
            var view = new HistorySoloRowView(go, rec, _jackets);
            view.SetTitle(TitleOf(rec.SongId));
            int idx = _soloViews.Count;
            view.Button.onClick.AddListener(() => OnSoloRowClicked(idx));
            _soloViews.Add(view);
        }

        SelectIndex(list.Count > 0 ? 0 : -1);
    }

    // ── Ladder (PVP) list ─────────────────────────────────────────────────────

    void RebuildPvpList()
    {
        if (_mode != Mode.Ladder) return;
        ClearRows();

        if (_pvpItemPrefab == null)
        {
            Debug.LogError("[History] _pvpItemPrefab is not wired — run menu 'Tools/Build History Scene + Prefab' to regenerate the scene.");
            ShowEmpty(true, "Scene not rebuilt");
            return;
        }

        ShowEmpty(_pvpMatches.Count == 0, "No matches yet");

        foreach (var m in _pvpMatches)
        {
            var go   = Instantiate(_pvpItemPrefab, _listContent);
            var view = new HistoryPvpRowView(go, m, _jackets, TitleOf);
            int idx = _pvpViews.Count;
            view.Button.onClick.AddListener(() => OnPvpRowClicked(idx));
            view.OnSongReplayRequested += songIndex => LaunchPvpReplay(m, songIndex);
            _pvpViews.Add(view);
        }

        SelectIndex(_pvpMatches.Count > 0 ? 0 : -1);
    }

    // ── Row interaction ─────────────────────────────────────────────────────────

    void OnSoloRowClicked(int index)
    {
        if (index == _selectedIndex) PlaySelectedSolo();   // 既選択を再クリック → 再生
        else                         SelectIndex(index);
    }

    void OnPvpRowClicked(int index)
    {
        if (index == _selectedIndex) return;               // 展開済みは維持 (ジャケットで再生)
        SelectIndex(index);
    }

    void SelectIndex(int index)
    {
        _selectedIndex = index;
        _pvpSongCursor = 0;

        if (_mode == Mode.Free)
        {
            for (int i = 0; i < _soloViews.Count; i++)
            {
                bool sel = i == index;
                _soloViews[i].SetSelected(sel);
                _soloViews[i].SetExpanded(sel);
            }
        }
        else
        {
            for (int i = 0; i < _pvpViews.Count; i++)
            {
                bool sel = i == index;
                _pvpViews[i].SetSelected(sel);
                _pvpViews[i].SetExpanded(sel);
                _pvpViews[i].SetSongCursor(sel ? 0 : -1);
            }
        }

        if (_listContent != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(_listContent);
        ScrollToSelected();
    }

    void PlaySelectedSolo()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _soloViews.Count) return;
        var rec = _soloViews[_selectedIndex].Record;
        LaunchReplay(rec.SongId, rec.Difficulty, rec.ReplayPath, isPvp: false);
    }

    void LaunchPvpReplay(PvpMatchRecord m, int songIndex)
    {
        if (m.SelfReplayPaths == null || songIndex < 0 || songIndex >= m.SelfReplayPaths.Length) return;
        string songId = (m.SongIds      != null && songIndex < m.SongIds.Length)      ? m.SongIds[songIndex]      : null;
        string diff   = (m.Difficulties != null && songIndex < m.Difficulties.Length) ? m.Difficulties[songIndex] : "extra";
        LaunchReplay(songId, diff, m.SelfReplayPaths[songIndex], isPvp: true);
    }

    // isPvp: PVP(Ladder)由来のリプレイか。リプレイ終了/ポーズ離脱時に History の正しいタブ
    // (Ladder/Free)へ戻すために GamePlayParameters.IsPvp へ伝搬する。
    void LaunchReplay(string songId, string difficulty, string replayPath, bool isPvp)
    {
        if (string.IsNullOrEmpty(replayPath) || !File.Exists(replayPath))
        {
            Debug.LogWarning("[History] Replay file not available: " + (replayPath ?? "null"));
            return;
        }
        var prm = new GamePlayParameters
        {
            SongId               = songId,
            Difficulty           = string.IsNullOrEmpty(difficulty) ? "extra" : difficulty,
            IsReplay             = true,
            IsPvp                = isPvp,
            ReplayPath           = replayPath,
            InitialPlaybackSpeed = 1.0,
        };
        if (SceneRouter.Instance != null) SceneRouter.Instance.GoTo(SceneId.GamePlay, prm);
        else UnityEngine.SceneManagement.SceneManager.LoadScene("GamePlay");
    }

    // ── List helpers ──────────────────────────────────────────────────────────

    void ClearRows()
    {
        foreach (var v in _soloViews) if (v.Root != null) Destroy(v.Root);
        foreach (var v in _pvpViews)  if (v.Root != null) Destroy(v.Root);
        _soloViews.Clear();
        _pvpViews.Clear();
        _selectedIndex = -1;
    }

    void ShowEmpty(bool empty, string message)
    {
        if (_emptyState != null) _emptyState.SetActive(empty);
        if (empty && _emptyStateText != null) _emptyStateText.text = message;
    }

    void ScrollToSelected()
    {
        if (_scrollRect == null || _selectedIndex < 0 || RowCount == 0) return;
        var root = _mode == Mode.Free ? _soloViews[_selectedIndex].Root : _pvpViews[_selectedIndex].Root;
        var itemRT   = root.GetComponent<RectTransform>();
        float itemY  = Mathf.Abs(itemRT.anchoredPosition.y);
        float contentH = _listContent.rect.height;
        float viewH    = _scrollRect.viewport.rect.height;
        if (contentH <= viewH) return;
        _scrollRect.verticalNormalizedPosition = 1f - Mathf.Clamp01(itemY / (contentH - viewH));
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    void OnNavigate(InputAction.CallbackContext ctx)
    {
        if (RowCount == 0) return;
        Vector2 v = ctx.ReadValue<Vector2>();
        if      (v.y >  0.5f) SelectIndex((_selectedIndex - 1 + RowCount) % RowCount);
        else if (v.y < -0.5f) SelectIndex((_selectedIndex + 1) % RowCount);
        else if (_mode == Mode.Ladder && _selectedIndex >= 0)
        {
            if      (v.x >  0.5f) MoveSongCursor(+1);
            else if (v.x < -0.5f) MoveSongCursor(-1);
        }
    }

    void MoveSongCursor(int delta)
    {
        _pvpSongCursor = Mathf.Clamp(_pvpSongCursor + delta, 0, 2);
        _pvpViews[_selectedIndex].SetSongCursor(_pvpSongCursor);
    }

    void OnSubmit(InputAction.CallbackContext ctx)
    {
        if (_selectedIndex < 0) return;
        if (_mode == Mode.Free) PlaySelectedSolo();
        else                    LaunchPvpReplay(_pvpViews[_selectedIndex].Match, _pvpSongCursor);
    }

    void OnCancel(InputAction.CallbackContext ctx) => SceneRouter.Instance.GoTo(SceneId.Title);

    void Update()
    {
        // モード切替(Tab/LB RB)は独立画面化に伴い廃止 — モードはタイトルの子ボタンで選ぶ。
        var kb = Keyboard.current;
        if (kb != null && _mode == Mode.Free)
        {
            // 数字キー 1-4: 難易度フィルター（Free モード時）
            if (kb.digit1Key.wasPressedThisFrame) SelectDifficulty(DiffOrder[0]);
            if (kb.digit2Key.wasPressedThisFrame) SelectDifficulty(DiffOrder[1]);
            if (kb.digit3Key.wasPressedThisFrame) SelectDifficulty(DiffOrder[2]);
            if (kb.digit4Key.wasPressedThisFrame) SelectDifficulty(DiffOrder[3]);
        }
    }
}
