using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace OpenArchiveAuthoring
{
    /// <summary>Measurements of the loaded authored geometry, plus a camera-rendered debug image.</summary>
    public static class OpenArchiveImprovementReview
    {
        const string Folder = OpenArchiveBuilder.Folder;
        [Serializable] public class Footprint
        {
            public string name;
            public Vector3 center;
            public float minX, maxX, minZ, maxZ, width, depth, groundMinY, groundMaxY, circulationWidth;
            public int groundSamples, obstructingGeometryTriangles;
            public bool flat, clear, circulationClear;
        }
        [Serializable] public class Entrance
        {
            public string name;
            public Vector3 center;
            public float rampWidth, rampRise, rampRun;
            public bool playerClearAcrossWidth, largeBodyClearAcrossWidth;
        }
        [Serializable] public class Review
        {
            public bool passed, footprintsNonOverlapping, shadersSupported, allOriginalObjectsRetained;
            public string scene, generatedUtc;
            public int originalObjects, retainedObjects, meshRenderers, colliders, lodGroups, sharedMaterials, allLodTriangleUpperBound, staticRenderers, shadowCastingRenderers, realtimeLights;
            public float spawnMinimumSeparation;
            public List<Footprint> footprints = new List<Footprint>();
            public List<Entrance> entrances = new List<Entrance>();
            public List<string> materials = new List<string>();
            public List<string> limitations = new List<string>();
        }

        [MenuItem("Tools/Logic Legends/Open Archive/Measure Improved Environment")]
        public static Review Measure()
        {
            OpenArchiveImprovements.RequireSavedEditor();
            var scene = SceneManager.GetActiveScene();
            if (scene.path != OpenArchiveBuilder.ScenePath)
                scene = EditorSceneManager.OpenScene(OpenArchiveBuilder.ScenePath, OpenSceneMode.Single);
            var root = scene.GetRootGameObjects().Single(g => g.name == "OpenArchive").transform;
            var colliders = root.GetComponentsInChildren<Collider>().Where(c => c.enabled && !c.isTrigger).ToArray();
            var owned = new HashSet<int>(colliders.Select(c => c.GetInstanceID()));
            Physics.SyncTransforms();
            var report = new Review { scene = scene.path, generatedUtc = DateTime.UtcNow.ToString("o") };
            var slots = root.Find("FutureStructureSlots").Cast<Transform>().OrderBy(t => t.name).ToArray();
            foreach (var slot in slots)
            {
                var f = new Footprint { name = slot.name, center = slot.position, width = 14, depth = 14, circulationWidth = 6, groundMinY = float.PositiveInfinity, groundMaxY = float.NegativeInfinity };
                var corners = new[] { new Vector3(-7,0,-7), new Vector3(-7,0,7), new Vector3(7,0,-7), new Vector3(7,0,7) }.Select(slot.TransformPoint).ToArray();
                f.minX = corners.Min(p => p.x); f.maxX = corners.Max(p => p.x); f.minZ = corners.Min(p => p.z); f.maxZ = corners.Max(p => p.z);
                for (int x = -7; x <= 7; x++) for (int z = -7; z <= 7; z++)
                {
                    Vector3 p = slot.TransformPoint(new Vector3(x, 0, z));
                    if (!Support(colliders, p, out var hit)) continue;
                    f.groundSamples++; f.groundMinY = Mathf.Min(f.groundMinY, hit.point.y); f.groundMaxY = Mathf.Max(f.groundMaxY, hit.point.y);
                }
                f.flat = f.groundSamples == 225 && f.groundMaxY - f.groundMinY < .02f && Mathf.Abs(f.groundMaxY) < .05f;
                f.obstructingGeometryTriangles = CountDecorationTriangles(root, slot);
                f.clear = f.obstructingGeometryTriangles == 0 && !Physics.OverlapBox(slot.position + Vector3.up * 2.9f, new Vector3(7, 2.7f, 7), slot.rotation)
                    .Any(c => owned.Contains(c.GetInstanceID()) && c.bounds.max.y > .25f);
                f.circulationClear = true;
                for (int side = 0; side < 4; side++) for (int sample = 0; sample <= 28; sample++)
                {
                    float a = -10.5f + sample * 21f / 28;
                    Vector3 local = side == 0 ? new Vector3(a,0,10.5f) : side == 1 ? new Vector3(a,0,-10.5f) : side == 2 ? new Vector3(10.5f,0,a) : new Vector3(-10.5f,0,a);
                    if (!CapsuleClear(colliders, owned, slot.TransformPoint(local), 2.55f)) f.circulationClear = false;
                }
                report.footprints.Add(f);
            }
            report.footprintsNonOverlapping = report.footprints.All(a => report.footprints.All(b => a == b || a.maxX <= b.minX || a.minX >= b.maxX || a.maxZ <= b.minZ || a.minZ >= b.maxZ));
            var nest = root.Find("UniversalMonsterNest");
            foreach (string direction in new[] { "North", "South", "East", "West" })
            {
                var gate = nest.Find("Entrance_" + direction);
                var ramp = nest.Find("Ramp_" + direction + "_Clear10m/Smooth_Walkable_Collision").GetComponent<BoxCollider>();
                var entrance = new Entrance { name = direction, center = gate.position, rampWidth = ramp.size.x * ramp.transform.lossyScale.x, rampRise = .8f, rampRun = 8, playerClearAcrossWidth = true, largeBodyClearAcrossWidth = true };
                for (float r = 13.5f; r <= 25; r += .5f)
                {
                    for (float x = -4.1f; x <= 4.11f; x += .82f)
                        if (!CapsuleClear(colliders, owned, gate.forward * r + gate.right * x, .8f)) entrance.playerClearAcrossWidth = false;
                    for (float x = -2.35f; x <= 2.36f; x += .47f)
                        if (!CapsuleClear(colliders, owned, gate.forward * r + gate.right * x, 2.55f)) entrance.largeBodyClearAcrossWidth = false;
                }
                report.entrances.Add(entrance);
            }
            var spawns = root.Find("PlayerSpawnArea").Cast<Transform>().Where(t => t.name.StartsWith("PlayerSpawn_")).ToArray();
            report.spawnMinimumSeparation = spawns.Length == 4 ? spawns.SelectMany(a => spawns.Where(b => a != b).Select(b => Vector3.Distance(a.position, b.position))).Min() : 0;
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            var materials = renderers.SelectMany(r => r.sharedMaterials).Distinct().ToArray();
            report.shadersSupported = materials.All(m => m != null && m.shader.isSupported && m.GetTag("RenderPipeline", false) == "UniversalPipeline" && !ShaderUtil.GetShaderMessages(m.shader).Any(e => e.severity.ToString() == "Error"));
            report.materials = materials.Select(m => m.name + " — " + m.shader.name).ToList();
            report.meshRenderers = renderers.Length; report.sharedMaterials = materials.Length;
            report.colliders = colliders.Length; report.lodGroups = root.GetComponentsInChildren<LODGroup>(true).Length;
            report.allLodTriangleUpperBound = root.GetComponentsInChildren<MeshFilter>(true).Sum(m => m.sharedMesh.triangles.Length / 3);
            report.staticRenderers = renderers.Count(r => (GameObjectUtility.GetStaticEditorFlags(r.gameObject) & StaticEditorFlags.BatchingStatic) != 0);
            report.shadowCastingRenderers = renderers.Count(r => r.shadowCastingMode != ShadowCastingMode.Off);
            report.realtimeLights = root.GetComponentsInChildren<Light>().Count();
            string baseline = Folder + "/Editor/Authoring/OriginalSceneObjectIds.txt";
            if (File.Exists(baseline))
            {
                var old = File.ReadAllLines(baseline);
                var now = new HashSet<string>(root.GetComponentsInChildren<Transform>(true).Select(t => GlobalObjectId.GetGlobalObjectIdSlow(t).ToString()));
                report.originalObjects = old.Length; report.retainedObjects = old.Count(now.Contains); report.allOriginalObjectsRetained = report.originalObjects == report.retainedObjects;
            }
            report.limitations.Add("All-LOD triangle total includes every tree LOD simultaneously; it is not a rendered-frame or Android performance measurement.");
            report.limitations.Add("Geometry queries use 5.5m tall player and large-body capsules (radii0.8m and2.55m). Future animated monster silhouettes and physical Android multitouch/performance still need device testing.");
            report.limitations.Add("NavMesh route acceptance is recorded separately in NavigationValidation.json; no multiplayer or monster behaviour is implemented.");
            report.passed = slots.Length == 10 && report.footprints.All(f => f.flat && f.clear && f.circulationClear) && report.footprintsNonOverlapping
                && nest.Cast<Transform>().Count(t => t.name.StartsWith("Entrance_")) == 4 && report.entrances.All(e => e.playerClearAcrossWidth && e.largeBodyClearAcrossWidth && Mathf.Abs(e.rampWidth - 10) < .01f)
                && report.spawnMinimumSeparation >= 4 && report.shadersSupported && report.allOriginalObjectsRetained;
            string file = Folder + "/Previews/ImprovementMeasurements.json";
            File.WriteAllText(file, JsonUtility.ToJson(report, true)); AssetDatabase.ImportAsset(file);
            return report;
        }

        static bool Support(Collider[] colliders, Vector3 point, out RaycastHit best)
        {
            best = default; float distance = float.PositiveInfinity;
            var ray = new Ray(new Vector3(point.x, 6.6f, point.z), Vector3.down);
            foreach (var c in colliders)
                if (c.Raycast(ray, out var hit, 8) && hit.distance < distance) { best = hit; distance = hit.distance; }
            return !float.IsPositiveInfinity(distance) && best.point.y >= -.05f && best.point.y <= 1.1f;
        }

        static bool CapsuleClear(Collider[] colliders, HashSet<int> owned, Vector3 point, float radius)
        {
            if (!Support(colliders, point, out var ground)) return false;
            float sole = ground.point.y + .06f;
            return !Physics.OverlapCapsule(new Vector3(point.x, sole + radius, point.z), new Vector3(point.x, sole + 5.5f - radius, point.z), radius)
                .Any(c => owned.Contains(c.GetInstanceID()) && c.bounds.max.y > sole + .015f);
        }

        static int CountDecorationTriangles(Transform root, Transform slot)
        {
            int count = 0;
            // Combined vegetation bounds can span many plots, so inspect real triangles instead of renderer bounds alone.
            foreach (var f in root.GetComponentsInChildren<MeshFilter>(true))
            {
                string path = AnimationUtility.CalculateTransformPath(f.transform, root);
                if (!path.StartsWith("Environment/Trees/") && !path.StartsWith("Environment/Rocks/") && !path.StartsWith("Environment/Ruins/")) continue;
                var bounds = f.GetComponent<Renderer>().bounds;
                if (bounds.max.x < slot.position.x - 7 || bounds.min.x > slot.position.x + 7 || bounds.max.z < slot.position.z - 7 || bounds.min.z > slot.position.z + 7) continue;
                var vertices = f.sharedMesh.vertices; var t = f.sharedMesh.triangles;
                var matrix = slot.worldToLocalMatrix * f.transform.localToWorldMatrix;
                for (int i = 0; i < t.Length; i += 3)
                {
                    Vector3 a = matrix.MultiplyPoint3x4(vertices[t[i]]), b = matrix.MultiplyPoint3x4(vertices[t[i+1]]), c = matrix.MultiplyPoint3x4(vertices[t[i+2]]);
                    if (Mathf.Max(a.y, b.y, c.y) < .08f) continue;
                    if (Mathf.Max(a.x, b.x, c.x) < -7 || Mathf.Min(a.x, b.x, c.x) > 7 || Mathf.Max(a.z, b.z, c.z) < -7 || Mathf.Min(a.z, b.z, c.z) > 7) continue;
                    count++; // Conservative triangle AABB overlap; failures require visual/triangle review.
                }
            }
            return count;
        }

        [MenuItem("Tools/Logic Legends/Open Archive/Render Footprint Debug View")]
        public static void RenderDebug()
        {
            OpenArchiveImprovements.RequireSavedEditor();
            var root = SceneManager.GetActiveScene().GetRootGameObjects().Single(g => g.name == "OpenArchive").transform;
            var temporary = new GameObject("Temporary_Archive_Debug_Overlay");
            var cameraObject = new GameObject("Temporary_Debug_Camera", typeof(Camera)); cameraObject.transform.SetParent(temporary.transform, false);
            var camera = cameraObject.GetComponent<Camera>(); camera.enabled = false;
            camera.transform.SetPositionAndRotation(new Vector3(0, 220, 0), Quaternion.Euler(90, 0, 0));
            camera.orthographic = true; camera.orthographicSize = 110; camera.nearClipPlane = .3f; camera.farClipPlane = 700; camera.allowHDR = false;
            const int size = 1800;
            var texture = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2 };
            var previousRT = RenderTexture.active; var asynchronous = ShaderUtil.allowAsyncCompilation;
            var lineMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            var canvasObject = new GameObject("Debug_Labels", typeof(RectTransform), typeof(Canvas)); canvasObject.transform.SetParent(temporary.transform, false);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Texture2D pixels = null;
            try
            {
                ShaderUtil.allowAsyncCompilation = false; camera.targetTexture = texture;
                Canvas.ForceUpdateCanvases();
                Label(canvas.transform, font, new Vector2(size * .5f, size - 37), new Vector2(1720, 48), "OPEN ARCHIVE  |  RESERVED FOOTPRINTS & FOUR NEST ENTRANCES", 29, Color.white);
                Label(canvas.transform, font, new Vector2(size * .5f, size - 72), new Vector2(1680, 36), "NORTH ↑    Green: 14 × 14 m flat reserves    ·    Amber: four 10 m entry ramps    ·    6 m circulation collar checked", 21, new Color(.83f, .94f, .94f));
                foreach (Transform slot in root.Find("FutureStructureSlots"))
                {
                    var p = slot.position + Vector3.up * 18;
                    var corners = new[] { new Vector3(-7,0,-7), new Vector3(-7,0,7), new Vector3(7,0,7), new Vector3(7,0,-7) }.Select(v => p + slot.rotation * v).ToArray();
                    Lines(temporary.transform, lineMaterial, corners, new Color(.25f, 1, .58f), .32f, true);
                    Vector3 screen = camera.WorldToViewportPoint(p);
                    Label(canvas.transform, font, new Vector2(screen.x * size, screen.y * size), new Vector2(180, 68), "T" + slot.name.Substring(slot.name.Length - 2) + "\n14 × 14 m", 23, Color.white);
                }
                foreach (string direction in new[] { "North", "South", "East", "West" })
                {
                    var gate = root.Find("UniversalMonsterNest/Entrance_" + direction);
                    Vector3 center = gate.forward * 18.5f + Vector3.up * 19;
                    Lines(temporary.transform, lineMaterial, new[] { center - gate.right * 5 - gate.forward * 3.5f, center - gate.right * 5 + gate.forward * 3.5f,
                        center + gate.right * 5 + gate.forward * 3.5f, center + gate.right * 5 - gate.forward * 3.5f }, new Color(1, .7f, .13f), .34f, true);
                    Vector3 screen = camera.WorldToViewportPoint(center + gate.forward * 10);
                    Label(canvas.transform, font, new Vector2(screen.x * size, screen.y * size), new Vector2(190, 65), direction.ToUpperInvariant() + "\n10 m", 20, new Color(1, .83f, .32f));
                }
                var spawn = root.Find("PlayerSpawnArea");
                Vector3 spawnPixel = camera.WorldToViewportPoint(spawn.position + Vector3.forward * 6);
                Label(canvas.transform, font, new Vector2(spawnPixel.x * size, spawnPixel.y * size), new Vector2(250, 68), "SOUTH SPAWN\n4 anchors · 4 m spacing", 20, Color.white);
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = texture;
                pixels = new Texture2D(size, size, TextureFormat.RGB24, false); pixels.ReadPixels(new Rect(0,0,size,size), 0, 0); pixels.Apply();
                File.WriteAllBytes(Folder + "/Previews/OpenArchive_Footprints_Debug.png", pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previousRT; ShaderUtil.allowAsyncCompilation = asynchronous;
                Object.DestroyImmediate(temporary); Object.DestroyImmediate(lineMaterial); if (pixels != null) Object.DestroyImmediate(pixels); texture.Release(); Object.DestroyImmediate(texture);
            }
            AssetDatabase.ImportAsset(Folder + "/Previews/OpenArchive_Footprints_Debug.png");
        }

        static void Label(Transform parent, Font font, Vector2 pixel, Vector2 size, string value, int fontSize, Color color)
        {
            var go = new GameObject("Debug_Label", typeof(RectTransform), typeof(Text), typeof(Outline)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = Vector2.zero; rect.sizeDelta = size; rect.anchoredPosition = pixel;
            var text = go.GetComponent<Text>(); text.font = font; text.text = value; text.fontSize = fontSize; text.color = color; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            var outline = go.GetComponent<Outline>(); outline.effectColor = new Color(.01f, .035f, .04f, .95f); outline.effectDistance = new Vector2(1.2f, -1.2f);
        }
        static void Lines(Transform parent, Material template, Vector3[] points, Color color, float width, bool loop)
        {
            var go = new GameObject("Debug_Outline", typeof(LineRenderer)); go.transform.SetParent(parent, false);
            var line = go.GetComponent<LineRenderer>(); line.sharedMaterial = template; line.startColor = line.endColor = color;
            // Supply the line tint through a property block for URP/Unlit.
            var properties = new MaterialPropertyBlock(); properties.SetColor("_BaseColor", color); line.SetPropertyBlock(properties);
            line.widthMultiplier = width; line.loop = loop; line.useWorldSpace = true; line.positionCount = points.Length; line.SetPositions(points);
            line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
        }
    }
}
