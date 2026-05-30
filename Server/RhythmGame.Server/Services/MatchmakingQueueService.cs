using System;
using System.Collections.Generic;
using System.Linq;
using Domain.Pvp;

namespace RhythmGame.Server.Services
{
    /// <summary>
    /// PVP マッチング待ち行列 (in-memory)。
    /// 1 ユーザーが join したら待機リストへ。2 人目が join した瞬間ペアリング + ActiveMatchStore.Create でマッチ作成。
    /// マッチ成立後、相手側プレイヤーは status poll で matchId を取得する。
    ///
    /// 競合制御:
    ///   - 単一 lock で全状態を直列化 (短時間処理のためコンテンションは無視)
    ///   - プロセス再起動でロスト (短命データのため OK)
    /// </summary>
    public class MatchmakingQueueService
    {
        public enum Status { Idle = 0, Queued = 1, Matched = 2 }

        public class Snapshot
        {
            public Status Status      { get; set; }
            public string MatchId     { get; set; } = "";
            public string OpponentId  { get; set; } = "";
            public List<ActiveMatchStore.SongPick> Songs { get; set; } = new();
            public int    QueueDepth  { get; set; }
        }

        private readonly object _lock = new();
        private readonly Queue<string> _waiting = new();
        // 直近マッチング成立した相手側 userId → MatchId / 相手 / 楽曲
        private readonly Dictionary<string, MatchedNotice> _matched = new();
        private readonly ActiveMatchStore _store;

        public MatchmakingQueueService(ActiveMatchStore store)
        {
            _store = store;
        }

        private class MatchedNotice
        {
            public string MatchId;
            public string OpponentId;
            public List<ActiveMatchStore.SongPick> Songs;
        }

        public Snapshot Join(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return new Snapshot { Status = Status.Idle };

            lock (_lock)
            {
                // 既にキューにいる場合は何もしない (status だけ返す)
                if (_waiting.Contains(userId))
                    return new Snapshot { Status = Status.Queued, QueueDepth = _waiting.Count };
                // 既にマッチング済みなら matchId を返す
                if (_matched.TryGetValue(userId, out var notice))
                {
                    return new Snapshot
                    {
                        Status     = Status.Matched,
                        MatchId    = notice.MatchId,
                        OpponentId = notice.OpponentId,
                        Songs      = notice.Songs,
                    };
                }

                // 別ユーザーが待っているなら即ペアリング
                if (_waiting.Count > 0)
                {
                    string other = _waiting.Dequeue();
                    if (other == userId) // 自分自身はあり得ないが安全のため
                    {
                        _waiting.Enqueue(userId);
                        return new Snapshot { Status = Status.Queued, QueueDepth = _waiting.Count };
                    }
                    // 曲は空で作成 = ドラフト開始 (PICK/BAN はクライアントが polling で進める)。
                    // 旧来の「即3曲ランダム」は debug の match/create に残置。
                    var noSongs = new List<ActiveMatchStore.SongPick>();
                    var m = _store.Create(other, userId, noSongs);
                    _matched[other] = new MatchedNotice
                    {
                        MatchId    = m.MatchId,
                        OpponentId = userId,
                        Songs      = noSongs,
                    };
                    return new Snapshot
                    {
                        Status     = Status.Matched,
                        MatchId    = m.MatchId,
                        OpponentId = other,
                        Songs      = noSongs,
                    };
                }

                // キューに入れる
                _waiting.Enqueue(userId);
                return new Snapshot { Status = Status.Queued, QueueDepth = _waiting.Count };
            }
        }

        public Snapshot Leave(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return new Snapshot();
            lock (_lock)
            {
                if (_waiting.Contains(userId))
                {
                    var list = _waiting.ToList();
                    list.Remove(userId);
                    _waiting.Clear();
                    foreach (var u in list) _waiting.Enqueue(u);
                }
                _matched.Remove(userId);
                return new Snapshot { Status = Status.Idle, QueueDepth = _waiting.Count };
            }
        }

        /// <summary>poll: status のみ返す。matched 通知は consume 扱いで 1 度返したら _matched から消す。</summary>
        public Snapshot GetStatus(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return new Snapshot();
            lock (_lock)
            {
                if (_matched.TryGetValue(userId, out var notice))
                {
                    _matched.Remove(userId);
                    return new Snapshot
                    {
                        Status     = Status.Matched,
                        MatchId    = notice.MatchId,
                        OpponentId = notice.OpponentId,
                        Songs      = notice.Songs,
                    };
                }
                if (_waiting.Contains(userId))
                    return new Snapshot { Status = Status.Queued, QueueDepth = _waiting.Count };
                return new Snapshot { Status = Status.Idle, QueueDepth = _waiting.Count };
            }
        }
    }
}
