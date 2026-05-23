using Microsoft.EntityFrameworkCore;
using RhythmGame.Server.Data;
using RhythmGame.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// EF Core (SQLite)
var connStr = builder.Configuration.GetConnectionString("AppDb") ?? "Data Source=rhythmgame.db";
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlite(connStr));

// gRPC
builder.Services.AddGrpc();

// gRPC-Web (Unity / Browser fallback over HTTP/1.1)
// Note: GrpcWeb middleware is provided by Microsoft.AspNetCore.Grpc.Web (auto-registered when UseGrpcWeb is called).

// REST controllers (Unity client JSON 経路)
builder.Services.AddControllers();

// Health checks (REST GET /health)
builder.Services.AddHealthChecks();

// Shared replay validation core (used by gRPC / REST / PVP)
builder.Services.AddScoped<ReplayValidationCore>();

// Active PVP match store + matchmaking queue (in-memory)
builder.Services.AddSingleton<ActiveMatchStore>();
builder.Services.AddSingleton<MatchmakingQueueService>();

// Chart repository (FileSystem)
builder.Services.AddSingleton<IChartRepository>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<FileSystemChartRepository>>();
    var songsPath = Path.Combine(
        builder.Environment.ContentRootPath,
        "..",
        "..",
        "Assets",
        "StreamingAssets",
        "Songs");
    songsPath = Path.GetFullPath(songsPath);
    return new FileSystemChartRepository(songsPath, logger);
});


var app = builder.Build();

// gRPC-Web pipeline (must be before MapGrpcService)
app.UseRouting();
app.UseGrpcWeb(new GrpcWebOptions { DefaultEnabled = true });

// gRPC services (HTTP/2 + gRPC-Web)
app.MapGrpcService<GreeterService>().EnableGrpcWeb();
app.MapGrpcService<ReplayValidationService>().EnableGrpcWeb();

// REST endpoints
app.MapControllers();
app.MapHealthChecks("/health");
app.MapGet("/", () => "RhythmGame.Server — gRPC + gRPC-Web + REST ready. See /health for status.");

// 起動時に IChartRepository を強制初期化 (譜面インデックス構築 + ログ出力)
app.Services.GetRequiredService<IChartRepository>();

// DB 自動作成 (EnsureCreated; 本番では Migrate に切り替える)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    var dbLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    dbLogger.LogInformation("[Startup] AppDb ready: {ConnStr}", connStr);
}

// 起動ログ
var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
startupLogger.LogInformation("[Startup] RhythmGame.Server listening on: {Urls}",
    string.Join(", ", app.Urls.Count > 0 ? app.Urls : new[] { "(see launchSettings.json)" }));

app.Run();
