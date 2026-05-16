// Unity-independent. No UnityEngine references allowed in this assembly.
/// <summary>
/// 判定オフセット・映像オフセットをミリ秒単位で保持するアプリ全体のオフセット設定。
/// デバイスプロファイルに紐付いて管理され、±200 ms の範囲にクランプできる。
/// </summary>
public sealed class AppOffsetSettings
{
    public int JudgmentOffsetMs { get; set; }
    public int VisualOffsetMs   { get; set; }

    public const int MinMs = -200;
    public const int MaxMs = +200;

    public static AppOffsetSettings Default => new AppOffsetSettings
    {
        JudgmentOffsetMs = 0,
        VisualOffsetMs   = 0,
    };

    public AppOffsetSettings Clamped() => new AppOffsetSettings
    {
        JudgmentOffsetMs = System.Math.Max(MinMs, System.Math.Min(MaxMs, JudgmentOffsetMs)),
        VisualOffsetMs   = System.Math.Max(MinMs, System.Math.Min(MaxMs, VisualOffsetMs)),
    };
}
