using System.Threading.Tasks;

namespace RhythmGame.Network
{
    /// <summary>
    /// 直近のソロプレイのサーバー送信 (ValidateReplayAsync) を Result 画面が参照するための共有スロット。
    /// GamePlayController が完走時の fire-and-forget 送信を開始する際に Task を登録し、Result は
    /// 同じ playId のときだけその Task を await して VALID/INVALID を表示する (再送信を避けるため)。
    /// 永続化しない単純な静的スロット。次のプレイ送信で上書きされる。
    /// </summary>
    public static class ServerSubmissionTracker
    {
        /// <summary>登録された送信の playId。Result 側で「この回のプレイか」を一致確認するのに使う。</summary>
        public static string PlayId { get; private set; }

        /// <summary>進行中/完了済みの検証 Task。Result が同一インスタンスを await する。</summary>
        public static Task<NetworkClient.ValidateResult> Task { get; private set; }

        /// <summary>送信開始時に GamePlayController から登録する。</summary>
        public static void Register(string playId, Task<NetworkClient.ValidateResult> task)
        {
            PlayId = playId;
            Task   = task;
        }
    }
}
