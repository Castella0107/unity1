using Newtonsoft.Json;

// Go サーバー (PVPharmonics) /api/v1 の DTO 群。
// 命名規約: snake_case (docs/05_api_rest.md §4)。[JsonProperty] で明示マッピングする。
namespace RhythmGame.Network.Api
{
    /// <summary>共通レスポンス封筒。成功 = data 非null / 失敗 = error 非null。</summary>
    public class ApiEnvelope<T>
    {
        [JsonProperty("data")]  public T           Data;
        [JsonProperty("error")] public ApiErrorDto Error;
    }

    /// <summary>エラー封筒の中身 (code はエラーコード、message は日本語の人間可読文)。</summary>
    public class ApiErrorDto
    {
        [JsonProperty("code")]    public string Code;
        [JsonProperty("message")] public string Message;
    }

    /// <summary>空レスポンス用 (204 No Content 等)。</summary>
    public class EmptyDto { }

    // ── 認証 (docs/05 §6.2) ─────────────────────────────────────────────────

    public class RegisterRequestDto
    {
        [JsonProperty("email")]        public string Email;
        [JsonProperty("password")]     public string Password;
        [JsonProperty("display_name")] public string DisplayName;
    }

    public class LoginRequestDto
    {
        [JsonProperty("email")]    public string Email;
        [JsonProperty("password")] public string Password;
    }

    public class RefreshRequestDto
    {
        [JsonProperty("refresh_token")] public string RefreshToken;
    }

    public class LogoutRequestDto
    {
        [JsonProperty("refresh_token")] public string RefreshToken;
    }

    /// <summary>register / login / refresh 共通のトークンレスポンス (register のみ email 含む)。</summary>
    public class AuthResponseDto
    {
        [JsonProperty("user_id")]       public string UserId;
        [JsonProperty("email")]         public string Email;
        [JsonProperty("display_name")]  public string DisplayName;
        [JsonProperty("access_token")]  public string AccessToken;
        [JsonProperty("refresh_token")] public string RefreshToken;
        [JsonProperty("expires_in")]    public int    ExpiresIn;     // 秒 (アクセストークン寿命)
    }

    // ── ユーザー (docs/05 §6.3) ─────────────────────────────────────────────

    public class UserMeDto
    {
        [JsonProperty("user_id")]      public string UserId;
        [JsonProperty("email")]        public string Email;
        [JsonProperty("display_name")] public string DisplayName;
        [JsonProperty("rating")]       public double Rating;
        [JsonProperty("total_plays")]  public int    TotalPlays;
        [JsonProperty("win_count")]    public int    WinCount;
        [JsonProperty("loss_count")]   public int    LossCount;
        [JsonProperty("draw_count")]   public int    DrawCount;
        [JsonProperty("created_at")]   public string CreatedAt;
    }

    public class UserPublicDto
    {
        [JsonProperty("user_id")]      public string UserId;
        [JsonProperty("display_name")] public string DisplayName;
        [JsonProperty("rating")]       public double Rating;
        [JsonProperty("rating_peak")]  public double RatingPeak;
        [JsonProperty("total_plays")]  public int    TotalPlays;
        [JsonProperty("win_count")]    public int    WinCount;
        [JsonProperty("loss_count")]   public int    LossCount;
        [JsonProperty("draw_count")]   public int    DrawCount;
    }

    public class PatchMeRequestDto
    {
        [JsonProperty("display_name")] public string DisplayName;
    }

    // ── 曲・譜面 (docs/05 §6.4) ─────────────────────────────────────────────

    public class SongsListDto
    {
        [JsonProperty("songs")]  public System.Collections.Generic.List<SongListItemDto> Songs;
        [JsonProperty("total")]  public int Total;
        [JsonProperty("limit")]  public int Limit;
        [JsonProperty("offset")] public int Offset;
    }

    public class SongListItemDto
    {
        [JsonProperty("song_id")]          public string   SongId;
        [JsonProperty("title")]            public string   Title;
        [JsonProperty("artist")]           public string   Artist;
        [JsonProperty("bpm")]              public double   Bpm;
        [JsonProperty("duration_seconds")] public int      DurationSeconds;
        [JsonProperty("difficulties")]     public string[] Difficulties;
    }

    public class SongDetailDto
    {
        [JsonProperty("song_id")]          public string SongId;
        [JsonProperty("title")]            public string Title;
        [JsonProperty("artist")]           public string Artist;
        [JsonProperty("bpm")]              public double Bpm;
        [JsonProperty("duration_seconds")] public int    DurationSeconds;
        [JsonProperty("charts")]           public System.Collections.Generic.List<ChartInfoDto> Charts;
    }

    public class ChartInfoDto
    {
        [JsonProperty("chart_id")]   public string ChartId;
        [JsonProperty("difficulty")] public string Difficulty;
        [JsonProperty("level")]      public int    Level;
        [JsonProperty("note_count")] public int    NoteCount;
        [JsonProperty("chart_hash")] public string ChartHash;
        [JsonProperty("version")]    public int    Version;
    }

    // ── PVP キュー (docs/05 §6.10) ──────────────────────────────────────────

    public class QueueJoinRequestDto
    {
        [JsonProperty("difficulty_preference")] public string DifficultyPreference;
    }

    /// <summary>join / status / leave 共通レスポンス。status = "idle" / "queued" / "matched"。</summary>
    public class QueueStatusDto
    {
        [JsonProperty("status")]      public string Status;
        [JsonProperty("match_id")]    public string MatchId;
        [JsonProperty("opponent_id")] public string OpponentId;
        [JsonProperty("queue_depth")] public int    QueueDepth;
    }

    // ── PVP 試合: Prematch (docs/05 §6.11) ──────────────────────────────────

    public class PrematchDto
    {
        [JsonProperty("match_id")]                public string             MatchId;
        [JsonProperty("player_a")]                public PrematchPlayerDto  PlayerA;
        [JsonProperty("player_b")]                public PrematchPlayerDto  PlayerB;
        [JsonProperty("predicted_rating_change")] public RatingPredictionDto PredictedRatingChange;
        [JsonProperty("phase")]                   public string             Phase;
        [JsonProperty("phase_deadline")]          public string             PhaseDeadline;
    }

    public class PrematchPlayerDto
    {
        [JsonProperty("user_id")]      public string UserId;
        [JsonProperty("display_name")] public string DisplayName;
        [JsonProperty("rating")]       public double Rating;
        [JsonProperty("win_count")]    public int    WinCount;
        [JsonProperty("loss_count")]   public int    LossCount;
        [JsonProperty("draw_count")]   public int    DrawCount;
    }

    public class RatingPredictionDto
    {
        [JsonProperty("player_a_win")] public RatingDeltaDto PlayerAWin;
        [JsonProperty("player_b_win")] public RatingDeltaDto PlayerBWin;
        [JsonProperty("draw")]         public RatingDeltaDto Draw;
    }

    public class RatingDeltaDto
    {
        [JsonProperty("player_a_delta")] public double PlayerADelta;
        [JsonProperty("player_b_delta")] public double PlayerBDelta;
    }

    public class ReadyRequestDto
    {
        [JsonProperty("action")] public string Action;   // "ready" | "decline"
    }

    /// <summary>ready / pick / ban の共通フェーズ遷移レスポンス。</summary>
    public class PhaseDto
    {
        [JsonProperty("phase")]          public string Phase;
        [JsonProperty("phase_deadline")] public string PhaseDeadline;
        [JsonProperty("pick3_song_id")]  public string Pick3SongId;
    }

    /// <summary>GET /matches/{id}/state — フェーズ・手番・選曲状況 (実レスポンスで検証済み)。</summary>
    public class MatchStateDto
    {
        [JsonProperty("match_id")]       public string   MatchId;
        [JsonProperty("phase")]          public string   Phase;
        [JsonProperty("phase_deadline")] public string   PhaseDeadline;
        [JsonProperty("acting_player")]  public string   ActingPlayer;
        [JsonProperty("song3_pool")]     public string[] Song3Pool;
        [JsonProperty("picks")]          public MatchPicksDto Picks;
    }

    public class MatchPicksDto
    {
        [JsonProperty("song1")] public SongPickDto Song1;
        [JsonProperty("song2")] public SongPickDto Song2;
        [JsonProperty("song3")] public SongPickDto Song3;
    }

    public class SongPickDto
    {
        [JsonProperty("song_id")] public string SongId;
        [JsonProperty("diff_l")]  public string DiffL;
        [JsonProperty("diff_h")]  public string DiffH;
        [JsonProperty("diff_a")]  public string DiffA;
        [JsonProperty("diff_b")]  public string DiffB;
    }

    // ── PVP 試合: pick / ban / submit / result (docs/05 §6.11) ──────────────

    public class PickRequestDto
    {
        [JsonProperty("song_id",    NullValueHandling = NullValueHandling.Ignore)] public string SongId;
        [JsonProperty("difficulty", NullValueHandling = NullValueHandling.Ignore)] public string Difficulty;
    }

    public class BanRequestDto
    {
        [JsonProperty("song_id")] public string SongId;
    }

    public class SubmitRequestDto
    {
        [JsonProperty("sector_scores")]     public int[]  SectorScores;
        [JsonProperty("sector_tie_breaks")] public int[]  SectorTieBreaks;
        [JsonProperty("total_score")]       public int    TotalScore;
        [JsonProperty("replay_base64", NullValueHandling = NullValueHandling.Ignore)] public string ReplayBase64;
        [JsonProperty("claim",         NullValueHandling = NullValueHandling.Ignore)] public SubmitClaimDto Claim;
    }

    public class SubmitClaimDto
    {
        [JsonProperty("rank")]               public string Rank;
        [JsonProperty("perfect_plus_count")] public int    PerfectPlusCount;
        [JsonProperty("perfect_count")]      public int    PerfectCount;
        [JsonProperty("great_count")]        public int    GreatCount;
        [JsonProperty("good_count")]         public int    GoodCount;
        [JsonProperty("miss_count")]         public int    MissCount;
        [JsonProperty("max_combo")]          public int    MaxCombo;
        [JsonProperty("fast_count")]         public int    FastCount;
        [JsonProperty("late_count")]         public int    LateCount;
    }

    public class SubmitResponseDto
    {
        [JsonProperty("match_finalized")] public bool           MatchFinalized;   // この曲の両者提出が揃ったか
        [JsonProperty("match_over")]      public bool           MatchOver;
        [JsonProperty("clinch")]          public bool           Clinch;
        [JsonProperty("song_result")]     public SongResultDto  SongResult;
        [JsonProperty("result")]          public MatchResultDto Result;
    }

    public class SongResultDto
    {
        [JsonProperty("song_index")] public int               SongIndex;   // 1-3
        [JsonProperty("points_a")]   public int               PointsA;     // ミリポイント(難易度倍率適用後)
        [JsonProperty("points_b")]   public int               PointsB;
        [JsonProperty("sectors")]    public SectorResultDto[] Sectors;
    }

    // ── リーダーボード (docs/design_doc/leaderboard_client.md §2 / サーバー leaderboard_design.md) ──

    public class LeaderboardEntryDto
    {
        [JsonProperty("rank_position")] public long   RankPosition;
        [JsonProperty("user_id")]       public string UserId;
        [JsonProperty("display_name")]  public string DisplayName;
        [JsonProperty("score")]         public int    Score;
        [JsonProperty("rank")]          public string Rank;
        [JsonProperty("achieved_at")]   public string AchievedAt;
    }

    public class LeaderboardDto
    {
        [JsonProperty("song_id")]    public string                SongId;
        [JsonProperty("difficulty")] public string                Difficulty;
        [JsonProperty("season_id")]  public string                SeasonId;
        [JsonProperty("entries")]    public LeaderboardEntryDto[] Entries;
        [JsonProperty("total")]      public long                  Total;
    }

    public class PersonalBestDto
    {
        [JsonProperty("rank_position")] public int    RankPosition;
        [JsonProperty("score")]         public int    Score;
        [JsonProperty("rank")]          public string Rank;
        [JsonProperty("perfect_plus")]  public int    PerfectPlus;
        [JsonProperty("perfect")]       public int    Perfect;
        [JsonProperty("great")]         public int    Great;
        [JsonProperty("good")]          public int    Good;
        [JsonProperty("miss")]          public int    Miss;
        [JsonProperty("max_combo")]     public int    MaxCombo;
        [JsonProperty("achieved_at")]   public string AchievedAt;
    }

    public class PersonalBestFetchDto
    {
        [JsonProperty("song_id")]       public string          SongId;
        [JsonProperty("difficulty")]    public string          Difficulty;
        [JsonProperty("season_id")]     public string          SeasonId;
        [JsonProperty("personal_best")] public PersonalBestDto PersonalBest;   // null = 未提出
    }

    // ── POST /score/validate (phase7 spec §1 + best_updated 拡張) ───────────────

    public class ScoreValidateClaimDto
    {
        [JsonProperty("score")]        public long   Score;
        [JsonProperty("rank")]         public string Rank;
        [JsonProperty("perfect_plus")] public int    PerfectPlus;
        [JsonProperty("perfect")]      public int    Perfect;
        [JsonProperty("great")]        public int    Great;
        [JsonProperty("good")]         public int    Good;
        [JsonProperty("miss")]         public int    Miss;
        [JsonProperty("max_combo")]    public int    MaxCombo;
    }

    public class ScoreValidateRequestDto
    {
        [JsonProperty("chart_id")]      public string ChartId;
        [JsonProperty("chart_hash")]    public string ChartHash;
        [JsonProperty("replay_base64")] public string ReplayBase64;
        [JsonProperty("claim", NullValueHandling = NullValueHandling.Ignore)] public ScoreValidateClaimDto Claim;
    }

    public class ScoreValidateResultDto
    {
        [JsonProperty("score")]     public long   Score;
        [JsonProperty("rank")]      public string Rank;
        [JsonProperty("max_combo")] public int    MaxCombo;
        [JsonProperty("miss")]      public int    Miss;
    }

    public class ScoreValidateResponseDto
    {
        [JsonProperty("chart_id")]            public string                 ChartId;
        [JsonProperty("verified")]            public bool                   Verified;
        [JsonProperty("result")]              public ScoreValidateResultDto Result;
        [JsonProperty("claim_matched")]       public bool?                  ClaimMatched;
        [JsonProperty("best_updated")]        public bool                   BestUpdated;
        [JsonProperty("personal_best_score")] public int?                   PersonalBestScore;
    }

    /// <summary>GET /matches/{id}/songs/{order}/result のレスポンス。
    /// 先攻提出側が相手の提出後に曲リザルトを取得するために使う (K 報告のリザルト非表示バグ対応)。</summary>
    public class SongResultFetchDto
    {
        [JsonProperty("song_order")]      public int           SongOrder;
        [JsonProperty("confirmed")]       public bool          Confirmed;       // 両者提出済み
        [JsonProperty("my_submitted")]    public bool          MySubmitted;
        [JsonProperty("their_submitted")] public bool          TheirSubmitted;
        [JsonProperty("song_result")]     public SongResultDto SongResult;      // confirmed=true のとき
        [JsonProperty("my_scores")]       public int[]         MyScores;
    }

    public class SectorResultDto
    {
        [JsonProperty("score_a")]     public int ScoreA;
        [JsonProperty("score_b")]     public int ScoreB;
        [JsonProperty("points_a")]    public int PointsA;
        [JsonProperty("points_b")]    public int PointsB;
        [JsonProperty("tie_break_a")] public int TieBreakA;
        [JsonProperty("tie_break_b")] public int TieBreakB;
    }

    public class MatchResultDto
    {
        [JsonProperty("match_id")]         public string MatchId;
        [JsonProperty("outcome_kind")]     public string OutcomeKind;   // "win_a" | "win_b" | "draw"
        [JsonProperty("total_points_a")]   public int    TotalPointsA;  // ミリポイント (最大15000)
        [JsonProperty("total_points_b")]   public int    TotalPointsB;
        [JsonProperty("rating_a_before")]  public double RatingABefore;
        [JsonProperty("rating_a_after")]   public double RatingAAfter;
        [JsonProperty("rating_change_a")]  public double RatingChangeA;
        [JsonProperty("rating_b_before")]  public double RatingBBefore;
        [JsonProperty("rating_b_after")]   public double RatingBAfter;
        [JsonProperty("rating_change_b")]  public double RatingChangeB;
        [JsonProperty("forfeit")]          public bool   Forfeit;
        [JsonProperty("forfeit_reason")]   public string ForfeitReason;
        [JsonProperty("forfeited_player")] public string ForfeitedPlayer;
    }

    // ── Phase 7: ロビー統計 / 対戦履歴 (docs/phase7_api_dto_spec.md) ─────────────

    /// <summary>GET /users/me/stats — ロビー戦績サマリ (全期間集計)。</summary>
    public class UserStatsDto
    {
        [JsonProperty("user_id")]          public string UserId;
        [JsonProperty("display_name")]     public string DisplayName;
        [JsonProperty("rating")]           public double Rating;
        [JsonProperty("rating_deviation")] public double RatingDeviation;
        [JsonProperty("total_matches")]    public int    TotalMatches;
        [JsonProperty("wins")]             public int    Wins;
        [JsonProperty("losses")]           public int    Losses;
        [JsonProperty("draws")]            public int    Draws;
        [JsonProperty("win_rate")]         public double WinRate;      // 0.0〜1.0
        [JsonProperty("best_rating")]      public double BestRating;
    }

    /// <summary>GET /users/me/matches — PVP 対戦履歴 (新しい順, ページング)。</summary>
    public class UserMatchesDto
    {
        [JsonProperty("matches")] public System.Collections.Generic.List<UserMatchDto> Matches;
        [JsonProperty("total")]   public int Total;
    }

    /// <summary>対戦履歴 1 件 (自分視点に正規化済み)。</summary>
    public class UserMatchDto
    {
        [JsonProperty("match_id")]        public string MatchId;
        [JsonProperty("played_at")]       public string PlayedAt;       // ISO8601 UTC
        [JsonProperty("opponent")]        public MatchOpponentDto Opponent;
        [JsonProperty("outcome")]         public string Outcome;        // "win" | "lose" | "draw"
        [JsonProperty("outcome_kind")]    public string OutcomeKind;    // "win_a" | "win_b" | "draw"
        [JsonProperty("forfeit")]         public bool   Forfeit;
        [JsonProperty("my_points")]       public int    MyPoints;       // ミリポイント (最大15000)
        [JsonProperty("opponent_points")] public int    OpponentPoints;
        [JsonProperty("rating_before")]   public double RatingBefore;
        [JsonProperty("rating_after")]    public double RatingAfter;
        [JsonProperty("rating_delta")]    public double RatingDelta;
        [JsonProperty("songs")]           public System.Collections.Generic.List<UserMatchSongDto> Songs;
    }

    public class MatchOpponentDto
    {
        [JsonProperty("user_id")]      public string UserId;
        [JsonProperty("display_name")] public string DisplayName;
    }

    /// <summary>対戦履歴の曲別簡易結果 (曲スコアは5セクター合計, 0〜1,000,000)。</summary>
    public class UserMatchSongDto
    {
        [JsonProperty("song_order")]     public int    SongOrder;       // 1始まり
        [JsonProperty("song_id")]        public string SongId;
        [JsonProperty("difficulty")]     public string Difficulty;
        [JsonProperty("my_score")]       public int    MyScore;
        [JsonProperty("opponent_score")] public int    OpponentScore;
    }
}
