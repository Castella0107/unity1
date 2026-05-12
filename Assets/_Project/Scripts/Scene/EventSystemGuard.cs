using UnityEngine;
using UnityEngine.EventSystems;

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
