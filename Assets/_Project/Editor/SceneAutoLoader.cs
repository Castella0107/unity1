using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// エディターの Play モード開始時に常に Bootstrap.unity から起動し、停止後に直前のシーンへ自動復帰させるエディター拡張クラス。
/// Tools メニューから有効/無効の切り替えが可能。
/// </summary>
// Ensures Play mode always starts from Bootstrap.unity, regardless of which scene
// is currently open in the editor. After stopping, restores the previously open scene.
[InitializeOnLoad]
public static class SceneAutoLoader
{
    const string BootstrapPath   = "Assets/_Project/Scenes/Bootstrap.unity";
    const string PrevSceneKey    = "SceneAutoLoader_PreviousScene";
    const string EnabledKey      = "SceneAutoLoader_Enabled";

    static SceneAutoLoader()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    static bool IsEnabled => EditorPrefs.GetBool(EnabledKey, true);

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (!IsEnabled) return;

        switch (state)
        {
            case PlayModeStateChange.ExitingEditMode:
                BeforeEnterPlayMode();
                break;
            case PlayModeStateChange.EnteredEditMode:
                AfterExitPlayMode();
                break;
        }
    }

    static void BeforeEnterPlayMode()
    {
        var currentPath = EditorSceneManager.GetActiveScene().path;
        EditorPrefs.SetString(PrevSceneKey, currentPath);

        if (currentPath == BootstrapPath) return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            EditorApplication.isPlaying = false;
            return;
        }

        if (!File.Exists(BootstrapPath))
        {
            Debug.LogError("[SceneAutoLoader] Bootstrap scene not found at " + BootstrapPath);
            EditorApplication.isPlaying = false;
            return;
        }

        EditorSceneManager.OpenScene(BootstrapPath);
    }

    static void AfterExitPlayMode()
    {
        var prev = EditorPrefs.GetString(PrevSceneKey, "");
        if (string.IsNullOrEmpty(prev) || prev == BootstrapPath) return;
        if (!File.Exists(prev)) return;

        EditorSceneManager.OpenScene(prev);
    }

    [MenuItem("Tools/Scene AutoLoader/Toggle (currently ON)", priority = 200)]
    static void ToggleOn()  => SetEnabled(false);

    [MenuItem("Tools/Scene AutoLoader/Toggle (currently ON)", true)]
    static bool ValidateOn() => IsEnabled;

    [MenuItem("Tools/Scene AutoLoader/Toggle (currently OFF)", priority = 200)]
    static void ToggleOff() => SetEnabled(true);

    [MenuItem("Tools/Scene AutoLoader/Toggle (currently OFF)", true)]
    static bool ValidateOff() => !IsEnabled;

    static void SetEnabled(bool value)
    {
        EditorPrefs.SetBool(EnabledKey, value);
        Debug.Log("[SceneAutoLoader] " + (value ? "Enabled" : "Disabled"));
    }
}
