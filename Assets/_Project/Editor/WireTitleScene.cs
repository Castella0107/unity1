using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 現在開いているシーンの TitleController に InputActionAsset と UI 参照を自動配線するエディターオンリーのヘルパークラス。
/// </summary>
public static class WireTitleScene
{
    /// <summary>現在開いているシーンの TitleController に InputActionAsset と UI 参照を自動配線する。</summary>
    [MenuItem("Tools/Wire Title Scene References")]
    public static void Wire()
    {
        // Load InputActionAsset
        var inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
            "Assets/InputSystem_Actions.inputactions");
        if (inputAsset == null)
        {
            Debug.LogError("[WireTitleScene] InputActionAsset not found at Assets/InputSystem_Actions.inputactions");
            return;
        }

        var ctrl = Object.FindFirstObjectByType<TitleController>();
        if (ctrl == null)
        {
            Debug.LogError("[WireTitleScene] TitleController not found in open scenes");
            return;
        }

        var so = new SerializedObject(ctrl);

        // InputActionAsset
        so.FindProperty("_inputAsset").objectReferenceValue = inputAsset;

        // Find UI elements by name in scene
        so.FindProperty("_menuItemContainer")
          .objectReferenceValue = GameObject.Find("MenuItemContainer")
                                             ?.GetComponent<RectTransform>();
        so.FindProperty("_menuItemText")
          .objectReferenceValue = GameObject.Find("MenuItemText")
                                             ?.GetComponent<TMPro.TextMeshProUGUI>();
        so.FindProperty("_arrowLeft")
          .objectReferenceValue = GameObject.Find("ArrowLeft")
                                             ?.GetComponent<TMPro.TextMeshProUGUI>();
        so.FindProperty("_arrowRight")
          .objectReferenceValue = GameObject.Find("ArrowRight")
                                             ?.GetComponent<TMPro.TextMeshProUGUI>();

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(ctrl.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("[WireTitleScene] Done. inputAsset=" + inputAsset.name
            + " menuItemText=" + so.FindProperty("_menuItemText").objectReferenceValue);
    }
}
