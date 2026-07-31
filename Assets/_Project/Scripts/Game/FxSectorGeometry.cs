using UnityEngine;

/// <summary>
/// プレイフィールド幾何 (画面設計モック(4) PLAYFIELD_SPEC.md 準拠・最終版)。
///
/// 座標系は 3 層構造 (SPEC §0):
///   1. スクリーン: 1280x720、Y 下向き
///   2. プレイフィールド: 全体を translate(96,103.5) scale(0.85) で縮小配置 (固定点 (640,690))
///   3. FX: FX_ALIGN_SPEC (2026-07-29) により FX 内部座標系は廃止。FX の円弧は
///      中央レーンの消失点 (CVpX,CVpY) を中心とする同心円としてプレイフィールド座標に直接描く。
///
/// ⚠️ FX レーンは直線レーンではない。中央 VP を中心とした「扇形 (円弧セクター)」であり、
/// ノーツは判定弧と同幅の円弧線分として半径方向に外側へ流れる (WACCA 方式)。
/// 判定線も直線ではなく円弧。進行度 s(z) は中央レーンと共有 (R(z)=JudgeRadius·s(z))。
/// FX レーンを平行四辺形・斜め直線レーン・ベジェリボンとして実装してはならない。
///
/// 中央 4 レーンはプレイフィールド座標で VP(640,-60)・judgeY=600 の台形ハイウェイ
/// (SPEC §1/§2 + CAMERA_SPEC 2026-07-30: VP=画面y120・FieldScale0.76 で遠景化)。
///
/// 本クラスは仕様ローカル座標→実効スクリーン px→ゲームプレイカメラで床平面 (y=0) へ
/// 逆投影しワールド座標へ変換する。カメラが固定である前提 (Tools/Playfield/1)。
/// SPEC の数値は変更・"改善" せずそのまま転写すること。
/// </summary>
public static class FxSectorGeometry
{
    // ── プレイフィールド変換 (SPEC §0、固定点 (640,690) 縮小) ──────────────────
    // K 指示 2026-07-30: カメラをもう少し遠くから → FieldScale 0.85 → 0.76
    // (判定幅 ×0.894、カメラ〜判定線距離 ×1.118)。off = (1-scale)·(640,690)。
    public const float FieldScale = 0.76f;
    public const float FieldOffX  = 153.6f;
    public const float FieldOffY  = 165.6f;

    // ── FX 幾何 (FX_ALIGN_SPEC 2026-07-29 全面適用) ──────────────────────────
    //
    // 旧: FX 内部座標 (VP=(640,256)・scale1.21変換・nearFx=155) — 中央レーンと遠近が
    //     異なり、同時刻の小節線/ノーツが境界で折れ曲がって見えた (K 指摘)。
    // 新 (制約A): FX の全円弧は「中央レーンの消失点 (CVpX,CVpY)」中心の同心円。
    //     放射エッジは延長すると必ず VP を通る。FX 専用の頂点・変換・near は持たない。
    // 新 (制約B): 進行度 s(z)=CNear/(CNear+z) を中央レーンと共有し、R(z)=JudgeRadius·s(z)。
    //
    // JudgeRadius = CDepth/sin(SectorThetaMin)。この関係を保つ限り、弧の内側端点
    // (θ=SectorThetaMin) の高さは任意の z で中央線の高さと厳密一致する (小節線の段差ゼロ)。
    // SectorThetaMin をトラック端角より外へ振ると、高さ一致を保ったまま横方向の隙間だけが生まれる。

    /// <summary>扇形の角度範囲 (+X 軸基準・Y 下向き、中心=中央 VP)。
    /// Min=トラック端の角度 (109.440° — VP=screen120・FieldScale0.76) + 5° — K 要望 (2026-07-29):
    /// 翼は DFJK レーンにくっつけない。5° のくさび状の隙間を空ける。
    /// 隙間があっても JudgeRadius=CDepth/sin(SectorThetaMin) を保つ限り、弧の内側端点の高さは
    /// 全 z で中央線と一致する (小節線はブリッジが隙間を水平に渡る)。Max=外側エッジ。</summary>
    public const float SectorThetaMin = 114.440f;
    /// <summary>外側エッジ。VP 上昇で JudgeRadius が 522→725 に拡大し翼が巨大化 (159°) →
    /// 縮小しすぎて立体感が消えた (143.61°) → 折衷の 150° (K 指示 2026-07-30 ×2)。
    /// 底面の整合は θmax に依存しない (弧は VP 同心円・s(z) 共有のため内側端点は常に中央線と一致)。</summary>
    public const float SectorThetaMax = 150f;
    /// <summary>ノーツ円弧の角度スパン (判定弧と同幅 = 扇形全幅)。</summary>
    public const float NoteThetaMin = SectorThetaMin;
    public const float NoteThetaMax = SectorThetaMax;
    /// <summary>ノーツ円弧の中心角度 (エフェクト発生位置などに使う)。</summary>
    public const float NoteThetaMid = (SectorThetaMin + SectorThetaMax) * 0.5f;
    /// <summary>判定円弧の半径 (プレイフィールド座標、中央 VP 中心) = CDepth/sin(SectorThetaMin) = 660/sin(114.440°)。</summary>
    public const float JudgeRadius = 724.96f;
    /// <summary>判定弧の外側に添える細弧の半径 (旧 +10fx px ≒ +12.1)。</summary>
    public const float JudgeThinRadius = JudgeRadius + 12.1f;
    /// <summary>FX ノーツのグロー線幅 (判定時、プレイフィールド px。旧 36fx px×1.21)。</summary>
    public const float NoteGlowStrokePx = 43.6f;
    /// <summary>FX ノーツのコア線幅 (旧 16fx px×1.21)。</summary>
    public const float NoteCoreStrokePx = 19.4f;
    /// <summary>S/L ボタン環: 判定弧の外側 (旧 r475〜513 = 判定+10〜+48 fx px ×1.21)。</summary>
    public const float ButtonR0 = JudgeRadius + 12.1f;
    public const float ButtonR1 = JudgeRadius + 58.1f;

    /// <summary>
    /// 扇形メッシュの内側半径の下限。VP 至近 (地平線) は逆投影レイがほぼ平行になり
    /// 頂点が遠方 (farClip 超え) へ飛んでクリップ破綻するため、安全な半径から面を張る
    /// (実際の可視下限は透明遮蔽壁 WallR ≥ 84.6 なので描画には影響しない)。
    /// </summary>
    public const float SectorInnerR = 40f;

    /// <summary>
    /// ノーツ/小節線の FX 半径 (FX_ALIGN_SPEC 制約B)。中央レーンと同一の進行度
    /// s(z)=CNear/(CNear+z·CPxPerWorld) を共有し R(z)=JudgeRadius·s(z)。
    /// FX 独自の near・独自の補間は禁止 (1 つの s(z) を両レーンに配る)。
    /// </summary>
    public static float NoteRadius(float zWorld)
    {
        float denom = Mathf.Max(CNear + zWorld * CPxPerWorld, 20f); // 判定通過側の漸近発散を回避
        return JudgeRadius * (CNear / denom);
    }

    /// <summary>中央レーンの ワールド z → ローカル px 換算 (スポーン z=22 ↔ フォグ帯上端 y≈235)。</summary>
    public const float CPxPerWorld = 31.8f;

    // ── 中央 4 レーンの定数 (SPEC §1/§2、プレイフィールド座標) ───────────────
    // CAMERA_SPEC (2026-07-30): 消失点はスクリーン (640,120) — FieldScale 0.76 ではローカル (640,-60)。
    // 判定ライン (CJudgeY=600 = スクリーン 621.6) は下端付近を維持。カメラは Tools/Playfield/1 の
    // 再ソルブ値に固定 (FOV 79.016 は不変、距離は「遠くから」指示で 5.174 → ≈5.79)。
    // カメラ再ソルブ値 (Tools/Playfield/1): y=5.0411 z=-3.4361 pitch=28.8534 fov=79.016
    // 判定線距離 5.834 (旧 5.174 — 「遠くから」指示による意図的変更)。
    public const float CVpX    = 640f;
    public const float CVpY    = -60f;
    public const float CJudgeY = 600f;
    public const float CDepth  = CJudgeY - CVpY;   // = 660
    public const float CNear   = 181.33f;          // near_world=5.702 × CPxPerWorld
    // 旧フォグ帯 (CTrackTopY/CFogEndY) は FAR_WALL_SPEC で廃止。奥端は WallY のハードエッジ。
    /// <summary>床の手前端 y と半幅 (手前端 (400,620)-(880,620))。</summary>
    public const float CTrackBotY  = 620f;
    /// <summary>judgeY(600) の判定ストリップ帯: y=600〜620。</summary>
    public const float CStripTopY  = 600f;

    // ── FAR_WALL_SPEC: 透明遮蔽壁 + laneLength ────────────────────────────────
    //
    // 奥端の距離フェードは廃止 (opacity=1 定数)。代わりに「透明な壁」より奥を一切描かない。
    // 壁は描画せず、壁位置でノーツ/レーンの輪郭がハードに切れる。壁位置は laneLength (0.25〜1.0)
    // で可変。中央は y &lt; WallY を、FX は r &lt; WallR (VP 同心円の内側) を隠す。

    // 上限 2.0 へ拡張 (K 指示 2026-07-30: レーンを長く)。LL=2 の壁は世界 z=45 —
    // LaneLayout.NoteSpawnZ (50) はこれより奥に保つこと。
    public const float LaneLengthMin = 0.25f;
    public const float LaneLengthMax = 2.00f;

    static float _laneLength = -1f;

    /// <summary>レーン長設定 (0.25〜1.0、既定 1.0)。PlayerPrefs "LaneLength"。</summary>
    public static float LaneLength
    {
        get
        {
            if (_laneLength < 0f)
                _laneLength = Mathf.Clamp(PlayerPrefs.GetFloat("LaneLength", 1f), LaneLengthMin, LaneLengthMax);
            return _laneLength;
        }
    }

    /// <summary>PlayerPrefs "LaneLength" 変更後に呼ぶ (キャッシュ破棄 → 次フレームで再構築)。</summary>
    public static void RefreshLaneLength() => _laneLength = -1f;

    /// <summary>中央レーンの壁 y (スペック座標)。LL=1 で ≈73、LL=2 で ≈14 (FieldScale0.76 遠景化後)。</summary>
    public static float WallY => CVpY + CDepth * (CNear / (CNear + 715.4f * LaneLength));

    /// <summary>FX レーンの壁半径。r &lt; WallR を隠す。
    /// FX_ALIGN_SPEC の統一ジオメトリでは壁も共有進行度で決まる:
    /// WallR = R(z_wall) = JudgeRadius·CNear/(CNear+715.4·LL) (LL=1 で ≈147、LL=2 で ≈82)。
    /// 中央と FX のノーツが厳密に同時刻に壁から現れる。</summary>
    public static float WallR => JudgeRadius * (CNear / (CNear + 715.4f * LaneLength));

    /// <summary>中央レーンの壁のワールド z (判定線からの残距離)。ノーツのシェーダクリップ用。</summary>
    public static float WallZCenterWorld => 715.4f * LaneLength / CPxPerWorld;

    /// <summary>中央レーン: y におけるレーン全幅の半分 halfW(y) = 240·(y−CVpY)/(CTrackBotY−CVpY) (SPEC §1)。</summary>
    public static float CenterHalfW(float yLocal) => 240f * (yLocal - CVpY) / (CTrackBotY - CVpY);

    /// <summary>中央レーン: scale(z) = near/(near+z)、y(z) = CVpY + CDepth·scale (SPEC §1)。</summary>
    public static float CenterScale(float zPx) => CNear / (CNear + zPx);
    public static float CenterY(float zPx)     => CVpY + CDepth * CenterScale(zPx);

    /// <summary>
    /// 暗色面用のアルファ補正。モック (ブラウザ) は sRGB 空間で合成するが Unity の
    /// リニアレンダリングはリニア空間で合成するため、暗い半透明面が明るい背景の上で
    /// 薄く見えてしまう。暗色 src・明色 dst の組では a' = 1−(1−a)^2.2 が良い近似。
    /// </summary>
    public static float DarkA(float a)
        => QualitySettings.activeColorSpace == ColorSpace.Linear
            ? 1f - Mathf.Pow(Mathf.Clamp01(1f - a), 2.2f)
            : a;

    /// <summary>
    /// 明色オーバーレイ用のアルファ補正 (DarkA の逆)。明色 src・暗色 dst の組では
    /// リニア合成が sRGB 合成より強く出るため a' = a^2.2 で抑える。
    /// </summary>
    public static float BrightA(float a)
        => QualitySettings.activeColorSpace == ColorSpace.Linear
            ? Mathf.Pow(Mathf.Clamp01(a), 2.2f)
            : a;

    static Camera _cam;

    /// <summary>逆投影に使うゲームプレイカメラ (Main Camera)。</summary>
    public static Camera Cam
    {
        get
        {
            if (_cam == null) _cam = Camera.main;
            return _cam;
        }
    }

    /// <summary>逆投影の準備ができているか (カメラが存在するか)。</summary>
    public static bool Ready => Cam != null;

    /// <summary>プレイフィールドローカル座標 → 実効スクリーン px (SPEC §0 の 0.85 縮小)。</summary>
    public static Vector2 FieldPoint(float xLocal, float yLocal)
        => new Vector2(FieldOffX + FieldScale * xLocal, FieldOffY + FieldScale * yLocal);

    /// <summary>
    /// FX 極座標 (θ°, r) → 実効スクリーン px (FX_ALIGN_SPEC 制約A)。
    /// 中心は中央レーンの消失点 (CVpX, CVpY)。左翼が基準、右翼は x=1280−x ミラー。
    /// FX 専用の変換・頂点は存在しない (プレイフィールド座標に直接描く)。
    /// </summary>
    public static Vector2 SpecPoint(bool right, float thetaDeg, float r)
    {
        float rad = thetaDeg * Mathf.Deg2Rad;
        float x = CVpX + r * Mathf.Cos(rad);
        float y = CVpY + r * Mathf.Sin(rad); // Y 下向き
        if (right) x = 1280f - x;
        return FieldPoint(x, y);
    }

    /// <summary>実効スクリーン px 座標をカメラ経由で床平面 y=0 へ逆投影する。</summary>
    public static Vector3 FloorPoint(Vector2 specPx, float yLift = 0f)
    {
        var cam = Cam;
        if (cam == null) return Vector3.zero;
        // スペック座標 (1280x720) は 16:9 前提。ビューが 16:9 以外だと水平画角が変わり、
        // ビューポート比率のままでは横に伸縮してワールド固定のノーツとズレるため、
        // 水平方向を 16:9 相当の画角へ補正する (16:9 では fix=1 で従来と同一)。
        float aspectFix = (16f / 9f) / Mathf.Max(cam.aspect, 0.01f);
        float u = 0.5f + (specPx.x / 1280f - 0.5f) * aspectFix;
        var ray = cam.ViewportPointToRay(new Vector3(u, 1f - specPx.y / 720f, 0f));
        // 地平線 (レイが床と交わらない) 越えの保険としてレイ長を制限する。
        // ⚠️ farClip(120) を超える頂点はクリップ破綻の原因になるため 100 まで。
        float t = ray.direction.y < -1e-4f ? -ray.origin.y / ray.direction.y : 100f;
        var p = ray.origin + ray.direction * Mathf.Clamp(t, 0f, 100f);
        p.y = yLift;
        return p;
    }

    /// <summary>FX 内部極座標を直接床平面ワールド座標へ変換する。</summary>
    public static Vector3 FloorPoint(bool right, float thetaDeg, float r, float yLift = 0f)
        => FloorPoint(SpecPoint(right, thetaDeg, r), yLift);

    /// <summary>プレイフィールドローカル座標を直接床平面ワールド座標へ変換する (中央レーン用)。</summary>
    public static Vector3 CenterFloorPoint(float xLocal, float yLocal, float yLift = 0f)
        => FloorPoint(FieldPoint(xLocal, yLocal), yLift);
}
