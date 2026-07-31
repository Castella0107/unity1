using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 中央 4 レーンの静的ビジュアル (画面設計モック(4) PLAYFIELD_SPEC.md §2 準拠)。
/// プレイフィールド座標 (VP(640,160)・judgeY=600) の台形ハイウェイを、スクリーン仕様
/// どおり床平面へ逆投影したフラットな「デカール」として生成する (固定カメラ前提なので
/// スクリーン上ではモックと同一形状になる):
///   床 (asf 縦グラデ + 奥端フォグ) / 側壁 / 白銀エッジ 2 本 (先細り) /
///   サブレーン分割線 (VP 収束・中央は破線) / 判定ストリップ (y=600-620 シアン台形) /
///   接近グロー / 手前断面 (#081c2c)。
///
/// 奥端 (y=235) にハードエッジを描いてはならない — y=235〜257 のフォグ帯で霧から出現する。
///
/// ExecuteAlways: エディタでもカメラの移動・FOV 変更に追従して再投影する。
/// マテリアルは PlayfieldRedesignBuilder (Tools/Playfield/3) が割り当てる。
/// </summary>
[ExecuteAlways]
public class CenterTrackVisuals : MonoBehaviour
{
    // BUTTONS_SPEC §1 押下フラッシュ: キー面全体に P-Glow の下→上フェード (0.32→0)、0.18s
    readonly MeshRenderer[] _capFlash = new MeshRenderer[4];
    readonly float[] _capLevel = new float[4];
    readonly bool[]  _capHeld  = new bool[4];
    const float CapFlashFadeSec = 0.18f;
    GameInputController _input;
    MaterialPropertyBlock _pb;
    static readonly int IdBaseColor = Shader.PropertyToID("_BaseColor");

    [Header("Materials (頂点カラー対応 / Builder Stage3 が割当)")]
    [SerializeField] Material _faceMat; // 床・壁・断面・接近グロー (アルファ)
    [SerializeField] Material _lineMat; // エッジ・分割線・判定コア (アルファ)
    [SerializeField] Material _glowMat; // 判定ストリップ発光 (加算)

    [Header("Shape")]
    [SerializeField] float _yLift = 0.008f; // 床との z-fight 回避 (FX セクターより下)
    [SerializeField] int   _rows  = 26;     // 縦方向の分割数

    // ── カラーパレット (SPEC §4 / dc.html グラデ定義) ──
    // 床 asf: y235 → 46% → y620
    static readonly Color FloorTop = new Color(16 / 255f, 42 / 255f, 62 / 255f, 0.88f);
    static readonly Color FloorMid = new Color(10 / 255f, 28 / 255f, 44 / 255f, 0.90f);
    static readonly Color FloorBot = new Color(6 / 255f, 17 / 255f, 28 / 255f, 0.92f);
    // レーンの縁・中央線・判定ストリップは「判定線」色 (色タブで変更可) に統一 — K 指示 2026-07-30
    // 「レーンの横の線と判定線を同じものに」。既定 #7DEEFA シアン = FX 判定弧とも同系。
    // 旧: COLOR_SPEC 琥珀 (P-Core #FBEED0 / P-Glow #E3B03B) の固定色。
    // アルファ/グラデは従来の係数を維持し、RGB のみ設定色を使う。
    static Color EdgeCore     => GameColorSettings.JudgmentLineColor;
    static Color EdgeGlow     => GameColorSettings.JudgmentLineColor;
    static Color StripCol     => GameColorSettings.JudgmentLineColor;
    static Color StripBright  => GameColorSettings.JudgmentLineColor;
    static Color JudgeLineCol => GameColorSettings.JudgmentLineColor;
    // 接近グロー (appr): rgba(70,216,232,0) → 0.17
    const float ApprMaxA = 0.17f;
    // 手前断面 #081C2C / 側壁
    static readonly Color FrontFace = new Color(8 / 255f, 28 / 255f, 44 / 255f, 1f);
    static readonly Color WallCol   = new Color(9 / 255f, 24 / 255f, 38 / 255f, 0.92f);

    readonly List<GameObject> _built = new();
    readonly List<Vector3> _v = new();
    readonly List<Color>   _c = new();
    readonly List<int>     _t = new();

    Vector3 _camPos; Quaternion _camRot; float _camFov; float _camAspect; float _lastLL; bool _builtOnce;

    void OnEnable()  { _builtOnce = false; Rebuild(); }
    void OnDisable() { UnhookInput(); Clear(); }

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

        // FAR_WALL_SPEC: 中央レーンの透明遮蔽壁のワールド z をシェーダグローバルへ
        // (ノーツ/小節線のマテリアルが _WallClipOn=1 でこの z より奥をピクセル破棄する)。
        Shader.SetGlobalFloat("_PlayfieldWallZ",
            LaneLayout.JudgmentLineZ + FxSectorGeometry.WallZCenterWorld);

        if (!Application.isPlaying) return;
        if (_input == null) HookInput();
        for (int i = 0; i < 4; i++)
        {
            _capLevel[i] = Mathf.MoveTowards(_capLevel[i], _capHeld[i] ? 1f : 0f, Time.deltaTime / CapFlashFadeSec);
            var mr = _capFlash[i];
            if (mr == null) continue;
            bool on = _capLevel[i] > 0.01f;
            if (mr.enabled != on) mr.enabled = on;
            if (!on) continue;
            _pb ??= new MaterialPropertyBlock();
            _pb.SetColor(IdBaseColor, new Color(1f, 1f, 1f, _capLevel[i]));
            mr.SetPropertyBlock(_pb);
        }
    }

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

    static int CapIndex(LaneRef lane) => lane switch
    {
        LaneRef.Lane0 => 0, LaneRef.Lane1 => 1, LaneRef.Lane2 => 2, LaneRef.Lane3 => 3, _ => -1
    };
    void OnLaneDown(LaneRef lane, double timeMs) { int i = CapIndex(lane); if (i >= 0) _capHeld[i] = true; }
    void OnLaneUp(LaneRef lane, double timeMs)   { int i = CapIndex(lane); if (i >= 0) _capHeld[i] = false; }

    // ── メッシュ構築ヘルパー ────────────────────────────────────────────────

    void MeshClear() { _v.Clear(); _c.Clear(); _t.Clear(); }

    /// <summary>
    /// y0→y1 を rows 分割した縦ストリップ。xL(y)〜xR(y) の間を張り、color(y) で塗る。
    /// 頂点はプレイフィールドローカル座標 → 床ワールド座標。
    /// </summary>
    void AddStrip(float y0, float y1, System.Func<float, float> xL, System.Func<float, float> xR,
                  System.Func<float, Color> color, int rows, float yLift)
    {
        int baseIndex = _v.Count;
        for (int i = 0; i <= rows; i++)
        {
            float y = Mathf.Lerp(y0, y1, (float)i / rows);
            // 仕様の色は sRGB 値。リニア色空間では頂点色が変換されないため変換して渡す。
            var col = color(y);
            if (QualitySettings.activeColorSpace == ColorSpace.Linear) col = col.linear;
            _v.Add(FxSectorGeometry.CenterFloorPoint(xL(y), y, yLift));
            _c.Add(col);
            _v.Add(FxSectorGeometry.CenterFloorPoint(xR(y), y, yLift));
            _c.Add(col);
        }
        for (int i = 0; i < rows; i++)
        {
            int a = baseIndex + i * 2;
            _t.Add(a); _t.Add(a + 2); _t.Add(a + 1);
            _t.Add(a + 1); _t.Add(a + 2); _t.Add(a + 3);
        }
    }

    // モック §0 のレイヤー順を sortingOrder で固定する (等キュー距離ソートは
    // Game View と RT レンダで結果が揺れるため信頼しない)。
    MeshRenderer Spawn(string name, Material mat, int order)
    {
        if (mat == null) return null;
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.hideFlags = HideFlags.DontSave;
        go.transform.SetParent(transform, false);
        var mesh = new Mesh { name = name };
        mesh.SetVertices(_v);
        mesh.SetColors(_c);
        mesh.SetTriangles(_t, 0);
        mesh.RecalculateBounds();
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.sortingOrder = order;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _built.Add(go);
        return mr;
    }

    static Color WithA(Color c, float a) { c.a = a; return c; }

    /// <summary>床 asf 縦グラデ (y235 → 46% → y620)。暗色面はアルファ補正。
    /// フォグは FAR_WALL_SPEC で廃止 (壁より手前は常に全不透明のグラデ)。</summary>
    static Color FloorColor(float y)
    {
        float g = Mathf.InverseLerp(235f, FxSectorGeometry.CTrackBotY, y);
        var c = g < 0.46f
            ? Color.Lerp(FloorTop, FloorMid, g / 0.46f)
            : Color.Lerp(FloorMid, FloorBot, (g - 0.46f) / 0.54f);
        c.a = FxSectorGeometry.DarkA(c.a);
        return c;
    }

    // ── 生成 ────────────────────────────────────────────────────────────────

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

        // FAR_WALL_SPEC: 奥端は laneLength の壁 (WallY) のハードエッジ。フォグ帯は廃止。
        float yTop = FxSectorGeometry.WallY;
        const float yBot = 620f;
        float HalfW(float y) => FxSectorGeometry.CenterHalfW(y);
        float S(float y) => (y - FxSectorGeometry.CVpY) /
                            (FxSectorGeometry.CTrackBotY - FxSectorGeometry.CVpY); // 太さの距離減衰係数 (y620 → 1)

        // ── 床 (asf グラデ + フォグ帯 y235〜257) ──
        MeshClear();
        AddStrip(yTop, yBot, y => 640f - HalfW(y), y => 640f + HalfW(y), FloorColor, _rows, _yLift);
        // 側壁 (600.9,235)-(400,620)-(358,650) / ミラー
        AddStrip(yTop, 650f,
            y => 640f - Mathf.LerpUnclamped(HalfW(y), 282f, Mathf.Max(0f, (y - yBot) / 30f)),
            y => y <= yBot ? 640f - HalfW(y) : 640f - Mathf.Lerp(240f, 282f, (y - yBot) / 30f),
            y => WithA(WallCol, FxSectorGeometry.DarkA(WallCol.a)), _rows, _yLift - 0.002f);
        AddStrip(yTop, 650f,
            y => y <= yBot ? 640f + HalfW(y) : 640f + Mathf.Lerp(240f, 282f, (y - yBot) / 30f),
            y => 640f + Mathf.LerpUnclamped(HalfW(y), 282f, Mathf.Max(0f, (y - yBot) / 30f)),
            y => WithA(WallCol, FxSectorGeometry.DarkA(WallCol.a)), _rows, _yLift - 0.002f);
        // 手前断面 (400,620)-(880,620)-(922,650)-(358,650) #081c2c
        AddStrip(yBot, 650f,
            y => 640f - Mathf.Lerp(240f, 282f, (y - yBot) / 30f),
            y => 640f + Mathf.Lerp(240f, 282f, (y - yBot) / 30f),
            _ => FrontFace, 3, _yLift - 0.001f);
        // 接近グロー (appr): y490→620、レーン縁の内側、alpha 0 → 0.17
        AddStrip(490f, yBot, y => 640f - HalfW(y), y => 640f + HalfW(y),
            y => WithA(StripCol, FxSectorGeometry.BrightA(ApprMaxA * Mathf.InverseLerp(490f, yBot, y))), 10, _yLift + 0.001f);
        Spawn("CtFloor", _faceMat, 30);

        // ── レーン縁: 発光は必ず「Glow + Core」二層 (COLOR_SPEC 実装ルール)。
        //    Glow = 広いソフト帯 + 明るい狭帯 (加算・生アルファ)、Core は下の CtLines で通常合成 ──
        MeshClear();
        foreach (float sx in new[] { -1f, 1f })
        {
            AddStrip(yTop, yBot,
                y => 640f + sx * HalfW(y) - 6f * S(y),
                y => 640f + sx * HalfW(y) + 6f * S(y),
                y => WithA(EdgeGlow, 0.22f * S(y)), _rows, _yLift + 0.0035f);
            AddStrip(yTop, yBot,
                y => 640f + sx * HalfW(y) - 3f * S(y),
                y => 640f + sx * HalfW(y) + 3f * S(y),
                y => WithA(EdgeGlow, 0.55f * S(y)), _rows, _yLift + 0.004f);
        }
        Spawn("CtEdgeGlow", _glowMat, 31);

        MeshClear();
        foreach (float sx in new[] { -1f, 1f })
        {
            AddStrip(yTop, yBot,
                y => 640f + sx * HalfW(y) - 1.2f * S(y),
                y => 640f + sx * HalfW(y) + 1.2f * S(y),
                y => WithA(EdgeCore, 0.95f * S(y)), _rows, _yLift + 0.006f);
        }
        // ── サブレーン分割線: VP→(520,620) / VP→(760,620)、α0.45 ──
        foreach (float bx in new[] { 520f, 760f })
        {
            float off = bx - 640f; // レーン全幅 ±240 に対する分割位置
            AddStrip(yTop, yBot,
                y => 640f + off * HalfW(y) / 240f - 0.8f * S(y),
                y => 640f + off * HalfW(y) / 240f + 0.8f * S(y),
                y => WithA(EdgeCore, 0.45f * S(y)), _rows, _yLift + 0.005f);
        }
        // ── 中央のレーン線 (x=640、実線・幅 1.4 — K 指示 2026-07-30: 破線をやめ、壁まで途切れず描く) ──
        AddStrip(yTop, yBot, yy => 640f - 0.7f * S(yy), yy => 640f + 0.7f * S(yy),
            yy => WithA(JudgeLineCol, 0.45f * S(yy)), _rows, _yLift + 0.005f);
        Spawn("CtLines", _lineMat, 32);

        // ── 判定ストリップ: P-Glow のブラー入り幅広ストリップ (Glow 層、加算・生アルファ)。
        //    本体 y600-620 は opacity 0.12→0.55→1.0 の縦グラデ、上下に σ≈5px 相当のガウス裾。
        //    コア (P-Core 幅2px) は下の CtJudgeCore が通常合成で重なる ──
        MeshClear();
        AddStrip(590f, 600f,
            y => Mathf.Lerp(407f, 414f, (y - 590f) / 10f) - 7f,
            y => Mathf.Lerp(873f, 866f, (y - 590f) / 10f) + 7f,
            y => { float t = (y - 590f) / 10f; return WithA(StripCol, 0.12f * t * t); }, 6, _yLift + 0.0065f);
        AddStrip(600f, 620f,
            y => Mathf.Lerp(414f, 400f, (y - 600f) / 20f),
            y => Mathf.Lerp(866f, 880f, (y - 600f) / 20f),
            y =>
            {
                float g = (y - 600f) / 20f; // strip: 0.12 → 0.55@70% → 1.0
                return g < 0.7f
                    ? WithA(StripCol, Mathf.Lerp(0.12f, 0.55f, g / 0.7f))
                    : Color.Lerp(WithA(StripCol, 0.55f), StripBright, (g - 0.7f) / 0.3f);
            }, 8, _yLift + 0.007f);
        AddStrip(620f, 628f,
            y => 400f - (y - 620f) * 1.4f,
            y => 880f + (y - 620f) * 1.4f,
            y => { float t = 1f - (y - 620f) / 8f; return WithA(StripCol, 0.9f * t * t); }, 5, _yLift + 0.0065f);
        Spawn("CtJudgeStrip", _glowMat, 33);

        // 判定線 (BUTTONS_SPEC §3): Glow 層 = P-Glow 幅16・ガウス・opacity0.95 (加算) +
        // Core 層 = P-Core 幅2.5 (通常合成)。くっきりした太線にしない。
        MeshClear();
        AddStrip(592f, 608f, y => 414f, y => 866f,
            y => { float x = (y - 600f) / 8f; return WithA(StripCol, 0.95f * Mathf.Exp(-2f * x * x)); },
            10, _yLift + 0.008f);
        Spawn("CtJudgeLineGlow", _glowMat, 33);
        MeshClear();
        AddStrip(598.75f, 601.25f, y => 414f, y => 866f, _ => JudgeLineCol, 1, _yLift + 0.009f);
        Spawn("CtJudgeCore", _lineMat, 34);

        // ── デッキ (BUTTONS_SPEC §5): 台形パネル廃止。画面全幅の影バンドのみ。
        //    rect (0,648)〜(1280,720)、縦グラデ rgba(4,14,24): 0 → 0.72@45% → 0.94@100% ──
        MeshClear();
        AddStrip(648f, 720f, _ => -120f, _ => 1400f,
            y =>
            {
                float t = (y - 648f) / 72f;
                float a = t < 0.45f ? Mathf.Lerp(0f, 0.72f, t / 0.45f)
                                    : Mathf.Lerp(0.72f, 0.94f, (t - 0.45f) / 0.55f);
                return new Color(4 / 255f, 14 / 255f, 24 / 255f, FxSectorGeometry.DarkA(a));
            }, 14, _yLift + 0.010f);
        Spawn("CtDeckShadow", _faceMat, 35);

        // ── D/F/J/K キーキャップ (BUTTONS_SPEC §1: 遠近台形・角 join round 相当) ──
        // 頂点 (時計回り: 左上→右上→右下→左下)、面 rgba(4,16,26,0.55)、枠 1.5px、
        // 上辺アクセント y=660.5 (両端3px内側・P-Glow 0.55・1.5px)、押下フラッシュ。
        var capVerts = new Vector2[4][]
        {
            new Vector2[] { new(384.3f, 658f), new(500.2f, 658f), new(486.4f, 702f), new(356.9f, 702f) },
            new Vector2[] { new(516.2f, 658f), new(632f, 658f),   new(632f, 702f),   new(502.4f, 702f) },
            new Vector2[] { new(648f, 658f),   new(763.8f, 658f), new(777.6f, 702f), new(648f, 702f) },
            new Vector2[] { new(779.8f, 658f), new(895.7f, 658f), new(923.1f, 702f), new(793.6f, 702f) },
        };
        var capFace   = WithA(new Color(4 / 255f, 16 / 255f, 26 / 255f, 1f), FxSectorGeometry.DarkA(0.55f));
        var capBorder = WithA(new Color(225 / 255f, 244 / 255f, 248 / 255f, 1f), FxSectorGeometry.BrightA(0.5f));
        var capAccent = WithA(StripCol, FxSectorGeometry.BrightA(0.55f)); // P-Glow
        for (int i = 0; i < 4; i++)
        {
            var v = capVerts[i];
            // 面
            MeshClear();
            AddQuad(v[0], v[1], v[2], v[3], capFace, _yLift + 0.011f);
            Spawn($"CtCapFace{i}", _faceMat, 36);
            // 枠 (1.5px 内側オフセットとの間のストリップ)
            var inner2 = InsetQuad(v, 1.5f);
            MeshClear();
            AddQuadBorder(v, inner2, capBorder, _yLift + 0.012f);
            Spawn($"CtCapLine{i}", _lineMat, 37);
            // 上辺アクセント (y=660.5、両端 3px 内側、太さ 1.5)
            float tL = Mathf.Lerp(v[0].x, v[3].x, (660.5f - 658f) / (702f - 658f)) + 3f;
            float tR = Mathf.Lerp(v[1].x, v[2].x, (660.5f - 658f) / (702f - 658f)) - 3f;
            MeshClear();
            AddStrip(659.75f, 661.25f, _ => tL, _ => tR, _ => capAccent, 1, _yLift + 0.012f);
            Spawn($"CtCapAccent{i}", _lineMat, 37);
            // 押下フラッシュ: 下→上フェード (0.32→0)、P-Glow (加算)
            MeshClear();
            AddQuadGradient(v, StripCol, 0f, 0.32f, _yLift + 0.0115f);
            var fmr = Spawn($"CtCapFlash{i}", _glowMat, 38);
            if (fmr != null) fmr.enabled = false;
            _capFlash[i] = fmr;
        }
    }

    /// <summary>任意四角形 (時計回り) を追加する。</summary>
    void AddQuad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color col, float yLift)
    {
        int i0 = _v.Count;
        if (QualitySettings.activeColorSpace == ColorSpace.Linear) col = col.linear;
        foreach (var pt in new[] { a, b, c, d })
        {
            _v.Add(FxSectorGeometry.CenterFloorPoint(pt.x, pt.y, yLift));
            _c.Add(col);
        }
        _t.AddRange(new[] { i0, i0 + 1, i0 + 3, i0 + 3, i0 + 1, i0 + 2 });
    }

    /// <summary>四角形の上辺→下辺で alpha を補間するグラデーション面 (押下フラッシュ用)。</summary>
    void AddQuadGradient(Vector2[] v, Color col, float aTop, float aBottom, float yLift)
    {
        int i0 = _v.Count;
        for (int k = 0; k < 4; k++)
        {
            var c = col; c.a = (k < 2) ? aTop : aBottom; // 0,1=上辺 / 2,3=下辺
            if (QualitySettings.activeColorSpace == ColorSpace.Linear) c = c.linear;
            _v.Add(FxSectorGeometry.CenterFloorPoint(v[k].x, v[k].y, yLift));
            _c.Add(c);
        }
        _t.AddRange(new[] { i0, i0 + 1, i0 + 3, i0 + 3, i0 + 1, i0 + 2 });
    }

    /// <summary>凸四角形を全辺 inset px 内側へ縮めた四角形を返す (枠線用)。</summary>
    static Vector2[] InsetQuad(Vector2[] v, float inset)
    {
        var c = (v[0] + v[1] + v[2] + v[3]) * 0.25f;
        var outp = new Vector2[4];
        for (int k = 0; k < 4; k++)
        {
            // 頂点を重心方向へ、隣接2辺の角度を考慮した近似で移動
            var dir = (c - v[k]).normalized;
            outp[k] = v[k] + dir * inset * 1.6f; // 鋭角補正込みの近似
        }
        return outp;
    }

    /// <summary>外周と内周四角形の間をストリップで張る (枠線)。</summary>
    void AddQuadBorder(Vector2[] outer, Vector2[] inner, Color col, float yLift)
    {
        if (QualitySettings.activeColorSpace == ColorSpace.Linear) col = col.linear;
        int i0 = _v.Count;
        for (int k = 0; k < 4; k++)
        {
            _v.Add(FxSectorGeometry.CenterFloorPoint(outer[k].x, outer[k].y, yLift)); _c.Add(col);
            _v.Add(FxSectorGeometry.CenterFloorPoint(inner[k].x, inner[k].y, yLift)); _c.Add(col);
        }
        for (int k = 0; k < 4; k++)
        {
            int a = i0 + k * 2, b = i0 + ((k + 1) % 4) * 2;
            _t.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
        }
    }

    void Clear()
    {
        for (int i = 0; i < 4; i++) _capFlash[i] = null;
        foreach (var go in _built)
            if (go != null)
            {
                if (Application.isPlaying) Destroy(go);
                else DestroyImmediate(go);
            }
        _built.Clear();
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var c = transform.GetChild(i).gameObject;
            if (c.name.StartsWith("Ct"))
            {
                if (Application.isPlaying) Destroy(c);
                else DestroyImmediate(c);
            }
        }
    }
}
