using UnityEngine;

/// <summary>
/// Spawns vertical lane-divider lines at Awake.
/// The LaneDivider prefab must have two children:
///   "QuadVertical"   - rotation (0,90,0)  - visible from left/right
///   "QuadHorizontal" - rotation (90,0,0)  - visible from above (camera angled down)
/// Combining both ensures the center divider at x=0 is always visible.
/// </summary>
public class LaneVisuals : MonoBehaviour
{
    [Header("Divider Prefab")]
    [SerializeField] GameObject _dividerPrefab;

    [Header("Divider Z range")]
    // Near Z is always LaneLayout.JudgmentLineZ — kept in sync automatically
    [SerializeField] float _dividerFarZ  = 22f;

    [Header("Heights (vertical extent)")]
    [SerializeField] float _outerHeight  = 0.10f;
    [SerializeField] float _centerHeight = 0.18f;  // center taller for emphasis

    [Header("Horizontal width (floor quad)")]
    [SerializeField] float _floorWidth = 0.025f;

    [Header("Length ratios (0-1)")]
    [SerializeField] float _outerLengthRatio = 0.85f;

    void Awake()
    {
        if (_dividerPrefab == null)
        {
            Debug.LogWarning("[LaneVisuals] _dividerPrefab not assigned.");
            return;
        }
        SpawnMainLaneDividers();
    }

    void SpawnMainLaneDividers()
    {
        // 6 lanes: FX (±1.2 wide) flank 4 main lanes (±1.0). Field spans X -3.2..+3.2.
        // 7 boundary lines: outer FX edges at ±3.2, inner boundaries at integer X.
        SpawnDivider(-3.2f, _outerHeight,  _outerLengthRatio);  // FxL outer edge
        SpawnDivider(-2.0f, _outerHeight,  _outerLengthRatio);  // FxL|Lane0
        SpawnDivider(-1.0f, _outerHeight,  _outerLengthRatio);  // Lane0|Lane1
        SpawnDivider( 0.0f, _centerHeight, 1.0f);               // Lane1|Lane2 center
        SpawnDivider( 1.0f, _outerHeight,  _outerLengthRatio);  // Lane2|Lane3
        SpawnDivider( 2.0f, _outerHeight,  _outerLengthRatio);  // Lane3|FxR
        SpawnDivider( 3.2f, _outerHeight,  _outerLengthRatio);  // FxR outer edge
    }

    void SpawnDivider(float xPos, float height, float lengthRatio)
    {
        var go = Instantiate(_dividerPrefab, transform);
        go.name = $"Divider_X{xPos:F2}";

        float nearZ   = LaneLayout.JudgmentLineZ;
        float fullLen = _dividerFarZ - nearZ;
        float length  = fullLen * lengthRatio;
        float midZ    = nearZ + length * 0.5f;

        go.transform.localPosition = new Vector3(xPos, 0f, midZ);
        go.transform.localScale    = Vector3.one;

        // QuadVertical (0,90,0):
        //   x-scale -> world Z (depth), y-scale -> world Y (height)
        //   Offset upward so bottom edge sits exactly on the floor
        var qv = go.transform.Find("QuadVertical");
        if (qv != null)
        {
            qv.localScale    = new Vector3(length, height, 1f);
            qv.localPosition = new Vector3(0f, height * 0.5f, 0f);
        }

        // QuadHorizontal (90,0,0):
        //   x-scale -> world X (narrow width), y-scale -> world Z (depth)
        //   Tiny Y offset above floor to avoid z-fighting with ground mesh
        var qh = go.transform.Find("QuadHorizontal");
        if (qh != null)
        {
            qh.localScale    = new Vector3(_floorWidth, length, 1f);
            qh.localPosition = new Vector3(0f, 0.005f, 0f);
        }
    }
}
