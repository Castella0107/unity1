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

    /// <summary>プールから再利用される際に頭/ボディ/テールを再表示する(消費で非表示にした名残を解除)。</summary>
    public override void Initialize(NoteData data)
    {
        base.Initialize(data);
        if (_headTransform != null) _headTransform.gameObject.SetActive(true);
        if (_bodyTransform != null) _bodyTransform.gameObject.SetActive(true);
        if (_tailTransform != null) _tailTransform.gameObject.SetActive(true);
    }

    /// <inheritdoc/>
    public override void UpdatePosition(double currentVisualMs, float scrollSpeed)
    {
        if (Data == null) return;

        float startZ = LaneLayout.JudgmentLineZ + (float)((Data.TimeMs - currentVisualMs)                   / 1000.0 * scrollSpeed);
        float endZ   = LaneLayout.JudgmentLineZ + (float)((Data.TimeMs + Data.DurationMs - currentVisualMs) / 1000.0 * scrollSpeed);
        float width  = LaneLayout.GetNoteWidth(Data.Lane);
        float x      = LaneLayout.GetX(Data.Lane);
        float judgeZ = LaneLayout.JudgmentLineZ;

        // Root at lane X, Z = 0 (children use world-space Z via localPosition)
        transform.localPosition = new Vector3(x, 0f, 0f);

        // Once the head is tapped (IsHit) the hold is "consumed" at the judgment line:
        // nothing is drawn past (below) the line. The head vanishes at the line, the body's
        // leading edge stays pinned there and shrinks while held, and the whole note is gone
        // once the tail reaches the line. Before the tap it renders normally (head approaches
        // and crosses the line; a missed head still scrolls past as usual).
        bool  consumed  = IsHit;
        float visStartZ = consumed ? Mathf.Max(startZ, judgeZ) : startZ;

        // Head/tail caps: match their width to the body so FX holds aren't wider than
        // their lane (prefab caps were baked for the old 2-unit-wide FX lanes). Y/Z kept.
        if (_headTransform != null)
        {
            bool showHead = !(consumed && startZ <= judgeZ);
            _headTransform.gameObject.SetActive(showHead);
            if (showHead)
            {
                _headTransform.localPosition = new Vector3(0f, 0f, startZ);
                var hs = _headTransform.localScale;
                _headTransform.localScale = new Vector3(width, hs.y, hs.z);
            }
        }

        if (_bodyTransform != null)
        {
            float len      = endZ - visStartZ;
            bool  showBody = len > 0.001f;
            _bodyTransform.gameObject.SetActive(showBody);
            if (showBody)
            {
                _bodyTransform.localPosition = new Vector3(0f, 0f, (visStartZ + endZ) / 2f);
                _bodyTransform.localScale    = new Vector3(width, NoteHeight, len);
            }
        }

        if (_tailTransform != null)
        {
            bool showTail = !(consumed && endZ <= judgeZ);
            _tailTransform.gameObject.SetActive(showTail);
            if (showTail)
            {
                _tailTransform.localPosition = new Vector3(0f, 0f, endZ);
                var ts = _tailTransform.localScale;
                _tailTransform.localScale = new Vector3(width, ts.y, ts.z);
            }
        }
    }
}
