using UnityEngine;

public class HoldNoteController : NoteController
{
    [SerializeField] Transform _headTransform;
    [SerializeField] Transform _bodyTransform;
    [SerializeField] Transform _tailTransform;

    private const float NoteHeight = 0.05f;

    public override void UpdatePosition(double currentVisualMs, float scrollSpeed)
    {
        if (Data == null) return;

        float startZ = LaneLayout.JudgmentLineZ + (float)((Data.TimeMs - currentVisualMs)                   / 1000.0 * scrollSpeed);
        float endZ   = LaneLayout.JudgmentLineZ + (float)((Data.TimeMs + Data.DurationMs - currentVisualMs) / 1000.0 * scrollSpeed);
        float width  = LaneLayout.GetNoteWidth(Data.Lane);
        float x      = LaneLayout.GetX(Data.Lane);

        // Root at lane X, Z = 0 (children use world-space Z via localPosition)
        transform.localPosition = new Vector3(x, 0f, 0f);

        if (_headTransform != null)
            _headTransform.localPosition = new Vector3(0f, 0f, startZ);

        if (_bodyTransform != null)
        {
            _bodyTransform.localPosition = new Vector3(0f, 0f, (startZ + endZ) / 2f);
            _bodyTransform.localScale    = new Vector3(width, NoteHeight, Mathf.Max(0.001f, endZ - startZ));
        }

        if (_tailTransform != null)
            _tailTransform.localPosition = new Vector3(0f, 0f, endZ);
    }
}
