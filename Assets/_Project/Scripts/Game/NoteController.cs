using UnityEngine;

[DisallowMultipleComponent]
/// <summary>
/// タップノートの基底コントローラー。NoteData を保持し、スクロール位置の更新・ヒット／ミス時の非表示処理を担う。
/// HoldNoteController がこのクラスを継承して長押しノートの挙動を拡張する。
/// レーンごとの色は LaneColors テーブルで定義し、MaterialPropertyBlock 経由でレンダラーに適用する。
/// </summary>
public class NoteController : MonoBehaviour
{
    // ── State ──────────────────────────────────────────────────────────────
    /// <summary>このノートが表すノーツデータ。</summary>
    public NoteData Data       { get; private set; }
    /// <summary>GameObject がアクティブ(使用中)か。</summary>
    public bool     IsActive   => gameObject.activeSelf;
    /// <summary>プール上の種別。NotePool が事前生成時に設定する。</summary>
    public NoteType PoolType   { get; set; }
    /// <summary>ヒット/ミス処理済みか。</summary>
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

    /// <summary>ノーツデータを割り当ててノートを初期化し、レーン色を適用して表示する。</summary>
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
    /// 毎フレーム呼ばれ、現在の視覚時刻とスクロール速度からノートの Z 位置を更新する。
    /// 時間差 &gt; 0 はノートが未来(カメラ前方)にあることを意味する。
    /// </summary>
    public virtual void UpdatePosition(double currentVisualMs, float scrollSpeed)
    {
        if (Data == null) return;
        double dtMs = Data.TimeMs - currentVisualMs;
        float  z    = (float)(dtMs / 1000.0 * scrollSpeed);
        transform.localPosition = new Vector3(LaneLayout.GetX(Data.Lane), 0f, LaneLayout.JudgmentLineZ + z);

        // Tap visual width is driven by the lane note width (FX lanes are wider) so prefab
        // scale needn't be tuned per lane. HoldNoteController overrides this and sizes its
        // own head/body/tail, so only Tap / FxTap are affected here.
        float width = LaneLayout.GetNoteWidth(Data.Lane);
        foreach (var r in _renderers)
        {
            var t = r.transform;
            var s = t.localScale;
            if (!Mathf.Approximately(s.x, width))
                t.localScale = new Vector3(width, s.y, s.z);
        }
    }

    /// <summary>ヒット時に呼ばれ、ヒット済みにして非表示にする。</summary>
    public virtual void OnHit(Judgment j)
    {
        IsHit = true;
        gameObject.SetActive(false);
    }

    /// <summary>ミス時に呼ばれ、処理済みにして非表示にする。</summary>
    public virtual void OnMiss()
    {
        IsHit = true;
        gameObject.SetActive(false);
    }

    /// <summary>非表示にせずヒット済みフラグだけ立てる。</summary>
    public void SetHit() => IsHit = true;
}
