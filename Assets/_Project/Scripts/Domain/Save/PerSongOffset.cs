// Unity-independent. No UnityEngine references allowed in this assembly.
/// <summary>
/// 楽曲ごとの判定オフセット設定（±50 ms の範囲）。
/// アプリ全体オフセットとは独立した楽曲単位の微調整用データ。
/// </summary>
public sealed class PerSongOffset
{
    /// <summary>対象楽曲ID。</summary>
    public string SongId           { get; set; }
    /// <summary>この楽曲固有の判定オフセット(ms)。</summary>
    public int    JudgmentOffsetMs { get; set; }
    /// <summary>更新日時(Unix エポックからのミリ秒)。</summary>
    public long   UpdatedAtUnixMs  { get; set; }

    /// <summary>曲別調整の下限(ms)。意図的に狭め(±50ms)。</summary>
    public const int MinMs = -50;
    /// <summary>曲別調整の上限(ms)。</summary>
    public const int MaxMs = +50;

    /// <summary>指定楽曲の既定オフセット(0)を生成する。</summary>
    public static PerSongOffset DefaultFor(string songId) => new PerSongOffset
    {
        SongId           = songId,
        JudgmentOffsetMs = 0,
        UpdatedAtUnixMs  = 0,
    };

    /// <summary>オフセットを ±50ms にクランプした新インスタンスを返す。</summary>
    public PerSongOffset Clamped() => new PerSongOffset
    {
        SongId           = SongId,
        JudgmentOffsetMs = System.Math.Max(MinMs, System.Math.Min(MaxMs, JudgmentOffsetMs)),
        UpdatedAtUnixMs  = UpdatedAtUnixMs,
    };
}
