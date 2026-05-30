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
        /// <summary>サーバーへの疎通確認(ping)。</summary>
        Task<NetworkClient.PingResult> PingAsync();

        /// <summary>ソロプレイのリプレイをサーバーで検証し、サーバー側判定結果を取得する。</summary>
        Task<NetworkClient.ValidateResult> ValidateReplayAsync(
            string chartHashHex,
            byte[] replayBytes,
            ResultClaimDto claim,
            ValidateRequestDto metadata = null);

        /// <summary>指定楽曲・難易度のリーダーボード上位を取得する。</summary>
        Task<NetworkClient.LeaderboardResult>  FetchLeaderboardAsync(string songId, string difficulty, int limit = 10);
        /// <summary>指定ユーザーのパーソナルベストと順位を取得する。</summary>
        Task<NetworkClient.PersonalBestResult> FetchPersonalBestAsync(string songId, string difficulty, string userId);

        /// <summary>PVP マッチを作成する(選曲プールは任意)。</summary>
        Task<NetworkClient.PvpCreateResult> CreateMatchAsync(string userIdA, string userIdB, string[] poolSongIds = null);
        /// <summary>マッチ結果(全曲リプレイ)を提出する。</summary>
        Task<NetworkClient.PvpSubmitResult> SubmitMatchAsync(string matchId, string userId, List<SubmitMatchSongDto> songs);
        /// <summary>確定済みマッチ結果を取得する。</summary>
        Task<NetworkClient.PvpFetchResult>  FetchMatchAsync(string matchId);

        /// <summary>試合中のリアルタイム進捗をサーバーへ送信する。</summary>
        Task<NetworkClient.PvpProgressResult> SendPvpProgressAsync(
            string matchId, string userId, int songIndex, int percentX1000, int score);
        /// <summary>試合中の相手進捗を取得する。</summary>
        Task<NetworkClient.PvpProgressResult> FetchPvpProgressAsync(string matchId);

        /// <summary>マッチキューに参加する。</summary>
        Task<NetworkClient.QueueResult> JoinQueueAsync(string userId);
        /// <summary>マッチキューから退出する。</summary>
        Task<NetworkClient.QueueResult> LeaveQueueAsync(string userId);
        /// <summary>マッチキューの現在状態を取得する。</summary>
        Task<NetworkClient.QueueResult> GetQueueStatusAsync(string userId);

        /// <summary>ドラフトの現在状態を取得する。</summary>
        Task<NetworkClient.DraftResult> FetchDraftAsync(string matchId);
        /// <summary>PICK を送信する(プールから 1 曲)。</summary>
        Task<NetworkClient.DraftResult> DraftPickAsync(string matchId, string userId, string songId);
        /// <summary>BAN を送信する(候補 3 曲から 1 曲)。</summary>
        Task<NetworkClient.DraftResult> DraftBanAsync(string matchId, string userId, string songId);
    }
}
