#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class ResultSceneBuilder
{
    const string ScenePath   = "Assets/_Project/Scenes/Result.unity";
    const string PrefabPath  = "Assets/_Project/Prefabs/UI/Result/SectorScoreItem.prefab";

    [MenuItem("Tools/Build Result Scene")]
    public static void Build()
    {
        EnsureFolder("Assets/_Project/Prefabs/UI/Result");

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var canvasGO = GameObject.Find("Canvas");
        if (canvasGO == null) { Debug.LogError("[ResultSceneBuilder] Canvas not found"); return; }

        // Remove old ResultController and clear children
        var oldRc = canvasGO.GetComponent<ResultController>();
        if (oldRc != null) Object.DestroyImmediate(oldRc);
        while (canvasGO.transform.childCount > 0)
            Object.DestroyImmediate(canvasGO.transform.GetChild(0).gameObject);

        var ct = canvasGO.transform;

        // ── Background ────────────────────────────────────────────────────────
        var bgGO = Child("Background", ct);
        SR(bgGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        bgGO.AddComponent<Image>().color = Hex("050810");

        // ── Header ────────────────────────────────────────────────────────────
        var headerGO = Child("Header", ct);
        SR(headerGO, V(0,1), V(1,1), V(.5f,1), V(0,0), V(0,90));
        headerGO.AddComponent<Image>().color = new Color(0,0,0,.55f);

        var modeGO = Child("ModeText", headerGO.transform);
        SR(modeGO, V(0,.5f), V(0,.5f), V(0,.5f), V(40,0), V(120,46));
        var modeTMP = T(modeGO, "Single", 26, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

        var diffGO = Child("DifficultyText", headerGO.transform);
        SR(diffGO, V(0,.5f), V(0,.5f), V(0,.5f), V(180,0), V(240,46));
        var diffTMP = T(diffGO, "EXTRA  Lv.--", 20, new Color(1f,.35f,.35f), TextAlignmentOptions.MidlineLeft);

        var siFrameGO = Child("SongInfoFrame", headerGO.transform);
        SR(siFrameGO, V(1,.5f), V(1,.5f), V(1,.5f), V(-40,0), V(700,54));
        siFrameGO.AddComponent<Image>().color = new Color(1,1,1,.06f);
        var siGO = Child("SongInfoText", siFrameGO.transform);
        SR(siGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(-20,0));
        var siTMP = T(siGO, "Song Title  -  Artist", 22, Color.white, TextAlignmentOptions.Center);
        siTMP.overflowMode = TextOverflowModes.Ellipsis;

        // ── Body ──────────────────────────────────────────────────────────────
        // top=100, bottom=140 → anchoredPos=(0,20), sizeDelta=(0,-240)
        var bodyGO = Child("Body", ct);
        SR(bodyGO, V(0,0), V(1,1), V(.5f,.5f), V(0,20), V(0,-240));

        // ── Left Column (sector scores) ───────────────────────────────────────
        var leftGO = Child("LeftColumn", bodyGO.transform);
        SR(leftGO, V(0,0), V(0,1), V(0,.5f), V(60,0), V(360,0));

        var lcContentGO = Child("Content", leftGO.transform);
        var lcContentRT = SR(lcContentGO, V(0,1), V(1,1), V(.5f,1), V(0,0), V(0,0));
        var lcVLG = lcContentGO.AddComponent<VerticalLayoutGroup>();
        lcVLG.childControlHeight = false; lcVLG.childForceExpandHeight = false;
        lcVLG.childControlWidth  = true;  lcVLG.childForceExpandWidth  = true;
        lcVLG.spacing = 12; lcVLG.padding = new RectOffset(0,0,20,0);
        lcVLG.childAlignment = TextAnchor.UpperCenter;
        var lcCSF = lcContentGO.AddComponent<ContentSizeFitter>();
        lcCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // ── Center Column ─────────────────────────────────────────────────────
        var centerGO = Child("CenterColumn", bodyGO.transform);
        SR(centerGO, V(.5f,.5f), V(.5f,.5f), V(.5f,.5f), V(0,20), V(600,560));

        // RankFrame
        var rankFrameGO = Child("RankFrame", centerGO.transform);
        SR(rankFrameGO, V(0,1), V(1,1), V(.5f,1), V(0,-10), V(0,260));
        var rankTMP = T(rankFrameGO, "S+", 220, new Color(1f,.84f,0f),
            TextAlignmentOptions.Center, FontStyles.Bold);

        // ScoreSection (VLG below RankFrame)
        var scoreSectionGO = Child("ScoreSection", centerGO.transform);
        SR(scoreSectionGO, V(0,1), V(1,1), V(.5f,1), V(0,-280), V(-60,200));
        var ssVLG = scoreSectionGO.AddComponent<VerticalLayoutGroup>();
        ssVLG.childControlHeight = false; ssVLG.childForceExpandHeight = false;
        ssVLG.childControlWidth  = true;  ssVLG.childForceExpandWidth  = true;
        ssVLG.spacing = 4; ssVLG.childAlignment = TextAnchor.UpperCenter;

        var csLblGO = Child("CurrentScoreLabel", scoreSectionGO.transform);
        csLblGO.AddComponent<LayoutElement>().minHeight = 24;
        T(csLblGO, "SCORE", 17, new Color(.6f,.6f,.6f), TextAlignmentOptions.Center);

        var csTxtGO = Child("CurrentScoreText", scoreSectionGO.transform);
        csTxtGO.AddComponent<LayoutElement>().minHeight = 76;
        var csTMP = T(csTxtGO, "0", 64, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

        var bsLblGO = Child("BestScoreLabel", scoreSectionGO.transform);
        bsLblGO.AddComponent<LayoutElement>().minHeight = 22;
        T(bsLblGO, "BEST", 15, new Color(.55f,.55f,.55f), TextAlignmentOptions.Center);

        var bsTxtGO = Child("BestScoreText", scoreSectionGO.transform);
        bsTxtGO.AddComponent<LayoutElement>().minHeight = 46;
        var bsTMP = T(bsTxtGO, "---", 36, new Color(.7f,.7f,.7f), TextAlignmentOptions.Center);

        var newBestGO = Child("NewBestBadge", scoreSectionGO.transform);
        newBestGO.AddComponent<LayoutElement>().minHeight = 38;
        newBestGO.AddComponent<Image>().color = new Color(.2f,.15f,0f,.7f);
        // Text must be on a child — Image and TextMeshProUGUI cannot share a GameObject
        var nbLblGO = Child("Label", newBestGO.transform);
        SR(nbLblGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        T(nbLblGO, "★  NEW BEST  ★", 26, new Color(1f,.84f,0f),
            TextAlignmentOptions.Center, FontStyles.Bold);
        newBestGO.SetActive(false);

        // AchievementBadges (HLG below ScoreSection)
        var achvGO = Child("AchievementBadges", centerGO.transform);
        SR(achvGO, V(0,1), V(1,1), V(.5f,1), V(0,-492), V(-20,54));
        var achvHLG = achvGO.AddComponent<HorizontalLayoutGroup>();
        achvHLG.childControlWidth = true; achvHLG.childForceExpandWidth = true;
        achvHLG.childControlHeight = false; achvHLG.childForceExpandHeight = false;
        achvHLG.spacing = 10; achvHLG.childAlignment = TextAnchor.MiddleCenter;

        GameObject fcBadge   = BuildBadge("FullComboBadge",   "FULL COMBO",   new Color(.4f,1f,.4f), achvGO.transform);
        GameObject apBadge   = BuildBadge("AllPerfectBadge",  "ALL PERFECT",  new Color(1f,.95f,.4f), achvGO.transform);
        GameObject appBadge  = BuildBadge("AllPerfectPlusBadge","ALL PERFECT+",new Color(1f,.84f,0f), achvGO.transform);
        fcBadge.SetActive(false); apBadge.SetActive(false); appBadge.SetActive(false);

        // ── Right Column (judgment + combo) ───────────────────────────────────
        var rightGO = Child("RightColumn", bodyGO.transform);
        SR(rightGO, V(1,0), V(1,1), V(1,.5f), V(-60,0), V(360,0));
        rightGO.AddComponent<Image>().color = new Color(1,1,1,.05f);

        var rightVLG = rightGO.AddComponent<VerticalLayoutGroup>();
        rightVLG.childControlHeight = false; rightVLG.childForceExpandHeight = false;
        rightVLG.childControlWidth  = true;  rightVLG.childForceExpandWidth  = true;
        rightVLG.spacing = 6; rightVLG.padding = new RectOffset(20,20,24,24);
        rightVLG.childAlignment = TextAnchor.UpperLeft;

        // Judgment rows (P+, P, Gr, Gd, M)
        string[] jLabels = { "P+", "P", "Gr", "Gd", "M" };
        Color[] jColors = {
            new Color(1f,.84f,.2f), new Color(.8f,.95f,1f),
            new Color(.3f,1f,.4f),  new Color(1f,.6f,.2f), new Color(1f,.35f,.35f)
        };
        var jCounts = new TextMeshProUGUI[5];
        for (int i = 0; i < 5; i++)
            jCounts[i] = BuildJudgmentRow(jLabels[i], jColors[i], rightGO.transform);

        // Separator
        var sepGO = Child("Separator", rightGO.transform);
        sepGO.AddComponent<LayoutElement>().minHeight = 2;
        sepGO.AddComponent<Image>().color = new Color(1,1,1,.2f);

        // MaxCombo section
        var mcSectionGO = Child("MaxComboSection", rightGO.transform);
        mcSectionGO.AddComponent<LayoutElement>().minHeight = 100;
        var mcVLG = mcSectionGO.AddComponent<VerticalLayoutGroup>();
        mcVLG.childControlHeight = false; mcVLG.childForceExpandHeight = false;
        mcVLG.childControlWidth  = true;  mcVLG.childForceExpandWidth  = true;
        mcVLG.spacing = 4; mcVLG.padding = new RectOffset(0,0,8,0);

        var mcLblGO = Child("MaxComboLabel", mcSectionGO.transform);
        mcLblGO.AddComponent<LayoutElement>().minHeight = 24;
        T(mcLblGO, "MAX COMBO", 16, new Color(.6f,.6f,.6f), TextAlignmentOptions.Center);
        var mcTxtGO = Child("MaxComboText", mcSectionGO.transform);
        mcTxtGO.AddComponent<LayoutElement>().minHeight = 60;
        var mcTMP = T(mcTxtGO, "0", 52, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

        // ── Fast / Late bar ───────────────────────────────────────────────────
        var flBarGO = Child("FastLateBar", ct);
        SR(flBarGO, V(0,0), V(1,0), V(.5f,0), V(0,102), V(0,40));

        var flHLG = flBarGO.AddComponent<HorizontalLayoutGroup>();
        flHLG.childControlWidth = false; flHLG.childForceExpandWidth = false;
        flHLG.childControlHeight = false; flHLG.childForceExpandHeight = false;
        flHLG.spacing = 60; flHLG.childAlignment = TextAnchor.MiddleCenter;

        TextMeshProUGUI fastTMP, lateTMP;
        BuildFLGroup("FastGroup",  "FAST", new Color(.27f,.53f,1f),  flBarGO.transform, out fastTMP);
        BuildFLGroup("LateGroup",  "LATE", new Color(1f,.33f,.33f),  flBarGO.transform, out lateTMP);

        // ── Button row ────────────────────────────────────────────────────────
        var btnRowGO = Child("ButtonRow", ct);
        SR(btnRowGO, V(0,0), V(1,0), V(.5f,0), V(0,30), V(0,62));

        var btnHLG = btnRowGO.AddComponent<HorizontalLayoutGroup>();
        btnHLG.childControlWidth = false; btnHLG.childForceExpandWidth = false;
        btnHLG.childControlHeight = false; btnHLG.childForceExpandHeight = false;
        btnHLG.spacing = 30; btnHLG.childAlignment = TextAnchor.MiddleCenter;

        var retryBtn    = BuildButton("RetryButton",    "RETRY  (R)",        btnRowGO.transform);
        var toSelectBtn = BuildButton("ToSelectButton", "SONG SELECT  (S)",  btnRowGO.transform);
        var toTitleBtn  = BuildButton("ToTitleButton",  "TO TITLE  (Esc)",   btnRowGO.transform);

        // ── SectorScoreItem prefab ────────────────────────────────────────────
        var prefabRoot  = BuildSectorItemPrefab();
        var savedPrefab = PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
        Object.DestroyImmediate(prefabRoot);

        // ── ResultController + field wiring ───────────────────────────────────
        var rc = canvasGO.AddComponent<ResultController>();
        var so = new SerializedObject(rc);

        so.FindProperty("_modeText").objectReferenceValue       = modeTMP;
        so.FindProperty("_difficultyText").objectReferenceValue = diffTMP;
        so.FindProperty("_songInfoText").objectReferenceValue   = siTMP;
        so.FindProperty("_sectorListContent").objectReferenceValue = lcContentRT;
        so.FindProperty("_sectorItemPrefab").objectReferenceValue  = savedPrefab;
        so.FindProperty("_rankText").objectReferenceValue           = rankTMP;
        so.FindProperty("_currentScoreText").objectReferenceValue   = csTMP;
        so.FindProperty("_bestScoreText").objectReferenceValue      = bsTMP;
        so.FindProperty("_newBestBadge").objectReferenceValue       = newBestGO;
        so.FindProperty("_fullComboBadge").objectReferenceValue     = fcBadge;
        so.FindProperty("_allPerfectBadge").objectReferenceValue    = apBadge;
        so.FindProperty("_allPerfectPlusBadge").objectReferenceValue = appBadge;
        so.FindProperty("_ppCount").objectReferenceValue = jCounts[0];
        so.FindProperty("_pCount").objectReferenceValue  = jCounts[1];
        so.FindProperty("_grCount").objectReferenceValue = jCounts[2];
        so.FindProperty("_gdCount").objectReferenceValue = jCounts[3];
        so.FindProperty("_mCount").objectReferenceValue  = jCounts[4];
        so.FindProperty("_maxComboText").objectReferenceValue  = mcTMP;
        so.FindProperty("_fastCountText").objectReferenceValue = fastTMP;
        so.FindProperty("_lateCountText").objectReferenceValue = lateTMP;
        so.FindProperty("_retryButton").objectReferenceValue    = retryBtn;
        so.FindProperty("_toSelectButton").objectReferenceValue = toSelectBtn;
        so.FindProperty("_toTitleButton").objectReferenceValue  = toTitleBtn;

        foreach (var guid in AssetDatabase.FindAssets("InputActions t:InputActionAsset"))
        {
            var iaPath = AssetDatabase.GUIDToAssetPath(guid);
            if (iaPath.Contains("_Project"))
            {
                so.FindProperty("_inputAsset").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(iaPath);
                break;
            }
        }
        so.ApplyModifiedProperties();

        EditorSceneManager.SaveScene(scene);
        AssetDatabase.Refresh();
        Debug.Log("[ResultSceneBuilder] Done → " + ScenePath);
    }

    // ── Sub-builders ──────────────────────────────────────────────────────────

    static TextMeshProUGUI BuildJudgmentRow(string label, Color labelColor, Transform parent)
    {
        var rowGO = Child(label + "Row", parent);
        rowGO.AddComponent<LayoutElement>().minHeight = 50;
        var rowHLG = rowGO.AddComponent<HorizontalLayoutGroup>();
        rowHLG.childControlHeight = false; rowHLG.childForceExpandHeight = false;
        rowHLG.spacing = 0; rowHLG.childAlignment = TextAnchor.MiddleLeft;

        var lblGO = Child("Label", rowGO.transform);
        lblGO.AddComponent<LayoutElement>().minWidth = 80;
        T(lblGO, label, 26, labelColor, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

        var cntGO = Child("Count", rowGO.transform);
        var cntLE = cntGO.AddComponent<LayoutElement>(); cntLE.flexibleWidth = 1;
        return T(cntGO, "0", 28, Color.white, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
    }

    static void BuildFLGroup(string name, string label, Color labelColor,
        Transform parent, out TextMeshProUGUI countTMP)
    {
        var grpGO = Child(name, parent);
        grpGO.GetComponent<RectTransform>().sizeDelta = V(160, 40);
        var grpHLG = grpGO.AddComponent<HorizontalLayoutGroup>();
        grpHLG.childControlHeight = false; grpHLG.childForceExpandHeight = false;
        grpHLG.spacing = 10; grpHLG.childAlignment = TextAnchor.MiddleCenter;

        var lblGO = Child("Label", grpGO.transform);
        lblGO.GetComponent<RectTransform>().sizeDelta = V(70, 40);
        T(lblGO, label, 20, labelColor, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

        var cntGO = Child("Count", grpGO.transform);
        cntGO.GetComponent<RectTransform>().sizeDelta = V(60, 40);
        countTMP = T(cntGO, "0", 22, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
    }

    static Button BuildButton(string name, string label, Transform parent)
    {
        var btnGO  = Child(name, parent);
        btnGO.GetComponent<RectTransform>().sizeDelta = V(230, 62);
        var img    = btnGO.AddComponent<Image>(); img.color = Hex("2c5aa0");
        var btn    = btnGO.AddComponent<Button>(); btn.targetGraphic = img;
        var cb     = btn.colors;
        cb.normalColor      = Hex("2c5aa0");
        cb.highlightedColor = Hex("4477cc");
        cb.pressedColor     = Hex("1a3d70");
        cb.selectedColor    = Hex("4477cc");
        cb.fadeDuration     = 0.1f;
        btn.colors = cb;

        var lblGO = Child("Label", btnGO.transform);
        SR(lblGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        T(lblGO, label, 18, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

        return btn;
    }

    static GameObject BuildBadge(string name, string label, Color color, Transform parent)
    {
        var go = Child(name, parent);
        go.AddComponent<LayoutElement>().minHeight = 46;
        go.AddComponent<Image>().color = new Color(0,0,0,.5f);
        // TMP on child to avoid Image+TMP conflict on same GO
        var lblGO = Child("Label", go.transform);
        SR(lblGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        T(lblGO, label, 22, color, TextAlignmentOptions.Center, FontStyles.Bold);
        return go;
    }

    static GameObject BuildSectorItemPrefab()
    {
        var root   = new GameObject("SectorScoreItem");
        var rootRT = root.AddComponent<RectTransform>(); rootRT.sizeDelta = V(340, 60);
        root.AddComponent<Image>().color = new Color(1,1,1,.06f);

        var accentGO = Child("AccentBar", root.transform);
        SR(accentGO, V(0,0), V(0,1), V(0,.5f), V(0,0), V(4,0));
        accentGO.AddComponent<Image>().color = new Color(.3f,.55f,.9f,1f);

        var lblGO = Child("SectorLabel", root.transform);
        SR(lblGO, V(0,0), V(0,1), V(0,.5f), V(16,0), V(54,0));
        T(lblGO, "S?", 26, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

        var scoreGO = Child("SectorScore", root.transform);
        SR(scoreGO, V(1,0), V(1,1), V(1,.5f), V(-16,0), V(240,0));
        T(scoreGO, "0", 26, new Color(.9f,.9f,.9f,1f), TextAlignmentOptions.MidlineRight, FontStyles.Bold);

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

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out var c);
        return c;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var idx = path.LastIndexOf('/');
        AssetDatabase.CreateFolder(path[..idx], path[(idx + 1)..]);
    }
}
#endif
