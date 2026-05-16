// Unity-independent. No UnityEngine references allowed in this assembly.

/// <summary>
/// シーン遷移時に次シーンへ渡すパラメータの基底マーカーインターフェース。
/// </summary>
public interface ISceneParameters { }

/// <summary>
/// パラメータなしシーン遷移で使用する空実装。シングルトンインスタンスを提供する。
/// </summary>
public sealed class EmptyParameters : ISceneParameters
{
    public static readonly EmptyParameters Instance = new EmptyParameters();
}

/// <summary>
/// ゲームプレイシーンへ渡す楽曲・難易度・オフセット・モディファイア・リプレイ設定などのパラメータ。
/// </summary>
public sealed class GamePlayParameters : ISceneParameters
{
    public string SongId       { get; set; }
    public string Difficulty   { get; set; }   // easy/normal/hard/extra
    public float  HiSpeed      { get; set; }
    public int    JudgeOffset  { get; set; }
    public int    VisualOffset { get; set; }
    public string Modifier     { get; set; }   // None/Mirror/Random

    // Replay mode
    public bool   IsReplay            { get; set; }
    public string ReplayPath          { get; set; }
    public double InitialPlaybackSpeed { get; set; } = 1.0;
}

/// <summary>
/// リザルトシーンへ渡すパラメータ。プレイ結果ビューと元の GamePlay パラメータを保持し、
/// リトライ時に同一設定で再開できるようにする。
/// </summary>
public sealed class ResultParameters : ISceneParameters
{
    public PlayResultView     View                    { get; set; }

    /// The original parameters used to start this GamePlay session.
    /// Stored here so ResultController can retry with the exact same settings.
    public GamePlayParameters SourceGamePlayParameters { get; set; }
}
