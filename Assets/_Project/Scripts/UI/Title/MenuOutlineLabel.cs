using TMPro;
using UnityEngine;

/// <summary>
/// タイトルメニュー用のアウトライン(中抜き)+グラデーション文字 (ユーザー提供リファレンス準拠)。
/// スタイル: 縦長の大文字 / 中抜きの細い輪郭線 / 左=濃紺 → 右=スチールブルー のグラデーション。
///
/// 実装: TMP 2層重ね —
///   Back  = 塗り文字に頂点グラデーション(左濃紺→右青)
///   Front = 同じ文字を背景色(黒)で一回り細く(_FaceDilate 負値)重ねて内側を抜く
/// 背景が真っ暗なタイトル画面前提(Front が背景に溶けて輪郭だけ残る)。
/// 縦長感はルートの localScale (x縮小/y拡大) で付与(ビルダー側で設定)。
/// </summary>
public class MenuOutlineLabel : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI _back;    // グラデ塗り層
    [SerializeField] TextMeshProUGUI _front;   // 背景色で内側を抜く層

    // 選択中: 明るい青グラデ / 非選択: 暗い紺グラデ (リファレンスの配色から)
    static readonly Color SelDark    = Hex("2A3DA8");
    static readonly Color SelLight   = Hex("7FB4FF");
    static readonly Color UnselDark  = Hex("141C49");
    static readonly Color UnselLight = Hex("3D5E8C");

    const float FrontDilate = -0.32f;   // 内抜き量 (負=細らせる)

    Material _frontMat;   // ランタイム複製 (共有マテリアルを汚さない)

    void Awake()
    {
        if (_front != null)
        {
            _frontMat = _front.fontMaterial;   // インスタンス化される
            _frontMat.SetFloat(ShaderUtilities.ID_FaceDilate, FrontDilate);
            _front.color = Color.black;        // タイトル背景色
        }
        if (_back != null)
        {
            _back.enableVertexGradient = true;
        }
    }

    void OnDestroy()
    {
        if (_frontMat != null) Destroy(_frontMat);
    }

    /// <summary>表示文字列を設定する(両層へ反映。リファレンス準拠で大文字化)。</summary>
    public void SetText(string text)
    {
        string t = (text ?? "").ToUpperInvariant();
        if (_back  != null) _back.text  = t;
        if (_front != null) _front.text = t;
    }

    /// <summary>フォントサイズを設定する(両層へ反映)。</summary>
    public void SetFontSize(float size)
    {
        if (_back  != null) _back.fontSize  = size;
        if (_front != null) _front.fontSize = size;
    }

    /// <summary>選択状態に応じてグラデーションの明度を切り替える。</summary>
    public void SetSelected(bool selected)
    {
        if (_back == null) return;
        var dark  = selected ? SelDark  : UnselDark;
        var light = selected ? SelLight : UnselLight;
        _back.colorGradient = new VertexGradient(dark, light, dark, light);   // TL,TR,BL,BR = 左→右
    }

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out var c);
        return c;
    }
}
