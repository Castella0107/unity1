using System.Collections.Generic;
using System.Threading.Tasks;

namespace RhythmGame.Network.Api
{
    /// <summary>Go サーバーの PVP キュー/試合 REST ラッパー (docs/05 §6.10-6.11)。</summary>
    public static class PvpApi
    {
        /// <summary>マッチングキューに参加する。即時成立時は status="matched"。</summary>
        public static Task<ApiResult<QueueStatusDto>> JoinQueueAsync(string difficultyPreference = "normal")
            => ApiClient.PostAsync<QueueStatusDto>("/pvp/queue/join",
                new QueueJoinRequestDto { DifficultyPreference = difficultyPreference });

        /// <summary>キュー状態を取得する (1.5秒間隔でポーリング。呼ぶたびに再マッチ試行される)。</summary>
        public static Task<ApiResult<QueueStatusDto>> GetQueueStatusAsync()
            => ApiClient.GetAsync<QueueStatusDto>("/pvp/queue/status");

        /// <summary>キューから離脱する。</summary>
        public static Task<ApiResult<QueueStatusDto>> LeaveQueueAsync()
            => ApiClient.PostAsync<QueueStatusDto>("/pvp/queue/leave", null);

        /// <summary>試合前情報 (両者プロフィール+レーティング変動予測) を取得する。</summary>
        public static Task<ApiResult<PrematchDto>> GetPrematchAsync(string matchId)
            => ApiClient.GetAsync<PrematchDto>($"/matches/{matchId}/prematch");

        /// <summary>準備完了/辞退を REST で通知する (WS の ready と同等。辞退は REST のみ)。</summary>
        public static Task<ApiResult<PhaseDto>> PostReadyAsync(string matchId, bool ready)
            => ApiClient.PostAsync<PhaseDto>($"/matches/{matchId}/ready",
                new ReadyRequestDto { Action = ready ? "ready" : "decline" });

        /// <summary>他ユーザーの公開情報 (表示名等)。</summary>
        public static Task<ApiResult<UserPublicDto>> GetUserAsync(string userId)
            => ApiClient.GetAsync<UserPublicDto>($"/users/{userId}");

        /// <summary>試合フェーズ状態を取得する。サーバーのタイムアウト自動処理もこの呼び出しで駆動される。</summary>
        public static Task<ApiResult<MatchStateDto>> GetStateAsync(string matchId)
            => ApiClient.GetAsync<MatchStateDto>($"/matches/{matchId}/state");

        /// <summary>楽曲/難易度をピックする (フェーズ依存: 曲+難易度 or 難易度のみ)。</summary>
        public static Task<ApiResult<PhaseDto>> PickAsync(string matchId, string songId, string difficulty)
            => ApiClient.PostAsync<PhaseDto>($"/matches/{matchId}/pick",
                new PickRequestDto { SongId = songId, Difficulty = difficulty });

        /// <summary>Song3 候補プールから 1 曲 BAN する (下位→上位の順)。</summary>
        public static Task<ApiResult<PhaseDto>> BanAsync(string matchId, string songId)
            => ApiClient.PostAsync<PhaseDto>($"/matches/{matchId}/ban",
                new BanRequestDto { SongId = songId });

        /// <summary>1 曲分のスコアを送信する (リプレイ添付時はサーバーengine再計算値で上書きされる)。</summary>
        public static Task<ApiResult<SubmitResponseDto>> SubmitAsync(string matchId, SubmitRequestDto dto)
            => ApiClient.PostAsync<SubmitResponseDto>($"/matches/{matchId}/submit", dto);

        /// <summary>終了済み試合の最終結果を取得する。</summary>
        public static Task<ApiResult<MatchResultDto>> GetResultAsync(string matchId)
            => ApiClient.GetAsync<MatchResultDto>($"/matches/{matchId}/result");

        // ── Phase 7 (DB変更不要の3本) ──────────────────────────────────────────

        /// <summary>ログインユーザーのロビー戦績サマリ (rating/勝敗/win_rate, 全期間)。</summary>
        public static Task<ApiResult<UserStatsDto>> GetMyStatsAsync()
            => ApiClient.GetAsync<UserStatsDto>("/users/me/stats");

        /// <summary>ログインユーザーの PVP 対戦履歴 (新しい順, limit≤50)。</summary>
        public static Task<ApiResult<UserMatchesDto>> GetMyMatchesAsync(int limit = 50, int offset = 0)
            => ApiClient.GetAsync<UserMatchesDto>($"/users/me/matches?limit={limit}&offset={offset}");
    }

    /// <summary>
    /// 進行中マッチのクロスシーン状態 (Go 移行版)。Matchmaking でセットされ、
    /// Prematch 以降の各画面が参照する。WebSocket 接続も試合を通してここで共有する。
    /// </summary>
    public static class PvpMatchContext
    {
        /// <summary>進行中の試合ID (なければ null)。</summary>
        public static string MatchId { get; private set; }
        /// <summary>対戦相手の user_id。</summary>
        public static string OpponentId { get; private set; }
        /// <summary>対戦相手の表示名 (Matchmaking/Prematch で取得。未取得なら user_id を返す)。</summary>
        public static string OpponentDisplayName
        {
            get => string.IsNullOrEmpty(_opponentDisplayName) ? OpponentId : _opponentDisplayName;
            set => _opponentDisplayName = value;
        }
        static string _opponentDisplayName;
        /// <summary>試合用 WebSocket (Prematch で接続)。</summary>
        public static MatchSocketClient Socket { get; set; }

        // ── ドラフト/対戦の進行状態 (Go 新フロー M4) ──────────────────────────

        /// <summary>自分が player_a 側か (Prematch で確定)。song3 の diff_a/diff_b 解決に使う。</summary>
        public static bool SelfIsA { get; set; }
        /// <summary>自分が下位レート側か (pick_song1 の acting_player で確定)。diff_l/diff_h 解決に使う。</summary>
        public static bool SelfIsLower { get; set; }

        /// <summary>現在プレイ中/直前の曲番号 (1-3)。</summary>
        public static int    CurrentSongOrder  { get; set; }
        /// <summary>現在の曲ID。</summary>
        public static string CurrentSongId     { get; set; }
        /// <summary>現在の自分の難易度。</summary>
        public static string CurrentDifficulty { get; set; }

        /// <summary>累計ミリポイント (自分/相手、曲リザルトで加算)。</summary>
        public static long CumulativeSelfMilli { get; set; }
        public static long CumulativeOppMilli  { get; set; }

        /// <summary>直近の submit レスポンス (曲リザルト画面が表示に使う)。</summary>
        public static SubmitResponseDto LastSubmit { get; set; }
        /// <summary>最終結果 (MatchEnd 用)。</summary>
        public static MatchResultDto FinalResult { get; set; }

        // ── ローカル PVP 履歴 (Ladder) 用の曲ごと集積 ───────────────────────────
        // 試合中に各曲を貯め、GoToMatchEnd で PvpMatchRecord に焼いて SavePvpMatchAsync する。
        // static シングルトンなので3曲(GamePlay→PVPResult→GamePlay…)をまたいで生存する。
        // SelfSectorScoresFlat は「曲×5セクター」のフラット配列(HistoryPvpRowView が j*5+s で参照)。
        public static readonly List<string> PlayedSongIds        = new List<string>();
        public static readonly List<string> PlayedDifficulties   = new List<string>();
        public static readonly List<string> SelfReplayPaths      = new List<string>();
        public static readonly List<int>    SelfSectorScoresFlat = new List<int>();

        /// <summary>1曲提出時に呼ぶ (PvpResultBridge から)。sectorScores は5要素。</summary>
        public static void RecordSongPlayed(string songId, string difficulty, string replayPath, int[] sectorScores)
        {
            PlayedSongIds.Add(songId ?? "");
            PlayedDifficulties.Add(difficulty ?? "");
            SelfReplayPaths.Add(replayPath ?? "");
            for (int i = 0; i < 5; i++)
                SelfSectorScoresFlat.Add(sectorScores != null && i < sectorScores.Length ? sectorScores[i] : 0);
        }

        static void ResetAccumulation()
        {
            PlayedSongIds.Clear();
            PlayedDifficulties.Clear();
            SelfReplayPaths.Clear();
            SelfSectorScoresFlat.Clear();
        }

        /// <summary>マッチ成立時にセットする。</summary>
        public static void StartMatch(string matchId, string opponentId)
        {
            MatchId    = matchId;
            OpponentId = opponentId;
            _opponentDisplayName = null;
            SelfIsA    = false;
            SelfIsLower = false;
            CurrentSongOrder  = 0;
            CurrentSongId     = null;
            CurrentDifficulty = null;
            CumulativeSelfMilli = 0;
            CumulativeOppMilli  = 0;
            LastSubmit  = null;
            FinalResult = null;
            ResetAccumulation();
        }

        /// <summary>試合終了/中断時にクリアする (WS も閉じる)。</summary>
        public static async System.Threading.Tasks.Task ClearAsync()
        {
            MatchId    = null;
            OpponentId = null;
            if (Socket != null)
            {
                await Socket.CloseAsync();
                Socket.Dispose();
                Socket = null;
            }
        }
    }
}
