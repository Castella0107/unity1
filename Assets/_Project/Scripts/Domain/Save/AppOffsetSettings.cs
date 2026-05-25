// Unity-independent. No UnityEngine references allowed in this assembly.
/// <summary>
/// 判定オフセット・映像オフセットをミリ秒単位で保持するアプリ全体のオフセット設定。
/// デバイスプロファイルに紐付いて管理され、±200 ms の範囲にクランプできる。
/// </summary>
public sealed class AppOffsetSettings
{
    /// <summary>判定オフセット(ms)。</summary>
    public int JudgmentOffsetMs { get; set; }
    /// <summary>映像オフセット(ms)。</summary>
    public int VisualOffsetMs   { get; set; }

    /// <summary>オフセットの下限(ms)。</summary>
    public const int MinMs = -200;
    /// <summary>オフセットの上限(ms)。</summary>
    public const int MaxMs = +200;

    /// <summary>既定設定(両オフセット 0)。</summary>
    public static AppOffsetSettings Default => new AppOffsetSettings
    {
        JudgmentOffsetMs = 0,
        VisualOffsetMs   = 0,
    };

    /// <summary>両オフセットを ±200ms にクランプした新インスタンスを返す。</summary>
    public AppOffsetSettings Clamped() => new AppOffsetSettings
    {
        JudgmentOffsetMs = System.Math.Max(MinMs, System.Math.Min(MaxMs, JudgmentOffsetMs)),
        VisualOffsetMs   = System.Math.Max(MinMs, System.Math.Min(MaxMs, VisualOffsetMs)),
    };
}
