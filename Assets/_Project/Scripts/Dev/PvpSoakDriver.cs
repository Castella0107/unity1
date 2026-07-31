using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using RhythmGame.Network.Api;

/// <summary>
/// PVP ソークテスト用の自走ドライバ (開発専用 — K 指示 2026-07-30:
/// Bot を対戦相手にフルマッチを何度もループし、スコアパリティと全画面を検査する)。
///
/// 有効化条件: **エディタ実行 かつ PlayerPrefs "SoakTest" == 1** のときのみ起動する
/// (ビルドでは絶対に動かない — PVP オートプレイのチート化を防ぐため二重ガード)。
///
/// 動作: 各シーンの UI ボタンを実際に押して人間の操作を再現する
/// (Title→PVPLobby→キュー→READY→ドラフト(ランダムピック/BAN)→オートプレイ→リザルト→次試合)。
/// パリティ検査: 各曲のリザルトで GET song result の my_scores とクライアント送信値を突き合わせ、
/// [SoakParity] 行としてログ出力する (不一致は mismatch=True)。
/// </summary>
public static class PvpSoakBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        if (!Application.isEditor) return;
        if (PlayerPrefs.GetInt("SoakTest", 0) != 1) return;
        // エディタが OS フォーカスを失うとプレイヤーループが frame=1 で凍結するため必須
        // (リモート自動実行では常にフォーカス無し。2026-07-30 実測)
        Application.runInBackground = true;

        var go = new GameObject("PvpSoakDriver");
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.AddComponent<PvpSoakDriver>();
        Debug.Log("[Soak] ドライバ起動 (SoakTest=1, runInBackground=true)");
    }
}

public class PvpSoakDriver : MonoBehaviour
{
    /// <summary>アセンブリ反映確認用マーカー (execute_code から参照)。</summary>
    public const string Version = "soak-v8";

    const float TickInterval = 0.6f;   // UI 操作の間隔
    const float StuckSeconds = 150f;   // 同一シーン滞留の警告閾値 (マッチング待ちは除外)

    float  _nextTick;
    string _lastScene = "";
    float  _sceneEnterTime;
    bool   _captured;
    int    _matchCount;
    int    _lastParityCheckedSong = -1;
    readonly System.Random _rng = new System.Random();

    void Update()
    {
        if (Time.unscaledTime < _nextTick) return;
        _nextTick = Time.unscaledTime + TickInterval;

        string scene = SceneManager.GetActiveScene().name;
        if (scene != _lastScene)
        {
            Debug.Log($"[Soak] scene → {scene}");
            _lastScene = scene;
            _sceneEnterTime = Time.unscaledTime;
            _captured = false;
        }

        // 各画面の自動スクリーンショット (UI が落ち着く 1.2 秒後に 1 回、シーン名で上書き保存)
        if (!_captured && Time.unscaledTime - _sceneEnterTime > 1.2f)
        {
            _captured = true;
            try
            {
                if (!System.IO.Directory.Exists("Assets/Screenshots"))
                    System.IO.Directory.CreateDirectory("Assets/Screenshots");
                ScreenCapture.CaptureScreenshot($"Assets/Screenshots/soak_auto_{scene}.png");
            }
            catch (Exception) { }
        }
        else if (Time.unscaledTime - _sceneEnterTime > StuckSeconds &&
                 scene != "Matchmaking" && scene != "GamePlay")
        {
            // 死んだマッチの画面に取り残された場合などの安全網: Title へ強制復帰
            Debug.LogWarning($"[Soak][STUCK] scene={scene} で {StuckSeconds}s 以上停滞 — Title へ強制復帰");
            _sceneEnterTime = Time.unscaledTime;
            SceneManager.LoadScene("Title");
        }

        try
        {
            switch (scene)
            {
                // Bootstrap のロードコルーチンが稀に無音停止する事象への自己回復
                // (エディタ+MCP 起動時に観測。_Persistent は正常ロード済みなので手動遷移で続行できる)
                case "Bootstrap":
                    if (Time.unscaledTime - _sceneEnterTime > 10f && SceneRouter.Instance != null)
                    {
                        // SceneRouter.GoTo はコルーチン依存で、停滞セッションでは効かないことが
                        // あるため直接 LoadScene で回復する
                        Debug.LogWarning("[Soak] Bootstrap 停滞を検出 — Login へ直接遷移して回復");
                        SceneManager.LoadScene("Login");
                        _sceneEnterTime = Time.unscaledTime;
                    }
                    break;

                case "Title":       StepTitle(); break;
                case "PVPLobby":    ClickButton<object>("PvpLobbyController", "_startButton"); break;
                case "Matchmaking": break;   // 自動 (Bot が拾うのを待つ)
                case "PVPPrematch": ClickButton<object>("PvpPrematchController", "_readyButton"); break;
                case "PVPSongPick": StepDraft(); break;
                case "GamePlay":    break;   // オートプレイ (PvpDraftController の SoakTest ゲート)
                case "PVPResult":   StepSongResult(); break;
                case "PVPMatchEnd": StepMatchEnd(); break;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Soak] step 例外: " + e.Message);
        }
    }

    // ── Title: PVP ロビーへ ──────────────────────────────────────────────────
    void StepTitle()
    {
        // オートプレイ設定 ON (ドラフトの SoakTest ゲートと併用)
        PlayOptionsController.AutoPlay = true;
        SceneRouter.Instance?.GoTo(SceneId.PVPLobby, null);
    }

    // ── ドラフト: ランダムピック / BAN ───────────────────────────────────────
    void StepDraft()
    {
        var ctrl = FindController("PvpDraftController");
        if (ctrl == null) return;

        var confirm = GetField<Button>(ctrl, "_confirmButton");
        if (confirm != null && confirm.gameObject.activeInHierarchy && confirm.interactable)
        {
            confirm.onClick.Invoke();
            return;
        }

        // 曲タイル: 押せるものからランダムに 1 つ
        var tiles = GetField<Button[]>(ctrl, "_songTiles");
        var candidates = new List<Button>();
        if (tiles != null)
            foreach (var t in tiles)
                if (t != null && t.gameObject.activeInHierarchy && t.interactable) candidates.Add(t);
        if (candidates.Count > 0)
        {
            candidates[_rng.Next(candidates.Count)].onClick.Invoke();
            // 曲を選んだ直後は難易度ボタンの有効・無効が「前の曲」基準のままなので、
            // 同じティックで難易度を押すと譜面の無い難易度を選んでしまう。
            // (ソーク 2026-07-31: song_005/hard を選び、play フェーズで譜面が取れず不戦敗)
            // UI が選択曲で組み直されるのを待って次のティックで選ぶ。
            return;
        }

        // 難易度: 押せるものからランダムに 1 つ
        var diffs = GetField<Button[]>(ctrl, "_diffButtons");
        var diffCand = new List<Button>();
        if (diffs != null)
            foreach (var d in diffs)
                if (d != null && d.gameObject.activeInHierarchy && d.interactable) diffCand.Add(d);
        if (diffCand.Count > 0)
            diffCand[_rng.Next(diffCand.Count)].onClick.Invoke();
    }

    // ── 曲リザルト: パリティ検査 → NEXT ─────────────────────────────────────
    async void StepSongResult()
    {
        int order = PvpMatchContext.CurrentSongOrder;
        if (order >= 1 && order != _lastParityCheckedSong)
        {
            _lastParityCheckedSong = order;
            await CheckParityAsync(order);
        }
        ClickButton<object>("PvpSongResultV2Controller", "_nextButton");
    }

    async System.Threading.Tasks.Task CheckParityAsync(int order)
    {
        try
        {
            // クライアント送信値 (RecordSongPlayed が積んだセクター合計)
            long clientTotal = -1;
            var flat = PvpMatchContext.SelfSectorScoresFlat;
            if (flat != null && flat.Count >= order * 5)
            {
                clientTotal = 0;
                for (int i = (order - 1) * 5; i < order * 5; i++) clientTotal += flat[i];
            }

            // 相手 (Bot) の提出が済むまで結果は確定しないため、少し粘って取得する。
            // my_scores は「自分だけ提出済み」の間のみ、両者提出後は song_result の
            // セクター内訳 (score_a/score_b) から自分側を合算する。
            long serverTotal = -1;
            for (int attempt = 0; attempt < 15 && serverTotal < 0; attempt++)
            {
                var r = await PvpApi.GetSongResultAsync(PvpMatchContext.MatchId, order);
                if (r.Ok && r.Data != null)
                {
                    if (r.Data.MyScores != null)
                    {
                        serverTotal = 0;
                        foreach (var s in r.Data.MyScores) serverTotal += s;
                        break;
                    }
                    if (r.Data.SongResult?.Sectors != null)
                    {
                        bool selfA = PvpMatchContext.SelfIsA;
                        serverTotal = 0;
                        foreach (var s in r.Data.SongResult.Sectors)
                            serverTotal += selfA ? s.ScoreA : s.ScoreB;
                        break;
                    }
                }
                await System.Threading.Tasks.Task.Delay(2000);
            }

            bool mismatch = clientTotal >= 0 && serverTotal >= 0 && clientTotal != serverTotal;
            Debug.Log($"[SoakParity] match={_matchCount + 1} order={order} client={clientTotal} server={serverTotal} mismatch={mismatch}");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SoakParity] 検査失敗: " + e.Message);
        }
    }

    // ── 試合終了: カウントして次へ ───────────────────────────────────────────
    string _lastCountedMatch;

    void StepMatchEnd()
    {
        // 最終結果画面はスクリーンショット確保のため 4 秒表示してから次へ
        if (Time.unscaledTime - _sceneEnterTime < 4f) return;
        var btn = FindButton("PvpMatchEndController", "_backToTitleButton");
        if (btn == null) return;
        // シーン遷移前に tick が複数回走ってもマッチごとに 1 回だけ数える
        string mid = RhythmGame.Network.Api.PvpMatchContext.MatchId;
        if (mid != _lastCountedMatch)
        {
            _lastCountedMatch = mid;
            _matchCount++;
            _lastParityCheckedSong = -1;
            Debug.Log($"[Soak] === match {_matchCount} 完了 → 次の試合へ ===");
        }
        btn.onClick.Invoke();
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    static Component FindController(string typeName)
    {
        foreach (var mb in FindObjectsOfType<MonoBehaviour>(true))
            if (mb != null && mb.GetType().Name == typeName) return mb;
        return null;
    }

    static T GetField<T>(Component c, string field) where T : class
        => c.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            ?.GetValue(c) as T;

    Button FindButton(string typeName, string field)
    {
        var ctrl = FindController(typeName);
        if (ctrl == null) return null;
        var btn = GetField<Button>(ctrl, field);
        return (btn != null && btn.gameObject.activeInHierarchy && btn.interactable) ? btn : null;
    }

    void ClickButton<TUnused>(string typeName, string field)
    {
        var btn = FindButton(typeName, field);
        if (btn != null) btn.onClick.Invoke();
    }
}
