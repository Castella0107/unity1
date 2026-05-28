using System.Threading.Tasks;
using UnityEngine;

// Shared visual-stage setup used by both GamePlayController (live) and
// ReplayPlaybackController (replay). Call BindStageVisuals after chart/meta
// are loaded, and UnbindStageVisuals when the session ends.

/// <summary>
/// GamePlayController（ライブプレイ）と ReplayPlaybackController（リプレイ）の両方で共有される
/// ステージビジュアルのセットアップ処理を提供する静的クラス。
/// BindStageVisuals() でビートグリッド・ノートスクローラー・HUD を初期化し、
/// UnbindStageVisuals() でセッション終了時にビートグリッドのバインドを解除する。
/// </summary>
public static class StageInitializer
{
    /// <summary>チャート/メタ読み込み後にステージビジュアル(ノートスクローラー・HUD)を初期化する。</summary>
    /// <param name="hiSpeed">スクロール速度(HiSpeed)。0以下なら PlayerPrefs "HiSpeed"(既定4.5)を使う。</param>
    public static void BindStageVisuals(
        AudioConductor conductor,
        ChartData      chart,
        SongMetadata   meta,
        NoteScroller   scroller,
        GameHud        hud,
        float          hiSpeed = 0f)
    {
        // Hide the persistent jacket-background canvas so it does not occlude
        // the 3D camera output. Must be the first call here.
        JacketBackgroundController.Instance?.SetCanvasEnabled(false);

        // Pulsing beat grid intentionally disabled — static gray background only.

        scroller?.Initialize(chart);
        scroller?.SetScrollSpeed(ResolveHiSpeed(hiSpeed));
        hud?.Initialize(meta, chart, isPvP: false);
    }

    /// <summary>HiSpeed を解決する。未指定(0以下)なら PlayerPrefs の保存値(既定4.5)。</summary>
    public static float ResolveHiSpeed(float hiSpeed)
        => hiSpeed > 0.01f ? hiSpeed : PlayerPrefs.GetFloat("HiSpeed", 4.5f);

    /// <summary>セッション終了時にステージビジュアルのバインドを解除する。</summary>
    public static void UnbindStageVisuals()
    {
        BeatGridController.Instance?.Unbind();
        JacketBackgroundController.Instance?.SetCanvasEnabled(true);
    }

    /// <summary>
    /// 楽曲のオフセットを AudioConductor に適用する。RepositoryService が利用可能ならアクティブプロファイル+曲別オフセットを、
    /// 無ければ SimpleCalibration の保存値(またはフォールバック引数)を使う。
    /// </summary>
    public static async Task ApplyAudioOffsetsAsync(
        AudioConductor conductor,
        string         songId,
        int            fallbackJudgeMs  = 0,
        int            fallbackVisualMs = 0)
    {
        if (conductor == null) return;

        var repo = RepositoryService.Instance;
        if (repo != null && repo.IsReady)
        {
            conductor.ApplyAppOffsets(repo.ActiveProfile.Offsets);
            var perSong = await repo.Offsets.GetPerSongOffsetAsync(songId);
            conductor.ApplyPerSongOffset(perSong);
        }
        else
        {
            conductor.ApplyAppOffsets(new AppOffsetSettings
            {
                JudgmentOffsetMs = fallbackJudgeMs  != 0 ? fallbackJudgeMs  : SimpleCalibration.GetJudgmentOffset(),
                VisualOffsetMs   = fallbackVisualMs != 0 ? fallbackVisualMs : SimpleCalibration.GetVisualOffset(),
            });
        }
    }
}
