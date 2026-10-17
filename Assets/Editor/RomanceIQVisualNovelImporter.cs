using UnityEditor;
using UnityEngine;

public sealed class RomanceIQVisualNovelImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/RomanceIQ/VisualNovel/")) return;
        var texture = (TextureImporter)assetImporter;
        texture.textureType = TextureImporterType.Default;
        texture.alphaSource = TextureImporterAlphaSource.FromInput;
        texture.alphaIsTransparency = true;
        texture.mipmapEnabled = false;
        texture.npotScale = TextureImporterNPOTScale.None;
        texture.maxTextureSize = 2048;
        texture.textureCompression = TextureImporterCompression.Uncompressed;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
    }
}
