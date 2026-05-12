// Unity-independent. No UnityEngine references allowed in this assembly.

public interface ISceneParameters { }

public sealed class EmptyParameters : ISceneParameters
{
    public static readonly EmptyParameters Instance = new EmptyParameters();
}

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

public sealed class ResultParameters : ISceneParameters
{
    public PlayResultView     View                    { get; set; }

    /// The original parameters used to start this GamePlay session.
    /// Stored here so ResultController can retry with the exact same settings.
    public GamePlayParameters SourceGamePlayParameters { get; set; }
}
