using UnityEngine;

/// <summary>
/// レーン(プレイフィールド)の明るさ・色を再生開始時に適用するユーティリティ。
///
/// 明るさ(Level 0=真っ黒 〜 10=等倍):
///   レーン背景(Ground)・レーン帯(LaneHighlights) を減光する。
///   ノーツ・拍線・判定線・区切り線は対象外。
///
/// 色(GameColorSettings、ユーザーカスタマイズ):
///   区切り線 = DividerColor / 判定線 = JudgmentLineColor (いずれも明るさ非依存で常に指定色を表示)。
///   ノーツ色は NoteController が個別に適用する。
///
/// マテリアル資産は書き換えず MaterialPropertyBlock で適用するので、再生のたびに元の色を基準にでき累積しない。
/// 正本の明るさは PlayerPrefs "LaneBrightness"。PLAY OPTIONS で変更し、
/// StageInitializer.BindStageVisuals がライブ/リプレイ両方の再生開始時に Apply() を呼ぶ。
/// </summary>
public static class LaneBrightness
{
    /// <summary>明るさの最大値(=等倍・現在の見た目)。</summary>
    public const int MaxLevel = 10;

    /// <summary>レーンの明るさ (0〜10、PlayerPrefs "LaneBrightness"、既定10)。</summary>
    public static int Level
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt("LaneBrightness", MaxLevel), 0, MaxLevel);
        set { PlayerPrefs.SetInt("LaneBrightness", Mathf.Clamp(value, 0, MaxLevel)); PlayerPrefs.Save(); }
    }

    static readonly int IdColor     = Shader.PropertyToID("_Color");
    static readonly int IdBaseColor = Shader.PropertyToID("_BaseColor");

    /// <summary>現在の Level / 色設定に応じてレーン背景・帯・区切り線・判定線へ適用する。再生開始時に1回呼ぶ。</summary>
    public static void Apply()
    {
        float f = Level / (float)MaxLevel;

        // 背景・帯: ベース色 × 明るさ
        var ground = GameObject.Find("Ground");
        if (ground != null) TintBrightness(ground.transform, f);

        var highlights = GameObject.Find("LaneHighlights");
        if (highlights != null) TintBrightness(highlights.transform, f);

        // 区切り線: ユーザー指定色(明るさ非依存で常に視認可能に。明るさ0でも黒潰れさせない)。
        // LaneVisuals 配下の "Divider*" のみ。LaneHighlights は上で明るさ減光済み。
        var lanes = Object.FindObjectOfType<LaneVisuals>();
        if (lanes != null)
        {
            Color dividerC = GameColorSettings.DividerColor;
            foreach (Transform child in lanes.transform)
                if (child.name.StartsWith("Divider"))
                    SetSolidColor(child, dividerC);
        }

        // 判定線: ユーザー指定色(明るさ非依存で常に視認可能に)
        var judgeLine = GameObject.Find("JudgmentLine");
        if (judgeLine != null) SetSolidColor(judgeLine.transform, GameColorSettings.JudgmentLineColor);

        Debug.Log($"[LaneBrightness] Applied level={Level}/{MaxLevel} (factor={f:F2}) + colors");
    }

    // root 以下の全 Renderer のベース色(sharedMaterial)を factor 倍して上書き。
    static void TintBrightness(Transform root, float f)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);
        var mpb = new MaterialPropertyBlock();
        foreach (var r in renderers)
        {
            var mat = r.sharedMaterial;
            if (mat == null) continue;
            r.GetPropertyBlock(mpb);
            bool any = false;
            if (mat.HasProperty(IdBaseColor)) { mpb.SetColor(IdBaseColor, Mul(mat.GetColor(IdBaseColor), f)); any = true; }
            if (mat.HasProperty(IdColor))     { mpb.SetColor(IdColor,     Mul(mat.GetColor(IdColor),     f)); any = true; }
            if (any) r.SetPropertyBlock(mpb);
        }
    }

    // root 以下の全 Renderer に指定色をそのまま上書き(ベース色を参照しない)。
    static void SetSolidColor(Transform root, Color c)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);
        var mpb = new MaterialPropertyBlock();
        foreach (var r in renderers)
        {
            var mat = r.sharedMaterial;
            if (mat == null) continue;
            r.GetPropertyBlock(mpb);
            bool any = false;
            if (mat.HasProperty(IdBaseColor)) { mpb.SetColor(IdBaseColor, c); any = true; }
            if (mat.HasProperty(IdColor))     { mpb.SetColor(IdColor,     c); any = true; }
            if (any) r.SetPropertyBlock(mpb);
        }
    }

    static Color Mul(Color c, float f) => new Color(c.r * f, c.g * f, c.b * f, c.a);
}
