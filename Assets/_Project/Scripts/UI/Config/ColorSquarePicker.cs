using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Windows「色の編集」風の 2D カラーピッカー。
/// SV 四角(横=彩度 S / 縦=明度 V)と縦の色相バー(H)をドラッグして色を選ぶ。
/// 値が変わるたび <see cref="ColorChanged"/> を発火。外部からは <see cref="SetColor"/> で
/// 同期する(その間はイベント抑制)。グラデーション用テクスチャは実行時に生成し、アセットは持たない。
///
/// 参照(SerializeField)の配線は BuildConfigScene.BuildColorsTab。入力は子の
/// <see cref="ColorPickerArea"/>(透明キャッチャ)が拾い <see cref="OnAreaPointer"/> へ転送する。
/// </summary>
[DisallowMultipleComponent]
public class ColorSquarePicker : MonoBehaviour
{
    [Header("SV 四角 (横=彩度 / 縦=明度) ※ pivot(0,0)")]
    [SerializeField] RectTransform _svRect;
    [SerializeField] Image         _svBase;   // 純色相の下地
    [SerializeField] Image         _svWhite;  // 白(左)→透明(右)
    [SerializeField] Image         _svBlack;  // 透明(上)→黒(下)
    [SerializeField] RectTransform _svCursor;

    [Header("色相バー (縦) ※ pivot(0,0)")]
    [SerializeField] RectTransform _hueRect;
    [SerializeField] Image         _hueImage;
    [SerializeField] RectTransform _hueCursor;

    /// <summary>ユーザー操作で色が変わったとき発火(SetColor 経由では発火しない)。</summary>
    public event Action<Color> ColorChanged;

    float _h, _s = 1f, _v = 1f;
    bool  _suppress;
    bool  _built;

    void Awake()   => BuildTextures();
    void OnEnable() => RefreshVisual();

    // ── 公開 API ────────────────────────────────────────────────────────────
    /// <summary>色を反映(イベント抑制)。行選択・スライダー編集からの同期用。</summary>
    public void SetColor(Color c)
    {
        Color.RGBToHSV(c, out float h, out float s, out float v);
        if (s > 0.0001f) _h = h;   // 無彩色だと h が 0 に飛ぶので彩度0のときは色相を保つ
        _s = s; _v = v;
        _suppress = true; RefreshVisual(); _suppress = false;
    }

    public Color Current => Color.HSVToRGB(_h, _s, _v);

    // ── 入力(ColorPickerArea から) ─────────────────────────────────────────
    public void OnAreaPointer(bool isHue, Vector2 screenPos, Camera cam)
    {
        var rt = isHue ? _hueRect : _svRect;
        if (rt == null) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPos, cam, out var lp))
            return;
        // pivot(0,0) なので local は [0..w]×[0..h]
        float nx = Mathf.Clamp01(lp.x / Mathf.Max(1f, rt.rect.width));
        float ny = Mathf.Clamp01(lp.y / Mathf.Max(1f, rt.rect.height));
        if (isHue) _h = 1f - ny;          // 上=赤(0)
        else { _s = nx; _v = ny; }        // 右=高彩度 / 上=高明度
        RefreshVisual();
        if (!_suppress) ColorChanged?.Invoke(Current);
    }

    // ── 表示更新 ────────────────────────────────────────────────────────────
    void RefreshVisual()
    {
        if (_svBase != null) _svBase.color = Color.HSVToRGB(_h, 1f, 1f);
        PlaceCursor(_svCursor,  _svRect,  _s, _v);
        PlaceCursor(_hueCursor, _hueRect, 0.5f, 1f - _h);
    }

    static void PlaceCursor(RectTransform cursor, RectTransform area, float nx, float ny)
    {
        if (cursor == null || area == null) return;
        var size = area.rect.size;
        cursor.anchoredPosition = new Vector2(nx * size.x, ny * size.y);
    }

    // ── テクスチャ生成(実行時・1回) ────────────────────────────────────────
    void BuildTextures()
    {
        if (_built) return;
        _built = true;
        if (_hueImage != null) _hueImage.sprite = MakeHueSprite(2, 64);
        if (_svWhite  != null) { _svWhite.sprite = MakeGradSprite(64, 1, horizontal: true);  _svWhite.color = Color.white; }
        if (_svBlack  != null) { _svBlack.sprite = MakeGradSprite(1, 64, horizontal: false); _svBlack.color = Color.white; }
    }

    // 上(y=h-1)=色相0(赤) → 下=色相1 近辺
    static Sprite MakeHueSprite(int w, int h)
    {
        var tex = NewTex(w, h);
        for (int y = 0; y < h; y++)
        {
            var col = Color.HSVToRGB(Mathf.Clamp01(1f - (float)y / (h - 1)), 1f, 1f);
            for (int x = 0; x < w; x++) tex.SetPixel(x, y, col);
        }
        return Finish(tex, w, h);
    }

    // horizontal=true : 白(左 x=0)→透明(右)。 false : 黒(下 y=0)→透明(上)。
    static Sprite MakeGradSprite(int w, int h, bool horizontal)
    {
        var tex = NewTex(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color c = horizontal
                    ? new Color(1f, 1f, 1f, 1f - (float)x / Mathf.Max(1, w - 1))
                    : new Color(0f, 0f, 0f, 1f - (float)y / Mathf.Max(1, h - 1));
                tex.SetPixel(x, y, c);
            }
        return Finish(tex, w, h);
    }

    static Texture2D NewTex(int w, int h)
        => new Texture2D(w, h, TextureFormat.RGBA32, false)
        { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

    static Sprite Finish(Texture2D tex, int w, int h)
    {
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 100f);
    }
}
