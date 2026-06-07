#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 楽曲別ランキング (SongRanking) シーンを構築する Editor ヘルパー (画面遷移図 2026-06-07 改訂 ④)。
/// ヘッダ(曲名/アーティスト/難易度) + ランキング20行(baked-in) + YOUR RANK フッタ + BACK。
/// SongSelect の R キーで遷移、ESC で SongSelect へ復帰。
/// </summary>
public static class BuildSongRankingScene
{
    const string ScenePath = "Assets/_Project/Scenes/SongRanking.unity";
    const int    RowCount  = 20;   // SongRankingController.FetchLimit と揃える

    const float Pad     = 30f;
    const float TopBarH = 90f;
    const float ListW   = 1400f;

    static readonly Color Cyan  = new Color(0.31f, 0.76f, 0.97f);
    static readonly Color Gold  = new Color(0.97f, 0.78f, 0.25f);
    static readonly Color Dim   = new Color(.7f, .7f, .7f);
    static readonly Color Faint = new Color(1f, 1f, 1f, .12f);

    [MenuItem("Tools/Build SongRanking Scene")]
    public static void Build()
    {
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
        esGO.AddComponent<EventSystemGuard>();

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
        // TOP BAR — ロゴ / BACK / 曲情報
        // ════════════════════════════════════════════════════════════════════
        var topGO = Child("TopBar", ct);
        SR(topGO, V(0,1), V(1,1), V(.5f,1), V(0,0), V(0,TopBarH));

        var logoGO = Child("ModeLogo", topGO.transform);
        SR(logoGO, V(0,0), V(0,1), V(0,.5f), V(Pad,0), V(360,0));
        T(logoGO, "♛  SONG RANKING", 30, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

        var backBtnGO  = Child("BackButton", topGO.transform);
        SR(backBtnGO, V(0,0), V(0,1), V(0,.5f), V(Pad+360,0), V(110,-30));
        var backBtnImg = backBtnGO.AddComponent<Image>(); backBtnImg.color = Faint;
        var backBtn    = backBtnGO.AddComponent<Button>(); backBtn.targetGraphic = backBtnImg;
        var backLbl    = Child("Label", backBtnGO.transform);
        SR(backLbl, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        T(backLbl, "< BACK", 16, Color.white, TextAlignmentOptions.Center);

        // 曲情報 (右)
        var titleGO  = Child("SongTitle", topGO.transform);
        SR(titleGO, V(1,.5f), V(1,1), V(1,1), V(-Pad,-8), V(700,34));
        var titleTMP = T(titleGO, "---", 26, Color.white, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
        titleTMP.overflowMode = TextOverflowModes.Ellipsis;

        var artistGO  = Child("SongArtist", topGO.transform);
        SR(artistGO, V(1,0), V(1,.5f), V(1,0), V(-Pad-120,8), V(580,24));
        var artistTMP = T(artistGO, "---", 16, Dim, TextAlignmentOptions.MidlineRight);
        artistTMP.overflowMode = TextOverflowModes.Ellipsis;

        var diffGO  = Child("DiffChip", topGO.transform);
        SR(diffGO, V(1,0), V(1,0), V(1,0), V(-Pad,10), V(110,26));
        diffGO.AddComponent<Image>().color = new Color(.85f,.1f,.5f);
        var diffLbl = Child("Label", diffGO.transform);
        SR(diffLbl, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        var diffTMP = T(diffLbl, "EXTRA", 15, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

        // アクセント線
        var lineGO = Child("AccentLine", ct);
        SR(lineGO, V(0,1), V(1,1), V(.5f,1), V(0,-TopBarH), V(-Pad*2, 2));
        var lineImg = lineGO.AddComponent<Image>(); lineImg.color = Cyan; lineImg.raycastTarget = false;

        // ════════════════════════════════════════════════════════════════════
        // COLUMN HEADER
        // ════════════════════════════════════════════════════════════════════
        const float HeaderY = TopBarH + 14f;
        var colGO = Child("ColumnHeader", ct);
        SR(colGO, V(.5f,1), V(.5f,1), V(.5f,1), V(0,-HeaderY), V(ListW, 30));
        ColLabel(colGO.transform, "RANK",   10,  70, TextAlignmentOptions.Center);
        ColLabel(colGO.transform, "",       85,  70, TextAlignmentOptions.Center);   // badge col
        ColLabel(colGO.transform, "PLAYER", 170, 440, TextAlignmentOptions.MidlineLeft);
        ColLabel(colGO.transform, "SCORE",  620, 240, TextAlignmentOptions.MidlineRight);
        ColLabel(colGO.transform, "GRADE",  880, 100, TextAlignmentOptions.Center);
        ColLabel(colGO.transform, "COMBO",  990, 150, TextAlignmentOptions.MidlineRight);

        // ════════════════════════════════════════════════════════════════════
        // SCROLL VIEW + 20 ROWS (baked-in)
        // ════════════════════════════════════════════════════════════════════
        const float ScrollTop = HeaderY + 36f;
        const float FooterH   = 70f;

        var svGO = Child("ScrollView", ct);
        SR(svGO, V(.5f,0), V(.5f,1), V(.5f,.5f),
           V(0, (FooterH - ScrollTop) / 2f), V(ListW, -(ScrollTop + FooterH)));
        var scrollRect = svGO.AddComponent<ScrollRect>();
        scrollRect.horizontal = false; scrollRect.scrollSensitivity = 30f;

        var vpGO = Child("Viewport", svGO.transform);
        SR(vpGO, V(0,0), V(1,1), V(0,0), V(0,0), V(0,0));
        vpGO.AddComponent<RectMask2D>();   // Mask+Sprite=None の罠回避

        var contentGO = Child("Content", vpGO.transform);
        var contentRT = SR(contentGO, V(0,1), V(1,1), V(.5f,1), V(0,0), V(0,0));
        var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
        vlg.childControlHeight = false; vlg.childForceExpandHeight = false;
        vlg.childControlWidth  = true;  vlg.childForceExpandWidth  = true;
        vlg.spacing = 4; vlg.padding = new RectOffset(0,0,4,4);
        var csf = contentGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = vpGO.GetComponent<RectTransform>();
        scrollRect.content  = contentRT;

        var rows = new RankingRowView[RowCount];
        for (int i = 0; i < RowCount; i++)
            rows[i] = MakeRow(contentGO.transform, i);

        // ステータス (取得中... / 記録なし) — 中央
        var statusGO  = Child("StatusText", ct);
        SR(statusGO, V(.5f,.5f), V(.5f,.5f), V(.5f,.5f), V(0,0), V(700,40));
        var statusTMP = T(statusGO, "取得中...", 22, Dim, TextAlignmentOptions.Center);

        // ════════════════════════════════════════════════════════════════════
        // FOOTER — YOUR RANK
        // ════════════════════════════════════════════════════════════════════
        var footGO = Child("Footer", ct);
        SR(footGO, V(.5f,0), V(.5f,0), V(.5f,0), V(0,12), V(ListW, 50));
        footGO.AddComponent<Image>().color = new Color(0,0,0,.45f);
        var personalGO  = Child("PersonalText", footGO.transform);
        SR(personalGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(-28,0));
        var personalTMP = T(personalGO, "", 18, Gold, TextAlignmentOptions.Center, FontStyles.Bold);

        // ════════════════════════════════════════════════════════════════════
        // CONTROLLER WIRING
        // ════════════════════════════════════════════════════════════════════
        var ctrlGO = new GameObject("SongRankingController");
        var ctrl   = ctrlGO.AddComponent<SongRankingController>();
        var so     = new SerializedObject(ctrl);
        SetRef(so, "_titleText",    titleTMP);
        SetRef(so, "_artistText",   artistTMP);
        SetRef(so, "_diffText",     diffTMP);
        SetArr(so, "_rows",         rows);
        SetRef(so, "_statusText",   statusTMP);
        SetRef(so, "_scrollRect",   scrollRect);
        SetRef(so, "_personalText", personalTMP);
        SetRef(so, "_backButton",   backBtn);
        so.ApplyModifiedProperties();

        // Save + Build Settings 登録
        EditorSceneManager.SaveScene(scene, ScenePath);
        RegisterInBuildSettings(ScenePath);
        AssetDatabase.Refresh();
        Debug.Log("[BuildSongRankingScene] Done → " + ScenePath);
    }

    // ── Row builder ───────────────────────────────────────────────────────────

    static RankingRowView MakeRow(Transform parent, int index)
    {
        var rowGO = Child("Row" + index, parent);
        var rowRT = rowGO.GetComponent<RectTransform>();
        rowRT.sizeDelta = V(ListW, 40);
        var le = rowGO.AddComponent<LayoutElement>();
        le.minHeight = 40; le.preferredHeight = 40;

        var bgImg = rowGO.AddComponent<Image>();
        bgImg.color = Faint; bgImg.raycastTarget = false;

        var rankTMP  = Cell(rowGO.transform, "Rank",  10,  70,  TextAlignmentOptions.Center,       18, Color.white, FontStyles.Bold);
        var badgeTMP = Cell(rowGO.transform, "Badge", 85,  70,  TextAlignmentOptions.Center,       14, Cyan,        FontStyles.Bold);
        var nameTMP  = Cell(rowGO.transform, "Name",  170, 440, TextAlignmentOptions.MidlineLeft,  18, Color.white, FontStyles.Bold);
        nameTMP.overflowMode = TextOverflowModes.Ellipsis;
        var scoreTMP = Cell(rowGO.transform, "Score", 620, 240, TextAlignmentOptions.MidlineRight, 18, Color.white, FontStyles.Bold);
        var gradeTMP = Cell(rowGO.transform, "Grade", 880, 100, TextAlignmentOptions.Center,       18, Gold,        FontStyles.Bold);
        var comboTMP = Cell(rowGO.transform, "Combo", 990, 150, TextAlignmentOptions.MidlineRight, 16, Dim);

        var view = rowGO.AddComponent<RankingRowView>();
        var so   = new SerializedObject(view);
        SetRef(so, "_background", bgImg);
        SetRef(so, "_rankText",   rankTMP);
        SetRef(so, "_nameText",   nameTMP);
        SetRef(so, "_scoreText",  scoreTMP);
        SetRef(so, "_gradeText",  gradeTMP);
        SetRef(so, "_comboText",  comboTMP);
        SetRef(so, "_badgeText",  badgeTMP);
        so.ApplyModifiedProperties();
        return view;
    }

    static TextMeshProUGUI Cell(Transform row, string name, float x, float w,
        TextAlignmentOptions align, float size, Color color, FontStyles style = FontStyles.Normal)
    {
        var go = Child(name, row);
        SR(go, V(0,0), V(0,1), V(0,.5f), V(x,0), V(w,0));
        var t = T(go, "", size, color, align, style);
        t.raycastTarget = false;
        return t;
    }

    static void ColLabel(Transform parent, string text, float x, float w, TextAlignmentOptions align)
    {
        var go = Child("Col_" + (string.IsNullOrEmpty(text) ? "Badge" : text), parent);
        SR(go, V(0,0), V(0,1), V(0,.5f), V(x,0), V(w,0));
        var t = T(go, text, 13, Dim, align, FontStyles.Bold);
        t.raycastTarget = false;
    }

    static void RegisterInBuildSettings(string scenePath)
    {
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(
            EditorBuildSettings.scenes);
        if (scenes.Exists(s => s.path == scenePath)) return;
        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log("[BuildSongRankingScene] Registered in Build Settings: " + scenePath);
    }

    // ── Micro helpers (SongSelectSceneBuilder と同方式) ──────────────────────

    static void SetRef(SerializedObject so, string prop, Object value)
    {
        var p = so.FindProperty(prop);
        if (p == null) { Debug.LogWarning($"[BuildSongRankingScene] missing prop: {prop}"); return; }
        p.objectReferenceValue = value;
    }

    static void SetArr(SerializedObject so, string prop, Object[] values)
    {
        var p = so.FindProperty(prop);
        if (p == null) { Debug.LogWarning($"[BuildSongRankingScene] missing prop: {prop}"); return; }
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
}
#endif
