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

        // 正規 PVP フロー 3 画面 (実ドラフト)。Prematch → SongPick(PICK) → BanPhase(BAN) → 本戦。
        BuildDraftScene("Assets/_Project/Scenes/PVPPrematch.unity",
            PvpDraftScreenController.Phase.Prematch, new Color(0.05f, 0.05f, 0.10f));
        BuildDraftScene("Assets/_Project/Scenes/PVPSongPick.unity",
            PvpDraftScreenController.Phase.SongPick, new Color(0.05f, 0.07f, 0.10f));
        BuildDraftScene("Assets/_Project/Scenes/PVPBanPhase.unity",
            PvpDraftScreenController.Phase.BanPhase, new Color(0.08f, 0.05f, 0.10f));

        // PVPResult はフロー未配線 (Result/PVPMatchEnd 流用) のためプレースホルダーのまま。
        BuildPlaceholderScene("Assets/_Project/Scenes/PVPResult.unity", "PVP RESULT",
            "PVP match result (currently reuses Result / PVPMatchEnd)", SceneId.Title,
            new Color(0.05f, 0.06f, 0.11f));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[BuildPvpScenes] Done.");
    }

    static void BuildMatchmakingScene()
    {
        var scene = NewEmptyScene();
        BuildBaseObjects(scene, new Color(0.03f, 0.04f, 0.08f));
        var canvasGO = GameObject.Find("Canvas");

        // DJMAX 風配色: 自分=シアン / 相手=レッド (History の勝敗色と統一)
        Color cyan = new Color(0.17f, 0.85f, 0.90f, 1f);
        Color red  = new Color(0.95f, 0.30f, 0.42f, 1f);
        Color dim  = new Color(1, 1, 1, 0.55f);

        // ── ヘッダー ──────────────────────────────────────────────
        var titleTMP = MakeTMP("Title", canvasGO, 60, "ONLINE MATCH");
        SetAnchored(titleTMP, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -80), new Vector2(1400, 80));
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.characterSpacing = 8f;

        var accent = MakeImage("HeaderAccent", canvasGO, cyan);
        SetRect(accent.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -140), new Vector2(560, 4));

        // ── 中央 VS 構成: YOU パネル / VS / OPPONENT パネル ─────────
        const float panelW = 480f, panelH = 340f, panelY = 70f, panelX = 340f;

        // 左 (YOU)
        var youPanel = MakeImage("YouPanel", canvasGO, new Color(0.10f, 0.30f, 0.42f, 0.55f));
        SetRect(youPanel.rectTransform, Center, Center, new Vector2(-panelX, panelY), new Vector2(panelW, panelH));
        var youStrip = MakeImage("YouStrip", canvasGO, cyan);
        SetRect(youStrip.rectTransform, Center, Center, new Vector2(-panelX, panelY + panelH / 2 - 3), new Vector2(panelW, 6));

        var youLabel = MakeTMP("YouLabel", canvasGO, 28, "YOU");
        SetAnchored(youLabel, Center, Center, new Vector2(-panelX, panelY + 120), new Vector2(panelW - 40, 40));
        youLabel.alignment = TextAlignmentOptions.Center;
        youLabel.color = cyan;
        youLabel.fontStyle = FontStyles.Bold;
        youLabel.characterSpacing = 6f;

        var youNameTMP = MakeTMP("YouNameText", canvasGO, 44, "YOU");
        SetAnchored(youNameTMP, Center, Center, new Vector2(-panelX, panelY), new Vector2(panelW - 30, 80));
        youNameTMP.alignment = TextAlignmentOptions.Center;
        youNameTMP.overflowMode = TextOverflowModes.Ellipsis;

        // 右 (OPPONENT)
        var oppPanel = MakeImage("OpponentPanel", canvasGO, new Color(0.40f, 0.12f, 0.20f, 0.55f));
        SetRect(oppPanel.rectTransform, Center, Center, new Vector2(panelX, panelY), new Vector2(panelW, panelH));
        var oppStrip = MakeImage("OpponentStrip", canvasGO, red);
        SetRect(oppStrip.rectTransform, Center, Center, new Vector2(panelX, panelY + panelH / 2 - 3), new Vector2(panelW, 6));

        var oppLabel = MakeTMP("OpponentLabel", canvasGO, 28, "OPPONENT");
        SetAnchored(oppLabel, Center, Center, new Vector2(panelX, panelY + 120), new Vector2(panelW - 40, 40));
        oppLabel.alignment = TextAlignmentOptions.Center;
        oppLabel.color = red;
        oppLabel.fontStyle = FontStyles.Bold;
        oppLabel.characterSpacing = 6f;

        var oppNameTMP = MakeTMP("OpponentNameText", canvasGO, 44, "???");
        SetAnchored(oppNameTMP, Center, Center, new Vector2(panelX, panelY), new Vector2(panelW - 30, 80));
        oppNameTMP.alignment = TextAlignmentOptions.Center;
        oppNameTMP.overflowMode = TextOverflowModes.Ellipsis;

        // VS
        var vsTMP = MakeTMP("VS", canvasGO, 84, "VS");
        SetAnchored(vsTMP, Center, Center, new Vector2(0, panelY), new Vector2(220, 120));
        vsTMP.alignment = TextAlignmentOptions.Center;
        vsTMP.fontStyle = FontStyles.Bold | FontStyles.Italic;

        // ── ステータス / タイマー / 楽曲 ─────────────────────────
        var statusTMP = MakeTMP("StatusText", canvasGO, 30, "Connecting...");
        SetAnchored(statusTMP, Center, Center, new Vector2(0, -150), new Vector2(1200, 50));
        statusTMP.alignment = TextAlignmentOptions.Center;
        statusTMP.color = dim;

        var timerTMP = MakeTMP("TimerText", canvasGO, 46, "00:00");
        SetAnchored(timerTMP, Center, Center, new Vector2(0, -210), new Vector2(400, 60));
        timerTMP.alignment = TextAlignmentOptions.Center;
        timerTMP.color = cyan;
        timerTMP.fontStyle = FontStyles.Bold;

        var songsTMP = MakeTMP("SongsText", canvasGO, 24, "");
        SetAnchored(songsTMP, Center, Center, new Vector2(0, -280), new Vector2(1400, 50));
        songsTMP.alignment = TextAlignmentOptions.Center;
        songsTMP.color = new Color(1, 1, 1, 0.8f);

        // ── Cancel ───────────────────────────────────────────────
        var cancelBtnGO = MakeButton("CancelButton", canvasGO, "CANCEL");
        var cancelRT = cancelBtnGO.GetComponent<RectTransform>();
        cancelRT.anchorMin = cancelRT.anchorMax = new Vector2(0.5f, 0f);
        cancelRT.pivot = new Vector2(0.5f, 0f);
        cancelRT.anchoredPosition = new Vector2(0, 90);
        cancelRT.sizeDelta = new Vector2(340, 72);
        var cancelBtn = cancelBtnGO.GetComponent<Button>();

        // ── Controller 配線 ──────────────────────────────────────
        var ctrlGO = new GameObject("MatchmakingController");
        var ctrl = ctrlGO.AddComponent<MatchmakingController>();
        var so = new SerializedObject(ctrl);
        so.FindProperty("_statusText")      .objectReferenceValue = statusTMP;
        so.FindProperty("_youNameText")     .objectReferenceValue = youNameTMP;
        so.FindProperty("_opponentNameText").objectReferenceValue = oppNameTMP;
        so.FindProperty("_timerText")       .objectReferenceValue = timerTMP;
        so.FindProperty("_songsText")       .objectReferenceValue = songsTMP;
        so.FindProperty("_cancelButton")    .objectReferenceValue = cancelBtn;
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

    // 仮 PVP 画面を1枚生成する。タイトル + 説明 + NEXT/BACK ボタンのみ。
    static void BuildPlaceholderScene(string scenePath, string titleText, string descText,
                                      SceneId nextScene, Color bg)
    {
        var scene = NewEmptyScene();
        BuildBaseObjects(scene, bg);
        var canvasGO = GameObject.Find("Canvas");

        Color cyan = new Color(0.17f, 0.85f, 0.90f, 1f);

        var titleTMP = MakeTMP("Title", canvasGO, 60, titleText);
        SetAnchored(titleTMP, Center, Center, new Vector2(0, 150), new Vector2(1500, 90));
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.characterSpacing = 6f;

        var tagTMP = MakeTMP("PlaceholderTag", canvasGO, 26, "PLACEHOLDER");
        SetAnchored(tagTMP, Center, Center, new Vector2(0, 80), new Vector2(1200, 40));
        tagTMP.alignment = TextAlignmentOptions.Center;
        tagTMP.color = cyan;
        tagTMP.characterSpacing = 8f;

        var descTMP = MakeTMP("DescText", canvasGO, 26, descText);
        SetAnchored(descTMP, Center, Center, new Vector2(0, 0), new Vector2(1300, 100));
        descTMP.alignment = TextAlignmentOptions.Center;
        descTMP.color = new Color(1, 1, 1, 0.7f);

        var backGO = MakeButton("BackButton", canvasGO, "< TITLE");
        var bRT = backGO.GetComponent<RectTransform>();
        bRT.anchorMin = bRT.anchorMax = new Vector2(0.5f, 0f);
        bRT.pivot = new Vector2(0.5f, 0f);
        bRT.anchoredPosition = new Vector2(-180, 120);
        bRT.sizeDelta = new Vector2(300, 72);

        var nextGO = MakeButton("NextButton", canvasGO, "NEXT >");
        var nRT = nextGO.GetComponent<RectTransform>();
        nRT.anchorMin = nRT.anchorMax = new Vector2(0.5f, 0f);
        nRT.pivot = new Vector2(0.5f, 0f);
        nRT.anchoredPosition = new Vector2(180, 120);
        nRT.sizeDelta = new Vector2(300, 72);

        var ctrlGO = new GameObject("PvpPlaceholderController");
        var ctrl = ctrlGO.AddComponent<PvpPlaceholderController>();
        var so = new SerializedObject(ctrl);
        so.FindProperty("_screenTitle").stringValue        = titleText;
        so.FindProperty("_nextScene").enumValueIndex       = (int)nextScene;
        so.FindProperty("_nextButton").objectReferenceValue = nextGO.GetComponent<Button>();
        so.FindProperty("_backButton").objectReferenceValue = backGO.GetComponent<Button>();
        so.ApplyModifiedPropertiesWithoutUndo();

        SaveAndRegister(scene, scenePath);
    }

    // 正規 PVP フロー画面 (Prematch/SongPick/BanPhase) を 1 枚生成する。
    // 実ドラフト UI: SongPick=20曲グリッド / BanPhase=3カード / Prematch=導入。各タイルは
    // DraftTileView を baked-in 結線 (ランタイム生成しない → [[feedback_unityRuntimeUiInLayoutGroup]])。
    // 文言・曲・ジャケットは PvpDraftScreenController が実行時に流し込む。
    // ※ 各要素の anchored 座標は概算。Editor で目視調整前提 (グリッドが画面中央を大きく占める)。
    static void BuildDraftScene(string scenePath, PvpDraftScreenController.Phase phase, Color bg)
    {
        var scene = NewEmptyScene();
        BuildBaseObjects(scene, bg);
        var canvasGO = GameObject.Find("Canvas");

        Color cyan = new Color(0.17f, 0.85f, 0.90f, 1f);
        Color red  = new Color(0.95f, 0.30f, 0.42f, 1f);
        Color dim  = new Color(1, 1, 1, 0.7f);

        // ── ヘッダー + アクセント線 ──────────────────────────────
        var headerTMP = MakeTMP("Header", canvasGO, 58, "MATCH READY");
        SetAnchored(headerTMP, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -64), new Vector2(1500, 84));
        headerTMP.alignment = TextAlignmentOptions.Center;
        headerTMP.fontStyle = FontStyles.Bold;
        headerTMP.characterSpacing = 6f;

        var accent = MakeImage("HeaderAccent", canvasGO, cyan);
        SetRect(accent.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -118), new Vector2(560, 4));

        // ── YOU / VS / OPPONENT ─────────────────────────────────
        var youTMP = MakeTMP("YouNameText", canvasGO, 38, "YOU");
        SetAnchored(youTMP, Center, Center, new Vector2(-360, 400), new Vector2(560, 64));
        youTMP.alignment = TextAlignmentOptions.Center;
        youTMP.color = cyan;
        youTMP.fontStyle = FontStyles.Bold;
        youTMP.overflowMode = TextOverflowModes.Ellipsis;

        var vsTMP = MakeTMP("VS", canvasGO, 50, "VS");
        SetAnchored(vsTMP, Center, Center, new Vector2(0, 400), new Vector2(160, 64));
        vsTMP.alignment = TextAlignmentOptions.Center;
        vsTMP.fontStyle = FontStyles.Bold | FontStyles.Italic;

        var oppTMP = MakeTMP("OpponentNameText", canvasGO, 38, "???");
        SetAnchored(oppTMP, Center, Center, new Vector2(360, 400), new Vector2(560, 64));
        oppTMP.alignment = TextAlignmentOptions.Center;
        oppTMP.color = red;
        oppTMP.fontStyle = FontStyles.Bold;
        oppTMP.overflowMode = TextOverflowModes.Ellipsis;

        // ── info (指示) / status / timer ────────────────────────
        var infoTMP = MakeTMP("InfoText", canvasGO, 24, "");
        SetAnchored(infoTMP, Center, Center, new Vector2(0, 348), new Vector2(1400, 40));
        infoTMP.alignment = TextAlignmentOptions.Center;
        infoTMP.color = dim;

        var statusTMP = MakeTMP("StatusText", canvasGO, 30, "");
        SetAnchored(statusTMP, Center, Center, new Vector2(0, 300), new Vector2(1200, 42));
        statusTMP.alignment = TextAlignmentOptions.Center;
        statusTMP.color = cyan;
        statusTMP.fontStyle = FontStyles.Bold;

        var timerTMP = MakeTMP("TimerText", canvasGO, 40, "");
        SetAnchored(timerTMP, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-150, -90), new Vector2(240, 56));
        timerTMP.alignment = TextAlignmentOptions.Center;
        timerTMP.fontStyle = FontStyles.Bold;

        // ── reveal (開示) / songs (候補・確定ラインナップ) ─────────
        var revealTMP = MakeTMP("RevealText", canvasGO, 30, "");
        SetAnchored(revealTMP, Center, Center, new Vector2(0, -352), new Vector2(1500, 46));
        revealTMP.alignment = TextAlignmentOptions.Center;
        revealTMP.fontStyle = FontStyles.Bold;

        var songsTMP = MakeTMP("SongsText", canvasGO, 26, "");
        SetAnchored(songsTMP, Center, Center, new Vector2(0, -420), new Vector2(1400, 110));
        songsTMP.alignment = TextAlignmentOptions.Center;
        songsTMP.color = new Color(1, 1, 1, 0.85f);

        // ── タイルグリッド (phase 別) ───────────────────────────
        var tiles = new System.Collections.Generic.List<DraftTileView>();
        if (phase == PvpDraftScreenController.Phase.SongPick)
        {
            var grid = MakeGrid("PoolGrid", canvasGO, new Vector2(0, -36),
                new Vector2(772, 608), new Vector2(140, 140), new Vector2(12, 12), 5);
            for (int i = 0; i < 20; i++) tiles.Add(MakeDraftTile(grid, 18, 15));
        }
        else if (phase == PvpDraftScreenController.Phase.BanPhase)
        {
            var grid = MakeGrid("CandidateGrid", canvasGO, new Vector2(0, 20),
                new Vector2(960, 340), new Vector2(280, 320), new Vector2(40, 0), 3);
            for (int i = 0; i < 3; i++) tiles.Add(MakeDraftTile(grid, 26, 22));
        }

        // ── Primary (LOCK IN / START / TO SONG PICK) / Cancel ────
        var primaryGO = MakeButton("PrimaryButton", canvasGO, "NEXT >");
        var pRT = primaryGO.GetComponent<RectTransform>();
        pRT.anchorMin = pRT.anchorMax = new Vector2(0.5f, 0f);
        pRT.pivot = new Vector2(0.5f, 0f);
        pRT.anchoredPosition = new Vector2(200, 56);
        pRT.sizeDelta = new Vector2(360, 76);
        var primaryLabel = primaryGO.GetComponentInChildren<TextMeshProUGUI>();

        var cancelGO = MakeButton("CancelButton", canvasGO, "CANCEL");
        var cRT = cancelGO.GetComponent<RectTransform>();
        cRT.anchorMin = cRT.anchorMax = new Vector2(0.5f, 0f);
        cRT.pivot = new Vector2(0.5f, 0f);
        cRT.anchoredPosition = new Vector2(-200, 56);
        cRT.sizeDelta = new Vector2(360, 76);

        // ── Controller 配線 ─────────────────────────────────────
        var ctrlGO = new GameObject("PvpDraftScreenController");
        var ctrl = ctrlGO.AddComponent<PvpDraftScreenController>();
        var so = new SerializedObject(ctrl);
        so.FindProperty("_phase").enumValueIndex            = (int)phase;
        so.FindProperty("_headerText").objectReferenceValue    = headerTMP;
        so.FindProperty("_youNameText").objectReferenceValue   = youTMP;
        so.FindProperty("_oppNameText").objectReferenceValue   = oppTMP;
        so.FindProperty("_infoText").objectReferenceValue      = infoTMP;
        so.FindProperty("_statusText").objectReferenceValue    = statusTMP;
        so.FindProperty("_timerText").objectReferenceValue     = timerTMP;
        so.FindProperty("_revealText").objectReferenceValue    = revealTMP;
        so.FindProperty("_songsText").objectReferenceValue     = songsTMP;
        so.FindProperty("_primaryLabel").objectReferenceValue  = primaryLabel;
        so.FindProperty("_primaryButton").objectReferenceValue = primaryGO.GetComponent<Button>();
        so.FindProperty("_cancelButton").objectReferenceValue  = cancelGO.GetComponent<Button>();

        var tilesProp = so.FindProperty("_tiles");
        tilesProp.arraySize = tiles.Count;
        for (int i = 0; i < tiles.Count; i++)
            tilesProp.GetArrayElementAtIndex(i).objectReferenceValue = tiles[i];

        so.ApplyModifiedPropertiesWithoutUndo();

        SaveAndRegister(scene, scenePath);
    }

    // GridLayoutGroup コンテナを生成する (タイルは baked-in で子に追加)。
    static GameObject MakeGrid(string name, GameObject parent, Vector2 pos, Vector2 size,
                               Vector2 cell, Vector2 spacing, int cols)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = Center;
        rt.pivot = Center;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var grid = go.AddComponent<GridLayoutGroup>();
        grid.cellSize        = cell;
        grid.spacing         = spacing;
        grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = cols;
        grid.childAlignment  = TextAnchor.MiddleCenter;
        return go;
    }

    // ドラフトの 1 タイルを生成し DraftTileView を結線して返す。
    // 構成(背面→前面): 選択枠(大きめ) / 背景(Buttonターゲット, raycast) / ジャケット / 暗転 / ラベル(下) / タグ(上)。
    static DraftTileView MakeDraftTile(GameObject grid, int labelSize, int tagSize)
    {
        var go = new GameObject("Tile");
        go.transform.SetParent(grid.transform, false);
        go.AddComponent<RectTransform>();

        // 選択枠 (背面・一回り大きい cyan)。選択時のみ enabled。
        var frame = MakeImageRay("SelFrame", go, new Color(0.17f, 0.85f, 0.90f, 0.95f), false);
        StretchInset(frame.rectTransform, -6);
        frame.enabled = false;

        // 背景 (Button のターゲット, クリックを受ける)
        var bgImg = MakeImageRay("BG", go, new Color(1, 1, 1, 0.12f), true);
        FullStretch(bgImg.rectTransform);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = bgImg;

        // ジャケット (sprite 未取得時は disabled)
        var jacket = MakeImageRay("Jacket", go, Color.white, false);
        StretchInset(jacket.rectTransform, 5);
        jacket.preserveAspect = true;
        jacket.enabled = false;

        // 暗転 (BAN / 非選択) 既定 disabled
        var dimImg = MakeImageRay("Dim", go, new Color(0, 0, 0, 0.62f), false);
        FullStretch(dimImg.rectTransform);
        dimImg.enabled = false;

        // ラベル (下部)
        var label = MakeTMP("Label", go, labelSize, "");
        var lrt = label.rectTransform;
        lrt.anchorMin = new Vector2(0, 0); lrt.anchorMax = new Vector2(1, 0); lrt.pivot = new Vector2(0.5f, 0);
        lrt.anchoredPosition = new Vector2(0, 4); lrt.sizeDelta = new Vector2(-8, 38);
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;

        // タグ (上部) 既定 disabled
        var tag = MakeTMP("Tag", go, tagSize, "");
        var trt = tag.rectTransform;
        trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1); trt.pivot = new Vector2(0.5f, 1);
        trt.anchoredPosition = new Vector2(0, -4); trt.sizeDelta = new Vector2(-8, 30);
        tag.alignment = TextAlignmentOptions.Center;
        tag.fontStyle = FontStyles.Bold;
        tag.raycastTarget = false;
        tag.enabled = false;

        var view = go.AddComponent<DraftTileView>();
        var so = new SerializedObject(view);
        so.FindProperty("_button").objectReferenceValue         = btn;
        so.FindProperty("_jacket").objectReferenceValue         = jacket;
        so.FindProperty("_label").objectReferenceValue          = label;
        so.FindProperty("_selectionFrame").objectReferenceValue = frame;
        so.FindProperty("_dim").objectReferenceValue            = dimImg;
        so.FindProperty("_tag").objectReferenceValue            = tag;
        so.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

    static Image MakeImage(string name, GameObject parent, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<RectTransform>();
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;   // 視覚専用 (クリックは Button のみに通す)
        return img;
    }

    // raycastTarget を明示指定できる Image 生成 (タイル背景はクリックを受けるため true)。
    static Image MakeImageRay(string name, GameObject parent, Color color, bool raycast)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<RectTransform>();
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    // 親に対し full-stretch しつつ四辺を inset px だけ内側(正)/外側(負)へ。
    static void StretchInset(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    static void SetRect(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.pivot = Center;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static Scene NewEmptyScene()
    {
        // Single モードで毎回作り直す。Additive だと前のシーンが開いたまま蓄積し、
        // 既存の対象シーンを開いている状態で再実行すると「同じパスを上書き不可」
        // 「未保存の untitled シーンがあるため additive 生成不可」で落ちる。
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
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
