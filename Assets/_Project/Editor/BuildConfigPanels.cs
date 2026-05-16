using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Config.unity の AudioTabPanel / DataTabPanel に CalibrationPanel / ManageSongsPanel を
/// 自動構築し、各 Controller の SerializeField を配線する Editor 拡張。
/// </summary>
public static class BuildConfigPanels
{
    [MenuItem("Tools/Build Calibration & Manage Songs Panels")]
    public static void Build()
    {
        const string scenePath = "Assets/_Project/Scenes/Config.unity";
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != scenePath)
        {
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }

        BuildCalibration();
        BuildManageSongs();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[BuildConfigPanels] Done.");
    }

    // ── Calibration Panel ─────────────────────────────────────────────────────

    static void BuildCalibration()
    {
        var audioTab = GameObject.Find("AudioTabPanel");
        if (audioTab == null)
        {
            // Inactive 状態でも探せるよう、すべての RectTransform から検索
            audioTab = FindByNameIncludeInactive("AudioTabPanel");
        }
        if (audioTab == null) { Debug.LogError("[BuildConfigPanels] AudioTabPanel not found"); return; }

        // 既存パネル削除(再実行可能にする)
        var existing = audioTab.transform.Find("CalibrationPanel");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        // ルート(画面全体に被さるモーダル)
        var root = new GameObject("CalibrationPanel");
        root.transform.SetParent(audioTab.transform, false);
        var rootRt = root.AddComponent<RectTransform>();
        FullStretch(rootRt);
        root.AddComponent<CanvasGroup>();
        var bgImg = root.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.85f);
        bgImg.raycastTarget = true;

        var panel = root.AddComponent<CalibrationPanel>();

        // Container(中央の固定サイズボックス)
        var container = MakeRT("Container", root);
        var containerRt = container.GetComponent<RectTransform>();
        containerRt.anchorMin = new Vector2(0.5f, 0.5f);
        containerRt.anchorMax = new Vector2(0.5f, 0.5f);
        containerRt.sizeDelta = new Vector2(720, 480);
        var containerImg = container.AddComponent<Image>();
        containerImg.color = new Color(0.10f, 0.12f, 0.16f, 1f);

        var title = MakeTMP("Title", container, 28, "Auto Calibration");
        var titleRt = title.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0, 1); titleRt.anchorMax = new Vector2(1, 1);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0, -20);
        titleRt.sizeDelta = new Vector2(0, 50);
        title.alignment = TextAlignmentOptions.Center;

        var closeBtn = MakeButton("CloseButton", container, "Close");
        var closeRt = closeBtn.GetComponent<RectTransform>();
        closeRt.anchorMin = new Vector2(1, 1); closeRt.anchorMax = new Vector2(1, 1);
        closeRt.pivot     = new Vector2(1, 1);
        closeRt.anchoredPosition = new Vector2(-12, -12);
        closeRt.sizeDelta = new Vector2(100, 36);

        // ── Idle group ────────────────────────────────────────────────────────
        var idleGroup = MakeRT("IdleGroup", container);
        var idleRt = idleGroup.GetComponent<RectTransform>();
        idleRt.anchorMin = new Vector2(0, 0); idleRt.anchorMax = new Vector2(1, 1);
        idleRt.offsetMin = new Vector2(40, 40); idleRt.offsetMax = new Vector2(-40, -80);

        var instruction = MakeTMP("InstructionText", idleGroup, 18,
            "Space キーをクリック音に合わせて押してください。\n最初の 4 ビートは準備カウントです。\n合計 16 ビート(約 8 秒)");
        var instRt = instruction.GetComponent<RectTransform>();
        instRt.anchorMin = new Vector2(0, 0.4f); instRt.anchorMax = new Vector2(1, 1);
        instRt.offsetMin = Vector2.zero; instRt.offsetMax = Vector2.zero;
        instruction.alignment = TextAlignmentOptions.Center;
        instruction.enableWordWrapping = true;

        var startBtn = MakeButton("StartButton", idleGroup, "Start");
        var startRt = startBtn.GetComponent<RectTransform>();
        startRt.anchorMin = new Vector2(0.5f, 0); startRt.anchorMax = new Vector2(0.5f, 0);
        startRt.anchoredPosition = new Vector2(0, 50);
        startRt.sizeDelta = new Vector2(200, 50);

        // ── Running group ─────────────────────────────────────────────────────
        var runningGroup = MakeRT("RunningGroup", container);
        var runningRt = runningGroup.GetComponent<RectTransform>();
        runningRt.anchorMin = new Vector2(0, 0); runningRt.anchorMax = new Vector2(1, 1);
        runningRt.offsetMin = new Vector2(40, 40); runningRt.offsetMax = new Vector2(-40, -80);
        runningGroup.SetActive(false);

        var beatCounter = MakeTMP("BeatCounterText", runningGroup, 36, "Beat 0 / 16");
        var bcRt = beatCounter.GetComponent<RectTransform>();
        bcRt.anchorMin = new Vector2(0, 0.5f); bcRt.anchorMax = new Vector2(1, 1);
        bcRt.offsetMin = Vector2.zero; bcRt.offsetMax = Vector2.zero;
        beatCounter.alignment = TextAlignmentOptions.Center;

        var progressBar = MakeSlider("ProgressBar", runningGroup);
        var pbRt = progressBar.GetComponent<RectTransform>();
        pbRt.anchorMin = new Vector2(0.1f, 0.25f); pbRt.anchorMax = new Vector2(0.9f, 0.35f);
        pbRt.offsetMin = Vector2.zero; pbRt.offsetMax = Vector2.zero;

        // ── Result group ──────────────────────────────────────────────────────
        var resultGroup = MakeRT("ResultGroup", container);
        var resultRt = resultGroup.GetComponent<RectTransform>();
        resultRt.anchorMin = new Vector2(0, 0); resultRt.anchorMax = new Vector2(1, 1);
        resultRt.offsetMin = new Vector2(40, 40); resultRt.offsetMax = new Vector2(-40, -80);
        resultGroup.SetActive(false);

        var resultText = MakeTMP("ResultText", resultGroup, 18, "");
        var rtRt = resultText.GetComponent<RectTransform>();
        rtRt.anchorMin = new Vector2(0, 0.35f); rtRt.anchorMax = new Vector2(1, 1);
        rtRt.offsetMin = Vector2.zero; rtRt.offsetMax = Vector2.zero;
        resultText.alignment = TextAlignmentOptions.Center;
        resultText.enableWordWrapping = true;

        var applyBtn  = MakeButton("ApplyButton",  resultGroup, "Apply");
        var retryBtn  = MakeButton("RetryButton",  resultGroup, "Retry");
        var cancelBtn = MakeButton("CancelButton", resultGroup, "Cancel");
        PlaceButtonRow(applyBtn, 0.5f - 1f, 0.15f);   // 左 ・ 中 ・ 右
        PlaceButtonRow(retryBtn, 0.5f,        0.15f);
        PlaceButtonRow(cancelBtn,0.5f + 1f, 0.15f);

        // SerializeField 配線
        var so = new SerializedObject(panel);
        so.FindProperty("_root")            .objectReferenceValue = root;
        so.FindProperty("_closeButton")     .objectReferenceValue = closeBtn;
        so.FindProperty("_idleGroup")       .objectReferenceValue = idleGroup;
        so.FindProperty("_startButton")     .objectReferenceValue = startBtn;
        so.FindProperty("_instructionText") .objectReferenceValue = instruction;
        so.FindProperty("_runningGroup")    .objectReferenceValue = runningGroup;
        so.FindProperty("_beatCounterText") .objectReferenceValue = beatCounter;
        so.FindProperty("_progressBar")     .objectReferenceValue = progressBar;
        so.FindProperty("_resultGroup")     .objectReferenceValue = resultGroup;
        so.FindProperty("_resultText")      .objectReferenceValue = resultText;
        so.FindProperty("_applyButton")     .objectReferenceValue = applyBtn;
        so.FindProperty("_retryButton")     .objectReferenceValue = retryBtn;
        so.FindProperty("_cancelButton")    .objectReferenceValue = cancelBtn;
        so.ApplyModifiedPropertiesWithoutUndo();

        // AudioTabController に CalibrationPanel 参照配線
        var audioCtrl = audioTab.GetComponent<AudioTabController>();
        if (audioCtrl != null)
        {
            var soCtrl = new SerializedObject(audioCtrl);
            soCtrl.FindProperty("_calibrationPanel").objectReferenceValue = panel;
            soCtrl.ApplyModifiedPropertiesWithoutUndo();
        }

        // 初期状態は非アクティブ(open() で表示)
        root.SetActive(false);
        Debug.Log("[BuildConfigPanels] CalibrationPanel built.");
    }

    // ── Manage Songs Panel ────────────────────────────────────────────────────

    static void BuildManageSongs()
    {
        var dataTab = GameObject.Find("DataTabPanel") ?? FindByNameIncludeInactive("DataTabPanel");
        if (dataTab == null) { Debug.LogError("[BuildConfigPanels] DataTabPanel not found"); return; }

        var existing = dataTab.transform.Find("ManageSongsPanel");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        var root = new GameObject("ManageSongsPanel");
        root.transform.SetParent(dataTab.transform, false);
        var rootRt = root.AddComponent<RectTransform>();
        FullStretch(rootRt);
        root.AddComponent<CanvasGroup>();
        var bgImg = root.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.85f);

        var panel = root.AddComponent<ManageSongsPanel>();

        var container = MakeRT("Container", root);
        var containerRt = container.GetComponent<RectTransform>();
        containerRt.anchorMin = new Vector2(0.5f, 0.5f);
        containerRt.anchorMax = new Vector2(0.5f, 0.5f);
        containerRt.sizeDelta = new Vector2(960, 600);
        container.AddComponent<Image>().color = new Color(0.10f, 0.12f, 0.16f, 1f);

        var title = MakeTMP("Title", container, 28, "Manage Songs");
        var titleRt = title.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0, 1); titleRt.anchorMax = new Vector2(1, 1);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0, -20);
        titleRt.sizeDelta = new Vector2(0, 50);
        title.alignment = TextAlignmentOptions.Center;

        var closeBtn = MakeButton("CloseButton", container, "Close");
        var closeRt = closeBtn.GetComponent<RectTransform>();
        closeRt.anchorMin = new Vector2(1, 1); closeRt.anchorMax = new Vector2(1, 1);
        closeRt.pivot     = new Vector2(1, 1);
        closeRt.anchoredPosition = new Vector2(-12, -12);
        closeRt.sizeDelta = new Vector2(100, 36);

        var refreshBtn = MakeButton("RefreshButton", container, "Refresh");
        var refreshRt = refreshBtn.GetComponent<RectTransform>();
        refreshRt.anchorMin = new Vector2(0, 1); refreshRt.anchorMax = new Vector2(0, 1);
        refreshRt.pivot     = new Vector2(0, 1);
        refreshRt.anchoredPosition = new Vector2(12, -12);
        refreshRt.sizeDelta = new Vector2(120, 36);

        // ScrollView
        var sv = MakeRT("ScrollView", container);
        var svRt = sv.GetComponent<RectTransform>();
        svRt.anchorMin = new Vector2(0, 0); svRt.anchorMax = new Vector2(1, 1);
        svRt.offsetMin = new Vector2(20, 20); svRt.offsetMax = new Vector2(-20, -80);
        var scrollRect = sv.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical   = true;

        var viewport = MakeRT("Viewport", sv);
        FullStretch(viewport.GetComponent<RectTransform>());
        var viewportImg = viewport.AddComponent<Image>();
        viewportImg.color = new Color(0, 0, 0, 0);
        viewport.AddComponent<RectMask2D>();

        var content = MakeRT("Content", viewport);
        var contentRt = content.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0, 1); contentRt.anchorMax = new Vector2(1, 1);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.offsetMin = Vector2.zero; contentRt.offsetMax = Vector2.zero;
        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 4;
        vlg.childControlWidth  = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.content  = contentRt;

        var emptyMsg = MakeTMP("EmptyMessage", container, 22, "");
        var emRt = emptyMsg.GetComponent<RectTransform>();
        emRt.anchorMin = new Vector2(0.5f, 0.5f); emRt.anchorMax = new Vector2(0.5f, 0.5f);
        emRt.anchoredPosition = Vector2.zero; emRt.sizeDelta = new Vector2(600, 60);
        emptyMsg.alignment = TextAlignmentOptions.Center;
        emptyMsg.gameObject.SetActive(false);

        // SerializeField 配線
        var so = new SerializedObject(panel);
        so.FindProperty("_root")          .objectReferenceValue = root;
        so.FindProperty("_closeButton")   .objectReferenceValue = closeBtn;
        so.FindProperty("_refreshButton") .objectReferenceValue = refreshBtn;
        so.FindProperty("_listContent")   .objectReferenceValue = contentRt;
        so.FindProperty("_emptyMessage")  .objectReferenceValue = emptyMsg;
        so.ApplyModifiedPropertiesWithoutUndo();

        // DataTabController に ManageSongsPanel 参照配線
        var dataCtrl = dataTab.GetComponent<DataTabController>();
        if (dataCtrl != null)
        {
            var soCtrl = new SerializedObject(dataCtrl);
            soCtrl.FindProperty("_manageSongsPanel").objectReferenceValue = panel;
            soCtrl.ApplyModifiedPropertiesWithoutUndo();
        }

        root.SetActive(false);
        Debug.Log("[BuildConfigPanels] ManageSongsPanel built.");
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

    static TextMeshProUGUI MakeTMP(string name, GameObject parent, int size, string text)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var rt = go.AddComponent<RectTransform>();
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text     = text;
        tmp.fontSize = size;
        tmp.color    = Color.white;
        rt.sizeDelta = new Vector2(300, size + 10);
        return tmp;
    }

    static Button MakeButton(string name, GameObject parent, string label)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<RectTransform>();
        var img = go.AddComponent<Image>();
        img.color = new Color(0.2f, 0.4f, 0.7f, 0.85f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var lbl = MakeTMP("Label", go, 18, label);
        var lblRt = lbl.GetComponent<RectTransform>();
        lblRt.anchorMin = Vector2.zero; lblRt.anchorMax = Vector2.one;
        lblRt.offsetMin = Vector2.zero; lblRt.offsetMax = Vector2.zero;
        lbl.alignment = TextAlignmentOptions.Center;

        return btn;
    }

    static Slider MakeSlider(string name, GameObject parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<RectTransform>();

        var bg = MakeRT("Background", go);
        var bgRt = bg.GetComponent<RectTransform>();
        FullStretch(bgRt);
        bg.AddComponent<Image>().color = new Color(1, 1, 1, 0.15f);

        var fillArea = MakeRT("Fill Area", go);
        var faRt = fillArea.GetComponent<RectTransform>();
        FullStretch(faRt);

        var fill = MakeRT("Fill", fillArea);
        var fillRt = fill.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = new Vector2(0.5f, 1);
        fillRt.offsetMin = Vector2.zero; fillRt.offsetMax = Vector2.zero;
        var fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(0.3f, 0.8f, 0.5f, 1f);

        var slider = go.AddComponent<Slider>();
        slider.fillRect = fillRt;
        slider.targetGraphic = fillImg;
        slider.minValue = 0;
        slider.maxValue = 1;
        slider.value = 0;
        slider.interactable = false;  // 表示専用
        return slider;
    }

    // ApplyButton/RetryButton/CancelButton を一列に配置するヘルパー
    static void PlaceButtonRow(Button btn, float anchorXOffset, float anchorY)
    {
        // anchorXOffset は -1, 0, +1 など(ボタン間隔の倍数)
        var rt = btn.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, anchorY);
        rt.anchorMax = new Vector2(0.5f, anchorY);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(180, 50);
        rt.anchoredPosition = new Vector2(anchorXOffset * 200f, 0);
    }

    static GameObject FindByNameIncludeInactive(string name)
    {
        var all = Resources.FindObjectsOfTypeAll<RectTransform>();
        foreach (var rt in all)
        {
            if (rt.gameObject.scene.IsValid() && rt.gameObject.name == name)
                return rt.gameObject;
        }
        return null;
    }
}
