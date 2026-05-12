using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using TMPro;
using System.IO;

public static class BuildHistoryScene
{
    [MenuItem("Tools/Build History Scene + Prefab")]
    public static void Build()
    {
        BuildItemPrefab();
        BuildScene();
        Debug.Log("[BuildHistoryScene] Done.");
    }

    // ── HistoryItem.prefab ────────────────────────────────────────────────────

    static void BuildItemPrefab()
    {
        string dir = "Assets/_Project/Prefabs/UI/History";
        if (!AssetDatabase.IsValidFolder("Assets/_Project/Prefabs/UI/History"))
            AssetDatabase.CreateFolder("Assets/_Project/Prefabs/UI", "History");

        var root = new GameObject("HistoryItem");
        root.AddComponent<RectTransform>();
        var btn = root.AddComponent<Button>();

        // Background
        var bg = MakeRT("Background", root);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(1,1,1, 0.04f);
        FullStretch(bg.GetComponent<RectTransform>());

        // Horizontal Layout
        var layout = MakeRT("Layout", root);
        var hLayout = layout.AddComponent<HorizontalLayoutGroup>();
        hLayout.spacing = 12;
        hLayout.childAlignment = TextAnchor.MiddleLeft;
        hLayout.childControlWidth = false;
        hLayout.childControlHeight = false;
        FullStretch(layout.GetComponent<RectTransform>());

        // Root RectTransform size
        var rootRT = root.GetComponent<RectTransform>();
        rootRT.sizeDelta = new Vector2(760, 80);

        // DateBlock
        var dateBlock = MakeVLayout("DateBlock", layout.gameObject, 100);
        MakeTMP("DateText",  dateBlock, 14, "2026/05/09");
        MakeTMP("TimeText",  dateBlock, 12, "14:32",      new Color(1,1,1,0.6f));

        // SongBlock
        var songBlock = MakeVLayout("SongBlock", layout.gameObject, 360);
        MakeTMP("TitleText", songBlock, 18, "Song Title");
        MakeTMP("DiffText",  songBlock, 14, "EXTRA");

        // ScoreBlock
        var scoreBlock = MakeVLayout("ScoreBlock", layout.gameObject, 160);
        var scoreT = MakeTMP("ScoreText", scoreBlock, 20, "985,432");
        scoreT.alignment = TextAlignmentOptions.Right;
        var rankT = MakeTMP("RankText", scoreBlock, 16, "S");
        rankT.alignment = TextAlignmentOptions.Right;

        // BadgeBlock
        var badgeBlock = MakeHLayout("BadgeBlock", layout.gameObject, 120);
        MakeBadge("FullComboBadge",      badgeBlock, "FC",  new Color(0.3f, 0.9f, 0.4f));
        MakeBadge("AllPerfectBadge",     badgeBlock, "AP",  new Color(0.3f, 0.6f, 1.0f));
        MakeBadge("AllPerfectPlusBadge", badgeBlock, "AP+", new Color(1.0f, 0.85f, 0.3f));

        string prefabPath = dir + "/HistoryItem.prefab";
        bool success;
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out success);
        Object.DestroyImmediate(root);
        Debug.Log("[BuildHistoryScene] Prefab saved: " + prefabPath + " success=" + success);
    }

    // ── History.unity scene ───────────────────────────────────────────────────

    static void BuildScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        EditorSceneManager.SetActiveScene(scene);

        // Camera
        var camGO = new GameObject("Main Camera");
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.02f, 0.03f, 0.06f);
        cam.orthographic = true;
        camGO.AddComponent<AudioListener>();
        camGO.tag = "MainCamera";

        // EventSystem
        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        esGO.AddComponent<InputSystemUIInputModule>();

        // Canvas
        var canvasGO = new GameObject("Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();

        // Background
        var bgGO = MakeRT("Background", canvasGO);
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0.02f, 0.03f, 0.06f, 1f);
        FullStretch(bgGO.GetComponent<RectTransform>());

        // ── Header ────────────────────────────────────────────────────────────
        var header = MakeRT("Header", canvasGO);
        var headerRT = header.GetComponent<RectTransform>();
        headerRT.anchorMin = new Vector2(0, 1); headerRT.anchorMax = new Vector2(1, 1);
        headerRT.pivot     = new Vector2(0.5f, 1f);
        headerRT.offsetMin = new Vector2(0, -80); headerRT.offsetMax = Vector2.zero;

        var backBtn = MakeRT("BackButton", header.gameObject);
        var backBtnRT = backBtn.GetComponent<RectTransform>();
        backBtnRT.anchorMin = new Vector2(0, 0.5f); backBtnRT.anchorMax = new Vector2(0, 0.5f);
        backBtnRT.anchoredPosition = new Vector2(100, 0); backBtnRT.sizeDelta = new Vector2(160, 50);
        backBtn.AddComponent<Image>().color = new Color(1,1,1,0.1f);
        var backBtnComp = backBtn.AddComponent<Button>();
        var backTMP = MakeTMP("Label", backBtn.gameObject, 22, "< BACK");
        backTMP.GetComponent<RectTransform>().anchorMin = Vector2.zero;
        backTMP.GetComponent<RectTransform>().anchorMax = Vector2.one;

        var titleTMP = MakeTMP("Title", header.gameObject, 36, "PLAY HISTORY");
        var titleRT = titleTMP.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.5f, 0.5f); titleRT.anchorMax = new Vector2(0.5f, 0.5f);
        titleRT.anchoredPosition = Vector2.zero; titleRT.sizeDelta = new Vector2(600, 60);
        titleTMP.alignment = TextAlignmentOptions.Center;

        var totalTMP = MakeTMP("TotalPlaysText", header.gameObject, 18, "Total: 0 plays");
        var totalRT = totalTMP.GetComponent<RectTransform>();
        totalRT.anchorMin = new Vector2(1, 0.5f); totalRT.anchorMax = new Vector2(1, 0.5f);
        totalRT.pivot = new Vector2(1f, 0.5f);
        totalRT.anchoredPosition = new Vector2(-40, 0); totalRT.sizeDelta = new Vector2(300, 40);
        totalTMP.alignment = TextAlignmentOptions.Right;

        // ── FilterBar ─────────────────────────────────────────────────────────
        var filterBar = MakeRT("FilterBar", canvasGO);
        var filterRT = filterBar.GetComponent<RectTransform>();
        filterRT.anchorMin = new Vector2(0, 1); filterRT.anchorMax = new Vector2(1, 1);
        filterRT.pivot = new Vector2(0.5f, 1f);
        filterRT.offsetMin = new Vector2(0, -160); filterRT.offsetMax = new Vector2(0, -80);
        filterBar.AddComponent<Image>().color = new Color(1,1,1, 0.03f);

        var filterLayout = filterBar.AddComponent<HorizontalLayoutGroup>();
        filterLayout.spacing = 16;
        filterLayout.padding = new RectOffset(20, 20, 8, 8);
        filterLayout.childAlignment = TextAnchor.MiddleLeft;
        filterLayout.childControlWidth = false;
        filterLayout.childControlHeight = false;

        // Mode Toggles
        var toggleGroup = filterBar.AddComponent<ToggleGroup>();
        var listToggle  = MakeToggle("ListModeToggle",  filterBar.gameObject, "All Plays",       toggleGroup, true);
        var bestToggle  = MakeToggle("BestModeToggle",  filterBar.gameObject, "Personal Bests",  toggleGroup, false);

        var diffDD   = MakeDropdown("DifficultyFilter", filterBar.gameObject, 180);
        var rankDD   = MakeDropdown("RankFilter",       filterBar.gameObject, 170);
        var sortDD   = MakeDropdown("SortOrder",        filterBar.gameObject, 220);

        // ── Content Area ──────────────────────────────────────────────────────
        var content = MakeRT("ContentArea", canvasGO);
        var contentRT = content.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 0); contentRT.anchorMax = new Vector2(1, 1);
        contentRT.offsetMin = new Vector2(40, 60); contentRT.offsetMax = new Vector2(-40, -160);

        // List Panel (left ~half)
        var listPanel = MakeRT("ListPanel", content.gameObject);
        var listPanelRT = listPanel.GetComponent<RectTransform>();
        listPanelRT.anchorMin = new Vector2(0, 0); listPanelRT.anchorMax = new Vector2(0.45f, 1f);
        listPanelRT.offsetMin = Vector2.zero; listPanelRT.offsetMax = Vector2.zero;

        // ScrollView
        var svGO = new GameObject("ScrollView");
        svGO.transform.SetParent(listPanel.gameObject.transform, false);
        var svRT = svGO.AddComponent<RectTransform>();
        FullStretch(svRT);

        var viewport = MakeRT("Viewport", svGO);
        FullStretch(viewport.GetComponent<RectTransform>());
        viewport.AddComponent<Image>().color = new Color(0,0,0,0);
        viewport.AddComponent<Mask>().showMaskGraphic = false;

        var listContentGO = MakeRT("Content", viewport.gameObject);
        var listContentRT = listContentGO.GetComponent<RectTransform>();
        listContentRT.anchorMin = new Vector2(0,1); listContentRT.anchorMax = new Vector2(1,1);
        listContentRT.pivot = new Vector2(0.5f, 1f);
        listContentRT.offsetMin = Vector2.zero; listContentRT.offsetMax = Vector2.zero;
        var vlg = listContentGO.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 4;
        vlg.childControlWidth  = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        listContentGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scrollRect = svGO.AddComponent<ScrollRect>();
        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.content  = listContentRT;
        scrollRect.horizontal = false;
        scrollRect.vertical   = true;

        var emptyState = MakeTMP("EmptyState", listPanel.gameObject, 24, "No plays yet");
        var emptyRT = emptyState.GetComponent<RectTransform>();
        emptyRT.anchorMin = new Vector2(0.5f, 0.5f); emptyRT.anchorMax = new Vector2(0.5f, 0.5f);
        emptyRT.anchoredPosition = Vector2.zero; emptyRT.sizeDelta = new Vector2(400, 60);
        emptyState.alignment = TextAlignmentOptions.Center;
        emptyState.gameObject.SetActive(false);

        // Detail Panel (right ~half)
        var detailPanel = MakeRT("DetailPanel", content.gameObject);
        var detailPanelRT = detailPanel.GetComponent<RectTransform>();
        detailPanelRT.anchorMin = new Vector2(0.47f, 0); detailPanelRT.anchorMax = new Vector2(1, 1);
        detailPanelRT.offsetMin = Vector2.zero; detailPanelRT.offsetMax = Vector2.zero;
        detailPanel.AddComponent<Image>().color = new Color(1,1,1, 0.02f);

        var detailEmpty = MakeTMP("DetailEmptyState", detailPanel.gameObject, 22, "Select a play to see details");
        var detailEmptyRT = detailEmpty.GetComponent<RectTransform>();
        detailEmptyRT.anchorMin = new Vector2(0.5f, 0.5f); detailEmptyRT.anchorMax = new Vector2(0.5f, 0.5f);
        detailEmptyRT.anchoredPosition = Vector2.zero; detailEmptyRT.sizeDelta = new Vector2(500, 60);
        detailEmpty.alignment = TextAlignmentOptions.Center;
        detailEmpty.color = new Color(1,1,1,0.5f);

        var detailContent = MakeRT("DetailContent", detailPanel.gameObject);
        FullStretch(detailContent.GetComponent<RectTransform>());
        var detailContentPad = new RectOffset(20, 20, 20, 20);

        // HistoryDetailView sub-elements
        var dvGO = detailContent.gameObject;
        var detailView = dvGO.AddComponent<HistoryDetailView>();

        float y = -30;
        void Row(string name, string val, int size = 20) {
            var t = MakeTMP(name, dvGO, size, val);
            var rt = t.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(20, y - 30); rt.offsetMax = new Vector2(-20, y);
            y -= 34;
        }

        Row("TitleText",     "Song Title", 28);
        Row("DifficultyText","EXTRA", 20);
        Row("DateText",      "2026/05/09 14:32", 16);
        y -= 10;
        Row("EffectiveScoreText", "1,000,000", 40);
        Row("RawScoreText",       "Raw: 1,000,000", 16);
        Row("RankText",           "S+", 30);
        y -= 10;

        // Badges row
        var badgesRow = MakeRT("BadgesRow", dvGO);
        var badgesRT = badgesRow.GetComponent<RectTransform>();
        badgesRT.anchorMin = new Vector2(0,1); badgesRT.anchorMax = new Vector2(1,1);
        badgesRT.pivot = new Vector2(0.5f,1f);
        badgesRT.offsetMin = new Vector2(20, y-40); badgesRT.offsetMax = new Vector2(-20, y);
        y -= 50;
        var fcBadge  = MakeBadge("FullComboBadge",      badgesRow.gameObject, "FC",  new Color(0.3f,0.9f,0.4f));
        var apBadge  = MakeBadge("AllPerfectBadge",     badgesRow.gameObject, "AP",  new Color(0.3f,0.6f,1.0f));
        var appBadge = MakeBadge("AllPerfectPlusBadge", badgesRow.gameObject, "AP+", new Color(1.0f,0.85f,0.3f));

        y -= 10;
        Row("PerfectPlusCountText", "PP: 0", 18);
        Row("PerfectCountText",     "P: 0",  18);
        Row("GreatCountText",       "Gr: 0", 18);
        Row("GoodCountText",        "Gd: 0", 18);
        Row("MissCountText",        "M: 0",  18);
        Row("MaxComboText",         "MaxCombo: 0", 18);
        Row("FastCountText",        "Fast: 0", 16);
        Row("LateCountText",        "Late: 0", 16);
        y -= 10;
        Row("ModifiersText", "Modifiers: none", 16);
        Row("ReplayInfoText","Replay: not saved", 14);

        // HistoryDetailView field binding via SerializedObject
        var so = new SerializedObject(detailView);
        so.FindProperty("_titleText")          .objectReferenceValue = Find<TextMeshProUGUI>(dvGO, "TitleText");
        so.FindProperty("_difficultyText")     .objectReferenceValue = Find<TextMeshProUGUI>(dvGO, "DifficultyText");
        so.FindProperty("_dateText")           .objectReferenceValue = Find<TextMeshProUGUI>(dvGO, "DateText");
        so.FindProperty("_effectiveScoreText") .objectReferenceValue = Find<TextMeshProUGUI>(dvGO, "EffectiveScoreText");
        so.FindProperty("_rawScoreText")       .objectReferenceValue = Find<TextMeshProUGUI>(dvGO, "RawScoreText");
        so.FindProperty("_rankText")           .objectReferenceValue = Find<TextMeshProUGUI>(dvGO, "RankText");
        so.FindProperty("_fullComboBadge")     .objectReferenceValue = fcBadge.gameObject;
        so.FindProperty("_allPerfectBadge")    .objectReferenceValue = apBadge.gameObject;
        so.FindProperty("_allPerfectPlusBadge").objectReferenceValue = appBadge.gameObject;
        so.FindProperty("_ppCountText")        .objectReferenceValue = Find<TextMeshProUGUI>(dvGO, "PerfectPlusCountText");
        so.FindProperty("_pCountText")         .objectReferenceValue = Find<TextMeshProUGUI>(dvGO, "PerfectCountText");
        so.FindProperty("_grCountText")        .objectReferenceValue = Find<TextMeshProUGUI>(dvGO, "GreatCountText");
        so.FindProperty("_gdCountText")        .objectReferenceValue = Find<TextMeshProUGUI>(dvGO, "GoodCountText");
        so.FindProperty("_mCountText")         .objectReferenceValue = Find<TextMeshProUGUI>(dvGO, "MissCountText");
        so.FindProperty("_maxComboText")       .objectReferenceValue = Find<TextMeshProUGUI>(dvGO, "MaxComboText");
        so.FindProperty("_fastCountText")      .objectReferenceValue = Find<TextMeshProUGUI>(dvGO, "FastCountText");
        so.FindProperty("_lateCountText")      .objectReferenceValue = Find<TextMeshProUGUI>(dvGO, "LateCountText");
        so.FindProperty("_modifiersText")      .objectReferenceValue = Find<TextMeshProUGUI>(dvGO, "ModifiersText");
        so.FindProperty("_replayInfoText")     .objectReferenceValue = Find<TextMeshProUGUI>(dvGO, "ReplayInfoText");
        so.ApplyModifiedPropertiesWithoutUndo();

        detailContent.gameObject.SetActive(false);  // start hidden

        // ── Footer ────────────────────────────────────────────────────────────
        var footer = MakeRT("Footer", canvasGO);
        var footerRT = footer.GetComponent<RectTransform>();
        footerRT.anchorMin = new Vector2(0, 0); footerRT.anchorMax = new Vector2(1, 0);
        footerRT.pivot = new Vector2(0.5f, 0f);
        footerRT.offsetMin = Vector2.zero; footerRT.offsetMax = new Vector2(0, 50);
        footer.AddComponent<Image>().color = new Color(1,1,1, 0.04f);
        var hintTMP = MakeTMP("KeyHint", footer.gameObject, 16, "↑↓: Select   Esc: Back");
        FullStretch(hintTMP.GetComponent<RectTransform>());
        hintTMP.alignment = TextAlignmentOptions.Center;
        hintTMP.color = new Color(1,1,1,0.4f);

        // ── HistoryController GO ──────────────────────────────────────────────
        var ctrlGO = new GameObject("HistoryController");
        var ctrl = ctrlGO.AddComponent<HistoryController>();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Prefabs/UI/History/HistoryItem.prefab");

        var soCtrl = new SerializedObject(ctrl);
        soCtrl.FindProperty("_backButton")          .objectReferenceValue = backBtnComp;
        soCtrl.FindProperty("_totalPlaysText")      .objectReferenceValue = totalTMP;
        soCtrl.FindProperty("_listModeToggle")      .objectReferenceValue = listToggle;
        soCtrl.FindProperty("_bestModeToggle")      .objectReferenceValue = bestToggle;
        soCtrl.FindProperty("_difficultyDropdown")  .objectReferenceValue = diffDD;
        soCtrl.FindProperty("_rankDropdown")        .objectReferenceValue = rankDD;
        soCtrl.FindProperty("_sortDropdown")        .objectReferenceValue = sortDD;
        soCtrl.FindProperty("_listContent")         .objectReferenceValue = listContentRT;
        soCtrl.FindProperty("_scrollRect")          .objectReferenceValue = scrollRect;
        soCtrl.FindProperty("_historyItemPrefab")   .objectReferenceValue = prefab;
        soCtrl.FindProperty("_emptyState")          .objectReferenceValue = emptyState.gameObject;
        soCtrl.FindProperty("_detailEmptyState")    .objectReferenceValue = detailEmpty.gameObject;
        soCtrl.FindProperty("_detailContent")       .objectReferenceValue = detailContent.gameObject;
        soCtrl.FindProperty("_detailView")          .objectReferenceValue = detailView;
        // _inputAsset: must be set manually in Inspector (same asset used by other controllers)
        soCtrl.ApplyModifiedPropertiesWithoutUndo();

        // ── Save scene ────────────────────────────────────────────────────────
        string scenePath = "Assets/_Project/Scenes/History.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log("[BuildHistoryScene] Scene saved: " + scenePath);

        // ── Build Settings ────────────────────────────────────────────────────
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(
            EditorBuildSettings.scenes);
        bool exists = scenes.Exists(s => s.path == scenePath);
        if (!exists)
        {
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[BuildHistoryScene] Added to Build Settings");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static GameObject MakeRT(string name, GameObject parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    static void FullStretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    static TextMeshProUGUI MakeTMP(string name, GameObject parent, int size, string text,
                                    Color? color = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var rt = go.AddComponent<RectTransform>();
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text     = text;
        tmp.fontSize = size;
        tmp.color    = color ?? Color.white;
        rt.sizeDelta = new Vector2(300, size + 10);
        return tmp;
    }

    static TextMeshProUGUI MakeBadge(string name, GameObject parent, string text, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<RectTransform>().sizeDelta = new Vector2(50, 30);
        var bg = go.AddComponent<Image>();
        bg.color = new Color(color.r, color.g, color.b, 0.3f);
        var tmp = new GameObject("Label");
        tmp.transform.SetParent(go.transform, false);
        var tmpRT = tmp.AddComponent<RectTransform>();
        FullStretch(tmpRT);
        var t = tmp.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = 14; t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        return t;
    }

    static GameObject MakeVLayout(string name, GameObject parent, float width)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(width, 70);
        var vl = go.AddComponent<VerticalLayoutGroup>();
        vl.childAlignment = TextAnchor.MiddleLeft;
        vl.childControlWidth = false;
        vl.childControlHeight = false;
        return go;
    }

    static GameObject MakeHLayout(string name, GameObject parent, float width)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(width, 30);
        var hl = go.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = 4;
        hl.childAlignment = TextAnchor.MiddleLeft;
        hl.childControlWidth = false;
        hl.childControlHeight = false;
        return go;
    }

    static Toggle MakeToggle(string name, GameObject parent, string label,
                              ToggleGroup group, bool isOn)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(160, 40);
        var img = go.AddComponent<Image>();
        img.color = new Color(1,1,1, isOn ? 0.2f : 0.08f);
        var toggle = go.AddComponent<Toggle>();
        toggle.group = group;
        toggle.isOn  = isOn;
        toggle.targetGraphic = img;
        var lbl = MakeTMP("Label", go, 16, label);
        lbl.alignment = TextAlignmentOptions.Center;
        FullStretch(lbl.GetComponent<RectTransform>());
        return toggle;
    }

    static TMP_Dropdown MakeDropdown(string name, GameObject parent, float width)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(width, 40);
        go.AddComponent<Image>().color = new Color(1,1,1,0.1f);
        var dd = go.AddComponent<TMP_Dropdown>();
        return dd;
    }

    static T Find<T>(GameObject root, string name) where T : Component
    {
        var t = root.transform.Find(name);
        return t != null ? t.GetComponent<T>() : null;
    }
}
