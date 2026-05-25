#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// GamePlay シーンのプレイ時 HUD を構築するエディターオンリーのヘルパー。
/// 左上の楽曲情報ボックス・上中央の判定カウント・左の縦スコアゲージ＋総合 RATE＋
/// セクター達成率(S1..S5 菱形)・下部キーガイド・3D レーンハイライトを生成し、
/// GameHud / LaneKeyGuide / GamePlayController へ結線する。右側は PVP 用に空けておく。
/// </summary>
public static class HudSceneBuilder
{
    const string PrefabDir   = "Assets/_Project/Prefabs/UI/HUD";
    const string MatPath     = "Assets/_Project/Prefabs/UI/HUD/LaneHighlight.mat";
    const string GradTexPath = "Assets/_Project/Prefabs/UI/HUD/LaneHighlightGradient.asset";
    const string ScenePath   = "Assets/_Project/Scenes/GamePlay.unity";

    // Judgment count label colors (P+, P, Gr, Gd, M)
    static readonly Color CGold   = new Color(1.00f, 0.84f, 0.20f);
    static readonly Color CCyan   = new Color(0.80f, 0.95f, 1.00f);
    static readonly Color CGreen  = new Color(0.30f, 1.00f, 0.40f);
    static readonly Color COrange = new Color(1.00f, 0.60f, 0.20f);
    static readonly Color CRed    = new Color(1.00f, 0.35f, 0.35f);
    static readonly Color CDim    = new Color(1f, 1f, 1f, 0.55f);
    static readonly Color CPanelBg = new Color(0f, 0f, 0f, 0.45f);

    static Sprite _uiSprite;

    [MenuItem("Tools/Build GamePlay HUD")]
    public static void Build()
    {
        EnsureFolder("Assets/_Project/Prefabs/UI");
        EnsureFolder(PrefabDir);
        _uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var canvasGO = GameObject.Find("Canvas");
        if (canvasGO == null) { Debug.LogError("[HudSceneBuilder] Canvas not found"); return; }
        var ct = canvasGO.transform;

        // ── Cleanup (idempotent re-runs: remove old & previous-layout objects) ─────
        var oldHud = canvasGO.GetComponent<HudDisplay>();
        if (oldHud != null) Object.DestroyImmediate(oldHud);
        var oldGameHud = canvasGO.GetComponent<GameHud>();
        if (oldGameHud != null) Object.DestroyImmediate(oldGameHud);
        var oldGuide = canvasGO.GetComponent<LaneKeyGuide>();
        if (oldGuide != null) Object.DestroyImmediate(oldGuide);

        var hudTextGO = GameObject.Find("HudText");
        if (hudTextGO != null) hudTextGO.SetActive(false);

        foreach (var n in new[] { "TopBar", "SectorPanel", "BottomBar", "NextSongIndicator",
                                  "SongInfoBox", "JudgmentCountsBar", "ScorePanel", "KeyGuide" })
            DestroyByName(ct, n);
        DestroyExisting("LaneHighlights");

        // ── 1. Song Info Box (top-left) ───────────────────────────────────────────
        var box = Child("SongInfoBox", ct);
        SR(box, V(0,1), V(0,1), V(0,1), V(20,-20), V(560,140));
        Img(box, CPanelBg);

        var jacketGO = Child("Jacket", box.transform);
        SR(jacketGO, V(0,1), V(0,1), V(0,1), V(12,-12), V(116,116));
        var jacketImg = jacketGO.AddComponent<RawImage>();
        jacketImg.color = new Color(0.12f, 0.13f, 0.18f, 1f);

        var titleGO = Child("Title", box.transform);
        SR(titleGO, V(0,1), V(0,1), V(0,1), V(140,-14), V(404,52));
        var titleTMP = T(titleGO, "Song Title", 30, Color.white, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        titleTMP.overflowMode = TextOverflowModes.Ellipsis; titleTMP.enableWordWrapping = false;

        var artistGO = Child("Artist", box.transform);
        SR(artistGO, V(0,1), V(0,1), V(0,1), V(140,-70), V(404,30));
        var artistTMP = T(artistGO, "Artist", 18, new Color(1,1,1,.7f), TextAlignmentOptions.TopLeft);
        artistTMP.overflowMode = TextOverflowModes.Ellipsis; artistTMP.enableWordWrapping = false;

        var diffGO = Child("Difficulty", box.transform);
        SR(diffGO, V(1,0), V(1,0), V(1,0), V(-16,12), V(220,46));
        var diffTMP = T(diffGO, "EX 18", 34, new Color(1,.35f,.35f), TextAlignmentOptions.BottomRight, FontStyles.Bold);

        // ── 2. Judgment Counts Bar (top, right of song box) ────────────────────────
        var jBar = Child("JudgmentCountsBar", ct);
        SR(jBar, V(0,1), V(0,1), V(0,1), V(600,-20), V(1020,72));
        Img(jBar, CPanelBg);
        var jHLG = jBar.AddComponent<HorizontalLayoutGroup>();
        jHLG.childControlHeight = true;  jHLG.childForceExpandHeight = true;
        jHLG.childControlWidth  = true;  jHLG.childForceExpandWidth  = true;
        jHLG.childAlignment = TextAnchor.MiddleCenter;
        jHLG.padding = new RectOffset(24, 24, 6, 6);

        string[] jLabels = { "PERFECT+", "PERFECT", "GREAT", "GOOD", "MISS" };
        Color[]  jCols   = { CGold, CCyan, CGreen, COrange, CRed };
        var counts = new TextMeshProUGUI[5];
        for (int i = 0; i < 5; i++)
        {
            var grp = Child(jLabels[i] + "Group", jBar.transform);
            var gHLG = grp.AddComponent<HorizontalLayoutGroup>();
            gHLG.childControlHeight = false; gHLG.childForceExpandHeight = false;
            gHLG.childControlWidth  = false; gHLG.childForceExpandWidth  = false;
            gHLG.spacing = 8; gHLG.childAlignment = TextAnchor.MiddleCenter;

            var lbl = Child("Label", grp.transform);
            lbl.GetComponent<RectTransform>().sizeDelta = V(i == 0 ? 116 : 96, 40);
            T(lbl, jLabels[i], 18, jCols[i], TextAlignmentOptions.MidlineRight, FontStyles.Bold);

            var cnt = Child("Count", grp.transform);
            cnt.GetComponent<RectTransform>().sizeDelta = V(58, 40);
            counts[i] = T(cnt, "0", 26, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        }

        // ── 3. Score Panel (left) ──────────────────────────────────────────────────
        var panel = Child("ScorePanel", ct);
        SR(panel, V(0,1), V(0,1), V(0,1), V(20,-200), V(380,770));

        // Gauge frame + fill (vertical)
        var gaugeBg = Child("GaugeFrame", panel.transform);
        SR(gaugeBg, V(0,1), V(0,1), V(0,1), V(8,0), V(30,704));
        Img(gaugeBg, new Color(0,0,0,.55f));

        var gaugeFillGO = Child("GaugeFill", gaugeBg.transform);
        SR(gaugeFillGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        var gauge = gaugeFillGO.AddComponent<Image>();
        gauge.sprite     = _uiSprite;
        gauge.type       = Image.Type.Filled;
        gauge.fillMethod = Image.FillMethod.Vertical;
        gauge.fillOrigin = (int)Image.OriginVertical.Bottom;
        gauge.fillAmount = 0f;
        gauge.color      = new Color(0.30f, 0.32f, 0.85f, 1f);

        // SCORE
        var scLbl = Child("ScoreLabel", panel.transform);
        SR(scLbl, V(0,1), V(0,1), V(0,1), V(54,-6), V(96,40));
        T(scLbl, "SCORE", 24, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        var scVal = Child("ScoreValue", panel.transform);
        SR(scVal, V(0,1), V(0,1), V(0,1), V(150,-2), V(220,48));
        var scoreTMP = T(scVal, "0", 36, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

        // RATE
        var rtLbl = Child("RateLabel", panel.transform);
        SR(rtLbl, V(0,1), V(0,1), V(0,1), V(54,-58), V(80,36));
        T(rtLbl, "RATE", 22, CDim, TextAlignmentOptions.MidlineLeft);
        var rtVal = Child("RateValue", panel.transform);
        SR(rtVal, V(0,1), V(0,1), V(0,1), V(150,-56), V(200,40));
        var rateTMP = T(rtVal, "0.00%", 28, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

        // Sector diamonds — visual rows top→bottom are S5..S1; index by sector (0=S1).
        var diamonds = new Image[5];
        var percents = new TextMeshProUGUI[5];
        const float rowTop = -132f, rowStep = 112f;
        for (int row = 0; row < 5; row++)
        {
            int sector = 4 - row;   // top row = S5 (index 4)
            float y = rowTop - row * rowStep;

            var dia = Child($"S{sector + 1}Diamond", panel.transform);
            var diaRT = SR(dia, V(0,1), V(0,1), V(0,1), V(58, y), V(40,40));
            diaRT.localRotation = Quaternion.Euler(0, 0, 45);
            var diaImg = dia.AddComponent<Image>();
            diaImg.color = new Color(0.35f,0.35f,0.35f,1f);
            diamonds[sector] = diaImg;

            var pct = Child($"S{sector + 1}Percent", panel.transform);
            SR(pct, V(0,1), V(0,1), V(0,1), V(110, y + 4), V(220,46));
            percents[sector] = T(pct, "--", 28, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        }

        // ── 4. NextSongIndicator (PVP, default inactive) ───────────────────────────
        var nextGO = Child("NextSongIndicator", ct);
        SR(nextGO, V(1,0), V(1,0), V(1,0), V(-20,110), V(260,96));
        Img(nextGO, new Color(0,0,0,.55f));
        var nextHLG = nextGO.AddComponent<HorizontalLayoutGroup>();
        nextHLG.childControlHeight = false; nextHLG.childForceExpandHeight = false;
        nextHLG.spacing = 8; nextHLG.padding = new RectOffset(10,10,10,10);
        nextHLG.childAlignment = TextAnchor.MiddleLeft;
        var nLbl = Child("NextLabel", nextGO.transform);
        nLbl.GetComponent<RectTransform>().sizeDelta = V(56, 36);
        T(nLbl, "NEXT >", 13, new Color(1,1,1,.85f), TextAlignmentOptions.MidlineLeft);
        var nJacketGO = Child("NextJacket", nextGO.transform);
        nJacketGO.GetComponent<RectTransform>().sizeDelta = V(72, 72);
        var nextRaw = nJacketGO.AddComponent<RawImage>();
        nextRaw.color = new Color(.35f,.35f,.35f,1f);
        var nTitleGO = Child("NextSongTitle", nextGO.transform);
        nTitleGO.GetComponent<RectTransform>().sizeDelta = V(0, 72);
        var nextTitleTMP = T(nTitleGO, "---", 13, new Color(1,1,1,.8f), TextAlignmentOptions.MidlineLeft);
        nextTitleTMP.overflowMode = TextOverflowModes.Ellipsis;
        nextGO.SetActive(false);

        // ── 5. Key Guide (bottom) ──────────────────────────────────────────────────
        var guide = Child("KeyGuide", ct);
        SR(guide, V(.5f,0), V(.5f,0), V(.5f,0), V(0,36), V(1120,64));
        var gHLG2 = guide.AddComponent<HorizontalLayoutGroup>();
        gHLG2.childControlHeight = true;  gHLG2.childForceExpandHeight = true;
        gHLG2.childControlWidth  = false; gHLG2.childForceExpandWidth  = false;
        gHLG2.spacing = 14; gHLG2.childAlignment = TextAnchor.MiddleCenter;

        // Visual order L→R: L-SHIFT(FxL) D(L0) F(L1) J(L2) K(L3) R-SHIFT(FxR)
        int[]    chipLaneIds = { (int)LaneRef.FxL, 0, 1, 2, 3, (int)LaneRef.FxR };
        string[] chipLabels  = { "L-SHIFT", "D", "F", "J", "K", "R-SHIFT" };
        float[]  chipWidths  = { 184, 92, 92, 92, 92, 184 };
        var chips = new Image[6];   // indexed by LaneId
        for (int i = 0; i < 6; i++)
        {
            var chip = Child(chipLabels[i] + "Chip", guide.transform);
            chip.GetComponent<RectTransform>().sizeDelta = V(chipWidths[i], 56);
            var img = chip.AddComponent<Image>();
            img.sprite = _uiSprite; img.type = Image.Type.Sliced;
            img.color  = new Color(0.10f, 0.11f, 0.14f, 0.80f);
            chips[chipLaneIds[i]] = img;

            var lblGO = Child("Label", chip.transform);
            SR(lblGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
            T(lblGO, chipLabels[i], 22, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        }

        // ── 6. 3D Lane Highlights (4 main columns) ─────────────────────────────────
        var highlights = BuildLaneHighlights();

        // ── 7. Reposition ComboDisplay to screen center, enlarge ───────────────────
        RepositionCombo();

        // ── 8. GameHud component + wiring ──────────────────────────────────────────
        var gameHud = canvasGO.AddComponent<GameHud>();
        var js = Object.FindObjectOfType<JudgmentSystem>();
        var ac = Object.FindObjectOfType<AudioConductor>();
        var so = new SerializedObject(gameHud);
        so.FindProperty("_jacket").objectReferenceValue      = jacketImg;
        so.FindProperty("_songTitle").objectReferenceValue   = titleTMP;
        so.FindProperty("_songArtist").objectReferenceValue  = artistTMP;
        so.FindProperty("_difficulty").objectReferenceValue  = diffTMP;
        so.FindProperty("_ppCount").objectReferenceValue     = counts[0];
        so.FindProperty("_pCount").objectReferenceValue      = counts[1];
        so.FindProperty("_grCount").objectReferenceValue     = counts[2];
        so.FindProperty("_gdCount").objectReferenceValue     = counts[3];
        so.FindProperty("_mCount").objectReferenceValue      = counts[4];
        so.FindProperty("_scoreGauge").objectReferenceValue  = gauge;
        so.FindProperty("_scoreValue").objectReferenceValue  = scoreTMP;
        so.FindProperty("_rateValue").objectReferenceValue   = rateTMP;
        WireArray(so, "_sectorDiamonds", diamonds);
        WireArray(so, "_sectorPercents", percents);
        so.FindProperty("_nextIndicator").objectReferenceValue = nextGO;
        so.FindProperty("_nextJacket").objectReferenceValue    = nextRaw;
        so.FindProperty("_nextSongTitle").objectReferenceValue = nextTitleTMP;
        so.FindProperty("_judgment").objectReferenceValue  = js;
        so.FindProperty("_conductor").objectReferenceValue = ac;
        so.ApplyModifiedProperties();

        // ── 9. LaneKeyGuide component + wiring ──────────────────────────────────────
        var keyGuide = canvasGO.AddComponent<LaneKeyGuide>();
        var soKG = new SerializedObject(keyGuide);
        WireArray(soKG, "_keyChips", chips);
        WireArray(soKG, "_laneHighlights", highlights);
        soKG.FindProperty("_highlightColor").colorValue   = Color.white;   // white lit lanes
        soKG.FindProperty("_highlightOnAlpha").floatValue = 0.22f;          // peak alpha (near edge); fades to 0 toward the back
        soKG.ApplyModifiedProperties();

        // ── 10. Wire GamePlayController / ReplayPlaybackController._hud ─────────────
        WireHudField(Object.FindObjectOfType<GamePlayController>(), gameHud);
        WireHudField(Object.FindObjectOfType<ReplayPlaybackController>(), gameHud);

        // ── 11. Fit 3D stage (camera / judgment line / ground) to the 6-lane field ──
        FitStageToLanes();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[HudSceneBuilder] GamePlay HUD rebuilt → " + ScenePath);
    }

    // ── Lane highlights ─────────────────────────────────────────────────────────

    // Highlight column order matches LaneId/LaneRef indexing (0..3 main, 4=FxL, 5=FxR).
    static readonly LaneRef[] HighlightLanes =
        { LaneRef.Lane0, LaneRef.Lane1, LaneRef.Lane2, LaneRef.Lane3, LaneRef.FxL, LaneRef.FxR };

    static Renderer[] BuildLaneHighlights()
    {
        var mat = CreateHighlightMaterial();

        // Parent under the lane stage if present, else a root at origin.
        var laneVisuals = Object.FindObjectOfType<LaneVisuals>();
        Transform stage = laneVisuals != null ? laneVisuals.transform : null;
        var root = new GameObject("LaneHighlights");
        root.transform.SetParent(stage, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;

        const float nearZ = -0.5f, farZ = 5.5f;
        float midZ = (nearZ + farZ) * 0.5f, depth = farZ - nearZ;

        var rends = new Renderer[HighlightLanes.Length];
        for (int c = 0; c < HighlightLanes.Length; c++)
        {
            var lane  = HighlightLanes[c];
            float x   = LaneLayout.GetX(lane);
            float w   = LaneLayout.GetNoteWidth(lane);

            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = $"LaneHighlight{c}";
            q.transform.SetParent(root.transform, false);
            var col = q.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            q.transform.localPosition = new Vector3(x, 0.02f, midZ);
            q.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);  // lie flat, face +Y
            q.transform.localScale    = new Vector3(w, depth, 1f);
            var r = q.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            rends[c] = r;
        }
        return rends;
    }

    static Material CreateHighlightMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        var mat = new Material(shader);
        // Configure URP Unlit for alpha-blended transparency.
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_SURFACE_TYPE_OPAQUE");
        mat.renderQueue = (int)RenderQueue.Transparent;
        var baseCol = new Color(1f, 1f, 1f, 0f);   // white, fully transparent at rest
        mat.SetColor("_BaseColor", baseCol);
        mat.color = baseCol;

        // Depth alpha-gradient: opaque at the judgment line (near, UV.v=0) → fully
        // transparent at the back (far, UV.v=1). Multiplies _BaseColor so the lit lane
        // fades out toward the distance and vanishes completely at the far edge.
        var grad = CreateGradientTexture();
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", grad);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", grad);

        if (AssetDatabase.LoadAssetAtPath<Material>(MatPath) != null)
            AssetDatabase.DeleteAsset(MatPath);
        AssetDatabase.CreateAsset(mat, MatPath);
        return mat;
    }

    // 1×64 vertical alpha ramp: white throughout, alpha 1 at the near edge (UV.v=0,
    // judgment line) easing to 0 at the far edge (UV.v=1, 奥). Saved as a native
    // .asset so no PNG import settings are needed; idempotent (deletes any prior asset).
    static Texture2D CreateGradientTexture()
    {
        const int h = 64;
        var tex = new Texture2D(1, h, TextureFormat.RGBA32, false)
        {
            name       = "LaneHighlightGradient",
            wrapMode   = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        var px = new Color[h];
        for (int row = 0; row < h; row++)
        {
            float v = row / (float)(h - 1);   // 0 = near (judgment line), 1 = far (奥)
            px[row] = new Color(1f, 1f, 1f, 1f - v);   // linear fade to fully transparent
        }
        tex.SetPixels(px);
        tex.Apply(false, false);

        if (AssetDatabase.LoadAssetAtPath<Texture2D>(GradTexPath) != null)
            AssetDatabase.DeleteAsset(GradTexPath);
        AssetDatabase.CreateAsset(tex, GradTexPath);
        return tex;
    }

    // ── Stage fit (camera / judgment line / ground) ────────────────────────────────

    /// <summary>
    /// 6 レーン化に合わせて判定ライン幅・床幅・カメラ位置を調整する。
    /// すべて絶対値で設定するためメニュー再実行で累積しない(冪等)。
    /// </summary>
    static void FitStageToLanes()
    {
        // Judgment line spans the full lane field (Quad base = 1 unit → scale.x == width).
        var jline = GameObject.Find("JudgmentLine");
        if (jline != null)
        {
            var s = jline.transform.localScale;
            jline.transform.localScale = new Vector3(LaneLayout.TotalWidth, s.y, s.z);
        }

        // Ground plane (base 10 units → scale.x == width * 0.1) widened to cover all lanes.
        var ground = GameObject.Find("Ground");
        if (ground != null)
        {
            var s = ground.transform.localScale;
            ground.transform.localScale = new Vector3(LaneLayout.TotalWidth * 0.1f, s.y, s.z);
        }

        // Camera: dollied back from (0,3.0,-1.4) to ~0.7× apparent size and pitched down to
        // 40°, then panned up along the view-up axis so the judgment line (world Z=-0.5)
        // sits at ~1/7 up from the bottom of the screen, leaving the full note runway above
        // it. The up-axis pan keeps the depth (apparent size) unchanged. FOV 70°.
        // Absolute values keep the builder idempotent.
        var cam = GameObject.Find("Main Camera");
        if (cam != null)
        {
            cam.transform.position    = new Vector3(0f, 4.41f, -2.41f);
            cam.transform.eulerAngles = new Vector3(40f, 0f, 0f);
            var c = cam.GetComponent<Camera>();
            if (c != null) c.fieldOfView = 70f;
        }
    }

    // ── ComboDisplay reposition ───────────────────────────────────────────────────

    static void RepositionCombo()
    {
        var combo = Object.FindObjectOfType<ComboDisplay>(true);
        if (combo == null) { Debug.LogWarning("[HudSceneBuilder] ComboDisplay not found — skip reposition"); return; }

        var rt = combo.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = V(.5f, .5f);
            rt.anchoredPosition = V(0, -150);
            rt.sizeDelta = V(420, 220);
        }
        // Enlarge the combo number; keep the label small above it.
        var soC = new SerializedObject(combo);
        var txt = soC.FindProperty("_comboText").objectReferenceValue as TextMeshProUGUI;
        var lbl = soC.FindProperty("_comboLabel").objectReferenceValue as TextMeshProUGUI;
        if (txt != null) { txt.fontSize = 96; txt.fontStyle = FontStyles.Bold; txt.alignment = TextAlignmentOptions.Center; }
        if (lbl != null) { lbl.fontSize = 26; lbl.alignment = TextAlignmentOptions.Center; lbl.color = CDim; }
    }

    // ── Micro helpers ─────────────────────────────────────────────────────────────

    static void WireHudField(MonoBehaviour comp, GameHud hud)
    {
        if (comp == null) return;
        var so = new SerializedObject(comp);
        var p  = so.FindProperty("_hud");
        if (p != null) { p.objectReferenceValue = hud; so.ApplyModifiedProperties(); }
    }

    static void WireArray(SerializedObject so, string prop, Object[] items)
    {
        var p = so.FindProperty(prop);
        p.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++)
            p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }

    static void DestroyByName(Transform parent, string name)
    {
        var t = parent.Find(name);
        if (t != null) Object.DestroyImmediate(t.gameObject);
    }

    static void DestroyExisting(string name)
    {
        var go = GameObject.Find(name);
        if (go != null) Object.DestroyImmediate(go);
    }

    static GameObject Child(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.AddComponent<RectTransform>();
        go.transform.SetParent(parent, false);
        return go;
    }

    static Image Img(GameObject go, Color color)
    {
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    static RectTransform SR(GameObject go,
        Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var r = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        r.anchorMin = aMin; r.anchorMax = aMax;
        r.pivot = pivot; r.anchoredPosition = pos; r.sizeDelta = size;
        return r;
    }

    static TextMeshProUGUI T(GameObject go, string text, float size, Color color,
        TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft,
        FontStyles style = FontStyles.Normal)
    {
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.color = color;
        t.alignment = align; t.fontStyle = style;
        t.raycastTarget = false;
        return t;
    }

    static Vector2 V(float x, float y) => new Vector2(x, y);

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var idx = path.LastIndexOf('/');
        AssetDatabase.CreateFolder(path[..idx], path[(idx + 1)..]);
    }
}
#endif
