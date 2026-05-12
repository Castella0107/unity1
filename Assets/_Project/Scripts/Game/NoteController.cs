using UnityEngine;

[DisallowMultipleComponent]
public class NoteController : MonoBehaviour
{
    // ── State ──────────────────────────────────────────────────────────────
    public NoteData Data       { get; private set; }
    public bool     IsActive   => gameObject.activeSelf;
    public NoteType PoolType   { get; set; }   // set by NotePool on pre-warm
    public bool     IsHit      { get; private set; }

    // ── Lane colour table (indexed by (int)LaneRef) ────────────────────────
    protected static readonly Color[] LaneColors =
    {
        new Color(1.00f, 0.27f, 0.27f), // Lane0 – red
        new Color(1.00f, 0.87f, 0.27f), // Lane1 – yellow
        new Color(0.27f, 1.00f, 0.53f), // Lane2 – green
        new Color(0.27f, 0.53f, 1.00f), // Lane3 – blue
        new Color(1.00f, 0.53f, 0.10f), // FxL   – orange
        new Color(1.00f, 0.53f, 0.10f), // FxR   – orange
    };

    // ── Cached components ──────────────────────────────────────────────────
    protected MeshRenderer[]      _renderers;
    private   MaterialPropertyBlock _propBlock;

    protected virtual void Awake()
    {
        _renderers = GetComponentsInChildren<MeshRenderer>(true);
        _propBlock = new MaterialPropertyBlock();
    }

    // ── Public API ─────────────────────────────────────────────────────────

    public virtual void Initialize(NoteData data)
    {
        Data  = data;
        IsHit = false;
        gameObject.SetActive(true);

        _propBlock.SetColor("_BaseColor", LaneColors[(int)data.Lane]);
        foreach (var r in _renderers)
            r.SetPropertyBlock(_propBlock);
    }

    /// <summary>
    /// Called every frame by the gameplay manager.
    /// dtMs &gt; 0 = note is in the future (in front of camera).
    /// </summary>
    public virtual void UpdatePosition(double currentVisualMs, float scrollSpeed)
    {
        if (Data == null) return;
        double dtMs = Data.TimeMs - currentVisualMs;
        float  z    = (float)(dtMs / 1000.0 * scrollSpeed);
        transform.localPosition = new Vector3(LaneLayout.GetX(Data.Lane), 0f, LaneLayout.JudgmentLineZ + z);
    }

    public virtual void OnHit(Judgment j)
    {
        IsHit = true;
        gameObject.SetActive(false);
    }

    public virtual void OnMiss()
    {
        IsHit = true;
        gameObject.SetActive(false);
    }

    public void SetHit() => IsHit = true;
}
