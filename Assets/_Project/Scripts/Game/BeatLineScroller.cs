using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Scrolls horizontal beat / measure grid lines across the playfield in sync with the notes.
// A line at song-time T sits at the same Z as a note at T:
//   z = LaneLayout.JudgmentLineZ + (T - VisualTimeMs)/1000 * scrollSpeed
// Beat lines (1拍ごと) are faint; measure lines (1小節ごと / downbeat) are stronger and thicker.
// Line times follow the same timesig formula as scoring (BpmTimeline), so the grid lines up
// with the notes for any time signature. Lines are pooled flat quads parented in lane-space.

/// <summary>
/// 譜面再生中、ノートと同じスクロールに乗せて拍線・小節線を描画するランタイムコンポーネント。
/// 1 拍ごとに薄い線、1 小節(小節頭)ごとに濃く太い線を引く。線の時刻は BpmTimeline で
/// 解決するためスコア(拍子)と同一格子に並ぶ。StageInitializer が生成・初期化する。
/// </summary>
public class BeatLineScroller : MonoBehaviour
{
    /// <summary>直近に生成されたインスタンス(StageInitializer の Unbind 用)。</summary>
    public static BeatLineScroller Instance { get; private set; }

    // 床からわずかに浮かせて地面との z-fighting を避ける(ノートは y=0 なので線はその下)。
    const float LineY            = 0.012f;
    const float BeatThicknessZ   = 0.03f;
    const float MeasureThicknessZ= 0.07f;
    static readonly Color BeatColor    = new Color(1f, 1f, 1f, 0.13f);   // 薄い拍線
    static readonly Color MeasureColor = new Color(1f, 1f, 1f, 0.42f);   // 濃い小節線

    AudioConductor _conductor;
    NoteScroller   _scroller;

    double[] _lineTimes;     // 昇順
    bool[]   _isMeasure;

    readonly List<Transform>           _pool      = new List<Transform>();
    readonly List<MeshRenderer>        _renderers = new List<MeshRenderer>();
    MaterialPropertyBlock              _propBlock;
    Material                           _material;

    void Awake()
    {
        Instance   = this;
        _propBlock = new MaterialPropertyBlock();
        _material  = CreateLineMaterial();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_material != null) Destroy(_material);
    }

    // ── Public API ──────────────────────────────────────────────────────────────

    /// <summary>譜面の拍/小節線時刻を事前計算し、スクロール用の参照をバインドする。</summary>
    public void Initialize(AudioConductor conductor, ChartData chart, NoteScroller scroller)
    {
        _conductor = conductor;
        _scroller  = scroller;
        BuildLineTimes(chart);
        HideAll();
    }

    /// <summary>全ての線を非表示にする(セッション終了時)。</summary>
    public void Clear()
    {
        _lineTimes = null;
        _isMeasure = null;
        HideAll();
    }

    // ── Line-time precompute (timesig-aware measure scan) ─────────────────────────

    // 小節走査方式。ChartEditor の TimelineRenderer.BuildBeatLines と同じ格子(=スコアと同一公式)。
    // 小節長 = 四分音符長 × 分子 × (4/分母)、1 小節を「分子」個の拍に分割。bpm/timesig アンカーで再整列。
    void BuildLineTimes(ChartData chart)
    {
        _lineTimes = null;
        _isMeasure = null;
        if (chart == null || chart.Notes == null || chart.Notes.Count == 0) return;

        double endMs = 0;
        foreach (var n in chart.Notes)
        {
            bool isHold = n.Type == NoteType.Hold || n.Type == NoteType.FxHold;
            double e = n.TimeMs + (isHold ? n.DurationMs : 0);
            if (e > endMs) endMs = e;
        }
        if (endMs <= 0) return;

        var events  = chart.Events;
        BpmTimeline bpm = (events != null && events.Count > 0) ? new BpmTimeline(events) : null;

        var anchors = new List<double>();
        if (events != null)
            foreach (var e in events)
                if (e != null && (e.Type == "bpm" || e.Type == "timesig")) anchors.Add(e.TimeMs);
        anchors.Sort();

        var times    = new List<double>();
        var measures = new List<bool>();

        double mStart = 0.0;
        int guard = 0;
        while (mStart < endMs - 0.001 && guard++ < 200000)
        {
            double bpmNow = bpm != null ? bpm.GetBpmAt(mStart) : 120.0;
            if (bpmNow <= 0) bpmNow = 120.0;
            int num = 4, den = 4;
            if (bpm != null) { var sig = bpm.GetTimeSignatureAt(mStart); num = sig.num; den = sig.den; }
            if (num <= 0) num = 4;
            if (den <= 0) den = 4;

            double quarterMs = 60000.0 / bpmNow;
            double denBeatMs = quarterMs * 4.0 / den;   // 1 拍(分母音価)の長さ
            double measureMs = denBeatMs * num;          // 1 小節 = 分子拍
            if (measureMs <= 0.001) break;

            double mEnd = mStart + measureMs;
            for (int ai = 0; ai < anchors.Count; ai++)
            {
                double a = anchors[ai];
                if (a > mStart + 0.5 && a < mEnd - 0.001) { mEnd = a; break; }   // アンカーで切って再整列
            }
            if (mEnd > endMs) mEnd = endMs;

            for (int bi = 0; ; bi++)
            {
                double bStart = mStart + bi * denBeatMs;
                if (bStart >= mEnd - 0.001) break;
                times.Add(bStart);
                measures.Add(bi == 0);   // 各小節の先頭拍 = 小節線
            }

            mStart = mEnd;
        }

        _lineTimes = times.ToArray();
        _isMeasure = measures.ToArray();
    }

    // ── Frame update ─────────────────────────────────────────────────────────────

    void Update()
    {
        if (_conductor == null || _lineTimes == null || _lineTimes.Length == 0) return;
        if (!_conductor.IsPlaying && !_conductor.IsPaused) { HideAll(); return; }

        double visualMs = _conductor.VisualTimeMs;
        float  speed    = Mathf.Max(0.1f, _scroller != null ? _scroller.ScrollSpeed : 4.5f);

        // 可視 Z 窓 [NoteDespawnZ, NoteSpawnZ] に対応する時刻窓。
        double aheadMs  = (LaneLayout.NoteSpawnZ   - LaneLayout.JudgmentLineZ) / speed * 1000.0;
        double behindMs = (LaneLayout.JudgmentLineZ - LaneLayout.NoteDespawnZ) / speed * 1000.0;
        double minT = visualMs - behindMs;
        double maxT = visualMs + aheadMs;

        int start = LowerBound(_lineTimes, minT);   // seek にも追従(毎フレーム二分探索)
        float denomZ = Mathf.Max(0.001f, LaneLayout.NoteSpawnZ - LaneLayout.JudgmentLineZ);

        int used = 0;
        for (int i = start; i < _lineTimes.Length; i++)
        {
            double t = _lineTimes[i];
            if (t > maxT) break;

            float z = LaneLayout.JudgmentLineZ + (float)((t - visualMs) / 1000.0 * speed);
            bool  measure = _isMeasure[i];

            var tr = GetQuad(used++);
            tr.localPosition = new Vector3(0f, LineY, z);
            tr.localScale    = new Vector3(LaneLayout.TotalWidth,
                                           measure ? MeasureThicknessZ : BeatThicknessZ, 1f);

            // 奥ほどフェード(判定ライン=不透明 → スポーン位置=透明)。
            float fade = Mathf.Clamp01(1f - (z - LaneLayout.JudgmentLineZ) / denomZ);
            Color c = measure ? MeasureColor : BeatColor;
            c.a *= fade;
            _propBlock.SetColor("_BaseColor", c);
            _renderers[used - 1].SetPropertyBlock(_propBlock);
        }

        for (int i = used; i < _pool.Count; i++)
            if (_pool[i].gameObject.activeSelf) _pool[i].gameObject.SetActive(false);
    }

    // ── Pool / material helpers ───────────────────────────────────────────────────

    Transform GetQuad(int index)
    {
        while (_pool.Count <= index) CreateQuad();
        var tr = _pool[index];
        if (!tr.gameObject.activeSelf) tr.gameObject.SetActive(true);
        return tr;
    }

    void CreateQuad()
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = "BeatLine" + _pool.Count;
        var col = q.GetComponent<Collider>();
        if (col != null) Destroy(col);
        q.transform.SetParent(transform, false);
        q.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // 床に寝かせて +Y を向く
        var r = q.GetComponent<MeshRenderer>();
        r.sharedMaterial      = _material;
        r.shadowCastingMode   = ShadowCastingMode.Off;
        r.receiveShadows      = false;
        r.lightProbeUsage     = LightProbeUsage.Off;
        _pool.Add(q.transform);
        _renderers.Add(r);
    }

    void HideAll()
    {
        for (int i = 0; i < _pool.Count; i++)
            if (_pool[i].gameObject.activeSelf) _pool[i].gameObject.SetActive(false);
    }

    // _BaseColor のアルファで透明度を出す URP Unlit 透過マテリアル(LaneHighlight と同設定・テクスチャ無し)。
    static Material CreateLineMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        var mat = new Material(shader);
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend",   0f);
        mat.SetFloat("_ZWrite",  0f);
        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_SURFACE_TYPE_OPAQUE");
        mat.renderQueue = (int)RenderQueue.Transparent;
        mat.SetColor("_BaseColor", Color.white);
        return mat;
    }

    // 昇順配列で value 以上が最初に現れる位置を返す。
    static int LowerBound(double[] arr, double value)
    {
        int lo = 0, hi = arr.Length;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            if (arr[mid] < value) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }
}
