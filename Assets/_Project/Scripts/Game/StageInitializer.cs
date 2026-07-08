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

        // カメラ視点(0°=フラット/32°=傾斜)をプレイ用カメラへ適用(ライブ/リプレイ共通)。
        // メニュー等のカメラは触らず、ゲームプレイの Camera.main のみ対象。
        ApplyCameraAngle(Camera.main, PlayerPrefs.GetInt("CameraAngleIdx", DefaultCameraAngleIdx));

        EnsureBeatLines(conductor, chart, scroller);

        // レーン(背景・帯・区切り線)の明るさを PLAY OPTIONS の設定値で減光。ノーツ・拍線は対象外。
        LaneBrightness.Apply();
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

    // ── カメラ視点(2D/3D プリセット) ──────────────────────────────────────────
    // ノーツは床面(y=0)を Z=+大(奥)→ JudgmentLineZ(手前)へ流れる薄板。
    //   index 0 = "2D": 真上からの正射影(トップダウン)。DJMAX 風に、レーンは画面中央の縦帯・ノーツは
    //                    真下へ落下・判定ラインは画面下寄り。遠近ゼロでレーン幅一定・平行。
    //   index 1 = "3D": 透視投影の斜め見下ろしハイウェイ(現行の GamePlay シーン授権カメラと一致)。
    //   3D 授権値: FocusZ=2.84 / Distance=6.86 / pitch=40° / Fov=70。
    const float             Camera3DPitchDeg = 40f;
    const float             CameraFocusZ     = 2.84f;
    const float             CameraDistance   = 6.86f;
    const float             CameraFov        = 70f;
    // 2D(トップダウン正射影)の調整値。
    //   OrthoSize   = 正射影の半縦幅(Z方向)。小さいほど寄り(ノーツ大・中央帯は広く)。中央帯の横幅は
    //                 レーン全幅 6.4 / (2*OrthoSize*アスペクト) で決まる(16:9・5.5 で画面幅の約33%)。
    //   JudgeFromBottom = 判定ラインを画面下から何割の位置に置くか(0.22 = 下から22%)。
    //   CamHeight   = 真上からの高さ。正射影なのでスケールには無関係(クリップ内であれば任意)。
    const float             TwoDOrthoSize       = 5.5f;
    const float             TwoDJudgeFromBottom = 0.22f;
    const float             TwoDCamHeight       = 12f;
    /// <summary>カメラ視点設定の既定インデックス(1 = 3D = 従来の斜め見下ろし表示)。</summary>
    public const int        DefaultCameraAngleIdx = 1;

    /// <summary>現在のプレイ視点が 2D(トップダウン)か。NoteController が 2D 時にタップの厚みを増やす判定に使う。</summary>
    public static bool      Is2DView { get; private set; }

    /// <summary>
    /// プレイ用カメラの視点を設定インデックスに合わせて絶対値で設定する(冪等)。
    /// idx 0 = 2D(トップダウン正射影)/ idx 1 = 3D(透視投影の斜め見下ろし)。範囲外は 3D にクランプ。
    /// cam が null なら何もしない。
    /// </summary>
    public static void ApplyCameraAngle(Camera cam, int idx)
    {
        if (cam == null) return;
        Is2DView = (idx == 0);

        if (idx == 0)
        {
            // ── 2D: 真上から見下ろす正射影 ──────────────────────────────────
            // 画面の上=奥(+Z)、下=手前(-Z)。ノーツは +Z から流れてきて下へ落下し、判定ラインへ。
            // 判定ライン(JudgmentLineZ)が画面下から JudgeFromBottom の位置に来るよう中心 Z を決める。
            float s       = TwoDOrthoSize;
            float centerZ = LaneLayout.JudgmentLineZ + s * (1f - 2f * TwoDJudgeFromBottom);
            cam.orthographic     = true;
            cam.orthographicSize = s;
            cam.transform.position = new Vector3(0f, TwoDCamHeight, centerZ);   // レーンは X=0 中心=画面中央
            cam.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward); // 真下向き・画面上=+Z
        }
        else
        {
            // ── 3D: 斜め見下ろしの透視投影(現行) ───────────────────────────
            float r     = Camera3DPitchDeg * Mathf.Deg2Rad;
            var   focus = new Vector3(0f, 0f, CameraFocusZ);
            cam.orthographic = false;
            cam.fieldOfView  = CameraFov;
            cam.transform.position = focus + new Vector3(0f, CameraDistance * Mathf.Sin(r), -CameraDistance * Mathf.Cos(r));
            cam.transform.rotation = Quaternion.Euler(Camera3DPitchDeg, 0f, 0f);
        }
    }

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
