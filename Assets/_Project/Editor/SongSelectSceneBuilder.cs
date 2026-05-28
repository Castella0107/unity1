#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Editor-only helper that builds the SongSelect scene (DJMAX-style:
/// 詳細ペイン=左 / 楽曲リスト=右 / プロフィールカード=右上).
/// </summary>
public static class SongSelectSceneBuilder
{
    const string ScenePath  = "Assets/_Project/Scenes/SongSelect.unity";
    const string PrefabPath = "Assets/_Project/Prefabs/UI/SongListItem.prefab";

    // Layout constants
    const float TopBarH   = 90f;
    const float DetailW   = 560f;
    const float Pad       = 30f;

    static readonly Color Accent  = new Color(0.31f, 0.62f, 0.97f);   // blue
    static readonly Color Cyan    = new Color(0.31f, 0.76f, 0.97f);
    static readonly Color Dim     = new Color(.7f, .7f, .7f);
    static readonly Color Faint   = new Color(1f, 1f, 1f, .12f);

    /// <summary>SongSelect シーンをスクラッチから構築する。</summary>
    [MenuItem("Tools/Build SongSelect Scene")]
    public static void Build()
    {
        EnsureFolder("Assets/_Project/Prefabs");
        EnsureFolder("Assets/_Project/Prefabs/UI");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── Camera ────────────────────────────────────────────────────────────
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.transform.position = new Vector3(0, 1, -10);
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Hex("050810");
        camGO.AddComponent<AudioListener>();

        // ── EventSystem ───────────────────────────────────────────────────────
        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        esGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

        // ── Canvas ────────────────────────────────────────────────────────────
        var canvasGO = new GameObject("Canvas");
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight  = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        var ct = canvasGO.transform;

        // Background
        var bgGO = Child("Background", ct);
        SR(bgGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        bgGO.AddComponent<Image>().color = Hex("050810");

        // ════════════════════════════════════════════════════════════════════
        // TOP BAR
        // ════════════════════════════════════════════════════════════════════
        var topGO = Child("TopBar", ct);
        SR(topGO, V(0,1), V(1,1), V(.5f,1), V(0,0), V(0,TopBarH));

        // Mode logo (left)
        var logoGO = Child("ModeLogo", topGO.transform);
        SR(logoGO, V(0,0), V(0,1), V(0,.5f), V(Pad,0), V(360,0));
        T(logoGO, "△▽✕  FREE PLAY", 30, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

        // Back button (left, under-ish — small)
        var backBtnGO  = Child("BackButton", topGO.transform);
        SR(backBtnGO, V(0,0), V(0,1), V(0,.5f), V(Pad+360,0), V(110,-30));
        var backBtnImg = backBtnGO.AddComponent<Image>(); backBtnImg.color = Faint;
        var backBtn    = backBtnGO.AddComponent<Button>(); backBtn.targetGraphic = backBtnImg;
        var backLbl    = Child("Label", backBtnGO.transform);
        SR(backLbl, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        T(backLbl, "< BACK", 16, Color.white, TextAlignmentOptions.Center);

        // Profile card (right)
        var (profName, profSub) = BuildProfileCard(topGO.transform);

        // ════════════════════════════════════════════════════════════════════
        // MAIN AREA
        // ════════════════════════════════════════════════════════════════════
        var mainGO = Child("MainArea", ct);
        SR(mainGO, V(0,0), V(1,1), V(.5f,.5f), V(0,-(TopBarH+10)/2f), V(0,-(TopBarH+10)-20));

        // ─────────────────────────────────────────────────────────────────────
        // DETAIL PANE (left)
        // ─────────────────────────────────────────────────────────────────────
        var detailGO = Child("DetailPane", mainGO.transform);
        SR(detailGO, V(0,0), V(0,1), V(0,.5f), V(Pad,0), V(DetailW,0));
        detailGO.AddComponent<Image>().color = new Color(0,0,0,.25f);
        var dt = detailGO.transform;

        float W = DetailW;       // local content width reference
        float y = -14f;          // running top offset inside detail pane

        // Category tag
        var catGO = Child("CategoryTag", dt);
        SR(catGO, V(0,1), V(0,1), V(0,1), V(14, y), V(220,26));
        catGO.AddComponent<Image>().color = Accent;
        var catLbl = Child("Label", catGO.transform);
        SR(catLbl, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        T(catLbl, "FREE PLAY", 14, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        y -= 34;

        // Jacket
        var jaGO = Child("JacketArea", dt);
        SR(jaGO, V(0,1), V(1,1), V(.5f,1), V(0, y), V(-28, 300));
        var jiGO = Child("JacketImage", jaGO.transform);
        SR(jiGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        var jacketRaw = jiGO.AddComponent<RawImage>(); jacketRaw.color = new Color(.25f,.25f,.25f);
        var jfGO = Child("JacketFrame", jaGO.transform);
        SR(jfGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        var jfImg = jfGO.AddComponent<Image>(); jfImg.color = new Color(.6f,.6f,.6f,.25f); jfImg.raycastTarget = false;
        y -= 308;

        // Title
        var stGO  = Child("TitleText", dt);
        SR(stGO, V(0,1), V(1,1), V(.5f,1), V(0, y), V(-28, 40));
        var stTMP = T(stGO, "---", 32, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        stTMP.overflowMode = TextOverflowModes.Ellipsis; Indent(stGO, 14);
        y -= 42;

        // Artist
        var artGO  = Child("ArtistText", dt);
        SR(artGO, V(0,1), V(1,1), V(.5f,1), V(0, y), V(-28, 24));
        var artTMP = T(artGO, "---", 18, Dim, TextAlignmentOptions.MidlineLeft);
        artTMP.overflowMode = TextOverflowModes.Ellipsis; Indent(artGO, 14);
        y -= 26;

        // BPM / Length
        var bpmGO  = Child("BpmDurationText", dt);
        SR(bpmGO, V(0,1), V(1,1), V(.5f,1), V(0, y), V(-28, 22));
        var bpmTMP = T(bpmGO, "BPM ---   Length 0:00", 15, new Color(.6f,.6f,.6f), TextAlignmentOptions.MidlineLeft);
        Indent(bpmGO, 14);
        y -= 30;

        // Difficulty buttons
        var (diffBtns, diffLvlTexts) = BuildDifficultyRow(dt, ref y, W);

        // Stats — one row (SCORE / RATE / COMBO) + rank
        var (statsTMP, rankTMP) = BuildStatsRow(dt, ref y, W);

        // Sector diamonds
        var sectorIcons = BuildSectorRow(dt, ref y, W);

        // Settings
        var (hiSpdSldr, hiSpdVal, offSldr, offVal, saveBtn, saveLbl, modDD) =
            BuildSettings(dt, ref y, W);

        // Key hint (Enter plays / Esc back) — replaces the PLAY button
        var hintGO = Child("KeyHint", dt);
        SR(hintGO, V(0,0), V(1,0), V(.5f,0), V(0,16), V(-28, 30));
        T(hintGO, "ENTER ▷ PLAY     ESC ◁ BACK", 16, new Color(.75f,.75f,.75f),
          TextAlignmentOptions.Center, FontStyles.Bold);

        // ─────────────────────────────────────────────────────────────────────
        // LIST PANE (right)
        // ─────────────────────────────────────────────────────────────────────
        var listGO = Child("ListPane", mainGO.transform);
        SR(listGO, V(0,0), V(1,1), V(.5f,.5f), V((Pad+DetailW+20)/2f, 0), V(-(Pad+DetailW+20)-Pad, 0));
        var lt = listGO.transform;

        // Sort header (clickable → cycles sort)
        var sortGO  = Child("SortHeader", lt);
        SR(sortGO, V(0,1), V(1,1), V(.5f,1), V(0,0), V(0,52));
        var sortImg = sortGO.AddComponent<Image>(); sortImg.color = new Color(1,1,1,.06f);
        var sortBtn = sortGO.AddComponent<Button>(); sortBtn.targetGraphic = sortImg;

        var burgGO = Child("Burger", sortGO.transform);
        SR(burgGO, V(0,.5f), V(0,.5f), V(0,.5f), V(18,0), V(22,16));
        T(burgGO, "≡", 26, Color.white, TextAlignmentOptions.Center);

        var sortByGO = Child("SortByLabel", sortGO.transform);
        SR(sortByGO, V(0,0), V(0,1), V(0,.5f), V(52,0), V(110,0));
        T(sortByGO, "SORT BY", 18, Dim, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

        var sortValGO = Child("SortLabel", sortGO.transform);
        SR(sortValGO, V(0,0), V(0,1), V(0,.5f), V(168,0), V(360,0));
        var sortLabelTMP = T(sortValGO, "TITLE (A to Z)", 18, Hex("FFD23C"), TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

        var f4GO = Child("F4Hint", sortGO.transform);
        SR(f4GO, V(1,.5f), V(1,.5f), V(1,.5f), V(-18,0), V(44,26));
        f4GO.AddComponent<Image>().color = Faint;
        var f4Lbl = Child("Label", f4GO.transform);
        SR(f4Lbl, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        T(f4Lbl, "F4", 14, Color.white, TextAlignmentOptions.Center);

        // Category tab bar (visual; ALL TRACK active)
        var tabsGO = Child("CategoryTabs", lt);
        SR(tabsGO, V(0,1), V(1,1), V(.5f,1), V(0,-58), V(0,34));
        var tHLG = tabsGO.AddComponent<HorizontalLayoutGroup>();
        tHLG.childControlWidth = false; tHLG.childForceExpandWidth = false;
        tHLG.childControlHeight = true; tHLG.childForceExpandHeight = true;
        tHLG.spacing = 26; tHLG.childAlignment = TextAnchor.MiddleLeft;
        tHLG.padding = new RectOffset(8,8,0,0);
        string[] tabs = { "ALL TRACK", "PLAYABLE", "FAVORITE", "RECENT", "CLEAR" };
        for (int i = 0; i < tabs.Length; i++)
        {
            var tgGO = Child("Tab_" + tabs[i], tabsGO.transform);
            LE(tgGO, 110, 30, false);
            T(tgGO, tabs[i], 16, i == 0 ? Color.white : new Color(.5f,.5f,.5f),
              TextAlignmentOptions.Center, i == 0 ? FontStyles.Bold : FontStyles.Normal);
        }

        // ScrollView (below header+tabs)
        var (scrollRect, contentRT) = BuildScrollView(lt, 100f);

        // ════════════════════════════════════════════════════════════════════
        // CONTROLLER WIRING
        // ════════════════════════════════════════════════════════════════════
        var ctrlGO = new GameObject("SongSelectController");
        var ctrl   = ctrlGO.AddComponent<SongSelectController>();
        var so     = new SerializedObject(ctrl);

        SetRef(so, "_listContent", contentRT);
        SetRef(so, "_scrollRect",  scrollRect);
        SetRef(so, "_sortButton",  sortBtn);
        SetRef(so, "_sortLabel",   sortLabelTMP);
        SetRef(so, "_jacketImage", jacketRaw);
        SetRef(so, "_titleText",   stTMP);
        SetRef(so, "_artistText",  artTMP);
        SetRef(so, "_bpmDurationText", bpmTMP);
        SetRef(so, "_statsText",   statsTMP);
        SetRef(so, "_bestRankText",rankTMP);
        SetRef(so, "_btnEasy",     diffBtns[0]);
        SetRef(so, "_btnNormal",   diffBtns[1]);
        SetRef(so, "_btnHard",     diffBtns[2]);
        SetRef(so, "_btnExtra",    diffBtns[3]);
        SetArr(so, "_diffLevelTexts", diffLvlTexts);
        SetArr(so, "_sectorIcons", sectorIcons);
        SetRef(so, "_hiSpeedSlider", hiSpdSldr);
        SetRef(so, "_hiSpeedValue",  hiSpdVal);
        SetRef(so, "_perSongOffsetSlider", offSldr);
        SetRef(so, "_perSongOffsetValue",  offVal);
        SetRef(so, "_perSongOffsetSaveButton", saveBtn);
        SetRef(so, "_saveButtonLabel", saveLbl);
        SetRef(so, "_modifierDropdown", modDD);
        SetRef(so, "_profileName", profName);
        SetRef(so, "_profileSub",  profSub);
        SetRef(so, "_backButton",  backBtn);

        foreach (var guid in AssetDatabase.FindAssets("InputActions t:InputActionAsset"))
        {
            var iaPath = AssetDatabase.GUIDToAssetPath(guid);
            if (iaPath.Contains("_Project"))
            {
                SetRef(so, "_inputAsset",
                    AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(iaPath));
                break;
            }
        }
        so.ApplyModifiedProperties();

        // SongListItem prefab
        var prefabRoot  = BuildPrefabGO();
        var savedPrefab = PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
        Object.DestroyImmediate(prefabRoot);

        var so2 = new SerializedObject(ctrl);
        SetRef(so2, "_songItemPrefab", savedPrefab);
        so2.ApplyModifiedProperties();

        // Save
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();
        Debug.Log("[SongSelectSceneBuilder] Done → " + ScenePath);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // SECTION BUILDERS
    // ═════════════════════════════════════════════════════════════════════════

    static (TextMeshProUGUI, TextMeshProUGUI) BuildProfileCard(Transform parent)
    {
        var cardGO = Child("ProfileCard", parent);
        SR(cardGO, V(1,.5f), V(1,.5f), V(1,.5f), V(-Pad,0), V(360,62));
        cardGO.AddComponent<Image>().color = new Color(0,0,0,.4f);

        var avGO = Child("Avatar", cardGO.transform);
        SR(avGO, V(0,.5f), V(0,.5f), V(0,.5f), V(8,0), V(46,46));
        avGO.AddComponent<RawImage>().color = new Color(.3f,.3f,.4f);

        var nameGO = Child("Name", cardGO.transform);
        SR(nameGO, V(0,.5f), V(1,1), V(0,1), V(64,-6), V(-72,26));
        var nameTMP = T(nameGO, "player", 20, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        nameTMP.overflowMode = TextOverflowModes.Ellipsis;

        var subGO = Child("Sub", cardGO.transform);
        SR(subGO, V(0,0), V(1,.5f), V(0,0), V(64,6), V(-72,22));
        var subTMP = T(subGO, "FREE PLAY", 14, Hex("FFD23C"), TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

        return (nameTMP, subTMP);
    }

    static (Button[], TextMeshProUGUI[]) BuildDifficultyRow(Transform dt, ref float y, float W)
    {
        var diffGO = Child("DifficultyButtons", dt);
        SR(diffGO, V(0,1), V(1,1), V(.5f,1), V(0, y), V(-28, 52));
        var dHLG = diffGO.AddComponent<HorizontalLayoutGroup>();
        dHLG.childControlWidth  = true; dHLG.childForceExpandWidth  = true;
        dHLG.childControlHeight = true; dHLG.childForceExpandHeight = true;
        dHLG.spacing = 8; dHLG.padding = new RectOffset(14,14,0,0);

        string[] dlabels = { "EZ","NM","HD","EX" };
        Color[] dcolors = {
            new Color(.2f,.75f,.35f), new Color(.2f,.5f,.9f),
            new Color(.9f,.5f,.1f),   new Color(.85f,.1f,.5f)
        };
        var diffBtns  = new Button[4];
        var diffTexts = new TextMeshProUGUI[4];
        for (int i = 0; i < 4; i++)
        {
            var bGO  = Child("Btn" + dlabels[i], diffGO.transform);
            var bImg = bGO.AddComponent<Image>(); bImg.color = Faint;
            var btn  = bGO.AddComponent<Button>(); btn.targetGraphic = bImg;
            var tGO = Child("Text", bGO.transform);
            SR(tGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
            var tTMP = T(tGO, dlabels[i] + " -", 18, dcolors[i], TextAlignmentOptions.Center, FontStyles.Bold);
            diffBtns[i] = btn; diffTexts[i] = tTMP;
        }
        y -= 60;
        return (diffBtns, diffTexts);
    }

    static (TextMeshProUGUI, TextMeshProUGUI) BuildStatsRow(Transform dt, ref float y, float W)
    {
        var rowGO = Child("StatsRow", dt);
        SR(rowGO, V(0,1), V(1,1), V(.5f,1), V(0, y), V(-28, 40));
        rowGO.AddComponent<Image>().color = new Color(0,0,0,.25f);

        // Rank (right)
        var rankGO = Child("Rank", rowGO.transform);
        SR(rankGO, V(1,0), V(1,1), V(1,.5f), V(-8,0), V(70,0));
        var rankTMP = T(rankGO, "-", 28, Hex("FFD23C"), TextAlignmentOptions.Center, FontStyles.Bold);

        // SCORE / RATE / COMBO を1行に (controller がまとめて文字列を入れる)
        var stGO = Child("StatsLine", rowGO.transform);
        var stRT = stGO.GetComponent<RectTransform>();
        stRT.anchorMin = V(0,0); stRT.anchorMax = V(1,1);
        stRT.offsetMin = V(14,0); stRT.offsetMax = V(-78,0);
        var statsTMP = T(stGO, "SCORE 0     RATE 0.00%     COMBO 0", 16, Color.white,
                         TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

        y -= 48;
        return (statsTMP, rankTMP);
    }

    static Image[] BuildSectorRow(Transform dt, ref float y, float W)
    {
        var rowGO = Child("SectorRow", dt);
        SR(rowGO, V(0,1), V(1,1), V(.5f,1), V(0, y), V(-28, 30));
        var lGO = Child("Label", rowGO.transform);
        SR(lGO, V(0,0), V(0,1), V(0,.5f), V(14,0), V(90,0));
        T(lGO, "SECTOR", 13, Dim, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

        var icons = new Image[5];
        for (int i = 0; i < 5; i++)
        {
            var dGO = Child("Sector" + i, rowGO.transform);
            SR(dGO, V(0,.5f), V(0,.5f), V(.5f,.5f), V(118 + i*38, 0), V(22,22));
            dGO.transform.localRotation = Quaternion.Euler(0,0,45);   // diamond
            var img = dGO.AddComponent<Image>(); img.color = Faint; img.raycastTarget = false;
            icons[i] = img;
        }
        y -= 38;
        return icons;
    }

    static (Slider, TextMeshProUGUI, Slider, TextMeshProUGUI, Button, TextMeshProUGUI, TMP_Dropdown)
        BuildSettings(Transform dt, ref float y, float W)
    {
        var setGO = Child("SettingsPanel", dt);
        SR(setGO, V(0,1), V(1,1), V(.5f,1), V(0, y), V(-28, 180));
        setGO.AddComponent<Image>().color = new Color(0,0,0,.3f);

        var (hiSpdSldr, hiSpdVal) = SliderRow(setGO.transform, "SPEED", -8,  .5f, 20f, 4.5f, false);
        var (offSldr,   offVal)   = SliderRow(setGO.transform, "OFFSET", -54, PerSongOffset.MinMs, PerSongOffset.MaxMs, 0f, true, rightInset: 80);

        // SAVE button on the offset row (top-right area)
        var saveGO  = Child("OffsetSave", setGO.transform);
        SR(saveGO, V(1,1), V(1,1), V(1,1), V(-12,-50), V(64,30));
        var saveImg = saveGO.AddComponent<Image>(); saveImg.color = Faint;
        var saveBtn = saveGO.AddComponent<Button>(); saveBtn.targetGraphic = saveImg;
        var saveLblGO = Child("Label", saveGO.transform);
        SR(saveLblGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        var saveLbl = T(saveLblGO, "SAVE", 14, new Color(1,1,1,.35f), TextAlignmentOptions.Center, FontStyles.Bold);

        // Modifier row
        var modRowGO = Child("ModifierRow", setGO.transform);
        SR(modRowGO, V(0,1), V(1,1), V(.5f,1), V(0,-100), V(-24, 44));
        var mLblGO = Child("Label", modRowGO.transform);
        SR(mLblGO, V(0,0), V(0,1), V(0,.5f), V(16,0), V(120,0));
        T(mLblGO, "MODIFIER", 14, Dim, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        var modDD = MakeDropdown(modRowGO.transform);

        return (hiSpdSldr, hiSpdVal, offSldr, offVal, saveBtn, saveLbl, modDD);
    }

    static (ScrollRect, RectTransform) BuildScrollView(Transform parent, float topInset)
    {
        var svGO = Child("ScrollView", parent);
        SR(svGO, V(0,0), V(1,1), V(.5f,.5f), V(0, -topInset/2f), V(0, -topInset));
        var scrollRect = svGO.AddComponent<ScrollRect>();
        scrollRect.horizontal = false; scrollRect.scrollSensitivity = 30f;

        var vpGO  = Child("Viewport", svGO.transform);
        SR(vpGO, V(0,0), V(1,1), V(0,0), V(0,0), V(-14,0));
        var vpImg = vpGO.AddComponent<Image>(); vpImg.color = Color.white;
        vpGO.AddComponent<Mask>().showMaskGraphic = false;

        var contentGO = Child("Content", vpGO.transform);
        var contentRT = SR(contentGO, V(0,1), V(1,1), V(.5f,1), V(0,0), V(0,0));
        var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
        vlg.childControlHeight = false; vlg.childForceExpandHeight = false;
        vlg.childControlWidth  = true;  vlg.childForceExpandWidth  = true;
        vlg.spacing = 4; vlg.padding = new RectOffset(0,0,4,4);
        var csf = contentGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var sbGO  = Child("Scrollbar", svGO.transform);
        SR(sbGO, V(1,0), V(1,1), V(1,.5f), V(0,0), V(12,0));
        sbGO.AddComponent<Image>().color = new Color(1,1,1,.1f);
        var sb = sbGO.AddComponent<Scrollbar>(); sb.direction = Scrollbar.Direction.BottomToTop;
        var sbSlideGO = Child("Sliding Area", sbGO.transform);
        SR(sbSlideGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(-20,-20));
        var sbHdlGO = Child("Handle", sbSlideGO.transform);
        SR(sbHdlGO, V(0,0), V(1,.2f), V(.5f,.5f), V(0,0), V(20,20));
        var sbHdlImg = sbHdlGO.AddComponent<Image>(); sbHdlImg.color = new Color(.3f,.5f,.9f,.8f);
        sb.handleRect = sbHdlGO.GetComponent<RectTransform>(); sb.targetGraphic = sbHdlImg;
        scrollRect.viewport = vpGO.GetComponent<RectTransform>(); scrollRect.content = contentRT;
        scrollRect.verticalScrollbar = sb;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

        return (scrollRect, contentRT);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Prefab — list row
    // ═════════════════════════════════════════════════════════════════════════

    static GameObject BuildPrefabGO()
    {
        var root   = new GameObject("SongListItem");
        var rootRT = root.AddComponent<RectTransform>(); rootRT.sizeDelta = V(900, 64);
        var rootImg = root.AddComponent<Image>(); rootImg.color = new Color(1,1,1,.05f);
        var rootBtn = root.AddComponent<Button>(); rootBtn.targetGraphic = rootImg;

        var bgGO = Child("Background", root.transform);
        SR(bgGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        var bgImg = bgGO.AddComponent<Image>(); bgImg.color = new Color(1,1,1,.05f); bgImg.raycastTarget = false;

        // Jacket thumbnail
        var jkGO = Child("Jacket", root.transform);
        SR(jkGO, V(0,.5f), V(0,.5f), V(0,.5f), V(8,0), V(48,48));
        var jkImg = jkGO.AddComponent<Image>(); jkImg.color = new Color(.3f,.3f,.35f); jkImg.raycastTarget = false;

        var titleGO = Child("TitleText", root.transform);
        SR(titleGO, V(0,.5f), V(1,1), V(0,.5f), V(66,-2), V(-260,30));
        var ttmp = T(titleGO, "Song Title", 20, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        ttmp.overflowMode = TextOverflowModes.Ellipsis; ttmp.raycastTarget = false;

        var artistGO = Child("ArtistText", root.transform);
        SR(artistGO, V(0,0), V(1,.5f), V(0,.5f), V(66,2), V(-260,24));
        var atmp = T(artistGO, "Artist", 13, new Color(.65f,.65f,.65f), TextAlignmentOptions.MidlineLeft);
        atmp.raycastTarget = false;

        // Difficulty cells (4) on the right (visual placeholder)
        string[] d = { "EZ","NM","HD","EX" };
        for (int i = 0; i < 4; i++)
        {
            var cGO = Child("Diff" + d[i], root.transform);
            SR(cGO, V(1,.5f), V(1,.5f), V(1,.5f), V(-14 - (3-i)*52, 0), V(46,40));
            var cImg = cGO.AddComponent<Image>(); cImg.color = new Color(1,1,1,.06f); cImg.raycastTarget = false;
            var cl = Child("Lv", cGO.transform);
            SR(cl, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
            var clt = T(cl, "✕", 16, new Color(.45f,.45f,.45f), TextAlignmentOptions.Center, FontStyles.Bold);
            clt.raycastTarget = false;
        }

        return root;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // UI helpers
    // ═════════════════════════════════════════════════════════════════════════

    static (Slider, TextMeshProUGUI) SliderRow(
        Transform parent, string label, float topY,
        float min, float max, float def, bool wholeNums, float rightInset = 0)
    {
        var rowGO = Child(label + "Row", parent);
        SR(rowGO, V(0,1), V(1,1), V(.5f,1), V(0, topY), V(-24, 40));

        var lGO = Child("Label", rowGO.transform);
        SR(lGO, V(0,0), V(0,1), V(0,.5f), V(16,0), V(110,0));
        T(lGO, label, 14, new Color(.8f,.8f,.8f), TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

        var valStr = wholeNums ? $"{(int)def} ms" : def.ToString("F1");
        var vGO = Child("Value", rowGO.transform);
        SR(vGO, V(1,0), V(1,1), V(1,.5f), V(-(12+rightInset),0), V(80,0));
        var vTMP = T(vGO, valStr, 16, Color.white, TextAlignmentOptions.MidlineRight, FontStyles.Bold);

        var sGO = Child("Slider", rowGO.transform);
        SR(sGO, V(0,.5f), V(1,.5f), V(.5f,.5f), V(0,0), V(-(220+rightInset),20));
        // center slider between label and value
        var sRT = sGO.GetComponent<RectTransform>();
        sRT.anchorMin = new Vector2(0,.5f); sRT.anchorMax = new Vector2(1,.5f);
        sRT.offsetMin = new Vector2(126, -10); sRT.offsetMax = new Vector2(-(100+rightInset), 10);

        var bgGO = Child("Background", sGO.transform);
        SR(bgGO, V(0,.25f), V(1,.75f), V(.5f,.5f), V(0,0), V(0,0));
        bgGO.AddComponent<Image>().color = new Color(.2f,.2f,.2f);

        var faGO  = Child("Fill Area", sGO.transform);
        SR(faGO, V(0,.25f), V(1,.75f), V(.5f,.5f), V(0,0), V(-20,0));
        var fillGO = Child("Fill", faGO.transform);
        var fillRT = SR(fillGO, V(0,0), V(0,1), V(0,.5f), V(0,0), V(10,0));
        fillGO.AddComponent<Image>().color = new Color(.3f,.5f,.9f);

        var hsaGO = Child("Handle Slide Area", sGO.transform);
        SR(hsaGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(-20,0));
        var hdlGO  = Child("Handle", hsaGO.transform);
        var hdlRT  = SR(hdlGO, V(0,0), V(0,1), V(.5f,.5f), V(0,0), V(20,0));
        var hdlImg = hdlGO.AddComponent<Image>(); hdlImg.color = Color.white;

        var slider = sGO.AddComponent<Slider>();
        slider.fillRect = fillRT; slider.handleRect = hdlRT; slider.targetGraphic = hdlImg;
        slider.minValue = min; slider.maxValue = max; slider.value = def;
        slider.wholeNumbers = wholeNums; slider.direction = Slider.Direction.LeftToRight;

        return (slider, vTMP);
    }

    static TMP_Dropdown MakeDropdown(Transform parent)
    {
        var ddGO  = Child("Dropdown", parent);
        SR(ddGO, V(1,.5f), V(1,.5f), V(1,.5f), V(-12,0), V(200,36));
        var ddImg = ddGO.AddComponent<Image>(); ddImg.color = new Color(.12f,.12f,.12f);
        var dd    = ddGO.AddComponent<TMP_Dropdown>(); dd.targetGraphic = ddImg;

        var capGO = Child("Label", ddGO.transform);
        SR(capGO, V(0,0), V(1,1), V(.5f,.5f), V(-12,0), V(-24,-8));
        dd.captionText = T(capGO, "None", 15, Color.white, TextAlignmentOptions.MidlineLeft);

        var arwGO = Child("Arrow", ddGO.transform);
        SR(arwGO, V(1,.5f), V(1,.5f), V(1,.5f), V(-14,0), V(20,20));
        arwGO.AddComponent<Image>().color = new Color(.8f,.8f,.8f);

        var tplGO = Child("Template", ddGO.transform);
        SR(tplGO, V(0,0), V(1,0), V(.5f,1), V(0,2), V(0,150));
        tplGO.AddComponent<Image>().color = new Color(.1f,.1f,.1f);
        var tplSR = tplGO.AddComponent<ScrollRect>();
        tplSR.horizontal = false;
        tplGO.AddComponent<CanvasGroup>();
        dd.template = tplGO.GetComponent<RectTransform>();

        var tvpGO = Child("Viewport", tplGO.transform);
        SR(tvpGO, V(0,0), V(1,1), V(0,1), V(0,0), V(0,0));
        tvpGO.AddComponent<Image>().color = Color.clear;
        tvpGO.AddComponent<Mask>().showMaskGraphic = false;

        var tcGO = Child("Content", tvpGO.transform);
        var tcRT = SR(tcGO, V(0,1), V(1,1), V(.5f,1), V(0,0), V(0,28));

        var itemGO = Child("Item", tcGO.transform);
        SR(itemGO, V(0,1), V(1,1), V(.5f,1), V(0,0), V(0,28));
        var tog = itemGO.AddComponent<Toggle>();
        var ibGO  = Child("Item Background", itemGO.transform);
        SR(ibGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        var ibImg = ibGO.AddComponent<Image>(); ibImg.color = Color.clear;
        var ickGO = Child("Item Checkmark", itemGO.transform);
        SR(ickGO, V(0,.5f), V(0,.5f), V(.5f,.5f), V(10,0), V(16,16));
        ickGO.AddComponent<Image>().color = new Color(.3f,.5f,.9f);
        var ilGO  = Child("Item Label", itemGO.transform);
        SR(ilGO, V(0,0), V(1,1), V(.5f,.5f), V(5,0), V(-10,0));
        dd.itemText = T(ilGO, "Option A", 15, Color.white, TextAlignmentOptions.MidlineLeft);
        tog.graphic = ickGO.GetComponent<Image>(); tog.targetGraphic = ibImg;

        tplSR.content = tcRT;
        tplSR.viewport = tvpGO.GetComponent<RectTransform>();
        tplGO.SetActive(false);
        return dd;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Micro helpers
    // ═════════════════════════════════════════════════════════════════════════

    static void SetRef(SerializedObject so, string prop, Object value)
    {
        var p = so.FindProperty(prop);
        if (p == null) { Debug.LogWarning($"[SongSelectSceneBuilder] missing prop: {prop}"); return; }
        p.objectReferenceValue = value;
    }

    static void SetArr(SerializedObject so, string prop, Object[] values)
    {
        var p = so.FindProperty(prop);
        if (p == null) { Debug.LogWarning($"[SongSelectSceneBuilder] missing prop: {prop}"); return; }
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    static void Indent(GameObject go, float left)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.offsetMin = new Vector2(rt.offsetMin.x + left, rt.offsetMin.y);
    }

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

    static void LE(GameObject go, float minW, float minH, bool flexW = true)
    {
        var le = go.AddComponent<LayoutElement>();
        le.minWidth = minW; le.minHeight = minH;
        le.flexibleWidth = flexW ? 1 : 0;
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
