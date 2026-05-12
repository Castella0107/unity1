#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class HudSceneBuilder
{
    const string PrefabDir  = "Assets/_Project/Prefabs/UI/HUD";
    const string PrefabPath = "Assets/_Project/Prefabs/UI/HUD/SectorPanelItem.prefab";
    const string ScenePath  = "Assets/_Project/Scenes/GamePlay.unity";

    [MenuItem("Tools/Build GamePlay HUD")]
    public static void Build()
    {
        EnsureFolder("Assets/_Project/Prefabs/UI");
        EnsureFolder(PrefabDir);

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var canvasGO = GameObject.Find("Canvas");
        if (canvasGO == null) { Debug.LogError("[HudSceneBuilder] Canvas not found"); return; }
        var ct = canvasGO.transform;

        // ── Remove old HudDisplay ─────────────────────────────────────────────
        var oldHud = canvasGO.GetComponent<HudDisplay>();
        if (oldHud != null) Object.DestroyImmediate(oldHud);

        var hudTextGO = GameObject.Find("HudText");
        if (hudTextGO != null) hudTextGO.SetActive(false);

        // ── TopBar ────────────────────────────────────────────────────────────
        var topBar = Child("TopBar", ct);
        SR(topBar, V(0,1), V(1,1), V(.5f,1), V(0,0), V(0,80));
        topBar.AddComponent<Image>().color = new Color(0,0,0,.4f);

        var judgeRow = Child("JudgmentRow", topBar.transform);
        SR(judgeRow, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        var jrHLG = judgeRow.AddComponent<HorizontalLayoutGroup>();
        jrHLG.childControlHeight = false; jrHLG.childForceExpandHeight = false;
        jrHLG.childControlWidth  = false; jrHLG.childForceExpandWidth  = false;
        jrHLG.spacing = 20; jrHLG.childAlignment = TextAnchor.MiddleLeft;
        jrHLG.padding = new RectOffset(40, 10, 20, 20);

        // Judgment count groups (P+, P, Gr, Gd, M)
        string[] jLabels = { "P+", "P", "Gr", "Gd", "M" };
        Color[] jColors  = {
            new Color(1.00f, 0.84f, 0.20f),  // P+ gold
            new Color(0.80f, 0.95f, 1.00f),  // P cyan-white
            new Color(0.30f, 1.00f, 0.40f),  // Gr green
            new Color(1.00f, 0.60f, 0.20f),  // Gd orange
            new Color(1.00f, 0.35f, 0.35f),  // M red
        };
        var jCounts = new TextMeshProUGUI[5];
        for (int i = 0; i < 5; i++)
        {
            var grp = Child(jLabels[i] + "Group", judgeRow.transform);
            grp.GetComponent<RectTransform>().sizeDelta = V(76, 40);
            var grpHLG = grp.AddComponent<HorizontalLayoutGroup>();
            grpHLG.childControlHeight = false; grpHLG.childForceExpandHeight = false;
            grpHLG.spacing = 4; grpHLG.childAlignment = TextAnchor.MiddleLeft;

            var lbl = Child("Label", grp.transform);
            lbl.GetComponent<RectTransform>().sizeDelta = V(28, 40);
            T(lbl, jLabels[i], 18, jColors[i], TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

            var cnt = Child("Count", grp.transform);
            cnt.GetComponent<RectTransform>().sizeDelta = V(40, 40);
            jCounts[i] = T(cnt, "0", 24, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        }

        // Combo group (larger, separated visually)
        var sep = Child("Separator", judgeRow.transform);
        sep.GetComponent<RectTransform>().sizeDelta = V(20, 40);
        T(sep, "|", 20, new Color(1,1,1,.3f), TextAlignmentOptions.Center);

        var comboGrp = Child("ComboGroup", judgeRow.transform);
        comboGrp.GetComponent<RectTransform>().sizeDelta = V(160, 40);
        var comboHLG = comboGrp.AddComponent<HorizontalLayoutGroup>();
        comboHLG.childControlHeight = false; comboHLG.childForceExpandHeight = false;
        comboHLG.spacing = 6; comboHLG.childAlignment = TextAnchor.MiddleLeft;

        var comboLbl = Child("ComboLabel", comboGrp.transform);
        comboLbl.GetComponent<RectTransform>().sizeDelta = V(62, 40);
        T(comboLbl, "COMBO", 14, new Color(1,1,1,.55f), TextAlignmentOptions.MidlineLeft);

        var comboVal = Child("ComboValue", comboGrp.transform);
        comboVal.GetComponent<RectTransform>().sizeDelta = V(80, 40);
        var comboTMP = T(comboVal, "0", 34, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

        // ── SectorPanel ───────────────────────────────────────────────────────
        var sectorPanel = Child("SectorPanel", ct);
        SR(sectorPanel, V(0,0), V(0,1), V(0,.5f), V(20,-4), V(178,-164));
        // leave 80px top (header) + 70px bottom (bottombar) + 4px margin = -160 height
        sectorPanel.AddComponent<Image>().color = new Color(0,0,0,.35f);

        var sectorContent = Child("Content", sectorPanel.transform);
        var sectorContentRT = SR(sectorContent, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        var sVLG = sectorContent.AddComponent<VerticalLayoutGroup>();
        sVLG.childControlHeight = false; sVLG.childForceExpandHeight = false;
        sVLG.childControlWidth  = true;  sVLG.childForceExpandWidth  = true;
        sVLG.spacing = 8; sVLG.padding = new RectOffset(8,8,12,12);
        sVLG.childAlignment = TextAnchor.UpperCenter;

        // ── BottomBar ─────────────────────────────────────────────────────────
        var bottomBar = Child("BottomBar", ct);
        SR(bottomBar, V(0,0), V(1,0), V(.5f,0), V(0,0), V(0,70));
        bottomBar.AddComponent<Image>().color = new Color(0,0,0,.5f);

        var scoreGO = Child("ScoreText", bottomBar.transform);
        SR(scoreGO, V(0,0), V(0,1), V(0,.5f), V(40,0), V(320,0));
        var scoreTMP = T(scoreGO, "SCORE: 0,000,000", 26, Color.white,
            TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

        var infoGO = Child("SongInfoText", bottomBar.transform);
        SR(infoGO, V(.5f,0), V(.5f,1), V(.5f,.5f), V(0,0), V(800,0));
        var infoTMP = T(infoGO, "--- - --- [Lv.--]", 17, new Color(1,1,1,.7f),
            TextAlignmentOptions.Center);
        infoTMP.overflowMode = TextOverflowModes.Ellipsis;

        // ── NextSongIndicator (PVP, default inactive) ─────────────────────────
        var nextGO = Child("NextSongIndicator", ct);
        SR(nextGO, V(1,0), V(1,0), V(1,0), V(-20,80), V(200,88));
        nextGO.AddComponent<Image>().color = new Color(0,0,0,.55f);
        var nextHLG = nextGO.AddComponent<HorizontalLayoutGroup>();
        nextHLG.childControlHeight = false; nextHLG.childForceExpandHeight = false;
        nextHLG.spacing = 6; nextHLG.padding = new RectOffset(8,8,8,8);
        nextHLG.childAlignment = TextAnchor.MiddleLeft;

        var nextLblGO = Child("NextLabel", nextGO.transform);
        nextLblGO.GetComponent<RectTransform>().sizeDelta = V(54, 36);
        T(nextLblGO, "NEXT >", 13, new Color(1,1,1,.85f), TextAlignmentOptions.MidlineLeft);

        var nextJacketGO = Child("NextJacket", nextGO.transform);
        nextJacketGO.GetComponent<RectTransform>().sizeDelta = V(68, 68);
        var nextRawImg = nextJacketGO.AddComponent<RawImage>();
        nextRawImg.color = new Color(.35f,.35f,.35f,1f);

        var nextTitleGO = Child("NextSongTitle", nextGO.transform);
        nextTitleGO.GetComponent<RectTransform>().sizeDelta = V(0, 68);
        var nextTitleTMP = T(nextTitleGO, "---", 13, new Color(1,1,1,.8f),
            TextAlignmentOptions.MidlineLeft);
        nextTitleTMP.overflowMode = TextOverflowModes.Ellipsis;
        nextGO.SetActive(false);

        // ── SectorPanelItem prefab ────────────────────────────────────────────
        var prefabRoot  = BuildSectorItemPrefab();
        var savedPrefab = PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
        Object.DestroyImmediate(prefabRoot);

        // ── GameHud component + field wiring ──────────────────────────────────
        var gameHud = canvasGO.AddComponent<GameHud>();
        var so      = new SerializedObject(gameHud);
        so.FindProperty("_ppCount").objectReferenceValue    = jCounts[0];
        so.FindProperty("_pCount").objectReferenceValue     = jCounts[1];
        so.FindProperty("_grCount").objectReferenceValue    = jCounts[2];
        so.FindProperty("_gdCount").objectReferenceValue    = jCounts[3];
        so.FindProperty("_mCount").objectReferenceValue     = jCounts[4];
        so.FindProperty("_comboValue").objectReferenceValue          = comboTMP;
        so.FindProperty("_sectorPanelContent").objectReferenceValue  = sectorContentRT;
        so.FindProperty("_sectorItemPrefab").objectReferenceValue    = savedPrefab;
        so.FindProperty("_scoreText").objectReferenceValue           = scoreTMP;
        so.FindProperty("_songInfoText").objectReferenceValue        = infoTMP;
        so.FindProperty("_nextIndicator").objectReferenceValue       = nextGO;
        so.FindProperty("_nextJacket").objectReferenceValue          = nextRawImg;
        so.FindProperty("_nextSongTitle").objectReferenceValue       = nextTitleTMP;

        var js = Object.FindObjectOfType<JudgmentSystem>();
        var ac = Object.FindObjectOfType<AudioConductor>();
        so.FindProperty("_judgment").objectReferenceValue  = js;
        so.FindProperty("_conductor").objectReferenceValue = ac;
        so.ApplyModifiedProperties();

        // ── Wire GamePlayController._hud ──────────────────────────────────────
        var gpc = Object.FindObjectOfType<GamePlayController>();
        if (gpc != null)
        {
            var soGpc = new SerializedObject(gpc);
            soGpc.FindProperty("_hud").objectReferenceValue = gameHud;
            soGpc.ApplyModifiedProperties();
        }

        EditorSceneManager.SaveScene(scene);
        AssetDatabase.Refresh();
        Debug.Log("[HudSceneBuilder] GamePlay HUD built → " + ScenePath);
    }

    // ── SectorPanelItem prefab builder ────────────────────────────────────────

    static GameObject BuildSectorItemPrefab()
    {
        var root = new GameObject("SectorPanelItem");
        root.AddComponent<RectTransform>().sizeDelta = V(162, 52);

        // Background (controlled by SectorItemView)
        var bgGO = Child("Background", root.transform);
        SR(bgGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        bgGO.AddComponent<Image>().color = new Color(1,1,1,.08f);

        // SectorLabel (left)
        var lblGO = Child("SectorLabel", root.transform);
        SR(lblGO, V(0,0), V(0,1), V(0,.5f), V(10,0), V(40,0));
        T(lblGO, "S?", 18, Color.white, TextAlignmentOptions.MidlineLeft);

        // RankText (right)
        var rnkGO = Child("RankText", root.transform);
        SR(rnkGO, V(1,0), V(1,1), V(1,.5f), V(-10,0), V(64,0));
        T(rnkGO, "--", 22, new Color(.5f,.5f,.5f,1f), TextAlignmentOptions.MidlineRight, FontStyles.Bold);

        // ProgressBar (bottom strip, filled horizontal)
        var pbGO = Child("ProgressBar", root.transform);
        SR(pbGO, V(0,0), V(1,0), V(0,0), V(0,0), V(0,4));
        var pb = pbGO.AddComponent<Image>();
        pb.color      = new Color(.3f,.55f,.95f,.75f);
        pb.type       = Image.Type.Filled;
        pb.fillMethod  = Image.FillMethod.Horizontal;
        pb.fillOrigin  = 0;
        pb.fillAmount  = 0f;

        return root;
    }

    // ── Micro helpers ─────────────────────────────────────────────────────────

    static GameObject Child(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.AddComponent<RectTransform>();
        go.transform.SetParent(parent, false);
        return go;
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
