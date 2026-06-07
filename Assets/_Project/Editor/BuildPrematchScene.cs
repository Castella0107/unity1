#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// PVPPrematch.unity を READY 画面 (Go サーバー移行 M3) として再構築する Editor ヘルパー。
/// 旧 PvpDraftScreenController(Prematch) レイアウトを置き換える。
/// 中央: YOU(シアン) vs OPP(レッド) パネル+戦績 / レート変動予測 / READY 状態 / deadline タイマー / READY ボタン。
/// </summary>
public static class BuildPrematchScene
{
    const string ScenePath = "Assets/_Project/Scenes/PVPPrematch.unity";

    static readonly Color Cyan  = new Color(0.17f, 0.85f, 0.90f, 1f);   // 自分
    static readonly Color Red   = new Color(0.95f, 0.30f, 0.42f, 1f);   // 相手
    static readonly Color Gold  = new Color(0.97f, 0.78f, 0.25f, 1f);
    static readonly Color Faint = new Color(1f, 1f, 1f, .12f);
    static readonly Color Dim   = new Color(.72f, .72f, .78f);

    [MenuItem("Tools/PVP/Build Prematch (READY) Scene")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.transform.position = new Vector3(0, 1, -10);
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.04f, 0.02f, 0.07f);
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
        var ct = canvasGO.transform;

        var bgGO = Child("Background", ct);
        SR(bgGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        bgGO.AddComponent<Image>().color = new Color(0.04f, 0.02f, 0.07f, 1f);

        // ── ヘッダ ────────────────────────────────────────────────────────────
        var headGO = Child("Header", ct);
        SR(headGO, V(.5f,1), V(.5f,1), V(.5f,1), V(0,-60), V(900,60));
        T(headGO, "MATCH READY", 44, Color.white, TextAlignmentOptions.Center, FontStyles.Bold | FontStyles.Italic);

        var lineGO = Child("AccentLine", ct);
        SR(lineGO, V(.5f,1), V(.5f,1), V(.5f,1), V(0,-130), V(1200,2));
        var lineImg = lineGO.AddComponent<Image>(); lineImg.color = Cyan; lineImg.raycastTarget = false;

        // ── プレイヤーパネル (YOU vs OPP) ─────────────────────────────────────
        (TextMeshProUGUI, TextMeshProUGUI, TextMeshProUGUI) Panel(string name, float cx, Color accent, string title)
        {
            var pGO = Child(name, ct);
            SR(pGO, V(.5f,.5f), V(.5f,.5f), V(.5f,.5f), V(cx, 110), V(480, 300));
            pGO.AddComponent<Image>().color = new Color(0,0,0,.45f);

            var tagGO = Child("Tag", pGO.transform);
            SR(tagGO, V(0,1), V(1,1), V(.5f,1), V(0,0), V(0,40));
            tagGO.AddComponent<Image>().color = new Color(accent.r, accent.g, accent.b, .25f);
            var tagLbl = Child("Label", tagGO.transform);
            SR(tagLbl, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
            T(tagLbl, title, 20, accent, TextAlignmentOptions.Center, FontStyles.Bold);

            var nameGO = Child("Name", pGO.transform);
            SR(nameGO, V(0,1), V(1,1), V(.5f,1), V(0,-60), V(-40,52));
            var nameTMP = T(nameGO, "---", 34, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            nameTMP.overflowMode = TextOverflowModes.Ellipsis;

            var ratingGO = Child("Rating", pGO.transform);
            SR(ratingGO, V(0,1), V(1,1), V(.5f,1), V(0,-130), V(-40,36));
            var ratingTMP = T(ratingGO, "RATING ----", 24, Gold, TextAlignmentOptions.Center, FontStyles.Bold);

            var recGO = Child("Record", pGO.transform);
            SR(recGO, V(0,1), V(1,1), V(.5f,1), V(0,-180), V(-40,30));
            var recTMP = T(recGO, "-W - -L - -D", 18, Dim, TextAlignmentOptions.Center);

            return (nameTMP, ratingTMP, recTMP);
        }

        var (selfName, selfRating, selfRecord) = Panel("SelfPanel", -380, Cyan, "YOU");
        var (oppName,  oppRating,  oppRecord)  = Panel("OppPanel",   380, Red,  "OPPONENT");

        var vsGO = Child("VS", ct);
        SR(vsGO, V(.5f,.5f), V(.5f,.5f), V(.5f,.5f), V(0,110), V(160,80));
        T(vsGO, "VS", 56, new Color(1,1,1,.85f), TextAlignmentOptions.Center, FontStyles.Bold | FontStyles.Italic);

        // ── レート変動予測 ───────────────────────────────────────────────────
        var predLblGO = Child("PredictionLabel", ct);
        SR(predLblGO, V(.5f,.5f), V(.5f,.5f), V(.5f,.5f), V(0,-90), V(700,26));
        T(predLblGO, "― RATING PREDICTION ―", 15, Dim, TextAlignmentOptions.Center, FontStyles.Bold);

        var predGO  = Child("PredictionText", ct);
        SR(predGO, V(.5f,.5f), V(.5f,.5f), V(.5f,.5f), V(0,-122), V(800,34));
        var predTMP = T(predGO, "WIN --    DRAW --    LOSE --", 22, Gold, TextAlignmentOptions.Center, FontStyles.Bold);

        // ── READY 状態 / タイマー / ステータス ────────────────────────────────
        var readyStateGO  = Child("ReadyState", ct);
        SR(readyStateGO, V(.5f,.5f), V(.5f,.5f), V(.5f,.5f), V(0,-180), V(800,34));
        var readyStateTMP = T(readyStateGO, "", 22, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        readyStateTMP.richText = true;

        var timerGO  = Child("Timer", ct);
        SR(timerGO, V(.5f,1), V(.5f,1), V(.5f,1), V(0,-150), V(200,60));
        var timerTMP = T(timerGO, "", 42, Gold, TextAlignmentOptions.Center, FontStyles.Bold);

        var statusGO  = Child("Status", ct);
        SR(statusGO, V(.5f,0), V(.5f,0), V(.5f,0), V(0,160), V(1100,30));
        var statusTMP = T(statusGO, "", 17, Dim, TextAlignmentOptions.Center);

        // ── READY ボタン ─────────────────────────────────────────────────────
        var btnGO  = Child("ReadyButton", ct);
        SR(btnGO, V(.5f,0), V(.5f,0), V(.5f,0), V(0,70), V(420,70));
        var btnImg = btnGO.AddComponent<Image>(); btnImg.color = new Color(Cyan.r, Cyan.g, Cyan.b, .85f);
        var btn    = btnGO.AddComponent<Button>(); btn.targetGraphic = btnImg;
        var btnLbl = Child("Label", btnGO.transform);
        SR(btnLbl, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        var btnTMP = T(btnLbl, "READY  (Space)", 26, Color.black, TextAlignmentOptions.Center, FontStyles.Bold);
        btnTMP.raycastTarget = false;

        // ── コントローラー配線 ────────────────────────────────────────────────
        var ctrlGO = new GameObject("PvpPrematchController");
        var ctrl   = ctrlGO.AddComponent<RhythmGame.UI.Pvp.PvpPrematchController>();
        var so     = new SerializedObject(ctrl);
        SetRef(so, "_selfNameText",   selfName);
        SetRef(so, "_selfRatingText", selfRating);
        SetRef(so, "_selfRecordText", selfRecord);
        SetRef(so, "_oppNameText",    oppName);
        SetRef(so, "_oppRatingText",  oppRating);
        SetRef(so, "_oppRecordText",  oppRecord);
        SetRef(so, "_predictionText", predTMP);
        SetRef(so, "_readyStateText", readyStateTMP);
        SetRef(so, "_timerText",      timerTMP);
        SetRef(so, "_statusText",     statusTMP);
        SetRef(so, "_readyButton",    btn);
        so.ApplyModifiedProperties();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();
        Debug.Log("[BuildPrematchScene] Done → " + ScenePath);
    }

    // ── Micro helpers ─────────────────────────────────────────────────────────

    static void SetRef(SerializedObject so, string prop, Object value)
    {
        var p = so.FindProperty(prop);
        if (p == null) { Debug.LogWarning($"[BuildPrematchScene] missing prop: {prop}"); return; }
        p.objectReferenceValue = value;
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
