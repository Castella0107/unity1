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

        // FAR_WALL_SPEC: レーン長 (透明遮蔽壁位置) を設定から再読込し、
        // ノーツ用シェーダの壁クリップ z を初期化 (毎フレームの追従は CenterTrackVisuals)。
        FxSectorGeometry.RefreshLaneLength();
        Shader.SetGlobalFloat("_PlayfieldWallZ",
            LaneLayout.JudgmentLineZ + FxSectorGeometry.WallZCenterWorld);

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
    // ノーツは床面(y=0)を +Z→手前へ流れる薄板。判定ライン付近の注視点 FocusZ とカメラ距離 Distance を
    // 固定し、俯角(pitch)から絶対ポーズを算出する(冪等)。視点の本質的な差は「投影方式」:
    //   index 0 = "0° (flat)"  : 正射影(オルソ)。遠近の広がりが消え、レーン幅一定・平行の完全2D。
    //   index 1 = "32° (steep)": 透視投影。奥に収束する3Dハイウェイ(現行の授権カメラと一致)。
    // ※俯角は両者とも 40°(=現行授権値)で揃え、見え方の違いは投影方式だけにしている。
    //   これによりノーツ描画は現行と同一のまま、flat では遠近の広がりだけが消える。
    //   FocusZ=2.84 / Distance=6.86 / pitch=40° / Fov=70 は既存 GamePlay シーンのカメラ授権値。
    //   OrthoSize(flat の正射影の半縦幅)は見た目のズームで、暫定値。近い/遠いと感じたら調整。
    static readonly float[] CameraPitchDeg     = { 40f, 40f };
    static readonly bool[]  CameraOrthographic = { true, false };
    static readonly float[] CameraOrthoSize    = { 5.5f, 5f };
    const float             CameraFocusZ       = 2.84f;
    const float             CameraDistance     = 6.86f;
    const float             CameraFov          = 70f;
    /// <summary>カメラ視点設定の既定インデックス(1 = 32° steep = 従来の3D表示)。</summary>
    public const int        DefaultCameraAngleIdx = 1;

    /// <summary>
    /// プレイ用カメラの視点を設定インデックスに合わせて絶対値で設定する(冪等)。
    /// flat=正射影(完全2D)/steep=透視投影(3D)。範囲外(旧 18°/32° の idx=2 等)は末尾(steep)にクランプ。
    /// cam が null なら何もしない。
    /// </summary>
    public static void ApplyCameraAngle(Camera cam, int idx)
    {
        if (cam == null) return;
        // 画面設計モック(4) リデザイン: カメラは SPEC 射影に数値ソルバで一致させた
        // 授権フレーミング (Tools/Playfield/1) に固定。CAMERA_SPEC (2026-07-30):
        // VP=画面y120 + FieldScale0.76 (遠景化 — 判定線距離 5.174→5.834、FOV は不変)。
        // プレイフィールドはスクリーン仕様駆動のデカール (CenterTrackVisuals/FxLaneVisuals) が
        // カメラへ追従再投影するため、旧 2D(flat)/3D(steep) の視点プリセットは廃止
        // (画角を変えても画面上の見た目が変わらず、ノーツとデカールの整合だけが壊れる)。
        // idx は後方互換のため受けるが無視する。
        cam.orthographic       = false;
        cam.transform.position = new Vector3(0f, 5.0411f, -3.4361f);
        cam.transform.rotation = Quaternion.Euler(28.8534f, 0f, 0f);
        cam.fieldOfView        = 79.016f;
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
