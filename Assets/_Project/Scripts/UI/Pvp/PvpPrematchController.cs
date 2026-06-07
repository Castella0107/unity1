using System;
using System.Threading.Tasks;
using RhythmGame.Network.Api;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RhythmGame.UI.Pvp
{
    /// <summary>
    /// PVPPrematch (READY 画面、Go サーバー移行 M3 で全面再実装)。
    ///   - GET /matches/{id}/prematch: 両者プロフィール+レーティング変動予測を表示
    ///   - WebSocket 接続 → ready_check(deadline)/ready_ack/start の READY 同期
    ///   - Space/ボタン=READY (WS、未接続時は REST fallback) / ESC=辞退確認 (REST decline)
    /// start 受信でドラフト (pick_song1) へ進む — ドラフト画面は M4 実装のため、現状はその旨を表示。
    /// </summary>
    public class PvpPrematchController : MonoBehaviour
    {
        [Header("Self (シアン)")]
        [SerializeField] TextMeshProUGUI _selfNameText;
        [SerializeField] TextMeshProUGUI _selfRatingText;
        [SerializeField] TextMeshProUGUI _selfRecordText;

        [Header("Opponent (レッド)")]
        [SerializeField] TextMeshProUGUI _oppNameText;
        [SerializeField] TextMeshProUGUI _oppRatingText;
        [SerializeField] TextMeshProUGUI _oppRecordText;

        [Header("Info")]
        [SerializeField] TextMeshProUGUI _predictionText;   // レート変動予測 (自分視点)
        [SerializeField] TextMeshProUGUI _readyStateText;   // YOU ● READY / OPP ○ ...
        [SerializeField] TextMeshProUGUI _timerText;        // ready deadline カウントダウン
        [SerializeField] TextMeshProUGUI _statusText;       // 接続状態・start 通知
        [SerializeField] Button          _readyButton;

        bool _selfIsA;
        bool _selfReady;
        bool _oppReady;
        bool _started;
        bool _leaving;
        DateTimeOffset? _deadline;

        string _fallbackLine = "loading...";

        async void Start()
        {
            Application.runInBackground = true;
            JacketBackgroundController.Instance?.SetCanvasEnabled(true);
            JacketBackgroundController.Instance?.SetFallback();

            if (_readyButton != null) _readyButton.onClick.AddListener(() => _ = SendReadyAsync());
            RhythmGame.UI.Common.ShortcutHintOverlay.Set("Space: READY   ESC: 辞退(不戦敗)");

            if (string.IsNullOrEmpty(PvpMatchContext.MatchId))
            {
                SetStatus("マッチ情報がありません — ロビーへ戻ります");
                await Task.Delay(1200);
                SceneRouter.Instance?.GoTo(SceneId.PVPLobby);
                return;
            }

            await LoadPrematchAsync();
            await ConnectSocketAsync();
            _ = PollStateAsync();   // WS start のフォールバック+サーバーのタイムアウト処理駆動
        }

        /// <summary>
        /// GET /state を 2 秒間隔でポーリングする。
        /// ①WS start の取りこぼし対策 (相手が REST ready の場合、サーバーの WS Hub は start を
        ///   ブロードキャストしない既知のギャップがある — K 報告済み)
        /// ②サーバーのフェーズタイムアウト自動処理は state 呼び出し時に駆動される設計のため、
        ///   ポーリング自体が必須。
        /// </summary>
        async Task PollStateAsync()
        {
            while (this != null && !_started && !_leaving)
            {
                await Task.Delay(2000);
                if (this == null || _started || _leaving) return;

                var r = await PvpApi.GetStateAsync(PvpMatchContext.MatchId);
                if (this == null || _started || _leaving) return;
                if (!r.Ok || r.Data == null) continue;

                string phase = r.Data.Phase;
                if (phase == "aborted")
                {
                    SetStatus("試合が中断されました (ready タイムアウト等)");
                    await Task.Delay(1500);
                    await PvpMatchContext.ClearAsync();
                    SceneRouter.Instance?.GoTo(SceneId.PVPLobby);
                    return;
                }
                if (phase != "pre_match")
                {
                    OnMatchStart();   // WS start を取りこぼしてもフェーズ遷移で開始を検知
                    return;
                }
            }
        }

        // ── データ取得 ──────────────────────────────────────────────────────

        async Task LoadPrematchAsync()
        {
            var r = await PvpApi.GetPrematchAsync(PvpMatchContext.MatchId);
            if (!r.Ok || r.Data == null)
            {
                SetStatus("試合情報の取得に失敗: " + r.ErrorMessage);
                return;
            }

            var d = r.Data;
            _selfIsA = d.PlayerA != null && d.PlayerA.UserId == AuthManager.UserId;
            PvpMatchContext.SelfIsA = _selfIsA;   // song3 の diff_a/diff_b 解決にドラフト以降で使う

            var oppDto = _selfIsA ? d.PlayerB : d.PlayerA;
            if (!string.IsNullOrEmpty(oppDto?.DisplayName))
                PvpMatchContext.OpponentDisplayName = oppDto.DisplayName;   // 以降の画面の表示名ソース
            var self = _selfIsA ? d.PlayerA : d.PlayerB;
            var opp  = _selfIsA ? d.PlayerB : d.PlayerA;

            SetPlayer(_selfNameText, _selfRatingText, _selfRecordText, self);
            SetPlayer(_oppNameText,  _oppRatingText,  _oppRecordText,  opp);

            if (_predictionText != null && d.PredictedRatingChange != null)
            {
                var p = d.PredictedRatingChange;
                double win  = _selfIsA ? p.PlayerAWin.PlayerADelta : p.PlayerBWin.PlayerBDelta;
                double lose = _selfIsA ? p.PlayerBWin.PlayerADelta : p.PlayerAWin.PlayerBDelta;
                double draw = _selfIsA ? p.Draw.PlayerADelta       : p.Draw.PlayerBDelta;
                _predictionText.text =
                    $"WIN {Signed(win)}    DRAW {Signed(draw)}    LOSE {Signed(lose)}";
            }

            if (!string.IsNullOrEmpty(d.PhaseDeadline)) TrySetDeadline(d.PhaseDeadline);
            UpdateReadyState();
        }

        static void SetPlayer(TextMeshProUGUI name, TextMeshProUGUI rating, TextMeshProUGUI record, PrematchPlayerDto p)
        {
            if (p == null) return;
            if (name   != null) name.text   = string.IsNullOrEmpty(p.DisplayName) ? p.UserId : p.DisplayName;
            if (rating != null) rating.text = $"RATING {p.Rating:F0}";
            if (record != null) record.text = $"{p.WinCount}W - {p.LossCount}L - {p.DrawCount}D";
        }

        static string Signed(double v) => (v >= 0 ? "+" : "") + v.ToString("F1");

        // ── WebSocket ───────────────────────────────────────────────────────

        async Task ConnectSocketAsync()
        {
            SetStatus("サーバーに接続中...");
            var socket = new MatchSocketClient();
            socket.OnReadyCheck += deadline => { TrySetDeadline(deadline); SetStatus("READY を押してください"); };
            socket.OnReadyAck   += OnReadyAck;
            socket.OnStart      += OnMatchStart;
            socket.OnSocketError += (code, msg) => SetStatus($"接続エラー: {code} {msg}");
            socket.OnClosed     += () => { if (!_started && !_leaving) SetStatus("接続が切断されました (30秒以内の再入室で復帰可)"); };

            bool ok = await socket.ConnectAsync(PvpMatchContext.MatchId);
            if (!ok)
            {
                socket.Dispose();
                SetStatus("WebSocket 接続失敗 — READY は REST で送信します");
                return;
            }
            PvpMatchContext.Socket = socket;
            SetStatus("READY を押してください");
        }

        void OnReadyAck(string userId)
        {
            if (userId == AuthManager.UserId) _selfReady = true;
            else                              _oppReady  = true;
            UpdateReadyState();
        }

        void OnMatchStart()
        {
            if (_started) return;
            _started = true;
            SetStatus("MATCH START!");
            if (_timerText != null) _timerText.text = "";
            Debug.Log("[Prematch] start — ドラフト (PVPSongPick) へ遷移");
            SceneRouter.Instance?.GoTo(SceneId.PVPSongPick);
        }

        // ── READY / 辞退 ────────────────────────────────────────────────────

        async Task SendReadyAsync()
        {
            if (_selfReady || _started) return;

            if (PvpMatchContext.Socket != null && PvpMatchContext.Socket.IsConnected)
            {
                await PvpMatchContext.Socket.SendReadyAsync();
                // ready_ack のブロードキャストで _selfReady が立つ
            }
            else
            {
                var r = await PvpApi.PostReadyAsync(PvpMatchContext.MatchId, ready: true);
                if (r.Ok)
                {
                    _selfReady = true;
                    UpdateReadyState();
                    if (r.Data?.Phase == "pick_song1") OnMatchStart();
                }
                else SetStatus("READY 送信失敗: " + r.ErrorMessage);
            }
        }

        void ShowDeclineConfirm()
        {
            RhythmGame.UI.Common.ConfirmDialog.Show(
                "対戦を辞退しますか？(不戦敗扱い)", "辞退する", "もどる",
                onConfirm: () => _ = DeclineAsync());
        }

        async Task DeclineAsync()
        {
            _leaving = true;
            try { await PvpApi.PostReadyAsync(PvpMatchContext.MatchId, ready: false); }
            catch { }
            await PvpMatchContext.ClearAsync();
            SceneRouter.Instance?.GoTo(SceneId.PVPLobby);
        }

        // ── Update (入力+タイマー) ──────────────────────────────────────────

        void Update()
        {
            if (!RhythmGame.UI.Common.ConfirmDialog.IsOpen)
            {
                var kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)
                        _ = SendReadyAsync();
                    if (kb.escapeKey.wasPressedThisFrame && !_started)
                        ShowDeclineConfirm();
                }
                var pad = Gamepad.current;
                if (pad != null)
                {
                    if (RhythmGame.Input.GamepadLayout.ConfirmPressed(pad)) _ = SendReadyAsync();
                    if (RhythmGame.Input.GamepadLayout.BackPressed(pad) && !_started) ShowDeclineConfirm();
                }
            }

            if (_deadline.HasValue && _timerText != null && !_started)
            {
                double remain = (_deadline.Value - DateTimeOffset.UtcNow).TotalSeconds;
                _timerText.text = remain > 0 ? $"{remain:F0}" : "0";
            }
        }

        // ── UI helpers ──────────────────────────────────────────────────────

        void TrySetDeadline(string rfc3339)
        {
            if (DateTimeOffset.TryParse(rfc3339, null,
                    System.Globalization.DateTimeStyles.AdjustToUniversal, out var dt))
                _deadline = dt;
        }

        void UpdateReadyState()
        {
            if (_readyStateText == null) return;
            string self = _selfReady ? "<color=#2BD9E6>YOU ● READY</color>" : "<color=#2BD9E6>YOU ○ ...</color>";
            string opp  = _oppReady  ? "<color=#F24D6B>OPP ● READY</color>" : "<color=#F24D6B>OPP ○ ...</color>";
            _readyStateText.text = self + "      " + opp;
            if (_readyButton != null) _readyButton.interactable = !_selfReady && !_started;
        }

        void SetStatus(string msg)
        {
            _fallbackLine = msg;
            if (_statusText != null) _statusText.text = msg;
        }

        // ── OnGUI フォールバック ──────────────────────────────────────────────
        void OnGUI()
        {
            if (_statusText != null) return;
            const float w = 520f, h = 220f;
            var r = new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h);
            GUI.Box(r, "MATCH READY");
            GUILayout.BeginArea(new Rect(r.x + 16, r.y + 30, r.width - 32, r.height - 44));
            GUILayout.Label(_fallbackLine);
            GUILayout.Label($"self={_selfReady} opp={_oppReady} started={_started}");
            if (GUILayout.Button("READY")) _ = SendReadyAsync();
            if (GUILayout.Button("辞退"))   ShowDeclineConfirm();
            GUILayout.EndArea();
        }
    }
}
