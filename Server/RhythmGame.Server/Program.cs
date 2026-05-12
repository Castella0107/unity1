using RhythmGame.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddGrpc();

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

// Configure the HTTP request pipeline.
app.MapGrpcService<GreeterService>();
app.MapGrpcService<ReplayValidationService>();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

// 起動時に IChartRepository を強制初期化 (譜面インデックス構築 + ログ出力)
app.Services.GetRequiredService<IChartRepository>();

app.Run();





