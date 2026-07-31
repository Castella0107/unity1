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
    /// <summary>ホールドを取れていない間 (頭ミス、またはガード超過の離上中) の視覚状態。若干グレーに落とす。</summary>
    public bool     IsHoldDropped { get; private set; }

    // レーン色はユーザーがカスタマイズ可能 (Config「色」タブ)。正本は GameColorSettings。

    // ── Cached components ──────────────────────────────────────────────────
    protected MeshRenderer[]      _renderers;
    protected MaterialPropertyBlock _propBlock;
    protected Color               _laneColor = Color.white;
    private   FxArcNote           _fxArc;       // FX レーン用の円弧メッシュ (遅延生成)
    private   bool                _baseVisible = true;

    protected virtual void Awake()
    {
        _renderers = GetComponentsInChildren<MeshRenderer>(true);
        _propBlock = new MaterialPropertyBlock();
        // モック §0 のレイヤー順: 中央ノーツは中央レーン装飾 (sortingOrder 30〜34) の上。
        foreach (var r in _renderers) r.sortingOrder = 40;
    }

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>ノーツデータを割り当ててノートを初期化し、レーン色を適用して表示する。</summary>
    public virtual void Initialize(NoteData data)
    {
        Data  = data;
        IsHit = false;
        IsHoldDropped = false;   // プール再利用でグレー表示が残らないようリセット
        gameObject.SetActive(true);

        _laneColor = GameColorSettings.NoteColor((int)data.Lane);
        _propBlock.SetColor("_BaseColor", _laneColor);
        foreach (var r in _renderers)
            r.SetPropertyBlock(_propBlock);
        _fxArc?.SetColor(_laneColor);
    }

    /// <summary>取れていないホールドの視覚状態を切り替える (FX 円弧の色も即時更新)。</summary>
    public void SetHoldDropped(bool dropped)
    {
        if (IsHoldDropped == dropped) return;
        IsHoldDropped = dropped;
        _fxArc?.SetColor(EffectiveLaneColor);   // 中央レーンは毎フレームの ApplyLaneColor が反映する
    }

    /// <summary>同時押しノーツの色 (GameColorSettings.ChordColor、既定 黄) を適用する。
    /// NoteScroller がスポーン時に同時刻ノーツ (2 個以上) へ設定する。</summary>
    public void SetChord(bool isChord)
    {
        if (!isChord) return;   // Initialize でレーン色に戻っているため false は何もしない
        _laneColor = GameColorSettings.ChordColor;
        _propBlock.SetColor("_BaseColor", EffectiveLaneColor);
        foreach (var r in _renderers)
            r.SetPropertyBlock(_propBlock);
        _fxArc?.SetColor(EffectiveLaneColor);
    }

    /// <summary>適用するノーツ色。取れていないホールドは若干グレー寄りに落とす。</summary>
    protected Color EffectiveLaneColor => IsHoldDropped
        ? Color.Lerp(_laneColor, new Color(0.42f, 0.42f, 0.42f), 0.55f)
        : _laneColor;

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

        // FX ノーツは VP 中心の円弧線分 (PLAYFIELD_SPEC.md §3)。直線バーで描いてはならない。
        // z (判定からの奥行きオフセット) をスペック px 距離へ換算し、半径 R(z) の円弧を描く。
        if (IsFxLane(Data.Lane) && FxSectorGeometry.Ready)
        {
            transform.localPosition = Vector3.zero; // メッシュはワールド座標で構築される
            transform.localRotation = Quaternion.identity;
            EnsureFxArc().UpdateTap(Data.Lane == LaneRef.FxR, z, 0.035f);
            SetBaseRenderersEnabled(false);
            return;
        }
        _fxArc?.SetVisible(false);
        SetBaseRenderersEnabled(true);

        // Flat straight lane (main lanes, or FX fallback when no camera is available).
        transform.localPosition = new Vector3(LaneLayout.GetX(Data.Lane), 0f, LaneLayout.JudgmentLineZ + z);
        transform.localRotation = Quaternion.identity; // プール再利用時に FX の回転が残らないようリセット

        // Tap visual width is driven by the lane note width (FX lanes are wider) so prefab
        // scale needn't be tuned per lane. HoldNoteController overrides this and sizes its
        // own head/body/tail, so only Tap / FxTap are affected here.
        ApplyLaneWidth();
        ApplyLaneColor();
    }

    /// <summary>
    /// レーン色を全不透明で適用する。距離フェードは FAR_WALL_SPEC で廃止 —
    /// 奥端の消え方は透明遮蔽壁 (シェーダの _WallClipOn/_PlayfieldWallZ クリップ) が担う。
    /// </summary>
    protected void ApplyLaneColor()
    {
        _propBlock.SetColor("_BaseColor", EffectiveLaneColor);
        foreach (var r in _renderers)
            r.SetPropertyBlock(_propBlock);
    }

    /// <summary>FX レーン(FxL / FxR)か。</summary>
    protected static bool IsFxLane(LaneRef lane) => lane == LaneRef.FxL || lane == LaneRef.FxR;

    /// <summary>FX 円弧メッシュを遅延生成して返す。</summary>
    protected FxArcNote EnsureFxArc()
    {
        if (_fxArc == null)
        {
            _fxArc = FxArcNote.Create(transform);
            if (Data != null) _fxArc.SetColor(EffectiveLaneColor);
        }
        return _fxArc;
    }

    /// <summary>FX 円弧を非表示にする (直線描画へ切り替わるフレーム用)。</summary>
    protected void HideFxArc() => _fxArc?.SetVisible(false);

    /// <summary>プレハブ既定の MeshRenderer 群の表示/非表示を切り替える (FX 円弧モード用)。</summary>
    protected void SetBaseRenderersEnabled(bool on)
    {
        if (_baseVisible == on) return;
        _baseVisible = on;
        foreach (var r in _renderers)
            if (r != null) r.enabled = on;
    }

    /// <summary>各 MeshRenderer の localScale.x をレーンのノート幅に合わせる。</summary>
    protected void ApplyLaneWidth()
    {
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
