using UnityEngine;

/// <summary>
/// ホールドノートの描画を担う NoteController サブクラス。
/// ヘッド・ボディ・テール の 3 つの Transform を使い、現在のビジュアル時刻とスクロール速度から
/// ノートの開始 Z・終了 Z・幅を計算して各 Transform のローカル座標とスケールを更新する。
/// </summary>
public class HoldNoteController : NoteController
{
    [SerializeField] Transform _headTransform;
    [SerializeField] Transform _bodyTransform;
    [SerializeField] Transform _tailTransform;

    private const float NoteHeight = 0.05f;

    /// <inheritdoc/>
    public override void UpdatePosition(double currentVisualMs, float scrollSpeed)
    {
        if (Data == null) return;

        float startZ = LaneLayout.JudgmentLineZ + (float)((Data.TimeMs - currentVisualMs)                   / 1000.0 * scrollSpeed);
        float endZ   = LaneLayout.JudgmentLineZ + (float)((Data.TimeMs + Data.DurationMs - currentVisualMs) / 1000.0 * scrollSpeed);
        float width  = LaneLayout.GetNoteWidth(Data.Lane);
        float x      = LaneLayout.GetX(Data.Lane);

        // Root at lane X, Z = 0 (children use world-space Z via localPosition)
        transform.localPosition = new Vector3(x, 0f, 0f);

        // Head/tail caps: match their width to the body so FX holds aren't wider than
        // their lane (prefab caps were baked for the old 2-unit-wide FX lanes). Y/Z kept.
        if (_headTransform != null)
        {
            _headTransform.localPosition = new Vector3(0f, 0f, startZ);
            var hs = _headTransform.localScale;
            _headTransform.localScale = new Vector3(width, hs.y, hs.z);
        }

        if (_bodyTransform != null)
        {
            _bodyTransform.localPosition = new Vector3(0f, 0f, (startZ + endZ) / 2f);
            _bodyTransform.localScale    = new Vector3(width, NoteHeight, Mathf.Max(0.001f, endZ - startZ));
        }

        if (_tailTransform != null)
        {
            _tailTransform.localPosition = new Vector3(0f, 0f, endZ);
            var ts = _tailTransform.localScale;
            _tailTransform.localScale = new Vector3(width, ts.y, ts.z);
        }
    }
}
