using UnityEngine;

/// <summary>
/// プレイ画面の色設定(ユーザーカスタマイズ)の正本。
/// ノーツのレーン別色(6レーン)・レーン仕切り線の色・判定線の色を PlayerPrefs に保存する。
/// 値は "RRGGBB"(16進RGB)で保持し、アルファは各対象の既定値を維持する(仕切り線の半透明など)。
///
/// 編集UIは Config の「色」タブ(ColorsTabController)。反映先:
///   ノーツ   → NoteController.Initialize
///   仕切り線 → LaneBrightness.Apply(明るさ係数と合成)
///   判定線   → LaneBrightness.Apply
/// </summary>
public static class GameColorSettings
{
    /// <summary>カスタマイズ対象のレーン数(Lane0〜Lane3 + FxL + FxR)。</summary>
    public const int LaneCount = 6;

    // 既定色。参照パレット(青基調+オレンジ差し色)に準拠:
    //   #3498DB スカイブルー / #FEBD69 アンバー / #4C81B9 くすみブルー。
    //   外側メイン=主役の明るい青、内側メイン=中央で映える橙、FX=同系のくすみ青で統一。
    static readonly Color[] NoteDefaults =
    {
        new Color(0.204f, 0.596f, 0.859f), // Lane0 (鍵1 左) – #3498DB sky blue
        new Color(0.996f, 0.741f, 0.412f), // Lane1 (鍵2)    – #FEBD69 amber
        new Color(0.996f, 0.741f, 0.412f), // Lane2 (鍵3)    – #FEBD69 amber
        new Color(0.204f, 0.596f, 0.859f), // Lane3 (鍵4 右) – #3498DB sky blue
        new Color(0.298f, 0.506f, 0.725f), // FxL  (FX 左)   – #4C81B9 muted blue
        new Color(0.298f, 0.506f, 0.725f), // FxR  (FX 右)   – #4C81B9 muted blue
    };

    static readonly Color DividerDefault      = new Color(1.00f, 1.00f, 1.00f, 0.65f); // LaneDividerMaterial
    static readonly Color JudgmentLineDefault = new Color(0.20f, 1.00f, 1.00f, 1.00f); // JudgmentLine material (cyan)

    /// <summary>各設定の PlayerPrefs キー一覧(F9 リセット対象に渡す)。</summary>
    public static readonly string[] AllKeys =
    {
        "NoteColor0", "NoteColor1", "NoteColor2", "NoteColor3", "NoteColor4", "NoteColor5",
        "DividerColor", "JudgmentLineColor",
    };

    static string NoteKey(int lane) => "NoteColor" + lane;

    /// <summary>レーン <paramref name="lane"/> のノーツ色(未設定なら既定色)。アルファは1。</summary>
    public static Color NoteColor(int lane)
    {
        lane = Mathf.Clamp(lane, 0, LaneCount - 1);
        return Load(NoteKey(lane), NoteDefaults[lane]);
    }

    /// <summary>レーン <paramref name="lane"/> のノーツ色を保存する(RGBのみ)。</summary>
    public static void SetNoteColor(int lane, Color c) => Save(NoteKey(lane), c);

    /// <summary>レーン仕切り線の色(未設定なら既定の白・半透明)。</summary>
    public static Color DividerColor
    {
        get => Load("DividerColor", DividerDefault);
        set => Save("DividerColor", value);
    }

    /// <summary>判定線の色(未設定なら既定のシアン)。</summary>
    public static Color JudgmentLineColor
    {
        get => Load("JudgmentLineColor", JudgmentLineDefault);
        set => Save("JudgmentLineColor", value);
    }

    // PlayerPrefs から "RRGGBB" を読み、既定色のアルファを維持して返す。未設定/不正なら既定色。
    static Color Load(string key, Color def)
    {
        string s = PlayerPrefs.GetString(key, "");
        if (!string.IsNullOrEmpty(s) && ColorUtility.TryParseHtmlString("#" + s, out var c))
        {
            c.a = def.a;
            return c;
        }
        return def;
    }

    static void Save(string key, Color c)
    {
        PlayerPrefs.SetString(key, ColorUtility.ToHtmlStringRGB(c));
        PlayerPrefs.Save();
    }
}
