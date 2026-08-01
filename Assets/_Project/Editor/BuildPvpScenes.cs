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
/// Go フロー対応の PVP 周辺シーンを programmatically 構築する。
/// このビルダーが所有するのは Matchmaking / PVPMatchEnd / PVPLobby の 3 画面。
/// (PVPPrematch=BuildPrematchScene, PVPSongPick/PVPResult=BuildGoDraftScenes が所有)
/// 各 Controller は OnGUI フォールバックを持つので最小限の Camera/EventSystem/Canvas/Controller のみ baked-in する。
/// </summary>
public static class BuildPvpScenes
{
    /// <summary>Matchmaking / PVPMatchEnd / PVPLobby をプログラム的に構築する。</summary>
    [MenuItem("Tools/PVP/Build PVP Scenes")]
    public static void BuildAll()
    {
        BuildMatchmakingScene();
        BuildPvpMatchEndScene();

        // オンラインロビー (対戦待合, フロー ②)。Title Online → ここ → START → Matchmaking。
        BuildLobbyScene("Assets/_Project/Scenes/PVPLobby.unity",
            new Color(0.10f, 0.04f, 0.06f));

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

    // オンラインロビー (対戦待合, フロー ②) を生成する。3パネル + START バー。
    // 右パネルの TOTAL MATCH/MATCH WIN/WIN RATIO のみ実データ結線、その他(ティア/LP/TOP5/
    // YOUR RANKING/難易度別表)は K ドメインのため baked プレースホルダー文言。
    static void BuildLobbyScene(string scenePath, Color bg)
    {
        var scene = NewEmptyScene();
        BuildBaseObjects(scene, bg);
        var canvasGO = GameObject.Find("Canvas");

        Color gold = new Color(0.97f, 0.78f, 0.25f, 1f);
        Color dim  = new Color(1, 1, 1, 0.6f);
        Color panel= new Color(1, 1, 1, 0.06f);

        // ── ヘッダー: ONLINE / LADDER MATCH + LOBBY ──────────────────
        var kicker = MakeTMP("Kicker", canvasGO, 26, "ONLINE   ·   LADDER MATCH");
        SetAnchored(kicker, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(530, -64), new Vector2(700, 36)); // pivot中心のため x=左端180+幅/2
        kicker.alignment = TextAlignmentOptions.Left;
        kicker.color = dim;
        kicker.characterSpacing = 6f;

        var lobbyTitle = MakeTMP("LobbyTitle", canvasGO, 72, "LOBBY");
        SetAnchored(lobbyTitle, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(530, -130), new Vector2(700, 90)); // 同上 (旧値180では左に340px見切れ)
        lobbyTitle.alignment = TextAlignmentOptions.Left;
        lobbyTitle.color = gold;
        lobbyTitle.fontStyle = FontStyles.Bold;

        // ── 左パネル: SEASON TOP5 + YOUR RANKING (placeholder) ──────
        var leftBG = MakeImage("LeftPanel", canvasGO, panel);
        SetRect(leftBG.rectTransform, Center, Center, new Vector2(-620, 40), new Vector2(560, 600));

        var seasonTMP = MakeTMP("SeasonText", canvasGO, 26, "SEASON --");
        SetAnchored(seasonTMP, Center, Center, new Vector2(-760, 290), new Vector2(280, 40));
        seasonTMP.alignment = TextAlignmentOptions.Left;
        seasonTMP.color = gold;
        seasonTMP.fontStyle = FontStyles.Bold;

        var top5Head = MakeTMP("Top5Head", canvasGO, 22, "TOP 5");
        SetAnchored(top5Head, Center, Center, new Vector2(-500, 290), new Vector2(160, 40));
        top5Head.alignment = TextAlignmentOptions.Right;
        top5Head.color = dim;

        // ランキング5行 (K ラダー API 待ちのプレースホルダー)
        var rankRows = MakeTMP("RankRows", canvasGO, 26,
            "1.   ----------\n\n2.   ----------\n\n3.   ----------\n\n4.   ----------\n\n5.   ----------");
        SetAnchored(rankRows, Center, Center, new Vector2(-620, 70), new Vector2(520, 380));
        rankRows.alignment = TextAlignmentOptions.TopLeft;
        rankRows.color = dim;

        var yourRankHead = MakeTMP("YourRankHead", canvasGO, 22, "YOUR RANKING");
        SetAnchored(yourRankHead, Center, Center, new Vector2(-700, -160), new Vector2(400, 36));
        yourRankHead.alignment = TextAlignmentOptions.Left;
        yourRankHead.color = gold;
        yourRankHead.characterSpacing = 4f;

        var yourRankTMP = MakeTMP("YourRankingText", canvasGO, 28, "-.--%  OF TOP");
        SetAnchored(yourRankTMP, Center, Center, new Vector2(-700, -210), new Vector2(400, 44));
        yourRankTMP.alignment = TextAlignmentOptions.Left;
        yourRankTMP.color = Color.white;

        // ── 中央: 大ティアバッジ (placeholder) ──────────────────────
        var centerTier = MakeTMP("CenterTierText", canvasGO, 56, "UNRANKED");
        SetAnchored(centerTier, Center, Center, new Vector2(0, 40), new Vector2(620, 90));
        centerTier.alignment = TextAlignmentOptions.Center;
        centerTier.fontStyle = FontStyles.Bold;
        centerTier.characterSpacing = 4f;

        // ── 右パネル: LADDER TIER + 実データ3項目 + 難易度表(placeholder) ─
        var rightBG = MakeImage("RightPanel", canvasGO, panel);
        SetRect(rightBG.rectTransform, Center, Center, new Vector2(620, 40), new Vector2(620, 600));

        var ladderHead = MakeTMP("LadderHead", canvasGO, 22, "LADDER TIER");
        SetAnchored(ladderHead, Center, Center, new Vector2(620, 290), new Vector2(560, 36));
        ladderHead.alignment = TextAlignmentOptions.Left;
        ladderHead.color = gold;
        ladderHead.characterSpacing = 4f;

        var ladderTier = MakeTMP("LadderTierText", canvasGO, 44, "UNRANKED");
        SetAnchored(ladderTier, Center, Center, new Vector2(620, 240), new Vector2(560, 60));
        ladderTier.alignment = TextAlignmentOptions.Left;
        ladderTier.fontStyle = FontStyles.Bold;

        // TOTAL MATCH (real)
        var tmHead = MakeTMP("TotalMatchHead", canvasGO, 20, "TOTAL MATCH");
        SetAnchored(tmHead, Center, Center, new Vector2(490, 150), new Vector2(280, 30));
        tmHead.alignment = TextAlignmentOptions.Left; tmHead.color = gold;
        var tmVal = MakeTMP("TotalMatchText", canvasGO, 48, "0");
        SetAnchored(tmVal, Center, Center, new Vector2(490, 100), new Vector2(280, 60));
        tmVal.alignment = TextAlignmentOptions.Left; tmVal.fontStyle = FontStyles.Bold;

        // MATCH WIN (real)
        var mwHead = MakeTMP("MatchWinHead", canvasGO, 20, "MATCH WIN");
        SetAnchored(mwHead, Center, Center, new Vector2(760, 150), new Vector2(280, 30));
        mwHead.alignment = TextAlignmentOptions.Left; mwHead.color = gold;
        var mwVal = MakeTMP("MatchWinText", canvasGO, 48, "0");
        SetAnchored(mwVal, Center, Center, new Vector2(720, 100), new Vector2(120, 60));
        mwVal.alignment = TextAlignmentOptions.Left; mwVal.fontStyle = FontStyles.Bold;
        var wrVal = MakeTMP("WinRatioText", canvasGO, 26, "0.00%");
        SetAnchored(wrVal, Center, Center, new Vector2(850, 108), new Vector2(180, 40));
        wrVal.alignment = TextAlignmentOptions.Left; wrVal.color = dim;

        // 難易度別スタッツ表 (placeholder; PVP難易度別集計は未追跡)
        var statsTable = MakeTMP("StatsTable", canvasGO, 22,
            "          ROUND   WIN   PERFECT   COMBO    RATE\n" +
            "TOTAL       0       0       0        0      0.00%\n" +
            "normal      0       0       0        0      0.00%\n" +
            "hard        0       0       0        0      0.00%\n" +
            "extra       0       0       0        0      0.00%");
        SetAnchored(statsTable, Center, Center, new Vector2(620, -110), new Vector2(580, 240));
        statsTable.alignment = TextAlignmentOptions.TopLeft;
        statsTable.color = dim;
        statsTable.enableWordWrapping = false;

        // ── START バー ───────────────────────────────────────────────
        var startGO = MakeButton("StartButton", canvasGO, "");
        var sRT = startGO.GetComponent<RectTransform>();
        sRT.anchorMin = new Vector2(0f, 0f); sRT.anchorMax = new Vector2(1f, 0f); sRT.pivot = new Vector2(0.5f, 0f);
        sRT.anchoredPosition = new Vector2(0, 60); sRT.sizeDelta = new Vector2(-200, 150);
        var startImg = startGO.GetComponent<Image>();
        if (startImg != null) startImg.color = new Color(0.12f, 0.18f, 0.30f, 0.55f);
        // ボタン内ラベルを START / PRESS SPACE に差し替え
        var startLbl = startGO.GetComponentInChildren<TextMeshProUGUI>();
        if (startLbl != null)
        {
            startLbl.text = "START";
            startLbl.fontSize = 64;
            startLbl.fontStyle = FontStyles.Bold;
            startLbl.alignment = TextAlignmentOptions.Center;
        }
        var pressF5 = MakeTMP("PressF5", canvasGO, 24, "PRESS  SPACE"); // 実装/ヒントとも Space (F5 は誤記)
        SetAnchored(pressF5, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 80), new Vector2(400, 36));
        pressF5.alignment = TextAlignmentOptions.Center;
        pressF5.color = dim;
        pressF5.characterSpacing = 6f;
        pressF5.raycastTarget = false;

        var backGO = MakeButton("BackButton", canvasGO, "< MENU");
        var bRT = backGO.GetComponent<RectTransform>();
        bRT.anchorMin = bRT.anchorMax = new Vector2(1f, 0f); bRT.pivot = new Vector2(1f, 0f);
        bRT.anchoredPosition = new Vector2(-40, 20); bRT.sizeDelta = new Vector2(220, 56);

        // ── Controller 配線 ─────────────────────────────────────────
        var ctrlGO = new GameObject("PvpLobbyController");
        var ctrl = ctrlGO.AddComponent<PvpLobbyController>();
        var so = new SerializedObject(ctrl);
        so.FindProperty("_ladderTierText").objectReferenceValue  = ladderTier;
        so.FindProperty("_totalMatchText").objectReferenceValue  = tmVal;
        so.FindProperty("_matchWinText").objectReferenceValue    = mwVal;
        so.FindProperty("_winRatioText").objectReferenceValue    = wrVal;
        so.FindProperty("_centerTierText").objectReferenceValue  = centerTier;
        so.FindProperty("_seasonText").objectReferenceValue      = seasonTMP;
        so.FindProperty("_yourRankingText").objectReferenceValue = yourRankTMP;
        so.FindProperty("_startButton").objectReferenceValue     = startGO.GetComponent<Button>();
        so.FindProperty("_backButton").objectReferenceValue      = backGO.GetComponent<Button>();
        so.ApplyModifiedPropertiesWithoutUndo();

        SaveAndRegister(scene, scenePath);
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
