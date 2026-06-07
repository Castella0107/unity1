using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace RhythmGame.Network.Api
{
    /// <summary>
    /// Go 新フローの曲提出と結果画面への橋渡し (M4)。
    ///   - <see cref="SubmitAndContinueAsync"/>: GamePlay 完了 → submit (リプレイ+claim) → PVPResult (曲リザルト) へ
    ///   - <see cref="GoToMatchEnd"/>: GET /result の値を既存 PVPMatchEnd 画面のパラメータに変換して遷移
    /// </summary>
    public static class PvpResultBridge
    {
        /// <summary>
        /// 1 曲分のスコアをサーバーに提出し、曲リザルト画面へ遷移する。
        /// sector_tie_breaks はクライアント未集計のためゼロ送信
        /// (リプレイ添付によりサーバー engine 再計算値で上書きされる前提 — docs/08 案B)。
        /// </summary>
        public static async Task SubmitAndContinueAsync(PlayRecord record)
        {
            string matchId = PvpMatchContext.MatchId;
            if (string.IsNullOrEmpty(matchId))
            {
                Debug.LogError("[PvpSubmit] MatchId がありません");
                SceneRouter.Instance?.GoTo(SceneId.PVPLobby);
                return;
            }

            // セクタースコア (5要素に整形)
            var sectors = new int[5];
            if (record.SectorScores != null)
                for (int i = 0; i < 5 && i < record.SectorScores.Length; i++)
                    sectors[i] = record.SectorScores[i];
            int total = 0;
            foreach (var s in sectors) total += s;

            // リプレイ (保存済みファイル → base64)
            string replayB64 = null;
            try
            {
                if (!string.IsNullOrEmpty(record.ReplayPath) && File.Exists(record.ReplayPath))
                    replayB64 = Convert.ToBase64String(File.ReadAllBytes(record.ReplayPath));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PvpSubmit] リプレイ読込失敗: " + e.Message);
            }

            var dto = new SubmitRequestDto
            {
                SectorScores    = sectors,
                SectorTieBreaks = new int[5],   // engine 再計算で上書きされる (リプレイ添付時)
                TotalScore      = total,
                ReplayBase64    = replayB64,
                Claim = new SubmitClaimDto
                {
                    Rank             = record.Rank,
                    PerfectPlusCount = record.PerfectPlusCount,
                    PerfectCount     = record.PerfectCount,
                    GreatCount       = record.GreatCount,
                    GoodCount        = record.GoodCount,
                    MissCount        = record.MissCount,
                    MaxCombo         = record.MaxCombo,
                    FastCount        = record.FastCount,
                    LateCount        = record.LateCount,
                },
            };

            Debug.Log($"[PvpSubmit] submit song{PvpMatchContext.CurrentSongOrder}: total={total} replay={(replayB64 != null ? replayB64.Length + "B64" : "なし")}");
            var r = await PvpApi.SubmitAsync(matchId, dto);

            if (r.Ok)
            {
                PvpMatchContext.LastSubmit = r.Data;
                if (r.Data?.SongResult != null)
                {
                    // 自分が後攻提出 → 曲結果が返る → 累計を更新
                    long selfPts = PvpMatchContext.SelfIsA ? r.Data.SongResult.PointsA : r.Data.SongResult.PointsB;
                    long oppPts  = PvpMatchContext.SelfIsA ? r.Data.SongResult.PointsB : r.Data.SongResult.PointsA;
                    PvpMatchContext.CumulativeSelfMilli += selfPts;
                    PvpMatchContext.CumulativeOppMilli  += oppPts;
                }
                if (r.Data?.Result != null)
                    PvpMatchContext.FinalResult = r.Data.Result;
            }
            else
            {
                Debug.LogWarning($"[PvpSubmit] submit 失敗: {r.ErrorCode} {r.ErrorMessage}");
                PvpMatchContext.LastSubmit = new SubmitResponseDto { MatchFinalized = false };
            }

            JacketBackgroundController.Instance?.SetCanvasEnabled(true);
            SceneRouter.Instance?.GoTo(SceneId.PVPResult);
        }

        /// <summary>
        /// GET /result の最終結果を既存 PVPMatchEnd 画面 (PvpMatchEndParameters) に変換して遷移する。
        /// </summary>
        public static void GoToMatchEnd()
        {
            var fin = PvpMatchContext.FinalResult;
            if (fin == null)
            {
                Debug.LogWarning("[PvpResultBridge] FinalResult なし — ロビーへ");
                _ = PvpMatchContext.ClearAsync();
                SceneRouter.Instance?.GoTo(SceneId.PVPLobby);
                return;
            }

            string selfId = AuthManager.UserId;
            string oppId  = PvpMatchContext.OpponentId;
            bool selfIsA  = PvpMatchContext.SelfIsA;

            string forfeitNote = fin.Forfeit
                ? (fin.ForfeitedPlayer == selfId ? "タイムアウトにより不戦敗" : "相手の棄権により不戦勝")
                : null;

            var p = new PvpMatchEndParameters
            {
                MatchId       = fin.MatchId,
                UserIdA       = selfIsA ? selfId : oppId,
                UserIdB       = selfIsA ? oppId : selfId,
                SelfUserId    = selfId,
                TotalPointsA  = fin.TotalPointsA / 1000.0,   // ミリポイント → pt 表示
                TotalPointsB  = fin.TotalPointsB / 1000.0,
                OutcomeKind   = fin.OutcomeKind == "draw" ? 0 : fin.OutcomeKind == "win_a" ? 1 : 2,
                RatingABefore = fin.RatingABefore,
                RatingAAfter  = fin.RatingAAfter,
                RatingBBefore = fin.RatingBBefore,
                RatingBAfter  = fin.RatingBAfter,
                ErrorMessage  = forfeitNote,
                Songs         = null,   // 曲別内訳はサーバー result に含まれない (Phase 7 で拡充検討)
            };

            _ = PvpMatchContext.ClearAsync();   // WS クローズ (MatchEnd では不要)
            SceneRouter.Instance?.GoTo(SceneId.PVPMatchEnd, p);
        }
    }
}
