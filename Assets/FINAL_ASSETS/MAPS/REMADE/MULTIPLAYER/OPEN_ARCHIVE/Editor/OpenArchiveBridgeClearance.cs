using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LogicLegends.OpenArchive.Hunting;
using LogicLegends.Tests.Khezu.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace OpenArchiveAuthoring
{
    // An authoring repair only. The existing gameplay, monster size and agent settings are retained.
    public static class OpenArchiveBridgeClearance
    {
        const string Folder = OpenArchiveBuilder.Folder;
        const float RailScale = 15f / 22.78f;
        public const string Reports = Folder + "/Testing/KhezuHunting/Reports";
        static readonly string[] Scenes = {
            Folder + "/Scenes/MultiplayerGameplay.unity",
            Folder + "/Scenes/OpenArchive_Explore.unity",
            Folder + "/Testing/KhezuAwakening/OpenArchive_KhezuAwakening_Test.unity",
            KhezuHuntingSetup.ScenePath,
            Folder + "/Testing/KhezuCinematic/OpenArchive_KhezuCinematic_Test.unity"
        };

        [Serializable] public class Check { public string name, detail; public bool passed; }
        [Serializable] public class Report {
            public string checkedUtc;
            public bool passed;
            public List<Check> checks = new List<Check>();
        }
        static void CheckResult(Report r, string name, bool pass, string detail = "") {
            r.checks.Add(new Check { name = name, passed = pass, detail = detail });
        }
        public static void Save(Report r, string name) {
            r.passed = r.checks.Count > 0 && r.checks.All(c => c.passed);
            Directory.CreateDirectory(Reports);
            File.WriteAllText(Reports + "/" + name + ".json", JsonUtility.ToJson(r, true));
        }

        [MenuItem("Tools/Logic Legends/Open Archive/Repair Bridge Path Clearance")]
        public static void Repair()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open edits first.");
            var active = SceneManager.GetActiveScene().path;
            var report = new Report { checkedUtc = DateTime.UtcNow.ToString("o") };
            var prefabPath = Folder + "/Prefabs/Bridges/Bridge_Modular_10x24m.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
            try {
                var deck = new Geometry();
                for (int i = 0; i < 17; i++)
                    deck.Box(new Vector3(0, .05f, -8 + i), new Vector3(10, .4f, .94f), i % 3 == 0 ? 10 : 11);
                Replace(prefab.transform.Find("Wooden_Bridge_Deck").GetComponent<MeshFilter>().sharedMesh, deck);
                var boxes = prefab.GetComponents<BoxCollider>();
                if (boxes.Length != 3) throw new InvalidOperationException("Unexpected bridge collider layout.");
                boxes[0].center = new Vector3(0, .02f, 0); boxes[0].size = new Vector3(10, .46f, 17);
                for (int i = 1; i < 3; i++) {
                    boxes[i].center = new Vector3(i == 1 ? -4.8f : 4.8f, 1, .5f);
                    boxes[i].size = new Vector3(.25f, 1.5f, 15);
                }
                var rails = prefab.GetComponentsInChildren<Transform>().Where(t => t.name == "Bridge_Railing_Module").ToArray();
                if (rails.Length != 18) throw new InvalidOperationException("Unexpected bridge railing modules.");
                for (int i = 0; i < rails.Length; i++) {
                    rails[i].localPosition = new Vector3(i < 9 ? -4.8f : 4.8f, .25f, .5f + (-10 + i % 9 * 2.5f) * RailScale);
                    rails[i].localScale = new Vector3(RailScale, 1, 1);
                }
                var ramps = new Geometry();
                ramps.Quad(new Vector3(-5, 0, -9.5f), new Vector3(-5, .25f, -8.5f), new Vector3(5, .25f, -8.5f), new Vector3(5, 0, -9.5f), 47);
                ramps.Quad(new Vector3(-5, .25f, 8.5f), new Vector3(-5, 0, 10.5f), new Vector3(5, 0, 10.5f), new Vector3(5, .25f, 8.5f), 47);
                var ramp = prefab.transform.Find("Bridge_Approach_Ramps");
                Replace(ramp.GetComponent<MeshFilter>().sharedMesh, ramps);
                ramp.GetComponent<MeshCollider>().sharedMesh = ramp.GetComponent<MeshFilter>().sharedMesh;
                PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            for (int i = 0; i < 4; i++) {
                var approach = new Geometry();
                approach.Ribbon(45 + 90 * i, 24.4f, 27, 12, 10, .047f, 46);
                approach.Ribbon(45 + 90 * i, 44, 51, 10, 12, .047f, 46);
                Replace(AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/Meshes/Bridge_Dry_Approach_" + i + ".asset"), approach);
            }
            AssetDatabase.SaveAssets();
            try {
                foreach (var path in Scenes) {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    var root = scene.GetRootGameObjects().Single(g => g.name == "OpenArchive");
                    var before = root.GetComponentsInChildren<Transform>(true).ToDictionary(t => t, t => new[] {t.localPosition, t.localEulerAngles, t.localScale});
                    int piers = 0;
                    foreach (Transform bridge in root.transform.Find("Environment/Bridges")) {
                        var support = bridge.Find("Mossy_Ancient_Bridge_Piers");
                        if (support == null) continue;
                        support.localScale = new Vector3(1, 1, 7.3f / 8.9f); piers++;
                    }
                    bool preserved = before.All(pair => pair.Key.name == "Mossy_Ancient_Bridge_Piers" ||
                        (pair.Key.localPosition == pair.Value[0] && pair.Key.localEulerAngles == pair.Value[1] && pair.Key.localScale == pair.Value[2]));
                    CheckResult(report, Path.GetFileNameWithoutExtension(path) + " layout retained", preserved && piers == 4,
                        before.Count + " transforms retained; only the four bridge supports moved under the shorter span.");
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save " + path);
                }
                // Both hunting scenes use the same environment geometry and the same world-scale navigation asset.
                var cinematic = EditorSceneManager.OpenScene(Scenes[4], OpenSceneMode.Single);
                var environment = cinematic.GetRootGameObjects().Single(g => g.name == "OpenArchive");
                var hunting = UnityEngine.Object.FindFirstObjectByType<KhezuHuntingController>();
                var agent = hunting.GetComponent<NavMeshAgent>();
                float radius = agent.radius, height = agent.height;
                UnityEngine.Object.FindFirstObjectByType<KhezuNavigationData>().data = KhezuHuntingSetup.Bake(environment, agent);
                CheckResult(report, "Khezu retains original body clearance", agent.radius == radius && agent.height == height && Mathf.Approximately(radius, 9.7f));
                EditorSceneManager.SaveScene(cinematic);
                AssetDatabase.SaveAssets();
            }
            finally { if (!string.IsNullOrEmpty(active) && SceneManager.GetActiveScene().path != active) EditorSceneManager.OpenScene(active, OpenSceneMode.Single); }
            Save(report, "BridgeRepair");
        }

        public static void VerifyRoutes(string reportName)
        {
            var report = new Report { checkedUtc = DateTime.UtcNow.ToString("o") };
            var root = GameObject.Find("OpenArchive").transform;
            var loader = UnityEngine.Object.FindFirstObjectByType<KhezuNavigationData>();
            var monster = UnityEngine.Object.FindFirstObjectByType<KhezuHuntingController>();
            Vector3 offset = new Vector3(20000, 0, 20000);
            var instance = NavMesh.AddNavMeshData(loader.data, offset, Quaternion.identity);
            try {
                var filter = new NavMeshQueryFilter {agentTypeID = monster.Agent.agentTypeID, areaMask = NavMesh.AllAreas};
                for (int i = 0; i < 4; i++) {
                    float angle = 45 + i * 90;
                    for (int direction = -1; direction <= 1; direction += 2) {
                        Route(report, root, offset, filter, "Bridge " + i + " inner loop " + direction,
                            Polar(angle - 20 * direction, 24.4f), Polar(angle + 20 * direction, 24.4f), 60, 21, 29);
                        Route(report, root, offset, filter, "Bridge " + i + " outer loop " + direction,
                            Polar(angle - 15 * direction, 54), Polar(angle + 15 * direction, 54), 90, 47, 61);
                        Route(report, root, offset, filter, "Bridge " + i + " deck " + direction,
                            Polar(angle, direction == 1 ? 24.4f : 54), Polar(angle, direction == 1 ? 54 : 24.4f), 85, 22, 56);
                        Route(report, root, offset, filter, "Bridge " + i + " inner turn " + direction,
                            Polar(angle, 35.5f), Polar(angle + 20 * direction, 24.4f), 58, 21, 37);
                    }
                }
            }
            finally { if (instance.valid) instance.Remove(); }
            Save(report, reportName);
        }
        static void Route(Report r, Transform root, Vector3 offset, NavMeshQueryFilter filter, string name, Vector3 from, Vector3 to, float maxLength, float minRadius, float maxRadius) {
            NavMeshHit a, b;
            bool found = NavMesh.SamplePosition(root.TransformPoint(from) + offset, out a, 2, filter) & NavMesh.SamplePosition(root.TransformPoint(to) + offset, out b, 2, filter);
            var path = new NavMeshPath();
            bool pass = found && NavMesh.CalculatePath(a.position, b.position, filter, path) && path.status == NavMeshPathStatus.PathComplete;
            float length = 0, min = float.MaxValue, max = 0;
            for (int i = 0; i < path.corners.Length; i++) {
                if (i > 0) length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
                Vector3 previous = path.corners[Mathf.Max(0, i - 1)];
                for (int s = 0; s <= 20; s++) {
                    var point = root.InverseTransformPoint(Vector3.Lerp(previous, path.corners[i], s / 20f) - offset);
                    float radius = new Vector2(point.x, point.z).magnitude;
                    min = Mathf.Min(min, radius); max = Mathf.Max(max, radius);
                }
            }
            CheckResult(r, name, pass && length <= maxLength && min >= minRadius && max <= maxRadius,
                "length=" + length.ToString("F2") + "m, source radius=" + min.ToString("F2") + ".." + max.ToString("F2") + ", " + path.status);
        }
        public static Vector3 Polar(float angle, float radius) { angle *= Mathf.Deg2Rad; return new Vector3(Mathf.Sin(angle) * radius, 0, Mathf.Cos(angle) * radius); }

        static void Replace(Mesh mesh, Geometry geometry) {
            if (mesh == null) throw new InvalidOperationException("Missing original bridge mesh.");
            Undo.RegisterCompleteObjectUndo(mesh, "Repair bridge clearance");
            mesh.Clear(); mesh.SetVertices(geometry.vertices); mesh.SetUVs(0, geometry.uv); mesh.SetTriangles(geometry.triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
        }
        sealed class Geometry {
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> triangles = new List<int>();
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int tile) {
                int start = vertices.Count; vertices.AddRange(new[] {a, b, c, d});
                for (int i = 0; i < 4; i++) uv.Add(new Vector2((tile % 8 + .5f) / 8, (tile / 8 + .5f) / 8));
                triangles.AddRange(new[] {start, start + 1, start + 2, start, start + 2, start + 3});
            }
            public void Ribbon(float angle, float from, float to, float startWidth, float endWidth, float y, int tile) {
                var a = Polar(angle, from) + Vector3.up * y; var b = Polar(angle, to) + Vector3.up * y;
                var side = Polar(angle + 90, 1);
                Quad(a - side * startWidth * .5f, b - side * endWidth * .5f, b + side * endWidth * .5f, a + side * startWidth * .5f, tile);
            }
            public void Box(Vector3 center, Vector3 size, int tile) {
                var a = center - size * .5f; var b = center + size * .5f;
                Quad(new Vector3(a.x,b.y,a.z),new Vector3(a.x,b.y,b.z),new Vector3(b.x,b.y,b.z),new Vector3(b.x,b.y,a.z),tile);
                Quad(new Vector3(a.x,a.y,a.z),new Vector3(b.x,a.y,a.z),new Vector3(b.x,a.y,b.z),new Vector3(a.x,a.y,b.z),tile);
                Quad(new Vector3(a.x,a.y,a.z),new Vector3(a.x,a.y,b.z),new Vector3(a.x,b.y,b.z),new Vector3(a.x,b.y,a.z),tile);
                Quad(new Vector3(b.x,a.y,b.z),new Vector3(b.x,a.y,a.z),new Vector3(b.x,b.y,a.z),new Vector3(b.x,b.y,b.z),tile);
                Quad(new Vector3(b.x,a.y,a.z),new Vector3(a.x,a.y,a.z),new Vector3(a.x,b.y,a.z),new Vector3(b.x,b.y,a.z),tile);
                Quad(new Vector3(a.x,a.y,b.z),new Vector3(b.x,a.y,b.z),new Vector3(b.x,b.y,b.z),new Vector3(a.x,b.y,b.z),tile);
            }
        }
    }
}
