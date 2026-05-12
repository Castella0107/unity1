using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CreateBootstrapScene
{
    [MenuItem("Tools/Create Bootstrap Scene")]
    public static void Create()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        EditorSceneManager.SetActiveScene(scene);

        var go = new GameObject("Bootstrap");
        go.AddComponent<BootstrapController>();
        // _inputAsset must be set manually in Inspector after scene is saved

        string path = "Assets/_Project/Scenes/Bootstrap.unity";
        EditorSceneManager.SaveScene(scene, path);

        // Add to Build Settings: Bootstrap first, then _Persistent
        var current = new System.Collections.Generic.List<EditorBuildSettingsScene>(
            EditorBuildSettings.scenes);

        // Remove Bootstrap if already there
        current.RemoveAll(s => s.path == path);

        // Insert Bootstrap at index 0 (before _Persistent)
        current.Insert(0, new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = current.ToArray();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CreateBootstrapScene] Created and added to Build Settings at index 0. " +
                  "Assign _inputAsset in the Inspector.");
    }
}
