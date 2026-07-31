#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 動的 TMP フォントアセット (NotoSansJP) のグリフ事前焼き込み。
///
/// 背景 (2026-06-08): リリースビルド限定で Config 遷移時にネイティブクラッシュ。
/// 原因は clearOnBuild=true の動的フォントが実行時にグリフを追加し続け、
/// Login+Title 通過後の Config (漢字最多画面) で 1024x1024 アトラス (容量~200グリフ) が溢れて
/// マルチアトラス 2 ページ目を実行時生成 → FontEngine がクラッシュするため。
/// (Boot→Config 直行はページ1に収まりセーフ、という再現実験と整合)
///
/// 対策: アトラスを 4096 に拡張し、全 UI 使用文字 (prebake_chars.txt、ソースから自動収集) を
/// エディタ時に焼き込み、clearOnBuild=false で出荷。実行時のグリフ追加をほぼゼロにする。
/// メニュー: Tools/Fonts/Prebake NotoSansJP Glyphs (batch: -executeMethod PreBakeFontGlyphs.Run)
/// </summary>
public static class PreBakeFontGlyphs
{
    const string FontPath  = "Assets/_Project/Fonts/NotoSansJP-Regular SDF.asset";
    const string CharsPath = "Assets/_Project/Fonts/prebake_chars.txt";
    const int    AtlasSize = 4096;

    [MenuItem("Tools/Fonts/Prebake NotoSansJP Glyphs")]
    public static void Run()
    {
        var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (fa == null) { Fail("font asset not found: " + FontPath); return; }
        if (!File.Exists(CharsPath)) { Fail("chars file not found: " + CharsPath); return; }

        string chars = File.ReadAllText(CharsPath) + CollectSongTitleChars();

        // アトラスサイズ拡張+焼き込み済みデータのクリア (serialized fields 直接変更)
        var so = new SerializedObject(fa);
        so.FindProperty("m_AtlasWidth").intValue  = AtlasSize;
        so.FindProperty("m_AtlasHeight").intValue = AtlasSize;
        // ビルド時に動的データを消さない (これが true だとビルドは空アトラスから始まる)
        var clearProp = so.FindProperty("m_ClearDynamicDataOnBuild");
        if (clearProp != null) clearProp.boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();

        fa.ClearFontAssetData(setAtlasSizeToZero: false);

        bool ok = fa.TryAddCharacters(chars, out string missing);
        int baked = fa.characterTable.Count;
        Debug.Log($"[PreBakeFontGlyphs] baked={baked} chars (requested {chars.Length})"
                  + (string.IsNullOrEmpty(missing) ? "" : $"  missing={missing.Length}: {Truncate(missing, 60)}"));

        EditorUtility.SetDirty(fa);
        if (fa.atlasTexture != null) EditorUtility.SetDirty(fa.atlasTexture);
        if (fa.material != null)     EditorUtility.SetDirty(fa.material);
        AssetDatabase.SaveAssets();

        Debug.Log($"[PreBakeFontGlyphs] DONE  atlas={fa.atlasWidth}x{fa.atlasHeight} pages={fa.atlasTextureCount} glyphs={fa.glyphTable.Count}");
        if (Application.isBatchMode) EditorApplication.Exit(baked > 0 ? 0 : 4);
    }

    /// <summary>
    /// 同梱楽曲の曲名・アーティスト名で使われている文字を集める。
    ///
    /// prebake_chars.txt は UI の固定文言だけなので、曲名の漢字が焼き込まれず
    /// 日本語の曲名が表示されなかった (K 報告 2026-08-01: 「女」「捧」「神」「欲」「愛」「華」等が未収録)。
    /// StreamingAssets の meta.json から拾って必ず含める。
    ///
    /// ※サーバーから後で追加された曲は当然ここに無い。動的追加のフォールバックは
    ///   生きているが、アトラス溢れを避けるため新曲を入れたら本メニューを再実行すること。
    /// </summary>
    static string CollectSongTitleChars()
    {
        var sb = new System.Text.StringBuilder();
        string root = Path.Combine(Application.streamingAssetsPath, "Songs");
        if (!Directory.Exists(root)) return "";

        foreach (var dir in Directory.GetDirectories(root))
        {
            string meta = Path.Combine(dir, "meta.json");
            if (!File.Exists(meta)) continue;
            try
            {
                string json = File.ReadAllText(meta);
                foreach (var key in new[] { "title", "artist" })
                {
                    var m = System.Text.RegularExpressions.Regex.Match(
                        json, "\"" + key + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
                    if (m.Success) sb.Append(System.Text.RegularExpressions.Regex.Unescape(m.Groups[1].Value));
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[PreBakeFontGlyphs] meta 読み込み失敗 " + dir + ": " + e.Message);
            }
        }
        Debug.Log($"[PreBakeFontGlyphs] 曲名から {sb.Length} 文字を追加収集");
        return sb.ToString();
    }

    static void Fail(string msg)
    {
        Debug.LogError("[PreBakeFontGlyphs] " + msg);
        if (Application.isBatchMode) EditorApplication.Exit(4);
    }

    static string Truncate(string s, int n) => s.Length <= n ? s : s.Substring(0, n) + "...";
}
#endif
