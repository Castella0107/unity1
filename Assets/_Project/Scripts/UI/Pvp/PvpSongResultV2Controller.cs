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
            RhythmGame.UI.Common.ShortcutHintOverlay.Set("Space: 次へ   ESC: ロビーへ戻る");

            // 提出自体が失敗していたら待たずに戻す (待つと永久に進まない)
            if (!string.IsNullOrEmpty(PvpMatchContext.SubmitError))
            {
                string reason = PvpMatchContext.SubmitError;
                PvpMatchContext.SubmitError = null;
                SetStatus($"スコアの提出に失敗しました — ロビーへ戻ります\n({reason})");
                _ = ReturnToLobbyAsync();
                return;
            }

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
            // 待機中でも ESC で抜けられるようにする (進まないまま操作不能を避ける)
            var esc = Keyboard.current;
            if (!_leaving && esc != null && esc.escapeKey.wasPressedThisFrame)
            {
                SetStatus("ロビーへ戻ります");
                _ = ReturnToLobbyAsync();
                return;
            }

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

            ShowPoints(sr);
            ShowSectorDiamonds(sr);

            if (_cumulativeText != null)
                _cumulativeText.text = $"累計  {PvpMatchContext.CumulativeSelfMilli / 1000.0:F1} pt  —  {PvpMatchContext.CumulativeOppMilli / 1000.0:F1} pt   (8pt で決着)";

            _matchOver = submit.MatchOver;
            if (_clinchText != null)
                _clinchText.text = submit.Clinch ? "CLINCH!  (3曲目スキップ)" : "";

            EnableNext(_matchOver ? "FINAL RESULT" : "NEXT");
            SetStatus("");
        }

        void ShowSectorDiamonds(SongResultDto sr)
        {
            if (_sectorText == null || sr?.Sectors == null) return;
            bool selfIsA = PvpMatchContext.SelfIsA;
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

            // 無限待ちにしない。相手が提出できない状況 (スコア検証の食い違い、
            // クライアントとサーバーのバージョン差など) では永久に進まないため、
            // 上限を超えたら理由を出してロビーへ戻す (K 報告 2026-08-01)。
            // サーバーの演奏フェーズのタイムアウトより十分長く取る。
            const int WaitLimitSec = 300;
            float waited = 0f;

            while (this != null && !_leaving)
            {
                await Task.Delay(2000);
                if (this == null || _leaving) return;

                waited += 2f;
                if (waited >= WaitLimitSec)
                {
                    SetStatus("相手の提出が確認できませんでした — ロビーへ戻ります");
                    await ReturnToLobbyAsync();
                    return;
                }

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
                    // 最終曲: 曲別リザルトを取得して表示してから FINAL RESULT へ
                    await ShowFetchedResultAsync(playOrder);
                    await FetchFinalAsync();
                    return;
                }

                // 次のピックフェーズへ進んだ = 両者提出完了 → 曲別リザルト GET で詳細を表示
                // (旧: サーバーに取得 GET が無く先攻側は詳細非表示 → 「リザルトが出ない」K 報告の原因。
                //  GET /matches/{id}/songs/{order}/result 追加済みのため取得して表示する)
                await ShowFetchedResultAsync(playOrder);
                return;
            }
        }

        /// <summary>先攻提出側: 相手の提出完了後に曲別リザルトを取得して表示し、累計も更新する。</summary>
        async Task ShowFetchedResultAsync(int songOrder)
        {
            var r = await PvpApi.GetSongResultAsync(PvpMatchContext.MatchId, songOrder);
            if (this == null || _leaving) return;

            if (r.Ok && r.Data != null && r.Data.Confirmed && r.Data.SongResult != null)
            {
                var sr = r.Data.SongResult;
                // 後攻提出側 (PvpResultBridge) と同じ規則で累計を更新する
                // (先攻側の submit レスポンスには song_result が無く、ここが唯一の加算点)。
                long selfPts = PvpMatchContext.SelfIsA ? sr.PointsA : sr.PointsB;
                long oppPts  = PvpMatchContext.SelfIsA ? sr.PointsB : sr.PointsA;
                PvpMatchContext.CumulativeSelfMilli += selfPts;
                PvpMatchContext.CumulativeOppMilli  += oppPts;

                ShowPoints(sr);
                ShowSectorDiamonds(sr);
                if (_cumulativeText != null)
                    _cumulativeText.text = $"累計  {PvpMatchContext.CumulativeSelfMilli / 1000.0:F1} pt  —  {PvpMatchContext.CumulativeOppMilli / 1000.0:F1} pt   (8pt で決着)";
                SetStatus("");
            }
            else
            {
                SetStatus("両者提出完了 (詳細の取得に失敗しました)");
            }
            EnableNext("NEXT");
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
                // GoToWhenIdle: 遷移中ガードで握り潰されると _leaving=true のまま復帰不能になる
                // (SOAK 検出 2026-07-30: リザルトから song2 ドラフトへ進めず不戦敗)
                SceneRouter.Instance?.GoToWhenIdle(SceneId.PVPSongPick);   // 次のドラフトフェーズへ
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
            SceneRouter.Instance?.GoToWhenIdle(SceneId.PVPLobby);
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
