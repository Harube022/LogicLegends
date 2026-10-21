#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LogicLegends.OpenArchive.Hunting;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace LogicLegends.OpenArchive.Editor
{
    /// <summary>Fits the existing generated tree boxes to their bark, keeping foliage and imported meshes intact.</summary>
    public static class OpenArchiveTreeCollisionRepair
    {
        const string Map = "Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE";
        [Serializable] sealed class TreeCheck
        {
            public string prefab;
            public Vector3 previousSize, trunkSize;
            public bool geometryAndMaterialsPreserved, transformsPreserved;
        }
        [Serializable] sealed class NavigationCheck
        {
            public string asset;
            public int sources;
            public bool updated, settingsPreserved;
        }
        [Serializable] sealed class Report
        {
            public string checkedUtc;
            public bool passed;
            public int sceneTrees;
            public List<TreeCheck> trees = new List<TreeCheck>();
            public List<NavigationCheck> navigation = new List<NavigationCheck>();
        }
        static Bounds Trunk(Mesh mesh)
        {
            Bounds wood = default, baseWood = default; bool hasWood = false, hasBase = false;
            // Editor mesh access works without enabling Read/Write on Android meshes.
            using (var data = MeshUtility.AcquireReadOnlyMeshData(mesh))
            using (var vertices = new NativeArray<Vector3>(mesh.vertexCount, Allocator.Temp))
            using (var tags = new NativeArray<Vector4>(mesh.vertexCount, Allocator.Temp))
            {
                data[0].GetVertices(vertices); data[0].GetUVs(2, tags);
                for (int i = 0; i < vertices.Length; i++)
                {
                    // The existing TreeTimber UV channel identifies bark as w=0.
                    if (tags[i].w > .5f) continue;
                    if (!hasWood) { wood = new Bounds(vertices[i], Vector3.zero); hasWood = true; } else wood.Encapsulate(vertices[i]);
                    if (vertices[i].y > mesh.bounds.min.y + mesh.bounds.size.y * .2f) continue;
                    if (!hasBase) { baseWood = new Bounds(vertices[i], Vector3.zero); hasBase = true; } else baseWood.Encapsulate(vertices[i]);
                }
            }
            if (!hasWood || !hasBase || wood.size.y < .5f) throw new InvalidOperationException("Missing tree bark data: " + mesh.name);
            return new Bounds(new Vector3(baseWood.center.x, wood.center.y, baseWood.center.z),
                new Vector3(Mathf.Max(.25f, baseWood.size.x + .08f), wood.size.y + .08f, Mathf.Max(.25f, baseWood.size.z + .08f)));
        }
        static string Transforms(GameObject root) => string.Join(";", root.GetComponentsInChildren<Transform>(true).Select(t => t.name + t.localPosition.ToString("R") + t.localRotation.ToString("R") + t.localScale.ToString("R")));
        static string Art(GameObject root) => string.Join(";", root.GetComponentsInChildren<MeshFilter>(true).Select(f => AssetDatabase.GetAssetPath(f.sharedMesh) + ":" + AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(f.sharedMesh)))) +
            string.Join(";", root.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Select(m => AssetDatabase.GetAssetPath(m)));

        [MenuItem("Tools/Logic Legends/Open Archive/Fit Tree Trunks And Update Monster Navigation")]
        static void RepairFromMenu() { Debug.Log(Repair()); }

        public static string Repair()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new InvalidOperationException("Use stopped, compiled Edit Mode.");
            var environment = GameObject.Find("OpenArchive");
            if (environment == null || environment.scene.isDirty) throw new InvalidOperationException("Open the saved Open Archive scene first.");
            var report = new Report { checkedUtc = DateTime.UtcNow.ToString("o") };
            for (int i = 1; i <= 5; i++)
            {
                string path = Map + "/Prefabs/Nature/Archive_Tree_0" + i + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    string transforms = Transforms(root), art = Art(root);
                    var box = root.GetComponentInChildren<BoxCollider>(true);
                    var check = new TreeCheck { prefab = path, previousSize = box.size };
                    var fit = Trunk(box.GetComponent<MeshFilter>().sharedMesh); box.center = fit.center; box.size = fit.size;
                    check.trunkSize = box.size; check.transformsPreserved = transforms == Transforms(root); check.geometryAndMaterialsPreserved = art == Art(root);
                    if (!check.transformsPreserved || !check.geometryAndMaterialsPreserved) throw new InvalidOperationException("Tree source changed outside collision.");
                    PrefabUtility.SaveAsPrefabAsset(root, path); report.trees.Add(check);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            bool sceneChanged = false;
            foreach (var box in environment.transform.Find("Environment/Trees").GetComponentsInChildren<BoxCollider>(true))
            {
                if (!box.name.StartsWith("Tree_")) continue;
                var fit = Trunk(box.GetComponent<MeshFilter>().sharedMesh);
                if (Vector3.Distance(box.center, fit.center) > .0001f || Vector3.Distance(box.size, fit.size) > .0001f)
                {
                    box.center = fit.center; box.size = fit.size; PrefabUtility.RecordPrefabInstancePropertyModifications(box); sceneChanged = true;
                }
                report.sceneTrees++;
            }
            Physics.SyncTransforms();
            var valid = environment.GetComponentsInChildren<Collider>(true).Where(c => c.enabled && !c.isTrigger && c.gameObject.activeInHierarchy && !(c is CharacterController)).Select(c => c.GetInstanceID()).ToHashSet();
            var collected = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(environment.transform, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), collected);
            var sources = collected.Where(s => s.component != null && valid.Contains(s.component.GetInstanceID())).Select(s =>
            {
                for (var t = s.component.transform; t != null && t != environment.transform; t = t.parent)
                    if (t.name == "Cliffs" || t.name == "Rivers" || t.name == "Ponds" || t.name == "Waterfalls") { s.area = 1; break; }
                return s;
            }).ToList();
            foreach (var nav in Object.FindObjectsByType<KhezuNavigationData>(FindObjectsSortMode.None).Select(l => l.data).Distinct())
            {
                int type = new SerializedObject(nav).FindProperty("m_AgentTypeID").intValue;
                var settings = NavMesh.GetSettingsByID(type); string originalSettings = JsonUtility.ToJson(settings);
                bool updated = NavMeshBuilder.UpdateNavMeshData(nav, settings, sources, nav.sourceBounds);
                report.navigation.Add(new NavigationCheck { asset = AssetDatabase.GetAssetPath(nav), sources = sources.Count, updated = updated, settingsPreserved = originalSettings == JsonUtility.ToJson(NavMesh.GetSettingsByID(type)) });
                if (!updated) throw new InvalidOperationException("Could not update existing monster navigation.");
                EditorUtility.SetDirty(nav);
            }
            if (sceneChanged) { EditorSceneManager.MarkSceneDirty(environment.scene); EditorSceneManager.SaveScene(environment.scene); }
            AssetDatabase.SaveAssets();
            report.passed = report.trees.Count == 5 && report.sceneTrees > 0 && report.navigation.Count == 2 && report.navigation.All(n => n.updated && n.settingsPreserved) && report.trees.All(t => t.transformsPreserved && t.geometryAndMaterialsPreserved);
            string json = JsonUtility.ToJson(report, true);
            File.WriteAllText(Map + "/Art/Reports/TreeTrunkCollisionRepair.json", json);
            return json;
        }
    }
}
#endif
