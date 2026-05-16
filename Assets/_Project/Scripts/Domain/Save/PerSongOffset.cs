// Unity-independent. No UnityEngine references allowed in this assembly.
/// <summary>
/// 楽曲ごとの判定オフセット設定（±50 ms の範囲）。
/// アプリ全体オフセットとは独立した楽曲単位の微調整用データ。
/// </summary>
public sealed class PerSongOffset
{
    public string SongId           { get; set; }
    public int    JudgmentOffsetMs { get; set; }
    public long   UpdatedAtUnixMs  { get; set; }

    // Per-song adjustment is intentionally narrow (±50 ms)
    public const int MinMs = -50;
    public const int MaxMs = +50;

    public static PerSongOffset DefaultFor(string songId) => new PerSongOffset
    {
        SongId           = songId,
        JudgmentOffsetMs = 0,
        UpdatedAtUnixMs  = 0,
    };

    public PerSongOffset Clamped() => new PerSongOffset
    {
        SongId           = SongId,
        JudgmentOffsetMs = System.Math.Max(MinMs, System.Math.Min(MaxMs, JudgmentOffsetMs)),
        UpdatedAtUnixMs  = UpdatedAtUnixMs,
    };
}
