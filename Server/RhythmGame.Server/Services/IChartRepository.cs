using System.Threading.Tasks;

namespace RhythmGame.Server.Services
{
    /// <summary>
    /// Chart hash (SHA-256 hex) → (ChartData, SongMetadata) を解決するリポジトリ。
    /// 譜面はサーバー側に予め登録されている前提。
    /// </summary>
    public interface IChartRepository
    {
        /// <summary>
        /// ChartHash から譜面データとメタデータを取得。
        /// 該当なしの場合は (null, null) を返す。
        /// </summary>
        Task<(ChartData chart, SongMetadata meta)?> TryGetByHashAsync(string chartHashHex);
    }
}
