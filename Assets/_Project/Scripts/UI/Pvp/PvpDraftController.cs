using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RhythmGame.Network.Api;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmGame.UI.Pvp
{
    /// <summary>
    /// 統合ドラフト画面 (Go サーバー移行 M4、PVPSongPick.unity を置換)。
    /// 交互ターン制の全ドラフトフェーズを 1 画面で処理する:
    ///   pick_song1 (下位: 曲+難易度) → pick_song1_h (上位: 難易度後出し)
    ///   pick_song2 (上位: 曲+難易度) → pick_song2_l (下位: 難易度後出し)
    ///   ban_song3 (下位→上位の順 BAN) → pick_song3_diff (両者ブラインド難易度)
    /// GET /state 1.5秒ポーリングが正 (タイムアウト自動処理の駆動も兼ねる)。
    /// play_songN を検知すると GamePlay (IsPvp) を起動する。
    /// </summary>
    public class PvpDraftController : MonoBehaviour
    {
        [Header("Header")]
        [SerializeField] TextMeshProUGUI _phaseTitle;
        [SerializeField] TextMeshProUGUI _timerText;
        [SerializeField] TextMeshProUGUI _infoText;     // 相手のピック表示等
        [SerializeField] TextMeshProUGUI _statusText;

        [Header("Song tiles (baked 6)")]
        [SerializeField] Button[]          _songTiles;
        [SerializeField] TextMeshProUGUI[] _songTileLabels;
        [SerializeField] Image[]           _songTileBgs;

        [Header("Difficulty buttons (EZ/NM/HD/EX)")]
        [SerializeField] Button[]          _diffButtons;
        [SerializeField] TextMeshProUGUI[] _diffLabels;
        [SerializeField] Image[]           _diffBgs;

        [Header("Confirm")]
        [SerializeField] Button          _confirmButton;
        [SerializeField] TextMeshProUGUI _confirmLabel;

        static readonly string[] DiffNames = { "easy", "normal", "hard", "extra" };
        static readonly string[] DiffShort = { "EASY", "NORMAL", "HARD", "EXTRA" };
        static readonly Color TileIdle     = new Color(1f, 1f, 1f, 0.08f);
        static readonly Color TileSelected = new Color(0.17f, 0.85f, 0.90f, 0.45f);
        static readonly Color TileDisabled = new Color(1f, 1f, 1f, 0.03f);

        readonly List<string> _tileSongIds = new List<string>();

        bool   _busy;            // pick/ban 送信中
        bool   _myTurn;          // 直近の state で自分が acting_player か
        bool   _leaving;
        string _selectedSongId;
        int    _selectedDiff = -1;
        string _lastPhase = "";
        DateTimeOffset? _deadline;

        void Start()
        {
            Application.runInBackground = true;
            JacketBackgroundController.Instance?.SetCanvasEnabled(true);
            JacketBackgroundController.Instance?.SetFallback();

            for (int i = 0; i < (_songTiles?.Length ?? 0); i++)
            {
                int idx = i;
                if (_songTiles[i] != null) _songTiles[i].onClick.AddListener(() => SelectSong(idx));
            }
            for (int i = 0; i < (_diffButtons?.Length ?? 0); i++)
            {
                int idx = i;
                if (_diffButtons[i] != null) _diffButtons[i].onClick.AddListener(() => SelectDiff(idx));
            }
            if (_confirmButton != null) _confirmButton.onClick.AddListener(() => _ = ConfirmAsync());

            RhythmGame.UI.Common.ShortcutHintOverlay.Set("クリック: 選択   決定ボタン: 確定");

            if (string.IsNullOrEmpty(PvpMatchContext.MatchId))
            {
                SetStatus("マッチ情報がありません — ロビーへ戻ります");
                SceneRouter.Instance?.GoTo(SceneId.PVPLobby);
                return;
            }

            _ = PollLoopAsync();
        }

        void Update()
        {
            if (_deadline.HasValue && _timerText != null)
            {
                double remain = (_deadline.Value - DateTimeOffset.UtcNow).TotalSeconds;
                _timerText.text = remain > 0 ? $"{remain:F0}" : "0";
            }

            // ESC: ドラフト離脱 = 不戦敗の確認 (離脱で WS が切れ、サーバーの 30s グレース後 forfeit)
            if (!RhythmGame.UI.Common.ConfirmDialog.IsOpen)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null && kb.escapeKey.wasPressedThisFrame) ShowLeaveConfirm();
                var pad = UnityEngine.InputSystem.Gamepad.current;
                if (pad != null && RhythmGame.Input.GamepadLayout.BackPressed(pad)) ShowLeaveConfirm();
            }
        }

        void ShowLeaveConfirm()
        {
            RhythmGame.UI.Common.ConfirmDialog.Show(
                "対戦から離脱しますか？(不戦敗扱い)", "離脱する", "もどる",
                onConfirm: () => _ = LeaveAsync());
        }

        async Task LeaveAsync()
        {
            _leaving = true;
            await PvpMatchContext.ClearAsync();   // WS クローズ → サーバーのグレース → forfeit
            SceneRouter.Instance?.GoTo(SceneId.PVPLobby);
        }

        // ── State polling (正) ───────────────────────────────────────────────

        async Task PollLoopAsync()
        {
            while (this != null && !_leaving)
            {
                var r = await PvpApi.GetStateAsync(PvpMatchContext.MatchId);
                if (this == null || _leaving) return;

                if (r.Ok && r.Data != null)
                    await ApplyStateAsync(r.Data);
                else if (r.Status == 404)
                {
                    // 既に終了して active store から消えた等 → 結果取得を試みる
                    await TryFinishAsync();
                    return;
                }

                await EnsureSocketAsync();   // WS 切断時の自動再接続 (30s グレース内なら復帰可)
                await Task.Delay(1500);
            }
        }

        bool _reconnecting;

        // WS が落ちていたら再接続を試みる (相手進捗中継・start通知の復旧。draft はグレース 30 秒)
        async Task EnsureSocketAsync()
        {
            if (_leaving || _reconnecting) return;
            var sock = PvpMatchContext.Socket;
            if (sock != null && sock.IsConnected) return;

            _reconnecting = true;
            try
            {
                sock?.Dispose();
                var newSock = new MatchSocketClient();
                bool ok = await newSock.ConnectAsync(PvpMatchContext.MatchId);
                if (ok)
                {
                    PvpMatchContext.Socket = newSock;
                    Debug.Log("[Draft] WS 再接続成功");
                }
                else
                {
                    newSock.Dispose();
                    PvpMatchContext.Socket = null;
                }
            }
            finally
            {
                _reconnecting = false;
            }
        }

        async Task ApplyStateAsync(MatchStateDto st)
        {
            string phase = st.Phase ?? "";
            string me    = AuthManager.UserId;
            bool   myTurn = st.ActingPlayer == me;
            _myTurn = myTurn;   // 決定ボタンの手番ガードに使う

            if (!string.IsNullOrEmpty(st.PhaseDeadline) &&
                DateTimeOffset.TryParse(st.PhaseDeadline, null,
                    System.Globalization.DateTimeStyles.AdjustToUniversal, out var dl))
                _deadline = dl;

            // 下位/上位の確定 (単独アクターのフェーズで判定できる)
            switch (phase)
            {
                case "pick_song1":   PvpMatchContext.SelfIsLower = myTurn;  break;
                case "pick_song1_h": PvpMatchContext.SelfIsLower = !myTurn; break;
                case "pick_song2":   PvpMatchContext.SelfIsLower = !myTurn; break;
                case "pick_song2_l": PvpMatchContext.SelfIsLower = myTurn;  break;
            }

            bool phaseChanged = phase != _lastPhase;
            if (phaseChanged)
            {
                _lastPhase = phase;
                _selectedSongId = null;
                _selectedDiff   = -1;
                _busy = false;
                Debug.Log($"[Draft] phase={phase} acting={st.ActingPlayer} myTurn={myTurn}");
            }

            switch (phase)
            {
                case "pick_song1":
                case "pick_song2":
                {
                    int order = phase == "pick_song1" ? 1 : 2;
                    if (myTurn)
                    {
                        SetTitle($"SONG {order} — あなたが選曲");
                        SetInfo(order == 2 && st.Picks?.Song1 != null
                            ? $"SONG 1 は {SongTitle(st.Picks.Song1.SongId)} でした (同じ曲は選べません)"
                            : "曲と自分の難易度を選んで決定");
                        // SONG1/2 も試合プール内からしか選べない (サーバーは全ピックを
                        // Song3Pool = 試合プールで検証し、外れると SONG_NOT_IN_POOL 400)。
                        // 全曲を出していたため、プール外を選ぶとエラーでドラフトが止まり
                        // フェーズタイムアウトで不戦敗になっていた (ソークで再現・2026-07-31)。
                        ShowSongGrid(excludeSongId: order == 2 ? st.Picks?.Song1?.SongId : null,
                                     poolOnly: st.Song3Pool);
                        ShowDiffButtons(true);
                        UpdateConfirm("この曲・難易度で決定",
                            interactable: !_busy && _selectedSongId != null && _selectedDiff >= 0);
                    }
                    else
                    {
                        SetTitle($"SONG {order} — 相手が選曲中...");
                        SetInfo("相手が曲と難易度を選んでいます");
                        HideInteractives();
                    }
                    break;
                }

                case "pick_song1_h":
                case "pick_song2_l":
                {
                    int order = phase == "pick_song1_h" ? 1 : 2;
                    var pick  = order == 1 ? st.Picks?.Song1 : st.Picks?.Song2;
                    string oppDiff = order == 1 ? pick?.DiffL : pick?.DiffH;   // 先出し側の難易度
                    if (myTurn)
                    {
                        SetTitle($"SONG {order} — 難易度を後出し");
                        SetInfo($"曲: {SongTitle(pick?.SongId)}   相手の難易度: {(oppDiff ?? "?").ToUpperInvariant()}");
                        ShowSongGrid(onlySongId: pick?.SongId);
                        ShowDiffButtons(true, fixedSongId: pick?.SongId);
                        UpdateConfirm("この難易度で決定", interactable: !_busy && _selectedDiff >= 0);
                    }
                    else
                    {
                        SetTitle($"SONG {order} — 相手が難易度を選択中...");
                        SetInfo($"曲: {SongTitle(pick?.SongId)}   あなたの難易度: {(oppDiff ?? "?").ToUpperInvariant()}");
                        HideInteractives();
                    }
                    break;
                }

                case "ban_song3":
                {
                    if (myTurn)
                    {
                        SetTitle("SONG 3 — BAN する曲を選択");
                        SetInfo("候補プールから 1 曲 BAN (残りからランダムで SONG 3 が決まる)");
                        ShowSongGrid(poolOnly: st.Song3Pool);
                        ShowDiffButtons(false);
                        UpdateConfirm("この曲を BAN", interactable: !_busy && _selectedSongId != null);
                    }
                    else
                    {
                        SetTitle("SONG 3 — 相手が BAN 中...");
                        SetInfo("BAN は 下位レート → 上位レート の順");
                        ShowSongGrid(poolOnly: st.Song3Pool);
                        ShowDiffButtons(false);
                        UpdateConfirm("待機中...", interactable: false);
                    }
                    break;
                }

                case "pick_song3_diff":
                {
                    SetTitle("SONG 3 — 難易度をブラインド選択");
                    SetInfo($"SONG 3: {SongTitle(st.Picks?.Song3?.SongId)}   (相手には見えません)");
                    ShowSongGrid(onlySongId: st.Picks?.Song3?.SongId);
                    ShowDiffButtons(true, fixedSongId: st.Picks?.Song3?.SongId);
                    UpdateConfirm("この難易度で決定", interactable: !_busy && _selectedDiff >= 0);
                    break;
                }

                case "play_song1":
                case "play_song2":
                case "play_song3":
                {
                    int order = phase == "play_song1" ? 1 : phase == "play_song2" ? 2 : 3;
                    await HandlePlayPhaseAsync(order, st);
                    break;
                }

                case "finished":
                    await TryFinishAsync();
                    break;

                case "aborted":
                    SetTitle("MATCH ABORTED");
                    SetInfo("試合が中断されました");
                    _leaving = true;
                    await Task.Delay(1500);
                    await PvpMatchContext.ClearAsync();
                    SceneRouter.Instance?.GoToWhenIdle(SceneId.PVPLobby);
                    break;

                default:
                    SetTitle(phase);
                    break;
            }
        }

        // ── play フェーズ → GamePlay 起動 / 提出済みなら待機 ──────────────────

        async Task HandlePlayPhaseAsync(int order, MatchStateDto st)
        {
            // 既にこの曲を提出済み (相手待ち) → 待機表示
            if (PvpMatchContext.CurrentSongOrder >= order &&
                PvpMatchContext.LastSubmit != null)
            {
                SetTitle($"SONG {order} — 相手の提出待ち...");
                SetInfo("");
                HideInteractives();
                return;
            }
            if (_leaving) return;

            var pick = order == 1 ? st.Picks?.Song1 : order == 2 ? st.Picks?.Song2 : st.Picks?.Song3;
            string songId = pick?.SongId;
            string myDiff = order == 3
                ? (PvpMatchContext.SelfIsA ? pick?.DiffA : pick?.DiffB)
                : (PvpMatchContext.SelfIsLower ? pick?.DiffL : pick?.DiffH);

            if (string.IsNullOrEmpty(songId) || string.IsNullOrEmpty(myDiff))
            {
                // ここで返すとドラフト画面に留まり続け、サーバーのフェーズタイムアウトで
                // 不戦敗になる。無言で試合が死ぬので必ずログに残すこと。
                Debug.LogWarning($"[Draft] play_song{order} を開始できない: song={songId} diff={myDiff}");
                SetStatus($"曲情報の解決に失敗 (song={songId} diff={myDiff}) — ポーリング継続");
                return;
            }

            if (!ServerSongLibrary.TryGetChart(songId, myDiff, out _))
            {
                SetStatus("譜面を同期中...");
                await ServerSongLibrary.EnsureSyncedAsync();
                if (!ServerSongLibrary.TryGetChart(songId, myDiff, out _))
                {
                    Debug.LogWarning($"[Draft] play_song{order} を開始できない: 譜面が無い {songId}/{myDiff} (同期後も未取得)");
                    SetStatus($"譜面の取得に失敗: {songId}/{myDiff}");
                    return;
                }
            }

            _leaving = true;   // ポーリング停止して GamePlay へ
            PvpMatchContext.CurrentSongOrder  = order;
            PvpMatchContext.CurrentSongId     = songId;
            PvpMatchContext.CurrentDifficulty = myDiff;
            PvpMatchContext.LastSubmit        = null;

            Debug.Log($"[Draft] play_song{order}: {songId}/{myDiff} → GamePlay");
            // GoToWhenIdle: 遷移中ガードによる取りこぼしで _leaving=true のまま固まるのを防ぐ
            SceneRouter.Instance?.GoToWhenIdle(SceneId.GamePlay, new GamePlayParameters
            {
                SongId        = songId,
                Difficulty    = myDiff,
                HiSpeed       = PlayOptionsController.HiSpeed,
                Modifier      = "None",   // PVP はモディファイア無効
                // PVP のオートプレイはソークテスト専用 (PvpSoakDriver)。エディタ+SoakTest=1 の
                // 二重ガード — ビルドでは常に false (チート防止)。
                IsAutoPlay    = Application.isEditor && PlayerPrefs.GetInt("SoakTest", 0) == 1,
                IsPvp         = true,
                PvpMatchId    = PvpMatchContext.MatchId,
                PvpSongIndex  = order - 1,
                PvpOpponentId = PvpMatchContext.OpponentId,
            });
        }

        async Task TryFinishAsync()
        {
            if (_leaving) return;
            _leaving = true;
            var r = await PvpApi.GetResultAsync(PvpMatchContext.MatchId);
            if (r.Ok && r.Data != null)
            {
                PvpMatchContext.FinalResult = r.Data;
                PvpResultBridge.GoToMatchEnd();
            }
            else
            {
                await PvpMatchContext.ClearAsync();
                SceneRouter.Instance?.GoTo(SceneId.PVPLobby);
            }
        }

        // ── 選択+確定 ────────────────────────────────────────────────────────

        void SelectSong(int tileIdx)
        {
            if (tileIdx >= _tileSongIds.Count) return;
            string id = _tileSongIds[tileIdx];
            if (id == null) return;
            _selectedSongId = id;
            RefreshTileVisuals();
            // 難易度ボタンの有効・無効は選択曲に依存する。ここで組み直さないと
            // 次のポーリング (最大 1.5 秒後) まで「前の曲」基準のままで、
            // その曲に無い難易度を選べてしまう → 演奏開始時に譜面が取れず不戦敗。
            // (ソークで song_005/hard, song_004/easy を選んで再現・2026-07-31)
            ShowDiffButtons(true);
            RefreshConfirmInteractable();
        }

        void SelectDiff(int idx)
        {
            _selectedDiff = idx;
            for (int i = 0; i < (_diffBgs?.Length ?? 0); i++)
                if (_diffBgs[i] != null) _diffBgs[i].color = i == idx ? TileSelected : TileIdle;
            RefreshConfirmInteractable();
        }

        void RefreshConfirmInteractable()
        {
            if (_confirmButton == null) return;

            // 自分の手番でなければ決定させない。
            // 相手の BAN 待ち中もタイルは押せる (プールを見せるため) ので、曲を選ぶと
            // ここが呼ばれて決定ボタンが点灯し、順番前に送信できてしまっていた
            // → サーバーが 409 BAN_ORDER_VIOLATION を返し、ドラフトが止まって不戦敗。
            // (ソークで再現・2026-07-31)
            // pick_song3_diff は両者同時のブラインド選択で acting_player が null なので除外する。
            if (!_myTurn && _lastPhase != "pick_song3_diff")
            {
                _confirmButton.interactable = false;
                return;
            }

            switch (_lastPhase)
            {
                case "pick_song1":
                case "pick_song2":
                    _confirmButton.interactable = !_busy && _selectedSongId != null && _selectedDiff >= 0;
                    break;
                case "pick_song1_h":
                case "pick_song2_l":
                case "pick_song3_diff":
                    _confirmButton.interactable = !_busy && _selectedDiff >= 0;
                    break;
                case "ban_song3":
                    _confirmButton.interactable = !_busy && _selectedSongId != null;
                    break;
            }
        }

        async Task ConfirmAsync()
        {
            if (_busy) return;
            _busy = true;
            RefreshConfirmInteractable();
            SetStatus("送信中...");

            ApiResult<PhaseDto> r;
            switch (_lastPhase)
            {
                case "pick_song1":
                case "pick_song2":
                    r = await PvpApi.PickAsync(PvpMatchContext.MatchId, _selectedSongId, DiffNames[_selectedDiff]);
                    break;
                case "pick_song1_h":
                case "pick_song2_l":
                case "pick_song3_diff":
                    r = await PvpApi.PickAsync(PvpMatchContext.MatchId, null, DiffNames[_selectedDiff]);
                    break;
                case "ban_song3":
                    r = await PvpApi.BanAsync(PvpMatchContext.MatchId, _selectedSongId);
                    break;
                default:
                    _busy = false;
                    return;
            }

            if (r.Ok)
            {
                SetStatus("");
                // 次フェーズはポーリングが拾う (ALREADY_ACTED 防止に busy は phase 変化まで維持)
            }
            else if (r.ErrorCode == "ALREADY_ACTED")
            {
                SetStatus("送信済み — 相手を待っています");
            }
            else
            {
                SetStatus("送信失敗: " + r.ErrorMessage);
                _busy = false;
                RefreshConfirmInteractable();
            }
        }

        // ── 表示ヘルパー ─────────────────────────────────────────────────────

        void ShowSongGrid(string excludeSongId = null, string onlySongId = null, string[] poolOnly = null)
        {
            _tileSongIds.Clear();
            var ids = new List<string>();
            if (onlySongId != null) ids.Add(onlySongId);
            else if (poolOnly != null) ids.AddRange(poolOnly);
            else foreach (var id in ServerSongLibrary.PvpSongIds) ids.Add(id);   // テストソング除外 (K 指示 2026-07-30)

            for (int i = 0; i < (_songTiles?.Length ?? 0); i++)
            {
                bool used = i < ids.Count;
                if (_songTiles[i] != null) _songTiles[i].gameObject.SetActive(used);
                if (!used) { _tileSongIds.Add(null); continue; }

                string id = ids[i];
                bool excluded = id == excludeSongId;
                _tileSongIds.Add(excluded ? null : id);

                if (_songTileLabels != null && _songTileLabels[i] != null)
                {
                    var meta = ServerSongLibrary.GetMetaOrNull(id);
                    _songTileLabels[i].text = (meta?.Title ?? id) + (excluded ? "\n<size=60%>(SONG1で使用済)</size>" : "");
                }
                if (_songTiles[i] != null) _songTiles[i].interactable = !excluded && onlySongId == null;
            }
            RefreshTileVisuals();
        }

        void RefreshTileVisuals()
        {
            for (int i = 0; i < (_songTileBgs?.Length ?? 0); i++)
            {
                if (_songTileBgs[i] == null) continue;
                string id = i < _tileSongIds.Count ? _tileSongIds[i] : null;
                _songTileBgs[i].color = id == null ? TileDisabled
                                      : id == _selectedSongId ? TileSelected : TileIdle;
            }
        }

        void ShowDiffButtons(bool show, string fixedSongId = null)
        {
            for (int i = 0; i < (_diffButtons?.Length ?? 0); i++)
            {
                if (_diffButtons[i] == null) continue;
                _diffButtons[i].gameObject.SetActive(show);
                if (show && _diffLabels != null && _diffLabels[i] != null)
                {
                    // 対象曲: 後出し/ブラインドは曲が確定済み (fixedSongId)。それ以外は選択中の曲。
                    string songForLevel = fixedSongId ?? _selectedSongId ?? (_tileSongIds.Count > 0 ? _tileSongIds[0] : null);
                    int level = -1;
                    bool exists = songForLevel != null &&
                                  ServerSongLibrary.TryGetChart(songForLevel, DiffNames[i], out var c);
                    if (exists && ServerSongLibrary.TryGetChart(songForLevel, DiffNames[i], out var c2))
                        level = c2.Level;
                    // 存在しない難易度は選択不可 (SOAK 検出 2026-07-30: 譜面のない難易度を
                    // 選べてしまい、play フェーズで譜面が取れず永久スタック→不戦敗になっていた)
                    _diffButtons[i].interactable = exists;
                    _diffLabels[i].text = DiffShort[i] + (level >= 0 ? $" {level}" : "");
                    if (!exists && _selectedDiff == i) _selectedDiff = -1;
                }
            }
        }

        void HideInteractives()
        {
            ShowSongGrid(onlySongId: "__none__");   // 全タイル非表示相当
            for (int i = 0; i < (_songTiles?.Length ?? 0); i++)
                if (_songTiles[i] != null) _songTiles[i].gameObject.SetActive(false);
            ShowDiffButtons(false);
            UpdateConfirm("待機中...", false);
        }

        void UpdateConfirm(string label, bool interactable)
        {
            if (_confirmLabel  != null) _confirmLabel.text = label;
            if (_confirmButton != null) _confirmButton.interactable = interactable;
        }

        string SongTitle(string songId)
        {
            if (string.IsNullOrEmpty(songId)) return "???";
            var meta = ServerSongLibrary.GetMetaOrNull(songId);
            return meta?.Title ?? songId;
        }

        void SetTitle(string t)  { if (_phaseTitle != null) _phaseTitle.text = t; }
        void SetInfo(string t)   { if (_infoText   != null) _infoText.text   = t; }
        void SetStatus(string t) { if (_statusText != null) _statusText.text = t; }
    }
}
