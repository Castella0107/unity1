// Unity-independent. No UnityEngine references allowed in this assembly.
/// <summary>
/// PVP 1試合分のローカル履歴記録（自分視点）。直近 N 戦の対戦結果と
/// 自分の 3 曲分リプレイへの参照を保持する。スコア・レーティングはすべて
/// 自分(Self)／相手(Opponent)の視点に正規化済みで、A/B の区別は保存時に解決される。
/// </summary>
public sealed class PvpMatchRecord
{
    /// <summary>マッチID(主キー)。</summary>
    public string   MatchId               { get; set; }
    /// <summary>自分のユーザーID。</summary>
    public string   SelfUserId            { get; set; }
    /// <summary>対戦相手のユーザーID。</summary>
    public string   OpponentId            { get; set; }

    /// <summary>自分視点の勝敗 (0=引分け / 1=勝ち / 2=負け)。</summary>
    public int      ResultKind            { get; set; }

    /// <summary>自分の合計ポイント(難易度倍率適用済み)。</summary>
    public double   SelfPoints            { get; set; }
    /// <summary>相手の合計ポイント(難易度倍率適用済み)。</summary>
    public double   OpponentPoints        { get; set; }

    /// <summary>試合前の自分のレーティング。</summary>
    public double   SelfRatingBefore      { get; set; }
    /// <summary>試合後の自分のレーティング。</summary>
    public double   SelfRatingAfter       { get; set; }
    /// <summary>試合前の相手のレーティング。</summary>
    public double   OpponentRatingBefore  { get; set; }
    /// <summary>試合後の相手のレーティング。</summary>
    public double   OpponentRatingAfter   { get; set; }

    /// <summary>この試合の楽曲ID(通常3曲)。</summary>
    public string[] SongIds               { get; set; }
    /// <summary>各曲の難易度(SongIds と同順)。</summary>
    public string[] Difficulties          { get; set; }

    /// <summary>自分のセクター別スコア(3曲×5=15要素)。</summary>
    public int[]    SelfSectorScores      { get; set; }
    /// <summary>相手のセクター別スコア(3曲×5=15要素、スコア詳細のみ・リプレイ無し)。</summary>
    public int[]    OpponentSectorScores  { get; set; }

    /// <summary>自分の各曲リプレイファイルのパス(自分の3曲分のみ保存)。</summary>
    public string[] SelfReplayPaths       { get; set; }

    /// <summary>試合確定日時(Unix エポックからのミリ秒)。リングバッファの並び替えキー。</summary>
    public long     CompletedAtUnixMs     { get; set; }
}
