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
    /// <summary>共有シングルトンインスタンス。</summary>
    public static readonly EmptyParameters Instance = new EmptyParameters();
}

/// <summary>
/// ゲームプレイシーンへ渡す楽曲・難易度・オフセット・モディファイア・リプレイ設定などのパラメータ。
/// </summary>
public sealed class GamePlayParameters : ISceneParameters
{
    /// <summary>プレイする楽曲ID。</summary>
    public string SongId       { get; set; }
    /// <summary>難易度 (easy/normal/hard/extra)。</summary>
    public string Difficulty   { get; set; }
    /// <summary>ハイスピード倍率。</summary>
    public float  HiSpeed      { get; set; }
    /// <summary>判定オフセット(ms)。</summary>
    public int    JudgeOffset  { get; set; }
    /// <summary>表示オフセット(ms)。</summary>
    public int    VisualOffset { get; set; }
    /// <summary>モディファイア (None/Mirror/Random)。</summary>
    public string Modifier     { get; set; }

    /// <summary>リプレイ再生モードか。</summary>
    public bool   IsReplay            { get; set; }
    /// <summary>リプレイファイルのパス。</summary>
    public string ReplayPath          { get; set; }
    /// <summary>リプレイの初期再生速度。</summary>
    public double InitialPlaybackSpeed { get; set; } = 1.0;

    /// <summary>PVP マッチでのプレイか。</summary>
    public bool   IsPvp           { get; set; }
    /// <summary>PVP マッチID。</summary>
    public string PvpMatchId      { get; set; }
    /// <summary>PVP の曲インデックス(0〜2、1試合3曲)。</summary>
    public int    PvpSongIndex    { get; set; }
    /// <summary>PVP 対戦相手のユーザーID。</summary>
    public string PvpOpponentId   { get; set; }
}

/// <summary>PVP マッチ確定後の最終結果シーンへ渡すパラメータ。</summary>
public sealed class PvpMatchEndParameters : ISceneParameters
{
    /// <summary>マッチID。</summary>
    public string MatchId        { get; set; }
    /// <summary>Player A のユーザーID。</summary>
    public string UserIdA        { get; set; }
    /// <summary>Player B のユーザーID。</summary>
    public string UserIdB        { get; set; }
    /// <summary>自分が A か B かを区別する自ユーザーID。</summary>
    public string SelfUserId     { get; set; }
    /// <summary>Player A の合計ポイント。</summary>
    public double TotalPointsA   { get; set; }
    /// <summary>Player B の合計ポイント。</summary>
    public double TotalPointsB   { get; set; }
    /// <summary>勝敗種別 (0=Draw, 1=AWins, 2=BWins)。</summary>
    public int    OutcomeKind    { get; set; }
    /// <summary>試合前の Player A レーティング。</summary>
    public double RatingABefore  { get; set; }
    /// <summary>試合後の Player A レーティング。</summary>
    public double RatingAAfter   { get; set; }
    /// <summary>試合前の Player B レーティング。</summary>
    public double RatingBBefore  { get; set; }
    /// <summary>試合後の Player B レーティング。</summary>
    public double RatingBAfter   { get; set; }
    /// <summary>エラー時のフォールバックメッセージ(試合 abandoned 等)。</summary>
    public string ErrorMessage   { get; set; }
    /// <summary>曲別の難易度・獲得ポイント内訳(3曲)。難易度倍率の効きを試合終了画面に見せるために使う。</summary>
    public System.Collections.Generic.List<PvpSongLine> Songs { get; set; }
}

/// <summary>PVP 試合終了画面の曲別内訳1行。ポイントは難易度倍率適用済み (MatchScoring.Score 由来)。</summary>
public sealed class PvpSongLine
{
    /// <summary>楽曲ID。</summary>
    public string SongId     { get; set; }
    /// <summary>難易度("easy"/"normal"/"hard"/"extra")。</summary>
    public string Difficulty { get; set; }
    /// <summary>この曲での Player A の獲得ポイント(難易度倍率適用済み)。</summary>
    public double PointsA     { get; set; }
    /// <summary>この曲での Player B の獲得ポイント(難易度倍率適用済み)。</summary>
    public double PointsB     { get; set; }
}

/// <summary>
/// リザルトシーンへ渡すパラメータ。プレイ結果ビューと元の GamePlay パラメータを保持し、
/// リトライ時に同一設定で再開できるようにする。
/// </summary>
public sealed class ResultParameters : ISceneParameters
{
    /// <summary>表示するプレイ結果ビュー。</summary>
    public PlayResultView     View                    { get; set; }

    /// <summary>この GamePlay を開始した元パラメータ。リトライ時に同一設定で再開するため保持する。</summary>
    public GamePlayParameters SourceGamePlayParameters { get; set; }
}
