using System.Threading.Tasks;

namespace RhythmGame.Network.Api
{
    /// <summary>
    /// リーダーボード API クライアント (docs/design_doc/leaderboard_client.md §2)。
    /// サーバー側仕様: pvpharmonics-server/docs/leaderboard_design.md (phase7_api_dto_spec §2-a/2-b)。
    /// </summary>
    public static class LeaderboardApi
    {
        /// <summary>楽曲・難易度別のスコアランキングを取得する (既定: アクティブシーズン)。</summary>
        public static Task<ApiResult<LeaderboardDto>> GetLeaderboardAsync(
            string songId, string difficulty, int limit = 20, int offset = 0)
            => ApiClient.GetAsync<LeaderboardDto>(
                $"/leaderboard/{songId}/{difficulty}?limit={limit}&offset={offset}");

        /// <summary>自分のベストスコアと全体順位を取得する (未提出なら personal_best=null)。</summary>
        public static Task<ApiResult<PersonalBestFetchDto>> GetPersonalBestAsync(
            string songId, string difficulty)
            => ApiClient.GetAsync<PersonalBestFetchDto>(
                $"/leaderboard/{songId}/{difficulty}/personal-best");

        /// <summary>ソロリプレイをサーバー検証に提出する (検証成功時ベストスコアが取り込まれる)。</summary>
        public static Task<ApiResult<ScoreValidateResponseDto>> ValidateScoreAsync(ScoreValidateRequestDto dto)
            => ApiClient.PostAsync<ScoreValidateResponseDto>("/score/validate", dto);
    }
}
