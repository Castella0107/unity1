#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Title.unity をユーザー提供モック(KALPA風)準拠でフル再構築する Editor ヘルパー (2026-06-07 リデザイン)。
/// レイアウト: 左上=タイトルロゴ(円形エンブレム+タイトル) / 左下=縦メニュー5項目(選択中=拡大+エンブレム+説明文) /
/// 右上=プレイヤーチップ(名前+レーティング)。背景は BGA 未制作のため真っ暗。
/// 旧 WireTitleScene(カルーセル配線) は本ビルダーに置き換え。
/// </summary>
public static class BuildTitleScene
{
    const string ScenePath = "Assets/_Project/Scenes/Title.unity";

    static readonly Color Gold    = new Color(0.85f, 0.72f, 0.42f, 1f);   // モックの金アクセント
    static readonly Color GoldDim = new Color(0.85f, 0.72f, 0.42f, 0.7f);
    static readonly Color Faint   = new Color(1f, 1f, 1f, 0.10f);

    const int   MenuCount = 5;
    const float MenuX     = 90f;     // メニュー左端
    const float MenuTopY  = -560f;   // 1項目目の上端
    const float RowStep   = 90f;     // 項目間隔
    const float LabelX    = 78f;     // ラベルのインデント (アイコン分)

    [MenuItem("Tools/Build Title Scene")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── Camera (真っ暗背景 = BGA プレースホルダー) ───────────────────────
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.transform.position = new Vector3(0, 1, -10);
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
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

        // 背景 (真っ暗 — BGA 導入時にここを差し替える)
        var bgGO = Child("Background", ct);
        SR(bgGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        bgGO.AddComponent<Image>().color = Color.black;

        // ── 左上: タイトルロゴ ────────────────────────────────────────────────
        BuildLogo(ct);

        // ── 右上: プレイヤーチップ ────────────────────────────────────────────
        var (nameTMP, ratingTMP) = BuildPlayerChip(ct);

        // ── 左下: 縦メニュー ──────────────────────────────────────────────────
        var labels = new TextMeshProUGUI[MenuCount];
        var descs  = new TextMeshProUGUI[MenuCount];
        var icons  = new GameObject[MenuCount];
        var dots   = new GameObject[MenuCount];

        var menuGO = Child("Menu", ct);
        SR(menuGO, V(0,1), V(0,1), V(0,1), V(MenuX, MenuTopY), V(700, MenuCount * RowStep));

        for (int i = 0; i < MenuCount; i++)
        {
            float rowY = -i * RowStep;
            var rowGO = Child("Item" + i, menuGO.transform);
            SR(rowGO, V(0,1), V(0,1), V(0,1), V(0, rowY), V(700, 86));

            // 選択中エンブレム (◉ 金リング + ✦) — 初期は項目0のみ表示
            var iconGO = Child("Icon", rowGO.transform);
            SR(iconGO, V(0,1), V(0,1), V(.5f,.5f), V(30, -22), V(48,48));
            var ring = iconGO.AddComponent<Image>();
            ring.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            ring.color  = Gold; ring.raycastTarget = false;
            var innerGO = Child("Inner", iconGO.transform);
            SR(innerGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(-8,-8));
            var inner = innerGO.AddComponent<Image>();
            inner.sprite = ring.sprite; inner.color = Color.black; inner.raycastTarget = false;
            var starGO = Child("Star", iconGO.transform);
            SR(starGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
            var starTMP = T(starGO, "✦", 22, Gold, TextAlignmentOptions.Center, FontStyles.Bold);
            starTMP.raycastTarget = false;
            iconGO.SetActive(i == 0);
            icons[i] = iconGO;

            // 非選択ドット
            var dotGO = Child("Dot", rowGO.transform);
            SR(dotGO, V(0,1), V(0,1), V(.5f,.5f), V(30, -22), V(12,12));
            var dot = dotGO.AddComponent<Image>();
            dot.sprite = ring.sprite; dot.color = GoldDim; dot.raycastTarget = false;
            dotGO.SetActive(i != 0);
            dots[i] = dotGO;

            // ラベル (テキストは TitleController が Start で設定)
            var lblGO = Child("Label", rowGO.transform);
            SR(lblGO, V(0,1), V(0,1), V(0,1), V(LabelX, 0), V(560, 44));
            labels[i] = T(lblGO, "", 26, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            labels[i].raycastTarget = false;

            // 説明文 (選択中のみ表示・半透明黒帯)
            var descBgGO = Child("DescBg", rowGO.transform);
            SR(descBgGO, V(0,1), V(0,1), V(0,1), V(LabelX + 2, -46), V(520, 34));
            var descBg = descBgGO.AddComponent<Image>();
            descBg.color = new Color(0f, 0f, 0f, 0.6f); descBg.raycastTarget = false;
            var descGO = Child("Text", descBgGO.transform);
            SR(descGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(-20,0));
            descs[i] = T(descGO, "", 15, new Color(1f,1f,1f,.85f), TextAlignmentOptions.MidlineLeft);
            descs[i].raycastTarget = false;
            descBgGO.SetActive(i == 0);
        }

        // ── History 子メニュー (親 History 行の右に展開・初期非表示) ──────────
        // History は _menus の index 3 (FreePlay/Online/Config/History/Exit)
        const int HistoryIndex = 3;
        const int ChildCount   = 2;
        var childLabels = new TextMeshProUGUI[ChildCount];
        var childDescs  = new TextMeshProUGUI[ChildCount];

        var childRoot = Child("HistoryChildren", menuGO.transform);
        SR(childRoot, V(0,1), V(0,1), V(0,1), V(380, -HistoryIndex * RowStep), V(560, ChildCount * 76));

        for (int i = 0; i < ChildCount; i++)
        {
            float cy = -i * 76f;
            var rowGO = Child("Child" + i, childRoot.transform);
            SR(rowGO, V(0,1), V(0,1), V(0,1), V(0, cy), V(560, 72));

            // 接続線 (親→子のガイド、モックの下線に相当)
            var lineGO = Child("Line", rowGO.transform);
            SR(lineGO, V(0,1), V(0,1), V(0,.5f), V(-26, -20), V(20, 2));
            var lineImg = lineGO.AddComponent<Image>(); lineImg.color = GoldDim; lineImg.raycastTarget = false;

            var lblGO = Child("Label", rowGO.transform);
            SR(lblGO, V(0,1), V(0,1), V(0,1), V(4, 0), V(480, 36));
            childLabels[i] = T(lblGO, "", 22, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            childLabels[i].raycastTarget = false;

            var descBgGO = Child("DescBg", rowGO.transform);
            SR(descBgGO, V(0,1), V(0,1), V(0,1), V(6, -38), V(520, 30));
            var descBg = descBgGO.AddComponent<Image>();
            descBg.color = new Color(0f, 0f, 0f, 0.6f); descBg.raycastTarget = false;
            var descGO = Child("Text", descBgGO.transform);
            SR(descGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(-16,0));
            childDescs[i] = T(descGO, "", 14, new Color(1f,1f,1f,.85f), TextAlignmentOptions.MidlineLeft);
            childDescs[i].raycastTarget = false;
            descBgGO.SetActive(i == 0);
        }
        childRoot.SetActive(false);

        // ── コントローラー配線 ────────────────────────────────────────────────
        var ctrlGO = new GameObject("TitleController");
        var ctrl   = ctrlGO.AddComponent<TitleController>();
        var so     = new SerializedObject(ctrl);
        SetArr(so, "_itemLabels", labels);
        SetArr(so, "_itemDescs",  descs);
        SetArr(so, "_itemIcons",  icons);
        SetArr(so, "_itemDots",   dots);
        SetRef(so, "_childRoot",  childRoot);
        SetArr(so, "_childLabels", childLabels);
        SetArr(so, "_childDescs",  childDescs);
        SetRef(so, "_playerNameText",   nameTMP);
        SetRef(so, "_playerRatingText", ratingTMP);

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

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();
        Debug.Log("[BuildTitleScene] Done → " + ScenePath);
    }

    // ── 左上ロゴ: 円形エンブレム + タイトル + サブタイトル ────────────────────
    static void BuildLogo(Transform ct)
    {
        var logoGO = Child("TitleLogo", ct);
        SR(logoGO, V(0,1), V(0,1), V(0,1), V(60,-44), V(640,150));

        // 円形エンブレム (Knob スプライトで金リング)
        var emblemGO = Child("Emblem", logoGO.transform);
        SR(emblemGO, V(0,1), V(0,1), V(.5f,.5f), V(64,-66), V(118,118));
        var ring = emblemGO.AddComponent<Image>();
        ring.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        ring.color  = Gold; ring.raycastTarget = false;
        var innerGO = Child("Inner", emblemGO.transform);
        SR(innerGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(-10,-10));
        var inner = innerGO.AddComponent<Image>();
        inner.sprite = ring.sprite; inner.color = Color.black; inner.raycastTarget = false;
        var markGO = Child("Mark", emblemGO.transform);
        SR(markGO, V(0,0), V(1,1), V(.5f,.5f), V(0,0), V(0,0));
        var markTMP = T(markGO, "△▽✕", 26, Gold, TextAlignmentOptions.Center, FontStyles.Bold);
        markTMP.raycastTarget = false;

        // タイトル (仮名称 — 正式タイトル決定時に差し替え)
        var titleGO = Child("TitleText", logoGO.transform);
        SR(titleGO, V(0,1), V(0,1), V(0,1), V(140,-26), V(500,60));
        T(titleGO, "MUSICGAME", 48, Color.white, TextAlignmentOptions.MidlineLeft,
          FontStyles.Bold | FontStyles.Italic);

        var subGO = Child("SubTitle", logoGO.transform);
        SR(subGO, V(0,1), V(0,1), V(0,1), V(144,-88), V(500,26));
        T(subGO, ": Rhythm Action — prototype", 17, GoldDim, TextAlignmentOptions.MidlineLeft, FontStyles.Italic);
    }

    // ── 右上プレイヤーチップ: 名前 + レーティング ─────────────────────────────
    static (TextMeshProUGUI, TextMeshProUGUI) BuildPlayerChip(Transform ct)
    {
        var chipGO = Child("PlayerChip", ct);
        SR(chipGO, V(1,1), V(1,1), V(1,1), V(-40,-24), V(360,64));
        chipGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        var avGO = Child("Avatar", chipGO.transform);
        SR(avGO, V(0,.5f), V(0,.5f), V(0,.5f), V(9,0), V(46,46));
        avGO.AddComponent<Image>().color = new Color(.25f,.25f,.32f,1f);

        var nameGO = Child("Name", chipGO.transform);
        SR(nameGO, V(0,.5f), V(1,1), V(0,1), V(66,-6), V(-76,28));
        var nameTMP = T(nameGO, "player", 20, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        nameTMP.overflowMode = TextOverflowModes.Ellipsis;

        var rateGO = Child("Rating", chipGO.transform);
        SR(rateGO, V(0,0), V(1,.5f), V(0,0), V(66,6), V(-76,22));
        var rateTMP = T(rateGO, "RATING ----", 14, Gold, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

        return (nameTMP, rateTMP);
    }

    // ── Micro helpers ─────────────────────────────────────────────────────────

    static void SetRef(SerializedObject so, string prop, Object value)
    {
        var p = so.FindProperty(prop);
        if (p == null) { Debug.LogWarning($"[BuildTitleScene] missing prop: {prop}"); return; }
        p.objectReferenceValue = value;
    }

    static void SetArr(SerializedObject so, string prop, Object[] values)
    {
        var p = so.FindProperty(prop);
        if (p == null) { Debug.LogWarning($"[BuildTitleScene] missing prop: {prop}"); return; }
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
