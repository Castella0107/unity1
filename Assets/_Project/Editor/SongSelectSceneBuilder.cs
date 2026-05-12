#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Editor-only helper that builds the SongSelect scene from scratch.</summary>
public static class SongSelectSceneBuilder
{
    const string ScenePath  = "Assets/_Project/Scenes/SongSelect.unity";
    const string PrefabPath = "Assets/_Project/Prefabs/UI/SongListItem.prefab";

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
        scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode    = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        var ct = canvasGO.transform;

        // Background (full screen)
        var bgGO = Child("Background", ct);
        SR(bgGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        bgGO.AddComponent<Image>().color = Hex("050810");

        // ── Header ────────────────────────────────────────────────────────────
        var headerGO = Child("Header", ct);
        SR(headerGO, V(0,1), V(1,1), V(.5f,1), V(0,0), V(0,80));

        var backBtnGO  = Child("BackButton", headerGO.transform);
        SR(backBtnGO, V(0,0), V(0,1), V(0,.5f), V(20,0), V(120,-16));
        var backBtnImg = backBtnGO.AddComponent<Image>(); backBtnImg.color = new Color(1,1,1,.1f);
        var backBtn    = backBtnGO.AddComponent<Button>(); backBtn.targetGraphic = backBtnImg;
        var backLbl    = Child("Label", backBtnGO.transform);
        SR(backLbl, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        T(backLbl, "< BACK", 18, Color.white, TextAlignmentOptions.Center);

        var hTitleGO = Child("Title", headerGO.transform);
        SR(hTitleGO, V(.5f,0), V(.5f,1), V(.5f,.5f), V(0,0), V(500,0));
        T(hTitleGO, "SONG SELECT", 28, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

        // ── MainArea (inset header 80 + footer 30) ────────────────────────────
        var mainGO = Child("MainArea", ct);
        SR(mainGO, V(0,0), V(1,1), V(.5f,.5f), V(0,-25), V(0,-110));

        // ── LeftPane ──────────────────────────────────────────────────────────
        var leftGO = Child("LeftPane", mainGO.transform);
        SR(leftGO, V(0,0), V(0,1), V(0,.5f), V(20,0), V(600,0));

        var svGO = Child("ScrollView", leftGO.transform);
        SR(svGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(-14,0));
        var scrollRect = svGO.AddComponent<ScrollRect>();
        scrollRect.horizontal = false; scrollRect.scrollSensitivity = 30f;

        var vpGO  = Child("Viewport", svGO.transform);
        SR(vpGO, V(0,0), V(1,1), V(0,0), V(0,0), V(0,0));
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

        // ── RightPane ─────────────────────────────────────────────────────────
        var rightGO = Child("RightPane", mainGO.transform);
        SR(rightGO, V(1,0), V(1,1), V(1,.5f), V(-20,0), V(1260,0));
        var rt = rightGO.transform;

        // JacketArea
        var jaGO = Child("JacketArea", rt);
        SR(jaGO, V(.5f,1), V(.5f,1), V(.5f,1), V(0,-10), V(460,460));
        var jiGO = Child("JacketImage", jaGO.transform);
        SR(jiGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        var jacketRaw = jiGO.AddComponent<RawImage>(); jacketRaw.color = new Color(.3f,.3f,.3f);
        var jfGO = Child("JacketFrame", jaGO.transform);
        SR(jfGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        jfGO.AddComponent<Image>().color = new Color(.6f,.6f,.6f,.3f);

        // SongInfo
        var siGO = Child("SongInfo", rt);
        SR(siGO, V(0,1), V(1,1), V(.5f,1), V(0,-480), V(-40,168));
        var siVLG = siGO.AddComponent<VerticalLayoutGroup>();
        siVLG.childControlHeight = false; siVLG.childForceExpandHeight = false;
        siVLG.spacing = 6; siVLG.padding = new RectOffset(20,20,0,0);

        var stGO  = Child("TitleText", siGO.transform); LE(stGO, 0,44);
        var stTMP = T(stGO, "---", 34, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        stTMP.overflowMode = TextOverflowModes.Ellipsis;
        var artGO  = Child("ArtistText", siGO.transform); LE(artGO, 0,28);
        var artTMP = T(artGO, "---", 20, new Color(.7f,.7f,.7f));
        artTMP.overflowMode = TextOverflowModes.Ellipsis;
        var bpmGO  = Child("BpmDurationText", siGO.transform); LE(bpmGO, 0,24);
        var bpmTMP = T(bpmGO, "BPM ---   Length 0:00", 16, new Color(.6f,.6f,.6f));

        var pbGO   = Child("PersonalBestRow", siGO.transform); LE(pbGO, 0,46);
        var pbHLG  = pbGO.AddComponent<HorizontalLayoutGroup>();
        pbHLG.childControlHeight = false; pbHLG.childForceExpandHeight = false;
        pbHLG.spacing = 10; pbHLG.childAlignment = TextAnchor.MiddleLeft;
        var pbLblGO = Child("BestLabel", pbGO.transform); LE(pbLblGO, 70,36, false);
        T(pbLblGO, "BEST", 15, new Color(.8f,.8f,.6f), TextAlignmentOptions.MidlineLeft);
        var pbScoGO = Child("BestScoreText", pbGO.transform); LE(pbScoGO, 220,36, false);
        var pbScoreTMP = T(pbScoGO, "---", 24, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        var pbRnkGO = Child("BestRankText", pbGO.transform); LE(pbRnkGO, 60,36, false);
        var pbRankTMP = T(pbRnkGO, "-", 30, new Color(1f,.85f,.2f), TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

        // DifficultyButtons
        var diffGO = Child("DifficultyButtons", rt);
        SR(diffGO, V(0,1), V(1,1), V(.5f,1), V(0,-664), V(-40,70));
        var dHLG = diffGO.AddComponent<HorizontalLayoutGroup>();
        dHLG.childControlWidth = true; dHLG.childForceExpandWidth = true;
        dHLG.childControlHeight = false; dHLG.childForceExpandHeight = false; dHLG.spacing = 8;

        string[] dlabels = { "EZ","NM","HD","EX" };
        Color[] dcolors = {
            new Color(.2f,.75f,.35f), new Color(.2f,.5f,.9f),
            new Color(.9f,.5f,.1f),   new Color(.85f,.1f,.5f)
        };
        var diffBtns      = new Button[4];
        var diffLvlTexts  = new TextMeshProUGUI[4];
        for (int i = 0; i < 4; i++)
        {
            var bGO  = Child("Btn" + dlabels[i], diffGO.transform); LE(bGO, 0,62, false);
            var bImg = bGO.AddComponent<Image>(); bImg.color = new Color(1,1,1,.15f);
            var btn  = bGO.AddComponent<Button>(); btn.targetGraphic = bImg;
            var bVLG = bGO.AddComponent<VerticalLayoutGroup>();
            bVLG.childControlHeight = false; bVLG.childForceExpandHeight = false;
            bVLG.childAlignment = TextAnchor.MiddleCenter; bVLG.spacing = 2;
            bVLG.padding = new RectOffset(4,4,8,8);
            var lGO = Child("Label", bGO.transform); LE(lGO, 0,24);
            T(lGO, dlabels[i], 18, dcolors[i], TextAlignmentOptions.Center, FontStyles.Bold);
            var nGO  = Child("Level", bGO.transform); LE(nGO, 0,24);
            var nTMP = T(nGO, "-", 20, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            diffBtns[i] = btn; diffLvlTexts[i] = nTMP;
        }

        // SettingsPanel
        var setGO = Child("SettingsPanel", rt);
        SR(setGO, V(0,1), V(1,1), V(.5f,1), V(0,-748), V(-40,248));
        setGO.AddComponent<Image>().color = new Color(0,0,0,.3f);
        var sVLG = setGO.AddComponent<VerticalLayoutGroup>();
        sVLG.childControlHeight = false; sVLG.childForceExpandHeight = false;
        sVLG.spacing = 8; sVLG.padding = new RectOffset(20,20,14,14);

        var (hiSpdSldr, hiSpdVal) = SliderRow(setGO.transform, "レーンスピード", .5f, 10f, 4.5f, false);
        var (jdgSldr, jdgVal)     = SliderRow(setGO.transform, "判定オフセット", -100f, 100f, 0f, true);
        var (visSldr, visVal)     = SliderRow(setGO.transform, "映像オフセット", -100f, 100f, 0f, true);

        var modRowGO = Child("ModifierRow", setGO.transform); LE(modRowGO, 0,46);
        var mHLG = modRowGO.AddComponent<HorizontalLayoutGroup>();
        mHLG.childControlHeight = false; mHLG.childForceExpandHeight = false;
        mHLG.spacing = 10; mHLG.childAlignment = TextAnchor.MiddleLeft;
        var mLblGO = Child("Label", modRowGO.transform); LE(mLblGO, 180,36, false);
        T(mLblGO, "Modifier", 15, new Color(.8f,.8f,.8f));
        var modDD = MakeDropdown(modRowGO.transform);

        // PlayButton
        var plBtnGO  = Child("PlayButton", rt);
        SR(plBtnGO, V(1,0), V(1,0), V(1,0), V(-20,20), V(240,120));
        var plBtnImg = plBtnGO.AddComponent<Image>(); plBtnImg.color = Hex("2c5aa0");
        var playBtn  = plBtnGO.AddComponent<Button>(); playBtn.targetGraphic = plBtnImg;
        var plLblGO  = Child("Label", plBtnGO.transform);
        SR(plLblGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        T(plLblGO, "▷ PLAY", 36, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

        // ── Footer ────────────────────────────────────────────────────────────
        var footerGO = Child("Footer", ct);
        SR(footerGO, V(0,0), V(1,0), V(.5f,0), V(0,0), V(0,30));
        footerGO.AddComponent<Image>().color = new Color(0,0,0,.5f);
        var khGO = Child("KeyHint", footerGO.transform);
        SR(khGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        T(khGO, "↑↓: 楽曲   ←→: 難易度   Enter: PLAY   Esc: 戻る", 13,
            new Color(.7f,.7f,.7f), TextAlignmentOptions.Center);

        // ── SongSelectController ──────────────────────────────────────────────
        var ctrlGO = new GameObject("SongSelectController");
        var ctrl   = ctrlGO.AddComponent<SongSelectController>();
        var so     = new SerializedObject(ctrl);

        so.FindProperty("_listContent").objectReferenceValue        = contentRT;
        so.FindProperty("_scrollRect").objectReferenceValue         = scrollRect;
        so.FindProperty("_jacketImage").objectReferenceValue        = jacketRaw;
        so.FindProperty("_titleText").objectReferenceValue          = stTMP;
        so.FindProperty("_artistText").objectReferenceValue         = artTMP;
        so.FindProperty("_bpmDurationText").objectReferenceValue    = bpmTMP;
        so.FindProperty("_bestScoreText").objectReferenceValue      = pbScoreTMP;
        so.FindProperty("_bestRankText").objectReferenceValue       = pbRankTMP;
        so.FindProperty("_btnEasy").objectReferenceValue            = diffBtns[0];
        so.FindProperty("_btnNormal").objectReferenceValue          = diffBtns[1];
        so.FindProperty("_btnHard").objectReferenceValue            = diffBtns[2];
        so.FindProperty("_btnExtra").objectReferenceValue           = diffBtns[3];
        var dlArr = so.FindProperty("_diffLevelTexts");
        dlArr.arraySize = 4;
        for (int i = 0; i < 4; i++)
            dlArr.GetArrayElementAtIndex(i).objectReferenceValue = diffLvlTexts[i];
        so.FindProperty("_hiSpeedSlider").objectReferenceValue      = hiSpdSldr;
        so.FindProperty("_hiSpeedValue").objectReferenceValue       = hiSpdVal;
        so.FindProperty("_judgeOffsetSlider").objectReferenceValue  = jdgSldr;
        so.FindProperty("_judgeOffsetValue").objectReferenceValue   = jdgVal;
        so.FindProperty("_visualOffsetSlider").objectReferenceValue = visSldr;
        so.FindProperty("_visualOffsetValue").objectReferenceValue  = visVal;
        so.FindProperty("_modifierDropdown").objectReferenceValue   = modDD;
        so.FindProperty("_playButton").objectReferenceValue         = playBtn;
        so.FindProperty("_backButton").objectReferenceValue         = backBtn;

        // Find InputActionAsset in _Project/Settings
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

        // ── SongListItem prefab ───────────────────────────────────────────────
        var prefabRoot  = BuildPrefabGO();
        var savedPrefab = PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
        Object.DestroyImmediate(prefabRoot);

        var so2 = new SerializedObject(ctrl);
        so2.FindProperty("_songItemPrefab").objectReferenceValue = savedPrefab;
        so2.ApplyModifiedProperties();

        // ── Save ──────────────────────────────────────────────────────────────
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();
        Debug.Log("[SongSelectSceneBuilder] Done → " + ScenePath);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Prefab
    // ─────────────────────────────────────────────────────────────────────────

    static GameObject BuildPrefabGO()
    {
        var root   = new GameObject("SongListItem");
        var rootRT = root.AddComponent<RectTransform>(); rootRT.sizeDelta = V(560, 70);
        var rootImg = root.AddComponent<Image>(); rootImg.color = new Color(1,1,1,.05f);
        var rootBtn = root.AddComponent<Button>(); rootBtn.targetGraphic = rootImg;

        var bgGO = Child("Background", root.transform);
        SR(bgGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        bgGO.AddComponent<Image>().color = new Color(1,1,1,.05f);

        var titleGO = Child("TitleText", root.transform);
        SR(titleGO, V(0,.5f), V(1,1), V(0,.5f), V(14,0), V(-90,0));
        var ttmp = T(titleGO, "Song Title", 20, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        ttmp.overflowMode = TextOverflowModes.Ellipsis;

        var artistGO = Child("ArtistText", root.transform);
        SR(artistGO, V(0,0), V(1,.5f), V(0,.5f), V(14,0), V(-90,0));
        T(artistGO, "Artist", 13, new Color(.65f,.65f,.65f), TextAlignmentOptions.MidlineLeft);

        var lvlGO = Child("LevelText", root.transform);
        SR(lvlGO, V(1,0), V(1,1), V(1,.5f), V(-8,0), V(80,0));
        T(lvlGO, "Lv.--", 18, new Color(.9f,.9f,.5f), TextAlignmentOptions.MidlineRight, FontStyles.Bold);

        return root;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UI helpers
    // ─────────────────────────────────────────────────────────────────────────

    static (Slider, TextMeshProUGUI) SliderRow(
        Transform parent, string label, float min, float max, float def, bool wholeNums)
    {
        var rowGO = Child(label + "Row", parent); LE(rowGO, 0,46);
        var rHLG  = rowGO.AddComponent<HorizontalLayoutGroup>();
        rHLG.childControlHeight = false; rHLG.childForceExpandHeight = false;
        rHLG.spacing = 10; rHLG.childAlignment = TextAnchor.MiddleLeft;

        var lGO = Child("Label", rowGO.transform); LE(lGO, 180,36, false);
        T(lGO, label, 14, new Color(.8f,.8f,.8f));

        var valStr = wholeNums ? $"{(int)def} ms" : def.ToString("F1");
        var vGO = Child("Value", rowGO.transform); LE(vGO, 82,36, false);
        var vTMP = T(vGO, valStr, 16, Color.white, TextAlignmentOptions.MidlineRight, FontStyles.Bold);

        var sGO = Child("Slider", rowGO.transform); LE(sGO, 0,36, true);
        SR(sGO, V(0,0), V(0,1), V(.5f,.5f), V(0,0), V(0,36));

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
        var ddGO  = Child("Dropdown", parent); LE(ddGO, 200,40, false);
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

    // ─────────────────────────────────────────────────────────────────────────
    // Micro helpers
    // ─────────────────────────────────────────────────────────────────────────

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

    static void LE(GameObject go, float minH, float minW, bool flexW = true)
    {
        var le = go.AddComponent<LayoutElement>();
        le.minWidth = minH; le.minHeight = minW;
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
        var idx    = path.LastIndexOf('/');
        AssetDatabase.CreateFolder(path[..idx], path[(idx + 1)..]);
    }
}
#endif
