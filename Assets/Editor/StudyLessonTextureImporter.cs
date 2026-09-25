#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Preserve small mathematical symbols in source-slide pages. Pages are loaded
// two at a time by the existing book reader, not all retained in memory.
public class StudyLessonTextureImporter : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/StudyLibrary/")) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.mipmapEnabled = false;
        importer.isReadable = false;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp;
    }
}
#endif
