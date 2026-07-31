using UnityEditor;
using UnityEngine;

/// <summary>
/// Import pipeline for the プレイフィールド playfield redesign art (Assets/_Project/Art/Playfield).
/// - AssetPostprocessor: every texture under the folder is imported as an uncompressed-ish
///   UI Sprite (no mipmaps, 2048 max) so the design PNGs keep their crisp glow edges.
/// - Menu item Tools/Playfield/Reimport Art: force-reapplies the rule to already-imported files.
/// </summary>
public sealed class PlayfieldAssetImporter : AssetPostprocessor
{
    private const string ArtFolder = "Assets/_Project/Art/Playfield";

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(ArtFolder)) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 2048;
        importer.alphaIsTransparency = true;
    }

    [MenuItem("Tools/Playfield/Reimport Art")]
    public static void ReimportArt()
    {
        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { ArtFolder });
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
        Debug.Log($"[PlayfieldAssetImporter] Reimported {guids.Length} textures under {ArtFolder}");
    }
}
