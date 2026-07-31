using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Bootstrap.unity（Build Settings の先頭シーン）の GameObject にアタッチする MonoBehaviour。
/// _Persistent シーンをアディティブロードし、SceneRouter 経由で Title シーンへ遷移する起動シーケンスを制御する。
/// </summary>
// Attach to a GameObject in Bootstrap.unity (the first scene in Build Settings, index 0).
// Loads _Persistent additively (contains SceneRouter + LoadingOverlay), then navigates to Title.
public class BootstrapController : MonoBehaviour
{
    [SerializeField] InputActionAsset _inputAsset;

    IEnumerator Start()
    {
        Debug.Log("[Bootstrap] Start");

        // 譜面差し替え用フォルダを実体化しておく。
        // 存在しないと chart-admin の Unity 同期でフォルダを選べない。
        ChartLoader.EnsureUserSongsRoot();

        // Apply display settings before the first frame renders
        DisplayTabController.ApplySettingsOnBoot();

        // Restore custom key-binding overrides
        if (_inputAsset != null)
            InputTabController.LoadBindingsFromPrefs(_inputAsset);

        Debug.Log("[Bootstrap] Loading _Persistent");

        // Load _Persistent (SceneRouter and service singletons live here)
        if (!SceneManager.GetSceneByName("_Persistent").isLoaded)
        {
            var op = SceneManager.LoadSceneAsync("_Persistent", LoadSceneMode.Additive);
            int frames = 0;
            while (op != null && !op.isDone)
            {
                if (frames++ > 300)   // ~5 seconds at 60fps
                {
                    Debug.LogError("[Bootstrap] _Persistent load timed out! progress=" + op.progress);
                    yield break;
                }
                Debug.Log("[Bootstrap] loading progress=" + op.progress + " frame=" + frames);
                yield return null;
            }
            Debug.Log("[Bootstrap] LoadSceneAsync done. isDone=" + (op != null && op.isDone));
        }
        else
        {
            Debug.Log("[Bootstrap] _Persistent already loaded");
        }

        yield return null;   // let Awake() run on newly loaded _Persistent objects

        Debug.Log("[Bootstrap] _Persistent loaded. SceneRouter=" +
                  (SceneRouter.Instance != null ? "found" : "NULL"));

        if (SceneRouter.Instance == null)
        {
            Debug.LogError("[Bootstrap] SceneRouter not found — verify _Persistent.unity " +
                           "has a SceneRouter component.");
            yield break;
        }

        // Navigate to Title or directly to GamePlay (TestPlay CLI: --chart <path> [--difficulty <diff>])
        var testPlayParams = TryBuildTestPlayParams();
        var gotoScene      = TryParseGotoArg();   // デバッグ: --goto=<SceneId名> で直行
        if (testPlayParams != null)
        {
            Debug.Log("[Bootstrap] TestPlay mode: --chart=" + ChartLoader.OverrideBasePath
                      + " songId=" + testPlayParams.SongId
                      + " difficulty=" + testPlayParams.Difficulty);
            SceneRouter.Instance.GoTo(SceneId.GamePlay, testPlayParams, TransitionStyle.None);
        }
        else if (gotoScene.HasValue)
        {
            Debug.Log("[Bootstrap] Debug goto: --goto=" + gotoScene.Value);
            SceneRouter.Instance.GoTo(gotoScene.Value, null, TransitionStyle.None);
        }
        else
        {
            SceneRouter.Instance.InitialBoot();
        }

        Debug.Log("[Bootstrap] Boot navigation kicked. IsTransitioning=" + SceneRouter.Instance.IsTransitioning);

        // Wait for the transition to finish (Title is fully loaded and set active)
        while (SceneRouter.Instance.IsTransitioning)
            yield return null;

        Debug.Log("[Bootstrap] Transition complete. Unloading Bootstrap.");

        // Bootstrap can now be safely unloaded (Title is the active scene)
        SceneManager.UnloadSceneAsync("Bootstrap");
    }

    // デバッグ起動引数 --goto=<SceneId名> を解析する (例: PVP.exe --goto=Config)。
    // ビルドのみで再現する画面クラッシュの切り分け用。無効値は通常起動。
    static SceneId? TryParseGotoArg()
    {
        var args = System.Environment.GetCommandLineArgs();
        foreach (var a in args)
        {
            if (!a.StartsWith("--goto=")) continue;
            string name = a.Substring("--goto=".Length);
            if (System.Enum.TryParse<SceneId>(name, true, out var id)) return id;
            Debug.LogWarning("[Bootstrap] --goto: 不明なシーン名 " + name);
        }
        return null;
    }

    // ── TestPlay CLI ──────────────────────────────────────────────────────────
    // ChartEditor の TestPlayLauncher が <pvp.exe> --chart "<basePath>" [--difficulty <diff>]
    // で起動するときに参照される。--chart が無効ディレクトリなら null を返し通常起動にフォールバック。
    static GamePlayParameters TryBuildTestPlayParams()
    {
        string chartArg = CommandLineArgs.Get("chart");
        if (string.IsNullOrEmpty(chartArg)) return null;

        if (!Directory.Exists(chartArg))
        {
            Debug.LogWarning("[Bootstrap] --chart path does not exist, ignoring: " + chartArg);
            return null;
        }

        string difficulty = CommandLineArgs.Get("difficulty");
        if (string.IsNullOrEmpty(difficulty)) difficulty = "extra";

        ChartLoader.OverrideBasePath = chartArg;

        // songId は記録/リプレイ用。meta.json に依存しないようディレクトリ名から導出。
        string songId = Path.GetFileName(chartArg.TrimEnd('/', '\\'));
        if (string.IsNullOrEmpty(songId)) songId = "test_play_chart";

        // --hispeed <value> (ChartEditor から指定)。無効/未指定なら保存値→既定4.5。
        string hsArg = CommandLineArgs.Get("hispeed");
        if (string.IsNullOrEmpty(hsArg) ||
            !float.TryParse(hsArg, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out float hiSpeed))
        {
            hiSpeed = PlayerPrefs.GetFloat("HiSpeed", 4.5f);
        }

        return new GamePlayParameters
        {
            SongId       = songId,
            Difficulty   = difficulty,
            HiSpeed      = hiSpeed,
            JudgeOffset  = 0,
            VisualOffset = 0,
            Modifier     = "None",
            IsReplay     = false,
        };
    }
}
