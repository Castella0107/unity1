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

            // ローカル PVP 履歴(Ladder)用に各曲を集積する(GoToMatchEnd で PvpMatchRecord に焼く)。
            // submit 成否に関わらず手元の実プレイは記録する。
            PvpMatchContext.RecordSongPlayed(record.SongId, record.Difficulty, record.ReplayPath, sectors);

            // リプレイ (保存済みファイル → base64)。読込+Base64化は重いのでワーカースレッドへ退避し、
            // 曲間のメインスレッドフレーム落ちを防ぐ。
            string replayB64  = null;
            string replayPath = record.ReplayPath;
            if (!string.IsNullOrEmpty(replayPath) && File.Exists(replayPath))
            {
                try
                {
                    replayB64 = await System.Threading.Tasks.Task.Run(
                        () => Convert.ToBase64String(File.ReadAllBytes(replayPath)));
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[PvpSubmit] リプレイ読込失敗: " + e.Message);
                }
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
                // 提出が通らなかったのに「先攻提出＝相手待ち」と同じ状態にしていたため、
                // リザルト画面で永久に待ち続けていた (K 報告 2026-08-01:
                // サーバー未更新でスコア検証が食い違い、双方が提出待ちで停止)。
                // 待たせず理由を出してロビーへ戻す。
                Debug.LogWarning($"[PvpSubmit] submit 失敗: {r.ErrorCode} {r.ErrorMessage}");
                PvpMatchContext.SubmitError =
                    string.IsNullOrEmpty(r.ErrorMessage) ? (r.ErrorCode ?? "通信エラー") : r.ErrorMessage;
                PvpMatchContext.LastSubmit = null;
            }

            JacketBackgroundController.Instance?.SetCanvasEnabled(true);
            // GoToWhenIdle: 遷移中ガードによる取りこぼし防止 (SOAK 2026-07-30)
            SceneRouter.Instance?.GoToWhenIdle(SceneId.PVPResult);
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

            // MatchEnd 画面は名前を表示にも自他判定 (SelfUserId == UserIdA) にも使うため、
            // 表示名で統一して渡す (user_id の生表示を避ける)。
            string selfId = string.IsNullOrEmpty(AuthManager.DisplayName) ? AuthManager.UserId : AuthManager.DisplayName;
            string oppId  = PvpMatchContext.OpponentDisplayName;
            bool selfIsA  = PvpMatchContext.SelfIsA;

            string forfeitNote = fin.Forfeit
                ? (fin.ForfeitedPlayer == AuthManager.UserId ? "タイムアウトにより不戦敗" : "相手の棄権により不戦勝")
                : null;   // 判定は user_id で行う (selfId は表示名)

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

            // ── ローカル PVP 履歴(Ladder)に1試合=1レコードで保存 ──
            // 表示名で格納する(HistoryPvpRowView は SelfUserId/OpponentId をそのまま名前表示する)。
            try
            {
                bool selfWon = (selfIsA && fin.OutcomeKind == "win_a") || (!selfIsA && fin.OutcomeKind == "win_b");
                int resultKind = fin.OutcomeKind == "draw" ? 0 : (selfWon ? 1 : 2);
                var rec = new PvpMatchRecord
                {
                    MatchId              = fin.MatchId,
                    SelfUserId           = selfId,   // 表示名
                    OpponentId           = oppId,    // 表示名
                    ResultKind           = resultKind,
                    SelfPoints           = (selfIsA ? fin.TotalPointsA : fin.TotalPointsB) / 1000.0,
                    OpponentPoints       = (selfIsA ? fin.TotalPointsB : fin.TotalPointsA) / 1000.0,
                    SelfRatingBefore     = selfIsA ? fin.RatingABefore : fin.RatingBBefore,
                    SelfRatingAfter      = selfIsA ? fin.RatingAAfter  : fin.RatingBAfter,
                    OpponentRatingBefore = selfIsA ? fin.RatingBBefore : fin.RatingABefore,
                    OpponentRatingAfter  = selfIsA ? fin.RatingBAfter  : fin.RatingAAfter,
                    SongIds              = PvpMatchContext.PlayedSongIds.ToArray(),
                    Difficulties         = PvpMatchContext.PlayedDifficulties.ToArray(),
                    SelfSectorScores     = PvpMatchContext.SelfSectorScoresFlat.ToArray(),
                    OpponentSectorScores = null,   // 相手のセクタースコアは未取得(best-effort)
                    SelfReplayPaths      = PvpMatchContext.SelfReplayPaths.ToArray(),
                    CompletedAtUnixMs    = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                };
                _ = RepositoryService.Instance?.PlayRecords?.SavePvpMatchAsync(rec);
                Debug.Log($"[PvpResultBridge] Ladder履歴に保存: {rec.MatchId} songs={rec.SongIds.Length} replays={rec.SelfReplayPaths.Length}");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PvpResultBridge] Ladder履歴の保存に失敗: " + e.Message);
            }

            _ = PvpMatchContext.ClearAsync();   // WS クローズ (MatchEnd では不要)
            SceneRouter.Instance?.GoToWhenIdle(SceneId.PVPMatchEnd, p);
        }
    }
}
