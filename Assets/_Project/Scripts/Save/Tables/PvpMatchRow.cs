// Requires sqlite-net-pcl. Enable by adding SQLITE_NET_PCL to Scripting Define Symbols.
#if SQLITE_NET_PCL
using SQLite;

/// <summary>
/// SQLite の pvp_matches テーブルに対応する行クラス。PVP 1試合分の自分視点の対戦結果
/// (勝敗・ポイント・レーティング変動・両者のセクタースコア・自分のリプレイパス)を保持する。
/// 直近 N 戦だけを保持するリングバッファとして CompletedAtUnixMs で並び替える。
/// </summary>
[Table("pvp_matches")]
public class PvpMatchRow
{
    [PrimaryKey] public string MatchId    { get; set; }
    public string SelfUserId              { get; set; }
    public string OpponentId              { get; set; }

    public int    ResultKind              { get; set; }   // 0=Draw, 1=Win, 2=Loss (自分視点)

    public double SelfPoints              { get; set; }
    public double OpponentPoints          { get; set; }

    public double SelfRatingBefore        { get; set; }
    public double SelfRatingAfter         { get; set; }
    public double OpponentRatingBefore    { get; set; }
    public double OpponentRatingAfter     { get; set; }

    // 楽曲ID・難易度はカンマ区切り(値にカンマを含まない)
    public string SongIdsCsv              { get; set; }
    public string DifficultiesCsv         { get; set; }
    // セクタースコア(15要素)はカンマ区切り
    public string SelfSectorScoresCsv     { get; set; }
    public string OpponentSectorScoresCsv { get; set; }
    // リプレイパスはパイプ区切り(Windows ではファイルパスに '|' を含められないため安全)
    public string SelfReplayPathsBar      { get; set; }

    [Indexed] public long CompletedAtUnixMs { get; set; }
}
#endif
