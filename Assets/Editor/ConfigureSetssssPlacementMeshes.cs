using UnityEditor;
using UnityEngine;

/// <summary>Persist CPU geometry access required by position-based region validation.</summary>
public static class ConfigureSetssssPlacementMeshes
{
    [MenuItem("Tools/Logic Legends/Setssss/Prepare Placement Meshes")]
    public static void Configure()
    {
        foreach (string path in new[]
        {
            "Assets/FINAL_ASSETS/MAPS/REMADE/S3L7/AB.fbx",
            "Assets/FINAL_ASSETS/MAPS/REMADE/S3L7/A-B.fbx",
            "Assets/FINAL_ASSETS/MAPS/REMADE/S3L7/B-A.fbx"
        })
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null || importer.isReadable) continue;
            importer.isReadable = true;
            importer.SaveAndReimport();
        }
        foreach (string path in new[]
        {
            "Assets/Generated/Setssss/AB_ExistingA_Challenge5.asset",
            "Assets/Generated/Setssss/AB_ExistingB_Challenge5.asset"
        })
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) continue;
            var data = new SerializedObject(mesh);
            var readable = data.FindProperty("m_IsReadable");
            if (readable == null || readable.boolValue) continue;
            readable.boolValue = true;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(mesh);
        }
        AssetDatabase.SaveAssets();
    }
}
