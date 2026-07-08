using UnityEngine;

[DisallowMultipleComponent]
/// <summary>
/// タップノートの基底コントローラー。NoteData を保持し、スクロール位置の更新・ヒット／ミス時の非表示処理を担う。
/// HoldNoteController がこのクラスを継承して長押しノートの挙動を拡張する。
/// レーンごとの色は GameColorSettings(ユーザーカスタマイズ可)から取得し、MaterialPropertyBlock 経由で適用する。
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

    // レーン色はユーザーがカスタマイズ可能 (Config「色」タブ)。正本は GameColorSettings。

    // ── Cached components ──────────────────────────────────────────────────
    protected MeshRenderer[]      _renderers;
    protected Vector3[]           _baseScales;   // プレハブ既定のローカルスケール(2D の厚み計算の基準)
    private   MaterialPropertyBlock _propBlock;

    // 2D(トップダウン)時、タップは奥行き(Z)が薄いと真上から線状に潰れるため厚くする倍率。
    // Z 方向を中心対称に拡大する(=判定はノーツ中央のまま)。大きいほど太いバーになる。
    protected const float Note2DDepthMul = 5f;

    protected virtual void Awake()
    {
        _renderers  = GetComponentsInChildren<MeshRenderer>(true);
        _baseScales = new Vector3[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
            _baseScales[i] = _renderers[i].transform.localScale;
        _propBlock = new MaterialPropertyBlock();
    }

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>ノーツデータを割り当ててノートを初期化し、レーン色を適用して表示する。</summary>
    public virtual void Initialize(NoteData data)
    {
        Data  = data;
        IsHit = false;
        gameObject.SetActive(true);

        _propBlock.SetColor("_BaseColor", GameColorSettings.NoteColor((int)data.Lane));
        foreach (var r in _renderers)
            r.SetPropertyBlock(_propBlock);
    }

    /// <summary>speed イベント未指定の従来呼び出し向けオーバーロード(等速)。</summary>
    public void UpdatePosition(double currentVisualMs, float scrollSpeed)
        => UpdatePosition(currentVisualMs, scrollSpeed, null);

    /// <summary>
    /// 毎フレーム呼ばれ、現在の視覚時刻とスクロール速度からノートの Z 位置を更新する。
    /// 時間差 &gt; 0 はノートが未来(カメラ前方)にあることを意味する。
    /// <paramref name="speed"/> が指定されると "speed" イベントの倍率を時間積分した
    /// 視覚距離でスクロールする(視覚専用・判定/スコアには影響しない)。null なら等速。
    /// </summary>
    public virtual void UpdatePosition(double currentVisualMs, float scrollSpeed, ScrollSpeedTimeline speed)
    {
        if (Data == null) return;
        double dtMs = speed != null ? speed.VisualDistanceMs(currentVisualMs, Data.TimeMs)
                                    : Data.TimeMs - currentVisualMs;
        float  z    = (float)(dtMs / 1000.0 * scrollSpeed);
        transform.localPosition = new Vector3(LaneLayout.GetX(Data.Lane), 0f, LaneLayout.JudgmentLineZ + z);

        // Tap visual width is driven by the lane note width (FX lanes are wider) so prefab
        // scale needn't be tuned per lane. HoldNoteController overrides this and sizes its
        // own head/body/tail, so only Tap / FxTap are affected here.
        float width    = LaneLayout.GetNoteWidth(Data.Lane);
        // 2D(トップダウン)ではタップの奥行きを厚くして見えるバーにする。中心対称スケールなので
        // ノーツ中央は Z 位置(=on-time で JudgmentLineZ)のまま → 判定はノーツ中央に一致する。
        float depthMul = StageInitializer.Is2DView ? Note2DDepthMul : 1f;
        for (int i = 0; i < _renderers.Length; i++)
        {
            var t = _renderers[i].transform;
            var b = _baseScales[i];
            float targetZ = b.z * depthMul;
            var s = t.localScale;
            if (!Mathf.Approximately(s.x, width) || !Mathf.Approximately(s.z, targetZ))
                t.localScale = new Vector3(width, b.y, targetZ);
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
