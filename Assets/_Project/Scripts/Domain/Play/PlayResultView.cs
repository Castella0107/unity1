// Unity-independent. No UnityEngine references allowed in this assembly.
// Display-only view: wraps PlayRecord and adds song-master / best-score context.
/// <summary>
/// リザルト画面表示用のビューモデル。<see cref="PlayRecord"/> をラップし、
/// 楽曲メタデータとベストスコア比較情報を付加する。
/// </summary>
public sealed class PlayResultView
{
    /// <summary>ラップ対象のプレイ記録。</summary>
    public PlayRecord Record                  { get; set; }

    /// <summary>曲タイトル(楽曲メタ由来、PlayRecord には保存されない)。</summary>
    public string SongTitle                   { get; set; }
    /// <summary>アーティスト名(楽曲メタ由来)。</summary>
    public string SongArtist                  { get; set; }
    /// <summary>難易度レベル(楽曲メタ由来)。</summary>
    public int    Level                       { get; set; }

    /// <summary>今回プレイ前のベスト実効スコア。</summary>
    public int    BestEffectiveScoreBefore    { get; set; }
    /// <summary>今回がベスト更新か。</summary>
    public bool   IsNewBest                   { get; set; }

    /// <summary>楽曲ID(Record へ委譲)。</summary>
    public string SongId          => Record.SongId;
    /// <summary>難易度(Record へ委譲)。</summary>
    public string Difficulty      => Record.Difficulty;
    /// <summary>実効スコア(Record へ委譲)。</summary>
    public int    EffectiveScore  => Record.EffectiveScore;
    /// <summary>ランク(Record へ委譲)。</summary>
    public string Rank            => Record.Rank;
    /// <summary>PvP プレイか(Record へ委譲)。</summary>
    public bool   IsPvP           => Record.IsPvP;
    /// <summary>フルコンボか(Record へ委譲)。</summary>
    public bool   IsFullCombo     => Record.IsFullCombo;
    /// <summary>オールパーフェクトか(Record へ委譲)。</summary>
    public bool   IsAllPerfect    => Record.IsAllPerfect;
    /// <summary>オール PerfectPlus か(Record へ委譲)。</summary>
    public bool   IsAllPerfectPlus => Record.IsAllPerfectPlus;
}
