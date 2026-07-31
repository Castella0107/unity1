using UnityEngine;

/// <summary>
/// FX ノーツの円弧メッシュ描画 (画面設計モック(4) PLAYFIELD_SPEC.md §3)。
/// ノーツは判定弧と同幅の円弧 (θ130°〜170°) で、半径 R(z)=465·155/(155+z)。
/// 描画は 2 層: グロー #6fc4dc 幅 36·(155/(155+z)) (加算ブラー) + コア #f4fcfe 幅 16·同。
/// フェードイン (R−75)/28 で VP 付近の霧から出現する。白銀 = FX の特別色 (SPEC §4)。
/// NoteController / HoldNoteController がノーツの子として生成し、毎フレーム
/// UpdateTap / UpdateHold を呼んでメッシュを再構築する。頂点はワールド座標なので
/// この Transform は常にワールド原点へリセットされる。
/// </summary>
public class FxArcNote : MonoBehaviour
{
    Mesh _glowMesh, _coreMesh;
    MeshRenderer _glowMr, _coreMr;
    MaterialPropertyBlock _pb;
    readonly FxArcMeshBuilder _bGlow = new();
    readonly FxArcMeshBuilder _bCore = new();

    const int Segs = 30;
    static readonly int IdBaseColor = Shader.PropertyToID("_BaseColor");

    // COLOR_SPEC「琥珀&深緑」: FXノーツ = S-Glow グロー(36·scale) + S-Core コア(16·scale)
    static readonly Color GlowCol = new Color( 82 / 255f, 224 / 255f, 189 / 255f, 1f); // S-Glow #52E0BD
    static readonly Color CoreCol = new Color(223 / 255f, 251 / 255f, 244 / 255f, 1f); // S-Core #DFFBF4

    /// <summary>ノーツの子 GameObject として生成する。マテリアルは FxLaneVisuals から借りる。</summary>
    public static FxArcNote Create(Transform parent)
    {
        var go = new GameObject("FxArcNote");
        go.transform.SetParent(parent, false);
        var n = go.AddComponent<FxArcNote>();
        var vis = FxLaneVisuals.Instance;
        n._glowMr = n.MakeLayer(go.transform, "Glow", out n._glowMesh,
            vis != null ? vis.NoteGlowMaterial : null);
        n._coreMr = n.MakeLayer(go.transform, "Core", out n._coreMesh,
            vis != null ? vis.NoteMaterial : null);
        n._glowMr.sortingOrder = 16; // モック §0: FXノーツは FX面の上・判定弧の下
        n._coreMr.sortingOrder = 17;
        return n;
    }

    MeshRenderer MakeLayer(Transform parent, string name, out Mesh mesh, Material mat)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        mesh = new Mesh { name = "FxArcNote" + name };
        mesh.MarkDynamic();
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.sharedMaterial = mat != null ? mat : new Material(Shader.Find("Sprites/Default"));
        return mr;
    }

    /// <summary>レーン色を外部ティントとして適用する (GameColorSettings のノーツ色)。</summary>
    public void SetColor(Color c)
    {
        _pb ??= new MaterialPropertyBlock();
        _pb.SetColor(IdBaseColor, c);
        _coreMr.SetPropertyBlock(_pb);
        _glowMr.SetPropertyBlock(_pb);
    }

    public void SetVisible(bool v)
    {
        if (_glowMr != null && _glowMr.enabled != v) _glowMr.enabled = v;
        if (_coreMr != null && _coreMr.enabled != v) _coreMr.enabled = v;
    }

    /// <summary>メッシュはワールド座標で構築するため、自身の Transform を原点に固定する。</summary>
    void ResetWorld()
    {
        transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
    }

    /// <summary>ノーツの共通アルファ。FAR_WALL_SPEC §1: 距離フェード禁止 → 常に 1。
    /// 奥端の消え方は AddArcLayers/胴の壁クリップ (r &lt; WallR を描かない) が担う。</summary>
    static float ArcAlpha(float r) => 1f;

    /// <summary>タップノーツ: 半径 NoteRadius(zWorld) の円弧線分 (グロー + コアの 2 層)。</summary>
    public void UpdateTap(bool right, float zWorld, float yLift)
    {
        ResetWorld();
        float r     = FxSectorGeometry.NoteRadius(zWorld);
        float alpha = ArcAlpha(r);
        // FAR_WALL_SPEC §2: 壁 (r=WallR) の裏は描かない。壁を越えた瞬間に全不透明で出現。
        if (r + FxSectorGeometry.NoteGlowStrokePx * 0.5f * (r / FxSectorGeometry.JudgeRadius)
            <= FxSectorGeometry.WallR) { SetVisible(false); return; }

        _bGlow.Clear();
        _bCore.Clear();
        AddArcLayers(right, r, alpha, yLift);
        _bGlow.Apply(_glowMesh);
        _bCore.Apply(_coreMesh);
        SetVisible(true);
    }

    /// <summary>
    /// ホールドノーツ: 頭と尾の円弧バー + 両者を結ぶ円環帯の胴。
    /// consumed (頭タップ済み) の間は頭を判定円弧にピン留めし、判定より外側 (z&lt;0) は描かない。
    /// </summary>
    public void UpdateHold(bool right, float zHeadWorld, float zTailWorld, bool consumed, float yLift)
    {
        ResetWorld();
        if (consumed && zTailWorld <= 0f) { SetVisible(false); return; } // 尾まで判定を通過 → 全消し

        float zh    = consumed ? Mathf.Max(zHeadWorld, 0f) : zHeadWorld;
        float rHead = FxSectorGeometry.NoteRadius(zh);         // 手前 (半径大)
        float rTail = FxSectorGeometry.NoteRadius(zTailWorld); // 奥 (半径小)
        if (rHead <= FxSectorGeometry.WallR) { SetVisible(false); return; } // まだ壁の裏

        _bGlow.Clear();
        _bCore.Clear();

        // 胴: 頭〜尾を結ぶ円環帯 (グロー色を淡く)。壁より奥 (r<WallR) はハードに切る。
        float rTailVis = Mathf.Max(rTail, FxSectorGeometry.WallR);
        if (rHead - rTailVis > 0.5f)
            _bGlow.AddArcBand(right, FxSectorGeometry.NoteThetaMin, FxSectorGeometry.NoteThetaMax,
                          _ => rTailVis, _ => rHead,
                          (t, rt) =>
                          {
                              float r = Mathf.Lerp(rTailVis, rHead, rt);
                              var c = GlowCol; c.a = ArcAlpha(r) * 0.45f;
                              return c;
                          }, Segs, 10, yLift);

        if (!(consumed && zHeadWorld < 0f)) AddArcLayers(right, rHead, ArcAlpha(rHead), yLift + 0.002f);
        if (rTail > FxSectorGeometry.WallR) AddArcLayers(right, rTail, ArcAlpha(rTail), yLift + 0.002f);

        _bGlow.Apply(_glowMesh);
        _bCore.Apply(_coreMesh);
        SetVisible(true);
    }

    /// <summary>1 本のノーツ円弧をグロー帯 + コア帯として追加する (SPEC §3 の 2 層描画)。</summary>
    void AddArcLayers(bool right, float r, float alpha, float yLift)
    {
        if (alpha <= 0.01f) return;
        float sr       = r / FxSectorGeometry.JudgeRadius; // = 155/(155+z)
        float halfGlow = FxSectorGeometry.NoteGlowStrokePx * 0.5f * sr;
        float halfCore = FxSectorGeometry.NoteCoreStrokePx * 0.5f * sr;
        float wall     = FxSectorGeometry.WallR;   // FAR_WALL_SPEC: 壁より奥はハードに切る

        if (r + halfGlow > wall)
            _bGlow.AddArcBand(right, FxSectorGeometry.NoteThetaMin, FxSectorGeometry.NoteThetaMax,
                      _ => Mathf.Max(r - halfGlow, wall), _ => r + halfGlow,
                      (t, rt) =>
                      {
                          // ガウス断面 (ブラーの近似)。発光は Glow+Core の二層が必須。
                          // rt は可視範囲内の割合なので、元の全幅に対する位置へ換算して断面を評価する。
                          float rr = Mathf.Lerp(Mathf.Max(r - halfGlow, wall), r + halfGlow, rt);
                          float x = (rr - r) / Mathf.Max(halfGlow, 0.001f);
                          var c = GlowCol; c.a = alpha * Mathf.Exp(-4f * x * x);
                          return c;
                      }, Segs, 4, yLift);
        if (r + halfCore > wall)
            _bCore.AddArcBand(right, FxSectorGeometry.NoteThetaMin, FxSectorGeometry.NoteThetaMax,
                      _ => Mathf.Max(r - halfCore, wall), _ => r + halfCore,
                      (t, rt) => { var c = CoreCol; c.a = alpha; return c; }, Segs, 1, yLift + 0.001f);
    }

    void OnDestroy()
    {
        DestroyMesh(_glowMesh);
        DestroyMesh(_coreMesh);
    }

    static void DestroyMesh(Mesh m)
    {
        if (m == null) return;
        if (Application.isPlaying) Destroy(m);
        else DestroyImmediate(m);
    }
}
