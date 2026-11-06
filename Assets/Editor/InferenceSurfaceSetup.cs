#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Scene-only material overrides and a baked path mask. Never edits meshes or physics.</summary>
public static class InferenceSurfaceSetup
{
    public const string Folder = "Assets/Art/InferenceSurfaces";
    const string Archive = "Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE";
    const float Step = 1.5f;
    const int Resolution = 1024;
    [Serializable] public class Route { public string name; public Vector2[] points; }
    [Serializable] public class Report
    {
        public string reference;
        public int groundRenderers, stoneRenderers, changedSlots, pathResolution;
        public bool transformsCollidersAndGameplayPreserved;
        public Vector4 worldRect;
        public Route[] routes;
        public string[] changedRenderers;
    }
    static Bounds bounds;
    static Collider[] surfaces;
    static HashSet<Collider> surfaceSet;
    static bool[] walkable, accessible, paving;
    static float[] elevation;
    static int width, height;
    static float water;
    static readonly Collider[] nearby = new Collider[80];
    static readonly string[] StoneSources = { "/ROCKTEXTURE/", "/Materials/Rock/", "/Mountainset_1/", "/STONES.fbx", "/RUINS5.fbx", "/RUINS6.fbx", "/S1L3/ALTAR.fbx" };

    [MenuItem("Logic Legends/Rules of Inference/Apply Stylized Ground and Stone")]
    public static void ApplyMenu() { Apply(); }
    public static Report Apply()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.name != "RulesOfInference" || scene.isDirty)
            throw new InvalidOperationException("Open the saved RulesOfInference scene in Edit Mode first.");
        Directory.CreateDirectory(Folder);
        if (!File.Exists("Library/InferenceSurfacesBefore.unity")) EditorSceneManager.SaveScene(scene, "Library/InferenceSurfacesBefore.unity", true);
        string before = ImmutableSceneSnapshot(scene);
        var root = GameObject.Find("Rules_Of_Inference");
        var floor = root.transform.Find("FLOOR");
        var ground = floor.GetComponent<MeshRenderer>();
        if (ground == null || ground.GetComponent<MeshCollider>() == null) throw new InvalidOperationException("Original FLOOR renderer and mesh collider are required.");
        bounds = ground.bounds;
        var ocean = root.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Ocean50x50W750H750");
        water = ocean.position.y;
        var altar = floor.Find("ALTAR");
        surfaceSet = new HashSet<Collider> { ground.GetComponent<MeshCollider>() };
        foreach (var collider in altar.GetComponentsInChildren<Collider>(true)) if (collider.enabled && !collider.isTrigger) surfaceSet.Add(collider);
        surfaces = surfaceSet.ToArray(); Physics.SyncTransforms(); BuildGrid();
        var book = GameObject.Find("Book_Statue").transform;
        Vector2 hub = XZ(book.position);
        MarkAccessible(hub);
        var routes = new List<Route>();
        // Reach the existing stair/land approach, never paint a shortcut across the moat.
        Vector2 altarCenter = XZ(altar.GetComponentsInChildren<MeshRenderer>().OrderByDescending(r => r.bounds.size.x).First().bounds.center);
        Vector2 entrance = altarCenter + (hub - altarCenter).normalized * 26;
        routes.Add(FindRoute("Book to altar approach", hub, entrance));
        var challenge = UnityEngine.Object.FindFirstObjectByType<LogicLegends.Inference.InferenceChallenge>();
        for (int i = 0; i < challenge.crystalSpawns.Length; i++)
        {
            Vector2 tower = XZ(challenge.crystalSpawns[i].position);
            routes.Add(FindRoute("Crystal tower " + (i + 1), hub, tower + (altarCenter - tower).normalized * 6));
        }
        BakeMask(routes, hub, entrance);
        var grass = Material("Inference_GrassAndDirt", Shader.Find("LogicLegends/Inference/StylizedMeadowAndPaths"));
        grass.SetTexture("_GrassMap", Texture("Archive_GrassAlbedo"));
        grass.SetTexture("_MacroMap", Texture("Archive_GroundVariation"));
        grass.SetTexture("_DetailAtlas", Texture("Archive_SurfaceDetailAtlas"));
        grass.SetTexture("_PathMask", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Inference_PathMask.png"));
        grass.SetColor("_GrassColor", new Color(.44f, .59f, .27f));
        grass.SetColor("_EarthColor", new Color(.55f, .40f, .25f));
        grass.SetVector("_GrassReference", new Vector4(.073633f, .141184f, .026107f, 1));
        grass.SetVector("_WorldRect", new Vector4(bounds.min.x, bounds.min.z, bounds.size.x, bounds.size.z));
        grass.SetFloat("_TileMeters", 4); grass.SetFloat("_DetailStrength", .3f); grass.SetFloat("_Variation", .45f);
        ground.sharedMaterial = grass; Record(ground);

        var stoneShader = Shader.Find("LogicLegends/OpenArchive/DetailedSurface");
        var stone = Stone("Stone_MeadowGrey", new Color(.59f, .63f, .58f), stoneShader);
        var dark = Stone("Stone_Shadow", new Color(.39f, .43f, .44f), stoneShader);
        var light = Stone("Stone_RuinsLimestone", new Color(.72f, .73f, .66f), stoneShader);
        var mid = Stone("Stone_Altar", new Color(.57f, .59f, .57f), stoneShader);
        var changed = new List<string> { Path(ground.transform) }; int slots = 1, stoneCount = 0;
        foreach (var renderer in floor.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (renderer == ground) continue;
            var materials = renderer.sharedMaterials; bool any = false;
            for (int i = 0; i < materials.Length; i++)
            {
                var original = materials[i]; if (original == null) continue;
                string source = AssetDatabase.GetAssetPath(original);
                if (source.StartsWith(Folder + "/Stone_", StringComparison.Ordinal)) { slots++; any = true; continue; }
                if (!StoneSources.Any(source.Contains)) continue;
                // Preserve the altar's gold trim, blue inlays, black text, and wood slots.
                string name = original.name.ToLowerInvariant();
                bool natural = source.Contains("/Materials/Rock/") || source.Contains("/ROCKTEXTURE/") || source.Contains("/STONES.fbx") || source.Contains("/Mountainset_1/");
                if (!natural && !name.Contains("stone")) continue;
                materials[i] = natural ? stone : name.Contains("ultra") ? dark : name.Contains("mid") ? light : mid;
                slots++; any = true;
            }
            if (!any) continue;
            renderer.sharedMaterials = materials; Record(renderer); stoneCount++; changed.Add(Path(renderer.transform));
        }
        foreach (var material in new[] { grass, stone, dark, light, mid }) { material.enableInstancing = true; EditorUtility.SetDirty(material); }
        bool preserved = before == ImmutableSceneSnapshot(scene);
        if (!preserved) throw new InvalidOperationException("A transform, mesh, collider, water or gameplay component changed.");
        var report = new Report { reference = "Existing Open Archive multiplayer painted grass, earth atlas and broad variation textures", groundRenderers = 1, stoneRenderers = stoneCount,
            changedSlots = slots, pathResolution = Resolution, transformsCollidersAndGameplayPreserved = preserved,
            worldRect = new Vector4(bounds.min.x, bounds.min.z, bounds.size.x, bounds.size.z), routes = routes.ToArray(), changedRenderers = changed.ToArray() };
        File.WriteAllText(Folder + "/InferenceSurfaceRoutes.json", JsonUtility.ToJson(report, true));
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        return report;
    }
    static string Path(Transform t) => AnimationUtility.CalculateTransformPath(t, null);
    static Texture2D Texture(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(Archive + "/Art/Textures/" + name + ".png");
    static Material Material(string name, Shader shader)
    {
        if (shader == null) throw new InvalidOperationException("Surface shader has not imported.");
        string path = Folder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(shader) { name = name }; AssetDatabase.CreateAsset(material, path); }
        return material;
    }
    static Material Stone(string name, Color color, Shader shader)
    {
        var material = Material(name, shader);
        material.SetColor("_BaseColor", color); material.SetTexture("_BaseMap", null);
        material.SetTexture("_DetailAtlas", Texture("Archive_SurfaceDetailAtlas")); material.SetTexture("_MacroMap", Texture("Archive_GroundVariation"));
        material.SetFloat("_SurfaceStyle", 1); material.SetFloat("_VertexColor", 0); material.SetFloat("_TileMeters", 4.5f);
        material.SetFloat("_DetailStrength", .22f); material.SetFloat("_StoneTextureStrength", 0);
        material.SetFloat("_MossAmount", .1f); material.SetFloat("_MossHeight", -133); material.SetFloat("_EmissionStrength", 0);
        return material;
    }
    static void Record(MeshRenderer renderer) { PrefabUtility.RecordPrefabInstancePropertyModifications(renderer); EditorUtility.SetDirty(renderer); }
    // Everything except renderer material assignments is included in the invariant.
    public static string ImmutableSceneSnapshot(Scene scene)
    {
        var parts = new List<string>();
        foreach (var root in scene.GetRootGameObjects())
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            parts.Add(t.GetInstanceID() + ":" + t.gameObject.activeSelf + ":" + EditorJsonUtility.ToJson(t));
            foreach (var c in t.GetComponents<Component>())
                if (c != null && !(c is Transform) && !(c is MeshRenderer)) parts.Add(c.GetInstanceID() + ":" + EditorJsonUtility.ToJson(c));
        }
        return string.Join("\n", parts);
    }
    static Vector2 XZ(Vector3 p) => new Vector2(p.x, p.z);
    static Vector2 Position(int i) => new Vector2(bounds.min.x + (i % width) * Step, bounds.min.z + (i / width) * Step);
    static int Index(Vector2 p) => Mathf.Clamp(Mathf.RoundToInt((p.y - bounds.min.z) / Step), 0, height - 1) * width + Mathf.Clamp(Mathf.RoundToInt((p.x - bounds.min.x) / Step), 0, width - 1);
    static void BuildGrid()
    {
        width = Mathf.CeilToInt(bounds.size.x / Step) + 1; height = Mathf.CeilToInt(bounds.size.z / Step) + 1;
        accessible = null; walkable = new bool[width * height]; paving = new bool[walkable.Length]; elevation = new float[walkable.Length];
        for (int i = 0; i < walkable.Length; i++)
        {
            var p = Position(i); float y = float.NegativeInfinity; Collider support = null;
            var ray = new Ray(new Vector3(p.x, bounds.max.y + 40, p.y), Vector3.down);
            foreach (var collider in surfaces)
            {
                var b = collider.bounds; if (p.x < b.min.x || p.x > b.max.x || p.y < b.min.z || p.y > b.max.z) continue;
                if (collider.Raycast(ray, out var hit, 100) && hit.normal.y > .6f && hit.point.y > y) { y = hit.point.y; support = collider; }
            }
            if (support == null || y < water + .3f) continue;
            elevation[i] = y; paving[i] = support != surfaces[0];
            int count = Physics.OverlapCapsuleNonAlloc(new Vector3(p.x, y + .9f, p.y), new Vector3(p.x, y + 1.8f, p.y), .6f, nearby, ~0, QueryTriggerInteraction.Ignore);
            bool blocked = count == nearby.Length;
            for (int j = 0; j < count; j++) if (!surfaceSet.Contains(nearby[j])) { blocked = true; break; }
            walkable[i] = !blocked;
        }
    }
    static int Nearest(Vector2 p)
    {
        int best = -1; float distance = float.MaxValue;
        for (int i = 0; i < walkable.Length; i++) if (walkable[i] && (accessible == null || accessible[i]))
        { float d = (Position(i) - p).sqrMagnitude; if (d < distance) { distance = d; best = i; } }
        if (best < 0 || distance > 256) throw new InvalidOperationException("No reachable land near " + p);
        return best;
    }
    static IEnumerable<int> Neighbors(int i)
    {
        int x = i % width, z = i / width;
        for (int dz = -1; dz <= 1; dz++) for (int dx = -1; dx <= 1; dx++)
        {
            int nx = x + dx, nz = z + dz;
            if (dx == 0 && dz == 0 || nx < 0 || nx >= width || nz < 0 || nz >= height) continue;
            int next = nz * width + nx;
            if (!walkable[next] || Mathf.Abs(elevation[i] - elevation[next]) > 1.3f) continue;
            if (dx != 0 && dz != 0 && (!walkable[z * width + nx] || !walkable[nz * width + x])) continue;
            yield return next;
        }
    }
    static void MarkAccessible(Vector2 hub)
    {
        int first = Nearest(hub); accessible = new bool[walkable.Length]; accessible[first] = true;
        var queue = new Queue<int>(); queue.Enqueue(first);
        while (queue.Count > 0) foreach (int next in Neighbors(queue.Dequeue())) if (!accessible[next]) { accessible[next] = true; queue.Enqueue(next); }
    }
    static Route FindRoute(string name, Vector2 start, Vector2 end)
    {
        int from = Nearest(start), to = Nearest(end);
        var score = Enumerable.Repeat(float.PositiveInfinity, walkable.Length).ToArray();
        var previous = Enumerable.Repeat(-1, walkable.Length).ToArray(); var closed = new bool[walkable.Length];
        var queue = new SortedSet<KeyValuePair<float, int>>(Comparer<KeyValuePair<float, int>>.Create((a, b) => a.Key == b.Key ? a.Value.CompareTo(b.Value) : a.Key.CompareTo(b.Key)));
        score[from] = 0; queue.Add(new KeyValuePair<float, int>(0, from));
        while (queue.Count > 0)
        {
            var pair = queue.Min; queue.Remove(pair); int at = pair.Value; if (closed[at]) continue; closed[at] = true; if (at == to) break;
            foreach (int next in Neighbors(at))
            {
                float cost = score[at] + Vector2.Distance(Position(at), Position(next)) * (paving[next] ? .9f : 1) + Mathf.Abs(elevation[next] - elevation[at]) * .4f;
                if (closed[next] || cost >= score[next]) continue;
                score[next] = cost; previous[next] = at; queue.Add(new KeyValuePair<float, int>(cost + Vector2.Distance(Position(next), Position(to)) * .9f, next));
            }
        }
        if (!closed[to]) throw new InvalidOperationException("Unable to route " + name);
        var raw = new List<Vector2>(); for (int at = to; at >= 0; at = previous[at]) { raw.Add(Position(at)); if (at == from) break; } raw.Reverse();
        var points = new List<Vector2> { raw[0] }; int a = 0;
        while (a < raw.Count - 1) { int b = Mathf.Min(raw.Count - 1, a + 12); while (b > a + 1 && !LineClear(raw[a], raw[b])) b--; points.Add(raw[b]); a = b; }
        return new Route { name = name, points = points.ToArray() };
    }
    static bool LineClear(Vector2 a, Vector2 b)
    {
        int samples = Mathf.CeilToInt(Vector2.Distance(a, b) / (.4f * Step)), previous = Index(a);
        for (int s = 1; s <= samples; s++) { int i = Index(Vector2.Lerp(a, b, (float)s / samples)); if (!walkable[i] || Mathf.Abs(elevation[i] - elevation[previous]) > 1.3f) return false; previous = i; }
        return true;
    }
    static void BakeMask(List<Route> routes, Vector2 hub, Vector2 entrance)
    {
        var mask = new float[Resolution * Resolution]; int routeIndex = 0;
        foreach (var route in routes)
        {
            float length = 0;
            for (int i = 1; i < route.points.Length; i++)
            {
                var a = route.points[i - 1]; var b = route.points[i]; float distance = Vector2.Distance(a, b), offset = length; length += distance;
                Paint(mask, Vector2.Min(a, b) - Vector2.one * 5, Vector2.Max(a, b) + Vector2.one * 5, p =>
                {
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / Mathf.Max(.001f, (b - a).sqrMagnitude));
                    float radius = 2.05f * (1 + .18f * Mathf.Sin((offset + t * distance) * .13f + routeIndex));
                    float edge = (Mathf.PerlinNoise(p.x * .18f, p.y * .18f) - .5f) * 1.1f + (Mathf.PerlinNoise(p.x * .63f, p.y * .63f) - .5f) * .2f;
                    float d = Vector2.Distance(p, Vector2.Lerp(a, b, t)) + edge;
                    return 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(radius * .6f, radius + .85f, d));
                });
            }
            Ellipse(mask, route.points.Last(), new Vector2(3.3f, 2.6f), .8f); routeIndex++;
        }
        Ellipse(mask, hub, new Vector2(4.8f, 4), 1); Ellipse(mask, entrance, new Vector2(4, 3), 1);
        var random = new System.Random(310027); var candidates = Enumerable.Range(0, walkable.Length).Where(i => walkable[i] && accessible[i] && !paving[i]).ToArray();
        for (int i = 0; i < 34; i++) Ellipse(mask, Position(candidates[random.Next(candidates.Length)]), new Vector2(1.4f + (float)random.NextDouble() * 2.8f, 1.1f + (float)random.NextDouble() * 2), .4f + (float)random.NextDouble() * .45f);
        var pixels = new Color32[mask.Length]; for (int i = 0; i < mask.Length; i++) { byte v = (byte)Mathf.RoundToInt(Mathf.Clamp01(mask[i]) * 255); pixels[i] = new Color32(v, v, v, 255); }
        var texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false, true); texture.SetPixels32(pixels); texture.Apply();
        string path = Folder + "/Inference_PathMask.png"; File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.sRGBTexture = false; importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = true; importer.filterMode = FilterMode.Bilinear; importer.anisoLevel = 2; importer.maxTextureSize = Resolution;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
    }
    static void Ellipse(float[] mask, Vector2 center, Vector2 radius, float opacity)
    { Paint(mask, center - radius * 1.4f, center + radius * 1.4f, p => { var d = p - center; float r = new Vector2(d.x / radius.x, d.y / radius.y).magnitude + (Mathf.PerlinNoise(p.x * .27f, p.y * .27f) - .5f) * .3f; return (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.5f, 1.2f, r))) * opacity; }); }
    static void Paint(float[] mask, Vector2 min, Vector2 max, Func<Vector2, float> sample)
    {
        int x0 = Mathf.Clamp(Mathf.FloorToInt((min.x - bounds.min.x) / bounds.size.x * (Resolution - 1)), 0, Resolution - 1), x1 = Mathf.Clamp(Mathf.CeilToInt((max.x - bounds.min.x) / bounds.size.x * (Resolution - 1)), 0, Resolution - 1);
        int z0 = Mathf.Clamp(Mathf.FloorToInt((min.y - bounds.min.z) / bounds.size.z * (Resolution - 1)), 0, Resolution - 1), z1 = Mathf.Clamp(Mathf.CeilToInt((max.y - bounds.min.z) / bounds.size.z * (Resolution - 1)), 0, Resolution - 1);
        for (int z = z0; z <= z1; z++) for (int x = x0; x <= x1; x++) { var p = new Vector2(bounds.min.x + (float)x / (Resolution - 1) * bounds.size.x, bounds.min.z + (float)z / (Resolution - 1) * bounds.size.z); int i = z * Resolution + x; mask[i] = Mathf.Max(mask[i], sample(p)); }
    }
}
#endif
