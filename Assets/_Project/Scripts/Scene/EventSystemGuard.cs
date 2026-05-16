using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 別の EventSystem が既に存在する場合、この GameObject を破棄する MonoBehaviour。
/// アディティブシーンロード中の "2つのEventSystemが存在します" 警告を防止する。
/// </summary>
// Destroys this GameObject if another EventSystem already exists.
// Prevents "There are 2 event systems" warnings during additive scene loading.
[RequireComponent(typeof(EventSystem))]
public sealed class EventSystemGuard : MonoBehaviour
{
    void Awake()
    {
        if (EventSystem.current != null && EventSystem.current.gameObject != gameObject)
            Destroy(gameObject);
    }
}
