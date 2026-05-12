using UnityEngine;

// Disables the AudioListener on this camera if _Persistent already has one.
// Prevents "There are 2 audio listeners" warnings during additive scene loading.
// Uses enabled=false (not Destroy) to remain reversible and avoid RequireComponent conflicts.
public sealed class AudioListenerGuard : MonoBehaviour
{
    void Awake()
    {
        var listener = GetComponent<AudioListener>();
        if (listener == null) return;

        var all = FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (all.Length > 1)
            listener.enabled = false;
    }
}
