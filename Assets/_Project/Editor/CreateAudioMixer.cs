using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Assets/_Project/Audio/MainAudioMixer.mixer を生成し、Master / Music / SFX グループを追加するエディターオンリーのヘルパークラス。
/// </summary>
public static class CreateAudioMixer
{
    /// <summary>MainAudioMixer.mixer を生成し Master / Music / SFX グループを追加する。</summary>
    [MenuItem("Tools/Create MainAudioMixer")]
    public static void Create()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Project/Audio"))
            AssetDatabase.CreateFolder("Assets/_Project", "Audio");

        const string path = "Assets/_Project/Audio/MainAudioMixer.mixer";
        if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
        {
            Debug.Log("[CreateAudioMixer] Already exists: " + path);
            return;
        }

        // Access internal AudioMixerController via reflection
        var editorAssembly = typeof(AudioImporter).Assembly;
        var controllerType = editorAssembly.GetType("UnityEditor.Audio.AudioMixerController");
        if (controllerType == null)
        {
            Debug.LogError("[CreateAudioMixer] AudioMixerController not found — create mixer manually via Assets > Create > Audio Mixer");
            return;
        }

        var controller = ScriptableObject.CreateInstance(controllerType);
        controller.name = "MainAudioMixer";
        AssetDatabase.CreateAsset(controller, path);

        // Add Music and SFX groups under Master
        var masterGroupProp = controllerType.GetProperty("masterGroup",
            BindingFlags.Public | BindingFlags.Instance);
        var masterGroup = masterGroupProp?.GetValue(controller);

        var addGroupMethod = controllerType.GetMethod("CreateNewGroup",
            BindingFlags.Public | BindingFlags.Instance);

        if (addGroupMethod != null && masterGroup != null)
        {
            // Unity 6 signature: CreateNewGroup(string name, bool undoable)
            try
            {
                addGroupMethod.Invoke(controller, new object[] { "Music", false });
                addGroupMethod.Invoke(controller, new object[] { "SFX",   false });
                Debug.Log("[CreateAudioMixer] Added Music and SFX groups");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[CreateAudioMixer] Could not add groups: " + e.Message
                    + " — add Music/SFX groups manually in the Mixer window");
            }
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CreateAudioMixer] Created at " + path + " — open it and expose Volume params as MasterVolumeDb / MusicVolumeDb / SfxVolumeDb");
    }
}
