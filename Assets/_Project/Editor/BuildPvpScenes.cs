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

        // オンラインロビー (対戦待合, フロー ②)。Title Online → ここ → START → Matchmaking。
        BuildLobbyScene("Assets/_Project/Scenes/PVPLobby.unity",
            new Color(0.10f, 0.04f, 0.06f));

        // 各曲前の難易度選択 & プレイ設定画面 (フロー ⑦)。
        BuildSongSetupScene("Assets/_Project/Scenes/PVPSongSetup.unity",
            new Color(0.05f, 0.06f, 0.11f));

        // PVPResult = 各曲完走後の「曲リザルト」画面 (フロー ⑩、セクター勝敗 + 累計 + クリンチ)。
        BuildSongResultScene("Assets/_Project/Scenes/PVPResult.unity",
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

        // ── ロック状況 (YOU ● / OPP ○) 右上 ─────────────────────
        var lockTMP = MakeTMP("LockStatusText", canvasGO, 24, "");
        SetAnchored(lockTMP, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-300, -150), new Vector2(560, 40));
        lockTMP.alignment = TextAlignmentOptions.Right;
        lockTMP.richText = true;

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

        // ── ラインナップカード (BanPhase のみ: 確定3曲を開示時に発表) ─────
        // 候補グリッドと同じ中央領域に重ねて配置。選択中は隠れ、開示時に候補と入れ替えで表示する。
        var lineupTiles = new System.Collections.Generic.List<DraftTileView>();
        if (phase == PvpDraftScreenController.Phase.BanPhase)
        {
            var lgrid = MakeGrid("LineupGrid", canvasGO, new Vector2(0, 20),
                new Vector2(900, 320), new Vector2(260, 300), new Vector2(40, 0), 3);
            for (int i = 0; i < 3; i++) lineupTiles.Add(MakeDraftTile(lgrid, 24, 20));
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
        so.FindProperty("_lockStatusText").objectReferenceValue = lockTMP;
        so.FindProperty("_primaryButton").objectReferenceValue = primaryGO.GetComponent<Button>();
        so.FindProperty("_cancelButton").objectReferenceValue  = cancelGO.GetComponent<Button>();

        var tilesProp = so.FindProperty("_tiles");
        tilesProp.arraySize = tiles.Count;
        for (int i = 0; i < tiles.Count; i++)
            tilesProp.GetArrayElementAtIndex(i).objectReferenceValue = tiles[i];

        var lineupProp = so.FindProperty("_lineupTiles");
        lineupProp.arraySize = lineupTiles.Count;
        for (int i = 0; i < lineupTiles.Count; i++)
            lineupProp.GetArrayElementAtIndex(i).objectReferenceValue = lineupTiles[i];

        so.ApplyModifiedPropertiesWithoutUndo();

        SaveAndRegister(scene, scenePath);
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
        SetAnchored(kicker, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(180, -64), new Vector2(700, 36));
        kicker.alignment = TextAlignmentOptions.Left;
        kicker.color = dim;
        kicker.characterSpacing = 6f;

        var lobbyTitle = MakeTMP("LobbyTitle", canvasGO, 72, "LOBBY");
        SetAnchored(lobbyTitle, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(180, -130), new Vector2(700, 90));
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
            "easy        0       0       0        0      0.00%\n" +
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
        // ボタン内ラベルを START / PRESS F5 に差し替え
        var startLbl = startGO.GetComponentInChildren<TextMeshProUGUI>();
        if (startLbl != null)
        {
            startLbl.text = "START";
            startLbl.fontSize = 64;
            startLbl.fontStyle = FontStyles.Bold;
            startLbl.alignment = TextAlignmentOptions.Center;
        }
        var pressF5 = MakeTMP("PressF5", canvasGO, 24, "PRESS  F5");
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

    // 各曲前の「難易度選択 & プレイ設定」画面 (フロー ⑦) を生成する。
    // ジャケット+曲名 / 難易度4ボタン(easy..extra) / ノートスピード・判定/表示オフセットのステッパ /
    // 相手難易度(同期待ち) / READY・CANCEL。PvpSongSetupController を baked-in 結線。
    static void BuildSongSetupScene(string scenePath, Color bg)
    {
        var scene = NewEmptyScene();
        BuildBaseObjects(scene, bg);
        var canvasGO = GameObject.Find("Canvas");

        Color cyan = new Color(0.17f, 0.85f, 0.90f, 1f);
        Color red  = new Color(0.95f, 0.30f, 0.42f, 1f);
        Color dim  = new Color(1, 1, 1, 0.7f);

        // ── ヘッダー "SONG n / 3" + アクセント線 ──────────────────
        var headerTMP = MakeTMP("Header", canvasGO, 54, "SONG 1 / 3");
        SetAnchored(headerTMP, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -70), new Vector2(1400, 78));
        headerTMP.alignment = TextAlignmentOptions.Center;
        headerTMP.fontStyle = FontStyles.Bold;
        headerTMP.characterSpacing = 6f;

        var accent = MakeImage("HeaderAccent", canvasGO, cyan);
        SetRect(accent.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -122), new Vector2(560, 4));

        // ── ジャケット + 曲名 (左) ───────────────────────────────
        var jacketImg = MakeImageRay("Jacket", canvasGO, Color.white, false);
        SetRect(jacketImg.rectTransform, Center, Center, new Vector2(-520, 120), new Vector2(300, 300));
        jacketImg.preserveAspect = true;
        jacketImg.enabled = false;

        var songTitleTMP = MakeTMP("SongTitleText", canvasGO, 34, "");
        SetAnchored(songTitleTMP, Center, Center, new Vector2(-520, -60), new Vector2(360, 80));
        songTitleTMP.alignment = TextAlignmentOptions.Center;
        songTitleTMP.fontStyle = FontStyles.Bold;
        songTitleTMP.overflowMode = TextOverflowModes.Ellipsis;

        // ── 難易度 4 ボタン (easy/normal/hard/extra) ─────────────
        var diffLabel = MakeTMP("DifficultyHeader", canvasGO, 24, "DIFFICULTY");
        SetAnchored(diffLabel, Center, Center, new Vector2(220, 300), new Vector2(900, 36));
        diffLabel.alignment = TextAlignmentOptions.Left;
        diffLabel.color = dim;
        diffLabel.characterSpacing = 6f;

        string[] diffNames = { "EASY\nx0.75", "NORMAL\nx0.80", "HARD\nx0.90", "EXTRA\nx1.00" };
        var diffButtons = new Button[4];
        for (int i = 0; i < 4; i++)
        {
            var b = MakeButton("Diff_" + i, canvasGO, diffNames[i]);
            var rt = b.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = Center; rt.pivot = Center;
            rt.anchoredPosition = new Vector2(-20 + i * 200, 230);
            rt.sizeDelta = new Vector2(180, 96);
            diffButtons[i] = b.GetComponent<Button>();
        }

        var diffSelLabel = MakeTMP("DifficultyLabel", canvasGO, 26, "EXTRA   x1.00");
        SetAnchored(diffSelLabel, Center, Center, new Vector2(220, 150), new Vector2(900, 40));
        diffSelLabel.alignment = TextAlignmentOptions.Left;
        diffSelLabel.color = cyan;
        diffSelLabel.fontStyle = FontStyles.Bold;

        // ── プレイ設定 3 ステッパ ────────────────────────────────
        var setLabel = MakeTMP("SettingsHeader", canvasGO, 24, "PLAY SETTINGS");
        SetAnchored(setLabel, Center, Center, new Vector2(220, 90), new Vector2(900, 36));
        setLabel.alignment = TextAlignmentOptions.Left;
        setLabel.color = dim;
        setLabel.characterSpacing = 6f;

        TextMeshProUGUI noteVal, judgeVal, visualVal;
        Button noteDown, noteUp, judgeDown, judgeUp, visualDown, visualUp;
        MakeStepper(canvasGO, "Note Speed",   new Vector2(220, 30),  out noteVal,   out noteDown,   out noteUp);
        MakeStepper(canvasGO, "Audio Offset", new Vector2(220, -34), out judgeVal,  out judgeDown,  out judgeUp);
        MakeStepper(canvasGO, "Visual Offset",new Vector2(220, -98), out visualVal, out visualDown, out visualUp);

        // ── 相手難易度 (同期待ち) ───────────────────────────────
        var oppTMP = MakeTMP("OppDiffText", canvasGO, 24, "OPP   —");
        SetAnchored(oppTMP, Center, Center, new Vector2(-520, -180), new Vector2(360, 40));
        oppTMP.alignment = TextAlignmentOptions.Center;
        oppTMP.color = red;
        oppTMP.fontStyle = FontStyles.Bold;

        // ── status ───────────────────────────────────────────────
        var statusTMP = MakeTMP("StatusText", canvasGO, 24, "");
        SetAnchored(statusTMP, Center, Center, new Vector2(0, -250), new Vector2(1400, 40));
        statusTMP.alignment = TextAlignmentOptions.Center;
        statusTMP.color = dim;

        // ── READY / CANCEL ───────────────────────────────────────
        var readyGO = MakeButton("ReadyButton", canvasGO, "READY");
        var rRT = readyGO.GetComponent<RectTransform>();
        rRT.anchorMin = rRT.anchorMax = new Vector2(0.5f, 0f); rRT.pivot = new Vector2(0.5f, 0f);
        rRT.anchoredPosition = new Vector2(200, 56); rRT.sizeDelta = new Vector2(360, 76);

        var cancelGO = MakeButton("CancelButton", canvasGO, "CANCEL");
        var cRT = cancelGO.GetComponent<RectTransform>();
        cRT.anchorMin = cRT.anchorMax = new Vector2(0.5f, 0f); cRT.pivot = new Vector2(0.5f, 0f);
        cRT.anchoredPosition = new Vector2(-200, 56); cRT.sizeDelta = new Vector2(360, 76);

        // ── Controller 配線 ─────────────────────────────────────
        var ctrlGO = new GameObject("PvpSongSetupController");
        var ctrl = ctrlGO.AddComponent<PvpSongSetupController>();
        var so = new SerializedObject(ctrl);
        so.FindProperty("_headerText").objectReferenceValue     = headerTMP;
        so.FindProperty("_songTitleText").objectReferenceValue  = songTitleTMP;
        so.FindProperty("_jacket").objectReferenceValue         = jacketImg;
        so.FindProperty("_difficultyLabel").objectReferenceValue= diffSelLabel;
        so.FindProperty("_noteSpeedText").objectReferenceValue  = noteVal;
        so.FindProperty("_judgeOffsetText").objectReferenceValue= judgeVal;
        so.FindProperty("_visualOffsetText").objectReferenceValue = visualVal;
        so.FindProperty("_oppDiffText").objectReferenceValue    = oppTMP;
        so.FindProperty("_statusText").objectReferenceValue     = statusTMP;
        so.FindProperty("_noteSpeedDown").objectReferenceValue  = noteDown;
        so.FindProperty("_noteSpeedUp").objectReferenceValue    = noteUp;
        so.FindProperty("_judgeOffsetDown").objectReferenceValue= judgeDown;
        so.FindProperty("_judgeOffsetUp").objectReferenceValue  = judgeUp;
        so.FindProperty("_visualOffsetDown").objectReferenceValue = visualDown;
        so.FindProperty("_visualOffsetUp").objectReferenceValue = visualUp;
        so.FindProperty("_readyButton").objectReferenceValue    = readyGO.GetComponent<Button>();
        so.FindProperty("_cancelButton").objectReferenceValue   = cancelGO.GetComponent<Button>();

        var diffProp = so.FindProperty("_difficultyButtons");
        diffProp.arraySize = diffButtons.Length;
        for (int i = 0; i < diffButtons.Length; i++)
            diffProp.GetArrayElementAtIndex(i).objectReferenceValue = diffButtons[i];

        so.ApplyModifiedPropertiesWithoutUndo();

        SaveAndRegister(scene, scenePath);
    }

    // 各曲完走後の「曲リザルト」画面 (フロー ⑩) を生成する。
    // ヘッダ "SONG n / 3 RESULT" / 曲名 / 自分・相手の獲得pt / セクター勝敗行 / 累計バー / クリンチ告知 / NEXT。
    // PvpSongResultController を baked-in 結線。
    static void BuildSongResultScene(string scenePath, Color bg)
    {
        var scene = NewEmptyScene();
        BuildBaseObjects(scene, bg);
        var canvasGO = GameObject.Find("Canvas");

        Color cyan = new Color(0.17f, 0.85f, 0.90f, 1f);
        Color red  = new Color(0.95f, 0.30f, 0.42f, 1f);
        Color dim  = new Color(1, 1, 1, 0.7f);

        var headerTMP = MakeTMP("Header", canvasGO, 54, "SONG 1 / 3   RESULT");
        SetAnchored(headerTMP, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -70), new Vector2(1400, 78));
        headerTMP.alignment = TextAlignmentOptions.Center;
        headerTMP.fontStyle = FontStyles.Bold;
        headerTMP.characterSpacing = 6f;

        var accent = MakeImage("HeaderAccent", canvasGO, cyan);
        SetRect(accent.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -122), new Vector2(560, 4));

        var songTitleTMP = MakeTMP("SongTitleText", canvasGO, 30, "");
        SetAnchored(songTitleTMP, Center, Center, new Vector2(0, 280), new Vector2(1400, 50));
        songTitleTMP.alignment = TextAlignmentOptions.Center;
        songTitleTMP.color = dim;

        // 自分 / 相手の獲得 pt (左右)
        var selfPtsTMP = MakeTMP("SelfPointsText", canvasGO, 44, "YOU  +0.0");
        SetAnchored(selfPtsTMP, Center, Center, new Vector2(-340, 170), new Vector2(560, 70));
        selfPtsTMP.alignment = TextAlignmentOptions.Center;
        selfPtsTMP.color = cyan;
        selfPtsTMP.fontStyle = FontStyles.Bold;

        var oppPtsTMP = MakeTMP("OppPointsText", canvasGO, 44, "OPP  +0.0");
        SetAnchored(oppPtsTMP, Center, Center, new Vector2(340, 170), new Vector2(560, 70));
        oppPtsTMP.alignment = TextAlignmentOptions.Center;
        oppPtsTMP.color = red;
        oppPtsTMP.fontStyle = FontStyles.Bold;

        // セクター勝敗行
        var sectorsTMP = MakeTMP("SectorsText", canvasGO, 34, "");
        SetAnchored(sectorsTMP, Center, Center, new Vector2(0, 40), new Vector2(1500, 60));
        sectorsTMP.alignment = TextAlignmentOptions.Center;
        sectorsTMP.fontStyle = FontStyles.Bold;
        sectorsTMP.richText = true;

        // 累計
        var cumTMP = MakeTMP("CumulativeText", canvasGO, 30, "");
        SetAnchored(cumTMP, Center, Center, new Vector2(0, -60), new Vector2(1500, 50));
        cumTMP.alignment = TextAlignmentOptions.Center;
        cumTMP.richText = true;

        // クリンチ告知
        var clinchTMP = MakeTMP("ClinchText", canvasGO, 34, "");
        SetAnchored(clinchTMP, Center, Center, new Vector2(0, -140), new Vector2(1400, 56));
        clinchTMP.alignment = TextAlignmentOptions.Center;
        clinchTMP.color = new Color(1f, 0.85f, 0.2f, 1f);
        clinchTMP.fontStyle = FontStyles.Bold;
        clinchTMP.characterSpacing = 4f;

        // NEXT
        var nextGO = MakeButton("PrimaryButton", canvasGO, "NEXT SONG");
        var nRT = nextGO.GetComponent<RectTransform>();
        nRT.anchorMin = nRT.anchorMax = new Vector2(0.5f, 0f); nRT.pivot = new Vector2(0.5f, 0f);
        nRT.anchoredPosition = new Vector2(0, 70); nRT.sizeDelta = new Vector2(420, 80);
        var nextLabel = nextGO.GetComponentInChildren<TextMeshProUGUI>();

        var ctrlGO = new GameObject("PvpSongResultController");
        var ctrl = ctrlGO.AddComponent<PvpSongResultController>();
        var so = new SerializedObject(ctrl);
        so.FindProperty("_headerText").objectReferenceValue     = headerTMP;
        so.FindProperty("_songTitleText").objectReferenceValue  = songTitleTMP;
        so.FindProperty("_selfPointsText").objectReferenceValue = selfPtsTMP;
        so.FindProperty("_oppPointsText").objectReferenceValue  = oppPtsTMP;
        so.FindProperty("_sectorsText").objectReferenceValue    = sectorsTMP;
        so.FindProperty("_cumulativeText").objectReferenceValue = cumTMP;
        so.FindProperty("_clinchText").objectReferenceValue     = clinchTMP;
        so.FindProperty("_primaryLabel").objectReferenceValue   = nextLabel;
        so.FindProperty("_primaryButton").objectReferenceValue  = nextGO.GetComponent<Button>();
        so.ApplyModifiedPropertiesWithoutUndo();

        SaveAndRegister(scene, scenePath);
    }

    // ラベル + 値 + [-][+] の 1 行ステッパを生成する。値 TMP と両ボタンを out で返す。
    static void MakeStepper(GameObject parent, string label, Vector2 pos,
                            out TextMeshProUGUI valueText, out Button down, out Button up)
    {
        var lbl = MakeTMP("Step_" + label, parent, 22, label);
        SetAnchored(lbl, Center, Center, new Vector2(pos.x - 110, pos.y), new Vector2(260, 40));
        lbl.alignment = TextAlignmentOptions.Left;

        var downGO = MakeButton(label + "_Down", parent, "<");
        var dRT = downGO.GetComponent<RectTransform>();
        dRT.anchorMin = dRT.anchorMax = Center; dRT.pivot = Center;
        dRT.anchoredPosition = new Vector2(pos.x + 110, pos.y); dRT.sizeDelta = new Vector2(56, 48);
        down = downGO.GetComponent<Button>();

        var val = MakeTMP(label + "_Value", parent, 24, "-");
        SetAnchored(val, Center, Center, new Vector2(pos.x + 200, pos.y), new Vector2(120, 44));
        val.alignment = TextAlignmentOptions.Center;
        val.fontStyle = FontStyles.Bold;
        valueText = val;

        var upGO = MakeButton(label + "_Up", parent, ">");
        var uRT = upGO.GetComponent<RectTransform>();
        uRT.anchorMin = uRT.anchorMax = Center; uRT.pivot = Center;
        uRT.anchoredPosition = new Vector2(pos.x + 290, pos.y); uRT.sizeDelta = new Vector2(56, 48);
        up = upGO.GetComponent<Button>();
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
