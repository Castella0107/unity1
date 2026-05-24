using System.Collections.Generic;
using System.Threading.Tasks;

namespace RhythmGame.Network
{
    /// <summary>
    /// クライアント・サーバー間の通信を抽象化する interface。
    /// 現状の唯一の実装は <see cref="NetworkClient"/> (REST HTTP + JSON、ASP.NET Core Server/ 接続)。
    /// 将来 PVPharmonics (Go) サーバーへの移行時、WebSocket + S3 PUT 版実装に差し替える前提で抽出。
    ///
    /// caller は <see cref="NetworkClient.Instance"/> 経由で interface 型として受け取り、
    /// 具象実装の切替に対して透過になる。
    ///
    /// result 型は実装側 (NetworkClient) のネスト class をそのまま参照する。
    /// </summary>
    public interface INetworkClient
    {
        // ── Health ──────────────────────────────────────────────────────────────
        Task<NetworkClient.PingResult> PingAsync();

        // ── Replay validate (ソロ) ──────────────────────────────────────────────
        Task<NetworkClient.ValidateResult> ValidateReplayAsync(
            string chartHashHex,
            byte[] replayBytes,
            ResultClaimDto claim,
            ValidateRequestDto metadata = null);

        // ── Leaderboard ─────────────────────────────────────────────────────────
        Task<NetworkClient.LeaderboardResult>  FetchLeaderboardAsync(string songId, string difficulty, int limit = 10);
        Task<NetworkClient.PersonalBestResult> FetchPersonalBestAsync(string songId, string difficulty, string userId);

        // ── PVP Match ───────────────────────────────────────────────────────────
        Task<NetworkClient.PvpCreateResult> CreateMatchAsync(string userIdA, string userIdB, string[] poolSongIds = null);
        Task<NetworkClient.PvpSubmitResult> SubmitMatchAsync(string matchId, string userId, List<SubmitMatchSongDto> songs);
        Task<NetworkClient.PvpFetchResult>  FetchMatchAsync(string matchId);

        // ── PVP Progress (in-match real-time) ───────────────────────────────────
        Task<NetworkClient.PvpProgressResult> SendPvpProgressAsync(
            string matchId, string userId, int songIndex, int percentX1000, int score);
        Task<NetworkClient.PvpProgressResult> FetchPvpProgressAsync(string matchId);

        // ── PVP Queue ───────────────────────────────────────────────────────────
        Task<NetworkClient.QueueResult> JoinQueueAsync(string userId);
        Task<NetworkClient.QueueResult> LeaveQueueAsync(string userId);
        Task<NetworkClient.QueueResult> GetQueueStatusAsync(string userId);
    }
}
