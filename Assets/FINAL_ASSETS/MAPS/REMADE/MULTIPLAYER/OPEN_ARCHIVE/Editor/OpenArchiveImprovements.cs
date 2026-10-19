using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace OpenArchiveAuthoring
{
    /// <summary>Refines the saved map in place. Never calls the original scene generator.</summary>
    public static class OpenArchiveImprovements
    {
        public const string Folder = OpenArchiveBuilder.Folder;
        static Transform root;
        static Vector3[] slots;
        static Material atlas, meadow;
        static readonly List<Vector3[]> shoreline = new List<Vector3[]>();
        static System.Random random;
        static float Range(float a, float b) => a + (float)random.NextDouble() * (b - a);
        static Vector3 Polar(float degrees, float radius, float y = 0) => Quaternion.Euler(0, degrees, 0) * new Vector3(0, y, radius);

        [MenuItem("Tools/Logic Legends/Open Archive/Improve Existing Environment In Place")]
        public static void Apply()
        {
            RequireSavedEditor();
            var scene = SceneManager.GetActiveScene();
            if (scene.path != OpenArchiveBuilder.ScenePath)
                scene = EditorSceneManager.OpenScene(OpenArchiveBuilder.ScenePath, OpenSceneMode.Single);
            root = scene.GetRootGameObjects().Single(g => g.name == "OpenArchive").transform;
            string baseline = Folder + "/Editor/Authoring/OriginalSceneObjectIds.txt";
            var originalIDs = File.Exists(baseline) ? File.ReadAllLines(baseline) : root.GetComponentsInChildren<Transform>(true).Select(t => GlobalObjectId.GetGlobalObjectIdSlow(t).ToString()).ToArray();
            string guidBaseline = Folder + "/Editor/Authoring/OriginalAssetGuids.txt";
            var guids = File.Exists(guidBaseline) ? File.ReadAllLines(guidBaseline).Select(line => line.Split('|')).ToDictionary(p => p[1], p => p[0])
                : AssetDatabase.FindAssets("", new[] { Folder }).ToDictionary(g => AssetDatabase.GUIDToAssetPath(g), g => g);
            slots = root.Find("FutureStructureSlots").Cast<Transform>().OrderBy(t => t.name).Select(t => t.position).ToArray();
            if (slots.Length != 10 || root.Find("UniversalMonsterNest").Cast<Transform>().Count(t => t.name.StartsWith("Entrance_")) != 4)
                throw new InvalidOperationException("Expected the existing ten slots and four cardinal nest entrances.");
            atlas = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/Archive_Atlas.mat");
            var shader = Shader.Find("LogicLegends/OpenArchive/Meadow");
            if (shader == null || Shader.Find("LogicLegends/OpenArchive/TurquoiseWater") == null)
                throw new InvalidOperationException("Import the two Open Archive surface shaders first.");
            meadow = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/Archive_Meadow.mat");
            if (meadow == null) { meadow = new Material(shader); AssetDatabase.CreateAsset(meadow, Folder + "/Materials/Archive_Meadow.mat"); }
            meadow.shader = shader; meadow.SetColor("_BaseColor", Color.white); meadow.enableInstancing = true;
            var water = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/Archive_TurquoiseWater.mat");
            water.shader = Shader.Find("LogicLegends/OpenArchive/TurquoiseWater"); water.enableInstancing = true;
            water.SetColor("_DeepColor", new Color(.025f, .49f, .64f));
            water.SetColor("_ShoreColor", new Color(.18f, .72f, .70f));
            water.SetColor("_RippleColor", new Color(.48f, .89f, .86f)); water.SetFloat("_WaveHeight", .018f);
            EditorUtility.SetDirty(water); EditorUtility.SetDirty(meadow);

            // The terrain object, its mesh GUID, outer island vertices, paths, cliffs, and bridges survive.
            RefineShoreline();
            RefineClearings();
            RefinePathSurfaces();
            RefineBankScenery();
            SealNestWallGaps();
            RefineScenery();
            LimitEnvironmentShadows();
            RefreshTerrainCollider();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the improved source scene.");
            AssetDatabase.SaveAssets();
            var afterIDs = new HashSet<string>(root.GetComponentsInChildren<Transform>(true).Select(t => GlobalObjectId.GetGlobalObjectIdSlow(t).ToString()));
            var preservation = new Preservation
            {
                scene = scene.path, originalObjects = originalIDs.Length, retainedObjects = originalIDs.Count(afterIDs.Contains),
                finalObjects = afterIDs.Count, originalAssetGUIDs = guids.Count,
                preservedAssetGUIDs = guids.Count(p => AssetDatabase.AssetPathToGUID(p.Key) == p.Value),
                notes = "Updated existing mesh channels and materials; retained every original GameObject and asset GUID. No scene generator, map replacement, or gameplay changes."
            };
            preservation.passed = preservation.originalObjects == preservation.retainedObjects && preservation.originalAssetGUIDs == preservation.preservedAssetGUIDs;
            File.WriteAllText(Folder + "/Previews/InPlacePreservation.json", JsonUtility.ToJson(preservation, true));
            AssetDatabase.ImportAsset(Folder + "/Previews/InPlacePreservation.json");
            Debug.Log("Open Archive refined in place. Original objects retained: " + preservation.retainedObjects + "/" + preservation.originalObjects);
        }

        public static void RequireSavedEditor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scene edits before refining or reviewing the map.");
        }

        [Serializable] class Preservation
        {
            public bool passed;
            public string scene, notes;
            public int originalObjects, retainedObjects, finalObjects, originalAssetGUIDs, preservedAssetGUIDs;
        }

        // Negative inside water. Smooth caps retain broad dry cardinal connections instead of square ends.
        internal static float WaterField(Vector3 p)
        {
            float a = Mathf.Atan2(p.z, p.x), r = new Vector2(p.x, p.z).magnitude;
            float inner = 29.3f + .9f * Mathf.Sin(a * 5) + .4f * Mathf.Sin(a * 11 + 1);
            float outer = 41.5f + 1.3f * Mathf.Sin(a * 7 + 1) + .45f * Mathf.Cos(a * 13);
            float radial = Mathf.Max(inner - r, r - outer);
            float ends = Mathf.Max(8.8f + .5f * Mathf.Sin(p.z * .19f) - Mathf.Abs(p.x),
                8.8f + .5f * Mathf.Sin(p.x * .17f) - Mathf.Abs(p.z));
            float h = Mathf.Max(2 - Mathf.Abs(radial - ends), 0) / 2;
            return Mathf.Max(radial, ends) + h * h * .5f;
        }

        static Color MeadowColor(Vector3 p)
        {
            float n = Mathf.PerlinNoise(p.x * .052f + 27, p.z * .052f + 31);
            float fine = Mathf.PerlinNoise(p.x * .23f + 67, p.z * .23f + 9);
            var c = Color.Lerp(new Color(.34f, .52f, .235f), new Color(.48f, .60f, .29f), Mathf.SmoothStep(0, 1, n));
            c *= .965f + fine * .07f; c.a = 1;
            float clearing = 0;
            foreach (var slot in slots)
            {
                var d = p - slot; float angle = Mathf.Atan2(d.z, d.x);
                // Broad, irregular meadow clearings; the protected square is defined by coordinates, not this color edge.
                float reach = 12.2f + 1.0f * Mathf.Sin(angle * 3 + slot.x) + .7f * Mathf.Sin(angle * 5 + slot.z);
                float radius = new Vector2(d.x * .97f, d.z * 1.03f).magnitude;
                clearing = Mathf.Max(clearing, 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(8.0f, reach + 2.6f, radius)));
            }
            var dry = new Color(.62f + fine * .025f, .64f + fine * .025f, .365f + fine * .02f);
            return Color.Lerp(c, dry, clearing * .83f).linear;
        }

        static void RefineShoreline()
        {
            var groundFilter = root.Find("Environment/Terrain/Island_Grassland_WithRiverHoles").GetComponent<MeshFilter>();
            var ground = new Surface();
            var old = groundFilter.sharedMesh;
            var v = old.vertices; var tris = old.triangles;
            // Keep all original triangles outside the local water editing square, including the circular rim.
            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 a = v[tris[i]], b = v[tris[i + 1]], c = v[tris[i + 2]], mid = (a + b + c) / 3;
                if (Mathf.Abs(mid.x) >= 46 || Mathf.Abs(mid.z) >= 46)
                    ground.Tri(a, b, c, 22, MeadowColor);
            }
            var water = new[] { new Surface(), new Surface(), new Surface(), new Surface() };
            shoreline.Clear();
            for (int x = -46; x < 46; x += 2) for (int z = -46; z < 46; z += 2)
            {
                Vector3 a = new Vector3(x, 0, z), b = a + Vector3.forward * 2, c = b + Vector3.right * 2, d = a + Vector3.right * 2;
                CutTriangle(a, b, c, ground, water); CutTriangle(a, c, d, ground, water);
            }
            UpdateSurface(groundFilter, ground, meadow);
            for (int i = 0; i < 4; i++)
            {
                var filter = root.GetComponentsInChildren<MeshFilter>().Single(f => f.name == "Turquoise_RiverPond_" + (i + 1));
                UpdateSurface(filter, water[i], null);
                filter.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            var banks = new Surface();
            foreach (var edge in shoreline)
            {
                Vector3 a = edge[0], b = edge[1];
                banks.Quad(a, a + Vector3.down * 2.2f, b + Vector3.down * 2.2f, b, 4);
                banks.Quad(b, b + Vector3.down * 2.2f, a + Vector3.down * 2.2f, a, 4);
            }
            UpdateSurface(root.Find("Environment/Rocks/Riverbank_CutStone").GetComponent<MeshFilter>(), banks, atlas);
        }

        static void CutTriangle(Vector3 a, Vector3 b, Vector3 c, Surface ground, Surface[] water)
        {
            var input = new[] { a, b, c };
            var dry = Clip(input, true); var wet = Clip(input, false);
            for (int i = 1; i < dry.Count - 1; i++) ground.Tri(dry[0], dry[i], dry[i + 1], 22, MeadowColor);
            if (wet.Count < 3) return;
            var center = (a + b + c) / 3;
            int q = (center.x < 0 ? 2 : 0) + (center.z < 0 ? 1 : 0);
            Func<Vector3, Color> depth = p => new Color(Mathf.Clamp01(-WaterField(p) / 3.2f), 0, 0, 1);
            for (int i = 1; i < wet.Count - 1; i++)
                water[q].Tri(wet[0] + Vector3.down * 1.15f, wet[i] + Vector3.down * 1.15f, wet[i + 1] + Vector3.down * 1.15f, 45, depth);
            var crossings = new List<Vector3>();
            for (int i = 0; i < 3; i++)
            {
                var p = input[i]; var n = input[(i + 1) % 3];
                float fp = WaterField(p), fn = WaterField(n);
                if ((fp < 0) != (fn < 0)) crossings.Add(Vector3.Lerp(p, n, fp / (fp - fn)));
            }
            if (crossings.Count == 2 && Vector3.Distance(crossings[0], crossings[1]) > .001f) shoreline.Add(crossings.ToArray());
        }

        static List<Vector3> Clip(Vector3[] polygon, bool keepDry)
        {
            var output = new List<Vector3>();
            for (int i = 0; i < polygon.Length; i++)
            {
                Vector3 p = polygon[i], n = polygon[(i + 1) % polygon.Length];
                float a = WaterField(p), b = WaterField(n);
                bool inside = keepDry ? a >= 0 : a <= 0;
                bool next = keepDry ? b >= 0 : b <= 0;
                if (inside) output.Add(p);
                if (inside != next) output.Add(Vector3.Lerp(p, n, a / (a - b)));
            }
            return output;
        }

        static void RefineClearings()
        {
            for (int index = 0; index < slots.Length; index++)
            {
                var clearing = new Surface(); var p = slots[index];
                const int segments = 48;
                for (int ring = 0; ring < 5; ring++) for (int i = 0; i < segments; i++)
                {
                    Func<int, float, Vector3> point = (n, f) =>
                    {
                        float a = n * 360f / segments;
                        float boundary = 15.2f + .65f * Mathf.Sin(a * Mathf.Deg2Rad * 3 + index) + .4f * Mathf.Cos(a * Mathf.Deg2Rad * 5);
                        return p + Polar(a, boundary * f, .012f);
                    };
                    float inner = ring / 5f, outer = (ring + 1) / 5f;
                    clearing.Quad(point(i, inner), point(i, outer), point(i + 1, outer), point(i + 1, inner), 22, MeadowColor);
                }
                var filter = root.Find("Environment/SecondaryPaths/Empty_Ground_" + (index + 1).ToString("00")).GetComponent<MeshFilter>();
                UpdateSurface(filter, clearing, meadow);
                filter.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        [MenuItem("Tools/Logic Legends/Open Archive/Soften Existing Path Finishes")]
        public static void SoftenPathFinishes()
        {
            RequireSavedEditor();
            var scene = SceneManager.GetActiveScene();
            root = scene.GetRootGameObjects().Single(g => g.name == "OpenArchive").transform;
            slots = root.Find("FutureStructureSlots").Cast<Transform>().OrderBy(t => t.name).Select(t => t.position).ToArray();
            atlas = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/Archive_Atlas.mat");
            meadow = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/Archive_Meadow.mat");
            RefinePathSurfaces(); EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save path surface refinements.");
            AssetDatabase.SaveAssets();
        }

        static void RefinePathSurfaces()
        {
            var paths = new Surface();
            var sand = new Color(.77f, .71f, .49f).linear;
            float[] rings = { 21.5f, 22.1f, 26.7f, 27.3f };
            for (int band = 0; band < rings.Length - 1; band++) for (int i = 0; i < 128; i++)
            {
                float a = i * 360f / 128, b = (i + 1) * 360f / 128;
                Func<Vector3, Color> tint = p =>
                {
                    float r = new Vector2(p.x, p.z).magnitude;
                    float edge = Mathf.Min(r - 21.5f, 27.3f - r);
                    return Color.Lerp(MeadowColor(p), sand, Mathf.Clamp01(edge / .55f));
                };
                paths.Quad(Polar(a, rings[band], .035f), Polar(a, rings[band + 1], .035f), Polar(b, rings[band + 1], .035f), Polar(b, rings[band], .035f), 46, tint);
            }
            foreach (var slot in slots)
            {
                Vector3 d = slot.normalized, side = Vector3.Cross(Vector3.up, d);
                // The full six metre approach stays flat; its final six metres dissolve into the clearing.
                for (int along = 0; along < 9; along++) for (int lane = 0; lane < 6; lane++)
                {
                    float r0 = 56 + along * 2, r1 = r0 + 2;
                    float x0 = -3 + lane, x1 = x0 + 1;
                    Func<Vector3, Color> tint = p =>
                    {
                        float r = Vector3.Dot(p, d), x = Mathf.Abs(Vector3.Dot(p, side));
                        float end = Mathf.Clamp01((74 - r) / 6), edge = Mathf.Clamp01((3 - x) / .55f);
                        return Color.Lerp(MeadowColor(p), sand, end * edge);
                    };
                    paths.Quad(d * r0 + side * x0 + Vector3.up * .045f, d * r1 + side * x0 + Vector3.up * .045f,
                        d * r1 + side * x1 + Vector3.up * .045f, d * r0 + side * x1 + Vector3.up * .045f, 46, tint);
                }
            }
            UpdateSurface(root.Find("Environment/SecondaryPaths/Secondary_Loops_And_SlotApproaches_6m").GetComponent<MeshFilter>(), paths, meadow);
            var paving = new Surface(); random = new System.Random(471997);
            // Retain the ten metre main ring; weather only its decorative paving layer.
            for (int i = 0; i < 148; i++) for (int lane = 0; lane < 3; lane++)
            {
                if (random.NextDouble() < .24f) continue;
                float angle = (i + (lane % 2) * .5f) * 360f / 148;
                Vector3 center = Polar(angle, 50.8f + lane * 3.15f, .055f);
                var rotation = Quaternion.Euler(0, angle + 90 + Range(-4, 4), 0);
                float width = Range(1.45f, 1.95f), depth = Range(2.35f, 2.75f);
                var points = new[] { new Vector3(-.4f,0,-.5f), new Vector3(-.5f,0,-.3f), new Vector3(-.5f,0,.35f), new Vector3(-.3f,0,.5f),
                    new Vector3(.4f,0,.5f), new Vector3(.5f,0,.25f), new Vector3(.5f,0,-.35f), new Vector3(.3f,0,-.5f) };
                for (int n = 0; n < points.Length; n++)
                {
                    Vector3 a = points[n], b = points[(n + 1) % points.Length]; a.x *= width; a.z *= depth; b.x *= width; b.z *= depth;
                    paving.Tri(center, center + rotation * a, center + rotation * b, i % 5 == 0 ? 6 : 47);
                }
            }
            UpdateSurface(root.Find("Environment/MainPaths/MainRing_StonePaving").GetComponent<MeshFilter>(), paving, atlas);
        }

        static Vector3 LandNormal(Vector3 p)
        {
            var n = new Vector3(WaterField(p + Vector3.right * .1f) - WaterField(p - Vector3.right * .1f), 0,
                WaterField(p + Vector3.forward * .1f) - WaterField(p - Vector3.forward * .1f));
            return n.normalized;
        }

        static void RefineBankScenery()
        {
            random = new System.Random(196104);
            var rocks = new Surface(); var plants = Enumerable.Range(0, 8).Select(_ => new Surface()).ToArray();
            var rock = KitMesh("Rock_A"); var grass = KitMesh("Grass_Clump"); var bush = KitMesh("Bush_B"); var flower = KitMesh("Flower_Cluster");
            int flowers = 0;
            for (int i = 0; i < shoreline.Count; i++)
            {
                Vector3 p = (shoreline[i][0] + shoreline[i][1]) * .5f;
                if (Vector3.Distance(shoreline[i][0], shoreline[i][1]) < .35f || Protected(p, .6f)) continue;
                Vector3 normal = LandNormal(p);
                float patch = Mathf.PerlinNoise(p.x * .19f + 7, p.z * .19f + 31);
                if (i % 3 == 0 && patch > .37f)
                {
                    Vector3 size = new Vector3(Range(.65f, 1.05f), Range(.28f, .52f), Range(.6f, .95f));
                    rocks.Append(rock, Matrix4x4.TRS(p + normal * Range(-.3f, .2f) - Vector3.up * .12f, Quaternion.Euler(0, Range(0, 360), 0), size));
                }
                Vector3 plant = p + normal * Range(.8f, 1.7f);
                if (i % 4 != 0 || patch < .43f || Protected(plant, .65f) || WaterField(plant) < .4f) continue;
                int sector = Sector(plant);
                plants[sector].Append(grass, Matrix4x4.TRS(plant, Quaternion.Euler(0, Range(0, 360), 0), new Vector3(.8f, 1.6f, .8f)));
                if (i % 12 == 0) plants[sector].Append(bush, Matrix4x4.TRS(plant + normal * .4f, Quaternion.Euler(0, Range(0, 360), 0), Vector3.one * .45f));
                if (i % 17 == 0 && flowers++ < 10) plants[sector].Append(flower, Matrix4x4.TRS(plant + normal * .8f, Quaternion.identity, Vector3.one * .55f));
            }
            UpdateSurface(root.Find("Environment/Rocks/Riverbank_RockClusters").GetComponent<MeshFilter>(), rocks, atlas);
            for (int i = 0; i < plants.Length; i++) PutSurface("Natural_Bank_Plants_" + i, root.Find("Environment/Trees"), plants[i], atlas, false);

            var lilies = new Surface();
            for (int i = 0; i < 55; i++)
            {
                var p = Polar(Range(0, 360), Range(30, 41), -1.10f);
                if (WaterField(p) > -1 || ProtectedBridge(p, .8f)) continue;
                for (int n = 0; n < 7; n++) lilies.Tri(p, p + Polar(n * 360f / 7, .32f), p + Polar((n + 1) * 360f / 7, .32f), 22);
            }
            UpdateSurface(root.Find("Environment/Ponds/Occasional_LilyPads").GetComponent<MeshFilter>(), lilies, atlas);
        }

        static void SealNestWallGaps()
        {
            // Low, continuous stone plinth behind the existing broken walls. Only the four gate sectors remain open.
            var g = new Surface();
            for (int quadrant = 0; quadrant < 4; quadrant++) for (int i = 0; i < 16; i++)
            {
                float a = quadrant * 90 + 22.3f + i * 45.4f / 16, b = quadrant * 90 + 22.3f + (i + 1) * 45.4f / 16;
                Vector3 ia = Polar(a, 14.05f), ib = Polar(b, 14.05f), oa = Polar(a, 15.15f), ob = Polar(b, 15.15f);
                Vector3 up = Vector3.up * 1.85f;
                g.Quad(ia + up, oa + up, ob + up, ib + up, i % 4 == 0 ? 6 : 0);
                g.Quad(ia, ia + up, ib + up, ib, 4);
                g.Quad(ob, ob + up, oa + up, oa, i % 3 == 0 ? 2 : 0);
                if (i == 0) g.Quad(oa, oa + up, ia + up, ia, 0);
                if (i == 15) g.Quad(ib, ib + up, ob + up, ob, 0);
            }
            PutSurface("Nest_Continuous_Quarter_Wall_Plinths", root.Find("UniversalMonsterNest"), g, atlas, true);
        }

        static void RefineScenery()
        {
            var parent = root.Find("Environment/Trees");
            bool alreadyDone = parent.Find("Meadow_Edge_Vegetation_0") != null;
            random = new System.Random(76101);
            if (!alreadyDone)
            {
                foreach (var tree in parent.Cast<Transform>().Where(t => t.GetComponent<LODGroup>() != null))
                {
                    Vector3 old = tree.position, candidate;
                    float radius = new Vector2(old.x, old.z).magnitude;
                    if (radius > 94)
                    {
                        float a = Mathf.Atan2(old.x, old.z) * Mathf.Rad2Deg + Range(-1.4f, 1.4f);
                        candidate = Polar(a, Range(95.6f, 98.15f));
                    }
                    else candidate = old + new Vector3(Range(-2.1f, 2.1f), 0, Range(-2.1f, 2.1f));
                    if (!Protected(candidate, 1.4f) && WaterField(candidate) > 2) tree.position = candidate;
                    float s = Mathf.Clamp(tree.localScale.x * Range(.84f, 1.16f), .95f, 2.05f);
                    tree.localScale = new Vector3(s * Range(.94f, 1.04f), s, s * Range(.94f, 1.04f));
                    tree.rotation = Quaternion.Euler(0, Range(0, 360), 0);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(tree);
                }
                // Keep existing rocks dry after the shoreline adjustment without introducing new blockers.
                foreach (var rock in root.Find("Environment/Rocks").Cast<Transform>().Where(t => t.GetComponent<Collider>() != null))
                {
                    if (WaterField(rock.position) >= .6f) continue;
                    Vector3 candidate = rock.position + LandNormal(rock.position) * 3;
                    if (!Protected(candidate, 1.2f) && WaterField(candidate) > 1) { rock.position = candidate; PrefabUtility.RecordPrefabInstancePropertyModifications(rock); }
                }
            }
            var vegetation = Enumerable.Range(0, 8).Select(_ => new Surface()).ToArray();
            for (int i = 0; i < 500; i++)
            {
                Vector3 p = Polar(Range(0, 360), Range(60, 97));
                if (Protected(p, .55f) || WaterField(p) < 1 || Mathf.PerlinNoise(p.x * .12f + 21, p.z * .12f + 51) < .5f) continue;
                string mesh = i % 31 == 0 ? "Flower_Cluster" : i % 4 == 0 ? "Bush_A" : "Grass_Clump";
                vegetation[Sector(p)].Append(KitMesh(mesh), Matrix4x4.TRS(p, Quaternion.Euler(0, Range(0, 360), 0), Vector3.one * Range(.45f, .9f)));
            }
            for (int i = 0; i < vegetation.Length; i++) PutSurface("Meadow_Edge_Vegetation_" + i, parent, vegetation[i], atlas, false);
            if (!alreadyDone)
            {
                int placed = 0;
                for (int i = 0; i < 80 && placed < 8; i++)
                {
                    Vector3 p = Polar(i * 137.508f, Range(62, 84));
                    if (Protected(p, 2.4f)) continue;
                    string name = placed % 3 == 0 ? "Pillar_Broken" : "RuinedWall_Short";
                    string path = Folder + "/Prefabs/Architecture/" + name + ".prefab";
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null) continue;
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    go.name = "Reclaimed_Ruin_Fragment_" + placed.ToString("00");
                    go.transform.SetParent(root.Find("Environment/Ruins"), false); go.transform.position = p;
                    go.transform.rotation = Quaternion.Euler(0, Range(0, 360), 0); go.transform.localScale = Vector3.one * Range(.42f, .67f);
                    placed++;
                }
            }
        }

        static int Sector(Vector3 p) => Mathf.Clamp((int)((Mathf.Atan2(p.x, p.z) + Mathf.PI) / (Mathf.PI * 2) * 8), 0, 7);
        static Mesh KitMesh(string name)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/Meshes/" + name + ".asset");
            if (mesh == null) throw new InvalidOperationException("Missing original kit mesh " + name);
            return mesh;
        }

        internal static bool Protected(Vector3 p, float margin)
        {
            float r = new Vector2(p.x, p.z).magnitude;
            if (r < 28 + margin || r > 85 - margin && r < 94 + margin || r > 99) return true;
            if (r > 48 - margin && r < 60 + margin) return true;
            if (Mathf.Abs(p.x) < 6 + margin || Mathf.Abs(p.z) < 6 + margin) return true;
            if (Vector2.Distance(new Vector2(p.x, p.z), new Vector2(0, -83)) < 11 + margin) return true;
            foreach (var slot in slots)
            {
                // 14m footprint plus 6m on each side is kept free of newly authored scenery.
                if (Mathf.Abs(p.x - slot.x) < 13 + margin && Mathf.Abs(p.z - slot.z) < 13 + margin) return true;
                Vector3 a = slot.normalized * 54, b = slot, d = b - a;
                float t = Mathf.Clamp01(Vector3.Dot(p - a, d) / d.sqrMagnitude);
                if (Vector3.Distance(p, a + d * t) < 3.5f + margin) return true;
            }
            return ProtectedBridge(p, margin);
        }

        static bool ProtectedBridge(Vector3 p, float margin)
        {
            for (int i = 0; i < 4; i++)
            {
                Vector3 d = Polar(45 + 90 * i, 1), side = new Vector3(d.z, 0, -d.x);
                float along = Vector3.Dot(p, d), lateral = Mathf.Abs(Vector3.Dot(p, side));
                if (along > 20 - margin && along < 51 + margin && lateral < 5.5f + margin) return true;
            }
            return false;
        }

        static void RefreshTerrainCollider()
        {
            var filter = root.Find("Environment/Terrain/Island_Grassland_WithRiverHoles").GetComponent<MeshFilter>();
            var c = filter.GetComponent<MeshCollider>(); c.sharedMesh = null; c.sharedMesh = filter.sharedMesh;
            Physics.SyncTransforms();
        }

        [MenuItem("Tools/Logic Legends/Open Archive/Limit Environment Shadow Cost")]
        public static void LimitEnvironmentShadows()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            if (root == null || !root.gameObject.scene.isLoaded)
                root = SceneManager.GetActiveScene().GetRootGameObjects().Single(g => g.name == "OpenArchive").transform;
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                string path = AnimationUtility.CalculateTransformPath(renderer.transform, root);
                if (path.StartsWith("Environment/Cliffs/") || path.StartsWith("Environment/Rocks/") || path.StartsWith("Environment/MainPaths/")
                    || path.StartsWith("Environment/SecondaryPaths/") || path.StartsWith("Environment/Rivers/") || path.StartsWith("Environment/Ponds/")
                    || path.StartsWith("Environment/Waterfalls/") || path.StartsWith("Environment/Terrain/")
                    || path.StartsWith("Environment/Trees/") && renderer.GetComponentInParent<LODGroup>() == null)
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            foreach (var lod in root.GetComponentsInChildren<LODGroup>(true))
            {
                var levels = lod.GetLODs();
                for (int i = 0; i < levels.Length; i++) foreach (var renderer in levels[i].renderers)
                    if (renderer != null) renderer.shadowCastingMode = i == 0 ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
            var sun = root.Find("Lighting/Warm_Daylight").GetComponent<Light>();
            sun.shadows = LightShadows.Hard; sun.shadowStrength = .55f;
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        }

        static void UpdateSurface(MeshFilter filter, Surface data, Material material)
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null || !AssetDatabase.GetAssetPath(mesh).StartsWith(Folder + "/Meshes/", StringComparison.Ordinal))
                throw new InvalidOperationException("Refinement may only update owned Open Archive mesh assets.");
            var colliders = filter.GetComponents<MeshCollider>();
            foreach (var c in colliders) c.sharedMesh = null;
            data.Write(mesh); EditorUtility.SetDirty(mesh);
            foreach (var c in colliders) c.sharedMesh = mesh;
            if (material != null) filter.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        static GameObject PutSurface(string name, Transform parent, Surface data, Material material, bool collision)
        {
            var t = parent.Find(name); GameObject go;
            if (t == null)
            {
                go = new GameObject(name); go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>(); go.AddComponent<MeshRenderer>();
            }
            else go = t.gameObject;
            var filter = go.GetComponent<MeshFilter>();
            string path = Folder + "/Meshes/" + name + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { mesh = new Mesh { name = name }; AssetDatabase.CreateAsset(mesh, path); }
            filter.sharedMesh = mesh; UpdateSurface(filter, data, material);
            var renderer = go.GetComponent<MeshRenderer>(); renderer.shadowCastingMode = collision ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            if (collision)
            {
                var c = go.GetComponent<MeshCollider>(); if (c == null) c = go.AddComponent<MeshCollider>(); c.sharedMesh = mesh;
            }
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            return go;
        }

        /// <summary>Sync only changed environment properties and new scenery into the existing exploration copy.</summary>
        [MenuItem("Tools/Logic Legends/Open Archive/Sync Improvements To Existing Exploration Scene")]
        public static void SyncExploration()
        {
            RequireSavedEditor();
            var source = EditorSceneManager.OpenScene(OpenArchiveBuilder.ScenePath, OpenSceneMode.Single);
            var sourceRoot = source.GetRootGameObjects().Single(g => g.name == "OpenArchive").transform;
            var ambientSky = RenderSettings.ambientSkyColor; var ambientEquator = RenderSettings.ambientEquatorColor; var ambientGround = RenderSettings.ambientGroundColor;
            var explore = EditorSceneManager.OpenScene(OpenArchiveExplorationBuilder.TestScenePath, OpenSceneMode.Additive);
            var destination = explore.GetRootGameObjects().Single(g => g.name == "OpenArchive").transform;
            var originalTargets = destination.GetComponentsInChildren<Transform>(true).GroupBy(SceneObjectKey).ToDictionary(g => g.Key,g => g.First());
            var matched = new Dictionary<Transform,Transform> { {sourceRoot,destination} };
            var rigIDs = explore.GetRootGameObjects().Where(g => g.name != "OpenArchive").SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .Select(t => GlobalObjectId.GetGlobalObjectIdSlow(t).ToString()).ToArray();
            byte[] sourceBytes = File.ReadAllBytes(OpenArchiveBuilder.ScenePath);
            int added = 0, updated = 0;
            foreach (var t in sourceRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t == sourceRoot) continue;
                string path = AnimationUtility.CalculateTransformPath(t, sourceRoot);
                Transform target;
                if (!originalTargets.TryGetValue(SceneObjectKey(t),out target)) target = ChildOccurrence(matched[t.parent],t);
                if (target == null)
                {
                    var copy = Object.Instantiate(t.gameObject,matched[t.parent],false);copy.name=t.name;
                    matched[t]=copy.transform;added++;continue;
                }
                matched[t]=target;
                // Preview camera is intentionally disabled in the exploration scene.
                if (path.StartsWith("Lighting/", StringComparison.Ordinal))
                {
                    var light = t.GetComponent<Light>(); var other = target;
                    if (light != null && other != null && other.GetComponent<Light>() != null)
                    {
                        var targetLight = other.GetComponent<Light>(); targetLight.shadows = light.shadows; targetLight.shadowStrength = light.shadowStrength;
                        targetLight.color = light.color; targetLight.intensity = light.intensity; other.localRotation = t.localRotation;
                    }
                    continue;
                }
                target.localPosition = t.localPosition; target.localRotation = t.localRotation; target.localScale = t.localScale;
                var a = t.GetComponent<MeshRenderer>(); var b = target.GetComponent<MeshRenderer>();
                if (a != null && b != null)
                {
                    b.sharedMaterials = a.sharedMaterials; b.shadowCastingMode = a.shadowCastingMode; b.receiveShadows = a.receiveShadows;
                    if(PrefabUtility.IsPartOfPrefabInstance(b))PrefabUtility.RecordPrefabInstancePropertyModifications(b);
                }
                var sourceFilter = t.GetComponent<MeshFilter>(); var targetFilter = target.GetComponent<MeshFilter>();
                if (sourceFilter != null && targetFilter != null)
                {
                    targetFilter.sharedMesh = sourceFilter.sharedMesh;
                    if (PrefabUtility.IsPartOfPrefabInstance(targetFilter)) PrefabUtility.RecordPrefabInstancePropertyModifications(targetFilter);
                }
                var sourceBoxes = t.GetComponents<BoxCollider>(); var targetBoxes = target.GetComponents<BoxCollider>();
                for (int i = 0; i < Mathf.Min(sourceBoxes.Length, targetBoxes.Length); i++)
                {
                    targetBoxes[i].center = sourceBoxes[i].center; targetBoxes[i].size = sourceBoxes[i].size;
                    if (PrefabUtility.IsPartOfPrefabInstance(targetBoxes[i])) PrefabUtility.RecordPrefabInstancePropertyModifications(targetBoxes[i]);
                }
                var collider = target.GetComponent<MeshCollider>();
                if (collider != null) { var mesh = collider.sharedMesh; collider.sharedMesh = null; collider.sharedMesh = mesh; }
                if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                updated++;
            }
            SceneManager.SetActiveScene(explore);
            RenderSettings.ambientSkyColor = ambientSky; RenderSettings.ambientEquatorColor = ambientEquator; RenderSettings.ambientGroundColor = ambientGround;
            RenderSettings.sun = destination.Find("Lighting/Warm_Daylight").GetComponent<Light>();
            if (!EditorSceneManager.SaveScene(explore)) throw new InvalidOperationException("Could not save exploration environment updates.");
            var remainingRigIDs = new HashSet<string>(explore.GetRootGameObjects().Where(g => g.name != "OpenArchive").SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .Select(t => GlobalObjectId.GetGlobalObjectIdSlow(t).ToString()));
            var sync = new ExplorationSync { addedSceneryRoots = added, updatedEnvironmentTransforms = updated, existingRigObjects = rigIDs.Length,
                retainedRigObjects = rigIDs.Count(remainingRigIDs.Contains), sourceFileUnchanged = sourceBytes.SequenceEqual(File.ReadAllBytes(OpenArchiveBuilder.ScenePath)) };
            sync.passed = sync.retainedRigObjects == sync.existingRigObjects && sync.existingRigObjects == remainingRigIDs.Count && sync.sourceFileUnchanged;
            string report = Folder + "/Previews/ExplorationSyncValidation.json";
            File.WriteAllText(report, JsonUtility.ToJson(sync, true)); AssetDatabase.ImportAsset(report);
            EditorSceneManager.CloseScene(source, true); SceneManager.SetActiveScene(explore);
            Debug.Log("Existing exploration scene synchronized: " + added + " new scenery roots, " + updated + " existing transforms. Player/input/UI unchanged.");
        }

        [Serializable] class ExplorationSync
        {
            public bool passed, sourceFileUnchanged;
            public int addedSceneryRoots, updatedEnvironmentTransforms, existingRigObjects, retainedRigObjects;
        }

        static string SceneObjectKey(Transform t)
        {
            var id=GlobalObjectId.GetGlobalObjectIdSlow(t);return id.targetObjectId+":"+id.targetPrefabId;
        }
        static Transform ChildOccurrence(Transform parent,Transform source)
        {
            int index=0;foreach(Transform sibling in source.parent){if(sibling==source)break;if(sibling.name==source.name)index++;}
            return parent.Cast<Transform>().Where(t=>t.name==source.name).Skip(index).FirstOrDefault();
        }

        sealed class Surface
        {
            readonly List<Vector3> vertices = new List<Vector3>();
            readonly List<Vector2> uv = new List<Vector2>();
            readonly List<Color> colors = new List<Color>();
            readonly List<int> triangles = new List<int>();
            public void Tri(Vector3 a, Vector3 b, Vector3 c, int tile, Func<Vector3, Color> color = null)
            {
                if (Vector3.Cross(b - a, c - a).sqrMagnitude < .00000001f) return;
                int start = vertices.Count;
                foreach (var p in new[] { a, b, c })
                {
                    vertices.Add(p); uv.Add(new Vector2((tile % 8 + .5f) / 8, (tile / 8 + .5f) / 8)); colors.Add(color == null ? Color.white : color(p));
                }
                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            }
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int tile, Func<Vector3, Color> color = null) { Tri(a, b, c, tile, color); Tri(a, c, d, tile, color); }
            public void Append(Mesh mesh, Matrix4x4 matrix)
            {
                int start = vertices.Count;
                foreach (var p in mesh.vertices) { vertices.Add(matrix.MultiplyPoint3x4(p)); colors.Add(Color.white); }
                uv.AddRange(mesh.uv); foreach (int index in mesh.triangles) triangles.Add(start + index);
            }
            public void Write(Mesh mesh)
            {
                mesh.Clear(); mesh.indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
            }
        }
    }
}
