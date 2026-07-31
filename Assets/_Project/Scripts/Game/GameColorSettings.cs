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

    // 既定色。プレイフィールド 画面設計 (export-src.html) に準拠:
    //   ノーツは暗いトラックに映える白発光 (#EEF9FD/#FFFFFF)、
    //   仕切り線はごく薄い白青、判定線は明るいシアン #7DEEFA。
    static readonly Color[] NoteDefaults =
    {
        new Color(0.933f, 0.976f, 0.992f), // Lane0 (鍵1 左) – #EEF9FD white
        new Color(0.933f, 0.976f, 0.992f), // Lane1 (鍵2)    – #EEF9FD white
        new Color(0.933f, 0.976f, 0.992f), // Lane2 (鍵3)    – #EEF9FD white
        new Color(0.933f, 0.976f, 0.992f), // Lane3 (鍵4 右) – #EEF9FD white
        new Color(1.000f, 1.000f, 1.000f), // FxL  (FX 左)   – #FFFFFF white arc
        new Color(1.000f, 1.000f, 1.000f), // FxR  (FX 右)   – #FFFFFF white arc
    };

    static readonly Color DividerDefault      = new Color(0.784f, 0.922f, 0.973f, 0.22f); // 薄い白青 (プレイフィールド)
    static readonly Color JudgmentLineDefault = new Color(0.490f, 0.933f, 0.980f, 1.00f); // #7DEEFA cyan
    static readonly Color ChordDefault        = new Color(1.000f, 0.843f, 0.251f, 1.00f); // #FFD740 黄 (同時押し)

    /// <summary>各設定の PlayerPrefs キー一覧(F9 リセット対象に渡す)。</summary>
    public static readonly string[] AllKeys =
    {
        "NoteColor0", "NoteColor1", "NoteColor2", "NoteColor3", "NoteColor4", "NoteColor5",
        "DividerColor", "JudgmentLineColor", "ChordColor",
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

    /// <summary>同時押しノーツの色 (K 指示 2026-07-30。未設定なら既定の黄 #FFD740)。
    /// 同時刻に 2 ノーツ以上ある場合に NoteController がレーン色の代わりに使う。</summary>
    public static Color ChordColor
    {
        get => Load("ChordColor", ChordDefault);
        set => Save("ChordColor", value);
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
