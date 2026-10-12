using System;
using System.IO;
using System.Linq;
using LogicLegends.Graph;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>Imports only the newly generated Graph HUD art; source art and Canvas 1 stay intact.</summary>
public static class GraphHUDArtSetup
{
    public const string Folder = "Assets/Art/GraphHUD/";
    [MenuItem("Tools/Logic Legends/Apply New Graph HUD Artwork")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/Graph.unity") throw new InvalidOperationException("Open Graph first.");
        var ui = UnityEngine.Object.FindFirstObjectByType<GraphUIController>();
        Undo.RecordObject(ui, "New Graph HUD artwork");
        ui.hudMap = Import("GraphMapFrame");
        ui.hudObjective = Import("GraphObjectiveFrame");
        ui.hudStatus = Import("GraphStatusFrame");
        EditorUtility.SetDirty(ui);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
    private static Sprite Import(string name)
    {
        string path = Folder + name + ".png";
        var texture = new Texture2D(2, 2);
        texture.LoadImage(File.ReadAllBytes(path));
        var pixels = texture.GetPixels32();
        int left = texture.width, bottom = texture.height, right = 0, top = 0;
        for (int y = 0; y < texture.height; y++)
            for (int x = 0; x < texture.width; x++)
                if (pixels[y * texture.width + x].a > 8)
                { left = Mathf.Min(left, x); bottom = Mathf.Min(bottom, y); right = Mathf.Max(right, x); top = Mathf.Max(top, y); }
        var rect = new Rect(Mathf.Max(0, left - 2), Mathf.Max(0, bottom - 2),
            Mathf.Min(texture.width, right + 3) - Mathf.Max(0, left - 2), Mathf.Min(texture.height, top + 3) - Mathf.Max(0, bottom - 2));
        UnityEngine.Object.DestroyImmediate(texture);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 100;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        var factories = new SpriteDataProviderFactories(); factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
        var previous = provider.GetSpriteRects().FirstOrDefault(r => r.name == name);
        float edge = Mathf.Min(rect.width, rect.height) * .24f;
        var spriteRect = new SpriteRect { name = name, rect = rect, alignment = SpriteAlignment.Center,
            pivot = new Vector2(.5f, .5f), border = Vector4.one * edge, spriteID = previous == null ? GUID.Generate() : previous.spriteID };
        provider.SetSpriteRects(new[] { spriteRect });
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(new[] { new SpriteNameFileIdPair(name, spriteRect.spriteID) });
        provider.Apply(); importer.SaveAndReimport();
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().First();
    }
}
