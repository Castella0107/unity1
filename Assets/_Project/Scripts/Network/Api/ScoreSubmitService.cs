using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace RhythmGame.Network.Api
{
    /// <summary>
    /// ソロプレイのスコアをサーバー検証 (POST /score/validate) に提出し、
    /// リーダーボードへ取り込ませるサービス (docs/design_doc/leaderboard_client.md §3)。
    ///
    /// 送信条件 (ShouldSubmit): ソロ・非オートプレイ・ログイン済み・サーバー配信譜面。
    /// 失敗はログのみ (リトライ無し・プレイ体験をブロックしない)。
    /// </summary>
    public static class ScoreSubmitService
    {
        /// <summary>直近の提出結果 (ベスト更新バッジ等の将来フック用)。未提出/失敗は null。</summary>
        public static ScoreValidateResponseDto LastResult { get; private set; }

        /// <summary>提出条件の判定 (Domain の純関数へ委譲 — EditMode テストは ScoreSubmitPolicyTests)。</summary>
        public static bool ShouldSubmit(bool isPvp, bool isAutoPlay, bool loggedIn, bool hasChartId, bool hasReplayPath)
            => ScoreSubmitPolicy.ShouldSubmit(isPvp, isAutoPlay, loggedIn, hasChartId, hasReplayPath);

        /// <summary>
        /// 条件を満たせばリプレイをサーバーへ提出する (fire-and-forget 前提の async)。
        /// claim はワイド判定プレイでは省略する (サーバーは常に標準判定で再計算するため、
        /// ワイド時はクライアント値と一致せず claim_matched が常に false になりログを汚す)。
        /// </summary>
        public static async Task SubmitIfEligibleAsync(
            PlayRecord record, string chartHash, bool isPvp, bool isAutoPlay, bool judgeWide)
        {
            LastResult = null;
            bool loggedIn = !string.IsNullOrEmpty(AuthManager.UserId);
            bool hasChart = ServerSongLibrary.TryGetChartId(record.SongId, record.Difficulty, out string chartId);
            if (!ShouldSubmit(isPvp, isAutoPlay, loggedIn, hasChart, !string.IsNullOrEmpty(record.ReplayPath)))
            {
                Debug.Log($"[ScoreSubmit] スキップ (pvp={isPvp} auto={isAutoPlay} login={loggedIn} chart={hasChart})");
                return;
            }

            string replayB64;
            try
            {
                string path = record.ReplayPath;
                replayB64 = await Task.Run(() => Convert.ToBase64String(File.ReadAllBytes(path)));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ScoreSubmit] リプレイ読込失敗: " + e.Message);
                return;
            }

            var dto = new ScoreValidateRequestDto
            {
                ChartId      = chartId,
                ChartHash    = chartHash ?? "",
                ReplayBase64 = replayB64,
                Claim = judgeWide ? null : new ScoreValidateClaimDto
                {
                    Score       = record.EffectiveScore,
                    Rank        = record.Rank,
                    PerfectPlus = record.PerfectPlusCount,
                    Perfect     = record.PerfectCount,
                    Great       = record.GreatCount,
                    Good        = record.GoodCount,
                    Miss        = record.MissCount,
                    MaxCombo    = record.MaxCombo,
                },
            };

            var r = await LeaderboardApi.ValidateScoreAsync(dto);
            if (r.Ok && r.Data != null)
            {
                LastResult = r.Data;
                Debug.Log($"[ScoreSubmit] 提出完了 chart={chartId} server_score={r.Data.Result?.Score} " +
                          $"best_updated={r.Data.BestUpdated} claim_matched={r.Data.ClaimMatched}");
            }
            else
            {
                Debug.LogWarning($"[ScoreSubmit] 提出失敗: {r.ErrorCode} {r.ErrorMessage}");
            }
        }
    }
}
