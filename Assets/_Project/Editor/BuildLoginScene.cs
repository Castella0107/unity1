#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Login.unity (起動時ログイン/新規登録、Go サーバー移行 M1) を構築する Editor ヘルパー。
/// 黒背景+中央パネル: EMAIL / PASSWORD / (表示名=REGISTER時) / ログイン / 新規登録へ / オフラインで続行。
/// </summary>
public static class BuildLoginScene
{
    const string ScenePath = "Assets/_Project/Scenes/Login.unity";

    static readonly Color Accent = new Color(0.42f, 0.32f, 0.85f, 1f);
    static readonly Color BoxBg  = new Color(0.086f, 0.106f, 0.18f, 1f);   // #161B2E
    static readonly Color Gold   = new Color(0.85f, 0.72f, 0.42f, 1f);
    static readonly Color Dim    = new Color(.72f, .72f, .78f);

    [MenuItem("Tools/Build Login Scene")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.transform.position = new Vector3(0, 1, -10);
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        camGO.AddComponent<AudioListener>();
        camGO.AddComponent<AudioListenerGuard>();   // _Persistent と重複時に無効化 (警告スパム防止)

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
        bgGO.AddComponent<Image>().color = Color.black;

        // ── タイトルロゴ (簡易版) ────────────────────────────────────────────
        var logoGO = Child("Logo", ct);
        SR(logoGO, V(.5f,1), V(.5f,1), V(.5f,1), V(0,-120), V(800,70));
        T(logoGO, "MUSICGAME", 52, Color.white, TextAlignmentOptions.Center, FontStyles.Bold | FontStyles.Italic);

        // ── 中央パネル ───────────────────────────────────────────────────────
        var panelGO = Child("Panel", ct);
        SR(panelGO, V(.5f,.5f), V(.5f,.5f), V(.5f,.5f), V(0,-30), V(640,560));
        panelGO.AddComponent<Image>().color = new Color(1f,1f,1f,0.06f);
        var pt = panelGO.transform;

        var headGO = Child("Header", pt);
        SR(headGO, V(0,1), V(1,1), V(.5f,1), V(0,-18), V(0,40));
        T(headGO, "SIGN IN", 28, Gold, TextAlignmentOptions.Center, FontStyles.Bold);

        float y = -86f;

        (TMP_InputField, GameObject) InputRow(string label, bool password)
        {
            var rowGO = Child("Row_" + label, pt);
            SR(rowGO, V(0,1), V(1,1), V(.5f,1), V(0,y), V(-60,72));
            var lGO = Child("Label", rowGO.transform);
            SR(lGO, V(0,1), V(1,1), V(0,1), V(4,0), V(0,24));
            T(lGO, label, 15, Dim, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

            var inGO = Child("Input", rowGO.transform);
            SR(inGO, V(0,0), V(1,0), V(.5f,0), V(0,0), V(0,44));
            var img = inGO.AddComponent<Image>(); img.color = BoxBg;
            var input = inGO.AddComponent<TMP_InputField>();
            input.targetGraphic = img;

            var areaGO = Child("TextArea", inGO.transform);
            var areaRT = SR(areaGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(-24,-10));
            areaGO.AddComponent<RectMask2D>();
            var phGO = Child("Placeholder", areaGO.transform);
            SR(phGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
            var ph = T(phGO, label.ToLowerInvariant(), 17, new Color(1,1,1,.3f), TextAlignmentOptions.MidlineLeft);
            var txGO = Child("Text", areaGO.transform);
            SR(txGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
            var tx = T(txGO, "", 17, Color.white, TextAlignmentOptions.MidlineLeft);

            input.textViewport  = areaRT;
            input.textComponent = tx;
            input.placeholder   = ph;
            if (password) input.contentType = TMP_InputField.ContentType.Password;

            y -= 84f;
            return (input, rowGO);
        }

        var (emailInput, _)        = InputRow("EMAIL", false);
        var (passwordInput, _)     = InputRow("PASSWORD", true);
        var (displayNameInput, displayNameRow) = InputRow("DISPLAY NAME (新規登録時のみ)", false);

        // ステータス
        var statusGO = Child("Status", pt);
        SR(statusGO, V(0,1), V(1,1), V(.5f,1), V(0,y), V(-60,46));
        var statusTMP = T(statusGO, "", 15, new Color(1,1,1,.8f), TextAlignmentOptions.TopLeft);
        y -= 56f;

        // 実行ボタン (LOGIN / 登録する)
        var (submitBtn, submitLbl) = MakeButton(pt, "ログイン", V(.5f,1), V(0,y), V(520,52), Accent, 20);
        y -= 64f;

        // モード切替 / オフライン (横並び)
        var (modeBtn, modeLbl) = MakeButton(pt, "新規登録へ", V(.5f,1), V(-135,y), V(250,44),
            new Color(1,1,1,.12f), 16);
        var (offlineBtn, _)    = MakeButton(pt, "オフラインで続行", V(.5f,1), V(135,y), V(250,44),
            new Color(1,1,1,.12f), 16);

        // 接続先表示 (パネル下部)
        var serverGO = Child("ServerText", pt);
        SR(serverGO, V(0,0), V(1,0), V(.5f,0), V(0,10), V(-60,24));
        var serverTMP = T(serverGO, "http://localhost:8080", 13, new Color(1,1,1,.35f), TextAlignmentOptions.Center);

        // ── コントローラー配線 ────────────────────────────────────────────────
        var ctrlGO = new GameObject("LoginController");
        var ctrl   = ctrlGO.AddComponent<LoginController>();
        var so     = new SerializedObject(ctrl);
        SetRef(so, "_emailInput",       emailInput);
        SetRef(so, "_passwordInput",    passwordInput);
        SetRef(so, "_displayNameInput", displayNameInput);
        SetRef(so, "_displayNameRow",   displayNameRow);
        SetRef(so, "_submitButton",     submitBtn);
        SetRef(so, "_submitLabel",      submitLbl);
        SetRef(so, "_modeToggleButton", modeBtn);
        SetRef(so, "_modeToggleLabel",  modeLbl);
        SetRef(so, "_offlineButton",    offlineBtn);
        SetRef(so, "_statusText",       statusTMP);
        SetRef(so, "_serverText",       serverTMP);
        so.ApplyModifiedProperties();

        // 表示名行は初期非表示 (REGISTER モードで表示)
        displayNameRow.SetActive(false);

        EditorSceneManager.SaveScene(scene, ScenePath);
        RegisterInBuildSettings(ScenePath);
        AssetDatabase.Refresh();
        Debug.Log("[BuildLoginScene] Done → " + ScenePath);
    }

    static (Button, TextMeshProUGUI) MakeButton(Transform parent, string text,
        Vector2 anchor, Vector2 pos, Vector2 size, Color color, float fontSize)
    {
        var go = Child("Btn_" + text, parent);
        SR(go, anchor, anchor, V(.5f, 1), pos, size);
        var img = go.AddComponent<Image>(); img.color = color;
        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        var lblGO = Child("Label", go.transform);
        SR(lblGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        var lbl = T(lblGO, text, fontSize, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        lbl.raycastTarget = false;
        return (btn, lbl);
    }

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
        if (p == null) { Debug.LogWarning($"[BuildLoginScene] missing prop: {prop}"); return; }
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
