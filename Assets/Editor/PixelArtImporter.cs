using UnityEditor;

// Import settings for the pixel art under Resources/Art: crisp pixels, 16 px per world unit.
// Sheets stay whole; ArtLib cuts the sprites out at runtime.
public class PixelArtImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/Art/")) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 16;
        ti.filterMode = UnityEngine.FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.wrapMode = UnityEngine.TextureWrapMode.Clamp;
        var settings = new TextureImporterSettings();
        ti.ReadTextureSettings(settings);
        settings.spriteMeshType = UnityEngine.SpriteMeshType.FullRect;
        ti.SetTextureSettings(settings);
    }
}

// Music under Resources/Audio/Music is streamed instead of decompressed into memory at load.
public class MusicImporter : AssetPostprocessor
{
    void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith("Assets/Resources/Audio/Music/")) return;
        var ai = (AudioImporter)assetImporter;
        var s = ai.defaultSampleSettings;
        s.loadType = UnityEngine.AudioClipLoadType.Streaming;
        s.compressionFormat = UnityEngine.AudioCompressionFormat.Vorbis;
        s.quality = 0.6f;
        ai.defaultSampleSettings = s;
    }
}
