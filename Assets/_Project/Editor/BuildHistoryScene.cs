using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// History シーンと2種のアイテムプレハブ(ソロ=HistoryItem / PVP=PvpMatchItem)を
/// スクラッチで構築し、HistoryController のフィールドを配線するエディター専用ヘルパー。
/// 行の詳細(アコーディオン展開部)はプレハブに baked-in する(ランタイム生成しない)。
/// </summary>
public static class BuildHistoryScene
{
    const string SoloPrefabPath = "Assets/_Project/Prefabs/UI/History/HistoryItem.prefab";
    const string PvpPrefabPath  = "Assets/_Project/Prefabs/UI/History/PvpMatchItem.prefab";

    static readonly Color CyanWin = new Color(0.30f, 0.80f, 0.95f, 1f);
    static readonly Color RedLoss = new Color(0.92f, 0.30f, 0.30f, 1f);

    [MenuItem("Tools/Build History Scene + Prefab")]
    public static void Build()
    {
        EnsureFolder();
        var soloPrefab = BuildSoloPrefab();
        var pvpPrefab  = BuildPvpPrefab();
        BuildScene(soloPrefab, pvpPrefab);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[BuildHistoryScene] Done.");
    }

    static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Project/Prefabs/UI/History"))
            AssetDatabase.CreateFolder("Assets/_Project/Prefabs/UI", "History");
    }

    // ── Solo item prefab (HistoryItem.prefab) ───────────────────────────────────

    static GameObject BuildSoloPrefab()
    {
        var root = new GameObject("HistoryItem");
        root.AddComponent<RectTransform>();
        var btn = root.AddComponent<Button>();
        var rootVlg = root.AddComponent<VerticalLayoutGroup>();
        rootVlg.childControlWidth  = true;  rootVlg.childForceExpandWidth  = true;
        rootVlg.childControlHeight = true;  rootVlg.childForceExpandHeight = false;
        rootVlg.spacing = 0;
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(1400, 84);

        // Background (layout-ignored full stretch; doubles as Button target + selection tint)
        var bg = MakeRT("Background", root);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(1, 1, 1, 0.04f);
        FullStretch(bg.GetComponent<RectTransform>());
        Ignore(bg);
        btn.targetGraphic = bgImg;

        // Summary
        var summary = MakeRT("Summary", root);
        LE(summary, 84);
        var jacket = MakeRawImage("Jacket", summary, 64, 64);
        Anchor(jacket, 0, 0.5f, 0, 0.5f, new Vector2(16, 0), new Vector2(0, 0.5f));
        var title = MakeTMP("TitleText", summary, 22, "Song Title");
        AnchorBox(title, 0, 0.5f, 0, 0.5f, new Vector2(92, 0), new Vector2(0, 0.5f), new Vector2(760, 44));
        var score = MakeTMP("ScoreText", summary, 24, "100,000");
        AnchorBox(score, 1, 0.5f, 1, 0.5f, new Vector2(-360, 0), new Vector2(1, 0.5f), new Vector2(220, 44));
        score.alignment = TextAlignmentOptions.Right;
        var fc = MakeBadge("FCBadge", summary, "FC", new Color(0.3f, 0.8f, 0.95f));
        AnchorBox(fc, 1, 0.5f, 1, 0.5f, new Vector2(-300, 0), new Vector2(1, 0.5f), new Vector2(54, 32));
        var ap = MakeBadge("APBadge", summary, "AP", new Color(1.0f, 0.85f, 0.3f));
        AnchorBox(ap, 1, 0.5f, 1, 0.5f, new Vector2(-240, 0), new Vector2(1, 0.5f), new Vector2(54, 32));
        var date = MakeTMP("DateText", summary, 18, "2026/05/17");
        AnchorBox(date, 1, 0.5f, 1, 0.5f, new Vector2(-16, 0), new Vector2(1, 0.5f), new Vector2(180, 40));
        date.alignment = TextAlignmentOptions.Right;

        // Detail (starts inactive)
        var detail = MakeRT("Detail", root);
        LE(detail, 150);
        // Judgment breakdown (left, stacked)
        string[] jnames = { "PpText", "PText", "GrText", "GdText", "MText" };
        string[] jinit  = { "perfect+   0", "perfect    0", "great      0", "good       0", "miss       0" };
        float jy = -10;
        for (int i = 0; i < 5; i++)
        {
            var t = MakeTMP(jnames[i], detail, 16, jinit[i]);
            AnchorBox(t, 0, 1, 0, 1, new Vector2(24, jy), new Vector2(0, 1), new Vector2(240, 22));
            jy -= 24;
        }
        // Sectors (center) S1..S5
        var sectors = MakeRT("Sectors", detail);
        AnchorStretchTop(sectors, new Vector2(300, -120), new Vector2(-220, -10));
        for (int i = 0; i < 5; i++)
        {
            var cell = MakeRT($"S{i + 1}", sectors);
            Anchor(cell, 0, 0.5f, 0, 0.5f, new Vector2(60 + i * 130, 0), new Vector2(0.5f, 0.5f));
            cell.GetComponent<RectTransform>().sizeDelta = new Vector2(110, 100);
            var lbl = MakeTMP("Label", cell, 16, $"S{i + 1}");
            AnchorBox(lbl, 0.5f, 1, 0.5f, 1, new Vector2(0, -2), new Vector2(0.5f, 1), new Vector2(100, 22));
            lbl.alignment = TextAlignmentOptions.Center;
            var dia = MakeDiamond("Diamond", cell, 26);
            Anchor(dia, 0.5f, 0.5f, 0.5f, 0.5f, new Vector2(0, 4), new Vector2(0.5f, 0.5f));
            var sc = MakeTMP("Score", cell, 13, "0");
            AnchorBox(sc, 0.5f, 0, 0.5f, 0, new Vector2(0, 2), new Vector2(0.5f, 0), new Vector2(100, 20));
            sc.alignment = TextAlignmentOptions.Center;
        }
        // Accuracy (right)
        var acc = MakeTMP("AccuracyText", detail, 20, "0.00%");
        AnchorBox(acc, 1, 1, 1, 1, new Vector2(-20, -50), new Vector2(1, 1), new Vector2(190, 30));
        acc.alignment = TextAlignmentOptions.Right;

        detail.SetActive(false);

        return SaveAndDestroy(root, SoloPrefabPath);
    }

    // ── PVP item prefab (PvpMatchItem.prefab) ───────────────────────────────────

    static GameObject BuildPvpPrefab()
    {
        var root = new GameObject("PvpMatchItem");
        root.AddComponent<RectTransform>();
        var btn = root.AddComponent<Button>();
        var rootVlg = root.AddComponent<VerticalLayoutGroup>();
        rootVlg.childControlWidth  = true;  rootVlg.childForceExpandWidth  = true;
        rootVlg.childControlHeight = true;  rootVlg.childForceExpandHeight = false;
        rootVlg.spacing = 0;
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(1400, 96);

        var bg = MakeRT("Background", root);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(1, 1, 1, 0.04f);
        FullStretch(bg.GetComponent<RectTransform>());
        Ignore(bg);
        btn.targetGraphic = bgImg;

        // Summary: 3 jackets + selfName + result/score + oppName
        var summary = MakeRT("Summary", root);
        LE(summary, 96);
        for (int j = 0; j < 3; j++)
        {
            var jk = MakeRawImage($"Jacket{j}", summary, 64, 64);
            Anchor(jk, 0, 0.5f, 0, 0.5f, new Vector2(16 + j * 72, 0), new Vector2(0, 0.5f));
        }
        var selfName = MakeTMP("SelfName", summary, 22, "you");
        AnchorBox(selfName, 0.5f, 0.5f, 0.5f, 0.5f, new Vector2(-160, 0), new Vector2(0.5f, 0.5f), new Vector2(220, 40));
        selfName.alignment = TextAlignmentOptions.Center;
        var resultT = MakeTMP("ResultText", summary, 16, "win");
        AnchorBox(resultT, 0.5f, 0.5f, 0.5f, 0.5f, new Vector2(0, 18), new Vector2(0.5f, 0.5f), new Vector2(160, 24));
        resultT.alignment = TextAlignmentOptions.Center;
        var scoreT = MakeTMP("ScoreText", summary, 26, "8-7");
        AnchorBox(scoreT, 0.5f, 0.5f, 0.5f, 0.5f, new Vector2(0, -12), new Vector2(0.5f, 0.5f), new Vector2(160, 34));
        scoreT.alignment = TextAlignmentOptions.Center;
        var oppName = MakeTMP("OppName", summary, 22, "opp");
        AnchorBox(oppName, 0.5f, 0.5f, 0.5f, 0.5f, new Vector2(180, 0), new Vector2(0.5f, 0.5f), new Vector2(220, 40));
        oppName.alignment = TextAlignmentOptions.Center;

        // Detail: header + 3 song rows (jacket + 5 diamonds + accuracy + cursor) + rating
        var detail = MakeRT("Detail", root);
        LE(detail, 250);

        var hSelf = MakeTMP("HeaderSelf", detail, 22, "you");
        AnchorBox(hSelf, 0.5f, 1, 0.5f, 1, new Vector2(-160, -8), new Vector2(0.5f, 1), new Vector2(220, 34));
        hSelf.alignment = TextAlignmentOptions.Center;
        var hRes = MakeTMP("HeaderResult", detail, 16, "win");
        AnchorBox(hRes, 0.5f, 1, 0.5f, 1, new Vector2(0, -4), new Vector2(0.5f, 1), new Vector2(160, 22));
        hRes.alignment = TextAlignmentOptions.Center;
        var hScore = MakeTMP("HeaderScore", detail, 26, "8-7");
        AnchorBox(hScore, 0.5f, 1, 0.5f, 1, new Vector2(0, -26), new Vector2(0.5f, 1), new Vector2(160, 32));
        hScore.alignment = TextAlignmentOptions.Center;
        var hOpp = MakeTMP("HeaderOpp", detail, 22, "opp");
        AnchorBox(hOpp, 0.5f, 1, 0.5f, 1, new Vector2(180, -8), new Vector2(0.5f, 1), new Vector2(220, 34));
        hOpp.alignment = TextAlignmentOptions.Center;

        // Sector header labels S1..S5 (aligned with the diamond columns below)
        float diaX0 = 240, diaStep = 90;
        for (int s = 0; s < 5; s++)
        {
            var sl = MakeTMP($"SecHead{s + 1}", detail, 14, $"S{s + 1}");
            AnchorBox(sl, 0, 1, 0, 1, new Vector2(diaX0 + s * diaStep, -64), new Vector2(0.5f, 1), new Vector2(60, 20));
            sl.alignment = TextAlignmentOptions.Center;
        }

        // 3 song rows
        for (int j = 0; j < 3; j++)
        {
            var songRow = MakeRT($"Song{j}", detail);
            AnchorStretchTop(songRow, new Vector2(0, -(90 + j * 50) - 50), new Vector2(0, -(90 + j * 50)));
            // jacket (button → replay)
            var jk = MakeRawImage("Jacket", songRow, 40, 40);
            Anchor(jk, 0, 0.5f, 0, 0.5f, new Vector2(70, 0), new Vector2(0.5f, 0.5f));
            jk.gameObject.AddComponent<Button>().targetGraphic = jk.GetComponent<RawImage>();
            // cursor marker (▷) left of jacket
            var cursor = MakeTMP("Cursor", songRow, 22, "▶");
            AnchorBox(cursor, 0, 0.5f, 0, 0.5f, new Vector2(28, 0), new Vector2(0.5f, 0.5f), new Vector2(28, 28));
            cursor.alignment = TextAlignmentOptions.Center;
            cursor.gameObject.SetActive(false);
            // hidden title (optional)
            var stitle = MakeTMP("Title", songRow, 1, "");
            AnchorBox(stitle, 0, 0.5f, 0, 0.5f, new Vector2(110, 0), new Vector2(0, 0.5f), new Vector2(10, 10));
            stitle.gameObject.SetActive(false);
            // 5 diamonds
            for (int s = 0; s < 5; s++)
            {
                var cell = MakeRT($"S{s + 1}", songRow);
                Anchor(cell, 0, 0.5f, 0, 0.5f, new Vector2(diaX0 + s * diaStep, 0), new Vector2(0.5f, 0.5f));
                cell.GetComponent<RectTransform>().sizeDelta = new Vector2(40, 40);
                var dia = MakeDiamond("Diamond", cell, 24);
                Anchor(dia, 0.5f, 0.5f, 0.5f, 0.5f, Vector2.zero, new Vector2(0.5f, 0.5f));
                dia.color = s % 2 == 0 ? CyanWin : RedLoss;
            }
            var sacc = MakeTMP("Accuracy", songRow, 18, "0.00%");
            AnchorBox(sacc, 1, 0.5f, 1, 0.5f, new Vector2(-20, 0), new Vector2(1, 0.5f), new Vector2(150, 28));
            sacc.alignment = TextAlignmentOptions.Right;
        }

        var rating = MakeTMP("RatingText", detail, 18, "R 1500 → 1500 (+0)");
        AnchorBox(rating, 0, 0, 0, 0, new Vector2(24, 14), new Vector2(0, 0), new Vector2(420, 28));

        detail.SetActive(false);

        return SaveAndDestroy(root, PvpPrefabPath);
    }

    static GameObject SaveAndDestroy(GameObject root, string path)
    {
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path, out bool ok);
        Object.DestroyImmediate(root);
        Debug.Log($"[BuildHistoryScene] Prefab saved: {path} success={ok}");
        return prefab;
    }

    // ── History.unity scene ───────────────────────────────────────────────────

    static void BuildScene(GameObject soloPrefab, GameObject pvpPrefab)
    {
        // untitled な未保存シーンが開いていると Additive 生成は例外を出すため、
        // 対話時は保存可否を確認し常に Single で作り直す(中身と配線は本メソッドで再生成)。
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SetActiveScene(scene);

        var camGO = new GameObject("Main Camera");
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.02f, 0.03f, 0.06f);
        cam.orthographic = true;
        camGO.AddComponent<AudioListener>();
        camGO.tag = "MainCamera";

        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        esGO.AddComponent<InputSystemUIInputModule>();

        var canvasGO = new GameObject("Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 1f;
        canvasGO.AddComponent<GraphicRaycaster>();

        var bgGO = MakeRT("Background", canvasGO);
        bgGO.AddComponent<Image>().color = new Color(0.02f, 0.03f, 0.06f, 1f);
        FullStretch(bgGO.GetComponent<RectTransform>());

        // Back button (top-left)
        var backBtnGO = MakeRT("BackButton", canvasGO);
        Anchor(backBtnGO, 0, 1, 0, 1, new Vector2(110, -50), new Vector2(0.5f, 0.5f));
        backBtnGO.GetComponent<RectTransform>().sizeDelta = new Vector2(160, 50);
        var backImg = backBtnGO.AddComponent<Image>();
        backImg.color = new Color(1, 1, 1, 0.1f);
        var backBtn = backBtnGO.AddComponent<Button>();
        backBtn.targetGraphic = backImg;
        var backTMP = MakeTMP("Label", backBtnGO, 22, "< Back");
        FullStretch(backTMP.GetComponent<RectTransform>());
        backTMP.alignment = TextAlignmentOptions.Center;

        // Left rail: mode tabs
        var ladderTabGO = MakeRT("LadderTab", canvasGO);
        Anchor(ladderTabGO, 0, 0.5f, 0, 0.5f, new Vector2(210, 80), new Vector2(0.5f, 0.5f));
        ladderTabGO.GetComponent<RectTransform>().sizeDelta = new Vector2(220, 60);
        var ladderImg = ladderTabGO.AddComponent<Image>();
        var ladderBtn = ladderTabGO.AddComponent<Button>();
        ladderBtn.targetGraphic = ladderImg;
        var ladderLbl = MakeTMP("Label", ladderTabGO, 22, "Ladder match");
        FullStretch(ladderLbl.GetComponent<RectTransform>());
        ladderLbl.alignment = TextAlignmentOptions.Center;

        var freeTabGO = MakeRT("FreeTab", canvasGO);
        Anchor(freeTabGO, 0, 0.5f, 0, 0.5f, new Vector2(210, -80), new Vector2(0.5f, 0.5f));
        freeTabGO.GetComponent<RectTransform>().sizeDelta = new Vector2(220, 60);
        var freeImg = freeTabGO.AddComponent<Image>();
        var freeBtn = freeTabGO.AddComponent<Button>();
        freeBtn.targetGraphic = freeImg;
        var freeLbl = MakeTMP("Label", freeTabGO, 22, "Free play");
        FullStretch(freeLbl.GetComponent<RectTransform>());
        freeLbl.alignment = TextAlignmentOptions.Center;

        // Main panel (right area)
        var panel = MakeRT("MainPanel", canvasGO);
        var panelRT = panel.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0, 0); panelRT.anchorMax = new Vector2(1, 1);
        panelRT.offsetMin = new Vector2(440, 40); panelRT.offsetMax = new Vector2(-40, -120);
        panel.AddComponent<Image>().color = new Color(1, 1, 1, 0.03f);

        // Free filter bar (top of panel): search + sort + difficulty
        var filterBar = MakeRT("FreeFilterBar", panel);
        var filterRT = filterBar.GetComponent<RectTransform>();
        filterRT.anchorMin = new Vector2(0, 1); filterRT.anchorMax = new Vector2(1, 1);
        filterRT.pivot = new Vector2(0.5f, 1f);
        filterRT.offsetMin = new Vector2(16, -68); filterRT.offsetMax = new Vector2(-16, -8);

        var searchField = MakeInputField("SearchField", filterBar, 360, "search...");
        Anchor(searchField.gameObject, 0, 0.5f, 0, 0.5f, new Vector2(190, 0), new Vector2(0.5f, 0.5f));

        var sortDD = MakeDropdown("SortDropdown", filterBar, 220);
        Anchor(sortDD.gameObject, 0, 0.5f, 0, 0.5f, new Vector2(500, 0), new Vector2(0.5f, 0.5f));

        // Difficulty buttons (right): easy / normal / hard / extra
        string[] diffLabels = { "easy", "normal", "hard", "extra" };
        var diffBtns = new Button[4];
        var diffBgs  = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            var d = MakeRT($"Diff_{diffLabels[i]}", filterBar);
            Anchor(d, 1, 0.5f, 1, 0.5f, new Vector2(-16 - (3 - i) * 90, 0), new Vector2(1, 0.5f));
            d.GetComponent<RectTransform>().sizeDelta = new Vector2(86, 40);
            var dImg = d.AddComponent<Image>();
            dImg.color = new Color(1, 1, 1, 0f);
            var dBtn = d.AddComponent<Button>();
            dBtn.targetGraphic = dImg;
            var dLbl = MakeTMP("Label", d, 18, diffLabels[i]);
            FullStretch(dLbl.GetComponent<RectTransform>());
            dLbl.alignment = TextAlignmentOptions.Center;
            diffBtns[i] = dBtn;
            diffBgs[i]  = dImg;
        }

        // ScrollView (list)
        var svGO = MakeRT("ScrollView", panel);
        var svRT = svGO.GetComponent<RectTransform>();
        svRT.anchorMin = new Vector2(0, 0); svRT.anchorMax = new Vector2(1, 1);
        svRT.offsetMin = new Vector2(16, 16); svRT.offsetMax = new Vector2(-16, -76);

        var viewport = MakeRT("Viewport", svGO);
        FullStretch(viewport.GetComponent<RectTransform>());
        viewport.AddComponent<Image>().color = new Color(0, 0, 0, 0);
        viewport.AddComponent<RectMask2D>();

        var listContentGO = MakeRT("Content", viewport);
        var listContentRT = listContentGO.GetComponent<RectTransform>();
        listContentRT.anchorMin = new Vector2(0, 1); listContentRT.anchorMax = new Vector2(1, 1);
        listContentRT.pivot = new Vector2(0.5f, 1f);
        listContentRT.offsetMin = Vector2.zero; listContentRT.offsetMax = Vector2.zero;
        var vlg = listContentGO.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 6;
        vlg.childControlWidth  = true;  vlg.childForceExpandWidth  = true;
        vlg.childControlHeight = true;  vlg.childForceExpandHeight = false;
        listContentGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scrollRect = svGO.AddComponent<ScrollRect>();
        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.content  = listContentRT;
        scrollRect.horizontal = false;
        scrollRect.vertical   = true;

        // Empty state
        var emptyState = MakeTMP("EmptyState", panel, 24, "No plays yet");
        var emptyRT = emptyState.GetComponent<RectTransform>();
        emptyRT.anchorMin = new Vector2(0.5f, 0.5f); emptyRT.anchorMax = new Vector2(0.5f, 0.5f);
        emptyRT.anchoredPosition = Vector2.zero; emptyRT.sizeDelta = new Vector2(500, 60);
        emptyState.alignment = TextAlignmentOptions.Center;
        emptyState.color = new Color(1, 1, 1, 0.5f);
        emptyState.gameObject.SetActive(false);

        // Controller
        var ctrlGO = new GameObject("HistoryController");
        var ctrl = ctrlGO.AddComponent<HistoryController>();
        if (soloPrefab == null) soloPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SoloPrefabPath);
        if (pvpPrefab  == null) pvpPrefab  = AssetDatabase.LoadAssetAtPath<GameObject>(PvpPrefabPath);
        if (soloPrefab == null || pvpPrefab == null)
            Debug.LogError("[BuildHistoryScene] Prefab asset missing — solo=" + (soloPrefab != null) + " pvp=" + (pvpPrefab != null));

        var so = new SerializedObject(ctrl);
        so.FindProperty("_backButton").objectReferenceValue   = backBtn;
        so.FindProperty("_ladderTab").objectReferenceValue    = ladderBtn;
        so.FindProperty("_ladderTabBg").objectReferenceValue  = ladderImg;
        so.FindProperty("_freeTab").objectReferenceValue      = freeBtn;
        so.FindProperty("_freeTabBg").objectReferenceValue    = freeImg;
        so.FindProperty("_freeFilterBar").objectReferenceValue = filterBar;
        so.FindProperty("_searchField").objectReferenceValue  = searchField;
        so.FindProperty("_sortDropdown").objectReferenceValue = sortDD;
        SetArray(so, "_diffButtons",   diffBtns);
        SetArray(so, "_diffButtonBgs", diffBgs);
        so.FindProperty("_listContent").objectReferenceValue   = listContentRT;
        so.FindProperty("_scrollRect").objectReferenceValue    = scrollRect;
        so.FindProperty("_soloItemPrefab").objectReferenceValue = soloPrefab;
        so.FindProperty("_pvpItemPrefab").objectReferenceValue  = pvpPrefab;
        so.FindProperty("_emptyState").objectReferenceValue     = emptyState.gameObject;
        so.FindProperty("_emptyStateText").objectReferenceValue = emptyState;
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
        so.ApplyModifiedPropertiesWithoutUndo();

        string scenePath = "Assets/_Project/Scenes/History.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log("[BuildHistoryScene] Scene saved: " + scenePath);

        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!scenes.Exists(s => s.path == scenePath))
        {
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[BuildHistoryScene] Added to Build Settings");
        }
    }

    static void SetArray(SerializedObject so, string prop, Object[] values)
    {
        var p = so.FindProperty(prop);
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static GameObject MakeRT(string name, GameObject parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<RectTransform>();
        return go;
    }
    static GameObject MakeRT(string name, RectTransform parent) => MakeRT(name, parent.gameObject);

    static void FullStretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    static void Anchor(GameObject go, float aMinX, float aMinY, float aMaxX, float aMaxY,
                       Vector2 pos, Vector2 pivot)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(aMinX, aMinY);
        rt.anchorMax = new Vector2(aMaxX, aMaxY);
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
    }

    static void Anchor(Component c, float aMinX, float aMinY, float aMaxX, float aMaxY,
                       Vector2 pos, Vector2 pivot)
        => Anchor(c.gameObject, aMinX, aMinY, aMaxX, aMaxY, pos, pivot);

    static void AnchorBox(Component c, float aMinX, float aMinY, float aMaxX, float aMaxY,
                          Vector2 pos, Vector2 pivot, Vector2 size)
        => AnchorBox(c.gameObject, aMinX, aMinY, aMaxX, aMaxY, pos, pivot, size);

    static void AnchorBox(GameObject go, float aMinX, float aMinY, float aMaxX, float aMaxY,
                          Vector2 pos, Vector2 pivot, Vector2 size)
    {
        Anchor(go, aMinX, aMinY, aMaxX, aMaxY, pos, pivot);
        go.GetComponent<RectTransform>().sizeDelta = size;
    }

    static void AnchorStretchTop(GameObject go, Vector2 offMin, Vector2 offMax)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = offMin; rt.offsetMax = offMax;
    }

    static void LE(GameObject go, float preferredHeight)
    {
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = preferredHeight;
        le.minHeight = preferredHeight;
    }

    static void Ignore(GameObject go) => go.AddComponent<LayoutElement>().ignoreLayout = true;

    static TextMeshProUGUI MakeTMP(string name, GameObject parent, int size, string text, Color? color = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var rt = go.AddComponent<RectTransform>();
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = size; tmp.color = color ?? Color.white;
        rt.sizeDelta = new Vector2(300, size + 10);
        return tmp;
    }
    static TextMeshProUGUI MakeTMP(string name, RectTransform parent, int size, string text) =>
        MakeTMP(name, parent.gameObject, size, text);

    static RawImage MakeRawImage(string name, GameObject parent, float w, float h)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(w, h);
        var ri = go.AddComponent<RawImage>();
        ri.color = new Color(1, 1, 1, 1);
        return ri;
    }

    static Image MakeDiamond(string name, GameObject parent, float size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(size, size);
        rt.localEulerAngles = new Vector3(0, 0, 45);   // square → diamond
        var img = go.AddComponent<Image>();
        img.color = Color.white;
        return img;
    }

    static GameObject MakeBadge(string name, GameObject parent, string text, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<RectTransform>().sizeDelta = new Vector2(54, 32);
        var bg = go.AddComponent<Image>();
        bg.color = new Color(color.r, color.g, color.b, 0.25f);
        var lbl = new GameObject("Label");
        lbl.transform.SetParent(go.transform, false);
        var lrt = lbl.AddComponent<RectTransform>();
        FullStretch(lrt);
        var t = lbl.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = 16; t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        return go;
    }

    static TMP_Dropdown MakeDropdown(string name, GameObject parent, float width)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(width, 40);
        go.AddComponent<Image>().color = new Color(1, 1, 1, 0.1f);
        var dd = go.AddComponent<TMP_Dropdown>();

        // Caption label
        var caption = MakeTMP("Label", go, 18, "ソート：曲名");
        var capRT = caption.GetComponent<RectTransform>();
        capRT.anchorMin = new Vector2(0, 0); capRT.anchorMax = new Vector2(1, 1);
        capRT.offsetMin = new Vector2(10, 2); capRT.offsetMax = new Vector2(-25, -2);
        caption.alignment = TextAlignmentOptions.Left;
        dd.captionText = caption;

        // Template (collapsed dropdown list)
        var template = MakeRT("Template", go);
        var tRT = template.GetComponent<RectTransform>();
        tRT.anchorMin = new Vector2(0, 0); tRT.anchorMax = new Vector2(1, 0);
        tRT.pivot = new Vector2(0.5f, 1f);
        tRT.anchoredPosition = new Vector2(0, 2); tRT.sizeDelta = new Vector2(0, 150);
        template.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.14f, 1f);
        var tScroll = template.AddComponent<ScrollRect>();

        var tViewport = MakeRT("Viewport", template);
        FullStretch(tViewport.GetComponent<RectTransform>());
        tViewport.AddComponent<Image>().color = new Color(0, 0, 0, 0);
        tViewport.AddComponent<Mask>().showMaskGraphic = false;

        var tContent = MakeRT("Content", tViewport);
        var tcRT = tContent.GetComponent<RectTransform>();
        tcRT.anchorMin = new Vector2(0, 1); tcRT.anchorMax = new Vector2(1, 1);
        tcRT.pivot = new Vector2(0.5f, 1f); tcRT.sizeDelta = new Vector2(0, 32);

        var tItem = MakeRT("Item", tContent);
        var tiRT = tItem.GetComponent<RectTransform>();
        tiRT.anchorMin = new Vector2(0, 0.5f); tiRT.anchorMax = new Vector2(1, 0.5f);
        tiRT.sizeDelta = new Vector2(0, 30);
        var tItemToggle = tItem.AddComponent<Toggle>();

        var tItemBg = MakeRT("Item Background", tItem);
        FullStretch(tItemBg.GetComponent<RectTransform>());
        tItemBg.AddComponent<Image>().color = new Color(1, 1, 1, 0.05f);

        var tItemLabel = MakeTMP("Item Label", tItem, 18, "Option");
        var tilRT = tItemLabel.GetComponent<RectTransform>();
        tilRT.anchorMin = new Vector2(0, 0); tilRT.anchorMax = new Vector2(1, 1);
        tilRT.offsetMin = new Vector2(10, 1); tilRT.offsetMax = new Vector2(-10, -1);
        tItemLabel.alignment = TextAlignmentOptions.Left;

        tScroll.content  = tcRT;
        tScroll.viewport = tViewport.GetComponent<RectTransform>();
        tItemToggle.targetGraphic = tItemBg.GetComponent<Image>();

        dd.template      = tRT;
        dd.itemText      = tItemLabel;
        template.SetActive(false);

        return dd;
    }

    static TMP_InputField MakeInputField(string name, GameObject parent, float width, string placeholder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(width, 44);
        go.AddComponent<Image>().color = new Color(1, 1, 1, 0.1f);
        var field = go.AddComponent<TMP_InputField>();

        var textArea = MakeRT("Text Area", go);
        var taRT = textArea.GetComponent<RectTransform>();
        taRT.anchorMin = new Vector2(0, 0); taRT.anchorMax = new Vector2(1, 1);
        taRT.offsetMin = new Vector2(12, 4); taRT.offsetMax = new Vector2(-12, -4);
        textArea.AddComponent<RectMask2D>();

        var ph = MakeTMP("Placeholder", textArea, 18, placeholder, new Color(1, 1, 1, 0.4f));
        FullStretch(ph.GetComponent<RectTransform>());
        var txt = MakeTMP("Text", textArea, 18, "");
        FullStretch(txt.GetComponent<RectTransform>());

        field.textViewport  = taRT;
        field.textComponent = txt;
        field.placeholder   = ph;
        field.text          = "";
        return field;
    }
}
