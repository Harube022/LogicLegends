using UnityEditor;
using UnityEngine;

public static class RebuildFunctionsRelationsChallengeVariants
{
    private const string CatalogPath = "Assets/Resources/FunctionsRelationsChallenges.asset";

    [MenuItem("Logic Legends/Rebuild Functions and Relations Challenge Variants")]
    public static void Rebuild()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<MappingChallengeCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<MappingChallengeCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }
        catalog.SetChallengeGroups(MappingChallengeVariantFactory.BuildGroups());
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log("Functions and Relations now has ten four-diamond variants for each of its five challenges.");
    }
}
