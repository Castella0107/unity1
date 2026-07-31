using System;
using System.Threading.Tasks;
using UnityEngine;
using Velopack;

/// <summary>
/// Velopack 自動更新の起動時フック (docs/deployment/velopack_release.md / caddy_updates.md)。
///
/// フロー:
///   1. BeforeSplashScreen: VelopackApp.Build().Run() — インストール/更新フック処理
///      (Update.exe から --veloapp-* 引数付きで起動された場合はここで処理して終了する)
///   2. AfterSceneLoad: 更新チェックを非同期開始。新版があれば DL → 適用 → 再起動
///
/// 方針 (K 指示):
///   - オフライン/フィード到達不可/タイムアウト時は静かにスキップして通常起動
///     (更新失敗でゲームが起動しない事態を絶対に避ける)
///   - エディタ実行時はスキップ
///   - vpk でインストールされていない生ビルド (IsInstalled=false) もスキップ
/// </summary>
public static class VelopackAutoUpdater
{
    /// <summary>
    /// 更新フィード URL — 唯一の定義箇所。Caddy の非公開パス
    /// (docs/deployment/caddy_updates.md の handle_path) と一致させること。
    /// パスを変える場合はここと Caddy 設定・リリース手順書の scp 先を揃えて変更する。
    /// </summary>
    public const string FeedUrl = "https://pvpharmonics.duckdns.org/updates-x7q2mkv9tr4w/";

    /// <summary>更新チェックのタイムアウト (ms)。超過したら諦めて通常起動。</summary>
    public const int CheckTimeoutMs = 10000;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
    static void VelopackInit()
    {
        if (Application.isEditor) return;
        try
        {
            // インストール/アンインストール/更新後フックの処理。フック起動時はこの中で
            // プロセスが終了する。通常起動時は何もせず即座に戻る。
            VelopackApp.Build().Run();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Velopack] init failed (続行): " + e.Message);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartUpdateCheck()
    {
        if (Application.isEditor) return;
        var host = new GameObject("VelopackUpdater");
        UnityEngine.Object.DontDestroyOnLoad(host);
        host.AddComponent<VelopackUpdaterRunner>();
    }
}

/// <summary>
/// 更新チェック本体 + 「Updating...」の簡素なオーバーレイ表示 (OnGUI)。
/// 失敗はすべて握りつぶしてログのみ — ゲーム起動をブロックしない。
/// </summary>
public class VelopackUpdaterRunner : MonoBehaviour
{
    volatile string _status;   // null = オーバーレイ非表示

    async void Start()
    {
        try
        {
            await RunUpdateFlowAsync();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Velopack] 更新チェックをスキップ: " + e.Message);
        }
        finally
        {
            _status = null;
            if (this != null) Destroy(gameObject);
        }
    }

    async Task RunUpdateFlowAsync()
    {
        var mgr = new UpdateManager(VelopackAutoUpdater.FeedUrl);

        // vpk インストール経由でない生ビルド (エディタ Build 直後のフォルダ実行等) では
        // Update.exe が存在せず適用できないためスキップする。
        if (!mgr.IsInstalled)
        {
            Debug.Log("[Velopack] 未インストール実行 (生ビルド) — 更新チェックをスキップ");
            return;
        }

        // 更新確認 (タイムアウト付き — オフラインでも CheckTimeoutMs 以内に諦める)
        var checkTask = mgr.CheckForUpdatesAsync();
        var finished  = await Task.WhenAny(checkTask, Task.Delay(VelopackAutoUpdater.CheckTimeoutMs));
        if (finished != checkTask)
        {
            Debug.LogWarning("[Velopack] 更新チェックがタイムアウト — 通常起動します");
            return;
        }
        var info = await checkTask;   // 例外はここで伝播 → Start の catch でスキップ
        if (info == null)
        {
            Debug.Log("[Velopack] 最新版です");
            return;
        }

        // 新版あり: DL → 適用 → 再起動 (この先は失敗しても通常起動へフォールバック)
        string ver = info.TargetFullRelease != null ? info.TargetFullRelease.Version.ToString() : "?";
        Debug.Log("[Velopack] 新しいバージョンを検出: v" + ver);
        _status = "Updating... (v" + ver + ")";

        await mgr.DownloadUpdatesAsync(info, p => _status = "Updating... " + p + "%  (v" + ver + ")");

        _status = "Restarting...";
        Debug.Log("[Velopack] 更新を適用して再起動します");
        mgr.ApplyUpdatesAndRestart(info);   // Update.exe を起動して自プロセスを終了する
    }

    void OnGUI()
    {
        string s = _status;
        if (string.IsNullOrEmpty(s)) return;
        const float w = 420f, h = 54f;
        var rect = new Rect((Screen.width - w) / 2f, Screen.height * 0.12f, w, h);
        GUI.Box(rect, "");
        var style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize  = 20,
            fontStyle = FontStyle.Bold,
        };
        GUI.Label(rect, s, style);
    }
}
