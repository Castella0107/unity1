using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// シーン遷移を一元管理するシングルトン MonoBehaviour。
/// フェードや LoadingOverlay と連携しながらアディティブロード／アンロードを順番に実行し、_Persistent シーンを維持しつつ各シーンを切り替える。
/// </summary>
public class SceneRouter : MonoBehaviour
{
    /// <summary>シングルトンインスタンス。</summary>
    public static SceneRouter Instance { get; private set; }

    [SerializeField] TransitionFx _transitionFx;

    SceneId _currentScene   = SceneId.Bootstrap;
    bool    _isTransitioning;

    static readonly Dictionary<SceneId, string> SceneNames = new Dictionary<SceneId, string>
    {
        { SceneId.Bootstrap,   "Bootstrap"    },
        { SceneId.Persistent,  "_Persistent"  },
        { SceneId.Title,       "Title"        },
        { SceneId.SongSelect,  "SongSelect"   },
        { SceneId.GamePlay,    "GamePlay"     },
        { SceneId.Result,      "Result"       },
        { SceneId.Config,      "Config"       },
        { SceneId.History,     "History"      },
        { SceneId.Matchmaking, "Matchmaking"  },
        { SceneId.PVPPrematch, "PVPPrematch"  },
        { SceneId.PVPSongPick, "PVPSongPick"  },
        { SceneId.PVPBanPhase, "PVPBanPhase"  },
        { SceneId.PVPResult,   "PVPResult"    },
        { SceneId.PVPMatchEnd, "PVPMatchEnd"  },
    };

    // Scenes that show LoadingOverlay during transition
    static readonly HashSet<SceneId> HeavyLoadScenes = new HashSet<SceneId>
    {
        SceneId.GamePlay,
        SceneId.SongSelect,
    };

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>現在表示中のシーン。</summary>
    public SceneId CurrentScene    => _currentScene;
    /// <summary>シーン遷移の実行中か。</summary>
    public bool    IsTransitioning => _isTransitioning;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>指定シーンへ遷移する。パラメータと遷移演出を指定可能。遷移中の呼び出しは無視される。</summary>
    public void GoTo(
        SceneId          target,
        ISceneParameters parameters = null,
        TransitionStyle  style      = TransitionStyle.FadeBlack)
    {
        if (_isTransitioning)
        {
            Debug.LogWarning(string.Format("[SceneRouter] Transition in progress — ignoring GoTo({0})", target));
            return;
        }
        _isTransitioning = true;   // set synchronously so callers can check it immediately
        StartCoroutine(GoToRoutine(target, parameters ?? EmptyParameters.Instance, style));
    }

    /// <summary>起動時に Title シーンへ遷移する(起動時の黒フラッシュを避けるため演出なし)。</summary>
    public void InitialBoot()
    {
        // Use None for boot (no black flash on startup)
        GoTo(SceneId.Title, null, TransitionStyle.None);
    }

    // ── Core routine ──────────────────────────────────────────────────────────

    IEnumerator GoToRoutine(SceneId target, ISceneParameters parameters, TransitionStyle style)
    {
        // _isTransitioning already set synchronously in GoTo()
        ParameterStore.SetPending(parameters);

        string targetLabel;
        if (!SceneNames.TryGetValue(target, out targetLabel))
        {
            Debug.LogError("[SceneRouter] No scene name mapping for " + target);
            _isTransitioning = false;
            yield break;
        }

        LogSceneState("Before GoTo " + target);

        bool isHeavy = HeavyLoadScenes.Contains(target);

        // 1. Fade out (covers screen while loading)
        if (_transitionFx != null)
            yield return _transitionFx.FadeOut(style);

        // 2. Loading overlay (heavy scenes only, shown after fade)
        if (isHeavy && LoadingOverlay.Instance != null)
            yield return LoadingOverlay.Instance.Show("Loading " + target + "...");

        // 3. Pre-unload: evict any existing copy of the target scene before loading.
        //    Without this, GoTo(GamePlay) while GamePlay is already loaded creates a duplicate
        //    because step 5 skips scenes by name and misses the stale copy.
        for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.name != targetLabel || !s.isLoaded) continue;
            Debug.Log("[SceneRouter] Pre-evicting stale: " + s.name);
            var evict = SceneManager.UnloadSceneAsync(s);
            while (evict != null && !evict.isDone)
                yield return null;
        }

        // 4. Additive load of new scene
        var loadOp = SceneManager.LoadSceneAsync(targetLabel, LoadSceneMode.Additive);
        loadOp.allowSceneActivation = false;

        while (loadOp.progress < 0.9f)
        {
            if (isHeavy && LoadingOverlay.Instance != null)
                LoadingOverlay.Instance.SetProgress(loadOp.progress);
            yield return null;
        }

        if (isHeavy && LoadingOverlay.Instance != null)
            LoadingOverlay.Instance.SetProgress(1.0f, "Ready");

        loadOp.allowSceneActivation = true;
        yield return loadOp;

        // 5. Set new scene active first so physics/audio use the correct scene.
        //    GetSceneByName is now unambiguous because the pre-evict step removed any stale copy.
        var newScene = SceneManager.GetSceneByName(targetLabel);
        if (newScene.IsValid())
            SceneManager.SetActiveScene(newScene);

        // 6. Unload ALL remaining stale scenes (everything except _Persistent and the new scene)
        var toUnload = new List<Scene>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.name == "_Persistent") continue;
            if (s.name == targetLabel)   continue;
            if (s.isLoaded)              toUnload.Add(s);
        }

        foreach (var s in toUnload)
        {
            Debug.Log("[SceneRouter] Unloading: " + s.name);
            var unloadOp = SceneManager.UnloadSceneAsync(s);
            while (unloadOp != null && !unloadOp.isDone)
                yield return null;
        }

        // 7. Memory cleanup after unloads
        if (isHeavy || toUnload.Count > 0)
            yield return AggressiveCleanup();

        _currentScene = target;
        LogSceneState("After GoTo " + target);

        // 8. Hide overlay
        if (isHeavy && LoadingOverlay.Instance != null)
            yield return LoadingOverlay.Instance.Hide();

        // 9. Fade in
        if (_transitionFx != null)
            yield return _transitionFx.FadeIn(style);

        _isTransitioning = false;
    }

    static void LogSceneState(string label)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("[SceneRouter] ").Append(label).Append(" | scenes=").Append(SceneManager.sceneCount).Append(" [");
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (i > 0) sb.Append(", ");
            sb.Append(s.name);
            if (SceneManager.GetActiveScene() == s) sb.Append("*");
        }
        sb.Append("]");
        Debug.Log(sb.ToString());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>未使用アセットを解放し GC を強制実行してメモリを積極的に回収する。</summary>
    public static IEnumerator AggressiveCleanup()
    {
        yield return Resources.UnloadUnusedAssets();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}
