#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LogicLegends.OpenArchive.Hunting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LogicLegends.OpenArchive.Editor
{
    /// <summary>Authoring-only repair for solid scenery. No runtime collision scans or player changes.</summary>
    public static class OpenArchiveSolidCollisionRepair
    {
        const string Map = "Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE";
        const string ScenePath = "Assets/Scenes/MultiplayerGameplay.unity";
        static readonly string[] RootNames = { "OpenArchive", "Archive_Environment_Polish", "Archive_Field_Event_Presentation" };
        // These meshes contain disconnected pieces. Exact static meshes preserve the gaps;
        // one bounding box around a cluster would block roads, water and bridge underpasses.
        static readonly HashSet<string> SolidNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "Question_Board_Frame", "Board_Support", "Topic_Header_Backplate",
            "Weathered_Stone_Pedestal", "Pillar_Collar", "Stone_Post", "Brass_Frame",
            "Chamfered_Stone_And_Bench", "Weathered_Bank_Marker", "Weathered_Stone_Pylon",
            "Riverbank_RockClusters", "Clearing_Perimeter_Stones_And_Moss", "Mossy_Cascade_Stone_Mouths",
            "Island_CliffFoundation", "Broken_Verge_Masonry", "Weathered_Masonry_Sides_And_Undersides",
            "Diagonal_Broken_Courtyard_Stones", "Irregular_Retaining_Stones_And_Buried_Chips", "Weathered_Frontage_Ruins"
        };

        [Serializable] public sealed class Change { public string path, mesh; public int triangles; public bool added; }
        [Serializable] public sealed class NavigationCheck { public string asset; public int sources; public bool updated; }
        [Serializable] public sealed class Report
        {
            public string checkedUtc, scene;
            public bool passed, visualsPreserved, existingCollisionPreserved;
            public int addedSceneColliders, refreshedSceneMeshes, addedPrefabColliders, remainingMissing;
            public List<Change> sceneChanges = new List<Change>();
            public List<NavigationCheck> navigation = new List<NavigationCheck>();
            public List<string> updatedPrefabs = new List<string>();
        }
        public static bool IsSolid(MeshFilter filter) => filter != null && filter.sharedMesh != null &&
            SolidNames.Contains(filter.name) && filter.GetComponentInParent<Rigidbody>() == null;
        public static string PathOf(Transform transform)
        {
            var names = new List<string>();
            for (var t = transform; t != null; t = t.parent) names.Add(t.name + "[" + t.GetSiblingIndex() + "]");
            names.Reverse(); return string.Join("/", names);
        }
        static GameObject[] Roots() => SceneManager.GetActiveScene().GetRootGameObjects().Where(g => RootNames.Contains(g.name)).ToArray();
        public static MeshFilter[] SolidMeshes() => Roots().SelectMany(r => r.GetComponentsInChildren<MeshFilter>(true)).Where(IsSolid).ToArray();
        static bool Covered(MeshFilter filter) => filter.GetComponentsInParent<Collider>(true).Any(c => c.enabled && !c.isTrigger && !(c is CharacterController));
        // TMP creates font-fallback submeshes lazily when a prefab is reimported.
        // Compare its authored text/font, not those transient generated children.
        static bool Authored(Component component) => component.GetComponent<TMPro.TMP_SubMesh>() == null;
        static string Visuals(GameObject[] roots) => string.Join(";", roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Where(Authored)
            .Select(t => PathOf(t) + t.localPosition.ToString("R") + t.localRotation.ToString("R") + t.localScale.ToString("R") + ":" + t.gameObject.activeSelf)) +
            string.Join(";", roots.SelectMany(r => r.GetComponentsInChildren<MeshFilter>(true)).Where(Authored).Select(f => PathOf(f.transform) + ":" + AssetKey(f.sharedMesh))) +
            string.Join(";", roots.SelectMany(r => r.GetComponentsInChildren<Renderer>(true)).Where(Authored).Select(r => PathOf(r.transform) + ":" + string.Join(",", r.sharedMaterials.Select(AssetKey)))) +
            string.Join(";", roots.SelectMany(r => r.GetComponentsInChildren<TMPro.TMP_Text>(true)).Select(t => PathOf(t.transform) + ":" + t.text + ":" + AssetKey(t.font)));
        static string AssetKey(Object asset) => asset == null ? "null" : AssetDatabase.GetAssetPath(asset) + ":" + asset.name;
        static string CollisionKey(Collider collider)
        {
            // Saving a nested prefab can reinstantiate its native objects. Instance IDs
            // are not physics settings; compare stable paths and serialized values.
            string json = System.Text.RegularExpressions.Regex.Replace(EditorJsonUtility.ToJson(collider), "\"instanceID\":\\s*-?\\d+", "\"instanceID\":0");
            var mesh = collider as MeshCollider;
            return json + ":" + collider.gameObject.layer + ":" + AssetKey(collider.sharedMaterial) + ":" + (mesh != null ? AssetKey(mesh.sharedMesh) : "");
        }
        static Dictionary<string, string> CollisionSnapshot(GameObject[] roots) => roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true))
            .SelectMany(t => t.GetComponents<Collider>().Select((c, index) => new { key = PathOf(t) + "/" + c.GetType().Name + "[" + index + "]", value = CollisionKey(c) }))
            .ToDictionary(p => p.key, p => p.value);

        [MenuItem("Tools/Logic Legends/Open Archive/Repair Missing Solid Scenery Colliders")]
        static void Menu() => Debug.Log(Repair("Temp/ArchiveSolidCollision"));

        public static string Repair(string outputFolder)
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || scene.path != ScenePath || scene.isDirty)
                throw new InvalidOperationException("Use the saved MultiplayerGameplay scene in stopped, compiled Edit Mode.");
            var roots = Roots();
            if (roots.Length != RootNames.Length) throw new InvalidOperationException("The three existing environment roots are required.");
            Directory.CreateDirectory(outputFolder);
            var report = new Report { scene = scene.path, checkedUtc = DateTime.UtcNow.ToString("o") };
            var originalVisuals = Visuals(roots);
            var originalColliders = CollisionSnapshot(roots);
            var navigation = Object.FindObjectsByType<KhezuNavigationData>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(n => n.data != null).Select(n => n.data).Distinct().ToArray();
            if (navigation.Length != 2) throw new InvalidOperationException("Both existing monster navigation assets are required.");
            // Backups are outside Assets; never create competing scene/prefab GUIDs.
            string backup = System.IO.Path.Combine(outputFolder, "before-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(backup);
            File.Copy(scene.path, System.IO.Path.Combine(backup, "MultiplayerGameplay.unity"));
            foreach (var nav in navigation) File.Copy(AssetDatabase.GetAssetPath(nav), System.IO.Path.Combine(backup, nav.name + ".asset"));

            var prefabPaths = SolidMeshes().Select(f => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(f.gameObject))
                .Where(p => !string.IsNullOrEmpty(p)).Distinct().ToArray();
            foreach (string path in prefabPaths)
            {
                File.Copy(path, System.IO.Path.Combine(backup, System.IO.Path.GetFileName(path)));
                var prefab = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int added = 0;
                    foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true).Where(IsSolid))
                    {
                        if (Covered(filter)) continue;
                        var collider = filter.gameObject.AddComponent<MeshCollider>();
                        collider.sharedMesh = filter.sharedMesh;
                        collider.convex = false; collider.isTrigger = false;
                        added++;
                    }
                    if (added > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(prefab, path);
                        report.addedPrefabColliders += added; report.updatedPrefabs.Add(path);
                    }
                }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }

            Undo.SetCurrentGroupName("Repair archive solid scenery collision");
            foreach (var filter in SolidMeshes())
            {
                var collider = filter.GetComponent<MeshCollider>();
                bool added = false, changed = false;
                if (collider == null && !Covered(filter))
                {
                    collider = Undo.AddComponent<MeshCollider>(filter.gameObject); added = changed = true;
                    report.addedSceneColliders++;
                }
                // A scene mesh override (e.g. the carved instruction board) must not inherit
                // a collider that still references the prefab's older rectangular mesh.
                if (collider != null && collider.sharedMesh != filter.sharedMesh)
                {
                    Undo.RecordObject(collider, "Fit scenery collider to its authored mesh");
                    collider.sharedMesh = filter.sharedMesh; changed = true;
                    if (!added) report.refreshedSceneMeshes++;
                }
                if (!changed) continue;
                if (PrefabUtility.IsPartOfPrefabInstance(filter)) PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
                report.sceneChanges.Add(new Change { path = PathOf(filter.transform), mesh = filter.sharedMesh.name,
                    triangles = TriangleCount(filter.sharedMesh), added = added });
            }
            Physics.SyncTransforms();
            string currentVisuals = Visuals(roots);
            report.visualsPreserved = originalVisuals == currentVisuals;
            var currentColliders = CollisionSnapshot(roots);
            report.existingCollisionPreserved = originalColliders.All(p => currentColliders.TryGetValue(p.Key, out var current) && current == p.Value);
            report.remainingMissing = SolidMeshes().Count(f => !Covered(f));
            if (!report.visualsPreserved || !report.existingCollisionPreserved || report.remainingMissing != 0)
            {
                File.WriteAllText(System.IO.Path.Combine(outputFolder, "FailedPreflight.json"), JsonUtility.ToJson(report, true));
                File.WriteAllText(System.IO.Path.Combine(outputFolder, "ChangedExistingColliders.txt"), string.Join("\n", originalColliders
                    .Where(p => !currentColliders.TryGetValue(p.Key, out var current) || current != p.Value).Select(p => p.Key)));
                File.WriteAllText(System.IO.Path.Combine(outputFolder, "ChangedVisuals.txt"), "BEFORE ONLY\n" + string.Join("\n", originalVisuals.Split(';').Except(currentVisuals.Split(';'))) +
                    "\nAFTER ONLY\n" + string.Join("\n", currentVisuals.Split(';').Except(originalVisuals.Split(';'))));
                throw new InvalidOperationException("Collision-only preservation check failed; inspect the saved backup before proceeding.");
            }

            if (report.sceneChanges.Count > 0 || report.updatedPrefabs.Count > 0)
            {
                var sources = Sources(roots);
                foreach (var nav in navigation)
                {
                    int type = new SerializedObject(nav).FindProperty("m_AgentTypeID").intValue;
                    bool updated = NavMeshBuilder.UpdateNavMeshData(nav, NavMesh.GetSettingsByID(type), sources, nav.sourceBounds);
                    report.navigation.Add(new NavigationCheck { asset = AssetDatabase.GetAssetPath(nav), sources = sources.Count, updated = updated });
                    if (!updated) throw new InvalidOperationException("Existing monster navigation update failed: " + nav.name);
                    EditorUtility.SetDirty(nav);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save repaired scene.");
                AssetDatabase.SaveAssets();
            }
            report.passed = report.visualsPreserved && report.existingCollisionPreserved && report.remainingMissing == 0 && report.navigation.All(n => n.updated);
            string json = JsonUtility.ToJson(report, true);
            File.WriteAllText(System.IO.Path.Combine(outputFolder, "Repair.json"), json);
            return json;
        }
        static int TriangleCount(Mesh mesh)
        {
            int count = 0; for (int i = 0; i < mesh.subMeshCount; i++) count += (int)mesh.GetIndexCount(i) / 3;
            return count;
        }
        static List<NavMeshBuildSource> Sources(GameObject[] roots)
        {
            var result = new List<NavMeshBuildSource>();
            foreach (var root in roots)
            {
                var valid = new HashSet<int>(root.GetComponentsInChildren<Collider>(false)
                    .Where(c => c.enabled && !c.isTrigger && !(c is CharacterController)).Select(c => c.GetInstanceID()));
                var collected = new List<NavMeshBuildSource>();
                NavMeshBuilder.CollectSources(root.transform, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), collected);
                foreach (var entry in collected)
                {
                    if (entry.component == null || !valid.Contains(entry.component.GetInstanceID())) continue;
                    var source = entry;
                    for (var t = source.component.transform; t != null && t != root.transform; t = t.parent)
                        if (t.name == "Cliffs" || t.name == "Rivers" || t.name == "Ponds" || t.name == "Waterfalls") { source.area = 1; break; }
                    result.Add(source);
                }
            }
            return result;
        }
    }
}
#endif
