using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// PVP本体の Windows64 ビルドヘルパー。
/// メニュー: Tools/PVP/Build (Win64)
/// batch: Unity.exe -batchmode -nographics -projectPath "C:/Users/CaSte/PVP" -executeMethod BuildPvpStandalone.BuildWin64 -quit -logFile &lt;log&gt;
/// 出力先: {project}/Build/PVP-{version}/PVP.exe
/// </summary>
public static class BuildPvpStandalone
{
    const string ProductName = "PVP";
    const string ExeName     = "PVP.exe";

    [MenuItem("Tools/PVP/Build (Win64)")]
    public static void BuildWin64Menu() => BuildWin64();

    public static void BuildWin64()
    {
        var scenes = CollectEnabledScenes();
        if (scenes.Length == 0)
        {
            Debug.LogError("[Build] No enabled scenes — aborting.");
            if (Application.isBatchMode) EditorApplication.Exit(2);
            return;
        }

        string ver = PlayerSettings.bundleVersion;
        if (string.IsNullOrEmpty(ver)) ver = "0.0.0";
        string outDir = Path.Combine(Directory.GetCurrentDirectory(), "Build", $"{ProductName}-{ver}");
        Directory.CreateDirectory(outDir);
        string outExe = Path.Combine(outDir, ExeName);

        var opts = new BuildPlayerOptions
        {
            scenes           = scenes,
            locationPathName = outExe,
            target           = BuildTarget.StandaloneWindows64,
            targetGroup      = BuildTargetGroup.Standalone,
            options          = BuildOptions.None,
        };

        Debug.Log($"[Build] Output: {outExe}");
        Debug.Log($"[Build] Scenes ({scenes.Length}):");
        for (int i = 0; i < scenes.Length; i++) Debug.Log($"  [{i}] {scenes[i]}");

        var report  = BuildPipeline.BuildPlayer(opts);
        var summary = report.summary;
        Debug.Log($"[Build] Result={summary.result} Size={summary.totalSize} Err={summary.totalErrors} Warn={summary.totalWarnings} TimeMs={summary.totalTime.TotalMilliseconds:F0}");

        if (summary.result != BuildResult.Succeeded)
        {
            Debug.LogError($"[Build] Build FAILED ({summary.result}).");
            if (Application.isBatchMode) EditorApplication.Exit(3);
            return;
        }
        Debug.Log($"[Build] SUCCESS → {outExe}");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    static string[] CollectEnabledScenes()
    {
        var list = new System.Collections.Generic.List<string>();
        var all  = EditorBuildSettings.scenes;
        for (int i = 0; i < all.Length; i++)
            if (all[i].enabled && !string.IsNullOrEmpty(all[i].path)) list.Add(all[i].path);
        return list.ToArray();
    }
}
