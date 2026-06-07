using System;
using System.Collections.Concurrent;
using UnityEngine;

// Routes actions from background threads (e.g. AudioDevicePoll) onto the Unity main thread.
// Place in _Persistent.unity so it survives scene loads.
/// <summary>
/// バックグラウンドスレッド（例: AudioDevicePoll）からのアクションを Unity メインスレッドへ安全にディスパッチするシングルトン。
/// _Persistent.unity に配置してシーンロードを跨いで動作させる。
/// </summary>
public class MainThreadDispatcher : MonoBehaviour
{
    /// <summary>シングルトンインスタンス。</summary>
    public static MainThreadDispatcher Instance { get; private set; }

    readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();

    /// <summary>
    /// シーン配置に依存せず必ず存在させる自己ブートストラップ。
    /// (_Persistent 未配置のまま MatchSocketClient が Dispatch すると受信スレッドで
    /// Unity API に触れて例外死するため、BeforeSceneLoad で確実に生成する)
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("MainThreadDispatcher (auto)");
        go.AddComponent<MainThreadDispatcher>();
        // Awake で Instance 設定+DontDestroyOnLoad される
    }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>次回 Update でメインスレッド実行するアクションをキューに積む(スレッドセーフ)。</summary>
    public void Enqueue(Action action)
    {
        if (action != null) _queue.Enqueue(action);
    }

    void Update()
    {
        while (_queue.TryDequeue(out var action))
        {
            try   { action.Invoke(); }
            catch (Exception e) { Debug.LogError("[MainThreadDispatcher] " + e); }
        }
    }
}
