using System.Threading.Tasks;
using RhythmGame.Network.Api;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RhythmGame.UI.Pvp
{
    /// <summary>
    /// 曲リザルト画面 (Go サーバー移行 M4、PVPResult.unity を置換)。
    /// submit レスポンス (PvpMatchContext.LastSubmit) を表示する。
    ///   - 後攻提出: song_result あり → セクター勝敗◆/◇/—+ポイント+累計+クリンチ表示
    ///   - 先攻提出: song_result なし → 「相手の提出待ち」+ state ポーリングでフェーズ遷移を待つ
    ///     (※サーバーに曲リザルト取得 GET が無いため先攻側は詳細を表示できない — K へ要望済み)
    /// NEXT: 試合継続 → ドラフト画面 / 試合終了 → PVPMatchEnd。
    /// </summary>
    public class PvpSongResultV2Controller : MonoBehaviour
    {
        [Header("Texts")]
        [SerializeField] TextMeshProUGUI _titleText;       // SONG N RESULT
        [SerializeField] TextMeshProUGUI _selfPtsText;
        [SerializeField] TextMeshProUGUI _oppPtsText;
        [SerializeField] TextMeshProUGUI _sectorText;      // ◆ ◇ — ×5 (richtext)
        [SerializeField] TextMeshProUGUI _cumulativeText;
        [SerializeField] TextMeshProUGUI _clinchText;
        [SerializeField] TextMeshProUGUI _statusText;

        [Header("Next")]
        [SerializeField] Button          _nextButton;
        [SerializeField] TextMeshProUGUI _nextLabel;

        bool _matchOver;
        bool _canProceed;
        bool _leaving;

        void Start()
        {
            Application.runInBackground = true;
            JacketBackgroundController.Instance?.SetCanvasEnabled(true);
            JacketBackgroundController.Instance?.SetFallback();

            if (_nextButton != null) _nextButton.onClick.AddListener(Proceed);
            RhythmGame.UI.Common.ShortcutHintOverlay.Set("Space: 次へ");

            var submit = PvpMatchContext.LastSubmit;
            if (string.IsNullOrEmpty(PvpMatchContext.MatchId) || submit == null)
            {
                SetStatus("結果情報がありません — ロビーへ戻ります");
                _ = ReturnToLobbyAsync();
                return;
            }

            if (_titleText != null)
                _titleText.text = $"SONG {Mathf.Max(1, PvpMatchContext.CurrentSongOrder)}  RESULT";

            if (submit.SongResult != null)
            {
                ShowSongResult(submit);
            }
            else
            {
                // 先攻提出 — 相手待ち
                SetStatus("相手の提出待ち...");
                ShowPoints(null);
                _ = WaitOpponentAsync();
            }
        }

        void Update()
        {
            if (!_canProceed) return;
            var kb = Keyboard.current;
            if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
                Proceed();
            var pad = Gamepad.current;
            if (pad != null && RhythmGame.Input.GamepadLayout.ConfirmPressed(pad))
                Proceed();
        }

        // ── 表示 ─────────────────────────────────────────────────────────────

        void ShowSongResult(SubmitResponseDto submit)
        {
            var sr = submit.SongResult;
            bool selfIsA = PvpMatchContext.SelfIsA;

            ShowPoints(sr);

            if (_sectorText != null && sr.Sectors != null)
            {
                var sb = new System.Text.StringBuilder();
                foreach (var s in sr.Sectors)
                {
                    int mine = selfIsA ? s.PointsA : s.PointsB;
                    if      (mine >= 1000) sb.Append("<color=#2BD9E6>◆</color> ");   // 勝ち
                    else if (mine >= 500)  sb.Append("<color=#AAAAAA>—</color> ");    // 分け
                    else                   sb.Append("<color=#F24D6B>◇</color> ");   // 負け
                }
                _sectorText.text = sb.ToString().TrimEnd();
            }

            if (_cumulativeText != null)
                _cumulativeText.text = $"累計  {PvpMatchContext.CumulativeSelfMilli / 1000.0:F1} pt  —  {PvpMatchContext.CumulativeOppMilli / 1000.0:F1} pt   (8pt で決着)";

            _matchOver = submit.MatchOver;
            if (_clinchText != null)
                _clinchText.text = submit.Clinch ? "CLINCH!  (3曲目スキップ)" : "";

            EnableNext(_matchOver ? "FINAL RESULT" : "NEXT");
            SetStatus("");
        }

        void ShowPoints(SongResultDto sr)
        {
            bool selfIsA = PvpMatchContext.SelfIsA;
            if (_selfPtsText != null)
                _selfPtsText.text = sr != null ? $"{(selfIsA ? sr.PointsA : sr.PointsB) / 1000.0:F1} pt" : "-";
            if (_oppPtsText != null)
                _oppPtsText.text  = sr != null ? $"{(selfIsA ? sr.PointsB : sr.PointsA) / 1000.0:F1} pt" : "-";
            if (_sectorText != null && sr == null) _sectorText.text = "";
            if (_cumulativeText != null && sr == null) _cumulativeText.text = "";
            if (_clinchText != null && sr == null) _clinchText.text = "";
        }

        // ── 先攻提出: 相手待ちポーリング ─────────────────────────────────────

        async Task WaitOpponentAsync()
        {
            int playOrder = PvpMatchContext.CurrentSongOrder;
            string playPhase = "play_song" + playOrder;

            while (this != null && !_leaving)
            {
                await Task.Delay(2000);
                if (this == null || _leaving) return;

                var r = await PvpApi.GetStateAsync(PvpMatchContext.MatchId);
                if (this == null || _leaving) return;
                if (!r.Ok || r.Data == null)
                {
                    if (r.Status == 404) { await FetchFinalAsync(); return; }
                    continue;
                }

                string phase = r.Data.Phase;
                if (phase == playPhase) continue;   // まだ相手待ち

                if (phase == "aborted")
                {
                    SetStatus("試合が中断されました");
                    await ReturnToLobbyAsync();
                    return;
                }
                if (phase == "finished")
                {
                    await FetchFinalAsync();
                    return;
                }

                // 次のピックフェーズへ進んだ = 両者提出完了 (詳細結果は先攻側には届かない)
                SetStatus("両者提出完了 (詳細はサーバー側で確定)");
                EnableNext("NEXT");
                return;
            }
        }

        async Task FetchFinalAsync()
        {
            var r = await PvpApi.GetResultAsync(PvpMatchContext.MatchId);
            if (r.Ok && r.Data != null)
            {
                PvpMatchContext.FinalResult = r.Data;
                _matchOver = true;
                SetStatus("");
                EnableNext("FINAL RESULT");
            }
            else
            {
                SetStatus("結果の取得に失敗しました");
                await ReturnToLobbyAsync();
            }
        }

        // ── 遷移 ─────────────────────────────────────────────────────────────

        void Proceed()
        {
            if (!_canProceed || _leaving) return;
            _leaving = true;

            if (_matchOver || PvpMatchContext.FinalResult != null)
            {
                if (PvpMatchContext.FinalResult != null)
                {
                    PvpResultBridge.GoToMatchEnd();
                }
                else
                {
                    _ = FetchThenEndAsync();
                }
            }
            else
            {
                SceneRouter.Instance?.GoTo(SceneId.PVPSongPick);   // 次のドラフトフェーズへ
            }
        }

        async Task FetchThenEndAsync()
        {
            var r = await PvpApi.GetResultAsync(PvpMatchContext.MatchId);
            if (r.Ok && r.Data != null)
            {
                PvpMatchContext.FinalResult = r.Data;
                PvpResultBridge.GoToMatchEnd();
            }
            else
            {
                await ReturnToLobbyAsync();
            }
        }

        async Task ReturnToLobbyAsync()
        {
            _leaving = true;
            await Task.Delay(1500);
            await PvpMatchContext.ClearAsync();
            SceneRouter.Instance?.GoTo(SceneId.PVPLobby);
        }

        void EnableNext(string label)
        {
            _canProceed = true;
            if (_nextLabel  != null) _nextLabel.text = label + "  (Space)";
            if (_nextButton != null) _nextButton.interactable = true;
        }

        void SetStatus(string msg)
        {
            if (_statusText != null) _statusText.text = msg ?? "";
        }
    }
}
