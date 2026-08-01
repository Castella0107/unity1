#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Go サーバー移行 M4 のシーン群を構築する Editor ヘルパー。
///   - PVPSongPick.unity → 統合ドラフト画面 (PvpDraftController、交互ターン制)
///   - PVPResult.unity   → 曲リザルト画面 (PvpSongResultV2Controller)
/// 旧 BuildPvpScenes の同名シーン内容を置き換える。
/// </summary>
public static class BuildGoDraftScenes
{
    static readonly Color Cyan  = new Color(0.17f, 0.85f, 0.90f, 1f);
    static readonly Color Red   = new Color(0.95f, 0.30f, 0.42f, 1f);
    static readonly Color Gold  = new Color(0.97f, 0.78f, 0.25f, 1f);
    static readonly Color Faint = new Color(1f, 1f, 1f, .12f);
    static readonly Color Dim   = new Color(.72f, .72f, .78f);
    static readonly Color BgCol = new Color(0.04f, 0.02f, 0.07f, 1f);

    [MenuItem("Tools/PVP/Build Go Draft + SongResult Scenes")]
    public static void BuildAll()
    {
        BuildDraftScene();
        BuildSongResultScene();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 統合ドラフト画面 (PVPSongPick.unity)
    // ═════════════════════════════════════════════════════════════════════════

    static void BuildDraftScene()
    {
        var (scene, ct) = NewUiScene("DraftScene");

        // ヘッダ
        var titleGO  = Child("PhaseTitle", ct);
        SR(titleGO, V(.5f,1), V(.5f,1), V(.5f,1), V(0,-50), V(1400,56));
        var titleTMP = T(titleGO, "DRAFT", 40, Color.white, TextAlignmentOptions.Center, FontStyles.Bold | FontStyles.Italic);

        var infoGO  = Child("Info", ct);
        SR(infoGO, V(.5f,1), V(.5f,1), V(.5f,1), V(0,-116), V(1400,32));
        var infoTMP = T(infoGO, "", 20, Dim, TextAlignmentOptions.Center);

        var timerGO  = Child("Timer", ct);
        SR(timerGO, V(1,1), V(1,1), V(1,1), V(-70,-40), V(160,70));
        var timerTMP = T(timerGO, "", 52, Gold, TextAlignmentOptions.Center, FontStyles.Bold);

        // 曲タイル 6 枚 (3列×2行)
        var tiles  = new Button[6];
        var labels = new TextMeshProUGUI[6];
        var bgs    = new Image[6];
        for (int i = 0; i < 6; i++)
        {
            int col = i % 3, row = i / 3;
            float x = -420 + col * 420;
            float y = 130 - row * 180;

            var tGO = Child("SongTile" + i, ct);
            SR(tGO, V(.5f,.5f), V(.5f,.5f), V(.5f,.5f), V(x, y), V(380, 150));
            var img = tGO.AddComponent<Image>(); img.color = Faint;
            var btn = tGO.AddComponent<Button>(); btn.targetGraphic = img;

            var lGO = Child("Label", tGO.transform);
            SR(lGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(-24,-16));
            var lbl = T(lGO, "", 24, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            lbl.raycastTarget = false;

            tiles[i] = btn; labels[i] = lbl; bgs[i] = img;
        }

        // 難易度ボタン 3 つ (EASY は廃止 2026-08-01。配列 index は難易度 ID のままスロット 0 を空ける)
        var diffBtns = new Button[4];
        var diffLbls = new TextMeshProUGUI[4];
        var diffBgs  = new Image[4];
        string[] dn = { "EASY", "NORMAL", "HARD", "EXTRA" };
        Color[]  dc = { new Color(.2f,.75f,.35f), new Color(.2f,.5f,.9f), new Color(.9f,.5f,.1f), new Color(.85f,.1f,.5f) };
        for (int i = 1; i < 4; i++)
        {
            float x = -200 + (i - 1) * 200;
            var dGO = Child("Diff" + dn[i], ct);
            SR(dGO, V(.5f,0), V(.5f,0), V(.5f,0), V(x, 180), V(184, 60));
            var img = dGO.AddComponent<Image>(); img.color = Faint;
            var btn = dGO.AddComponent<Button>(); btn.targetGraphic = img;
            var lGO = Child("Label", dGO.transform);
            SR(lGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
            var lbl = T(lGO, dn[i], 19, dc[i], TextAlignmentOptions.Center, FontStyles.Bold);
            lbl.raycastTarget = false;
            diffBtns[i] = btn; diffLbls[i] = lbl; diffBgs[i] = img;
        }

        // 決定ボタン
        var cGO  = Child("ConfirmButton", ct);
        SR(cGO, V(.5f,0), V(.5f,0), V(.5f,0), V(0, 80), V(460, 70));
        var cImg = cGO.AddComponent<Image>(); cImg.color = new Color(Cyan.r, Cyan.g, Cyan.b, .85f);
        var cBtn = cGO.AddComponent<Button>(); cBtn.targetGraphic = cImg;
        var clGO = Child("Label", cGO.transform);
        SR(clGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        var cLbl = T(clGO, "決定", 26, Color.black, TextAlignmentOptions.Center, FontStyles.Bold);
        cLbl.raycastTarget = false;

        // ステータス
        var stGO  = Child("Status", ct);
        SR(stGO, V(0,0), V(0,0), V(0,0), V(40, 36), V(800,28));
        var stTMP = T(stGO, "", 16, Dim, TextAlignmentOptions.MidlineLeft);

        // 配線
        var ctrlGO = new GameObject("PvpDraftController");
        var ctrl   = ctrlGO.AddComponent<RhythmGame.UI.Pvp.PvpDraftController>();
        var so     = new SerializedObject(ctrl);
        SetRef(so, "_phaseTitle", titleTMP);
        SetRef(so, "_timerText",  timerTMP);
        SetRef(so, "_infoText",   infoTMP);
        SetRef(so, "_statusText", stTMP);
        SetArr(so, "_songTiles",      tiles);
        SetArr(so, "_songTileLabels", labels);
        SetArr(so, "_songTileBgs",    bgs);
        SetArr(so, "_diffButtons", diffBtns);
        SetArr(so, "_diffLabels",  diffLbls);
        SetArr(so, "_diffBgs",     diffBgs);
        SetRef(so, "_confirmButton", cBtn);
        SetRef(so, "_confirmLabel",  cLbl);
        so.ApplyModifiedProperties();

        EditorSceneManager.SaveScene(scene, "Assets/_Project/Scenes/PVPSongPick.unity");
        Debug.Log("[BuildGoDraftScenes] Draft → PVPSongPick.unity");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 曲リザルト画面 (PVPResult.unity)
    // ═════════════════════════════════════════════════════════════════════════

    static void BuildSongResultScene()
    {
        var (scene, ct) = NewUiScene("SongResultScene");

        var titleGO  = Child("Title", ct);
        SR(titleGO, V(.5f,1), V(.5f,1), V(.5f,1), V(0,-70), V(900,60));
        var titleTMP = T(titleGO, "SONG RESULT", 44, Color.white, TextAlignmentOptions.Center, FontStyles.Bold | FontStyles.Italic);

        // YOU / OPP ポイント
        (TextMeshProUGUI pts, TextMeshProUGUI _) PtsPanel(string name, float cx, Color accent, string tag)
        {
            var pGO = Child(name, ct);
            SR(pGO, V(.5f,.5f), V(.5f,.5f), V(.5f,.5f), V(cx, 130), V(420, 200));
            pGO.AddComponent<Image>().color = new Color(0,0,0,.45f);

            var tagGO = Child("Tag", pGO.transform);
            SR(tagGO, V(0,1), V(1,1), V(.5f,1), V(0,-12), V(0,32));
            var tagTMP = T(tagGO, tag, 20, accent, TextAlignmentOptions.Center, FontStyles.Bold);

            var ptsGO = Child("Pts", pGO.transform);
            SR(ptsGO, V(0,0), V(1,1), V(.5f,.5f), V(0,-16), V(0,0));
            var ptsTMP = T(ptsGO, "-", 56, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            return (ptsTMP, tagTMP);
        }
        var (selfPts, _) = PtsPanel("SelfPts", -260, Cyan, "YOU");
        var (oppPts, _)  = PtsPanel("OppPts",   260, Red,  "OPPONENT");

        // セクター◆/◇/—
        var secGO  = Child("Sectors", ct);
        SR(secGO, V(.5f,.5f), V(.5f,.5f), V(.5f,.5f), V(0,-30), V(700,60));
        var secTMP = T(secGO, "", 44, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        secTMP.richText = true;

        // 累計 / クリンチ
        var cumGO  = Child("Cumulative", ct);
        SR(cumGO, V(.5f,.5f), V(.5f,.5f), V(.5f,.5f), V(0,-110), V(1000,34));
        var cumTMP = T(cumGO, "", 22, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

        var clinchGO  = Child("Clinch", ct);
        SR(clinchGO, V(.5f,.5f), V(.5f,.5f), V(.5f,.5f), V(0,-160), V(700,40));
        var clinchTMP = T(clinchGO, "", 28, Gold, TextAlignmentOptions.Center, FontStyles.Bold);

        var stGO  = Child("Status", ct);
        SR(stGO, V(.5f,0), V(.5f,0), V(.5f,0), V(0,170), V(1000,30));
        var stTMP = T(stGO, "", 18, Dim, TextAlignmentOptions.Center);

        // NEXT
        var nGO  = Child("NextButton", ct);
        SR(nGO, V(.5f,0), V(.5f,0), V(.5f,0), V(0,80), V(420,70));
        var nImg = nGO.AddComponent<Image>(); nImg.color = new Color(Cyan.r, Cyan.g, Cyan.b, .85f);
        var nBtn = nGO.AddComponent<Button>(); nBtn.targetGraphic = nImg;
        nBtn.interactable = false;
        var nlGO = Child("Label", nGO.transform);
        SR(nlGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        var nLbl = T(nlGO, "NEXT", 26, Color.black, TextAlignmentOptions.Center, FontStyles.Bold);
        nLbl.raycastTarget = false;

        var ctrlGO = new GameObject("PvpSongResultV2Controller");
        var ctrl   = ctrlGO.AddComponent<RhythmGame.UI.Pvp.PvpSongResultV2Controller>();
        var so     = new SerializedObject(ctrl);
        SetRef(so, "_titleText",      titleTMP);
        SetRef(so, "_selfPtsText",    selfPts);
        SetRef(so, "_oppPtsText",     oppPts);
        SetRef(so, "_sectorText",     secTMP);
        SetRef(so, "_cumulativeText", cumTMP);
        SetRef(so, "_clinchText",     clinchTMP);
        SetRef(so, "_statusText",     stTMP);
        SetRef(so, "_nextButton",     nBtn);
        SetRef(so, "_nextLabel",      nLbl);
        so.ApplyModifiedProperties();

        EditorSceneManager.SaveScene(scene, "Assets/_Project/Scenes/PVPResult.unity");
        Debug.Log("[BuildGoDraftScenes] SongResult → PVPResult.unity");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 共通 scene base
    // ═════════════════════════════════════════════════════════════════════════

    static (UnityEngine.SceneManagement.Scene, Transform) NewUiScene(string label)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.transform.position = new Vector3(0, 1, -10);
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = BgCol;
        camGO.AddComponent<AudioListener>();
        camGO.AddComponent<AudioListenerGuard>();

        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        esGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        esGO.AddComponent<EventSystemGuard>();

        var canvasGO = new GameObject("Canvas");
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight  = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        var bgGO = Child("Background", canvasGO.transform);
        SR(bgGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        bgGO.AddComponent<Image>().color = BgCol;

        return (scene, canvasGO.transform);
    }

    static void SetRef(SerializedObject so, string prop, Object value)
    {
        var p = so.FindProperty(prop);
        if (p == null) { Debug.LogWarning($"[BuildGoDraftScenes] missing prop: {prop}"); return; }
        p.objectReferenceValue = value;
    }

    static void SetArr(SerializedObject so, string prop, Object[] values)
    {
        var p = so.FindProperty(prop);
        if (p == null) { Debug.LogWarning($"[BuildGoDraftScenes] missing prop: {prop}"); return; }
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
}
#endif
