using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using RhythmGame.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmGame.UI.Pvp
{
    /// <summary>
    /// PVP 正規フロー 3 画面 (Prematch / SongPick / BanPhase) 共通コントローラー。
    ///
    /// 実 BAN/PICK ドラフト(サーバー同期・ブラインド方式):
    ///   - Prematch  : 対戦カードの導入。READY で SongPick へ。
    ///   - SongPick  : プール 20 曲から各自 1 曲ブラインド PICK。両者完了で開示 → BanPhase。
    ///   - BanPhase  : 両 PICK 後に抽選された 3 候補から各自 1 曲ブラインド BAN。両者完了で
    ///                 残り 1 曲が確定 → 3 曲 = [PickA, PickB, 3曲目] → 本戦開始。
    ///
    /// サーバーは <c>POST/GET /api/pvp/match/{id}/draft[/pick|/ban]</c> を既に実装済み。
    /// 状態の真実はサーバーが保持し、本コントローラは polling で同期する(シーン再入も安全)。
    /// A/B↔Self/Opp の対応は <see cref="PvpFlowController.ResolveSidesAsync"/> で解決する。
    ///
    /// UI は <c>BuildPvpScenes</c> が baked-in 生成し SerializeField を結線する。未結線(_headerText==null)
    /// の場合は OnGUI フォールバックで操作可能。
    /// </summary>
    public class PvpDraftScreenController : MonoBehaviour
    {
        public enum Phase { Prematch, SongPick, BanPhase }

        enum Step { Loading, Intro, Selecting, Submitting, Waiting, Reveal, NoMatch }

        [SerializeField] Phase _phase = Phase.Prematch;

        [Header("Optional UI (OnGUI fallback if header text is null)")]
        [SerializeField] TextMeshProUGUI _headerText;
        [SerializeField] TextMeshProUGUI _youNameText;
        [SerializeField] TextMeshProUGUI _oppNameText;
        [SerializeField] TextMeshProUGUI _infoText;     // 画面説明 / 指示
        [SerializeField] TextMeshProUGUI _statusText;   // SELECT / WAITING / REVEALED 等の状態
        [SerializeField] TextMeshProUGUI _timerText;    // PICK/BAN 制限時間カウントダウン
        [SerializeField] TextMeshProUGUI _revealText;   // YOU x / OPP y の開示
        [SerializeField] TextMeshProUGUI _songsText;    // 候補 / 確定ラインナップ
        [SerializeField] TextMeshProUGUI _primaryLabel; // primary ボタンのラベル
        [SerializeField] Button          _primaryButton;
        [SerializeField] Button          _cancelButton;

        [Header("Tiles (baked-in: 20 for SongPick, 3 for BanPhase, 0 for Prematch)")]
        [SerializeField] DraftTileView[] _tiles = new DraftTileView[0];

        // 自分=シアン / 相手=レッド (History / Matchmaking と統一)
        static readonly Color Cyan = new Color(0.17f, 0.85f, 0.90f, 1f);
        static readonly Color Red  = new Color(0.95f, 0.30f, 0.42f, 1f);
        static readonly Color Dim  = new Color(1f, 1f, 1f, 0.45f);

        const float SelectSeconds = 60f;   // PICK/BAN 制限時間 (0 で自動ロック)

        Step   _step = Step.Loading;
        bool   _isBan;                     // _phase==BanPhase
        bool   _alive;
        string _selectedSongId;
        DraftStateDto _state;

        readonly JacketLoader _jackets = new JacketLoader();
        readonly Dictionary<string, string> _titles  = new Dictionary<string, string>();
        readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();

        bool  _timerActive;
        float _timeLeft;

        PvpFlowController Pvp => PvpFlowController.Instance;

        void Start()
        {
            // MCP/ウィンドウ非フォーカス時もループを止めないために必須 (他 PVP 画面と同様)。
            Application.runInBackground = true;
            JacketBackgroundController.Instance?.SetCanvasEnabled(true);
            JacketBackgroundController.Instance?.SetFallback();

            _alive = true;
            _isBan = _phase == Phase.BanPhase;

            if (_primaryButton != null) _primaryButton.onClick.AddListener(OnPrimary);
            if (_cancelButton  != null) _cancelButton.onClick.AddListener(OnCancel);

            // タイルのクリックを index で配線
            if (_tiles != null)
            {
                for (int i = 0; i < _tiles.Length; i++)
                {
                    if (_tiles[i] == null || _tiles[i].Button == null) continue;
                    int idx = i;
                    _tiles[i].Button.onClick.AddListener(() => OnTileClicked(idx));
                    _tiles[i].SetVisible(false);
                }
            }

            if (_headerText  != null) _headerText.text = HeaderFor(_phase);
            if (_timerText   != null) _timerText.text  = "";
            if (_revealText  != null) _revealText.text = "";
            if (_songsText   != null) _songsText.text  = "";

            _ = InitAsync();
        }

        void OnDisable() => _alive = false;

        async Task InitAsync()
        {
            var pvp = Pvp;
            if (pvp == null || !pvp.IsActive)
            {
                _step = Step.NoMatch;
                if (_youNameText != null) _youNameText.text = LocalIdentity.UserId;
                if (_oppNameText != null) _oppNameText.text = "???";
                if (_infoText    != null) _infoText.text    = "(no active match)";
                SetPrimary("BACK", true);
                return;
            }

            if (_youNameText != null) _youNameText.text = pvp.SelfUserId;
            if (_oppNameText != null) _oppNameText.text = string.IsNullOrEmpty(pvp.OpponentId) ? "???" : pvp.OpponentId;

            await pvp.ResolveSidesAsync();
            if (!_alive) return;

            switch (_phase)
            {
                case Phase.Prematch:
                    _step = Step.Intro;
                    if (_infoText   != null) _infoText.text   = "BEST OF 3     3 SONGS x 5 SECTORS";
                    if (_statusText != null) _statusText.text = "Ready when you are.";
                    SetPrimary("TO SONG PICK", true);
                    break;

                case Phase.SongPick:
                case Phase.BanPhase:
                    if (_infoText != null)
                        _infoText.text = _isBan
                            ? "Ban 1 of the 3 candidates. Lowest survivor becomes song 3."
                            : "Pick 1 song from the season pool (20).";
                    await DraftFlowAsync();
                    break;
            }
        }

        // ── ドラフト本体 (SongPick / BanPhase 共通) ──────────────────────────────
        async Task DraftFlowAsync()
        {
            _state = await SafeFetchDraftAsync();
            if (!_alive) return;
            if (_state == null) { ShowError("Could not load draft state."); return; }

            // BanPhase に来たがまだ pick 中(相手の PICK 待ち)の保険
            if (_isBan && _state.phase == "pick")
            {
                _step = Step.Waiting;
                SetStatus("WAITING FOR OPPONENT'S PICK...");
                SetPrimary("", false);
                await PollLoopAsync();
                return;
            }

            if (IsPhaseComplete(_state)) { EnterReveal(); return; }
            if (SelfHasChosen(_state))
            {
                _step = Step.Waiting;
                SetStatus("WAITING FOR OPPONENT...");
                SetPrimary("", false);
                await PollLoopAsync();
                return;
            }

            EnterSelecting();
        }

        void EnterSelecting()
        {
            _step = Step.Selecting;
            _selectedSongId = null;

            var set = CurrentSet(_state);
            PopulateTiles(set, interactable: true);
            _ = LoadVisualsAsync(set);

            SetStatus(_isBan ? "BAN 1 OF 3" : "PICK 1 SONG");
            SetPrimary("LOCK IN", false);

            _timeLeft    = SelectSeconds;
            _timerActive = SelectSeconds > 0f;
            if (!_timerActive && _timerText != null) _timerText.text = "";
        }

        void OnTileClicked(int index)
        {
            if (_step != Step.Selecting) return;
            if (_tiles == null || index < 0 || index >= _tiles.Length || _tiles[index] == null) return;
            var tile = _tiles[index];
            if (string.IsNullOrEmpty(tile.SongId)) return;

            _selectedSongId = tile.SongId;
            for (int i = 0; i < _tiles.Length; i++)
                if (_tiles[i] != null) _tiles[i].SetSelected(_tiles[i].SongId == _selectedSongId);

            SetPrimary("LOCK IN", true);
        }

        void OnPrimary()
        {
            switch (_step)
            {
                case Step.NoMatch:
                    SceneRouter.Instance?.GoTo(SceneId.Title);
                    break;
                case Step.Intro:
                    SceneRouter.Instance?.GoTo(SceneId.PVPSongPick);
                    break;
                case Step.Selecting:
                    if (!string.IsNullOrEmpty(_selectedSongId)) _ = LockInAsync();
                    break;
                case Step.Reveal:
                    AdvanceFromReveal();
                    break;
            }
        }

        void AdvanceFromReveal()
        {
            if (_isBan)
            {
                var pvp = Pvp;
                if (pvp != null && _state != null && _state.songs != null && _state.songs.Count > 0)
                {
                    pvp.SetDraftSongs(_state.songs);
                    pvp.BeginSongs();   // 1 曲目の GamePlay を起動
                }
                else
                {
                    ShowError("Draft incomplete — cannot start.");
                }
            }
            else
            {
                SceneRouter.Instance?.GoTo(SceneId.PVPBanPhase);
            }
        }

        async Task LockInAsync()
        {
            string songId = _selectedSongId;
            if (string.IsNullOrEmpty(songId)) return;

            _timerActive = false;
            _step = Step.Submitting;
            SetTilesInteractable(false);
            SetPrimary("", false);
            SetStatus("LOCKING IN...");

            var net = NetworkClient.Instance;
            var pvp = Pvp;
            if (net == null || pvp == null) { ShowError("Network unavailable."); return; }

            var res = _isBan
                ? await net.DraftBanAsync(pvp.MatchId, pvp.SelfUserId, songId)
                : await net.DraftPickAsync(pvp.MatchId, pvp.SelfUserId, songId);
            if (!_alive) return;

            if (!res.Ok)
            {
                // 既に選択済み(二重送信/再入)なら状態を取り直して継続。それ以外は選択に戻す。
                if (res.Error != null && res.Error.IndexOf("already", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _state = await SafeFetchDraftAsync();
                    if (!_alive) return;
                    if (_state != null && IsPhaseComplete(_state)) { EnterReveal(); return; }
                    _step = Step.Waiting;
                    SetStatus("WAITING FOR OPPONENT...");
                    await PollLoopAsync();
                    return;
                }
                ShowError("Lock-in failed: " + Short(res.Error));
                // 選択画面に戻す
                EnterSelecting();
                return;
            }

            _state = res.Body ?? _state;
            if (IsPhaseComplete(_state)) { EnterReveal(); return; }

            _step = Step.Waiting;
            SetStatus("WAITING FOR OPPONENT...");
            await PollLoopAsync();
        }

        async Task PollLoopAsync()
        {
            while (_alive && _step == Step.Waiting)
            {
                await Task.Delay(1200);
                if (!_alive || _step != Step.Waiting) return;

                var s = await SafeFetchDraftAsync();
                if (s == null) continue;   // 一時的なエラーは無視して継続
                _state = s;
                if (IsPhaseComplete(s)) { EnterReveal(); return; }
            }
        }

        void EnterReveal()
        {
            _step = Step.Reveal;
            _timerActive = false;
            if (_timerText != null) _timerText.text = "";

            bool selfIsA = Pvp != null && Pvp.SelfIsA;

            if (_isBan)
            {
                string selfBan = selfIsA ? _state.banA : _state.banB;
                string oppBan  = selfIsA ? _state.banB : _state.banA;

                // 候補タイルに BAN を反映 (banned=暗転+タグ / 生存=ハイライト)
                var cand = _state.candidates ?? new List<string>();
                PopulateTiles(cand, interactable: false);
                _ = LoadVisualsAsync(cand);
                foreach (var t in ActiveTiles())
                {
                    bool bannedBySelf = t.SongId == selfBan;
                    bool bannedByOpp  = t.SongId == oppBan;
                    if (bannedBySelf || bannedByOpp)
                    {
                        t.SetDimmed(true);
                        t.SetTag(bannedBySelf ? "YOU BAN" : "OPP BAN", bannedBySelf ? Cyan : Red);
                    }
                    else
                    {
                        t.SetSelected(true);
                        t.SetTag("SURVIVES", Color.white);
                    }
                }

                SetStatus("DRAFT COMPLETE");
                SetReveal($"YOU banned  {Label(selfBan)}        OPP banned  {Label(oppBan)}");
                if (_songsText != null) _songsText.text = "FINAL LINEUP\n" + BuildLineup(_state.songs);
                SetPrimary("START MATCH", true);
            }
            else
            {
                string selfPick = selfIsA ? _state.pickA : _state.pickB;
                string oppPick  = selfIsA ? _state.pickB : _state.pickA;
                var cand = _state.candidates ?? new List<string>();

                // プールタイルに開示を反映 (自分=シアン枠 / 相手=レッド枠 / 候補=タグ / 他=暗転)
                var pool = _state.pool ?? new List<string>();
                PopulateTiles(pool, interactable: false);
                _ = LoadVisualsAsync(pool);
                foreach (var t in ActiveTiles())
                {
                    if (t.SongId == selfPick)      { t.SetSelected(true); t.SetTag("YOU", Cyan); }
                    else if (t.SongId == oppPick)  { t.SetSelected(true); t.SetTag("OPP", Red); }
                    else if (cand.Contains(t.SongId)) { t.SetTag("CANDIDATE", Color.white); }
                    else                           { t.SetDimmed(true); }
                }

                SetStatus("PICKS REVEALED");
                SetReveal($"YOU picked  {Label(selfPick)}        OPP picked  {Label(oppPick)}");
                if (_songsText != null)
                    _songsText.text = "BAN CANDIDATES:   " + string.Join("    ", LabelList(cand));
                SetPrimary("TO BAN PHASE", true);
            }
        }

        // ── タイル描画 ───────────────────────────────────────────────────────────
        void PopulateTiles(IList<string> set, bool interactable)
        {
            if (_tiles == null) return;
            for (int i = 0; i < _tiles.Length; i++)
            {
                var t = _tiles[i];
                if (t == null) continue;
                if (set != null && i < set.Count)
                {
                    t.SetSong(set[i], Label(set[i]));
                    t.SetInteractable(interactable);
                    t.SetVisible(true);
                    var sp = SpriteFor(set[i]);
                    if (sp != null) t.SetJacket(sp);
                }
                else
                {
                    t.SetVisible(false);
                }
            }
        }

        async Task LoadVisualsAsync(IList<string> set)
        {
            if (set == null) return;
            foreach (var songId in set)
            {
                if (!_alive) return;
                // タイトル
                if (!_titles.ContainsKey(songId))
                {
                    try
                    {
                        var meta = await ChartLoader.LoadMetaAsync(songId);
                        if (!_alive) return;
                        _titles[songId] = (meta != null && !string.IsNullOrEmpty(meta.Title)) ? meta.Title : songId;
                    }
                    catch { _titles[songId] = songId; }
                    ApplyLabel(songId);
                }
                // ジャケット
                if (!_sprites.ContainsKey(songId))
                {
                    try
                    {
                        var tex = await _jackets.LoadAsync(songId);
                        if (!_alive) return;
                        _sprites[songId] = tex != null
                            ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f))
                            : null;
                    }
                    catch { _sprites[songId] = null; }
                    ApplyJacket(songId);
                }
            }
        }

        // Title 解決後に該当タイルのラベルだけ更新する (SetSong は選択状態もリセットするため使わない)。
        void ApplyLabel(string songId)
        {
            string label = Label(songId);
            foreach (var t in ActiveTiles())
                if (t.SongId == songId) t.SetLabel(label);
        }

        void ApplyJacket(string songId)
        {
            var sp = SpriteFor(songId);
            if (sp == null) return;
            foreach (var t in ActiveTiles())
                if (t.SongId == songId) t.SetJacket(sp);
        }

        // ── タイマー ─────────────────────────────────────────────────────────────
        void Update()
        {
            if (!_timerActive || _step != Step.Selecting) return;
            _timeLeft -= Time.unscaledDeltaTime;
            if (_timerText != null)
            {
                int s = Mathf.Max(0, Mathf.CeilToInt(_timeLeft));
                _timerText.text = $"{s / 60}:{s % 60:00}";
                _timerText.color = _timeLeft <= 10f ? Red : Color.white;
            }
            if (_timeLeft <= 0f)
            {
                _timerActive = false;
                AutoLock();
            }
        }

        void AutoLock()
        {
            if (string.IsNullOrEmpty(_selectedSongId))
            {
                // 未選択ならランダムに選ぶ
                var actives = new List<DraftTileView>(ActiveTiles());
                if (actives.Count > 0)
                {
                    var t = actives[UnityEngine.Random.Range(0, actives.Count)];
                    _selectedSongId = t.SongId;
                    foreach (var a in actives) a.SetSelected(a.SongId == _selectedSongId);
                }
            }
            if (!string.IsNullOrEmpty(_selectedSongId)) _ = LockInAsync();
        }

        // ── キャンセル ───────────────────────────────────────────────────────────
        void OnCancel()
        {
            var pvp = Pvp;
            if (pvp != null && pvp.IsActive) pvp.CancelMatch();
            else SceneRouter.Instance?.GoTo(SceneId.Title);
        }

        // ── 状態判定ヘルパ ───────────────────────────────────────────────────────
        bool IsPhaseComplete(DraftStateDto s)
            => _isBan ? s.phase == "done" : s.phase != "pick";   // pick 完了 = phase が ban/done

        bool SelfHasChosen(DraftStateDto s)
        {
            bool selfIsA = Pvp != null && Pvp.SelfIsA;
            if (_isBan) return selfIsA ? s.aBanned : s.bBanned;
            return selfIsA ? s.aPicked : s.bPicked;
        }

        IList<string> CurrentSet(DraftStateDto s)
            => _isBan ? (s.candidates ?? new List<string>()) : (s.pool ?? new List<string>());

        // ── UI ヘルパ ────────────────────────────────────────────────────────────
        string Label(string songId)
            => !string.IsNullOrEmpty(songId) && _titles.TryGetValue(songId, out var t) ? t : songId ?? "";

        List<string> LabelList(IEnumerable<string> ids)
        {
            var list = new List<string>();
            if (ids != null) foreach (var id in ids) list.Add(Label(id));
            return list;
        }

        Sprite SpriteFor(string songId)
            => !string.IsNullOrEmpty(songId) && _sprites.TryGetValue(songId, out var s) ? s : null;

        string BuildLineup(List<SongPickDto> songs)
        {
            if (songs == null || songs.Count == 0) return "(none)";
            var sb = new StringBuilder();
            for (int i = 0; i < songs.Count; i++)
            {
                string diff = string.IsNullOrEmpty(songs[i].difficulty) ? "extra" : songs[i].difficulty;
                sb.Append($"{i + 1}.  {Label(songs[i].songId)}   [{diff}]");
                if (i < songs.Count - 1) sb.Append('\n');
            }
            return sb.ToString();
        }

        IEnumerable<DraftTileView> ActiveTiles()
        {
            if (_tiles == null) yield break;
            foreach (var t in _tiles)
                if (t != null && t.gameObject.activeSelf && !string.IsNullOrEmpty(t.SongId)) yield return t;
        }

        void SetTilesInteractable(bool on)
        {
            foreach (var t in ActiveTiles()) t.SetInteractable(on);
        }

        void SetStatus(string s) { if (_statusText != null) _statusText.text = s; }
        void SetReveal(string s) { if (_revealText != null) _revealText.text = s; }

        void SetPrimary(string label, bool interactable)
        {
            if (_primaryLabel  != null) _primaryLabel.text = label;
            if (_primaryButton != null)
            {
                _primaryButton.interactable = interactable;
                _primaryButton.gameObject.SetActive(!string.IsNullOrEmpty(label));
            }
        }

        void ShowError(string msg)
        {
            Debug.LogWarning("[PvpDraft] " + msg);
            SetStatus(msg);
        }

        static string Short(string s) => string.IsNullOrEmpty(s) ? "" : (s.Length > 80 ? s.Substring(0, 80) : s);

        static string HeaderFor(Phase p) => p switch
        {
            Phase.Prematch => "MATCH READY",
            Phase.SongPick => "SONG PICK",
            Phase.BanPhase => "BAN PHASE",
            _ => "PVP",
        };

        async Task<DraftStateDto> SafeFetchDraftAsync()
        {
            var net = NetworkClient.Instance;
            var pvp = Pvp;
            if (net == null || pvp == null || string.IsNullOrEmpty(pvp.MatchId)) return null;
            var r = await net.FetchDraftAsync(pvp.MatchId);
            return r.Ok ? r.Body : null;
        }

        // ── OnGUI フォールバック (baked UI 未結線でも操作可) ────────────────────────
        void OnGUI()
        {
            if (_headerText != null) return;   // 正規 UI があれば描かない
            var pvp = Pvp;

            const float w = 640f, h = 460f;
            var r = new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h);
            GUI.Box(r, HeaderFor(_phase));
            GUILayout.BeginArea(new Rect(r.x + 16, r.y + 32, r.width - 32, r.height - 44));

            if (pvp == null || !pvp.IsActive)
            {
                GUILayout.Label("(no active PVP match)");
                if (GUILayout.Button("BACK TO TITLE")) SceneRouter.Instance?.GoTo(SceneId.Title);
                GUILayout.EndArea();
                return;
            }

            string opp = string.IsNullOrEmpty(pvp.OpponentId) ? "???" : pvp.OpponentId;
            GUILayout.Label($"{pvp.SelfUserId}   VS   {opp}    [{_step}]");
            GUILayout.Space(6);

            switch (_phase)
            {
                case Phase.Prematch:
                    GUILayout.Label("BEST OF 3   ·   3 SONGS x 5 SECTORS");
                    if (GUILayout.Button("TO SONG PICK")) SceneRouter.Instance?.GoTo(SceneId.PVPSongPick);
                    break;

                default:
                    if (_step == Step.Selecting && _state != null)
                    {
                        GUILayout.Label(_isBan ? "BAN 1 of 3:" : "PICK 1 song:");
                        if (_timerActive) GUILayout.Label($"time: {Mathf.Max(0, Mathf.CeilToInt(_timeLeft))}s");
                        foreach (var id in CurrentSet(_state))
                            if (GUILayout.Button((id == _selectedSongId ? "▶ " : "   ") + Label(id)))
                            { _selectedSongId = id; }
                        GUI.enabled = !string.IsNullOrEmpty(_selectedSongId);
                        if (GUILayout.Button("LOCK IN")) _ = LockInAsync();
                        GUI.enabled = true;
                    }
                    else if (_step == Step.Waiting)
                    {
                        GUILayout.Label("WAITING FOR OPPONENT...");
                    }
                    else if (_step == Step.Reveal && _revealText == null)
                    {
                        GUILayout.Label(_isBan ? "DRAFT COMPLETE" : "PICKS REVEALED");
                        if (_isBan && _state != null)
                            GUILayout.Label("FINAL: " + BuildLineup(_state.songs));
                        if (GUILayout.Button(_isBan ? "START MATCH" : "TO BAN PHASE")) AdvanceFromReveal();
                    }
                    else
                    {
                        GUILayout.Label("Loading draft...");
                    }
                    break;
            }

            GUILayout.Space(10);
            if (GUILayout.Button("CANCEL")) OnCancel();
            GUILayout.EndArea();
        }
    }
}
