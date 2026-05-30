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

    /// <summary>1曲分の選曲(楽曲ID + 難易度)。</summary>
    [Serializable]
    public class SongPickDto
    {
        public string songId;
        public string difficulty;
    }

    /// <summary>PVP マッチ作成リクエスト。</summary>
    [Serializable]
    public class CreateMatchRequestDto
    {
        public string userIdA;
        public string userIdB;
        /// <summary>選曲プール。任意、null/空ならサーバー側で MatchPool.CreateBootstrapPool() を使う。</summary>
        public string[] poolSongIds;
    }

    /// <summary>PVP マッチ作成レスポンス。確定したマッチIDと選曲を返す。</summary>
    [Serializable]
    public class CreateMatchResponseDto
    {
        public string matchId;
        public string userIdA;
        public string userIdB;
        public System.Collections.Generic.List<SongPickDto> songs;
    }

    /// <summary>マッチ提出時の1曲分(楽曲ID + base64 リプレイ)。</summary>
    [Serializable]
    public class SubmitMatchSongDto
    {
        public string songId;
        public string replayDataBase64;
    }

    /// <summary>マッチ結果の提出リクエスト(全曲のリプレイ)。</summary>
    [Serializable]
    public class SubmitMatchRequestDto
    {
        public string userId;
        public System.Collections.Generic.List<SubmitMatchSongDto> songs;
    }

    /// <summary>確定したマッチ結果。両者のセクタースコア・ポイント・勝敗・レーティング変動を含む。</summary>
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
        /// <summary>勝敗種別 (-1=進行中, 0=Draw, 1=AWins, 2=BWins)。</summary>
        public int                        outcomeKind;
        public double                     ratingABefore;
        public double                     ratingAAfter;
        public double                     ratingBBefore;
        public double                     ratingBAfter;
        public long                       completedAtUnixMs;
    }

    /// <summary>マッチ提出レスポンス。受理可否・エラー・確定有無と結果を返す。</summary>
    [Serializable]
    public class SubmitMatchResponseDto
    {
        public bool            accepted;
        public string          error;
        public bool            matchFinalized;
        public MatchResultDto  result;
    }

    /// <summary>マッチキュー参加/退出/状態取得リクエスト。</summary>
    [Serializable]
    public class QueueRequestDto
    {
        public string userId;
    }

    /// <summary>マッチキューのレスポンス。</summary>
    [Serializable]
    public class QueueResponseDto
    {
        /// <summary>キュー状態 ("idle" / "queued" / "matched")。</summary>
        public string status;
        public string matchId;
        public string opponentId;
        public System.Collections.Generic.List<SongPickDto> songs;
        public int    queueDepth;
    }

    /// <summary>ドラフト(PICK/BAN)の操作リクエスト(自分の userId + 対象 songId)。</summary>
    [Serializable]
    public class DraftActionRequestDto
    {
        public string userId;
        public string songId;
    }

    /// <summary>
    /// ドラフト状態スナップショット。サーバー <c>PvpController.DraftStateDto</c> に対応。
    /// ブラインド方式: 両者が完了するまで相手の選択 (pickA/pickB, banA/banB) は空で伏せられる。
    /// </summary>
    [Serializable]
    public class DraftStateDto
    {
        /// <summary>現在フェーズ ("pick" / "ban" / "done")。</summary>
        public string phase;
        public bool   aPicked;
        public bool   bPicked;
        public bool   aBanned;
        public bool   bBanned;
        /// <summary>両 PICK 完了まで空文字。</summary>
        public string pickA;
        public string pickB;
        /// <summary>両 PICK 後に確定する BAN 候補 3 曲 (songId)。</summary>
        public System.Collections.Generic.List<string> candidates;
        /// <summary>両 BAN 完了まで空文字。</summary>
        public string banA;
        public string banB;
        /// <summary>done 時の確定 3 曲 ([PickA, PickB, 3曲目])。</summary>
        public System.Collections.Generic.List<SongPickDto> songs;
        /// <summary>PICK 候補 = プール全曲 ID。</summary>
        public System.Collections.Generic.List<string> pool;
    }

    /// <summary>PVP 進捗の送信 DTO。</summary>
    [Serializable]
    public class ProgressUpdateDto
    {
        public string userId;
        public int    songIndex;
        /// <summary>進捗率 ×1000(0〜100000)。</summary>
        public int    percentX1000;
        public int    score;
    }

    /// <summary>片側プレイヤーの進捗 DTO。</summary>
    [Serializable]
    public class ProgressSideDto
    {
        public string userId;
        public int    songIndex;
        /// <summary>進捗率 ×1000(0〜100000)。</summary>
        public int    percentX1000;
        public int    score;
        public long   updatedAtUnixMs;
    }

    /// <summary>両プレイヤーの進捗スナップショット。</summary>
    [Serializable]
    public class ProgressSnapshotDto
    {
        public string          matchId;
        public ProgressSideDto a;
        public ProgressSideDto b;
        public bool            finalized;
    }
}
