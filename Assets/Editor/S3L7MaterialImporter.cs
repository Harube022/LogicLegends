#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Reconnect the known Blender material names when S3L7 FBXs are imported again,
// including when an exporter supplies a fresh .meta file. Never modify meshes.
public sealed class S3L7MaterialImporter : AssetPostprocessor
{
    private const string Root = "Assets/FINAL_ASSETS/MAPS/REMADE/S3L7";

    private static readonly Dictionary<string, string[]> SourceNames =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "1.fbx", new[] { "Sets_Surface", "Sets_Glow" } },
            { "2.fbx", new[] { "Ash Highlight", "Sets_Surface", "Sets_Glow" } },
            { "3.fbx", new[] { "Material.034", "Sets_Surface", "Sets_Glow" } },
            { "4.fbx", new[] { "Bamboo base", "Sets_Surface", "Sets_Glow" } },
            { "5.fbx", new[] { "Book Purple", "Sets_Surface", "Sets_Glow" } },
            { "6.fbx", new[] { "Book Red", "Sets_Surface", "Sets_Glow" } },
            { "7.fbx", new[] { "Building WindowFrame", "Sets_Surface", "Sets_Glow" } },
            { "8.fbx", new[] { "Clove Yellow Brown", "Sets_Surface", "Sets_Glow" } },
            { "9.fbx", new[] { "Borderlines", "Sets_Surface", "Sets_Glow" } },
            { "AB.fbx", new[] { "Sets_Surface.002", "Sets_Glow.002" } },
            { "BORDERLINES.fbx", new[] { "Sets_Glow.001", "Sets_Surface.001" } },
            { "Board.fbx", new[] { "Material.027" } },
            { "Spawner.fbx", new[] { "SchoolRoofPlates" } }
        };

    public override uint GetVersion() { return 1; }

    private void OnPreprocessModel()
    {
        if (!string.Equals(Path.GetDirectoryName(assetPath)?.Replace('\\', '/'),
            Root, StringComparison.Ordinal)) return;
        if (!SourceNames.TryGetValue(Path.GetFileName(assetPath), out var names)) return;

        var importer = (ModelImporter)assetImporter;
        var existing = importer.GetExternalObjectMap();
        foreach (var sourceName in names)
        {
            var materialName = sourceName;
            if (sourceName.StartsWith("Sets_Surface", StringComparison.Ordinal))
                materialName = "SetsGameplay_Palette";
            else if (sourceName.StartsWith("Sets_Glow", StringComparison.Ordinal))
                materialName = "Sets_Glow";
            else if (sourceName == "Material.027") materialName = "Board_Stone";
            else if (sourceName == "SchoolRoofPlates") materialName = "Spawner_Surface";

            var material = AssetDatabase.LoadAssetAtPath<Material>(
                Root + "/Materials/" + materialName + ".mat");
            if (material == null)
            {
                Debug.LogWarning("S3L7 material missing: " + materialName + " for " + assetPath);
                continue;
            }

            var identifier = new AssetImporter.SourceAssetIdentifier(typeof(Material), sourceName);
            if (!existing.TryGetValue(identifier, out var current) || current != material)
                importer.AddRemap(identifier, material);
        }
    }
}
#endif
