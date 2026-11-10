#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

// Build-copy changes only: avoid first cooking the huge render mesh at activation.
public sealed class AndroidEnvironmentBuildProcessor : IProcessSceneWithReport
{
    public int callbackOrder => 0;
    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (report == null || report.summary.platform != BuildTarget.Android || !SoloGameSettings.IsTrial(scene.name)) return;
        Apply(scene);
    }
    public static int Apply(Scene scene)
    {
        var catalog = Resources.Load<SoloMobileMeshes>("Settings/SoloMobileMeshes");
        if (catalog == null) throw new BuildFailedException("Prepare the shared Android mesh catalog before building.");
        int changed = 0;
        foreach (var root in scene.GetRootGameObjects()) foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var source = filter.sharedMesh; if (source == null) continue;
            var high = catalog.Resolve(source, 2); if (high == source) continue;
            filter.sharedMesh = high;
            var flags = GameObjectUtility.GetStaticEditorFlags(filter.gameObject);
            GameObjectUtility.SetStaticEditorFlags(filter.gameObject, flags & ~StaticEditorFlags.BatchingStatic);
            var collider = filter.GetComponent<MeshCollider>();
            if (collider != null && collider.sharedMesh == source && !collider.isTrigger)
                collider.sharedMesh = catalog.Resolve(source, 0);
            changed++;
        }
        return changed;
    }
}
#endif
