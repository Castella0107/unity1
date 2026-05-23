using System;

namespace RhythmGame.Network
{
    /// <summary>サーバーへの ping レスポンス DTO。</summary>
    [Serializable]
    public class PingResponseDto
    {
        public string status;
        public string serverVersion;
        public long   serverTimeUnixMs;
    }

    /// <summary>クライアントが主張するプレイ結果。</summary>
    [Serializable]
    public class ResultClaimDto
    {
        public long   score;
        public int    maxCombo;
        public int    perfectPlus;
        public int    perfect;
        public int    great;
        public int    good;
        public int    miss;
        public string rank;
    }

    /// <summary>リプレイ検証リクエスト。replayDataBase64 は ReplayEncoder.Encode の bytes を base64 化したもの。</summary>
    [Serializable]
    public class ValidateRequestDto
    {
        public string         chartHash;
        public string         replayDataBase64;
        public ResultClaimDto claim;

        // 永続化用 (空でも検証は通るが、空だとサーバーは保存しない)
        public string playId;
        public string songId;
        public string difficulty;
        public string userId;
        public long   playedAtUnixMs;
        public int    totalNotes;
        public bool   isFullCombo;
        public bool   isAllPerfect;
        public bool   isAllPerfectPlus;
    }

    /// <summary>リプレイ検証レスポンス。サーバー側の判定結果と差分理由を返す。</summary>
    [Serializable]
    public class ValidateResponseDto
    {
        public bool           isValid;
        public ResultClaimDto serverResult;
        public string         mismatchReason;
    }

    /// <summary>リーダーボード1件のエントリ。</summary>
    [Serializable]
    public class LeaderboardEntryDto
    {
        public int    rank;
        public string userId;
        public int    score;
        public string scoreRank;
        public int    maxCombo;
        public bool   isFullCombo;
        public bool   isAllPerfectPlus;
        public long   playedAtUnixMs;
    }

    /// <summary>GET /api/leaderboard レスポンス。</summary>
    [Serializable]
    public class LeaderboardResponseDto
    {
        public string                 songId;
        public string                 difficulty;
        public int                    total;
        public System.Collections.Generic.List<LeaderboardEntryDto> entries;
    }

    /// <summary>GET /api/leaderboard/{songId}/{difficulty}/me?userId=X レスポンス。</summary>
    [Serializable]
    public class PersonalBestResponseDto
    {
        public string              songId;
        public string              difficulty;
        public string              userId;
        public bool                hasRecord;
        public int                 overallRank;
        public int                 totalUsers;
        public LeaderboardEntryDto best;
    }

    // ── PVP DTOs ────────────────────────────────────────────────────────────

    [Serializable]
    public class SongPickDto
    {
        public string songId;
        public string difficulty;
    }

    [Serializable]
    public class CreateMatchRequestDto
    {
        public string userIdA;
        public string userIdB;
        public string[] poolSongIds;   // 任意、null/空ならサーバー側で MatchPool.CreateBootstrapPool()
    }

    [Serializable]
    public class CreateMatchResponseDto
    {
        public string matchId;
        public string userIdA;
        public string userIdB;
        public System.Collections.Generic.List<SongPickDto> songs;
    }

    [Serializable]
    public class SubmitMatchSongDto
    {
        public string songId;
        public string replayDataBase64;
    }

    [Serializable]
    public class SubmitMatchRequestDto
    {
        public string userId;
        public System.Collections.Generic.List<SubmitMatchSongDto> songs;
    }

    [Serializable]
    public class MatchResultDto
    {
        public string                     matchId;
        public string                     userIdA;
        public string                     userIdB;
        public System.Collections.Generic.List<SongPickDto> songs;
        public System.Collections.Generic.List<int> sectorScoresA;
        public System.Collections.Generic.List<int> sectorScoresB;
        public double                     totalPointsA;
        public double                     totalPointsB;
        public int                        outcomeKind;     // -1=in progress, 0=Draw, 1=AWins, 2=BWins
        public double                     ratingABefore;
        public double                     ratingAAfter;
        public double                     ratingBBefore;
        public double                     ratingBAfter;
        public long                       completedAtUnixMs;
    }

    [Serializable]
    public class SubmitMatchResponseDto
    {
        public bool            accepted;
        public string          error;
        public bool            matchFinalized;
        public MatchResultDto  result;
    }

    [Serializable]
    public class QueueRequestDto
    {
        public string userId;
    }

    [Serializable]
    public class QueueResponseDto
    {
        public string status;       // "idle" / "queued" / "matched"
        public string matchId;
        public string opponentId;
        public System.Collections.Generic.List<SongPickDto> songs;
        public int    queueDepth;
    }

    [Serializable]
    public class ProgressUpdateDto
    {
        public string userId;
        public int    songIndex;
        public int    percentX1000;   // 0..100000
        public int    score;
    }

    [Serializable]
    public class ProgressSideDto
    {
        public string userId;
        public int    songIndex;
        public int    percentX1000;
        public int    score;
        public long   updatedAtUnixMs;
    }

    [Serializable]
    public class ProgressSnapshotDto
    {
        public string          matchId;
        public ProgressSideDto a;
        public ProgressSideDto b;
        public bool            finalized;
    }
}
