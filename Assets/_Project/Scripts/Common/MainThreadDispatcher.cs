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
    public static MainThreadDispatcher Instance { get; private set; }

    readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

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
