#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Config.unity をモックレイアウト(ユーザー提供 SETTING 画面)のダーク版でフル再構築する Editor ヘルパー。
/// 構成: SETTING ヘッダ / 5タブバー(L・R Shift ヒント付き) / 左=項目行 / 右=説明カード / 下部バー(F9 リセット・ESC 閉じる)。
/// タブ: ゲームプレイ / キー設定 / グラフィック / オーディオ / アカウント設定 (旧7タブから統合)。
/// モーダル: CalibrationPanel(ゲームプレイ) / DevicesPanel(オーディオ) / ManageSongsPanel(アカウント)。
/// </summary>
public static class BuildConfigScene
{
    const string ScenePath              = "Assets/_Project/Scenes/Config.unity";
    const string TabPrefabPath          = "Assets/_Project/Prefabs/UI/ConfigTabButton.prefab";
    const string ProfileItemPrefabPath  = "Assets/_Project/Prefabs/UI/ProfileListItem.prefab";

    // ── ダークテーマ配色 (モックの紫アクセントをダーク変換) ──────────────────
    static readonly Color Bg        = Hex("050810");
    static readonly Color RowBg     = new Color(1f, 1f, 1f, 0.06f);
    static readonly Color CardBg    = new Color(1f, 1f, 1f, 0.09f);
    static readonly Color BoxBg     = Hex("161B2E");
    static readonly Color Accent    = Hex("6B52E0");   // 紫
    static readonly Color AccentHi  = Hex("8E78F0");
    static readonly Color Danger    = Hex("D8497A");
    static readonly Color Dim       = new Color(.72f, .72f, .78f);
    static readonly Color Faint     = new Color(1f, 1f, 1f, .12f);
    static readonly Color ChR       = Hex("E0584F");   // R チャンネルラベル
    static readonly Color ChG       = Hex("5FB85F");   // G
    static readonly Color ChB       = Hex("5A8EE0");   // B

    // コンテンツ領域
    const float ContentX = 80f,  ContentW = 1060f;
    const float ContentY = 230f, ContentH = 720f;
    const float RowH = 56f, RowStep = 64f;

    [MenuItem("Tools/Build Config Scene")]
    public static void Build()
    {
        EnsureFolder("Assets/_Project/Prefabs");
        EnsureFolder("Assets/_Project/Prefabs/UI");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── Camera / EventSystem ─────────────────────────────────────────────
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.transform.position = new Vector3(0, 1, -10);
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Bg;
        camGO.AddComponent<AudioListener>();
        camGO.AddComponent<AudioListenerGuard>();   // _Persistent と重複時に無効化 (警告スパム防止)

        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        esGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        esGO.AddComponent<EventSystemGuard>();

        // ── Canvas ───────────────────────────────────────────────────────────
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

        var bgGO = Child("Background", ct);
        SR(bgGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        bgGO.AddComponent<Image>().color = Bg;

        // ── SETTING ヘッダ (左上・紫アクセントバー) ──────────────────────────
        var headGO = Child("HeaderBar", ct);
        SR(headGO, V(0,1), V(0,1), V(0,1), V(0,-46), V(600,86));
        var headImg = headGO.AddComponent<Image>(); headImg.color = Accent; headImg.raycastTarget = false;
        var headLbl = Child("Label", headGO.transform);
        SR(headLbl, V(0,0), V(1,1), V(.5f,.5f), V(30,0), V(-60,0));
        T(headLbl, "SETTING", 52, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold | FontStyles.Italic);

        // ── タブバー (中央上・L/R Shift ヒント) ──────────────────────────────
        var tabRowGO = Child("TabBarRow", ct);
        SR(tabRowGO, V(.5f,1), V(.5f,1), V(.5f,1), V(170,-150), V(1180,52));

        MakeShiftHint(tabRowGO.transform, "L Shift", "◀", left: true);
        MakeShiftHint(tabRowGO.transform, "R Shift", "▶", left: false);

        var tabContentGO = Child("TabBarContent", tabRowGO.transform);
        var tabContentRT = SR(tabContentGO, V(.5f,0), V(.5f,1), V(.5f,.5f), V(0,0), V(980,0));
        var tHLG = tabContentGO.AddComponent<HorizontalLayoutGroup>();
        tHLG.childControlWidth = false; tHLG.childForceExpandWidth = false;
        tHLG.childControlHeight = true; tHLG.childForceExpandHeight = true;
        tHLG.spacing = 12; tHLG.childAlignment = TextAnchor.MiddleCenter;

        // ── 右側 説明カード ───────────────────────────────────────────────────
        var cardGO = Child("DescriptionCard", ct);
        // 上=固定 / 下=画面下基準のストレッチ (低アスペクト時に下部バーと重ならないように)
        SR(cardGO, V(1,0), V(1,1), V(1,1), V(-80,-ContentY), V(560,-(ContentY+120)));
        var cardImg = cardGO.AddComponent<Image>(); cardImg.color = CardBg; cardImg.raycastTarget = false;

        var descTitleGO  = Child("DescTitle", cardGO.transform);
        SR(descTitleGO, V(0,1), V(1,1), V(.5f,1), V(0,-26), V(-60,40));
        var descTitleTMP = T(descTitleGO, "ゲームプレイ", 30, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

        var descBodyGO  = Child("DescBody", cardGO.transform);
        SR(descBodyGO, V(0,1), V(1,1), V(.5f,1), V(0,-86), V(-60,400));
        var descBodyTMP = T(descBodyGO, "", 19, Dim, TextAlignmentOptions.TopLeft);

        // ── 下部バー (F9 リセット / ESC 閉じる) ──────────────────────────────
        var (resetBtn, _) = MakeChipButton(ct, "F9", "リセット", Danger,
            anchorLeft: true,  pos: V(80, 38),  size: V(190, 54));
        var (backBtn, _)  = MakeChipButton(ct, "ESC", "閉じる", Faint,
            anchorLeft: false, pos: V(-80, 38), size: V(210, 54));
        var (saveBtn, _)  = MakeChipButton(ct, "F5", "保存", Accent,
            anchorLeft: false, pos: V(-420, 38), size: V(190, 54));

        // ── 6 パネル ─────────────────────────────────────────────────────────
        var gameplayPanel = MakePanel(ct, "GameplayPanel");
        var keysPanel     = MakePanel(ct, "KeysPanel");
        var graphicsPanel = MakePanel(ct, "GraphicsPanel");
        var colorsPanel   = MakePanel(ct, "ColorsPanel");
        var audioPanel    = MakePanel(ct, "AudioPanel");
        var accountPanel  = MakePanel(ct, "AccountPanel");

        // ── モーダル (Canvas 直下・最前面・初期非表示) ────────────────────────
        var calibrationPanel = BuildCalibrationModal(ct);
        var devicesPanelRoot = BuildDevicesModal(ct);
        var manageSongsPanel = BuildManageSongsModal(ct);

        // ── InputActions アセット ────────────────────────────────────────────
        UnityEngine.InputSystem.InputActionAsset inputAsset = null;
        foreach (var guid in AssetDatabase.FindAssets("InputActions t:InputActionAsset"))
        {
            var iaPath = AssetDatabase.GUIDToAssetPath(guid);
            if (iaPath.Contains("_Project"))
            {
                inputAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(iaPath);
                break;
            }
        }

        // ── タブ内容 ─────────────────────────────────────────────────────────
        BuildGameplayTab(gameplayPanel, calibrationPanel);
        BuildKeysTab(keysPanel, inputAsset);
        BuildGraphicsTab(graphicsPanel);
        BuildColorsTab(colorsPanel);
        BuildAudioTab(audioPanel, devicesPanelRoot);
        BuildAccountTab(accountPanel, manageSongsPanel);

        // ── タブボタンプレハブ ────────────────────────────────────────────────
        var tabPrefab = BuildTabButtonPrefab();

        // ── ConfigController 配線 ────────────────────────────────────────────
        var ctrlGO = new GameObject("ConfigController");
        var ctrl   = ctrlGO.AddComponent<ConfigController>();
        var so     = new SerializedObject(ctrl);
        SetRef(so, "_tabBarContent",   tabContentRT);
        SetRef(so, "_tabButtonPrefab", tabPrefab);
        SetRef(so, "_gameplayPanel",   gameplayPanel);
        SetRef(so, "_keysPanel",       keysPanel);
        SetRef(so, "_graphicsPanel",   graphicsPanel);
        SetRef(so, "_colorsPanel",     colorsPanel);
        SetRef(so, "_audioPanel",      audioPanel);
        SetRef(so, "_accountPanel",    accountPanel);
        SetRef(so, "_descTitleText",   descTitleTMP);
        SetRef(so, "_descBodyText",    descBodyTMP);
        SetRef(so, "_backButton",      backBtn);
        SetRef(so, "_resetButton",     resetBtn);
        SetRef(so, "_saveButton",      saveBtn);
        SetRef(so, "_inputAsset",      inputAsset);
        so.ApplyModifiedProperties();

        // 初期表示: ゲームプレイ以外は隠す (ConfigController.SwitchTab でも制御される)
        keysPanel.SetActive(false);
        graphicsPanel.SetActive(false);
        colorsPanel.SetActive(false);
        audioPanel.SetActive(false);
        accountPanel.SetActive(false);

        EditorSceneManager.SaveScene(scene, ScenePath);
        RegisterInBuildSettings(ScenePath);
        AssetDatabase.Refresh();
        Debug.Log("[BuildConfigScene] Done → " + ScenePath);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // タブ別ビルド
    // ═════════════════════════════════════════════════════════════════════════

    static void BuildGameplayTab(GameObject panel, CalibrationPanel calibration)
    {
        float y = -8f;
        var pt = panel.transform;

        var (hiSlider, hiVal)   = SliderRow(pt, ref y, "ハイスピード",        0.5f, 20f, 4.5f, false, "4.5");
        var (judSlider, judVal) = SliderRow(pt, ref y, "判定タイミング補正",  AppOffsetSettings.MinMs, AppOffsetSettings.MaxMs, 0, true, "0 ms");
        var (visSlider, visVal) = SliderRow(pt, ref y, "表示タイミング補正",  AppOffsetSettings.MinMs, AppOffsetSettings.MaxMs, 0, true, "0 ms");
        var calBtn              = ButtonRow(pt, ref y, "キャリブレーション", "開始");
        var comboDD             = DropdownRow(pt, ref y, "コンボ継続境界");
        var flToggle            = ToggleRow(pt, ref y, "FAST/SLOW表示");
        var (bgSlider, bgVal)   = SliderRow(pt, ref y, "背景エフェクト強度", 0, 100, 100, true, "100%");
        var fxDD                = DropdownRow(pt, ref y, "判定エフェクト");

        var tab = panel.AddComponent<GameplayTabController>();
        var so  = new SerializedObject(tab);
        SetRef(so, "_hiSpeedSlider",           hiSlider);
        SetRef(so, "_hiSpeedValue",            hiVal);
        SetRef(so, "_judgmentOffsetSlider",    judSlider);
        SetRef(so, "_judgmentOffsetValue",     judVal);
        SetRef(so, "_visualOffsetSlider",      visSlider);
        SetRef(so, "_visualOffsetValue",       visVal);
        SetRef(so, "_calibrateButton",         calBtn);
        SetRef(so, "_calibrationPanel",        calibration);
        SetRef(so, "_comboBorderDropdown",     comboDD);
        SetRef(so, "_fastLateToggle",          flToggle);
        SetRef(so, "_backgroundEffectsSlider", bgSlider);
        SetRef(so, "_backgroundEffectsValue",  bgVal);
        SetRef(so, "_judgmentEffectDropdown",  fxDD);
        so.ApplyModifiedProperties();
    }

    static void BuildKeysTab(GameObject panel, UnityEngine.InputSystem.InputActionAsset inputAsset)
    {
        float y = -8f;
        var pt = panel.transform;

        // ── レーンプレビュー (モックのレーン+キー表示) ────────────────────────
        const float LaneBlockH = 250f;
        var blockGO = Child("LaneBlock", pt);
        SR(blockGO, V(0,1), V(1,1), V(.5f,1), V(0,y), V(0,LaneBlockH));
        var blockImg = blockGO.AddComponent<Image>(); blockImg.color = new Color(0,0,0,.55f); blockImg.raycastTarget = false;
        y -= LaneBlockH + 12f;

        // ゲーム内レーン配置に合わせて FX L を左端に(InputTabController.LaneActionNames と同順)。
        string[] laneNames = { "FX L", "LINE 1", "LINE 2", "LINE 3", "LINE 4", "FX R" };
        var keyDisplays = new TextMeshProUGUI[6];
        var changeBtns  = new Button[6];
        var highlights  = new Image[6];

        for (int i = 0; i < 6; i++)
        {
            float cx = 130f + i * 160f;   // 6列センター

            var lnGO = Child("LaneLabel" + i, blockGO.transform);
            SR(lnGO, V(0,1), V(0,1), V(.5f,1), V(cx,-26), V(120,26));
            T(lnGO, laneNames[i], 16, Dim, TextAlignmentOptions.Center, FontStyles.Bold);

            var keyGO  = Child("KeyButton" + i, blockGO.transform);
            SR(keyGO, V(0,1), V(0,1), V(.5f,1), V(cx,-62), V(76,76));
            var keyImg = keyGO.AddComponent<Image>(); keyImg.color = Faint;
            var keyBtn = keyGO.AddComponent<Button>(); keyBtn.targetGraphic = keyImg;
            var keyLbl = Child("KeyText", keyGO.transform);
            SR(keyLbl, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
            keyDisplays[i] = T(keyLbl, "?", 26, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            changeBtns[i]  = keyBtn;

            var hlGO = Child("Highlight" + i, blockGO.transform);
            SR(hlGO, V(0,1), V(0,1), V(.5f,1), V(cx,-152), V(76,14));
            var hlImg = hlGO.AddComponent<Image>();
            hlImg.color = new Color(1f, 1f, 1f, 0.25f); hlImg.raycastTarget = false;
            highlights[i] = hlImg;
        }

        var hintGO = Child("RebindHint", blockGO.transform);
        SR(hintGO, V(0,0), V(1,0), V(.5f,0), V(0,16), V(-40,30));
        T(hintGO, "キー変更ボタンをクリックしてから、希望のキーを押してください。", 17, Dim, TextAlignmentOptions.Center);

        var defaultsBtn = ButtonRow(pt, ref y, "キー割り当て", "デフォルトに戻す");
        var padToggle   = ToggleRow(pt, ref y, "ゲームパッドを使用");
        var ninToggle   = ToggleRow(pt, ref y, "パッド決定/戻る配置: 任天堂配置 (右=決定)");

        var tab = panel.AddComponent<InputTabController>();
        var so  = new SerializedObject(tab);
        SetArr(so, "_keyDisplays",        keyDisplays);
        SetArr(so, "_changeButtons",      changeBtns);
        SetRef(so, "_defaultsButton",     defaultsBtn);
        SetRef(so, "_controllerEnabledToggle", padToggle);
        SetRef(so, "_nintendoLayoutToggle",    ninToggle);
        SetArr(so, "_testKeyHighlights",  highlights);
        SetRef(so, "_inputAsset",         inputAsset);
        so.ApplyModifiedProperties();
    }

    static void BuildGraphicsTab(GameObject panel)
    {
        float y = -8f;
        var pt = panel.transform;

        var modeDD   = DropdownRow(pt, ref y, "画面モード");
        var resDD    = DropdownRow(pt, ref y, "画面解像度");
        var vsyncTgl = ToggleRow(pt, ref y, "垂直同期");
        var fpsDD    = DropdownRow(pt, ref y, "ゲームフレームレート制限");
        var camDD    = DropdownRow(pt, ref y, "カメラアングル");
        var bloomDD  = DropdownRow(pt, ref y, "ブルーム");
        var motTgl   = ToggleRow(pt, ref y, "モーションエフェクト");
        var fpsTgl   = ToggleRow(pt, ref y, "FPS表示");

        var tab = panel.AddComponent<DisplayTabController>();
        var so  = new SerializedObject(tab);
        SetRef(so, "_resolutionDropdown",  resDD);
        SetRef(so, "_screenModeDropdown",  modeDD);
        SetRef(so, "_fpsLimitDropdown",    fpsDD);
        SetRef(so, "_vsyncToggle",         vsyncTgl);
        SetRef(so, "_cameraAngleDropdown", camDD);
        SetRef(so, "_bloomLevelDropdown",  bloomDD);
        SetRef(so, "_motionEffectsToggle", motTgl);
        SetRef(so, "_showFpsToggle",       fpsTgl);
        so.ApplyModifiedProperties();
    }

    // ── 色タブ: ノーツ(レーン別6) / 仕切り線 / 判定線 を RGB(0-255)スライダーで設定 ──
    //   選択中の行(行頭の色見本クリックで選択)へ、プリセットパレット適用 / 行間コピー・貼り付けができる。
    static readonly string[] PaletteHex =
    {
        "FFFFFF","B8B8B8","707070","303030","FF4D4D","FF7A33","FFA640","FFD740","FFF066","C2F24D","7ED957","45C66B",
        "2EA86B","33E0C8","33C6E0","33A0FF","4C8CFF","5A6BE0","6B52E0","9B5CFF","C24DFF","FF5AD0","FF6FA0","FF8FB0",
    };

    static void BuildColorsTab(GameObject panel)
    {
        float y = -8f;
        var pt = panel.transform;

        var swatch = new Image[8]; var selBtn = new Button[8]; var rowBg = new Image[8];
        var rS = new Slider[8]; var gS = new Slider[8]; var bS = new Slider[8];
        var rV = new TextMeshProUGUI[8]; var gV = new TextMeshProUGUI[8]; var bV = new TextMeshProUGUI[8];
        int idx = 0;

        void Add(string label, Color c)
        {
            var (sw, sb, rb, r, g, b, rv, gv, bv) = ColorRow(pt, ref y, label, c);
            swatch[idx] = sw; selBtn[idx] = sb; rowBg[idx] = rb;
            rS[idx] = r; gS[idx] = g; bS[idx] = b; rV[idx] = rv; gV[idx] = gv; bV[idx] = bv;
            idx++;
        }

        ColorSection(pt, ref y, "ノーツ (レーン別)");
        Add("鍵1 (左)", GameColorSettings.NoteColor(0));
        Add("鍵2",      GameColorSettings.NoteColor(1));
        Add("鍵3",      GameColorSettings.NoteColor(2));
        Add("鍵4 (右)", GameColorSettings.NoteColor(3));
        Add("FX 左",    GameColorSettings.NoteColor(4));
        Add("FX 右",    GameColorSettings.NoteColor(5));
        ColorSection(pt, ref y, "レーン / 判定線");
        Add("レーン仕切り線", GameColorSettings.DividerColor);
        Add("判定線",         GameColorSettings.JudgmentLineColor);

        // ── ツールバー (選択中の行に適用) ──────────────────────────────────────
        var hdrGO = Child("ToolHeader", pt);
        SR(hdrGO, V(0,1), V(1,1), V(.5f,1), V(0,y), V(0,24));
        var hdrL = Child("Label", hdrGO.transform);
        SR(hdrL, V(0,0), V(1,1), V(0,.5f), V(24,0), V(-24,0));
        T(hdrL, "── ツール (選択中の行に適用) ──", 16, Hex("F7C740"), TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        y -= 30f;

        float infoY = y;
        var selGO = Child("SelLabel", pt);
        SR(selGO, V(0,1), V(0,1), V(0,1), V(24, infoY), V(90,40));
        T(selGO, "選択中", 16, Dim, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

        var actSwGO = Child("ActiveSwatch", pt);
        SR(actSwGO, V(0,1), V(0,1), V(0,1), V(116, infoY-1), V(40,40));
        var actSw = actSwGO.AddComponent<Image>(); actSw.raycastTarget = false;

        var actNameGO = Child("ActiveName", pt);
        SR(actNameGO, V(0,1), V(0,1), V(0,1), V(168, infoY), V(230,40));
        var actName = T(actNameGO, "-", 18, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

        var copyBtn  = SmallButton(pt, "コピー",   V(0,1), V(408, infoY), V(150,40), anchorLeftTop: true);
        var pasteBtn = SmallButton(pt, "貼り付け", V(0,1), V(570, infoY), V(150,40), anchorLeftTop: true);
        y -= 50f;

        var palLblGO = Child("PaletteLabel", pt);
        SR(palLblGO, V(0,1), V(0,1), V(0,1), V(24, y), V(360,22));
        T(palLblGO, "プリセットパレット (クリックで選択中の行に適用)", 14, Dim, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        y -= 26f;

        var palBtns = new Button[PaletteHex.Length];
        var palSw   = new Image[PaletteHex.Length];
        const int cols = 12; const float cell = 38f, gap = 6f;
        for (int i = 0; i < PaletteHex.Length; i++)
        {
            int col = i % cols, rowi = i / cols;
            var cGO = Child("Pal" + i, pt);
            SR(cGO, V(0,1), V(0,1), V(0,1), V(24 + col * (cell + gap), y - rowi * (cell + gap)), V(cell, cell));
            var img = cGO.AddComponent<Image>(); img.color = Hex(PaletteHex[i]);
            var btn = cGO.AddComponent<Button>(); btn.targetGraphic = img; btn.transition = Selectable.Transition.None;
            palSw[i] = img; palBtns[i] = btn;
        }

        // ── 2D カラーピッカー (右側・Windows「色の編集」風) ──────────────────────
        var picker = BuildColorPicker(pt);

        string[] rowNames = { "鍵1 (左)", "鍵2", "鍵3", "鍵4 (右)", "FX 左", "FX 右", "レーン仕切り線", "判定線" };

        var tab = panel.AddComponent<ColorsTabController>();
        var so  = new SerializedObject(tab);
        SetArr(so, "_swatches",         swatch);
        SetArr(so, "_rowSelectButtons", selBtn);
        SetArr(so, "_rowBackgrounds",   rowBg);
        SetArr(so, "_rSliders",         rS);
        SetArr(so, "_gSliders",         gS);
        SetArr(so, "_bSliders",         bS);
        SetArr(so, "_rValues",          rV);
        SetArr(so, "_gValues",          gV);
        SetArr(so, "_bValues",          bV);
        SetArr(so, "_paletteButtons",   palBtns);
        SetArr(so, "_paletteSwatches",  palSw);
        SetRef(so, "_activeSwatch",     actSw);
        SetRef(so, "_activeNameLabel",  actName);
        SetRef(so, "_copyButton",       copyBtn);
        SetRef(so, "_pasteButton",      pasteBtn);
        SetRef(so, "_picker",           picker);
        var rn = so.FindProperty("_rowNames");
        if (rn != null)
        {
            rn.arraySize = rowNames.Length;
            for (int i = 0; i < rowNames.Length; i++)
                rn.GetArrayElementAtIndex(i).stringValue = rowNames[i];
        }
        so.ApplyModifiedProperties();
    }

    // 右側に SV 四角(横=彩度 / 縦=明度) + 縦の色相バーを置く。テクスチャは実行時生成(ColorSquarePicker)。
    static ColorSquarePicker BuildColorPicker(Transform pt)
    {
        const float pX = 780f, sq = 220f, pTop = -34f, hueGap = 12f, hueW = 22f;

        var lbl = Child("PickerLabel", pt);
        SR(lbl, V(0,1), V(0,1), V(0,1), V(pX, -8f), V(280,22));
        T(lbl, "カラーピッカー (ドラッグで調整)", 14, Dim, TextAlignmentOptions.MidlineLeft, FontStyles.Bold)
            .raycastTarget = false;

        var root = Child("ColorPicker", pt);
        SR(root, V(0,1), V(0,1), V(0,1), V(pX, pTop), V(sq + hueGap + hueW + 8f, sq + 8f));
        var picker = root.AddComponent<ColorSquarePicker>();

        // SV 四角 (pivot(0,0) = 左下基準で座標計算を単純化)
        var svGO = Child("SVArea", root.transform);
        var svRT = SR(svGO, V(0,1), V(0,1), V(0,0), V(0,-sq), V(sq,sq));

        Image Layer(string n)
        {
            var g = Child(n, svGO.transform);
            SR(g, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));   // 全面ストレッチ・sizeDelta 0
            var im = g.AddComponent<Image>(); im.raycastTarget = false; im.type = Image.Type.Simple;
            return im;
        }
        var svBase  = Layer("Base");  svBase.color = Color.red;   // 実行時に純色相へ
        var svWhite = Layer("White");
        var svBlack = Layer("Black");

        var svCurGO = Child("Cursor", svGO.transform);
        var svCurRT = SR(svCurGO, V(0,0), V(0,0), V(.5f,.5f), V(sq,sq), V(14,14));
        var svCur   = svCurGO.AddComponent<Image>(); svCur.color = Color.white; svCur.raycastTarget = false;
        var svCurOl = svCurGO.AddComponent<Outline>(); svCurOl.effectColor = Color.black; svCurOl.effectDistance = V(2,2);

        var svCatch = Child("Catcher", svGO.transform);     // 透明・最前面で入力を拾う
        SR(svCatch, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        var svCatchImg = svCatch.AddComponent<Image>(); svCatchImg.color = new Color(0,0,0,0); svCatchImg.raycastTarget = true;
        var svArea = svCatch.AddComponent<ColorPickerArea>();

        // 色相バー (縦)
        float hueX = sq + hueGap;
        var hueGO = Child("HueBar", root.transform);
        var hueRT = SR(hueGO, V(0,1), V(0,1), V(0,0), V(hueX,-sq), V(hueW,sq));
        var hueImg = hueGO.AddComponent<Image>(); hueImg.raycastTarget = true; hueImg.type = Image.Type.Simple;
        var hueArea = hueGO.AddComponent<ColorPickerArea>();

        var hueCurGO = Child("Cursor", hueGO.transform);
        var hueCurRT = SR(hueCurGO, V(0,0), V(0,0), V(.5f,.5f), V(hueW/2f, sq), V(hueW + 6f, 6f));
        var hueCur   = hueCurGO.AddComponent<Image>(); hueCur.color = Color.white; hueCur.raycastTarget = false;
        var hueCurOl = hueCurGO.AddComponent<Outline>(); hueCurOl.effectColor = Color.black; hueCurOl.effectDistance = V(2,2);

        // 配線
        var pso = new SerializedObject(picker);
        SetRef(pso, "_svRect",    svRT);
        SetRef(pso, "_svBase",    svBase);
        SetRef(pso, "_svWhite",   svWhite);
        SetRef(pso, "_svBlack",   svBlack);
        SetRef(pso, "_svCursor",  svCurRT);
        SetRef(pso, "_hueRect",   hueRT);
        SetRef(pso, "_hueImage",  hueImg);
        SetRef(pso, "_hueCursor", hueCurRT);
        pso.ApplyModifiedProperties();

        WirePickerArea(svArea,  picker, isHue: false);
        WirePickerArea(hueArea, picker, isHue: true);
        return picker;
    }

    static void WirePickerArea(ColorPickerArea area, ColorSquarePicker owner, bool isHue)
    {
        var so = new SerializedObject(area);
        SetRef(so, "_owner", owner);
        var p = so.FindProperty("_isHue");
        if (p != null) p.boolValue = isHue;
        so.ApplyModifiedProperties();
    }

    static (Image, Button, Image, Slider, Slider, Slider, TextMeshProUGUI, TextMeshProUGUI, TextMeshProUGUI)
        ColorRow(Transform parent, ref float y, string label, Color init)
    {
        // 行は左カラム(幅 760)に収める。右側は 2D カラーピッカー用に空ける。
        var row = Child("CRow_" + label, parent);
        SR(row, V(0,1), V(0,1), V(0,1), V(0,y), V(760,52));
        var rowImg = row.AddComponent<Image>(); rowImg.color = RowBg; rowImg.raycastTarget = false;

        // 行頭の色見本はクリックで「この行を選択」するボタン。色をそのまま見せるため transition=None。
        var swGO = Child("Swatch", row.transform);
        SR(swGO, V(0,.5f), V(0,.5f), V(0,.5f), V(26,0), V(36,36));
        var sw = swGO.AddComponent<Image>();
        sw.color = new Color(init.r, init.g, init.b, 1f);
        var selBtn = swGO.AddComponent<Button>(); selBtn.targetGraphic = sw; selBtn.transition = Selectable.Transition.None;

        var lblGO = Child("Label", row.transform);
        SR(lblGO, V(0,.5f), V(0,.5f), V(0,.5f), V(70,0), V(146,40));
        T(lblGO, label, 17, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

        var (r, rv) = ColorChannel(row.transform, "R", ChR, 220f, init.r);
        var (g, gv) = ColorChannel(row.transform, "G", ChG, 400f, init.g);
        var (b, bv) = ColorChannel(row.transform, "B", ChB, 580f, init.b);

        y -= 56f;
        return (sw, selBtn, rowImg, r, g, b, rv, gv, bv);
    }

    static (Slider, TextMeshProUGUI) ColorChannel(
        Transform row, string letter, Color letterColor, float x, float v01)
    {
        var lGO = Child("Ch" + letter, row);
        SR(lGO, V(0,.5f), V(0,.5f), V(0,.5f), V(x,0), V(20,40));
        T(lGO, letter, 17, letterColor, TextAlignmentOptions.Center, FontStyles.Bold);

        var slider = MakeSliderAt(row, x + 24f, x + 126f, 0, 255, Mathf.Round(v01 * 255f), true);

        var boxGO = Child("Val" + letter, row);
        SR(boxGO, V(0,.5f), V(0,.5f), V(0,.5f), V(x + 132f, 0), V(44,32));
        var boxImg = boxGO.AddComponent<Image>(); boxImg.color = BoxBg; boxImg.raycastTarget = false;
        var vGO = Child("V", boxGO.transform);
        SR(vGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        var vTMP = T(vGO, Mathf.RoundToInt(v01 * 255f).ToString(), 15, Color.white,
            TextAlignmentOptions.Center, FontStyles.Bold);
        vTMP.raycastTarget = false;
        return (slider, vTMP);
    }

    static void ColorSection(Transform parent, ref float y, string title)
    {
        var secGO = Child("CSection_" + title, parent);
        SR(secGO, V(0,1), V(1,1), V(.5f,1), V(0,y), V(0,26));
        var lbl = Child("Label", secGO.transform);
        SR(lbl, V(0,0), V(1,1), V(0,.5f), V(24,0), V(-24,0));
        var lblT = T(lbl, "── " + title + " ──", 16, Hex("F7C740"), TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        lblT.raycastTarget = false;   // 右側ピッカー上のクリックを奪わない
        y -= 34f;
    }

    static void BuildAudioTab(GameObject panel, GameObject devicesPanelRoot)
    {
        float y = -8f;
        var pt = panel.transform;

        // デバイスプロファイル行: 現在名 + 管理ボタン
        var row = Row(pt, ref y, "オーディオデバイスプロファイル");
        var profGO  = Child("ProfileName", row.transform);
        SR(profGO, V(0,0), V(0,1), V(0,.5f), V(430,0), V(330,0));
        var profTMP = T(profGO, "-", 19, AccentHi, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        var manageBtn = SmallButton(row.transform, "管理...", V(1,.5f), V(-20,0), V(220,40));

        var muteTgl             = ToggleRow(pt, ref y, "ウィンドウ切替ミュート");
        var (masSlider, masVal) = SliderRow(pt, ref y, "全体音量",   0, 100, 80, true, "80%");
        var (musSlider, musVal) = SliderRow(pt, ref y, "楽曲音量",   0, 100, 90, true, "90%");
        var (sfxSlider, sfxVal) = SliderRow(pt, ref y, "効果音音量", 0, 100, 70, true, "70%");

        var tab = panel.AddComponent<AudioTabController>();
        var so  = new SerializedObject(tab);
        SetRef(so, "_activeProfileNameLabel", profTMP);
        SetRef(so, "_manageDevicesButton",    manageBtn);
        SetRef(so, "_devicesPanelRoot",       devicesPanelRoot);
        SetRef(so, "_muteOnFocusLossToggle",  muteTgl);
        SetRef(so, "_masterVolumeSlider",     masSlider);
        SetRef(so, "_masterVolumeValue",      masVal);
        SetRef(so, "_musicVolumeSlider",      musSlider);
        SetRef(so, "_musicVolumeValue",       musVal);
        SetRef(so, "_sfxVolumeSlider",        sfxSlider);
        SetRef(so, "_sfxVolumeValue",         sfxVal);
        so.ApplyModifiedProperties();
    }

    static void BuildAccountTab(GameObject panel, ManageSongsPanel manageSongs)
    {
        float y = -8f;
        var pt = panel.transform;

        // ログインセッション (Go サーバー認証, M6)
        var sessRow = Row(pt, ref y, "ログイン中のアカウント");
        var sessGO  = Child("SessionInfo", sessRow.transform);
        SR(sessGO, V(0,0), V(0,1), V(0,.5f), V(430,0), V(380,0));
        var sessTMP = T(sessGO, "-", 17, AccentHi, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        var logoutBtn = SmallButton(sessRow.transform, "ログアウト", V(1,.5f), V(-20,0), V(180,40), danger: true);

        // プレイヤー名
        var nameRow   = Row(pt, ref y, "プレイヤー名");
        var nameInput = MakeInput(nameRow.transform, 430, 380, "表示名を入力");
        var nameSave  = SmallButton(nameRow.transform, "SAVE", V(1,.5f), V(-20,0), V(110,40));

        // ステータスメッセージ
        var msgRow   = Row(pt, ref y, "ステータスメッセージ");
        var msgInput = MakeInput(msgRow.transform, 430, 380, "メッセージを入力");
        var msgSave  = SmallButton(msgRow.transform, "SAVE", V(1,.5f), V(-20,0), V(110,40));
        var cntGO    = Child("CharCount", msgRow.transform);
        SR(cntGO, V(1,.5f), V(1,.5f), V(1,.5f), V(-140,0), V(90,0));
        var cntTMP   = T(cntGO, "0 / 200", 14, Dim, TextAlignmentOptions.MidlineRight);

        var notifTgl = ToggleRow(pt, ref y, "通知");

        // ── データ管理 セクション ────────────────────────────────────────────
        var secGO = Child("DataSectionHeader", pt);
        SR(secGO, V(0,1), V(1,1), V(.5f,1), V(0,y), V(0,30));
        T(secGO, "── データ管理 ──", 17, Hex("F7C740"), TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        y -= 38f;

        // ストレージ情報 (3行 + 更新)
        const float StoreH = 96f;
        var storeRow = Child("StorageRow", pt);
        SR(storeRow, V(0,1), V(1,1), V(.5f,1), V(0,y), V(0,StoreH));
        storeRow.AddComponent<Image>().color = RowBg;
        var dbGO  = Child("DbSize", storeRow.transform);
        SR(dbGO, V(0,1), V(1,1), V(0,1), V(24,-8), V(-260,26));
        var dbTMP = T(dbGO, "Database Size: -", 16, Color.white, TextAlignmentOptions.MidlineLeft);
        var sgGO  = Child("SongsSize", storeRow.transform);
        SR(sgGO, V(0,1), V(1,1), V(0,1), V(24,-38), V(-260,26));
        var sgTMP = T(sgGO, "Songs Library: -", 16, Color.white, TextAlignmentOptions.MidlineLeft);
        var rpGO  = Child("ReplaysSize", storeRow.transform);
        SR(rpGO, V(0,1), V(1,1), V(0,1), V(24,-68), V(-260,26));
        var rpTMP = T(rpGO, "Replays: -", 16, Color.white, TextAlignmentOptions.MidlineLeft);
        var refreshBtn = SmallButton(storeRow.transform, "更新", V(1,.5f), V(-20,0), V(110,40));
        y -= StoreH + 8f;

        // バックアップ: エクスポート / インポート
        var bakRow = Row(pt, ref y, "バックアップ");
        var exportBtn = SmallButton(bakRow.transform, "エクスポート", V(1,.5f), V(-250,0), V(220,40));
        var importBtn = SmallButton(bakRow.transform, "インポート",   V(1,.5f), V(-20,0),  V(220,40));

        var songsBtn = ButtonRow(pt, ref y, "楽曲ライブラリ", "楽曲管理...");

        // Danger Zone (確認入力は controller が初期非表示化)
        var (clearHistBtn, clearHistInput, clearHistConfirm) = DangerRow(pt, ref y, "プレイ履歴を全削除");
        var (clearAllBtn,  clearAllInput,  clearAllConfirm)  = DangerRow(pt, ref y, "全データ削除 (NUKE)");

        var acct = panel.AddComponent<AccountTabController>();
        var soA  = new SerializedObject(acct);
        SetRef(soA, "_displayNameInput",        nameInput);
        SetRef(soA, "_displayNameSaveButton",   nameSave);
        SetRef(soA, "_statusMessageInput",      msgInput);
        SetRef(soA, "_statusMessageSaveButton", msgSave);
        SetRef(soA, "_charCountText",           cntTMP);
        SetRef(soA, "_notificationsToggle",     notifTgl);
        SetRef(soA, "_sessionInfoText",         sessTMP);
        SetRef(soA, "_logoutButton",            logoutBtn);
        soA.ApplyModifiedProperties();

        var data = panel.AddComponent<DataTabController>();
        var soD  = new SerializedObject(data);
        SetRef(soD, "_dbSizeText",                dbTMP);
        SetRef(soD, "_songsSizeText",             sgTMP);
        SetRef(soD, "_replaysSizeText",           rpTMP);
        SetRef(soD, "_refreshButton",             refreshBtn);
        SetRef(soD, "_manageSongsButton",         songsBtn);
        SetRef(soD, "_manageSongsPanel",          manageSongs);
        SetRef(soD, "_exportButton",              exportBtn);
        SetRef(soD, "_importButton",              importBtn);
        SetRef(soD, "_clearHistoryButton",        clearHistBtn);
        SetRef(soD, "_clearHistoryConfirmInput",  clearHistInput);
        SetRef(soD, "_clearHistoryConfirmButton", clearHistConfirm);
        SetRef(soD, "_clearAllButton",            clearAllBtn);
        SetRef(soD, "_clearAllConfirmInput",      clearAllInput);
        SetRef(soD, "_clearAllConfirmButton",     clearAllConfirm);
        soD.ApplyModifiedProperties();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // モーダル
    // ═════════════════════════════════════════════════════════════════════════

    static CalibrationPanel BuildCalibrationModal(Transform ct)
    {
        var root = Child("CalibrationPanel", ct);
        SR(root, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        root.AddComponent<CanvasGroup>();
        root.AddComponent<Image>().color = new Color(0,0,0,.85f);
        var panel = root.AddComponent<CalibrationPanel>();

        var container = Child("Container", root.transform);
        SR(container, V(.5f,.5f), V(.5f,.5f), V(.5f,.5f), V(0,0), V(720,480));
        container.AddComponent<Image>().color = Hex("10141F");

        var title = Child("Title", container.transform);
        SR(title, V(0,1), V(1,1), V(.5f,1), V(0,-20), V(0,50));
        T(title, "オートキャリブレーション", 28, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

        var closeBtn = SmallButton(container.transform, "閉じる", V(1,1), V(-12,-12), V(110,36), pivotTop: true);

        // Idle group
        var idle = Child("IdleGroup", container.transform);
        SR(idle, V(0,0), V(1,1), V(.5f,.5f), V(0,-20), V(-80,-120));
        var inst = Child("InstructionText", idle.transform);
        SR(inst, V(0,.4f), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        var instTMP = T(inst, "Space キーをクリック音に合わせて押してください。\n最初の 4 ビートは準備カウントです。\n合計 16 ビート(約 8 秒)", 18, Color.white, TextAlignmentOptions.Center);
        var startBtn = SmallButton(idle.transform, "開始", V(.5f,0), V(0,50), V(200,50));

        // Running group
        var running = Child("RunningGroup", container.transform);
        SR(running, V(0,0), V(1,1), V(.5f,.5f), V(0,-20), V(-80,-120));
        var beat = Child("BeatCounterText", running.transform);
        SR(beat, V(0,.5f), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        var beatTMP = T(beat, "Beat 0 / 16", 36, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        var progress = MakeBareSlider(running.transform, V(.1f,.25f), V(.9f,.35f));
        running.SetActive(false);

        // Result group
        var result = Child("ResultGroup", container.transform);
        SR(result, V(0,0), V(1,1), V(.5f,.5f), V(0,-20), V(-80,-120));
        var resText = Child("ResultText", result.transform);
        SR(resText, V(0,.35f), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        var resTMP = T(resText, "", 18, Color.white, TextAlignmentOptions.Center);
        var applyBtn  = SmallButton(result.transform, "適用",      V(.5f,.15f), V(-200,0), V(180,50));
        var retryBtn  = SmallButton(result.transform, "リトライ",  V(.5f,.15f), V(0,0),    V(180,50));
        var cancelBtn = SmallButton(result.transform, "キャンセル",V(.5f,.15f), V(200,0),  V(180,50));
        result.SetActive(false);

        var so = new SerializedObject(panel);
        SetRef(so, "_root",            root);
        SetRef(so, "_closeButton",     closeBtn);
        SetRef(so, "_idleGroup",       idle);
        SetRef(so, "_startButton",     startBtn);
        SetRef(so, "_instructionText", instTMP);
        SetRef(so, "_runningGroup",    running);
        SetRef(so, "_beatCounterText", beatTMP);
        SetRef(so, "_progressBar",     progress);
        SetRef(so, "_resultGroup",     result);
        SetRef(so, "_resultText",      resTMP);
        SetRef(so, "_applyButton",     applyBtn);
        SetRef(so, "_retryButton",     retryBtn);
        SetRef(so, "_cancelButton",    cancelBtn);
        so.ApplyModifiedProperties();

        root.SetActive(false);
        return panel;
    }

    static GameObject BuildDevicesModal(Transform ct)
    {
        var root = Child("DevicesPanel", ct);
        SR(root, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        root.AddComponent<Image>().color = new Color(0,0,0,.85f);

        var container = Child("Container", root.transform);
        SR(container, V(.5f,.5f), V(.5f,.5f), V(.5f,.5f), V(0,0), V(1200,720));
        container.AddComponent<Image>().color = Hex("10141F");

        var title = Child("Title", container.transform);
        SR(title, V(0,1), V(1,1), V(.5f,1), V(0,-18), V(0,44));
        T(title, "オーディオデバイスプロファイル管理", 26, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

        var closeBtn = SmallButton(container.transform, "閉じる", V(1,1), V(-12,-12), V(110,36), pivotTop: true);

        // ── 左: プロファイル一覧 ─────────────────────────────────────────────
        var listLbl = Child("ListLabel", container.transform);
        SR(listLbl, V(0,1), V(0,1), V(0,1), V(30,-70), V(300,30));
        T(listLbl, "プロファイル一覧", 18, Dim, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

        var sv = Child("ProfileScroll", container.transform);
        SR(sv, V(0,0), V(0,1), V(0,.5f), V(30,-30), V(420,-220));
        var scroll = sv.AddComponent<ScrollRect>();
        scroll.horizontal = false; scroll.scrollSensitivity = 25f;
        var vp = Child("Viewport", sv.transform);
        SR(vp, V(0,0), V(1,1), V(0,0), V(0,0), V(0,0));
        vp.AddComponent<RectMask2D>();
        var content = Child("Content", vp.transform);
        var contentRT = SR(content, V(0,1), V(1,1), V(.5f,1), V(0,0), V(0,0));
        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.childControlHeight = false; vlg.childForceExpandHeight = false;
        vlg.childControlWidth  = true;  vlg.childForceExpandWidth  = true;
        vlg.spacing = 4; vlg.padding = new RectOffset(0,0,4,4);
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = vp.GetComponent<RectTransform>(); scroll.content = contentRT;

        var addBtn = SmallButton(container.transform, "+ 新規プロファイル", V(0,0), V(30,24), V(420,44), anchorLeftBottom: true);

        // ── 右: 編集 ─────────────────────────────────────────────────────────
        float rx = 500f, rw = 660f;
        float ry = -76f;

        TextMeshProUGUI RightLabel(string text)
        {
            var go = Child("Lbl_" + text, container.transform);
            SR(go, V(0,1), V(0,1), V(0,1), V(rx,ry), V(240,28));
            return T(go, text, 16, Dim, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        }

        RightLabel("現在のOSデバイス");
        var curGO  = Child("CurrentDevice", container.transform);
        SR(curGO, V(0,1), V(0,1), V(0,1), V(rx+250,ry), V(rw-250-130,28));
        var curTMP = T(curGO, "(no device detected)", 16, Color.white, TextAlignmentOptions.MidlineLeft);
        var attachBtn = SmallButton(container.transform, "割当", V(0,1), V(rx+rw-110, ry+4), V(110,36), anchorLeftTop: true);
        ry -= 56f;

        RightLabel("プロファイル名");
        var nameInput = MakeInputAt(container.transform, rx+250, ry+4, rw-250, 40, "名前");
        ry -= 56f;

        RightLabel("割当OSデバイス");
        var osGO  = Child("OsDevice", container.transform);
        SR(osGO, V(0,1), V(0,1), V(0,1), V(rx+250,ry), V(rw-250-130,28));
        var osTMP = T(osGO, "-", 16, Color.white, TextAlignmentOptions.MidlineLeft);
        var clearBtn = SmallButton(container.transform, "クリア", V(0,1), V(rx+rw-110, ry+4), V(110,36), anchorLeftTop: true);
        ry -= 56f;

        RightLabel("デバイス検出で自動切替");
        var autoTgl = MakeToggleAt(container.transform, rx+250+18, ry-2);
        ry -= 56f;

        RightLabel("オフセット");
        var offGO  = Child("OffsetsInfo", container.transform);
        SR(offGO, V(0,1), V(0,1), V(0,1), V(rx+250,ry+6), V(rw-250,64));
        var offTMP = T(offGO, "判定: 0 ms\n映像: 0 ms", 16, Color.white, TextAlignmentOptions.TopLeft);
        ry -= 96f;

        var setActiveBtn = SmallButton(container.transform, "アクティブにする", V(0,1), V(rx,        ry), V(210,46), anchorLeftTop: true);
        var saveBtn      = SmallButton(container.transform, "保存",             V(0,1), V(rx+226,   ry), V(150,46), anchorLeftTop: true);
        var deleteBtn    = SmallButton(container.transform, "削除",             V(0,1), V(rx+392,   ry), V(150,46), anchorLeftTop: true, danger: true);

        // プロファイル行プレハブ
        var itemPrefab = BuildProfileItemPrefab();

        var tab = root.AddComponent<DevicesTabController>();
        var so  = new SerializedObject(tab);
        SetRef(so, "_currentDeviceText",     curTMP);
        SetRef(so, "_attachToProfileButton", attachBtn);
        SetRef(so, "_profileListContent",    contentRT);
        SetRef(so, "_profileListItemPrefab", itemPrefab);
        SetRef(so, "_addNewButton",          addBtn);
        SetRef(so, "_displayNameInput",      nameInput);
        SetRef(so, "_osDeviceValueText",     osTMP);
        SetRef(so, "_clearOsDeviceButton",   clearBtn);
        SetRef(so, "_autoSwitchToggle",      autoTgl);
        SetRef(so, "_offsetsInfoText",       offTMP);
        SetRef(so, "_setActiveButton",       setActiveBtn);
        SetRef(so, "_deleteButton",          deleteBtn);
        SetRef(so, "_saveButton",            saveBtn);
        SetRef(so, "_closeButton",           closeBtn);
        so.ApplyModifiedProperties();

        root.SetActive(false);
        return root;
    }

    static ManageSongsPanel BuildManageSongsModal(Transform ct)
    {
        var root = Child("ManageSongsPanel", ct);
        SR(root, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        root.AddComponent<CanvasGroup>();
        root.AddComponent<Image>().color = new Color(0,0,0,.85f);
        var panel = root.AddComponent<ManageSongsPanel>();

        var container = Child("Container", root.transform);
        SR(container, V(.5f,.5f), V(.5f,.5f), V(.5f,.5f), V(0,0), V(960,600));
        container.AddComponent<Image>().color = Hex("10141F");

        var title = Child("Title", container.transform);
        SR(title, V(0,1), V(1,1), V(.5f,1), V(0,-20), V(0,50));
        T(title, "楽曲管理", 28, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

        var closeBtn   = SmallButton(container.transform, "閉じる", V(1,1), V(-12,-12), V(110,36), pivotTop: true);
        var refreshBtn = SmallButton(container.transform, "更新",   V(0,1), V(12,-12),  V(110,36), anchorLeftTop: true);

        var sv = Child("ScrollView", container.transform);
        SR(sv, V(0,0), V(1,1), V(.5f,.5f), V(0,-30), V(-40,-140));
        var scroll = sv.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        var vp = Child("Viewport", sv.transform);
        SR(vp, V(0,0), V(1,1), V(0,0), V(0,0), V(0,0));
        vp.AddComponent<RectMask2D>();
        var content = Child("Content", vp.transform);
        var contentRT = SR(content, V(0,1), V(1,1), V(.5f,1), V(0,0), V(0,0));
        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 4;
        vlg.childControlWidth = true;  vlg.childForceExpandWidth  = true;
        vlg.childControlHeight = true; vlg.childForceExpandHeight = false;
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = vp.GetComponent<RectTransform>(); scroll.content = contentRT;

        var empty = Child("EmptyMessage", container.transform);
        SR(empty, V(.5f,.5f), V(.5f,.5f), V(.5f,.5f), V(0,0), V(600,60));
        var emptyTMP = T(empty, "", 22, Dim, TextAlignmentOptions.Center);
        empty.SetActive(false);

        var so = new SerializedObject(panel);
        SetRef(so, "_root",          root);
        SetRef(so, "_closeButton",   closeBtn);
        SetRef(so, "_refreshButton", refreshBtn);
        SetRef(so, "_listContent",   contentRT);
        SetRef(so, "_emptyMessage",  emptyTMP);
        so.ApplyModifiedProperties();

        root.SetActive(false);
        return panel;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // プレハブ
    // ═════════════════════════════════════════════════════════════════════════

    static GameObject BuildTabButtonPrefab()
    {
        var root   = new GameObject("ConfigTabButton");
        var rootRT = root.AddComponent<RectTransform>(); rootRT.sizeDelta = V(150, 52);
        root.AddComponent<LayoutElement>().minWidth = 150;
        var btn = root.AddComponent<Button>();

        var bgGO  = Child("Background", root.transform);
        SR(bgGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        var bgImg = bgGO.AddComponent<Image>(); bgImg.color = Faint;
        btn.targetGraphic = bgImg;

        var lblGO = Child("Label", root.transform);
        SR(lblGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        var lbl = T(lblGO, "TAB", 17, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        lbl.raycastTarget = false;

        var saved = PrefabUtility.SaveAsPrefabAsset(root, TabPrefabPath);
        Object.DestroyImmediate(root);
        return saved;
    }

    static GameObject BuildProfileItemPrefab()
    {
        var root   = new GameObject("ProfileListItem");
        var rootRT = root.AddComponent<RectTransform>(); rootRT.sizeDelta = V(400, 60);
        root.AddComponent<LayoutElement>().minHeight = 60;
        var btn = root.AddComponent<Button>();

        var bgGO  = Child("Background", root.transform);
        SR(bgGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        var bgImg = bgGO.AddComponent<Image>(); bgImg.color = new Color(1,1,1,.05f);
        btn.targetGraphic = bgImg;

        var indGO  = Child("ActiveIndicator", root.transform);
        SR(indGO, V(0,0), V(0,1), V(0,.5f), V(0,0), V(8,0));
        var indImg = indGO.AddComponent<Image>(); indImg.color = Accent; indImg.raycastTarget = false;

        var nameGO = Child("NameText", root.transform);
        SR(nameGO, V(0,.5f), V(1,1), V(0,1), V(20,-4), V(-30,26));
        var nameTMP = T(nameGO, "Profile", 18, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        nameTMP.raycastTarget = false;

        var devGO = Child("DeviceText", root.transform);
        SR(devGO, V(0,0), V(1,.5f), V(0,0), V(20,4), V(-30,22));
        var devTMP = T(devGO, "OS: -", 13, Dim, TextAlignmentOptions.MidlineLeft);
        devTMP.raycastTarget = false;

        var saved = PrefabUtility.SaveAsPrefabAsset(root, ProfileItemPrefabPath);
        Object.DestroyImmediate(root);
        return saved;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 行ヘルパー (モックの行スタイル: 帯背景 + 左ラベル + 右コントロール)
    // ═════════════════════════════════════════════════════════════════════════

    static GameObject MakePanel(Transform ct, string name)
    {
        var go = Child(name, ct);
        SR(go, V(0,1), V(0,1), V(0,1), V(ContentX,-ContentY), V(ContentW,ContentH));
        return go;
    }

    static GameObject Row(Transform parent, ref float y, string label, float h = RowH)
    {
        var row = Child("Row_" + label, parent);
        SR(row, V(0,1), V(1,1), V(.5f,1), V(0,y), V(0,h));
        var img = row.AddComponent<Image>(); img.color = RowBg; img.raycastTarget = false;

        var lblGO = Child("Label", row.transform);
        SR(lblGO, V(0,0), V(0,1), V(0,.5f), V(24,0), V(390,0));
        T(lblGO, label, 20, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

        y -= h + (RowStep - RowH);
        return row;
    }

    static (Slider, TextMeshProUGUI) SliderRow(Transform parent, ref float y, string label,
        float min, float max, float def, bool whole, string initialValueText)
    {
        var row = Row(parent, ref y, label);

        float step = whole ? 1f : 0.5f;
        var minusBtn = StepperButton(row.transform, "◁", V(430, 0));
        var slider   = MakeSliderAt(row.transform, 478, 790, min, max, def, whole);
        var plusBtn  = StepperButton(row.transform, "▷", V(816, 0));

        WireStepper(minusBtn, slider, -step);
        WireStepper(plusBtn,  slider, +step);

        // 値ボックス
        var boxGO = Child("ValueBox", row.transform);
        SR(boxGO, V(1,.5f), V(1,.5f), V(1,.5f), V(-20,0), V(150,40));
        var boxImg = boxGO.AddComponent<Image>(); boxImg.color = BoxBg; boxImg.raycastTarget = false;
        var valGO  = Child("Value", boxGO.transform);
        SR(valGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        var valTMP = T(valGO, initialValueText, 19, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

        return (slider, valTMP);
    }

    static TMP_Dropdown DropdownRow(Transform parent, ref float y, string label)
    {
        var row = Row(parent, ref y, label);
        return MakeDropdownAt(row.transform, V(1,.5f), V(-20,0), V(420,40));
    }

    static Toggle ToggleRow(Transform parent, ref float y, string label)
    {
        var row = Row(parent, ref y, label);
        return MakeToggleAt(row.transform, 0, 0, anchorRight: true);
    }

    static Button ButtonRow(Transform parent, ref float y, string label, string buttonText)
    {
        var row = Row(parent, ref y, label);
        return SmallButton(row.transform, buttonText, V(1,.5f), V(-20,0), V(240,40));
    }

    static (Button, TMP_InputField, Button) DangerRow(Transform parent, ref float y, string label)
    {
        var row = Row(parent, ref y, label);
        var mainBtn = SmallButton(row.transform, "実行...", V(1,.5f), V(-20,0), V(150,40), danger: true);

        var input = MakeInputAtRect(row.transform, V(1,.5f), V(-390,0), V(280,40), "確認ワード");
        var confirmBtn = SmallButton(row.transform, "確定", V(1,.5f), V(-190,0), V(100,40), danger: true);
        // controller (SetupDangerButton) が初期非表示化する
        return (mainBtn, input, confirmBtn);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // コントロールヘルパー
    // ═════════════════════════════════════════════════════════════════════════

    static void MakeShiftHint(Transform parent, string keyLabel, string arrow, bool left)
    {
        var chip = Child("Hint_" + keyLabel, parent);
        if (left) SR(chip, V(0,.5f), V(0,.5f), V(1,.5f), V(-14,0), V(86,30));
        else      SR(chip, V(1,.5f), V(1,.5f), V(0,.5f), V(14,0),  V(86,30));
        var chipImg = chip.AddComponent<Image>(); chipImg.color = BoxBg; chipImg.raycastTarget = false;
        var chipLbl = Child("Label", chip.transform);
        SR(chipLbl, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        T(chipLbl, keyLabel, 14, Dim, TextAlignmentOptions.Center, FontStyles.Bold);

        var arGO = Child("Arrow", chip.transform);
        if (left) SR(arGO, V(1,.5f), V(1,.5f), V(0,.5f), V(8,0), V(30,30));
        else      SR(arGO, V(0,.5f), V(0,.5f), V(1,.5f), V(-8,0), V(30,30));
        var arTMP = T(arGO, arrow, 22, AccentHi, TextAlignmentOptions.Center, FontStyles.Bold);
        arTMP.raycastTarget = false;
    }

    static (Button, GameObject) MakeChipButton(Transform ct, string keyLabel, string text, Color btnColor,
        bool anchorLeft, Vector2 pos, Vector2 size)
    {
        var group = Child("Bottom_" + keyLabel, ct);
        var a = anchorLeft ? V(0,0) : V(1,0);
        SR(group, a, a, anchorLeft ? V(0,0) : V(1,0), pos, V(size.x + 100, size.y));

        var chip = Child("KeyChip", group.transform);
        SR(chip, V(0,.5f), V(0,.5f), V(0,.5f), V(0,0), V(72,34));
        var chipImg = chip.AddComponent<Image>(); chipImg.color = BoxBg; chipImg.raycastTarget = false;
        var chipLbl = Child("Label", chip.transform);
        SR(chipLbl, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        T(chipLbl, keyLabel, 15, Dim, TextAlignmentOptions.Center, FontStyles.Bold);

        var btnGO  = Child("Button", group.transform);
        SR(btnGO, V(0,.5f), V(0,.5f), V(0,.5f), V(84,0), size);
        var btnImg = btnGO.AddComponent<Image>(); btnImg.color = btnColor;
        var btn    = btnGO.AddComponent<Button>(); btn.targetGraphic = btnImg;
        var btnLbl = Child("Label", btnGO.transform);
        SR(btnLbl, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        T(btnLbl, text, 20, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

        return (btn, group);
    }

    static Button StepperButton(Transform row, string arrow, Vector2 pos)
    {
        var go  = Child("Step" + arrow, row);
        SR(go, V(0,.5f), V(0,.5f), V(.5f,.5f), pos, V(36,36));
        var img = go.AddComponent<Image>(); img.color = Faint;
        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        var lbl = Child("Label", go.transform);
        SR(lbl, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        var t = T(lbl, arrow, 18, AccentHi, TextAlignmentOptions.Center, FontStyles.Bold);
        t.raycastTarget = false;
        return btn;
    }

    static void WireStepper(Button btn, Slider slider, float delta)
    {
        var stepper = btn.gameObject.AddComponent<RhythmGame.UI.Common.SliderStepper>();
        var so = new SerializedObject(stepper);
        SetRef(so, "_slider", slider);
        var p = so.FindProperty("_delta");
        if (p != null) p.floatValue = delta;
        so.ApplyModifiedProperties();
    }

    static Slider MakeSliderAt(Transform row, float x0, float x1,
        float min, float max, float def, bool whole)
    {
        var sGO = Child("Slider", row);
        // pivot=中央。anchoredPosition には中央座標を渡す (左pivotだと右に半幅ずれて ▷/値ボックスに重なる)
        var sRT = SR(sGO, V(0,.5f), V(0,.5f), V(.5f,.5f), V((x0+x1)/2f, 0), V(x1-x0, 22));

        var bgGO = Child("Background", sGO.transform);
        SR(bgGO, V(0,.3f), V(1,.7f), V(.5f,.5f), V(0,0), V(0,0));
        bgGO.AddComponent<Image>().color = new Color(1,1,1,.16f);

        var faGO   = Child("Fill Area", sGO.transform);
        SR(faGO, V(0,.3f), V(1,.7f), V(.5f,.5f), V(0,0), V(-20,0));
        var fillGO = Child("Fill", faGO.transform);
        var fillRT = SR(fillGO, V(0,0), V(0,1), V(0,.5f), V(0,0), V(10,0));
        fillGO.AddComponent<Image>().color = Accent;

        var hsaGO = Child("Handle Slide Area", sGO.transform);
        SR(hsaGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(-20,0));
        var hdlGO  = Child("Handle", hsaGO.transform);
        var hdlRT  = SR(hdlGO, V(0,0), V(0,1), V(.5f,.5f), V(0,0), V(22,4));
        var hdlImg = hdlGO.AddComponent<Image>(); hdlImg.color = Color.white;

        var slider = sGO.AddComponent<Slider>();
        slider.fillRect = fillRT; slider.handleRect = hdlRT; slider.targetGraphic = hdlImg;
        slider.minValue = min; slider.maxValue = max; slider.value = def;
        slider.wholeNumbers = whole;
        return slider;
    }

    static Slider MakeBareSlider(Transform parent, Vector2 aMin, Vector2 aMax)
    {
        var go = Child("ProgressBar", parent);
        var rt = SR(go, aMin, aMax, V(.5f,.5f), V(0,0), V(0,0));

        var bg = Child("Background", go.transform);
        SR(bg, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        bg.AddComponent<Image>().color = new Color(1,1,1,.15f);

        var fa = Child("Fill Area", go.transform);
        SR(fa, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        var fill = Child("Fill", fa.transform);
        var fillRT = SR(fill, V(0,0), V(.5f,1), V(0,.5f), V(0,0), V(0,0));
        var fillImg = fill.AddComponent<Image>(); fillImg.color = Accent;

        var slider = go.AddComponent<Slider>();
        slider.fillRect = fillRT; slider.targetGraphic = fillImg;
        slider.minValue = 0; slider.maxValue = 1; slider.value = 0;
        slider.interactable = false;
        return slider;
    }

    static Toggle MakeToggleAt(Transform parent, float x, float yPos, bool anchorRight = false)
    {
        var go = Child("Toggle", parent);
        if (anchorRight) SR(go, V(1,.5f), V(1,.5f), V(1,.5f), V(-24,0), V(40,40));
        else             SR(go, V(0,1),   V(0,1),   V(0,1),   V(x,yPos), V(40,40));

        var bgGO  = Child("Background", go.transform);
        SR(bgGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        var bgImg = bgGO.AddComponent<Image>(); bgImg.color = BoxBg;

        var ckGO  = Child("Checkmark", go.transform);
        SR(ckGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(-12,-12));
        var ckImg = ckGO.AddComponent<Image>(); ckImg.color = AccentHi; ckImg.raycastTarget = false;

        var tgl = go.AddComponent<Toggle>();
        tgl.targetGraphic = bgImg;
        tgl.graphic       = ckImg;
        return tgl;
    }

    static Button SmallButton(Transform parent, string text, Vector2 anchor, Vector2 pos, Vector2 size,
        bool pivotTop = false, bool anchorLeftBottom = false, bool anchorLeftTop = false, bool danger = false)
    {
        var go = Child("Btn_" + text, parent);
        Vector2 pivot;
        if (pivotTop)             pivot = V(1,1);
        else if (anchorLeftBottom){ anchor = V(0,0); pivot = V(0,0); }
        else if (anchorLeftTop)   { anchor = V(0,1); pivot = V(0,1); }
        else                      pivot = anchor;
        SR(go, anchor, anchor, pivot, pos, size);

        var img = go.AddComponent<Image>(); img.color = danger ? Danger : new Color(.30f,.34f,.52f,.95f);
        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        var lbl = Child("Label", go.transform);
        SR(lbl, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        var t = T(lbl, text, 17, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        t.raycastTarget = false;
        return btn;
    }

    static TMP_InputField MakeInput(Transform row, float x, float w, string placeholder)
        => MakeInputAtRect(row, V(0,.5f), V(x + w/2f, 0), V(w, 40), placeholder, pivotCenter: true);

    static TMP_InputField MakeInputAt(Transform parent, float x, float yTop, float w, float h, string placeholder)
        => MakeInputAtRect(parent, V(0,1), V(x, yTop), V(w, h), placeholder, pivotTopLeft: true);

    static TMP_InputField MakeInputAtRect(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size,
        string placeholder, bool pivotCenter = false, bool pivotTopLeft = false)
    {
        var go = Child("Input", parent);
        Vector2 pivot = pivotCenter ? V(.5f,.5f) : (pivotTopLeft ? V(0,1) : anchor);
        SR(go, anchor, anchor, pivot, pos, size);
        var img = go.AddComponent<Image>(); img.color = BoxBg;

        var input = go.AddComponent<TMP_InputField>();
        input.targetGraphic = img;

        var areaGO = Child("TextArea", go.transform);
        var areaRT = SR(areaGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(-20,-8));
        areaGO.AddComponent<RectMask2D>();

        var phGO  = Child("Placeholder", areaGO.transform);
        SR(phGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        var phTMP = T(phGO, placeholder, 16, new Color(1,1,1,.3f), TextAlignmentOptions.MidlineLeft);

        var txGO  = Child("Text", areaGO.transform);
        SR(txGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        var txTMP = T(txGO, "", 16, Color.white, TextAlignmentOptions.MidlineLeft);

        input.textViewport  = areaRT;
        input.textComponent = txTMP;
        input.placeholder   = phTMP;
        return input;
    }

    static TMP_Dropdown MakeDropdownAt(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var ddGO  = Child("Dropdown", parent);
        SR(ddGO, anchor, anchor, anchor, pos, size);
        var ddImg = ddGO.AddComponent<Image>(); ddImg.color = BoxBg;
        var dd    = ddGO.AddComponent<TMP_Dropdown>(); dd.targetGraphic = ddImg;

        var capGO = Child("Label", ddGO.transform);
        SR(capGO, V(0,0), V(1,1), V(.5f,.5f), V(-12,0), V(-44,-8));
        dd.captionText = T(capGO, "-", 17, Color.white, TextAlignmentOptions.MidlineLeft);

        var arwGO = Child("Arrow", ddGO.transform);
        SR(arwGO, V(1,.5f), V(1,.5f), V(1,.5f), V(-12,0), V(22,22));
        var arwTMP = T(arwGO, "▼", 14, AccentHi, TextAlignmentOptions.Center);
        arwTMP.raycastTarget = false;

        var tplGO = Child("Template", ddGO.transform);
        SR(tplGO, V(0,0), V(1,0), V(.5f,1), V(0,2), V(0,200));
        tplGO.AddComponent<Image>().color = Hex("10141F");
        var tplSR = tplGO.AddComponent<ScrollRect>();
        tplSR.horizontal = false;
        tplGO.AddComponent<CanvasGroup>();
        dd.template = tplGO.GetComponent<RectTransform>();

        var tvpGO = Child("Viewport", tplGO.transform);
        SR(tvpGO, V(0,0), V(1,1), V(0,1), V(0,0), V(0,0));
        // UI.Mask + スプライト無し Image は子要素(ドロップダウン項目のラベル)が全く描画されない罠。
        // RectMask2D はスプライト不要で矩形クリップするだけなので項目テキストが正しく表示される。
        tvpGO.AddComponent<RectMask2D>();

        var tcGO = Child("Content", tvpGO.transform);
        var tcRT = SR(tcGO, V(0,1), V(1,1), V(.5f,1), V(0,0), V(0,32));

        var itemGO = Child("Item", tcGO.transform);
        SR(itemGO, V(0,1), V(1,1), V(.5f,1), V(0,0), V(0,32));
        var tog = itemGO.AddComponent<Toggle>();
        var ibGO  = Child("Item Background", itemGO.transform);
        SR(ibGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        var ibImg = ibGO.AddComponent<Image>(); ibImg.color = Color.clear;
        var ickGO = Child("Item Checkmark", itemGO.transform);
        SR(ickGO, V(0,.5f), V(0,.5f), V(.5f,.5f), V(12,0), V(16,16));
        ickGO.AddComponent<Image>().color = Accent;
        var ilGO  = Child("Item Label", itemGO.transform);
        SR(ilGO, V(0,0), V(1,1), V(.5f,.5f), V(8,0), V(-16,0));
        dd.itemText = T(ilGO, "Option", 16, Color.white, TextAlignmentOptions.MidlineLeft);
        tog.graphic = ickGO.GetComponent<Image>(); tog.targetGraphic = ibImg;

        tplSR.content  = tcRT;
        tplSR.viewport = tvpGO.GetComponent<RectTransform>();
        tplGO.SetActive(false);
        return dd;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Micro helpers
    // ═════════════════════════════════════════════════════════════════════════

    static void RegisterInBuildSettings(string scenePath)
    {
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(
            EditorBuildSettings.scenes);
        if (scenes.Exists(s => s.path == scenePath)) return;
        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    static void SetRef(SerializedObject so, string prop, Object value)
    {
        var p = so.FindProperty(prop);
        if (p == null) { Debug.LogWarning($"[BuildConfigScene] missing prop: {prop}"); return; }
        p.objectReferenceValue = value;
    }

    static void SetArr(SerializedObject so, string prop, Object[] values)
    {
        var p = so.FindProperty(prop);
        if (p == null) { Debug.LogWarning($"[BuildConfigScene] missing prop: {prop}"); return; }
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
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
