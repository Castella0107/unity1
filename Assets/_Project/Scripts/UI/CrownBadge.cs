using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// FC/AP 表示用の王冠スプライトをコードで生成する静的ユーティリティ (画像アセット不要)。
/// 白で描いてあるので Image.color の乗算で銀 (FC) / 金 (AP) に着色して使う。
/// </summary>
public static class CrownBadge
{
    /// <summary>FC (フルコンボ) 用の銀色。</summary>
    public static readonly Color Silver = new Color(0.80f, 0.84f, 0.90f, 1f);
    /// <summary>AP (オールパーフェクト) 用の金色。</summary>
    public static readonly Color Gold   = new Color(0.98f, 0.80f, 0.20f, 1f);

    const int W = 96, H = 64;   // 表示は 20x13 程度に縮むので余裕をもった解像度で描く
    static Sprite _sprite;

    static Sprite CrownSprite
    {
        get
        {
            if (_sprite == null) _sprite = Build();
            return _sprite;
        }
    }

    // クラシックな王冠 (参考画像 2026-08-01): 3 つの山の先端に玉、下に少し離れた台座。
    // 形はプリミティブ (矩形/三角形/円) の合成で定義し、4x4 スーパーサンプリングで
    // 輪郭をアンチエイリアス。シルエット外は完全透過にして「くり抜き」で違和感を出さない。
    static Sprite Build()
    {
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp,
        };
        var px = new Color32[W * H];
        const int SS = 4;
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            int hit = 0;
            for (int sy = 0; sy < SS; sy++)
            for (int sx = 0; sx < SS; sx++)
            {
                float u = (x + (sx + 0.5f) / SS) / W;
                float v = (y + (sy + 0.5f) / SS) / H;
                if (InCrown(u, v)) hit++;
            }
            byte a = (byte)(hit * 255 / (SS * SS));
            px[y * W + x] = new Color32(255, 255, 255, a);
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        var s = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
        s.name = "CrownBadge";
        return s;
    }

    static bool InCrown(float u, float v)
    {
        // 台座 (本体と離す)
        if (InRect(u, v, 0.10f, 0.00f, 0.90f, 0.12f)) return true;
        // 本体ベース (山の付け根をつなぐ帯)
        if (InRect(u, v, 0.10f, 0.20f, 0.90f, 0.40f)) return true;
        // 3 つの山 (中央が最も高い)
        if (InTri(u, v, 0.06f, 0.30f, 0.38f, 0.30f, 0.15f, 0.80f)) return true;
        if (InTri(u, v, 0.26f, 0.30f, 0.74f, 0.30f, 0.50f, 0.86f)) return true;
        if (InTri(u, v, 0.62f, 0.30f, 0.94f, 0.30f, 0.85f, 0.80f)) return true;
        // 山の先端の玉
        if (InCircle(u, v, 0.15f, 0.85f, 0.062f)) return true;
        if (InCircle(u, v, 0.50f, 0.92f, 0.068f)) return true;
        if (InCircle(u, v, 0.85f, 0.85f, 0.062f)) return true;
        return false;
    }

    static bool InRect(float u, float v, float x0, float y0, float x1, float y1)
        => u >= x0 && u <= x1 && v >= y0 && v <= y1;

    static bool InCircle(float u, float v, float cx, float cy, float r)
    {
        // テクスチャは横長 (W:H = 3:2) のため、ピクセル上で真円に見えるよう縦差分を H/W 倍する。
        // 半径 r は横 (u) 方向の単位で指定する。
        float dx = u - cx;
        float dy = (v - cy) * ((float)H / W);
        return dx * dx + dy * dy <= r * r;
    }

    static bool InTri(float u, float v,
                      float ax, float ay, float bx, float by, float cx, float cy)
    {
        float d1 = Sign(u, v, ax, ay, bx, by);
        float d2 = Sign(u, v, bx, by, cx, cy);
        float d3 = Sign(u, v, cx, cy, ax, ay);
        bool hasNeg = d1 < 0 || d2 < 0 || d3 < 0;
        bool hasPos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(hasNeg && hasPos);
    }

    static float Sign(float px_, float py_, float ax, float ay, float bx, float by)
        => (px_ - bx) * (ay - by) - (ax - bx) * (py_ - by);

    /// <summary>parent (四角形セル等) の上辺中央に王冠 Image を載せる。</summary>
    public static Image Attach(Transform parent, Color color)
    {
        var go = new GameObject("Crown", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(20f, 13f);

        var img = go.AddComponent<Image>();
        img.sprite        = CrownSprite;
        img.color         = color;
        img.raycastTarget = false;
        return img;
    }
}
