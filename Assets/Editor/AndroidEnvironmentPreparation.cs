#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class AndroidEnvironmentPreparation
{
    const string Folder = "Assets/Resources/Settings/MobileMeshes";
    public static void Prepare()
    {
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        const string catalogPath = "Assets/Resources/Settings/SoloMobileMeshes.asset";
        var catalog = AssetDatabase.LoadAssetAtPath<SoloMobileMeshes>(catalogPath);
        if (catalog == null) { catalog = ScriptableObject.CreateInstance<SoloMobileMeshes>(); AssetDatabase.CreateAsset(catalog, catalogPath); }
        var paths = new[] { "Assets/FINAL_ASSETS/STREETLAMP2.fbx", "Assets/FINAL_ASSETS/MAPS/Stage 1 Logical Expressions/STAGE1/TREES_UPPERSIDE.fbx" };
        var entries = new List<SoloMobileMeshes.Entry>();
        var report = new System.Text.StringBuilder();
        for (int i = 0; i < paths.Length; i++)
        {
            var source = AssetDatabase.LoadAssetAtPath<Mesh>(paths[i]);
            var entry = new SoloMobileMeshes.Entry { source = source };
            entry.low = PrepareLod(source, 6, i + "_Low");
            entry.medium = PrepareLod(source, 5, i + "_Medium");
            entry.high = PrepareLod(source, 4, i + "_High");
            entries.Add(entry);
            report.AppendLine(paths[i] + ": " + source.triangles.Length / 3 + " -> " + entry.low.triangles.Length / 3 + "/" + entry.medium.triangles.Length / 3 + "/" + entry.high.triangles.Length / 3 + " triangles; original LODs 6/5/4, transforms/material slots/colliders unchanged.");
        }
        catalog.entries = entries.ToArray(); EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssetIfDirty(catalog);
        // Background surfaces only. Never resize font atlases, equations, UI or skins.
        var textures = new[] {
            "Assets/CHEATCODE/WOODTEXTURE/Textures/Wood2.tga",
            "Assets/CHEATCODE/WOODTEXTURE/Textures/Wood3.tga",
            "Assets/CHEATCODE/POLYART_Ancient Village/Textures/Rock/PA_RockSet1_Pile1/PA_RockSet1_occlusion.png",
            "Assets/CHEATCODE/POLYART_Ancient Village/Textures/Rock/PA_RockSet2_Pile4/PA_RockSet2_Pile4_mtl_baseColor.png",
            "Assets/CHEATCODE/POLYART_Ancient Village/Textures/Rock/PA_RockSet2_Pile4/PA_RockSet2_Pile4_mtl_occlusion.png",
            "Assets/CHEATCODE/GRASSTEXTURE/Textures/Grass/Grass_bluetint/Grass_bluetint_down.png" };
        foreach (string path in textures)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            var platform = importer.GetPlatformTextureSettings("Android");
            platform.overridden = true; platform.maxTextureSize = 1024;
            platform.format = TextureImporterFormat.ETC2_RGBA8;
            // ETC2 is the safe GLES3/Vulkan baseline until the exact phone GPU is known.
            importer.SetPlatformTextureSettings(platform); importer.SaveAndReimport();
            report.AppendLine("Android-only 1024 ETC2_RGBA8: " + path);
        }
        Directory.CreateDirectory("output/mobile-performance");
        File.WriteAllText("output/mobile-performance/PreparedEnvironment.txt", report.ToString());
    }
    static Mesh PrepareLod(Mesh source, int lod, string name)
    {
        string path = Folder + "/" + name + ".asset";
        var result = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (result != null) return result;
        var vertices = source.vertices; var normals = source.normals; var uv = source.uv; var tangents = source.tangents;
        var newVertices = new List<Vector3>(); var newNormals = new List<Vector3>(); var newUv = new List<Vector2>(); var newTangents = new List<Vector4>();
        var remap = new Dictionary<int,int>(); var indices = new List<int[]>();
        for (int sub = 0; sub < source.subMeshCount; sub++)
        {
            var data = source.GetIndices(sub, lod, true);
            for (int i = 0; i < data.Length; i++)
            {
                int old = data[i]; int next;
                if (!remap.TryGetValue(old, out next))
                {
                    next = newVertices.Count; remap.Add(old, next); newVertices.Add(vertices[old]);
                    if (normals.Length == vertices.Length) newNormals.Add(normals[old]);
                    if (uv.Length == vertices.Length) newUv.Add(uv[old]);
                    if (tangents.Length == vertices.Length) newTangents.Add(tangents[old]);
                }
                data[i] = next;
            }
            indices.Add(data);
        }
        result = new Mesh { name = source.name + " Android " + name };
        result.SetVertices(newVertices);
        if (newNormals.Count > 0) result.SetNormals(newNormals);
        if (newUv.Count > 0) result.SetUVs(0, newUv);
        if (newTangents.Count > 0) result.SetTangents(newTangents);
        result.subMeshCount = indices.Count;
        for (int sub = 0; sub < indices.Count; sub++) result.SetIndices(indices[sub], source.GetTopology(sub), sub, false);
        result.bounds = source.bounds; AssetDatabase.CreateAsset(result, path);
        AssetDatabase.SaveAssetIfDirty(result); return result;
    }
}
#endif
