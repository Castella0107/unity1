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

        EnsureBeatLines(conductor, chart, scroller);
    }

    /// <summary>拍線・小節線スクローラーを生成(または再利用)し、譜面で初期化する。</summary>
    static void EnsureBeatLines(AudioConductor conductor, ChartData chart, NoteScroller scroller)
    {
        var beatLines = BeatLineScroller.Instance;
        if (beatLines == null)
        {
            // ノートと完全に同じ空間に置く: ノートの親(NotePool)を最優先、無ければレーンステージ、原点。
            Transform parent = null;
            var pool = Object.FindObjectOfType<NotePool>();
            if (pool != null) parent = pool.transform;
            else { var lv = Object.FindObjectOfType<LaneVisuals>(); if (lv != null) parent = lv.transform; }

            var go = new GameObject("BeatLineScroller");
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale    = Vector3.one;
            beatLines = go.AddComponent<BeatLineScroller>();
        }
        beatLines.Initialize(conductor, chart, scroller);
    }

    /// <summary>HiSpeed を解決する。未指定(0以下)なら PlayerPrefs の保存値(既定4.5)。</summary>
    public static float ResolveHiSpeed(float hiSpeed)
        => hiSpeed > 0.01f ? hiSpeed : PlayerPrefs.GetFloat("HiSpeed", 4.5f);

    /// <summary>セッション終了時にステージビジュアルのバインドを解除する。</summary>
    public static void UnbindStageVisuals()
    {
        BeatGridController.Instance?.Unbind();
        BeatLineScroller.Instance?.Clear();
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
