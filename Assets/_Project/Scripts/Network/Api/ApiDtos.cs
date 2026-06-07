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
}
