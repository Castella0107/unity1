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
        /// <summary>試合用 WebSocket (Prematch で接続)。</summary>
        public static MatchSocketClient Socket { get; set; }

        /// <summary>マッチ成立時にセットする。</summary>
        public static void StartMatch(string matchId, string opponentId)
        {
            MatchId    = matchId;
            OpponentId = opponentId;
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
