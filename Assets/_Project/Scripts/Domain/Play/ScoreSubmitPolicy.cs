// Unity-independent. Part of Domain assembly.

/// <summary>
/// リーダーボードへのソロスコア提出可否の判定 (docs/design_doc/leaderboard_client.md §3)。
/// ScoreSubmitService から呼ばれる純関数 (EditMode テスト対象)。
/// </summary>
public static class ScoreSubmitPolicy
{
    /// <summary>
    /// 提出条件: ソロ (非 PVP)・非オートプレイ・ログイン済み・サーバー配信譜面 (chart_id あり)・
    /// リプレイ保存済み、のすべてを満たすときのみ true。
    /// </summary>
    public static bool ShouldSubmit(bool isPvp, bool isAutoPlay, bool loggedIn, bool hasChartId, bool hasReplayPath)
        => !isPvp && !isAutoPlay && loggedIn && hasChartId && hasReplayPath;
}
