using UnityEngine;

/// <summary>
/// FX レーンの静的ビジュアル (画面設計モック(4) PLAYFIELD_SPEC.md §3 準拠)。
/// FX 内部座標 VP(640,256) を中心とした扇形セクター θ130°〜170° を仕様どおり
/// 床へ逆投影して生成する:
///   面 (VP からの放射グラデ・明るいグレイブルー) / 白銀の放射エッジ 2 本 /
///   判定円弧 r=465 (#6fc4dc グロー + #f4fcfe コア + r475 細弧) /
///   同心ガイド弧 r=180/290/400 / 呼吸する光 (4.2s、左右位相 2.1s ずれ) / 押下フラッシュ。
///
/// ⚠️ FX レーンを直線・平行四辺形・ベジェリボンとして実装してはならない (仕様 §3)。
///
/// ExecuteAlways: エディタでもカメラの移動・FOV 変更に追従して再投影する。
/// マテリアルは PlayfieldRedesignBuilder (Tools/Playfield/4) が割り当てる
/// (頂点カラー対応シェーダ・Cull Off 必須)。
/// </summary>
[ExecuteAlways]
public class FxLaneVisuals : MonoBehaviour
{
    /// <summary>シーン内の唯一のインスタンス。FxArcNote がノーツ用マテリアルを参照する。</summary>
    public static FxLaneVisuals Instance { get; private set; }

    [Header("Materials (頂点カラー対応 / Builder Stage4 が割当)")]
    [SerializeField] Material _faceMat;     // セクター面 (アルファ)
    [SerializeField] Material _lineMat;     // エッジコア・判定コア・ガイド (アルファ、白ベース×頂点色)
    [SerializeField] Material _glowMat;     // エッジ/判定グロー (加算)
    [SerializeField] Material _flashMat;    // 押下フラッシュ (加算)
    [SerializeField] Material _noteMat;      // FX ノーツコア (FxArcNote が参照)
    [SerializeField] Material _noteGlowMat;  // FX ノーツグロー (加算、FxArcNote が参照)
    [SerializeField] Material _judgeGlowMat; // 判定弧グロー (加算・ノーツより上のキュー)
    [SerializeField] Material _judgeCoreMat; // 判定弧コア (アルファ・ノーツより上のキュー)
    [SerializeField] Material _btnFillMat;   // S/L ボタン面 (円環セグメント、アルファ)
    [SerializeField] Material _btnLineMat;   // S/L ボタン枠線 (弧に沿う、アルファ)
    [SerializeField] Material _keySMat;      // S 文字グリフ (key_S スプライトの中央部を UV 切り出し)
    [SerializeField] Material _keyLMat;      // L 文字グリフ (key_L スプライト)

    [Header("Shape")]
    [SerializeField] float _yLift   = 0.012f; // 床との z-fight 回避
    [SerializeField] int   _arcSegs = 40;     // 円弧の角度分割数

    /// <summary>FxArcNote (FX ノーツ円弧コア) が使うマテリアル。</summary>
    public Material NoteMaterial => _noteMat;
    /// <summary>FxArcNote のグロー層 (加算) が使うマテリアル。</summary>
    public Material NoteGlowMaterial => _noteGlowMat;

    // ── カラーパレット (SPEC §3/§4) ──
    // 面: 明るいグレイブルーの放射グラデ (VP 側 → 外周)
    static readonly Color FaceInner = new Color(38 / 255f, 58 / 255f, 72 / 255f, 0.88f);
    static readonly Color FaceMid   = new Color(28 / 255f, 46 / 255f, 58 / 255f, 0.90f);
    static readonly Color FaceOuter = new Color(20 / 255f, 36 / 255f, 46 / 255f, 0.92f);
    // COLOR_SPEC「琥珀&深緑」: FX系 = 深緑ミント。縁/判定弧の発光 = S-Glow、コア = S-Core。
    static readonly Color EdgeCore  = new Color(223 / 255f, 251 / 255f, 244 / 255f, 1f); // S-Core #DFFBF4
    static readonly Color EdgeGlow  = new Color( 82 / 255f, 224 / 255f, 189 / 255f, 1f); // S-Glow #52E0BD
    // 判定弧: S-Glow グロー(幅24) + S-Core コア(幅10)・外周細弧(r475)
    static readonly Color JudgeGlowCol = new Color( 82 / 255f, 224 / 255f, 189 / 255f, 1f); // S-Glow
    static readonly Color JudgeCoreCol = new Color(223 / 255f, 251 / 255f, 244 / 255f, 1f); // S-Core
    static readonly Color JudgeThinCol = new Color(223 / 255f, 251 / 255f, 244 / 255f, 0.35f); // S-Core
    // ガイド弧 rgba(200,240,248,0.20)
    static readonly Color GuideCol  = new Color(200 / 255f, 240 / 255f, 248 / 255f, 0.20f);
    // 呼吸する光 (fxSheen) / 押下フラッシュ (FXハイライトパルス) = S-Core
    static readonly Color SheenCol  = new Color(223 / 255f, 251 / 255f, 244 / 255f, 1f); // S-Core
    static readonly Color FlashCol  = new Color(223 / 255f, 251 / 255f, 244 / 255f, 1f); // S-Core

    static readonly int IdBaseColor = Shader.PropertyToID("_BaseColor");

    readonly System.Collections.Generic.List<GameObject> _built = new();
    readonly FxArcMeshBuilder _builder = new();

    // 押下フラッシュ (仕様 §5: 扇形全体がシアンで発光、離すと 0.18s でフェード)
    MeshRenderer _flashL, _flashR;
    MeshRenderer _btnFlashL, _btnFlashR; // S/L キー面の押下フラッシュ (BUTTONS_SPEC §2)
    bool  _heldL, _heldR;
    float _levelL, _levelR;
    const float FlashFadeSec = 0.18f;
    GameInputController _input;
    MaterialPropertyBlock _pb;

    // 呼吸する光 (SPEC §3: 面全体が 4.2s 周期で明滅、左右で位相 2.1s ずれ)
    MeshRenderer _sheenL, _sheenR;
    const float SheenPeriod = 4.2f;

    // カメラ変更検知 (エディタ調整に追従)
    Vector3 _camPos; Quaternion _camRot; float _camFov; float _camAspect; float _lastLL; bool _builtOnce;

    void OnEnable()
    {
        Instance = this;
        _builtOnce = false;
        Rebuild();
    }

    void OnDisable()
    {
        if (Instance == this) Instance = null;
        UnhookInput();
        Clear();
    }

    void Update()
    {
        var cam = FxSectorGeometry.Cam;
        if (cam == null) return;

        if (!_builtOnce ||
            (cam.transform.position - _camPos).sqrMagnitude > 1e-6f ||
            Quaternion.Angle(cam.transform.rotation, _camRot) > 0.01f ||
            !Mathf.Approximately(cam.fieldOfView, _camFov) ||
            !Mathf.Approximately(cam.aspect, _camAspect) ||   // ウィンドウリサイズ (アスペクト変化) 追従
            !Mathf.Approximately(FxSectorGeometry.LaneLength, _lastLL))   // laneLength (壁位置) 追従
            Rebuild();

        if (!Application.isPlaying) return;
        if (_input == null) HookInput();

        _levelL = Mathf.MoveTowards(_levelL, _heldL ? 1f : 0f, Time.deltaTime / FlashFadeSec);
        _levelR = Mathf.MoveTowards(_levelR, _heldR ? 1f : 0f, Time.deltaTime / FlashFadeSec);
        ApplyFlash(_flashL, _levelL);
        ApplyFlash(_flashR, _levelR);
        ApplyFlash(_btnFlashL, _levelL);
        ApplyFlash(_btnFlashR, _levelR);

        // 呼吸: opacity 0.25 → 0.65 → 0.25 (ease-in-out ≒ 正弦)、右は 2.1s 遅れ
        float tL = Time.time / SheenPeriod * 2f * Mathf.PI;
        ApplyAlpha(_sheenL, 0.45f + 0.20f * Mathf.Sin(tL - Mathf.PI * 0.5f));
        ApplyAlpha(_sheenR, 0.45f + 0.20f * Mathf.Sin(tL - Mathf.PI * 1.5f));
    }

    // ── 押下フラッシュ ──────────────────────────────────────────────────────

    void HookInput()
    {
        _input = FindFirstObjectByType<GameInputController>();
        if (_input == null) return;
        _input.OnLaneDown += OnLaneDown;
        _input.OnLaneUp   += OnLaneUp;
    }

    void UnhookInput()
    {
        if (_input == null) return;
        _input.OnLaneDown -= OnLaneDown;
        _input.OnLaneUp   -= OnLaneUp;
        _input = null;
    }

    void OnLaneDown(LaneRef lane, double timeMs)
    {
        if (lane == LaneRef.FxL) _heldL = true;
        else if (lane == LaneRef.FxR) _heldR = true;
    }

    void OnLaneUp(LaneRef lane, double timeMs)
    {
        if (lane == LaneRef.FxL) _heldL = false;
        else if (lane == LaneRef.FxR) _heldR = false;
    }

    void ApplyFlash(MeshRenderer mr, float level)
    {
        if (mr == null) return;
        bool on = level > 0.01f;
        if (mr.enabled != on) mr.enabled = on;
        if (!on) return;
        ApplyAlpha(mr, level);
    }

    void ApplyAlpha(MeshRenderer mr, float a)
    {
        if (mr == null) return;
        _pb ??= new MaterialPropertyBlock();
        _pb.SetColor(IdBaseColor, new Color(1f, 1f, 1f, a));
        mr.SetPropertyBlock(_pb);
    }

    // ── ジオメトリ生成 ──────────────────────────────────────────────────────

    public void Rebuild()
    {
        Clear();
        var cam = FxSectorGeometry.Cam;
        if (cam == null) return;
        if (_faceMat == null && _lineMat == null) return; // Builder 未適用

        _camPos = cam.transform.position;
        _camRot = cam.transform.rotation;
        _camFov = cam.fieldOfView;
        _camAspect = cam.aspect;
        _lastLL    = FxSectorGeometry.LaneLength;
        _builtOnce = true;

        // FAR_WALL_SPEC: r < WallR は透明遮蔽壁の裏 — 面・縁とも一切描かない (ハードエッジ)。
        float rWall = Mathf.Max(FxSectorGeometry.WallR, FxSectorGeometry.SectorInnerR);

        const float thMin = FxSectorGeometry.SectorThetaMin;
        const float thMax = FxSectorGeometry.SectorThetaMax;
        const float rJ    = FxSectorGeometry.JudgeRadius;
        // BUTTONS_SPEC §6: レーン面・縁・押下フラッシュは判定弧 (r=465) で終わる。
        // S/L キーは弧の外側 (r475〜513) に独立して置く。
        const float rOut  = FxSectorGeometry.JudgeRadius;

        foreach (bool right in new[] { false, true })
        {
            string side = right ? "R" : "L";

            // ── セクター面: VP からの放射グラデ (wg — 0%/55%/100% 相当)、奥端縁取り無し ──
            _builder.Clear();
            _builder.AddArcBand(right, thMin, thMax, _ => rWall, _ => rOut,
                (t, rt) =>
                {
                    float r = Mathf.Lerp(rWall, rOut, rt);
                    float g = r / 539f; // wg gradient (旧 r=505 基準を新半径スケールへ換算)
                    var c = g < 0.55f
                        ? Color.Lerp(FaceInner, FaceMid, g / 0.55f)
                        : Color.Lerp(FaceMid, FaceOuter, (g - 0.55f) / 0.45f);
                    // 暗色面: sRGB/リニア合成差の補正。距離フェードは FAR_WALL_SPEC で廃止。
                    c.a = FxSectorGeometry.DarkA(c.a);
                    return c;
                }, _arcSegs, 14, _yLift);
            Spawn($"FxFace{side}", _faceMat, 10);

            // ── 白銀の放射エッジ 2 本 (edgeCoreF/edgeGlowF: 外周ほど明るく、VP へ減衰) ──
            _builder.Clear();
            foreach (float th in new[] { thMin, thMax })
                AddRadialEdge(right, th, widthPx: 12.1f, a0: 0.02f, aMid: 0.22f, a1: 0.55f, EdgeGlow, _yLift + 0.004f);
            Spawn($"FxEdgeGlow{side}", _glowMat, 13);

            _builder.Clear();
            foreach (float th in new[] { thMin, thMax })
                AddRadialEdge(right, th, widthPx: 3.4f, a0: 0.05f, aMid: 0.45f, a1: 0.95f, EdgeCore, _yLift + 0.006f);
            Spawn($"FxEdgeCore{side}", _lineMat, 14);

            // ── 判定円弧 r=465: 発光は必ず「Glow + Core」二層 (COLOR_SPEC 実装ルール)。
            //    S-Glow 幅24 ブラー弧 (ガウス断面 + さらに広いソフトハロー) + S-Core 幅10 実線弧 ──
            _builder.Clear();
            _builder.AddArcBand(right, thMin, thMax, _ => rJ - 24.2f, _ => rJ + 24.2f,
                (t, rt) => WithA(JudgeGlowCol, 0.30f * Gauss(rt)), _arcSegs + 8, 6, _yLift + 0.007f);
            _builder.AddArcBand(right, thMin, thMax, _ => rJ - 14.5f, _ => rJ + 14.5f,
                (t, rt) => WithA(JudgeGlowCol, 0.85f * Gauss(rt)), _arcSegs + 8, 4, _yLift + 0.008f);
            Spawn($"FxJudgeGlow{side}", _judgeGlowMat != null ? _judgeGlowMat : _glowMat, 18);

            _builder.Clear();
            _builder.AddArcBand(right, thMin, thMax, _ => rJ - 6.1f, _ => rJ + 6.1f,
                (t, rt) => JudgeCoreCol, _arcSegs + 8, 1, _yLift + 0.010f);
            // 外側 r=475 の細弧 (幅 1.2、rgba(236,250,252,0.35))
            _builder.AddArcBand(right, thMin, thMax,
                _ => FxSectorGeometry.JudgeThinRadius - 0.75f, _ => FxSectorGeometry.JudgeThinRadius + 0.75f,
                (t, rt) => WithA(JudgeThinCol, FxSectorGeometry.BrightA(JudgeThinCol.a)), _arcSegs + 8, 1, _yLift + 0.010f);
            Spawn($"FxJudgeCore{side}", _judgeCoreMat != null ? _judgeCoreMat : _lineMat, 19);

            // ── 呼吸する光 (fxSheen: 0%/60%/88%/100% = 0/0.05/0.16/0.05、4.2s 明滅) ──
            _builder.Clear();
            _builder.AddArcBand(right, thMin, thMax, _ => rWall, _ => rOut,
                (t, rt) =>
                {
                    float r = Mathf.Lerp(rWall, rOut, rt);
                    float g = r / 539f;
                    float a = g < 0.60f ? Mathf.Lerp(0f, 0.05f, g / 0.60f)
                            : g < 0.88f ? Mathf.Lerp(0.05f, 0.16f, (g - 0.60f) / 0.28f)
                                        : Mathf.Lerp(0.16f, 0.05f, (g - 0.88f) / 0.12f);
                    return WithA(SheenCol, FxSectorGeometry.BrightA(a));
                }, _arcSegs, 12, _yLift + 0.002f);
            var sheen = Spawn($"FxSheen{side}", _glowMat, 12);
            if (right) _sheenR = sheen; else _sheenL = sheen;
            ApplyAlpha(sheen, 0.45f); // 静止時 (エディタ) は中間値

            // ── S/L ボタン (SPEC §3: r475〜513 の円環セグメント θ143°〜157°) ──
            // ⚠️ 回転矩形は禁止。VP 中心の極座標 (θ 8 分割 × 内外径) で頂点を生成し、
            // 内縁が判定弧 (r465) と同心の弧になるようにする。枠線も同じ弧に沿って描く。
            // BUTTONS_SPEC §2: θ=130°〜170° (判定弧と同範囲)、r475〜513、角丸 ~6 (=7.5/1.26)。
            // 面はほぼ透過 rgba(4,16,26,0.18)、枠 rgba(225,244,248,0.5)・1.5px。
            const float bTh0 = FxSectorGeometry.SectorThetaMin;
            const float bTh1 = FxSectorGeometry.SectorThetaMax;
            const float bR0  = FxSectorGeometry.ButtonR0;
            const float bR1  = FxSectorGeometry.ButtonR1;
            const float bCr  = 7.3f; // 角丸半径 (旧 6fx px ×1.21)
            var bFillCol = new Color(4 / 255f, 16 / 255f, 26 / 255f, 0.18f);
            var contour  = RoundedAnnulusContour(bTh0, bTh1, bR0, bR1, bCr);
            SpawnContourFan($"FxBtnFill{side}", _btnFillMat, 20, right, contour,
                            (bTh0 + bTh1) * 0.5f, (bR0 + bR1) * 0.5f, bFillCol, _yLift + 0.012f);
            // 枠線: 幅1.5 — 同じ角丸輪郭の内側オフセット輪郭との間をストリップで張る
            float insDeg = 1.8f / ((bR0 + bR1) * 0.5f) * Mathf.Rad2Deg;
            var inner = RoundedAnnulusContour(bTh0 + insDeg, bTh1 - insDeg, bR0 + 1.8f, bR1 - 1.8f, Mathf.Max(1f, bCr - 1.8f));
            var bBorderCol = WithA(new Color(225 / 255f, 244 / 255f, 248 / 255f, 1f), FxSectorGeometry.BrightA(0.5f));
            SpawnContourStrip($"FxBtnLine{side}", _btnLineMat, 21, right, contour, inner, bBorderCol, _yLift + 0.013f);
            // 琥珀アクセント: 内弧のすぐ外 r=477.5 (=601.6/1.26)、θ131.5°〜168.5°、P-Glow 0.55・幅1.5
            _builder.Clear();
            _builder.AddArcBand(right,
                FxSectorGeometry.SectorThetaMin + 1.55f, FxSectorGeometry.SectorThetaMax - 1.55f,
                _ => FxSectorGeometry.JudgeRadius + 15.1f - 0.9f, _ => FxSectorGeometry.JudgeRadius + 15.1f + 0.9f,
                (t, rt) => WithA(new Color(227 / 255f, 176 / 255f, 59 / 255f, 1f), FxSectorGeometry.BrightA(0.55f)),
                _arcSegs, 1, _yLift + 0.0135f);
            Spawn($"FxBtnAccent{side}", _btnLineMat, 21);
            // 押下フラッシュ: セグメント面に P-Glow の外→内フェード (0.32→0)、0.18s (§2)
            _builder.Clear();
            _builder.AddArcBand(right, bTh0, bTh1, _ => bR0, _ => bR1,
                (t, rt) => WithA(new Color(227 / 255f, 176 / 255f, 59 / 255f, 1f),
                                 FxSectorGeometry.BrightA(0.32f * rt)),
                _arcSegs, 6, _yLift + 0.0132f);
            var bFlash = Spawn($"FxBtnFlash{side}", _flashMat, 22);
            if (bFlash != null) bFlash.enabled = false;
            if (right) _btnFlashR = bFlash; else _btnFlashL = bFlash;
            // 文字 (S/L) は HUD の TMP ラベルが担う (リバインド追従のため)。

            // ── 押下フラッシュ (hitFx: 外周ほど強い 0/0.06/0.32、扇形全体) ──
            _builder.Clear();
            _builder.AddArcBand(right, thMin, thMax, _ => rWall, _ => rOut,
                (t, rt) =>
                {
                    float r = Mathf.Lerp(rWall, rOut, rt);
                    float g = r / 539f;
                    float a = g < 0.55f ? Mathf.Lerp(0f, 0.06f, g / 0.55f)
                                        : Mathf.Lerp(0.06f, 0.32f, (g - 0.55f) / 0.45f);
                    return WithA(FlashCol, FxSectorGeometry.BrightA(a));
                }, _arcSegs, 10, _yLift + 0.005f);
            var flash = Spawn($"FxFlash{side}", _flashMat, 15);
            if (flash != null) flash.enabled = false;
            if (right) _flashR = flash; else _flashL = flash;
        }
    }

    /// <summary>
    /// 放射エッジ = θ 一定の細い扇形スライス。スクリーン上の太さが R に比例する
    /// (= VP で 0 に収束する) ため、一定の角度幅で表現できる。
    /// アルファは edgeCoreF/edgeGlowF の放射グラデ (0% / 55% / 100%) を再現する。
    /// </summary>
    void AddRadialEdge(bool right, float thetaDeg, float widthPx, float a0, float aMid, float a1, Color col, float yLift)
    {
        float halfDeg = (widthPx * 0.5f / FxSectorGeometry.JudgeRadius) * Mathf.Rad2Deg;
        float r0 = Mathf.Max(FxSectorGeometry.WallR, FxSectorGeometry.SectorInnerR); // 壁より奥は描かない
        // BUTTONS_SPEC §6: 縁は判定弧 (r=465) で終わる — S/L ボタン環 (r475〜513) にはみ出さない
        const float r1 = FxSectorGeometry.JudgeRadius;
        _builder.AddArcBand(right, thetaDeg - halfDeg, thetaDeg + halfDeg,
            _ => r0, _ => r1,
            (t, rt) =>
            {
                float r = Mathf.Lerp(r0, r1, rt);
                float g = r / 505f;
                float a = g < 0.55f ? Mathf.Lerp(a0, aMid, g / 0.55f)
                                    : Mathf.Lerp(aMid, a1, (g - 0.55f) / 0.45f);
                return WithA(col, a); // 発光は仕様の生アルファ (BrightA で殺さない)。距離フェード廃止
            }, 1, 16, yLift);
    }

    static Color WithA(Color c, float a) { c.a = a; return c; }

    /// <summary>
    /// (θ°, r px) 空間の角丸矩形輪郭を生成する (S/L ボタン用)。θ 軸の実スケールは弧長
    /// ≈ r·rad なので、角丸半径を角度に換算した楕円角として生成すると画面上で円形の角丸になる。
    /// 内外縁 (r=const) は θ 方向に分割された弧 = 判定弧と同心。頂点数は固定 (輪郭同士の
    /// ストリップ張りに使うため)。
    /// </summary>
    static System.Collections.Generic.List<Vector2> RoundedAnnulusContour(
        float a0, float a1, float r0, float r1, float crPx)
    {
        float rMid = (r0 + r1) * 0.5f;
        float crA  = crPx / rMid * Mathf.Rad2Deg; // 角丸半径の角度換算
        var pts = new System.Collections.Generic.List<Vector2>();
        const int EdgeSegs = 6, CornerSegs = 4;
        void Corner(Vector2 c, float phi0, float phi1)
        {
            for (int i = 0; i <= CornerSegs; i++)
            {
                float phi = Mathf.Lerp(phi0, phi1, (float)i / CornerSegs) * Mathf.Deg2Rad;
                pts.Add(new Vector2(c.x + crA * Mathf.Cos(phi), c.y + crPx * Mathf.Sin(phi)));
            }
        }
        void Edge(Vector2 from, Vector2 to)
        {
            for (int i = 1; i <= EdgeSegs; i++)
                pts.Add(Vector2.Lerp(from, to, (float)i / EdgeSegs));
        }
        // 反時計回り: 内縁 (r0) → 角B → 放射辺 (a1) → 角C → 外縁 (r1) → 角D → 放射辺 (a0) → 角A
        pts.Add(new Vector2(a0 + crA, r0));
        Edge(new Vector2(a0 + crA, r0), new Vector2(a1 - crA, r0));
        Corner(new Vector2(a1 - crA, r0 + crPx), -90f, 0f);
        Edge(new Vector2(a1, r0 + crPx), new Vector2(a1, r1 - crPx));
        Corner(new Vector2(a1 - crA, r1 - crPx), 0f, 90f);
        Edge(new Vector2(a1 - crA, r1), new Vector2(a0 + crA, r1));
        Corner(new Vector2(a0 + crA, r1 - crPx), 90f, 180f);
        Edge(new Vector2(a0, r1 - crPx), new Vector2(a0, r0 + crPx));
        Corner(new Vector2(a0 + crA, r0 + crPx), 180f, 270f);
        return pts;
    }

    /// <summary>角丸輪郭 ((θ,r) リスト) を中心からの扇状三角形で塗り潰すメッシュを生成する。</summary>
    void SpawnContourFan(string name, Material mat, int order, bool right,
        System.Collections.Generic.List<Vector2> contour, float aC, float rC, Color col, float yLift)
    {
        if (mat == null) return;
        if (QualitySettings.activeColorSpace == ColorSpace.Linear) col = col.linear; // sRGB→リニア (他メッシュと同じ)
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.hideFlags = HideFlags.DontSave;
        go.transform.SetParent(transform, false);
        var mesh = new Mesh { name = name };
        var v = new System.Collections.Generic.List<Vector3>();
        var cl = new System.Collections.Generic.List<Color>();
        v.Add(FxSectorGeometry.FloorPoint(right, aC, rC, yLift)); cl.Add(col);
        foreach (var p in contour) { v.Add(FxSectorGeometry.FloorPoint(right, p.x, p.y, yLift)); cl.Add(col); }
        var t = new System.Collections.Generic.List<int>();
        for (int i = 1; i <= contour.Count; i++)
        {
            int nxt = i == contour.Count ? 1 : i + 1;
            t.Add(0); t.Add(i); t.Add(nxt);
        }
        mesh.SetVertices(v); mesh.SetColors(cl); mesh.SetTriangles(t, 0);
        mesh.RecalculateBounds();
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat; mr.sortingOrder = order;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _built.Add(go);
    }

    /// <summary>外輪郭と内輪郭 (同一頂点数) の間をストリップで張る枠線メッシュを生成する。</summary>
    void SpawnContourStrip(string name, Material mat, int order, bool right,
        System.Collections.Generic.List<Vector2> outer, System.Collections.Generic.List<Vector2> inner,
        Color col, float yLift)
    {
        if (mat == null || outer.Count != inner.Count) return;
        if (QualitySettings.activeColorSpace == ColorSpace.Linear) col = col.linear; // sRGB→リニア
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.hideFlags = HideFlags.DontSave;
        go.transform.SetParent(transform, false);
        var mesh = new Mesh { name = name };
        int n = outer.Count;
        var v = new Vector3[n * 2];
        var cl = new Color[n * 2];
        for (int i = 0; i < n; i++)
        {
            v[i * 2]     = FxSectorGeometry.FloorPoint(right, outer[i].x, outer[i].y, yLift);
            v[i * 2 + 1] = FxSectorGeometry.FloorPoint(right, inner[i].x, inner[i].y, yLift);
            cl[i * 2] = col; cl[i * 2 + 1] = col;
        }
        var t = new System.Collections.Generic.List<int>();
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            t.Add(i * 2); t.Add(j * 2); t.Add(i * 2 + 1);
            t.Add(i * 2 + 1); t.Add(j * 2); t.Add(j * 2 + 1);
        }
        mesh.vertices = v; mesh.colors = cl; mesh.SetTriangles(t, 0);
        mesh.RecalculateBounds();
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat; mr.sortingOrder = order;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _built.Add(go);
    }

    /// <summary>
    /// VP 側の内周フェード。レーンは消失点に到達しない (モック仕様): r≤110 で完全透明、
    /// r=230 で不透明になる長いグラデーション (弧全長の約 20% 帯)。VP の光の楕円との間に
    /// 暗い隙間ができるのが正しい。
    /// </summary>

    /// <summary>グロー帯用ガウス断面 (帯中心 rt=0.5 が最大、端で ≈0)。σ≈帯幅/4 相当。</summary>
    static float Gauss(float rt)
    {
        float x = (rt - 0.5f) * 2f; // -1..1
        return Mathf.Exp(-4f * x * x);
    }

    /// <summary>判定グロー用: 帯の中心 (rt=0.5) が最も明るい三角プロファイル。</summary>
    static float Bell(float rt) => 1f - Mathf.Abs(rt * 2f - 1f);

    // モック §0 のレイヤー順を sortingOrder で固定する。
    MeshRenderer Spawn(string name, Material mat, int order, float liftAdd = 0f)
    {
        if (mat == null) return null;
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.hideFlags = HideFlags.DontSave; // 生成物はシーンに保存しない (常に再生成)
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, liftAdd, 0f);
        var mesh = new Mesh { name = name };
        _builder.Apply(mesh);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial    = mat;
        mr.sortingOrder      = order;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _built.Add(go);
        return mr;
    }

    void Clear()
    {
        _flashL = _flashR = null;
        _btnFlashL = _btnFlashR = null;
        _sheenL = _sheenR = null;
        foreach (var go in _built)
            if (go != null)
            {
                if (Application.isPlaying) Destroy(go);
                else DestroyImmediate(go);
            }
        _built.Clear();
        // DontSave 漏れ (ドメインリロード後) も掃除
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var c = transform.GetChild(i).gameObject;
            if (c.name.StartsWith("Fx"))
            {
                if (Application.isPlaying) Destroy(c);
                else DestroyImmediate(c);
            }
        }
    }
}
