using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Scrolls horizontal beat / measure grid lines across the playfield in sync with the notes.
// A line at song-time T sits at the same Z as a note at T:
//   z = LaneLayout.JudgmentLineZ + (T - VisualTimeMs)/1000 * scrollSpeed
// Beat lines (1拍ごと) are faint and span the centre track only. Measure lines (1小節ごと /
// downbeat) are stronger and additionally drawn on both FX wings as concentric arcs at the
// same song-time radius NoteRadius(z) (seam-matched to the centre lane) — so the measure
// connecting DFJK lanes and the FX lanes, moving at note (hi-speed) speed.
// Line times follow the same timesig formula as scoring (BpmTimeline), so the grid lines up
// with the notes for any time signature. Lines are pooled flat quads / arc meshes.

/// <summary>
/// 譜面再生中、ノートと同じスクロールに乗せて拍線・小節線を描画するランタイムコンポーネント。
/// 1 拍ごとに薄い線 (中央のみ)、1 小節(小節頭)ごとに濃い細線を中央トラック+FX 両翼の円弧で
/// 「つながった 1 本の線」として引く。線の時刻は BpmTimeline で解決するためスコア(拍子)と
/// 同一格子に並ぶ。StageInitializer が生成・初期化する。
/// </summary>
public class BeatLineScroller : MonoBehaviour
{
    /// <summary>直近に生成されたインスタンス(StageInitializer の Unbind 用)。</summary>
    public static BeatLineScroller Instance { get; private set; }

    // 床からわずかに浮かせて地面との z-fighting を避ける(ノートは y=0 なので線はその下)。
    const float LineY            = 0.012f;
    const float FxArcYLift       = 0.02f;
    // 中央トラックの実幅 (ワールド、レーン端 x≈±2.0)。旧 TotalWidth=6.4 はフラット時代の値で
    // プレイフィールド ではトラック外へはみ出すため使わない。
    const float TrackWidth       = 4.0f;
    const float BeatThicknessZ   = 0.025f;
    const float MeasureThicknessZ= 0.045f;
    // FX 円弧線の全幅 (プレイフィールド px、判定弧時)。奥は r/JudgeRadius で細く (ノーツと同じ遠近則)。
    const float FxArcWidthPx     = 2.4f;
    const int   FxArcSegs        = 24;
    static readonly Color BeatColor    = new Color(1f, 1f, 1f, 0.13f);   // 薄い拍線
    static readonly Color MeasureColor = new Color(1f, 1f, 1f, 0.42f);   // 濃い小節線

    AudioConductor _conductor;
    NoteScroller   _scroller;

    double[] _lineTimes;     // 昇順
    double[] _lineVis;       // 各線の視覚位置 VisualPos(time)(speed 演出反映)。昇順。
    bool[]   _isMeasure;
    ScrollSpeedTimeline _speed;   // ノートと共有するスクロール速度演出(視覚専用)

    readonly List<Transform>           _pool      = new List<Transform>();
    readonly List<MeshRenderer>        _renderers = new List<MeshRenderer>();
    MaterialPropertyBlock              _propBlock;
    Material                           _material;

    // FX 両翼の小節円弧 (1 小節線 = 両翼まとめて 1 メッシュ)
    readonly List<MeshFilter>   _fxPool      = new List<MeshFilter>();
    readonly List<MeshRenderer> _fxRenderers = new List<MeshRenderer>();
    Material                    _fxMaterial;

    void Awake()
    {
        Instance   = this;
        _propBlock = new MaterialPropertyBlock();
        // 中央: 2633 = 中央トラックデカール (2630-2632) の上・キャップ/ノーツの下。
        // FX: 2615 = FX 面/縁/フラッシュの上・FX ノーツ (2616-2617)/判定弧 (2618-2619) の下。
        _material   = CreateLineMaterial(2633);
        _material.SetFloat("_WallClipOn", 1f);   // 壁より奥へ太さ分はみ出さない (FAR_WALL_SPEC)
        _fxMaterial = CreateLineMaterial(2615);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_material   != null) Destroy(_material);
        if (_fxMaterial != null) Destroy(_fxMaterial);
        foreach (var mf in _fxPool)
            if (mf != null && mf.sharedMesh != null) Destroy(mf.sharedMesh);
    }

    // ── Public API ──────────────────────────────────────────────────────────────

    /// <summary>譜面の拍/小節線時刻を事前計算し、スクロール用の参照をバインドする。</summary>
    public void Initialize(AudioConductor conductor, ChartData chart, NoteScroller scroller)
    {
        _conductor = conductor;
        _scroller  = scroller;
        _speed     = scroller != null ? scroller.Speed : null;
        BuildLineTimes(chart);
        BuildLineVisualPositions();
        HideAll();
    }

    /// <summary>各線の視覚位置(speed 倍率を積分した位置)を前計算する。speed 無しなら時刻と一致。</summary>
    void BuildLineVisualPositions()
    {
        _lineVis = null;
        if (_lineTimes == null) return;
        _lineVis = new double[_lineTimes.Length];
        for (int i = 0; i < _lineTimes.Length; i++)
            _lineVis[i] = _speed != null ? _speed.VisualPos(_lineTimes[i]) : _lineTimes[i];
    }

    /// <summary>全ての線を非表示にする(セッション終了時)。</summary>
    public void Clear()
    {
        _lineTimes = null;
        _lineVis   = null;
        _isMeasure = null;
        _speed     = null;
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

        // 可視 Z 窓 [NoteDespawnZ, NoteSpawnZ] を「視覚距離(ms 換算)」窓に変換し、
        // 各線の視覚位置 _lineVis と比較する。speed 演出に追従して可視範囲が伸縮する。
        double curVis   = _speed != null ? _speed.VisualPos(visualMs) : visualMs;
        double aheadMs  = (LaneLayout.NoteSpawnZ   - LaneLayout.JudgmentLineZ) / speed * 1000.0;
        double behindMs = (LaneLayout.JudgmentLineZ - LaneLayout.NoteDespawnZ) / speed * 1000.0;
        double minVis = curVis - behindMs;
        double maxVis = curVis + aheadMs;

        int start = LowerBound(_lineVis, minVis);   // seek にも追従(毎フレーム二分探索)
        bool fxReady = FxSectorGeometry.Ready;

        int used = 0, fxUsed = 0;
        for (int i = start; i < _lineVis.Length; i++)
        {
            double vis = _lineVis[i];
            if (vis > maxVis) break;

            float z = LaneLayout.JudgmentLineZ + (float)((vis - curVis) / 1000.0 * speed);
            float zFromJudge = z - LaneLayout.JudgmentLineZ;
            bool  measure = _isMeasure[i];

            // FAR_WALL_SPEC: フェードは廃止。中央は壁 (WallZCenterWorld) より奥を描かない。
            bool centerVisible = zFromJudge <= FxSectorGeometry.WallZCenterWorld;
            if (centerVisible)
            {
                var tr = GetQuad(used++);
                tr.localPosition = new Vector3(0f, LineY, z);
                tr.localScale    = new Vector3(TrackWidth,
                                               measure ? MeasureThicknessZ : BeatThicknessZ, 1f);
                _propBlock.SetColor("_BaseColor", measure ? MeasureColor : BeatColor);
                _renderers[used - 1].SetPropertyBlock(_propBlock);
            }

            // 小節線: FX 両翼に同時刻の円弧 (ノーツと同じ NoteRadius(z) — シーム整合 + ハイスピード同調)。
            // 壁 (r<WallR) の裏は描かない。中央側が見えていれば途中で折れて中央線とつなぐ。
            if (measure && fxReady)
            {
                float r = FxSectorGeometry.NoteRadius(zFromJudge);
                if (r > FxSectorGeometry.WallR)
                {
                    var mf = GetFxArc(fxUsed++);
                    BuildMeasureMesh(mf.sharedMesh, r, z, MeasureColor.a, withBridge: centerVisible);
                }
            }
        }

        for (int i = used; i < _pool.Count; i++)
            if (_pool[i].gameObject.activeSelf) _pool[i].gameObject.SetActive(false);
        for (int i = fxUsed; i < _fxPool.Count; i++)
            if (_fxPool[i].gameObject.activeSelf) _fxPool[i].gameObject.SetActive(false);
    }

    // ── FX measure-arc mesh (+中央線への折れブリッジ) ─────────────────────────────

    // 両翼の円弧帯 + (中央線が見えていれば) 円弧の内端 θ=130° と中央線の端をつなぐ
    // 折れ線ブリッジを 1 メッシュに構築する。頂点はワールド座標 (FloorPoint)。
    // 幅はノーツと同じ遠近則 (r/465) でスケールし、θ は扇形全幅 130°〜170°。
    void BuildMeasureMesh(Mesh mesh, float r, float zWorld, float alpha, bool withBridge)
    {
        float halfW = 0.5f * FxArcWidthPx * Mathf.Min(r / FxSectorGeometry.JudgeRadius, 1.5f);
        float r0 = Mathf.Max(r - halfW, FxSectorGeometry.WallR);   // 壁より奥は描かない
        float r1 = r + halfW;

        int arcVerts    = 2 * (FxArcSegs + 1) * 2;
        int bridgeVerts = withBridge ? 8 : 0;
        var verts = new Vector3[arcVerts + bridgeVerts];
        var cols  = new Color[verts.Length];
        var tris  = new int[FxArcSegs * 6 * 2 + (withBridge ? 12 : 0)];
        var col   = new Color(1f, 1f, 1f, alpha);

        int v = 0, t = 0;
        for (int wing = 0; wing < 2; wing++)
        {
            bool right = wing == 1;
            int baseV = v;
            for (int i = 0; i <= FxArcSegs; i++)
            {
                float th = Mathf.Lerp(FxSectorGeometry.SectorThetaMin,
                                      FxSectorGeometry.SectorThetaMax, (float)i / FxArcSegs);
                verts[v]     = FxSectorGeometry.FloorPoint(right, th, r0, FxArcYLift);
                verts[v + 1] = FxSectorGeometry.FloorPoint(right, th, r1, FxArcYLift);
                cols[v] = cols[v + 1] = col;
                v += 2;
            }
            for (int i = 0; i < FxArcSegs; i++)
            {
                int a0 = baseV + i * 2;
                tris[t++] = a0;     tris[t++] = a0 + 2; tris[t++] = a0 + 1;
                tris[t++] = a0 + 1; tris[t++] = a0 + 2; tris[t++] = a0 + 3;
            }
        }

        if (withBridge)
        {
            // 中央線の左右端 (x=±TrackWidth/2) と、各翼の円弧内端 (θ=130°) を直線でつなぐ。
            float half = MeasureThicknessZ * 0.5f;
            for (int wing = 0; wing < 2; wing++)
            {
                bool right = wing == 1;
                var a = new Vector3(right ? TrackWidth * 0.5f : -TrackWidth * 0.5f, FxArcYLift, zWorld);
                var b = FxSectorGeometry.FloorPoint(right, FxSectorGeometry.SectorThetaMin,
                                                    (r0 + r1) * 0.5f, FxArcYLift);
                var d = b - a; d.y = 0f;
                if (d.sqrMagnitude < 1e-6f) d = Vector3.right;
                var n = Vector3.Cross(Vector3.up, d.normalized) * half;
                int b0 = v;
                verts[v]     = a - n; verts[v + 1] = a + n;
                verts[v + 2] = b - n; verts[v + 3] = b + n;
                cols[v] = cols[v + 1] = cols[v + 2] = cols[v + 3] = col;
                v += 4;
                tris[t++] = b0;     tris[t++] = b0 + 2; tris[t++] = b0 + 1;
                tris[t++] = b0 + 1; tris[t++] = b0 + 2; tris[t++] = b0 + 3;
            }
        }

        mesh.Clear();
        mesh.vertices  = verts;
        mesh.colors    = cols;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
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
        r.sortingOrder        = 35;   // 中央デカール群の上・ノーツ (40) の下
        r.shadowCastingMode   = ShadowCastingMode.Off;
        r.receiveShadows      = false;
        r.lightProbeUsage     = LightProbeUsage.Off;
        _pool.Add(q.transform);
        _renderers.Add(r);
    }

    MeshFilter GetFxArc(int index)
    {
        while (_fxPool.Count <= index) CreateFxArc();
        var mf = _fxPool[index];
        if (!mf.gameObject.activeSelf) mf.gameObject.SetActive(true);
        return mf;
    }

    void CreateFxArc()
    {
        var go = new GameObject("MeasureArc" + _fxPool.Count);
        go.transform.SetParent(transform, false);
        // 頂点はワールド座標で構築するためトランスフォームはワールド原点に固定する。
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = new Mesh { name = "MeasureArcMesh" };
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial    = _fxMaterial;
        mr.sortingOrder      = 15;   // FX 面 (13/14) の上・FX ノーツ (16/17) の下
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows    = false;
        mr.lightProbeUsage   = LightProbeUsage.Off;
        _fxPool.Add(mf);
        _fxRenderers.Add(mr);
    }

    void HideAll()
    {
        for (int i = 0; i < _pool.Count; i++)
            if (_pool[i].gameObject.activeSelf) _pool[i].gameObject.SetActive(false);
        for (int i = 0; i < _fxPool.Count; i++)
            if (_fxPool[i].gameObject.activeSelf) _fxPool[i].gameObject.SetActive(false);
    }

    // Playfield/Alpha (renderQueue 直指定・頂点カラー×_BaseColor) の線用マテリアル。
    // シェーダが見つからない場合のみ URP Unlit にフォールバックする (描画順は保証されない)。
    static Material CreateLineMaterial(int queue)
    {
        var shader = Shader.Find("Playfield/Alpha");
        if (shader != null)
        {
            var mat0 = new Material(shader);
            mat0.renderQueue = queue;
            mat0.SetColor("_BaseColor", Color.white);
            return mat0;
        }

        var fallback = Shader.Find("Universal Render Pipeline/Unlit");
        if (fallback == null) fallback = Shader.Find("Unlit/Color");
        var mat = new Material(fallback);
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
