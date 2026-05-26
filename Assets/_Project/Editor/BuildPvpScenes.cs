using System.IO;
using RhythmGame.UI.Pvp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Matchmaking.unity と PvpMatchEnd.unity を programmatically 構築する。
/// 各 Controller は OnGUI フォールバックを持つので最小限の Camera/EventSystem/Canvas/Controller のみ baked-in する。
/// </summary>
public static class BuildPvpScenes
{
    /// <summary>Matchmaking.unity と PvpMatchEnd.unity をプログラム的に構築する。</summary>
    [MenuItem("Tools/PVP/Build PVP Scenes")]
    public static void BuildAll()
    {
        BuildMatchmakingScene();
        BuildPvpMatchEndScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[BuildPvpScenes] Done.");
    }

    static void BuildMatchmakingScene()
    {
        var scene = NewEmptyScene();
        BuildBaseObjects(scene, new Color(0.03f, 0.04f, 0.08f));
        var canvasGO = GameObject.Find("Canvas");

        // Header
        var titleTMP = MakeTMP("Title", canvasGO, 64, "ONLINE MATCHMAKING");
        SetAnchored(titleTMP, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -100), new Vector2(1200, 80));
        titleTMP.alignment = TextAlignmentOptions.Center;

        // Status box
        var statusTMP = MakeTMP("StatusText", canvasGO, 32, "Connecting...");
        SetAnchored(statusTMP, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 50), new Vector2(1200, 60));
        statusTMP.alignment = TextAlignmentOptions.Center;

        var opponentTMP = MakeTMP("OpponentText", canvasGO, 24, "");
        SetAnchored(opponentTMP, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(1200, 100));
        opponentTMP.alignment = TextAlignmentOptions.Center;
        opponentTMP.color = new Color(1, 1, 1, 0.75f);

        // Cancel button
        var cancelBtnGO = MakeButton("CancelButton", canvasGO, "CANCEL");
        var cancelRT = cancelBtnGO.GetComponent<RectTransform>();
        cancelRT.anchorMin = cancelRT.anchorMax = new Vector2(0.5f, 0f);
        cancelRT.pivot = new Vector2(0.5f, 0f);
        cancelRT.anchoredPosition = new Vector2(0, 100);
        cancelRT.sizeDelta = new Vector2(320, 70);
        var cancelBtn = cancelBtnGO.GetComponent<Button>();

        // Controller
        var ctrlGO = new GameObject("MatchmakingController");
        var ctrl = ctrlGO.AddComponent<MatchmakingController>();
        var so = new SerializedObject(ctrl);
        so.FindProperty("_statusText")  .objectReferenceValue = statusTMP;
        so.FindProperty("_opponentText").objectReferenceValue = opponentTMP;
        so.FindProperty("_cancelButton").objectReferenceValue = cancelBtn;
        so.ApplyModifiedPropertiesWithoutUndo();

        SaveAndRegister(scene, "Assets/_Project/Scenes/Matchmaking.unity");
    }

    static void BuildPvpMatchEndScene()
    {
        var scene = NewEmptyScene();
        BuildBaseObjects(scene, new Color(0.04f, 0.04f, 0.08f));
        var canvasGO = GameObject.Find("Canvas");

        // Header (verdict)
        var headerTMP = MakeTMP("ResultHeader", canvasGO, 72, "RESULT");
        SetAnchored(headerTMP, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -120), new Vector2(1400, 100));
        headerTMP.alignment = TextAlignmentOptions.Center;

        // Score line
        var scoreTMP = MakeTMP("ScoreText", canvasGO, 40, "0  -  0");
        SetAnchored(scoreTMP, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 140), new Vector2(1200, 70));
        scoreTMP.alignment = TextAlignmentOptions.Center;

        // Per-song breakdown (difficulty + multiplier + per-song points). Shows how the
        // difficulty multiplier weighted each song's contribution to the weighted total above.
        var breakdownTMP = MakeTMP("BreakdownText", canvasGO, 26, "");
        SetAnchored(breakdownTMP, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(1200, 140));
        breakdownTMP.alignment = TextAlignmentOptions.Center;
        breakdownTMP.color = new Color(1, 1, 1, 0.9f);

        // Rating block
        var ratingTMP = MakeTMP("RatingText", canvasGO, 24, "");
        SetAnchored(ratingTMP, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -180), new Vector2(1000, 120));
        ratingTMP.alignment = TextAlignmentOptions.Center;
        ratingTMP.color = new Color(1, 1, 1, 0.85f);

        // Back button
        var backBtnGO = MakeButton("BackToTitleButton", canvasGO, "BACK TO TITLE");
        var backRT = backBtnGO.GetComponent<RectTransform>();
        backRT.anchorMin = backRT.anchorMax = new Vector2(0.5f, 0f);
        backRT.pivot = new Vector2(0.5f, 0f);
        backRT.anchoredPosition = new Vector2(0, 100);
        backRT.sizeDelta = new Vector2(380, 70);
        var backBtn = backBtnGO.GetComponent<Button>();

        var ctrlGO = new GameObject("PvpMatchEndController");
        var ctrl = ctrlGO.AddComponent<PvpMatchEndController>();
        var so = new SerializedObject(ctrl);
        so.FindProperty("_resultHeaderText") .objectReferenceValue = headerTMP;
        so.FindProperty("_scoreText")        .objectReferenceValue = scoreTMP;
        so.FindProperty("_breakdownText")    .objectReferenceValue = breakdownTMP;
        so.FindProperty("_ratingText")       .objectReferenceValue = ratingTMP;
        so.FindProperty("_backToTitleButton").objectReferenceValue = backBtn;
        so.ApplyModifiedPropertiesWithoutUndo();

        SaveAndRegister(scene, "Assets/_Project/Scenes/PVPMatchEnd.unity");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static Scene NewEmptyScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        EditorSceneManager.SetActiveScene(scene);
        return scene;
    }

    static void BuildBaseObjects(Scene scene, Color clearColor)
    {
        // Camera
        var camGO = new GameObject("Main Camera");
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = clearColor;
        cam.orthographic = true;
        camGO.AddComponent<AudioListener>();
        camGO.AddComponent<AudioListenerGuard>();   // 重複検知ガード
        camGO.tag = "MainCamera";

        // EventSystem
        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        esGO.AddComponent<InputSystemUIInputModule>();
        esGO.AddComponent<EventSystemGuard>();

        // Canvas
        var canvasGO = new GameObject("Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Background
        var bgGO = MakeRT("Background", canvasGO);
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(clearColor.r, clearColor.g, clearColor.b, 0.7f);
        bgImg.raycastTarget = false;
        FullStretch(bgGO.GetComponent<RectTransform>());
    }

    static void SaveAndRegister(Scene scene, string scenePath)
    {
        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log("[BuildPvpScenes] Saved: " + scenePath);

        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(
            EditorBuildSettings.scenes);
        if (!scenes.Exists(s => s.path == scenePath))
        {
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[BuildPvpScenes] Added to Build Settings: " + scenePath);
        }
    }

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
        go.AddComponent<RectTransform>();
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = Color.white;
        return tmp;
    }

    static void SetAnchored(TextMeshProUGUI tmp, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
    {
        var rt = tmp.GetComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static GameObject MakeButton(string name, GameObject parent, string label)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<RectTransform>();
        var img = go.AddComponent<Image>();
        img.color = new Color(1, 1, 1, 0.15f);
        go.AddComponent<Button>();
        var t = MakeTMP("Label", go, 26, label);
        var trt = t.GetComponent<RectTransform>();
        FullStretch(trt);
        t.alignment = TextAlignmentOptions.Center;
        return go;
    }
}
