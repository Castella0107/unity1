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

    [Header("Play Option Info (読み取り専用 — 変更は O: PLAY OPTIONS)")]
    [SerializeField] TextMeshProUGUI    _playOptionInfoText;

    [Header("Settings — Per-Song Offset")]
    [SerializeField] Slider             _perSongOffsetSlider;
    [SerializeField] TextMeshProUGUI    _perSongOffsetValue;
    [SerializeField] Button             _perSongOffsetSaveButton;
    [SerializeField] TextMeshProUGUI    _saveButtonLabel;

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

    // ジャケットは LRU+破棄付きの JacketLoader を流用 (毎回 DL & Texture2D リークを防ぐ)。
    readonly JacketLoader _jacketLoader = new JacketLoader();
    // 難易度別 Level は曲を選ぶたびに譜面JSONを全パースしていた。表示に使うのは int 1個だけなので
    // (songId|difficulty)→Level をセッション内キャッシュし、再選択時の再パースを避ける。
    readonly Dictionary<string, int> _levelCache = new Dictionary<string, int>();
    // リスト行の難易度セル表示用ベスト記録 ((songId|difficulty) → ランク/FC/AP)
    readonly Dictionary<string, PersonalBest> _bests = new Dictionary<string, PersonalBest>();

    /// <summary>リスト並び替えモード。</summary>
    enum SortMode { IdAsc = 0, TitleAsc = 1, TitleDesc = 2, BpmAsc = 3, BpmDesc = 4 }
    static readonly string[] SortLabels =
        { "SONG ID", "TITLE (A to Z)", "TITLE (Z to A)", "BPM (LOW)", "BPM (HIGH)" };

    readonly List<SongMetadata> _songs     = new List<SongMetadata>();
    readonly List<GameObject>   _itemViews = new List<GameObject>();
    int        _selectedIndex;
    Difficulty _selectedDiff = Difficulty.Extra;
    SortMode   _sortMode     = SortMode.IdAsc;   // 既定は曲ID順 (= chart-admin の並び)

    PerSongOffset _currentPerSongOffset;
    bool          _perSongOffsetDirty;
    bool          _playOptionsWasOpen;   // PLAY OPTIONS クローズ検出 (閉じた直後に表示を同期)

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
        // [ ] / M 連打で溜めた HiSpeed/Modifier 変更をシーン離脱時にまとめて保存 (B-9)。
        PlayOptionsController.Flush();
    }

    void OnDestroy()
    {
        // JacketLoader が保持するテクスチャを破棄 (シーン離脱時の VRAM 解放)。
        _jacketLoader.ClearCache();
    }

    void Update()
    {
        // ポップアップ(プレイヤーデータ / PLAY OPTIONS)表示中は裏画面の入力を抑止する
        if (PlayerDataPopup.IsOpen) return;
        if (PlayOptionsController.IsOpen) { _playOptionsWasOpen = true; return; }
        if (_playOptionsWasOpen)
        {
            _playOptionsWasOpen = false;
            UpdatePlayOptionInfo();   // ポップアップで変更した SPEED/MODIFIER を反映
        }

        var kb = Keyboard.current;
        if (kb != null)
        {
            // O: 簡易コンフィグ (PLAY OPTIONS) ポップアップ
            if (kb.oKey.wasPressedThisFrame) { PlayOptionsController.Toggle(); return; }

            if (kb.f4Key.wasPressedThisFrame) CycleSort();

            // HiSpeed 調整: [ / ] で ±0.5（↑↓は曲選択のまま）
            if (kb.leftBracketKey.wasPressedThisFrame)  StepHiSpeed(-0.5f);
            if (kb.rightBracketKey.wasPressedThisFrame) StepHiSpeed(+0.5f);

            // Modifier 切替: M で None → Mirror → Random 循環
            if (kb.mKey.wasPressedThisFrame) CycleModifier();

            // F2: Config（ESC で本画面に戻る・選曲状態も復元）
            if (kb.f2Key.wasPressedThisFrame) OnOpenConfig();

            // R: 選択中の曲の楽曲別ランキング
            if (kb.rKey.wasPressedThisFrame) OnOpenRanking();
        }
    }

    // [ / ] で HiSpeed を 0.5 刻みで増減する (正本は PlayOptionsController.HiSpeed)。
    void StepHiSpeed(float delta)
    {
        PlayOptionsController.HiSpeed += delta;
        UpdatePlayOptionInfo();
    }

    // M で Modifier (None/Mirror/Random) を循環切替する (隠しショートカット、正本は PLAY OPTIONS)。
    void CycleModifier()
    {
        PlayOptionsController.ModifierIdx += 1;
        UpdatePlayOptionInfo();
    }

    // 詳細ペインの読み取り専用表示「SPEED x.x   MODIFIER XXX」を更新する。
    void UpdatePlayOptionInfo()
    {
        if (_playOptionInfoText == null) return;
        string mod = PlayOptionsController.ModifierIdx == 0
            ? "OFF" : PlayOptionsController.ModifierName.ToUpperInvariant();
        string auto = PlayOptionsController.AutoPlay ? "    AUTO ON" : "";
        _playOptionInfoText.text = $"SPEED {PlayOptionsController.HiSpeed:F1}    MODIFIER {mod}{auto}";
    }

    async void Start()
    {
        // EASY は廃止 (譜面が存在しない・K 指示 2026-08-01)。ボタンは旧ベイクのシーンに残っているので実行時に隠す
        if (_btnEasy != null) _btnEasy.gameObject.SetActive(false);
        _btnNormal.onClick.AddListener(() => SetDifficulty(Difficulty.Normal));
        _btnHard.onClick.AddListener(()   => SetDifficulty(Difficulty.Hard));
        _btnExtra.onClick.AddListener(()  => SetDifficulty(Difficulty.Extra));
        _backButton.onClick.AddListener(OnBack);
        if (_sortButton != null) _sortButton.onClick.AddListener(CycleSort);

        RhythmGame.UI.Common.ShortcutHintOverlay.Set(
            "↑↓: 曲選択   ←→: 難易度   Space: Play   O: プレイ設定   [ ]: 速度   F4: ソート   R: ランキング   F2: 設定   ESC: 戻る");

        // プロフィールカードをクリックでプレイヤーデータポップアップ（シーン再焼き不要のランタイム結線）
        WireProfileCardClick();

        // SPEED / MODIFIER の現在値表示 (変更は O: PLAY OPTIONS / [ ] / M)
        UpdatePlayOptionInfo();

        // Per-song offset
        _perSongOffsetSlider.minValue     = PerSongOffset.MinMs;
        _perSongOffsetSlider.maxValue     = PerSongOffset.MaxMs;
        _perSongOffsetSlider.wholeNumbers = true;
        _perSongOffsetSlider.onValueChanged.AddListener(OnPerSongOffsetChanged);
        _perSongOffsetSaveButton.onClick.AddListener(OnSavePerSongOffset);
        _currentPerSongOffset = PerSongOffset.DefaultFor("");
        UpdateSaveButtonAppearance();

        UpdateProfile();
        UpdateSortLabel();

        await LoadSongList();

        if (_songs.Count > 0)
        {
            // Config / SongRanking から戻った場合は選曲カーソルと難易度を復元する
            var restore = ParameterStore.GetPending<SongSelectParameters>();
            int focusIdx   = 0;
            var focusDiff  = Difficulty.Extra;
            if (restore != null)
            {
                int found = _songs.FindIndex(s => s.SongId == restore.FocusSongId);
                if (found >= 0) focusIdx = found;
                int d = System.Array.IndexOf(DiffNames, restore.Difficulty);
                if (d >= 1) focusDiff = (Difficulty)d;   // 0 (easy) は廃止済みなので復元しない
            }
            SelectSong(focusIdx);
            SetDifficulty(focusDiff);
        }
    }

    // プロフィールカード（_profileName の親）に Button を付与し、クリックでポップアップを開く。
    // 既存シーンの ProfileCard は Image 持ちなのでシーン再焼きなしで結線できる。
    void WireProfileCardClick()
    {
        if (_profileName == null) return;
        var card = _profileName.transform.parent;
        if (card == null) return;
        var img = card.GetComponent<Image>();
        if (img == null) return;

        var btn = card.GetComponent<Button>();
        if (btn == null) btn = card.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.transition    = Selectable.Transition.None;
        btn.onClick.AddListener(OnOpenPlayerData);
    }

    // ── Profile ──────────────────────────────────────────────────────────────

    void UpdateProfile()
    {
        if (_profileName != null)
            _profileName.text = string.IsNullOrEmpty(RhythmGame.Network.Api.AuthManager.DisplayName)
                ? RhythmGame.Network.Api.AuthManager.UserId
                : RhythmGame.Network.Api.AuthManager.DisplayName;
        if (_profileSub  != null) _profileSub.text  = "FREE PLAY";
    }

    // ── Song List ────────────────────────────────────────────────────────────

    async Task LoadSongList()
    {
        // サーバー楽曲同期を待ち合わせ (譜面はサーバーが正・chart_hash 整合。オフライン時はキャッシュ復元)
        await RhythmGame.Network.Api.ServerSongLibrary.EnsureSyncedAsync();

        // 譜面の置き場は複数 (ドキュメント / persistentDataPath / StreamingAssets)。
        // 差し替えを効かせるため列挙は ChartLoader に一本化する。

        // meta を1曲ずつ直列 await していたのを、同時数を絞ったバッチ並列に (起動待ちが曲数比例で伸びていた)。
        // 最終的に ApplySort で並べ替えるため取得順は不問。
        // chart-admin 由来の楽曲のみ表示 (K 指示 2026-07-30)。sample_*/test_song* は
        // 開発用データで、test_song は EditMode テストのフィクスチャとしてフォルダだけ残っている。
        var dirs = ChartLoader.EnumerateSongIds()
            .FindAll(id => !id.StartsWith("sample_") && !id.StartsWith("test_song"))
            .ConvertAll(id => ChartLoader.SongDir(id))
            .ToArray();
        _songs.Clear();
        const int Batch = 16;
        for (int start = 0; start < dirs.Length; start += Batch)
        {
            int end = Mathf.Min(start + Batch, dirs.Length);
            var tasks = new Task<SongMetadata>[end - start];
            for (int i = start; i < end; i++)
                tasks[i - start] = TryLoadMeta(Path.GetFileName(dirs[i]));
            var metas = await Task.WhenAll(tasks);
            foreach (var m in metas) if (m != null) _songs.Add(m);
        }

        await LoadAllBestRanksAsync();
        ApplySort();
        RebuildSongViews();
    }

    /// <summary>リスト行の難易度セル用に、全曲のベスト記録を一括取得してキャッシュする。</summary>
    async Task LoadAllBestRanksAsync()
    {
        _bests.Clear();
        if (RepositoryService.Instance?.IsReady != true) return;
        var bests = await RepositoryService.Instance.PlayRecords.GetAllBestsAsync();
        foreach (var b in bests)
            if (b != null && !string.IsNullOrEmpty(b.BestRank))
                _bests[b.SongId + "|" + b.Difficulty] = b;
    }

    static async Task<SongMetadata> TryLoadMeta(string songId)
    {
        try { return await ChartLoader.LoadMetaAsync(songId); }
        catch (System.Exception e) { Debug.LogWarning($"[SongSelect] Skip {songId}: {e.Message}"); return null; }
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
            StartCoroutine(LoadRowJacket(_songs[i].SongId, view));

            // 右端の難易度セル (旧「×」プレースホルダ) に難易度別ベストランクを表示。
            // FC は銀背景+銀冠、AP は金背景+金冠 (K 指示 2026-08-01、リザルトの判定フラグと同じ正本)。
            // EZ セルは EASY 廃止のため非表示 (再ベイク後のプレハブにはそもそも無い)
            for (int di = 0; di < 4; di++)
            {
                var cell = view.transform.Find("Diff" + DiffShort[di]);
                if (cell == null) continue;
                if (di == 0) { cell.gameObject.SetActive(false); continue; }
                var lv = cell.Find("Lv")?.GetComponent<TextMeshProUGUI>();
                if (lv == null) continue;
                if (_bests.TryGetValue(_songs[i].SongId + "|" + DiffNames[di], out var pb))
                {
                    // 旧ランク表で保存された記録もあるため、表示はスコアから引き直す (§2.6 現行表)
                    string rank = ScoreCalculator.DisplayRank(pb.BestEffectiveScore, pb.BestRank);
                    lv.text  = rank;
                    lv.color = RankColors.GetRankColor(rank);

                    bool ap = pb.HasAllPerfect || pb.HasAllPerfectPlus;
                    if (ap || pb.HasFullCombo)
                    {
                        var cellImg = cell.GetComponent<Image>();
                        if (cellImg != null)
                            cellImg.color = ap
                                ? new Color(0.85f, 0.66f, 0.14f, 0.55f)   // AP: 金背景
                                : new Color(0.72f, 0.76f, 0.84f, 0.40f);  // FC: 銀背景
                        CrownBadge.Attach(cell, ap ? CrownBadge.Gold : CrownBadge.Silver);
                    }
                }
                else
                {
                    lv.text  = "-";
                    lv.color = new Color(.45f, .45f, .45f);
                }
            }

            _itemViews.Add(view);
        }
    }

    /// <summary>リスト行の "Jacket" スロットへサムネイルを非同期ロードする。無い曲はグレーのまま。</summary>
    IEnumerator LoadRowJacket(string songId, GameObject view)
    {
        var task = _jacketLoader.LoadAsync(songId);
        yield return new WaitUntil(() => task.IsCompleted);
        if (view == null) yield break;                       // ソート等で行が破棄済み
        var slot = view.transform.Find("Jacket");
        if (slot == null) yield break;
        var img = slot.GetComponent<Image>();
        var tex = (!task.IsFaulted) ? task.Result : null;
        if (img == null || tex == null) yield break;         // 画像なし → プレースホルダー維持
        img.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        img.color  = Color.white;
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
            case SortMode.IdAsc:     _songs.Sort((a, b) => string.Compare(a.SongId, b.SongId, System.StringComparison.OrdinalIgnoreCase)); break;
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
        if (PlayerDataPopup.IsOpen || PlayOptionsController.IsOpen) return;
        var v = ctx.ReadValue<Vector2>();
        if      (v.y >  0.5f) SelectSong(_selectedIndex - 1);
        else if (v.y < -0.5f) SelectSong(_selectedIndex + 1);
        else if (v.x >  0.5f) SetDifficulty((Difficulty)Mathf.Min(3, (int)_selectedDiff + 1));
        else if (v.x < -0.5f) SetDifficulty((Difficulty)Mathf.Max(1, (int)_selectedDiff - 1));   // 下限は Normal (EASY 廃止)
    }

    void OnSubmit(InputAction.CallbackContext ctx) { if (!PlayerDataPopup.IsOpen && !PlayOptionsController.IsOpen) OnPlay(); }
    void OnCancel(InputAction.CallbackContext ctx) { if (!PlayerDataPopup.IsOpen && !PlayOptionsController.IsOpen) OnBack(); }

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
        StartCoroutine(LoadJacket(m.SongId, captured));
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

        if (_bestRankText != null)
            _bestRankText.text = ScoreCalculator.DisplayRank(best.BestEffectiveScore, best.BestRank);
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
        if (d == Difficulty.Easy) d = Difficulty.Normal;   // EASY 廃止
        _selectedDiff = d;
        var btns = new[] { _btnEasy, _btnNormal, _btnHard, _btnExtra };
        for (int i = 1; i < 4; i++)
        {
            if (btns[i] == null) continue;
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

    IEnumerator LoadJacket(string songId, int captured)
    {
        // LRU キャッシュ + 旧テクスチャ破棄は JacketLoader が担う。再選択時はキャッシュ即返しでDLなし。
        var task = _jacketLoader.LoadAsync(songId);
        while (!task.IsCompleted) yield return null;
        if (_selectedIndex != captured || _jacketImage == null) yield break;   // 選択が変わっていたら破棄
        var tex = (!task.IsFaulted) ? task.Result : null;
        _jacketImage.texture = tex;
        // 画像あり=白 (そのまま表示)。無し=プレースホルダーのグレー。
        // ビルダー既定の 25% グレーのままだと画像に乗算されて「グレーアウト」して見える。
        _jacketImage.color = tex != null ? Color.white : new Color(.25f, .25f, .25f);
    }

    IEnumerator LoadDifficultyLevels(string songId, int captured)
    {
        for (int i = 1; i < 4; i++)   // 0 (easy) は廃止・譜面なし
        {
            string key = songId + "|" + DiffNames[i];
            if (!_levelCache.TryGetValue(key, out int lvl))
            {
                // キャッシュミス時のみ譜面を読む。Level(int) 1個の取得のための全パースはここに限定される。
                var task = ChartLoader.LoadChartAsync(songId, DiffNames[i]);
                while (!task.IsCompleted) yield return null;
                if (_selectedIndex != captured) yield break;
                lvl = (!task.IsFaulted && task.Result != null) ? task.Result.Level : -1;
                _levelCache[key] = lvl;
            }
            else if (_selectedIndex != captured) yield break;

            if (_diffLevelTexts != null && i < _diffLevelTexts.Length && _diffLevelTexts[i] != null)
                _diffLevelTexts[i].text = $"{DiffShort[i]} {(lvl >= 0 ? lvl.ToString() : "-")}";
        }
    }

    // ── Play / Back ──────────────────────────────────────────────────────────

    void OnPlay()
    {
        if (_songs.Count == 0) return;

        if (_perSongOffsetDirty)
            Debug.LogWarning("[SongSelect] Unsaved per-song offset — will not affect this play");

        var meta = _songs[_selectedIndex];
        var parameters = new GamePlayParameters
        {
            SongId       = meta.SongId,
            Difficulty   = DiffNames[(int)_selectedDiff],
            HiSpeed      = PlayOptionsController.HiSpeed,
            JudgeOffset  = 0,   // offsets now come from DeviceProfile via RepositoryService
            VisualOffset = 0,
            Modifier     = PlayOptionsController.ModifierName,
            IsAutoPlay   = PlayOptionsController.AutoPlay,
        };

        SceneRouter.Instance.GoTo(SceneId.GamePlay, parameters);
    }

    void OnBack() => SceneRouter.Instance.GoTo(SceneId.Title);

    // ── Config / Ranking / Player Data ───────────────────────────────────────

    // 復帰時に現在の選曲状態を復元するためのパラメータ。
    SongSelectParameters CurrentSelectionParameters()
    {
        return new SongSelectParameters
        {
            FocusSongId = _songs.Count > 0 ? _songs[_selectedIndex].SongId : null,
            Difficulty  = DiffNames[(int)_selectedDiff],
        };
    }

    // F2: Config を開く（ESC で本画面に戻り、選曲状態も復元）。
    void OnOpenConfig()
    {
        SceneRouter.Instance.GoTo(SceneId.Config, new ConfigParameters
        {
            ReturnScene      = SceneId.SongSelect,
            ReturnParameters = CurrentSelectionParameters(),
        });
    }

    // R: 選択中の曲・難易度の楽曲別ランキングを開く。
    void OnOpenRanking()
    {
        if (_songs.Count == 0) return;
        var meta = _songs[_selectedIndex];
        SceneRouter.Instance.GoTo(SceneId.SongRanking, new SongRankingParameters
        {
            SongId     = meta.SongId,
            Difficulty = DiffNames[(int)_selectedDiff],
            SongTitle  = meta.Title,
            Artist     = meta.Artist,
        });
    }

    // プロフィールカードクリック: プレイヤーデータポップアップを開く。
    void OnOpenPlayerData()
    {
        if (PlayerDataPopup.IsOpen) return;
        PlayerDataPopup.Show();
    }
}
