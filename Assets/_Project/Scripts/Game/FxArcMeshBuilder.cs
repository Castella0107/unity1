using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// VP 中心の円弧ジオメトリ (FxSectorGeometry) をワールド床メッシュとして構築するビルダー。
/// 円弧帯 (θ 範囲 × 半径範囲のグリッド) を頂点色付きで追加していき、Apply で Mesh へ流し込む。
/// 頂点はすべてスクリーン仕様座標から床平面へ逆投影したワールド座標。
/// 左右ミラーで三角形の巻き順が反転するため、マテリアルは Cull Off にすること。
/// </summary>
public class FxArcMeshBuilder
{
    readonly List<Vector3> _v = new();
    readonly List<Color>   _c = new();
    readonly List<int>     _t = new();

    public void Clear()
    {
        _v.Clear();
        _c.Clear();
        _t.Clear();
    }

    /// <summary>
    /// 円弧帯を追加する。θ0→θ1 を segs 分割、半径 rIn(t)〜rOut(t) を rings 分割。
    /// t は角度方向 0〜1、rt は半径方向 0〜1 で、color(t, rt) が頂点色を決める。
    /// </summary>
    public void AddArcBand(bool right, float th0, float th1,
                           System.Func<float, float> rIn, System.Func<float, float> rOut,
                           System.Func<float, float, Color> color,
                           int segs, int rings, float yLift)
    {
        int baseIndex = _v.Count;
        for (int i = 0; i <= segs; i++)
        {
            float t  = (float)i / segs;
            float th = Mathf.Lerp(th0, th1, t);
            float r0 = rIn(t), r1 = rOut(t);
            for (int j = 0; j <= rings; j++)
            {
                float rt = (float)j / rings;
                float r  = Mathf.Lerp(r0, r1, rt);
                _v.Add(FxSectorGeometry.FloorPoint(right, th, r, yLift));
                // 仕様の色は sRGB 値。リニア色空間では頂点色が変換されずそのまま
                // シェーダへ渡るため、ここでリニアへ変換して見た目を一致させる。
                var c = color(t, rt);
                _c.Add(QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c);
            }
        }
        int stride = rings + 1;
        for (int i = 0; i < segs; i++)
            for (int j = 0; j < rings; j++)
            {
                int a = baseIndex + i * stride + j;
                int b = a + stride;
                _t.Add(a); _t.Add(b); _t.Add(a + 1);
                _t.Add(a + 1); _t.Add(b); _t.Add(b + 1);
            }
    }

    public void Apply(Mesh mesh)
    {
        mesh.Clear();
        mesh.SetVertices(_v);
        mesh.SetColors(_c);
        mesh.SetTriangles(_t, 0);
        mesh.RecalculateBounds();
    }
}
