using Microsoft.EntityFrameworkCore;

namespace RhythmGame.Server.Data
{
    /// <summary>
    /// サーバー側 SQLite データベース。匿名スコア + (Phase 4-B 以降) ユーザー / リプレイ。
    /// </summary>
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<PlayRecordEntity> PlayRecords => Set<PlayRecordEntity>();
        public DbSet<UserEntity>       Users       => Set<UserEntity>();
        public DbSet<MatchEntity>      Matches     => Set<MatchEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            var e = modelBuilder.Entity<PlayRecordEntity>();
            e.HasKey(x => x.PlayId);
            e.Property(x => x.PlayId).HasMaxLength(64);
            e.Property(x => x.SongId).HasMaxLength(128).IsRequired();
            e.Property(x => x.Difficulty).HasMaxLength(32).IsRequired();
            e.Property(x => x.UserId).HasMaxLength(64);
            e.Property(x => x.Rank).HasMaxLength(8);
            e.Property(x => x.ChartHash).HasMaxLength(64);

            // Leaderboard クエリ最適化 (song + difficulty + score 降順)
            e.HasIndex(x => new { x.SongId, x.Difficulty, x.EffectiveScore });
            // 最近のプレイ取得
            e.HasIndex(x => x.PlayedAtUnixMs);

            var u = modelBuilder.Entity<UserEntity>();
            u.HasKey(x => x.UserId);
            u.Property(x => x.UserId).HasMaxLength(64);
            u.Property(x => x.DisplayName).HasMaxLength(64);
            u.HasIndex(x => x.LastSeenUnixMs);
            u.HasIndex(x => x.Rating);

            var m = modelBuilder.Entity<MatchEntity>();
            m.HasKey(x => x.MatchId);
            m.Property(x => x.MatchId).HasMaxLength(64);
            m.Property(x => x.UserIdA).HasMaxLength(64).IsRequired();
            m.Property(x => x.UserIdB).HasMaxLength(64).IsRequired();
            m.HasIndex(x => new { x.UserIdA, x.CompletedAtUnixMs });
            m.HasIndex(x => new { x.UserIdB, x.CompletedAtUnixMs });
        }
    }

    /// <summary>
    /// 認証なし簡易ユーザー。PlayRecord 送信時に UserId で UPSERT 自動作成。
    /// Phase 4-B OAuth では認証情報 (Provider/Subject) を追加カラムで持つ。
    /// </summary>
    public class UserEntity
    {
        public string UserId           { get; set; } = "";  // クライアント PlayerPrefs DisplayName と一致
        public string DisplayName      { get; set; } = "";
        public long   FirstSeenUnixMs  { get; set; }
        public long   LastSeenUnixMs   { get; set; }
        public int    TotalPlays       { get; set; }

        // Glicko-2 (Phase 5-β)
        public double Rating             { get; set; } = 1500.0;
        public double RatingDeviation    { get; set; } = 350.0;
        public double Volatility         { get; set; } = 0.06;
        public long   LastRatedAtUnixMs  { get; set; }
        public int    TotalPvpMatches    { get; set; }
        public int    PvpWins            { get; set; }
        public int    PvpLosses          { get; set; }
        public int    PvpDraws           { get; set; }
    }

    /// <summary>
    /// 完了した PVP 試合 1 件。確定後のレーティング差分も保存する。
    /// SongIds と SectorScores* は CSV 文字列で永続化 (シンプル & SQLite 配列非サポート)。
    /// </summary>
    public class MatchEntity
    {
        public string MatchId          { get; set; } = "";
        public string UserIdA          { get; set; } = "";
        public string UserIdB          { get; set; } = "";
        public string SongIdsCsv       { get; set; } = "";   // "song1,song2,song3"
        public string DifficultiesCsv  { get; set; } = "";
        public string SectorScoresA    { get; set; } = "";   // "1234,5678,..." 15 個
        public string SectorScoresB    { get; set; } = "";
        public int    TotalPointsAx10  { get; set; }         // 1.5pt → 15 (×10 で整数化)
        public int    TotalPointsBx10  { get; set; }
        public int    OutcomeKind      { get; set; }         // 0=Draw, 1=AWins, 2=BWins
        public long   CreatedAtUnixMs  { get; set; }
        public long   CompletedAtUnixMs{ get; set; }

        public double RatingABefore    { get; set; }
        public double RatingAAfter     { get; set; }
        public double RatingBBefore    { get; set; }
        public double RatingBAfter     { get; set; }
    }

    /// <summary>
    /// クライアントから送信されサーバーで検証済みのプレイ記録。
    /// Domain.PlayRecord と概ね同一フィールドだが、サーバー側都合で UserId / SubmittedAt を持つ。
    /// </summary>
    public class PlayRecordEntity
    {
        public string PlayId         { get; set; } = "";
        public string UserId         { get; set; } = "anon";
        public string SongId         { get; set; } = "";
        public string Difficulty     { get; set; } = "";
        public long   PlayedAtUnixMs { get; set; }
        public long   SubmittedAtUnixMs { get; set; }

        public int    RawScore       { get; set; }
        public int    EffectiveScore { get; set; }
        public string Rank           { get; set; } = "";

        public int    PerfectPlus    { get; set; }
        public int    Perfect        { get; set; }
        public int    Great          { get; set; }
        public int    Good           { get; set; }
        public int    Miss           { get; set; }
        public int    MaxCombo       { get; set; }
        public int    TotalNotes     { get; set; }

        public string ChartHash      { get; set; } = "";
        public bool   IsFullCombo    { get; set; }
        public bool   IsAllPerfect   { get; set; }
        public bool   IsAllPerfectPlus { get; set; }
    }
}
