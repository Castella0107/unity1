using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// プレイフィールド 画面設計 (Downloads/画面設計モック(2)) を GamePlay シーンへ段階適用する
/// 再実行可能ビルダー。各ステージは冪等 (既存の生成物を消して作り直す)。
/// デザイン基準: 1280x720 モック (export-src.html)。色・座標はモックの値を踏襲。
/// </summary>
public static class PlayfieldRedesignBuilder
{
    private const string ArtDir = "Assets/_Project/Art/Playfield";

    // ── design tokens (export-src.html) ──────────────────────────────────────
    private static readonly Color BgDeep     = FromHex("06182A");
    private static readonly Color BgDim      = new Color(3 / 255f, 14 / 255f, 26 / 255f, 0.28f);
    private static readonly Color CyanEdge   = FromHex("35D8E8");
    private static readonly Color CyanBright = FromHex("7DEEFA");
    private static readonly Color CyanPale   = FromHex("AEF4FA");
    private static readonly Color MoteTeal   = new Color(96 / 255f, 235 / 255f, 225 / 255f, 0.75f);

    private static Color FromHex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out var c);
        return c;
    }

    private static Sprite LoadSprite(string name)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/{name}.png");
        if (s == null) Debug.LogWarning($"[PlayfieldRedesign] sprite not found: {name}");
        return s;
    }

    private static void Save()
    {
        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
    }

    private static GameObject Recreate(string rootName, Transform parent = null)
    {
        var old = parent == null ? GameObject.Find("/" + rootName)
                                 : parent.Find(rootName)?.gameObject;
        if (old != null) Object.DestroyImmediate(old);
        var go = new GameObject(rootName);
        if (parent != null) go.transform.SetParent(parent, false);
        return go;
    }

    // ═════════════════════════════ 1. Camera framing ════════════════════════
    // CAMERA_SPEC (2026-07-30): 消失点を画面上部へ — screen (640,160) = ローカル (640,66.47)。
    // 中央レーン射影 (VP(640,66.47)・judgeY=600、外側 0.85 縮小) にカメラ射影が一致するよう、
    // 3 つのターゲット投影点 (判定線上のレーン両縁 / 消失点) への座標降下法で
    // (高さ, 奥行き, 俯角) を数値的に解く。
    // FOV は 79.016 に固定 (CAMERA_SPEC: ズームで代用しない)。判定線の幅・位置と FOV を
    // 固定するため、カメラ〜判定線の距離も実質固定になる (手前は不変・奥だけが上へ伸びる)。
    //
    // world→spec 対応: レーン縁 x=±2.0 ↔ ローカル ±229.6@judgeY。
    // 収束後、実カメラ射影から遠近モデル near を 2 点プローブで再導出してログ出力する
    // (FxSectorGeometry.CNear へ転写 — s(z) を FX/中央で厳密共有し小節線の継ぎ目を保つため)。

    // FieldScale 0.76 (K 指示 2026-07-30: カメラをもう少し遠くから — 判定幅×0.894 → 距離×1.118)。
    // 固定点 (640,690) 縮小: off = (1-0.76)·(640,690) = (153.6, 165.6)。VP は screen 120 を維持。
    const float FieldScale = 0.76f, FieldOffX = 153.6f, FieldOffY = 165.6f;
    const float TargetVpLocalY = -60f;   // screen 120 = 165.6 + 0.76 × (-60)

    [MenuItem("Tools/Playfield/1. Camera Framing")]
    public static void ApplyCameraFraming()
    {
        var cam = Camera.main;
        if (cam == null) { Debug.LogError("[PlayfieldRedesign] Main Camera not found"); return; }
        Undo.RecordObject(cam.transform, "Playfield Camera Framing");
        Undo.RecordObject(cam, "Playfield Camera Framing");

        cam.nearClipPlane   = 0.05f;
        cam.farClipPlane    = 120f;
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = BgDeep;

        // ── ターゲット (world → 実効スクリーン px 1280x720・Y下向き) ──
        // ローカル→実効: eff = (96,103.5) + 0.85·local
        var targets = new (Vector3 world, Vector2 specPx)[]
        {
            (new Vector3(-2f, 0f, -0.5f), Eff(640f - 229.6f, 600f)),      // 判定線の左レーン縁
            (new Vector3( 2f, 0f, -0.5f), Eff(640f + 229.6f, 600f)),      // 右レーン縁
            (new Vector3( 0f, 0f, 5000f), Eff(640f, TargetVpLocalY)),     // 消失点
        };

        // ── 固定 16:9 の数式射影 (Game ビューのアスペクトに依存しない) ──
        // スペックは 1280x720 (16:9) 基準。cam.WorldToViewportPoint は現在のウィンドウ比率に
        // 依存して水平スケールが変わるため使わない (aspect 2.22 の環境で誤収束した実績あり)。
        const float Fov = 79.016f;   // 固定 (CAMERA_SPEC: ズームで代用しない)
        float tanHalfV = Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad);
        Vector2 ProjectPx(Vector3 world, float camY, float camZ, float pitchDeg)
        {
            float th = pitchDeg * Mathf.Deg2Rad;
            var d = new Vector3(world.x, world.y - camY, world.z - camZ);
            float vy = Mathf.Cos(th) * d.y + Mathf.Sin(th) * d.z;
            float vz = -Mathf.Sin(th) * d.y + Mathf.Cos(th) * d.z;
            float ndcY = vy / (vz * tanHalfV);
            float ndcX = d.x / (vz * tanHalfV * (16f / 9f));
            return new Vector2((0.5f + ndcX * 0.5f) * 1280f, (1f - (0.5f + ndcY * 0.5f)) * 720f);
        }

        // ── 座標降下法: P = (y, z, pitch) を順に微調整して二乗誤差を最小化 (FOV 固定) ──
        // 初期値 = 旧解 (y3.696 z-4.118 pitch15.478) を判定線点 J(0,0,-0.5) まわりに
        // Δpitch≈9.1° オービットした概算。判定線の幅・位置と FOV の固定により距離も実質固定。
        // ※実行時カメラは StageInitializer.ApplyCameraAngle が同値を絶対設定する (要同期)。
        var P = new float[] { 4.960f, -3.481f, 28.85f };
        float Err()
        {
            float e = 0f;
            foreach (var (world, specPx) in targets)
                e += (ProjectPx(world, P[0], P[1], P[2]) - specPx).sqrMagnitude;
            return e;
        }
        float best = Err();
        var steps = new float[] { 3f, 1f, 0.3f, 0.1f, 0.03f, 0.01f, 0.003f, 0.001f, 0.0003f };
        foreach (float step in steps)
            for (int iter = 0; iter < 120; iter++)
            {
                bool improved = false;
                for (int p = 0; p < 3; p++)
                    foreach (float dir in new[] { step, -step })
                    {
                        float old = P[p];
                        P[p] = old + dir;
                        float e = Err();
                        if (e < best - 1e-9f) { best = e; improved = true; }
                        else P[p] = old;
                    }
                if (!improved) break;
            }

        // 解をシーンのカメラへ書き戻す
        cam.transform.position    = new Vector3(0f, P[0], P[1]);
        cam.transform.eulerAngles = new Vector3(P[2], 0f, 0f);
        cam.fieldOfView           = Fov;

        // ── 遠近モデル near の再導出 (Möbius 射影は VP/judge 固定なら 1 パラメータ = near。
        //    2 点プローブが一致すればモデルはカメラと厳密整合) ──
        float NearWorldFrom(float zWorld)
        {
            float yLocal = (ProjectPx(new Vector3(0f, 0f, zWorld), P[0], P[1], P[2]).y - FieldOffY) / FieldScale;
            float s      = (yLocal - TargetVpLocalY) / (600f - TargetVpLocalY);
            float zOff   = zWorld + 0.5f;   // 判定線 z=-0.5 起点
            return zOff * s / (1f - s);
        }
        float n1 = NearWorldFrom(4.122f), n2 = NearWorldFrom(12f);
        float judgeDist = Vector3.Distance(cam.transform.position, new Vector3(0f, 0f, -0.5f));

        Save();
        Debug.Log($"[PlayfieldRedesign] 1. Camera framing solved: y={P[0]:F4} z={P[1]:F4} pitch={P[2]:F4} fov={Fov} err={best:F5}px² judgeDist={judgeDist:F3}");
        Debug.Log($"[PlayfieldRedesign] 1. near_world={n1:F4}/{n2:F4} → FxSectorGeometry.CNear = {n1 * 31.8f:F2}px (CPxPerWorld=31.8 のまま)");
    }

    /// <summary>プレイフィールドローカル座標 → 実効スクリーン px (SPEC §0 の FieldScale 縮小)。</summary>
    private static Vector2 Eff(float xLocal, float yLocal)
        => new Vector2(FieldOffX + FieldScale * xLocal, FieldOffY + FieldScale * yLocal);

    // ═════════════════════════════ 2. Background ════════════════════════════
    // Screen Space - Camera の背景キャンバス (planeDistance 大 = 3D の背後に描画):
    //   街アート (aspect-fill) → 暗色オーバーレイ → 消失点グロー。
    // ＋ ティール粒子 (particle_mote) のワールド空間 ParticleSystem。

    [MenuItem("Tools/Playfield/2. Background")]
    public static void ApplyBackground()
    {
        var cam = Camera.main;
        if (cam == null) { Debug.LogError("[PlayfieldRedesign] Main Camera not found"); return; }

        var root = Recreate("PlayfieldBackground");

        // ⚠️ Screen Space - Camera キャンバスは URP で 3D 半透明メッシュより後に描画され
        // プレイフィールド (床/翼) を覆ってしまうため、背景はカメラ子付けのワールド
        // クアッド (遠距離 = 距離ソートで最初に描画) として構築する。
        var rig = new GameObject("BgRig");
        rig.transform.SetParent(cam.transform, false);
        rig.transform.SetParent(root.transform, true); // 位置はカメラ基準で固定 (カメラは固定前提)
        rig.transform.SetPositionAndRotation(cam.transform.position, cam.transform.rotation);

        // 距離 d でのフラスタム全面サイズ
        Vector2 Frustum(float d)
        {
            float h = 2f * d * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            return new Vector2(h * cam.aspect, h);
        }

        GameObject BgQuad(string name, float dist, Vector2 size, Material mat, int order)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(rig.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, dist);
            go.transform.localEulerAngles = Vector3.zero; // Quad は -Z 向きが表 = カメラへ向く
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.sortingOrder = order; // モック §0: 背景は最初に描く (sortingOrder で描画順を固定)
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        // 背景マテリアル: Sprites/Default + カスタムキュー 2997〜2999。
        // URP シェーダはインポート時に renderQueue が正規化される (3000 に戻る) ため、
        // 正規化対象外の Sprites/Default で「全プレイフィールド (3000) より先に描く」を保証する。
        Material BgMat(string assetName, string texName, Color color, int queue)
        {
            var path = $"Assets/_Project/Materials/{assetName}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Playfield/Alpha"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = Shader.Find("Playfield/Alpha");
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            m.color = color;
            m.mainTexture = texName != null
                ? AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtDir}/{texName}.png") : null;
            m.renderQueue = queue;
            EditorUtility.SetDirty(m);
            return m;
        }

        // ── city artwork (aspect-fill、d=110) ──
        var cityTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtDir}/bg_city.png");
        float texAspect = cityTex != null ? (float)cityTex.width / cityTex.height : 16f / 9f;
        var f110 = Frustum(110f);
        var citySize = f110 * 1.02f; // 端の隙間保険
        if (texAspect > citySize.x / citySize.y) citySize.x = citySize.y * texAspect;   // envelope-fill
        else                                     citySize.y = citySize.x / texAspect;
        BgQuad("BgCity", 110f, citySize, BgMat("PlayfieldBgCity", "bg_city", Color.white, 2600), 0);

        // ── dark overlay rgba(3,14,26,0.28)、d=109 ──
        // sRGB 合成 (モック) とリニア合成の差を補正: a' = 1−(1−0.28)^2.2 ≈ 0.514
        BgQuad("BgDim", 109f, Frustum(109f) * 1.02f,
               BgMat("PlayfieldBgDim", null, new Color(3 / 255f, 14 / 255f, 26 / 255f, 0.514f), 2601), 1);

        // 消失点グロー (VanishGlow) は K 指示 (2026-07-30) により廃止 — 背景は街アート+暗色オーバーレイのみ。

        // ── drifting teal motes (world space) ──
        BuildMotes(root.transform);

        // ── ビネット (モック layer3: プレイフィールドの上・セクター表示の下) ──
        // radialGradient cx50% cy40% r72%: 66%→100% で rgba(2,10,18,0)→0.66。
        // テクスチャではなく頂点カラーグリッドメッシュで生成 (アルファ経路が確実)。
        {
            var vigGo = new GameObject("Vignette", typeof(MeshFilter), typeof(MeshRenderer));
            vigGo.transform.SetParent(rig.transform, false);
            vigGo.transform.localPosition = new Vector3(0f, 0f, 90f);
            var f90 = Frustum(90f);
            const int GX = 28, GY = 16;
            var vv = new System.Collections.Generic.List<Vector3>();
            var vc = new System.Collections.Generic.List<Color>();
            var vt = new System.Collections.Generic.List<int>();
            var vigCol = new Color(2 / 255f, 10 / 255f, 18 / 255f, 1f);
            bool lin = QualitySettings.activeColorSpace == ColorSpace.Linear;
            for (int gy = 0; gy <= GY; gy++)
            for (int gx = 0; gx <= GX; gx++)
            {
                float u = gx / (float)GX, v = gy / (float)GY; // v: 0=下, 1=上
                vv.Add(new Vector3((u - 0.5f) * f90.x * 1.02f, (v - 0.5f) * f90.y * 1.02f, 0f));
                float nx = u - 0.5f, ny = v - 0.6f; // cy=40% (上から) = 下から 60%
                float d = Mathf.Sqrt(nx * nx + ny * ny) / 0.72f;
                float t = Mathf.Clamp01((d - 0.66f) / 0.34f);
                float a = 1f - Mathf.Pow(1f - 0.66f * t * t, 2.2f); // sRGB/リニア合成差の補正込み
                var c = vigCol; c.a = a;
                if (lin) c = c.linear;
                vc.Add(c);
            }
            for (int gy = 0; gy < GY; gy++)
            for (int gx = 0; gx < GX; gx++)
            {
                int a0 = gy * (GX + 1) + gx, b0 = a0 + GX + 1;
                vt.AddRange(new[] { a0, b0, a0 + 1, a0 + 1, b0, b0 + 1 });
            }
            var vigMesh = new Mesh { name = "Vignette" };
            vigMesh.SetVertices(vv); vigMesh.SetColors(vc); vigMesh.SetTriangles(vt, 0);
            vigMesh.RecalculateBounds();
            vigGo.GetComponent<MeshFilter>().sharedMesh = vigMesh;
            var vmr = vigGo.GetComponent<MeshRenderer>();
            vmr.sharedMaterial = VertexColorMat("PlayfieldVignette", additive: false, queue: 2643);
            vmr.sortingOrder = 3;
            vmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        Save();
        Debug.Log("[PlayfieldRedesign] 2. Background applied (world quads + vignette)");
    }

    private static void BuildMotes(Transform parent)
    {
        var go = new GameObject("DriftingMotes", typeof(ParticleSystem));
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(0f, 0.4f, 4f);

        var ps   = go.GetComponent<ParticleSystem>();
        var main = ps.main;
        main.loop             = true;
        main.startLifetime    = new ParticleSystem.MinMaxCurve(7f, 13f);
        main.startSpeed       = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
        main.startSize        = new ParticleSystem.MinMaxCurve(0.05f, 0.16f);
        main.startColor       = MoteTeal;
        main.simulationSpace  = ParticleSystemSimulationSpace.World;
        main.maxParticles     = 60;

        var emission = ps.emission;
        emission.rateOverTime = 3.5f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale     = new Vector3(14f, 0.5f, 10f);

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        // ⚠️ x/y/z は全て同じ MinMaxCurve モードで指定すること。z を既定 (Constant) のまま
        // にすると "Particle Velocity curves must all be in the same mode" エラーが出て、
        // コンソールの Error Pause が有効な環境では再生が自動ポーズ (フリーズ状態) になる。
        vel.y       = new ParticleSystem.MinMaxCurve(0.25f, 0.55f); // dustDrift: ゆっくり上昇
        vel.x       = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
        vel.z       = new ParticleSystem.MinMaxCurve(0f, 0f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.6f, 0.12f),
                    new GradientAlphaKey(0.45f, 0.88f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = BuildMoteMaterial();
    }

    private static Material BuildMoteMaterial()
    {
        const string path = "Assets/_Project/Materials/PlayfieldMote.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtDir}/particle_mote.png");
        mat.mainTexture = texture;
        // URP Particles/Unlit: additive
        if (mat.HasProperty("_Surface"))  mat.SetFloat("_Surface", 1f);  // Transparent
        if (mat.HasProperty("_Blend"))    mat.SetFloat("_Blend", 2f);    // Additive
        if (mat.HasProperty("_BaseMap"))  mat.SetTexture("_BaseMap", texture);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        mat.renderQueue = 3000;
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }

    // ═════════════════════════════ 3. Center track ══════════════════════════
    // 中央 4 レーンのトラック: 暗い路面 + 明るいエッジ + 奥の発光ゲート + 判定ストリップ。
    // 既存の灰色 Ground は無効化し、x∈[-2,2] / z∈[-1.5,22] のトラック床に置き換える。

    // 画面設計モック(4) SPEC §2: 中央トラックはスクリーン仕様座標駆動の
    // CenterTrackVisuals (ExecuteAlways) が床デカールとして生成する。ここでは旧 3D クアッド
    // 群の撤去・旧レーン分割線 (LaneVisuals) の停止・マテリアル割当のみを行う。

    [MenuItem("Tools/Playfield/3. Center Track")]
    public static void ApplyCenterTrack()
    {
        // 旧 Ground / JudgmentLine の見た目は撤去 (オブジェクトは残すが renderer を無効化)
        var ground = GameObject.Find("/Ground");
        if (ground != null && ground.TryGetComponent<MeshRenderer>(out var gr)) gr.enabled = false;
        var oldLine = GameObject.Find("/JudgmentLine");
        if (oldLine != null && oldLine.TryGetComponent<MeshRenderer>(out var jr)) jr.enabled = false;

        // 旧 LaneVisuals (3D レーン分割線) は SPEC §2 の VP 収束分割線と二重になるため停止
        var laneVisuals = Object.FindFirstObjectByType<LaneVisuals>(FindObjectsInactive.Include);
        if (laneVisuals != null && laneVisuals.gameObject.activeSelf)
        {
            laneVisuals.gameObject.SetActive(false);
            Debug.Log("[PlayfieldRedesign] LaneVisuals deactivated (replaced by CenterTrackVisuals)");
        }

        var root = Recreate("PlayfieldTrack");
        var vis  = root.AddComponent<CenterTrackVisuals>();
        var so = new SerializedObject(vis);
        so.FindProperty("_faceMat").objectReferenceValue = VertexColorMat("PlayfieldCtFace", additive: false, queue: 2630);
        so.FindProperty("_glowMat").objectReferenceValue = VertexColorMat("PlayfieldCtGlow", additive: true,  queue: 2631);
        so.FindProperty("_lineMat").objectReferenceValue = VertexColorMat("PlayfieldCtLine", additive: false, queue: 2632);
        so.ApplyModifiedPropertiesWithoutUndo();

        // 中央ノーツのマテリアル: 不透明 (queue 2000) のままだと透明デカール床 (2630) に
        // 上書きされるため、Playfield/Alpha + queue 2640 (床より上) の透明へ変換する。
        // これで NoteController の奥フェード (アルファ) も効く。
        var noteMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Notes/NoteUnlit.mat");
        if (noteMat != null)
        {
            noteMat.shader = Shader.Find("Playfield/Alpha");
            noteMat.mainTexture = null;
            if (noteMat.HasProperty("_BaseColor")) noteMat.SetColor("_BaseColor", Color.white);
            noteMat.renderQueue = 2640;
            noteMat.SetFloat("_WallClipOn", 1f);   // FAR_WALL_SPEC: 壁より奥をピクセル破棄
            EditorUtility.SetDirty(noteMat);
        }

        AssetDatabase.SaveAssets();
        vis.Rebuild();
        Save();
        Debug.Log("[PlayfieldRedesign] 3. Center track applied (spec-driven CenterTrackVisuals)");
    }

    private const float LaneLayoutFar    = 22f;    // LaneLayout.NoteSpawnZ
    private const float LaneLayoutJudgeZ = -0.5f;  // LaneLayout.JudgmentLineZ

    private static GameObject Quad(Transform parent, string name, Vector3 pos, Vector3 euler, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition    = pos;
        go.transform.localEulerAngles = euler;
        go.transform.localScale       = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    private static Material SolidMat(string assetName, Color color, bool transparent)
    {
        var path = $"Assets/_Project/Materials/{assetName}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        if (transparent)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f); // alpha
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3000;
        }
        mat.SetColor("_BaseColor", color);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material SpriteMat(string assetName, string spriteName)
    {
        var path = $"Assets/_Project/Materials/{assetName}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetFloat("_Surface", 1f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = 3000;
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtDir}/{spriteName}.png");
        mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ═════════════════════════════ 4. FX arc lanes ══════════════════════════
    // PLAYFIELD_SPEC.md §3: FX レーンは消失点 VP(640,256) 中心の「扇形 (円弧セクター)」。
    // 直線・平行四辺形・ベジェリボンで実装してはならない (§0)。生成は FxLaneVisuals
    // (ExecuteAlways) がスクリーン仕様座標を床へ逆投影して行う。ここでは旧ベジェパス
    // (誤実装) の撤去とマテリアル割当のみ。

    [MenuItem("Tools/Playfield/4. FX Arc Lanes")]
    public static void ApplyFxLanes()
    {
        // 旧 FxLanePath (2 次ベジェ) 方式の残骸を撤去する
        var oldPath = Object.FindFirstObjectByType<FxLanePath>();
        if (oldPath != null)
        {
            Debug.Log("[PlayfieldRedesign] removing obsolete FxLanePath: " + oldPath.gameObject.name);
            Object.DestroyImmediate(oldPath.gameObject);
        }

        var root = Recreate("PlayfieldFxLanes");
        var vis  = root.AddComponent<FxLaneVisuals>();

        var so = new SerializedObject(vis);
        so.FindProperty("_faceMat").objectReferenceValue      = VertexColorMat("PlayfieldFxFace",      additive: false, queue: 2610);
        so.FindProperty("_glowMat").objectReferenceValue      = VertexColorMat("PlayfieldFxGlow",      additive: true,  queue: 2613);
        so.FindProperty("_lineMat").objectReferenceValue      = VertexColorMat("PlayfieldFxLine",      additive: false, queue: 2614);
        so.FindProperty("_flashMat").objectReferenceValue     = VertexColorMat("PlayfieldFxFlash",     additive: true,  queue: 2615);
        so.FindProperty("_noteGlowMat").objectReferenceValue  = VertexColorMat("PlayfieldFxNoteGlow",  additive: true,  queue: 2616);
        so.FindProperty("_noteMat").objectReferenceValue      = VertexColorMat("PlayfieldFxNote",      additive: false, queue: 2617);
        so.FindProperty("_judgeGlowMat").objectReferenceValue = VertexColorMat("PlayfieldFxJudgeGlow", additive: true,  queue: 2618);
        so.FindProperty("_judgeCoreMat").objectReferenceValue = VertexColorMat("PlayfieldFxJudgeCore", additive: false, queue: 2619);
        so.FindProperty("_btnFillMat").objectReferenceValue = VertexColorMat("PlayfieldFxBtnFill", additive: false, queue: 2620);
        so.FindProperty("_btnLineMat").objectReferenceValue = VertexColorMat("PlayfieldFxBtnLine", additive: false, queue: 2621);
        so.FindProperty("_keySMat").objectReferenceValue = KeycapMat("Playfield_key_S", "key_S", 2622);
        so.FindProperty("_keyLMat").objectReferenceValue = KeycapMat("Playfield_key_L", "key_L", 2622);
        so.ApplyModifiedPropertiesWithoutUndo();

        AssetDatabase.SaveAssets();
        vis.Rebuild();
        Save();
        Debug.Log("[PlayfieldRedesign] 4. FX arc lanes applied (VP-centered sector)");
    }

    /// <summary>
    /// 頂点カラー対応の透過マテリアル (URP Particles/Unlit)。FX セクターのグラデーション・
    /// フェードは頂点色で表現するため、白ベース × 頂点色。左右ミラーで巻き順が反転するので
    /// Cull Off。additive はグロー/フラッシュ用。
    /// </summary>
    /// <summary>キーキャップ用: Playfield/Alpha + モックのキー素材テクスチャ。</summary>
    private static Material KeycapMat(string assetName, string texName, int queue)
    {
        var path = $"Assets/_Project/Materials/{assetName}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find("Playfield/Alpha");
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = shader;
        mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtDir}/{texName}.png");
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        mat.renderQueue = queue;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // 自作の Playfield/* シェーダを使う。URP 標準シェーダはインポート時に
    // renderQueue が正規化される (カスタムキューが 3000 に戻る) ため、素の unlit
    // シェーダ + renderQueue 直指定で「モック §0 のレイヤー順」を厳密に固定する。
    private static Material VertexColorMat(string assetName, bool additive, int queue)
    {
        var path = $"Assets/_Project/Materials/{assetName}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find(additive ? "Playfield/Additive" : "Playfield/Alpha");
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = shader;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        mat.mainTexture = null;
        mat.renderQueue = queue;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ═════════════════════════════ 5. HUD restyle (プレイフィールド) ═══════════════════
    // HudSceneBuilder が構築・結線した HUD を プレイフィールド モックのレイアウトへ再配置する。
    // GameHud / LaneKeyGuide のバインディング (オブジェクト参照) は一切変えない。
    // 座標系: HUD Canvas は 1920x1080 基準 (モック 1280x720 の 1.5 倍)。

    [MenuItem("Tools/Playfield/5. HUD Restyle")]
    public static void ApplyHudRestyle()
    {
        var canvas = GameObject.Find("/Canvas");
        if (canvas == null) { Debug.LogError("[PlayfieldRedesign] Canvas not found"); return; }
        var ct = canvas.transform;

        var cText   = FromHex("E6F4FA");
        var cMuted  = FromHex("7DA2B4");
        var cSub    = FromHex("93B4C4");
        var cLore   = FromHex("5F8BA0");
        var cValue  = FromHex("DCF0F8");
        var cAccent = FromHex("4DD8E6");

        // ── 左コンソール: SongInfoBox ──
        var box = ct.Find("SongInfoBox")?.gameObject;
        if (box != null)
        {
            RT(box, new(0, 1), new(0, 1), new(0, 1), new(45, -51), new(400, 210));
            var img = box.GetComponent<Image>();
            if (img != null) img.color = Color.clear;

            KillChild(box.transform, "LeftRule");
            var rule = NewImage(box.transform, "LeftRule", null, new Color(80 / 255f, 210 / 255f, 230 / 255f, 0.5f));
            var rrt = rule.rectTransform;
            rrt.anchorMin = new(0, 0); rrt.anchorMax = new(0, 1); rrt.pivot = new(0, 1);
            rrt.anchoredPosition = Vector2.zero; rrt.sizeDelta = new(3, 0);

            SetActive(box.transform, "Jacket", false); // モックにジャケット表示は無い

            // バッジ背景 (P-Soft) を Difficulty テキストの背後に敷く
            KillChild(box.transform, "DifficultyBadge");
            var badge = NewImage(box.transform, "DifficultyBadge", null, FromHex("E2B95A")); // P-Soft
            var brt = badge.rectTransform;
            brt.anchorMin = brt.anchorMax = new(0, 1); brt.pivot = new(0, 1);
            brt.anchoredPosition = new(12, -4); brt.sizeDelta = new(112, 32);
            badge.transform.SetAsFirstSibling(); // テキストの背後
            Text(box.transform, "Difficulty", pos: new(20, -8),  size: new(300, 30), font: 20, color: FromHex("06182A"),
                 align: TextAlignmentOptions.TopLeft, bold: true);
            Text(box.transform, "Title",      pos: new(16, -40), size: new(360, 48), font: 39, color: cText,
                 align: TextAlignmentOptions.TopLeft, bold: true);
            Text(box.transform, "Artist",     pos: new(16, -92), size: new(360, 26), font: 18, color: cSub,
                 align: TextAlignmentOptions.TopLeft);

            // 旧: モックのフレーバーテキスト「器 REC.FRAGMENT — 継承済」を LoreLine として
            // 追加していたが、意味不明な表記として K 指摘 (2026-07-30) → 廃止 (既存も削除)。
            KillChild(box.transform, "LoreLine");
        }

        // ── 左コンソール: 判定カウント (横バー → 縦グリッド) ──
        var jBar = ct.Find("JudgmentCountsBar")?.gameObject;
        if (jBar != null)
        {
            RT(jBar, new(0, 1), new(0, 1), new(0, 1), new(48, -270), new(300, 200));
            var img = jBar.GetComponent<Image>();
            if (img != null) img.color = Color.clear;
            var hlg = jBar.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null) Object.DestroyImmediate(hlg);
            if (jBar.GetComponent<VerticalLayoutGroup>() == null)
            {
                var vlg = jBar.AddComponent<VerticalLayoutGroup>();
                vlg.childControlHeight = false; vlg.childForceExpandHeight = false;
                vlg.childControlWidth  = false; vlg.childForceExpandWidth  = false;
                vlg.spacing = 6; vlg.childAlignment = TextAnchor.UpperLeft;
            }

            // COLOR_SPEC「琥珀&深緑」: PERFECT=S-Text #70E1C5 / MISS=#DD705F。
            // PERFECT+ は上位判定として Judge-PERFECT #94F9E0。GREAT/GOOD はテーマ非依存の中立色。
            (string grp, string hex)[] rows =
            {
                ("PERFECT+Group", "94F9E0"), ("PERFECTGroup", "70E1C5"), ("GREATGroup", "7FB8CC"),
                ("GOODGroup", "7DA2B4"), ("MISSGroup", "DD705F"),
            };
            foreach (var (grp, hex) in rows)
            {
                var g = jBar.transform.Find(grp);
                if (g == null) continue;
                g.GetComponent<RectTransform>().sizeDelta = new(290, 32);
                var gh = g.GetComponent<HorizontalLayoutGroup>();
                if (gh != null) { gh.spacing = 12; gh.childAlignment = TextAnchor.MiddleLeft; }
                var lbl = g.Find("Label")?.GetComponent<TextMeshProUGUI>();
                if (lbl != null)
                {
                    lbl.rectTransform.sizeDelta = new(180, 30);
                    lbl.fontSize = 18; lbl.color = FromHex(hex);
                    lbl.alignment = TextAlignmentOptions.MidlineLeft;
                    lbl.characterSpacing = 6;
                }
                var cnt = g.Find("Count")?.GetComponent<TextMeshProUGUI>();
                if (cnt != null)
                {
                    cnt.rectTransform.sizeDelta = new(96, 30);
                    cnt.fontSize = 22;
                    cnt.color = grp == "MISSGroup" ? FromHex("E6B0A8") : cValue; // MISS 数字 = Miss-Soft
                    cnt.alignment = TextAlignmentOptions.MidlineRight;
                }
            }
        }

        // ── 右コンソール: ScorePanel (RATE / SCORE / セクター菱形) ──
        var panel = ct.Find("ScorePanel")?.gameObject;
        if (panel != null)
        {
            RT(panel, new(1, 1), new(1, 1), new(1, 1), new(-45, -51), new(280, 900));

            // ゲージは画面左端 (縦) へ移設・シアン化
            var gauge = panel.transform.Find("GaugeFrame") ?? ct.Find("GaugeFrame");
            if (gauge != null)
            {
                gauge.SetParent(ct, false);
                RT(gauge.gameObject, new(0, 0.5f), new(0, 0.5f), new(0, 0.5f), new(18, 0), new(14, 560));
                var bg = gauge.GetComponent<Image>();
                if (bg != null) bg.color = new Color(4 / 255f, 16 / 255f, 26 / 255f, 0.6f);
                var fill = gauge.Find("GaugeFill")?.GetComponent<Image>();
                if (fill != null) fill.color = CyanEdge;
            }

            Text(panel.transform, "RateLabel",  pos: new(0, 0),    size: new(280, 26), font: 18, color: cMuted,
                 align: TextAlignmentOptions.TopRight, anchorTopRight: true, spacing: 10);
            Text(panel.transform, "RateValue",  pos: new(0, -28),  size: new(280, 58), font: 50, color: cText,
                 align: TextAlignmentOptions.TopRight, anchorTopRight: true, bold: true);
            Text(panel.transform, "ScoreLabel", pos: new(0, -112), size: new(280, 24), font: 18, color: cMuted,
                 align: TextAlignmentOptions.TopRight, anchorTopRight: true, spacing: 10);
            Text(panel.transform, "ScoreValue", pos: new(0, -138), size: new(280, 46), font: 40, color: cText,
                 align: TextAlignmentOptions.TopRight, anchorTopRight: true, bold: true);

            // ── プレイフィールド モック: MAX COMBO (SCORE の下) + SEC 表示 ──
            MakeText(panel.transform, "MaxComboLabel", "MAX COMBO", pos: new(0, -206), size: new(280, 24),
                     font: 18, color: cMuted, align: TextAlignmentOptions.TopRight, anchorTopRight: true, spacing: 10);
            var maxCombo = MakeText(panel.transform, "MaxComboValue", "000", pos: new(0, -232), size: new(280, 42),
                     font: 36, color: FromHex("70E1C5"), align: TextAlignmentOptions.TopRight, anchorTopRight: true, bold: true); // S-Text
            MakeText(panel.transform, "SecLabel", "SEC", pos: new(48, -418), size: new(90, 22),
                     font: 18, color: FromHex("7FC4D8"), align: TextAlignmentOptions.Top, anchorTopRight: true, spacing: 6);
            var secCounter = MakeText(panel.transform, "SectorCounter", "01/05", pos: new(48, -776), size: new(90, 24),
                     font: 18, color: FromHex("FBEED0"), align: TextAlignmentOptions.Top, anchorTopRight: true); // P-Core

            // GameHud へバインド
            var hud = canvas.GetComponent<GameHud>();
            if (hud != null)
            {
                var hso = new SerializedObject(hud);
                hso.FindProperty("_maxComboValue").objectReferenceValue = maxCombo;
                hso.FindProperty("_sectorCounter").objectReferenceValue = secCounter;
                hso.ApplyModifiedPropertiesWithoutUndo();
            }

            // セクター菱形: スクリーン右端固定の縦列 (モック x=1252 → 右から 42px、y=308..484 → -462..-726)
            // 縦の接続線 (モック: y290..498、rgba(120,220,240,0.30))
            KillChild(panel.transform, "SectorLine");
            var secLine = NewImage(panel.transform, "SectorLine", null, new Color(120 / 255f, 220 / 255f, 240 / 255f, 0.30f));
            var slrt = secLine.rectTransform;
            slrt.anchorMin = slrt.anchorMax = new(1, 1); slrt.pivot = new(0.5f, 0.5f);
            slrt.anchoredPosition = new(3, -591); slrt.sizeDelta = new(2, 312);
            // 現在セクターのハロー (P-Glow、Knob スプライトの柔らかい円)
            KillChild(panel.transform, "SectorHalo");
            var halo = NewImage(panel.transform, "SectorHalo", AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
                                new Color(227 / 255f, 176 / 255f, 59 / 255f, 0.40f)); // P-Glow
            var hrt = halo.rectTransform;
            hrt.anchorMin = hrt.anchorMax = new(1, 1); hrt.pivot = new(0.5f, 0.5f);
            hrt.anchoredPosition = new(3, -594); hrt.sizeDelta = new(52, 52);
            halo.enabled = false; // 実行時に GameHud が現在ひし形の位置へ移動して表示
            {
                var hso2 = new SerializedObject(canvas.GetComponent<GameHud>());
                hso2.FindProperty("_sectorHalo").objectReferenceValue = halo;
                hso2.ApplyModifiedPropertiesWithoutUndo();
            }
            for (int s = 1; s <= 5; s++)
            {
                int row = 5 - s; // 上段 = S5
                float y = -462 - row * 66; // モック 308..484 → ×1.5
                var dia = panel.transform.Find($"S{s}Diamond");
                if (dia != null)
                {
                    var rt = dia.GetComponent<RectTransform>();
                    rt.anchorMin = rt.anchorMax = new(1, 1); rt.pivot = new(0.5f, 0.5f);
                    rt.anchoredPosition = new(3, y); rt.sizeDelta = new(26, 26);
                    rt.localRotation = Quaternion.Euler(0, 0, 45);
                }
                var pct = panel.transform.Find($"S{s}Percent");
                if (pct != null)
                {
                    var t = pct.GetComponent<TextMeshProUGUI>();
                    var rt = pct.GetComponent<RectTransform>();
                    rt.anchorMin = rt.anchorMax = new(1, 1); rt.pivot = new(1, 0.5f);
                    rt.anchoredPosition = new(-52, y); rt.sizeDelta = new(150, 30);
                    if (t != null) { t.fontSize = 20; t.alignment = TextAlignmentOptions.MidlineRight; }
                }
                var outc = panel.transform.Find($"S{s}Outcome");
                if (outc != null)
                {
                    var t = outc.GetComponent<TextMeshProUGUI>();
                    var rt = outc.GetComponent<RectTransform>();
                    rt.anchorMin = rt.anchorMax = new(1, 1); rt.pivot = new(1, 0.5f);
                    rt.anchoredPosition = new(-52, y - 24); rt.sizeDelta = new(150, 22);
                    if (t != null) { t.fontSize = 14; t.alignment = TextAlignmentOptions.MidlineRight; }
                }
            }
        }

        // ── 下部 (BUTTONS_SPEC §1/§5): デッキ台形パネルは廃止 (影バンドとキャップ台形は
        //    CenterTrackVisuals がデカールで描画)。HUD 側はキーラベル (TMP) のみを
        //    キャップ視覚中心 (x=432/570/710/848, baseline y=687) に重ねる ──
        KillChild(ct, "DeckPlate");
        var guide = ct.Find("KeyGuide")?.gameObject;
        if (guide != null)
        {
            RT(guide, new(0.5f, 0), new(0.5f, 0), new(0.5f, 0.5f), new(0, 57), new(1200, 70));
            var hlg = guide.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null) Object.DestroyImmediate(hlg); // 手動配置に切替
            var lkg = canvas.GetComponent<LaneKeyGuide>();
            var chipsProp = lkg != null ? new SerializedObject(lkg).FindProperty("_keyChips") : null;
            // 視覚中心 x (ローカル 432/570/710/848 → 1080p オフセット ×1.275)
            float[] xs = { -265.2f, -89.3f, 89.3f, 265.2f };
            for (int i = 0; i < 6; i++)
            {
                var chipRef = chipsProp != null && i < chipsProp.arraySize
                    ? chipsProp.GetArrayElementAtIndex(i).objectReferenceValue as Image : null;
                var chip = chipRef != null ? chipRef.transform
                    : (i < guide.transform.childCount ? guide.transform.GetChild(i) : null);
                if (chip == null) continue;
                bool isFx = i >= 4;
                chip.gameObject.SetActive(true);
                var crt = chip.GetComponent<RectTransform>();
                crt.anchorMin = crt.anchorMax = new(0.5f, 0.5f); crt.pivot = new(0.5f, 0.5f);
                if (!isFx)
                {
                    crt.anchoredPosition = new(xs[i], 0);
                }
                else
                {
                    // S/L ラベルの暫定位置 (エディタで見たとき用のおおよその値)。
                    // ⚠️ 実行時は LaneKeyGuide.PlaceFxLabels がボタンの実位置へ置き直す。
                    //    ボタンはワールド空間メッシュ、ラベルは HUD キャンバスなので、
                    //    固定座標だと 16:9 以外の画面比で必ずズレるため。
                    const float thMid = (FxSectorGeometry.SectorThetaMin
                                       + FxSectorGeometry.SectorThetaMax) * 0.5f;
                    const float rMid  = (FxSectorGeometry.ButtonR0
                                       + FxSectorGeometry.ButtonR1) * 0.5f;
                    float bx = FxSectorGeometry.CVpX + rMid * Mathf.Cos(thMid * Mathf.Deg2Rad);
                    float by = FxSectorGeometry.CVpY + rMid * Mathf.Sin(thMid * Mathf.Deg2Rad);

                    // HUD Canvas は 1920x1080 = プレイフィールド座標 (1280x720) の 1.5 倍。
                    // KeyGuide は画面下から y=57 の位置が原点なのでその差分を引く。
                    float sx = (bx - FxSectorGeometry.CVpX) * 1.5f;       // 左が負
                    if (i == 5) sx = -sx;                                  // 右 (L) はミラー
                    crt.anchoredPosition = new(sx, (720f - by) * 1.5f - 57f);
                }
                crt.sizeDelta = new(140, 60);
                var img = chip.GetComponent<Image>();
                if (img != null) img.enabled = false; // キャップ/ボタン面はデカール側が描く
                var lbl = chip.Find("Label")?.GetComponent<TextMeshProUGUI>();
                if (lbl != null)
                {
                    // FX (S/L) は暗い背景の円環帯上に単独で載るため、メインキーよりひと回り大きく・不透明寄りにする
                    lbl.fontSize = isFx ? 30 : 27;
                    lbl.color = new Color(235 / 255f, 249 / 255f, 251 / 255f, isFx ? 0.95f : 0.88f);
                    lbl.fontStyle = FontStyles.Normal; // Share Tech Mono 風 (細身)
                    // チップ矩形のど真ん中に置く (余白やはみ出しで寄らないよう明示)
                    lbl.alignment      = TextAlignmentOptions.Center;
                    lbl.margin         = Vector4.zero;
                    lbl.enableWordWrapping = false;
                    lbl.overflowMode   = TextOverflowModes.Overflow;
                    var lrt = lbl.rectTransform;
                    lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
                    lrt.pivot     = new Vector2(0.5f, 0.5f);
                    lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
                    lbl.text = new[] { "D", "F", "J", "K", "S", "L" }[i]; // 実行時は RefreshKeyLabels が上書き
                }
            }
        }

        Save();
        Debug.Log("[PlayfieldRedesign] 5. HUD restyle applied");
    }

    // ═════════════════════════ 6. Judgment popup & FX stencils ══════════════
    // 判定ポップアップをモック位置 (中央・縦 70%) と大きさに。FX 判定アークの外側に
    // S / L のステンシル (デザイン素材) を配置。
    // 注: 実際の FX キーバインドはユーザー設定可能 (既定 L/R-SHIFT)。S/L はモック準拠の装飾。

    [MenuItem("Tools/Playfield/6. Judgment Popup & Stencils")]
    public static void ApplyJudgmentAndStencils()
    {
        // ── popup ──
        var popup = GameObject.Find("/Canvas/JudgmentTextPopup") ?? FindInactive("Canvas", "JudgmentTextPopup");
        if (popup != null)
        {
            var rt = popup.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new(0.5f, 0.5f); rt.pivot = new(0.5f, 0.5f);
            rt.anchoredPosition = new(0, -216); // モック top:70% → 1080 基準で中心から -216
            rt.sizeDelta = new(900, 110);
            rt.localScale = Vector3.one; // 前回 Play 途中のスケールが残ることがある
            var tmp = popup.GetComponent<TMPro.TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.transform.localScale = Vector3.one;
                tmp.enableAutoSizing = false;
                tmp.fontSize = 66;
                tmp.fontStyle = TMPro.FontStyles.Bold;
                tmp.characterSpacing = 14; // letter-spacing .14em 相当
                tmp.alignment = TMPro.TextAlignmentOptions.Center;
            }
            popup.SetActive(false); // 実行時は Show() が有効化する。編集中は非表示が正
        }
        else Debug.LogWarning("[PlayfieldRedesign] JudgmentTextPopup not found");

        // ── S / L キーキャップは FxLaneVisuals (Tools/Playfield/4) が円環セグメント位置に
        //    スクリーン空間回転付きで生成する。旧ステンシルは撤去のみ。 ──
        var oldStencils = GameObject.Find("/PlayfieldStencils");
        if (oldStencils != null) Object.DestroyImmediate(oldStencils);

        Save();
        Debug.Log("[PlayfieldRedesign] 6. Judgment popup & stencils applied");
    }

    /// <summary>非アクティブ含めて Canvas 配下の子を探す。</summary>
    private static GameObject FindInactive(string rootName, string childName)
    {
        var root = GameObject.Find("/" + rootName);
        if (root == null) return null;
        var t = root.transform.Find(childName);
        return t != null ? t.gameObject : null;
    }

    private static void RT(GameObject go, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    /// <summary>子 TMP テキストが無ければ生成してから Text() で整形する。</summary>
    private static TextMeshProUGUI MakeText(Transform parent, string child, string content, Vector2 pos, Vector2 size,
                                            float font, Color color, TextAlignmentOptions align,
                                            bool bold = false, bool anchorTopRight = false, float spacing = 0)
    {
        var tr = parent.Find(child);
        if (tr == null)
        {
            var go = new GameObject(child, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.raycastTarget = false;
            tr = go.transform;
        }
        Text(parent, child, pos, size, font, color, align, bold, anchorTopRight, spacing);
        var t = tr.GetComponent<TextMeshProUGUI>();
        t.text = content;
        return t;
    }

    private static void Text(Transform parent, string child, Vector2 pos, Vector2 size, float font, Color color,
                             TextAlignmentOptions align, bool bold = false, bool anchorTopRight = false, float spacing = 0)
    {
        var t = parent.Find(child)?.GetComponent<TextMeshProUGUI>();
        if (t == null) { Debug.LogWarning($"[PlayfieldRedesign] TMP not found: {child}"); return; }
        var rt = t.rectTransform;
        var a = anchorTopRight ? new Vector2(1, 1) : new Vector2(0, 1);
        rt.anchorMin = rt.anchorMax = a; rt.pivot = a;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        t.fontSize = font; t.color = color; t.alignment = align;
        t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        t.characterSpacing = spacing;
    }

    private static void KillChild(Transform parent, string name)
    {
        var c = parent.Find(name);
        if (c != null) Object.DestroyImmediate(c.gameObject);
    }

    private static void SetActive(Transform parent, string name, bool active)
    {
        var c = parent.Find(name);
        if (c != null) c.gameObject.SetActive(active);
    }

    // ═════════════════════ Utility: HUD render mode (capture) ═══════════════
    // Screen Space - Overlay はエディタのカメラキャプチャに写らないため、
    // HUD Canvas を Screen Space - Camera (planeDistance 0.3 = ほぼ最前面) に切り替える。
    // 見た目・レイアウト・入力は Overlay と等価。戻す場合は Overlay へ。

    [MenuItem("Tools/Playfield/HUD Mode - Camera (screenshot 可)")]
    public static void HudModeCamera()
    {
        var canvas = GameObject.Find("/Canvas")?.GetComponent<Canvas>();
        var cam = Camera.main;
        if (canvas == null || cam == null) { Debug.LogError("[PlayfieldRedesign] Canvas or camera not found"); return; }
        canvas.renderMode    = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera   = cam;
        canvas.planeDistance = 0.3f; // near clip 0.05 の直後 = 3D より常に手前
        canvas.sortingOrder  = 100;
        Save();
        Debug.Log("[PlayfieldRedesign] HUD → Screen Space Camera");
    }

    [MenuItem("Tools/Playfield/HUD Mode - Overlay (元に戻す)")]
    public static void HudModeOverlay()
    {
        var canvas = GameObject.Find("/Canvas")?.GetComponent<Canvas>();
        if (canvas == null) { Debug.LogError("[PlayfieldRedesign] Canvas not found"); return; }
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        Save();
        Debug.Log("[PlayfieldRedesign] HUD → Screen Space Overlay");
    }

    // ═════════════════════════════ Utility: GameView 16:9 ═══════════════════

    [MenuItem("Tools/Playfield/Set GameView 16x9")]
    public static void SetGameView16x9()
    {
        const System.Reflection.BindingFlags Any =
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic;

        var asm    = typeof(Editor).Assembly;
        var gvType = asm.GetType("UnityEditor.GameView");
        var gv     = EditorWindow.GetWindow(gvType);
        var szType = asm.GetType("UnityEditor.GameViewSizes");
        var single = typeof(ScriptableSingleton<>).MakeGenericType(szType);
        var sizes  = single.GetProperty("instance").GetValue(null);
        var group  = szType.GetProperty("currentGroup").GetValue(sizes);
        var gType  = group.GetType();
        int count  = (int)gType.GetMethod("GetTotalCount").Invoke(group, null);

        int found = -1;
        var names = new System.Text.StringBuilder();
        for (int i = 0; i < count; i++)
        {
            var size = gType.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
            var text = (string)size.GetType().GetProperty("displayText", Any).GetValue(size);
            names.Append(i).Append(':').Append(text).Append("  ");
            if (found < 0 && text != null && (text.Contains("16:9") || text.Contains("1280x720"))) found = i;
        }
        Debug.Log("[PlayfieldRedesign] GameView sizes: " + names);

        if (found < 0)
        {
            // 1280x720 のカスタム固定解像度を追加して選択
            var sizeType    = asm.GetType("UnityEditor.GameViewSize");
            var sizeTypeEnu = asm.GetType("UnityEditor.GameViewSizeType");
            var ctor = sizeType.GetConstructor(new[] { sizeTypeEnu, typeof(int), typeof(int), typeof(string) });
            var custom = ctor.Invoke(new object[] { System.Enum.ToObject(sizeTypeEnu, 1 /*FixedResolution*/), 1280, 720, "プレイフィールド 720p" });
            gType.GetMethod("AddCustomSize", Any).Invoke(group, new[] { custom });
            szType.GetMethod("SaveToHDD", Any)?.Invoke(sizes, null);
            found = (int)gType.GetMethod("GetTotalCount").Invoke(group, null) - 1;
        }

        gvType.GetMethod("SizeSelectionCallback", Any).Invoke(gv, new object[] { found, null });
        gv.Repaint();
        Debug.Log($"[PlayfieldRedesign] GameView size index → {found}");
    }

    // ── UI helpers ───────────────────────────────────────────────────────────
    private static Image NewImage(Transform parent, string name, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite        = sprite;
        img.color         = color;
        img.raycastTarget = false;
        if (sprite != null) img.SetNativeSize();
        return img;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
