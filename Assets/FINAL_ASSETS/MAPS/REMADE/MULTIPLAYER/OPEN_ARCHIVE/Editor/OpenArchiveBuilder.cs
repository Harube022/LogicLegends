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
    /// <summary>Deterministic, editor-only environment authoring. No gameplay components are created.</summary>
    public static class OpenArchiveBuilder
    {
        public const string Folder = "Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE";
        public static string ScenePath
        {
            get
            {
                string renamed = AssetDatabase.GUIDToAssetPath("6a705d4447127c547bf25f4a7c90bf23");
                return string.IsNullOrEmpty(renamed) ? Folder + "/Scenes/OpenArchive.unity" : renamed;
            }
        }
        static Material palette, water, sea;
        static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        static readonly float[] SlotAngles = { 0, 37.5f, 75, 112.5f, 150, 210, 240, 270, 300, 330 };
        static readonly Vector3[] Cardinal = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
        static readonly string[] DirectionNames = { "North", "South", "East", "West" };
        static System.Random random;
        static Transform environment;
        static Vector3 Polar(float angle, float radius, float y = 0) { float a = angle * Mathf.Deg2Rad; return new Vector3(Mathf.Sin(a) * radius, y, Mathf.Cos(a) * radius); }
        static float Range(float a, float b) { return a + (float)random.NextDouble() * (b - a); }

        [MenuItem("Tools/Logic Legends/Open Archive/Build Complete Environment")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before rebuilding the environment.");
            EnsureFolders();
            AssetDatabase.Refresh();
            random = new System.Random(20261016);
            var previousScene = SceneManager.GetActiveScene();
            Scene old = SceneManager.GetSceneByPath(ScenePath);
            if (old.IsValid() && old.isLoaded && old.isDirty)
                throw new InvalidOperationException("Save or discard edits to OpenArchive before regenerating it. Other scenes are preserved.");
            // Unity refuses to close the last loaded scene. Create the replacement first so
            // the old scene and its registered navigation really unload before updating assets.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            if (old.IsValid() && old.isLoaded && !EditorSceneManager.CloseScene(old, true))
                throw new InvalidOperationException("Unity could not unload the previous Open Archive scene; no assets were regenerated.");
            try
            {
                Progress("Materials");
                PrepareMaterials();
                Progress("Modular kit");
                PrepareKit();
                var root = Group("OpenArchive", null);
                environment = Group("Environment", root);
                foreach (string name in new[] { "Terrain", "MainPaths", "SecondaryPaths", "Rivers", "Ponds", "Waterfalls", "Bridges", "Trees", "Rocks", "Cliffs", "Ruins" }) Group(name, environment);
                Progress("Terrain"); Terrain();
                Progress("Paths and slots"); PathsAndSlots(root);
                Progress("Nest"); Nest(root);
                Progress("Spawn"); Spawn(root);
                Progress("Bridges"); Bridges();
                Progress("Nature and ruins"); Scenery();
                Group("Navigation", root);
                Lighting(root);
                SaveAssembly("UniversalMonsterNest", root.Find("UniversalMonsterNest").gameObject, "Architecture");
                SaveAssembly("PlayerSpawnArea", root.Find("PlayerSpawnArea").gameObject, "Architecture");
                foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                {
                    renderer.receiveShadows = true;
                    if (renderer.gameObject.GetComponentInParent<LODGroup>() == null)
                        GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
                }
                // Establish a scene path before attaching scene-local legacy navigation settings.
                Progress("Saving geometry scene");
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Unity did not save the generated geometry scene.");
                Progress("Navigation bake and acceptance");
                var navigation = OpenArchiveNavigation.BuildAndValidate(root.gameObject, Folder);
                WriteInventory(root, navigation.passed);
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Unity did not save the validated navigation scene.");
                AssetDatabase.SaveAssets();
                Progress("Completed");
                Selection.activeGameObject = root.gameObject;
                if (SceneView.lastActiveSceneView != null)
                    SceneView.lastActiveSceneView.LookAt(new Vector3(0, 0, 0), Quaternion.Euler(55, 0, 0), 135);
                Debug.Log("Open Archive saved: " + ScenePath + ". Navigation acceptance: " + navigation.passed);
            }
            catch
            {
                if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
                throw;
            }
        }

        static void Progress(string phase) { File.WriteAllText(Folder + "/Previews/BuildStatus.json", "{\"phase\":\"" + phase + "\",\"utc\":\"" + DateTime.UtcNow.ToString("o") + "\"}"); }

        static void EnsureFolders()
        {
            foreach (string path in new[] { "Scenes", "Meshes", "Materials", "Textures", "Editor", "Previews", "Prefabs/Terrain", "Prefabs/Bridges", "Prefabs/Nature", "Prefabs/Architecture", "Prefabs/Decorations" })
                Directory.CreateDirectory(Folder + "/" + path);
        }

        static void PrepareMaterials()
        {
            foreach (string name in new[] { "OpenArchive_Palette", "OpenArchive_Emission" })
            {
                string path = Folder + "/Textures/" + name + ".png";
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti != null)
                {
                    ti.textureType = TextureImporterType.Default; ti.sRGBTexture = true;
                    ti.mipmapEnabled = true; ti.filterMode = FilterMode.Point; ti.wrapMode = TextureWrapMode.Clamp;
                    ti.textureCompression = TextureImporterCompression.Uncompressed; ti.maxTextureSize = 512;
                    ti.SaveAndReimport();
                }
            }
            palette = MaterialAsset("Archive_Atlas", "Universal Render Pipeline/Lit", Color.white);
            palette.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Textures/OpenArchive_Palette.png"));
            palette.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Textures/OpenArchive_Emission.png"));
            palette.SetColor("_EmissionColor", new Color(1.6f, 1.3f, .95f)); palette.EnableKeyword("_EMISSION");
            palette.SetFloat("_Smoothness", .12f); palette.SetFloat("_Metallic", 0);
            water = MaterialAsset("Archive_TurquoiseWater", "Universal Render Pipeline/Lit", new Color(.035f, .65f, .78f));
            water.SetFloat("_Smoothness", .32f); water.SetFloat("_Metallic", 0);
            sea = MaterialAsset("Archive_Sea", "Universal Render Pipeline/Unlit", new Color(.035f, .26f, .38f));
        }

        static Material MaterialAsset(string name, string shader, Color color)
        {
            string path = Folder + "/Materials/" + name + ".mat";
            var value = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (value == null) { value = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(value, path); }
            value.shader = Shader.Find(shader); value.SetColor("_BaseColor", color); value.enableInstancing = true;
            EditorUtility.SetDirty(value); return value;
        }

        static void PrepareKit()
        {
            prefabs.Clear();
            string path = Folder + "/Meshes/OpenArchive_Kit.fbx";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer != null)
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.importCameras = false; importer.importLights = false; importer.importAnimation = false;
                importer.isReadable = true; importer.SaveAndReimport();
            }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null) throw new InvalidOperationException("OpenArchive_Kit.fbx has not imported.");
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                string name = filter.gameObject.name;
                var mesh = Object.Instantiate(filter.sharedMesh);
                // Bake the importer's axis/scale conversion into metric, identity-transform Unity meshes.
                var matrix = filter.transform.localToWorldMatrix;
                mesh.vertices = mesh.vertices.Select(v => matrix.MultiplyPoint3x4(v)).ToArray();
                mesh.normals = mesh.normals.Select(n => matrix.inverse.transpose.MultiplyVector(n).normalized).ToArray();
                if (matrix.determinant < 0) { var t = mesh.triangles; for (int i = 0; i < t.Length; i += 3) { int tmp = t[i]; t[i] = t[i + 2]; t[i + 2] = tmp; } mesh.triangles = t; }
                mesh.RecalculateBounds(); mesh.name = name;
                mesh = SaveMesh(name, mesh);
                var go = new GameObject(name);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = palette;
                if (name == "Arch_Wide")
                {
                    Box(go, new Vector3(-6.35f, 4, 0), new Vector3(1.85f, 8, 2.2f));
                    Box(go, new Vector3(6.35f, 4, 0), new Vector3(1.85f, 8, 2.2f));
                    Box(go, new Vector3(0, 10.0f, 0), new Vector3(12, 5.4f, 1.5f));
                }
                else if (name == "Banner_Pillar") Box(go, new Vector3(0, 3.5f, 0), new Vector3(1.25f, 7, 1.25f));
                else if (name.StartsWith("Pillar") || name == "Torch_Pedestal" || name == "Small_Monument")
                    Box(go, mesh.bounds.center, new Vector3(mesh.bounds.size.x * .72f, mesh.bounds.size.y, mesh.bounds.size.z * .72f));
                else if (name.StartsWith("RuinedWall") || name.StartsWith("Rock") || name.StartsWith("Cliff")) Box(go, mesh.bounds.center, mesh.bounds.size * .85f);
                string category = name.StartsWith("Cliff") ? "Terrain" : name.StartsWith("Rock") || name.StartsWith("Bush") || name.StartsWith("Grass") || name.StartsWith("Flower") ? "Nature" : name.StartsWith("Bridge") ? "Bridges" : name.Contains("Torch") || name.Contains("Banner") ? "Decorations" : "Architecture";
                prefabs[name] = PrefabUtility.SaveAsPrefabAsset(go, Folder + "/Prefabs/" + category + "/" + name + ".prefab");
                Object.DestroyImmediate(go);
            }
            for (int i = 1; i <= 5; i++)
            {
                string name = "Tree_0" + i;
                var original = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CHEATCODE/SimpleNaturePack/Prefabs/" + name + ".prefab");
                if (original == null) continue;
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(original);
                instance.name = "Archive_" + name;
                string naturePath = Folder + "/Materials/Archive_NatureAtlas.mat";
                var nature = AssetDatabase.LoadAssetAtPath<Material>(naturePath);
                if (nature == null)
                {
                    nature = new Material(instance.GetComponentInChildren<Renderer>().sharedMaterial) { name = "Archive_NatureAtlas", enableInstancing = true };
                    AssetDatabase.CreateAsset(nature, naturePath);
                }
                nature.enableInstancing = true;
                foreach (var r in instance.GetComponentsInChildren<Renderer>()) r.sharedMaterials = r.sharedMaterials.Select(m => nature).ToArray();
                var lod = instance.GetComponent<LODGroup>();
                if (lod != null)
                {
                    var levels = lod.GetLODs();
                    for (int n = 0; n < levels.Length; n++) levels[n].screenRelativeTransitionHeight = n == 0 ? .12f : .008f;
                    lod.SetLODs(levels); lod.RecalculateBounds();
                }
                prefabs[name] = PrefabUtility.SaveAsPrefabAsset(instance, Folder + "/Prefabs/Nature/Archive_" + name + ".prefab");
                Object.DestroyImmediate(instance);
            }
        }

        static void Box(GameObject go, Vector3 center, Vector3 size) { var c = go.AddComponent<BoxCollider>(); c.center = center; c.size = size; }
        static Transform Group(string name, Transform parent) { var go = new GameObject(name); if (parent != null) go.transform.SetParent(parent, false); return go.transform; }
        static Transform Env(string name) { return environment.Find(name); }
        static GameObject Place(string name, Transform parent, Vector3 pos, float yaw = 0, Vector3? scale = null)
        {
            if (!prefabs.ContainsKey(name)) throw new InvalidOperationException("Missing kit prototype: " + name);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[name]); go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localRotation = Quaternion.Euler(0, yaw, 0); go.transform.localScale = scale ?? Vector3.one; return go;
        }

        static Mesh SaveMesh(string name, Mesh value)
        {
            value.name = name;
            string path = Folder + "/Meshes/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(value, path); return value; }
            // Update managed vertex/index channels explicitly. CopySerialized can alias native mesh
            // buffers while a previous generation's renderer/collider is being released by the Editor.
            existing.Clear(); existing.indexFormat = value.indexFormat;
            existing.vertices = value.vertices; existing.uv = value.uv; existing.normals = value.normals;
            existing.triangles = value.triangles; existing.bounds = value.bounds;
            EditorUtility.SetDirty(existing); Object.DestroyImmediate(value); return existing;
        }

        static GameObject MeshObject(string name, Transform parent, Geo geo, Material material = null, bool collision = false)
        {
            var mesh = SaveMesh(name, geo.Mesh(name));
            var go = Group(name, parent).gameObject;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material ?? palette;
            if (collision) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        static bool IsWater(float x, float z)
        {
            if (Mathf.Abs(x) <= 7.8f || Mathf.Abs(z) <= 7.8f) return false;
            float a = Mathf.Atan2(z, x), r = Mathf.Sqrt(x * x + z * z);
            return r > 29.3f + .9f * Mathf.Sin(a * 5f) && r < 41.5f + 1.3f * Mathf.Sin(a * 7f + 1f);
        }

        static bool WaterCell(float x, float z, float step)
        {
            return IsWater(x, z) || IsWater(x + step, z) || IsWater(x, z + step) || IsWater(x + step, z + step) || IsWater(x + step * .5f, z + step * .5f);
        }

        static void Terrain()
        {
            var ground = new Geo(); var basins = new[] { new Geo(), new Geo(), new Geo(), new Geo() }; var banks = new Geo(); var shore = new Geo();
            const float step = 2;
            for (float x = -100; x < 100; x += step) for (float z = -100; z < 100; z += step)
            {
                if (new Vector2(x + 1, z + 1).magnitude > 98) continue;
                var a = new Vector3(x, 0, z); var b = a + Vector3.forward * step; var c = b + Vector3.right * step; var d = a + Vector3.right * step;
                if (WaterCell(x, z, step))
                {
                    int q = (x < 0 ? 2 : 0) + (z < 0 ? 1 : 0);
                    basins[q].Quad(a + Vector3.down * 1.15f, b + Vector3.down * 1.15f, c + Vector3.down * 1.15f, d + Vector3.down * 1.15f, 45);
                    // Exposed solid banks follow every true hole edge. They are vertical, not a hidden walkable river floor.
                    if (!WaterCell(x - step, z, step)) banks.Quad(b, b + Vector3.down * 2, a + Vector3.down * 2, a, 4);
                    if (!WaterCell(x + step, z, step)) banks.Quad(d, d + Vector3.down * 2, c + Vector3.down * 2, c, 4);
                    if (!WaterCell(x, z - step, step)) banks.Quad(a, a + Vector3.down * 2, d + Vector3.down * 2, d, 4);
                    if (!WaterCell(x, z + step, step)) banks.Quad(c, c + Vector3.down * 2, b + Vector3.down * 2, b, 4);
                    bool edge = !WaterCell(x - step, z, step) || !WaterCell(x + step, z, step) || !WaterCell(x, z - step, step) || !WaterCell(x, z + step, step);
                    Vector3 rock = new Vector3(x + 1, -.35f, z + 1);
                    if (edge && random.NextDouble() < .65 && !Protected(rock, .6f))
                        shore.Append(prefabs["Rock_A"].GetComponent<MeshFilter>().sharedMesh, Matrix4x4.TRS(rock, Quaternion.Euler(0, Range(0, 360), 0), new Vector3(1.1f, .65f, .9f)));
                }
                else ground.Quad(a, b, c, d, 22);
            }
            // Continuous circular rim fills the square-grid silhouette and provides a generous outer grass bypass.
            ground.Annulus(new Vector3(0, -.008f, 0), 94, 100, 192, 22);
            MeshObject("Island_Grassland_WithRiverHoles", Env("Terrain"), ground, null, true);
            MeshObject("Riverbank_CutStone", Env("Rocks"), banks, null, false);
            MeshObject("Riverbank_RockClusters", Env("Rocks"), shore);
            for (int i = 0; i < 4; i++) MeshObject("Turquoise_RiverPond_" + (i + 1), i % 2 == 0 ? Env("Rivers") : Env("Ponds"), basins[i], water);
            var ocean = new Geo(); ocean.Disk(new Vector3(0, -10.1f, 0), 400, 128, 44); MeshObject("Perimeter_Sea", Env("Rivers"), ocean, sea);
            var skirt = new Geo();
            for (int i = 0; i < 192; i++)
            {
                var a = Polar(i * 360f / 192, 99.8f, -.1f); var b = Polar((i + 1) * 360f / 192, 99.8f, -.1f);
                skirt.Quad(a, a + Vector3.down * 10, b + Vector3.down * 10, b, i % 3 == 0 ? 2 : 0);
            }
            MeshObject("Island_CliffFoundation", Env("Cliffs"), skirt);
            SaveReusable("Island_Grassland_WithRiverHoles", Env("Terrain").Find("Island_Grassland_WithRiverHoles").gameObject, "Terrain");
        }

        static void PathsAndSlots(Transform root)
        {
            var ring = new Geo(); ring.Annulus(new Vector3(0, .025f, 0), 49, 59, 192, 48);
            MeshObject("Continuous_Main_Ring_10m", Env("MainPaths"), ring);
            var secondary = new Geo(); secondary.Annulus(new Vector3(0, .035f, 0), 21.5f, 27.3f, 96, 46);
            for (int i = 0; i < 4; i++)
            {
                var g = new Geo(); g.Ribbon(Cardinal[i] * 21, Cardinal[i] * (i == 1 ? 84 : 59), 10, .04f, 48);
                var side = new Vector3(Cardinal[i].z, 0, -Cardinal[i].x);
                for (float r = 24; r < (i == 1 ? 79 : 58); r += 2.3f)
                    for (int lane = -1; lane <= 1; lane++)
                        g.PavingStone(Cardinal[i] * (r + Range(-.18f, .18f)) + side * (lane * 3.05f), 2.75f, 1.9f, .065f, (i * 90), random.NextDouble() < .25 ? 6 : 47);
                MeshObject("Radial_" + DirectionNames[i] + "_10m", Env("MainPaths"), g);
            }
            var slots = Group("FutureStructureSlots", root);
            for (int i = 0; i < SlotAngles.Length; i++)
            {
                Vector3 p = Polar(SlotAngles[i], 74);
                var anchor = Group("StructureSlot_" + (i + 1).ToString("00"), slots); anchor.position = p;
                // Square, level paving is environment ground only. The reserved Transform has no children or components.
                secondary.Ribbon(Polar(SlotAngles[i], 56), p, 6, .045f, 46);
                var pad = new Geo(); pad.Disk(p + Vector3.up * .055f, 12.3f, 20, 46);
                for (float px = -10; px <= 10; px += 2.6f) for (float pz = -10; pz <= 10; pz += 2.6f)
                {
                    if (px * px + pz * pz > 114 || random.NextDouble() < .32) continue;
                    pad.PavingStone(p + new Vector3(px + Range(-.2f, .2f), 0, pz + Range(-.2f, .2f)), 2.3f, 2.1f, .061f, Range(-12, 12), random.NextDouble() < .3 ? 6 : 47);
                }
                MeshObject("Empty_Ground_" + (i + 1).ToString("00"), Env("SecondaryPaths"), pad);
            }
            MeshObject("Secondary_Loops_And_SlotApproaches_6m", Env("SecondaryPaths"), secondary);
            // Staggered paving gives the broad paths a ruin-like rhythm without adding collision steps.
            var paving = new Geo();
            for (int i = 0; i < 128; i++)
            {
                float angle = i * 360f / 128;
                Vector3 p = Polar(angle, 54, .055f); Vector3 f = Polar(angle + 90, 1); Vector3 side = Polar(angle, 1);
                paving.Quad(p - f * .7f - side * 3.8f, p - f * .7f + side * 3.8f, p + f * .7f + side * 3.8f, p + f * .7f - side * 3.8f, i % 4 == 0 ? 6 : 47);
            }
            MeshObject("MainRing_StonePaving", Env("MainPaths"), paving);
        }

        static void Nest(Transform root)
        {
            var nest = Group("UniversalMonsterNest", root);
            var floor = new Geo(); floor.Disk(new Vector3(0, .8f, 0), 15, 96, 6);
            for (float x = -12; x <= 12; x += 2.5f) for (float z = -12; z <= 12; z += 2.5f)
                if (x * x + z * z < 180) floor.PavingStone(new Vector3(x, 0, z), 2.3f, 2.3f, .802f, 0, 47);
            MeshObject("Nest_StoneFloor_30m", nest, floor, null, true);
            var rune = new Geo(); Rune(rune, new Vector3(0, .82f, 0), 8.8f, 32);
            MeshObject("Ancient_Gold_Rune", nest, rune);
            Group("MonsterSpawnPoint", nest).position = new Vector3(0, .8f, 0);
            var rampGeo = new Geo(); var footings = new Geo(); var landings = new Geo();
            for (int i = 0; i < 4; i++)
            {
                var dir = Cardinal[i]; var side = new Vector3(dir.z, 0, -dir.x);
                var anchor = Group("Entrance_" + DirectionNames[i], nest);
                anchor.position = dir * 15 + Vector3.up * .8f; anchor.rotation = Quaternion.LookRotation(dir);
                Place("Arch_Wide", nest, dir * 15.2f + Vector3.up * .8f, i < 2 ? 0 : 90);
                footings.Cuboid(dir * 15.2f - side * 6.35f + Vector3.up * .4f, new Vector3(i < 2 ? 2.4f : 2.7f, .8f, i < 2 ? 2.7f : 2.4f), 4);
                footings.Cuboid(dir * 15.2f + side * 6.35f + Vector3.up * .4f, new Vector3(i < 2 ? 2.4f : 2.7f, .8f, i < 2 ? 2.7f : 2.4f), 4);
                var ramp = Group("Ramp_" + DirectionNames[i] + "_Clear10m", nest);
                Vector3 high = dir * 15 + Vector3.up * .8f, low = dir * 23;
                Vector3 inner = dir * 12.5f + Vector3.up * .8f;
                landings.Quad(inner - side * 5, high - side * 5, high + side * 5, inner + side * 5, 6);
                var g = new Geo(); g.Quad(high - side * 5, low - side * 5, low + side * 5, high + side * 5, 47);
                // Collider follows the smooth rise; inset lines make the four wide ramps read as ancient stairways.
                var meshGo = MeshObject("RampSurface_" + DirectionNames[i], ramp, g);
                var smooth = Group("Smooth_Walkable_Collision", ramp);
                smooth.rotation = Quaternion.LookRotation(low - high, Vector3.up);
                smooth.position = (high + low) * .5f - smooth.up * .15f;
                var col = smooth.gameObject.AddComponent<BoxCollider>(); col.size = new Vector3(10, .3f, Vector3.Distance(high, low));
                for (int s = 1; s < 10; s++)
                {
                    Vector3 p = Vector3.Lerp(high, low, s / 10f) + Vector3.up * .012f;
                    rampGeo.Quad(p - side * 4.95f - dir * .055f, p - side * 4.95f + dir * .055f, p + side * 4.95f + dir * .055f, p + side * 4.95f - dir * .055f, 4);
                }
            }
            MeshObject("Four_Ramp_StoneJoints", nest, rampGeo);
            MeshObject("Nest_Four_Wide_Landings", nest, landings, null, true);
            MeshObject("Arch_Stone_Footings", nest, footings, null, true);
            for (int quadrant = 0; quadrant < 4; quadrant++)
            {
                for (int w = 0; w < 3; w++)
                {
                    float angle = quadrant * 90 + 31 + w * 14;
                    Place("RuinedWall_Short", nest, Polar(angle, 14.15f, .8f), angle, new Vector3(1.05f, w == 1 ? 1 : .72f, 1));
                }
                Place("Pillar_Broken", nest, Polar(quadrant * 90 + 37, 13.8f), quadrant * 90);
                Place("Torch_Pedestal", nest, Polar(quadrant * 90 + 55, 14.2f));
            }
        }

        static void Spawn(Transform root)
        {
            var spawn = Group("PlayerSpawnArea", root); spawn.position = new Vector3(0, 0, -83);
            var plaza = new Geo(); plaza.Disk(new Vector3(0, .15f, 0), 9, 64, 6);
            MeshObject("Spawn_StonePlaza_18m", spawn, plaza, null, true);
            var rune = new Geo(); Rune(rune, new Vector3(0, .17f, 0), 3.4f, 33); MeshObject("Green_Spawn_Glyph", spawn, rune);
            Place("Arch_Wide", spawn, new Vector3(0, .15f, -7.2f));
            var foot = new Geo(); foot.Cuboid(new Vector3(-6.35f, .075f, -7.2f), new Vector3(2.4f, .15f, 2.7f), 4); foot.Cuboid(new Vector3(6.35f, .075f, -7.2f), new Vector3(2.4f, .15f, 2.7f), 4);
            MeshObject("Spawn_Arch_Footings", spawn, foot, null, true);
            Place("Banner_Pillar", spawn, new Vector3(-7, .15f, -.5f)); Place("Banner_Pillar", spawn, new Vector3(7, .15f, -.5f));
            for (int i = 0; i < 4; i++)
            {
                var p = Group("PlayerSpawn_" + (i + 1).ToString("00"), spawn);
                p.localPosition = new Vector3(i % 2 == 0 ? -2 : 2, 1.23f, i < 2 ? -2 : 2);
                Place("Torch_Pedestal", spawn, new Vector3(i % 2 == 0 ? -6.5f : 6.5f, .15f, i < 2 ? -4.4f : 4.4f), 0, Vector3.one * .7f);
            }
        }

        static void Bridges()
        {
            var bridge = Group("Bridge_Modular_10x24m", null);
            var deck = new Geo();
            // Keep the river span clear of both walking loops. The legacy prefab name is retained for references.
            for (int i = 0; i < 17; i++) deck.Cuboid(new Vector3(0, .05f, -8 + i), new Vector3(10, .4f, .94f), i % 3 == 0 ? 10 : 11);
            MeshObject("Wooden_Bridge_Deck", bridge, deck);
            Box(bridge.gameObject, new Vector3(0, .02f, 0), new Vector3(10, .46f, 17));
            Box(bridge.gameObject, new Vector3(-4.8f, 1, .5f), new Vector3(.25f, 1.5f, 15));
            Box(bridge.gameObject, new Vector3(4.8f, 1, .5f), new Vector3(.25f, 1.5f, 15));
            const float railScale = 15f / 22.78f;
            for (int side = -1; side <= 1; side += 2) for (int i = 0; i < 9; i++)
                Place("Bridge_Railing_Module", bridge, new Vector3(side * 4.8f, .25f, .5f + (-10 + i * 2.5f) * railScale), 90, new Vector3(railScale, 1, 1));
            var approaches = new Geo();
            approaches.Quad(new Vector3(-5, 0, -9.5f), new Vector3(-5, .25f, -8.5f), new Vector3(5, .25f, -8.5f), new Vector3(5, 0, -9.5f), 47);
            approaches.Quad(new Vector3(-5, .25f, 8.5f), new Vector3(-5, 0, 10.5f), new Vector3(5, 0, 10.5f), new Vector3(5, .25f, 8.5f), 47);
            MeshObject("Bridge_Approach_Ramps", bridge, approaches, null, true);
            var prefab = PrefabUtility.SaveAsPrefabAsset(bridge.gameObject, Folder + "/Prefabs/Bridges/Bridge_Modular_10x24m.prefab");
            Object.DestroyImmediate(bridge.gameObject);
            prefabs["Bridge_Modular_10x24m"] = prefab;
            for (int i = 0; i < 4; i++)
            {
                float a = 45 + 90 * i;
                Place("Bridge_Modular_10x24m", Env("Bridges"), Polar(a, 35.5f), a);
                var g = new Geo(); var side = Polar(a + 90, 1);
                Vector3 inner = Polar(a, 24.4f, .047f), deckIn = Polar(a, 27, .047f);
                Vector3 deckOut = Polar(a, 44, .047f), outer = Polar(a, 51, .047f);
                g.Quad(inner - side * 6, deckIn - side * 5, deckIn + side * 5, inner + side * 6, 46);
                g.Quad(deckOut - side * 5, outer - side * 6, outer + side * 6, deckOut + side * 5, 46);
                MeshObject("Bridge_Dry_Approach_" + i, Env("SecondaryPaths"), g);
            }
        }

        static bool Protected(Vector3 p, float margin)
        {
            float r = new Vector2(p.x, p.z).magnitude;
            if (r < 28 + margin || r > 85 - margin && r < 94 + margin) return true;
            if (r > 48 - margin && r < 60 + margin) return true;
            if (Mathf.Abs(p.x) < 6 + margin || Mathf.Abs(p.z) < 6 + margin) return true;
            if (Vector2.Distance(new Vector2(p.x, p.z), new Vector2(0, -83)) < 11 + margin) return true;
            foreach (float angle in SlotAngles)
            {
                Vector3 s = Polar(angle, 74);
                if (Mathf.Abs(p.x - s.x) < 13 + margin && Mathf.Abs(p.z - s.z) < 13 + margin) return true;
                Vector3 a = Polar(angle, 54); Vector3 b = s; Vector3 d = b - a;
                float t = Mathf.Clamp01(Vector3.Dot(p - a, d) / d.sqrMagnitude);
                if (Vector3.Distance(p, a + d * t) < 3.5f + margin) return true;
            }
            for (int i = 0; i < 4; i++)
            {
                Vector3 d = Polar(45 + 90 * i, 1), side = new Vector3(d.z, 0, -d.x);
                float along = Vector3.Dot(p, d), lateral = Mathf.Abs(Vector3.Dot(p, side));
                if (along > 20 - margin && along < 51 + margin && lateral < 5.5f + margin) return true;
            }
            return false;
        }

        static void Scenery()
        {
            for (int i = 0; i < 168; i++)
            {
                float a = i * 360f / 168 + Range(-.4f, .4f);
                float height = Range(.94f, 1.25f);
                var go = Place(i % 2 == 0 ? "Cliff_Column_A" : "Cliff_Column_B", Env("Cliffs"), Polar(a, Range(98.5f, 100), -10), a, new Vector3(Range(.85f, 1.25f), height, Range(.85f, 1.1f)));
                go.name = "Perimeter_Cliff_" + i.ToString("000");
            }
            // Perimeter trees sit behind the clear grass bypass; inner groves use the same strict footprint/route mask.
            int trees = 0;
            for (int attempt = 0; attempt < 2500 && trees < 150; attempt++)
            {
                Vector3 p = attempt < 95 ? Polar(attempt * 360f / 95 + Range(-1, 1), Range(95, 97)) : Polar(Range(0, 360), Range(29, 84));
                if (Protected(p, 1.7f) || IsWater(p.x, p.z) || IsWater(p.x + 2, p.z + 2) || IsWater(p.x - 2, p.z - 2)) continue;
                float s = Range(1.3f, 1.9f);
                Place("Tree_0" + (1 + random.Next(5)), Env("Trees"), p, Range(0, 360), Vector3.one * s); trees++;
            }
            // Edge groves: scale is constrained so their canopies do not fill the outer movement corridor.
            for (int i = 0; i < 72; i++)
            {
                float a = i * 5 + 2.5f;
                Vector3 p = Polar(a, 97);
                if (Mathf.Abs(p.x) < 10 || Mathf.Abs(p.z) < 8 || Vector2.Distance(new Vector2(p.x, p.z), new Vector2(0, -83)) < 17) continue;
                Place("Tree_0" + (1 + random.Next(5)), Env("Trees"), p, Range(0, 360), Vector3.one * Range(1.1f, 1.55f));
            }
            for (int i = 0; i < 170; i++)
            {
                Vector3 p = Polar(Range(0, 360), Range(28, 84));
                if (Protected(p, .9f) || IsWater(p.x, p.z)) continue;
                Place(i % 3 == 0 ? "Rock_A" : "Rock_B", Env("Rocks"), p, Range(0, 360), Vector3.one * Range(.5f, 1.25f));
            }
            var vegetation = new[] { new Geo(), new Geo(), new Geo(), new Geo(), new Geo(), new Geo(), new Geo(), new Geo() };
            for (int i = 0; i < 1800; i++)
            {
                Vector3 p = Polar(Range(0, 360), Range(28, 98));
                if (Protected(p, .4f) || IsWater(p.x, p.z)) continue;
                int q = Mathf.Clamp((int)((Mathf.Atan2(p.x, p.z) + Mathf.PI) / (Mathf.PI * 2) * 8), 0, 7);
                string name = i % 45 == 0 ? "Flower_Cluster" : i % 3 == 0 ? "Grass_Clump" : i % 2 == 0 ? "Bush_A" : "Bush_B";
                var mf = prefabs[name].GetComponent<MeshFilter>();
                vegetation[q].Append(mf.sharedMesh, Matrix4x4.TRS(p, Quaternion.Euler(0, Range(0, 360), 0), Vector3.one * Range(.55f, 1.1f)));
            }
            for (int i = 0; i < vegetation.Length; i++) MeshObject("Vegetation_Cluster_" + i, Env("Trees"), vegetation[i]);
            // Sparse academy ruins frame the island and leave the empty construction footprints untouched.
            for (int i = 0; i < 20; i++)
            {
                Vector3 p = Polar(i * 18 + 9, 93.8f);
                if (Mathf.Abs(p.x) < 12 || Mathf.Abs(p.z) < 10) continue;
                Place(i % 4 == 0 ? "Pillar_Intact" : "Pillar_Broken", Env("Ruins"), p, i * 18);
                if (i % 3 == 0) Place("RuinedWall_Short", Env("Ruins"), p + Polar(i * 18 + 90, 2.7f), i * 18 + 90);
            }
            Place("Arch_Wide", Env("Ruins"), new Vector3(0, 0, 94));
            Place("Banner_Pillar", Env("Ruins"), new Vector3(-11, 0, 93)); Place("Banner_Pillar", Env("Ruins"), new Vector3(11, 0, 93));
            for (int i = 0; i < 8; i++) Waterfall(22.5f + 45 * i, i);
            var lilies = new Geo();
            for (int i = 0; i < 100; i++)
            {
                Vector3 p = Polar(Range(0, 360), Range(30, 41), -1.12f);
                if (!IsWater(p.x, p.z)) continue;
                lilies.Disk(p, Range(.2f, .5f), 7, 23);
            }
            MeshObject("Occasional_LilyPads", Env("Ponds"), lilies);
        }

        static void Waterfall(float angle, int index)
        {
            Vector3 p = Polar(angle, 100, 0); Vector3 side = Polar(angle + 90, 1), outward = Polar(angle, 1);
            var g = new Geo();
            for (int strip = 0; strip < 4; strip++)
            {
                float x = -1.8f + strip * 1.2f;
                Vector3 a = p + side * x + outward * .6f;
                Vector3 b = a + side * 1.3f;
                Vector3 d = a + outward * 1.3f + Vector3.down * Range(9.5f, 10);
                g.Quad(d, d + side * 1.3f, b, a, strip % 2 == 0 ? 30 : 34);
                // Horizontal source pool and foam are opaque, static geometry.
                g.Quad(a, b, b - outward * 2, a - outward * 2, 45);
            }
            g.Disk(p + outward * 2 + Vector3.down * 9.98f, 3.2f, 20, 34);
            MeshObject("Cliff_Waterfall_" + (index + 1), Env("Waterfalls"), g);
        }

        static void Rune(Geo g, Vector3 center, float radius, int color)
        {
            g.Annulus(center, radius - .12f, radius, 96, color);
            g.Annulus(center, radius * .74f - .07f, radius * .74f, 80, color);
            g.Annulus(center, radius * .22f - .08f, radius * .22f, 48, color);
            for (int i = 0; i < 8; i++) g.Ribbon(center + Polar(i * 45, radius * .22f), center + Polar(i * 45, radius), .075f, center.y, color);
        }

        static void Lighting(Transform root)
        {
            var lighting = Group("Lighting", root);
            var sun = Group("Warm_Daylight", lighting).gameObject.AddComponent<Light>(); sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(48, -32, 0); sun.color = new Color(1, .91f, .76f); sun.intensity = 1.45f;
            sun.shadows = LightShadows.Soft; sun.shadowStrength = .65f; sun.shadowBias = .05f;
            RenderSettings.sun = sun; RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.57f, .75f, .91f); RenderSettings.ambientEquatorColor = new Color(.49f, .58f, .51f);
            RenderSettings.ambientGroundColor = new Color(.32f, .34f, .27f); RenderSettings.ambientIntensity = 1;
            RenderSettings.fog = true; RenderSettings.fogColor = new Color(.42f, .67f, .78f); RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogStartDistance = 320; RenderSettings.fogEndDistance = 650;
            var sky = MaterialAsset("Archive_DaylightSky", "Skybox/Procedural", Color.white);
            sky.SetColor("_SkyTint", new Color(.5f, .55f, .65f)); sky.SetColor("_GroundColor", new Color(.4f, .5f, .6f)); sky.SetFloat("_Exposure", 1.15f); sky.SetFloat("_AtmosphereThickness", .85f); RenderSettings.skybox = sky;
            var camera = Group("Environment_Preview_Camera", lighting).gameObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 8.5f, -91); camera.transform.LookAt(new Vector3(0, 4, -5));
            camera.fieldOfView = 65; camera.nearClipPlane = .3f; camera.farClipPlane = 600; camera.allowHDR = false; camera.allowMSAA = true;
            camera.tag = "MainCamera";
        }

        static void SaveReusable(string name, GameObject go, string category) { PrefabUtility.SaveAsPrefabAsset(go, Folder + "/Prefabs/" + category + "/" + name + ".prefab"); }
        static void SaveAssembly(string name, GameObject go, string category)
        {
            var copy = Object.Instantiate(go); copy.name = name; copy.transform.position = Vector3.zero; copy.transform.rotation = Quaternion.identity;
            PrefabUtility.SaveAsPrefabAsset(copy, Folder + "/Prefabs/" + category + "/" + name + ".prefab"); Object.DestroyImmediate(copy);
        }

        [Serializable] class Inventory { public string scene; public int meshRenderers, lodGroups, colliders, prefabInstances, materials, triangleUpperBound; public bool navigationPassed; public string notes; }
        [Serializable] class RebuildReport { public bool passed, assetGuidsPreserved, objectCountsMatch; public int checkedGuids, objectsBefore, objectsAfter; public string scene; }

        [MenuItem("Tools/Logic Legends/Open Archive/Verify Repeatable Build")]
        public static void VerifyRepeatability()
        {
            var beforeScene = SceneManager.GetSceneByPath(ScenePath);
            if (!beforeScene.IsValid() || !beforeScene.isLoaded) throw new InvalidOperationException("Open the saved Open Archive scene first.");
            var before = beforeScene.GetRootGameObjects().Single(g => g.name == "OpenArchive");
            int count = before.GetComponentsInChildren<Transform>(true).Length;
            var guids = AssetDatabase.FindAssets("", new[] { Folder }).ToDictionary(g => AssetDatabase.GUIDToAssetPath(g), g => g);
            Build();
            var after = SceneManager.GetSceneByPath(ScenePath).GetRootGameObjects().Single(g => g.name == "OpenArchive");
            var report = new RebuildReport { scene = ScenePath, checkedGuids = guids.Count, objectsBefore = count, objectsAfter = after.GetComponentsInChildren<Transform>(true).Length,
                assetGuidsPreserved = guids.All(p => AssetDatabase.AssetPathToGUID(p.Key) == p.Value) };
            report.objectCountsMatch = report.objectsBefore == report.objectsAfter;
            report.passed = report.assetGuidsPreserved && report.objectCountsMatch;
            File.WriteAllText(Folder + "/Previews/RepeatabilityValidation.json", JsonUtility.ToJson(report, true));
            AssetDatabase.ImportAsset(Folder + "/Previews/RepeatabilityValidation.json");
            if (!report.passed) throw new InvalidOperationException("Repeatability checks failed; inspect RepeatabilityValidation.json.");
        }
        static void WriteInventory(Transform root, bool navigationPassed)
        {
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            int triangles = root.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null).Sum(f => f.sharedMesh.triangles.Length / 3);
            var inventory = new Inventory { scene = ScenePath, meshRenderers = renderers.Length, lodGroups = root.GetComponentsInChildren<LODGroup>(true).Length,
                colliders = root.GetComponentsInChildren<Collider>(true).Length, prefabInstances = root.GetComponentsInChildren<Transform>(true).Count(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)),
                materials = renderers.SelectMany(r => r.sharedMaterials).Distinct().Count(), triangleUpperBound = triangles, navigationPassed = navigationPassed,
                notes = "Triangle upper bound sums ALL LOD meshes and does not represent a single frame. One directional light, opaque water, 512px shared kit atlas, vendor tree atlas, clustered small vegetation. Actual Android hardware profiling remains required." };
            File.WriteAllText(Folder + "/Previews/EnvironmentInventory.json", JsonUtility.ToJson(inventory, true));
        }

        /// <summary>Flat-shaded geometry with palette tile UVs. Winding is explicitly upward for walkable faces.</summary>
        sealed class Geo
        {
            readonly List<Vector3> v = new List<Vector3>(); readonly List<Vector2> uv = new List<Vector2>(); readonly List<int> t = new List<int>();
            public void Tri(Vector3 a, Vector3 b, Vector3 c, int color)
            {
                int i = v.Count; v.Add(a); v.Add(b); v.Add(c); t.Add(i); t.Add(i + 1); t.Add(i + 2);
                var p = new Vector2((color % 8 + .5f) / 8, (color / 8 + .5f) / 8); uv.Add(p); uv.Add(p); uv.Add(p);
            }
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int color) { Tri(a, b, c, color); Tri(a, c, d, color); }
            public void Disk(Vector3 c, float r, int segments, int color) { for (int i = 0; i < segments; i++) Tri(c, c + Polar(i * 360f / segments, r), c + Polar((i + 1) * 360f / segments, r), color); }
            public void Annulus(Vector3 c, float inner, float outer, int segments, int color)
            {
                for (int i = 0; i < segments; i++) { float a = i * 360f / segments, b = (i + 1) * 360f / segments; Quad(c + Polar(a, inner), c + Polar(a, outer), c + Polar(b, outer), c + Polar(b, inner), color); }
            }
            public void Ribbon(Vector3 a, Vector3 b, float width, float y, int color)
            {
                a.y = b.y = y; Vector3 side = Vector3.Cross(Vector3.up, (b - a).normalized) * width * .5f;
                Quad(a - side, b - side, b + side, a + side, color);
            }
            public void PavingStone(Vector3 center, float width, float depth, float y, float yaw, int color)
            {
                center.y = y; var rotation = Quaternion.Euler(0, yaw, 0);
                var points = new[] { new Vector3(-.40f,0,-.5f), new Vector3(-.5f,0,-.32f), new Vector3(-.5f,0,.37f), new Vector3(-.28f,0,.5f), new Vector3(.4f,0,.5f), new Vector3(.5f,0,.25f), new Vector3(.5f,0,-.38f), new Vector3(.3f,0,-.5f) };
                for (int i=0;i<points.Length;i++) { var a=points[i]; var b=points[(i+1)%points.Length]; a.x*=width; a.z*=depth; b.x*=width; b.z*=depth; Tri(center, center+rotation*a, center+rotation*b, color); }
            }
            public void Cuboid(Vector3 center, Vector3 size, int color)
            {
                var a = center - size * .5f; var b = center + size * .5f;
                Quad(new Vector3(a.x,b.y,a.z), new Vector3(a.x,b.y,b.z), new Vector3(b.x,b.y,b.z), new Vector3(b.x,b.y,a.z), color);
                Quad(new Vector3(a.x,a.y,a.z), new Vector3(b.x,a.y,a.z), new Vector3(b.x,a.y,b.z), new Vector3(a.x,a.y,b.z), color);
                Quad(new Vector3(a.x,a.y,a.z), new Vector3(a.x,a.y,b.z), new Vector3(a.x,b.y,b.z), new Vector3(a.x,b.y,a.z), color);
                Quad(new Vector3(b.x,a.y,b.z), new Vector3(b.x,a.y,a.z), new Vector3(b.x,b.y,a.z), new Vector3(b.x,b.y,b.z), color);
                Quad(new Vector3(b.x,a.y,a.z), new Vector3(a.x,a.y,a.z), new Vector3(a.x,b.y,a.z), new Vector3(b.x,b.y,a.z), color);
                Quad(new Vector3(a.x,a.y,b.z), new Vector3(b.x,a.y,b.z), new Vector3(b.x,b.y,b.z), new Vector3(a.x,b.y,b.z), color);
            }
            public void Append(Mesh m, Matrix4x4 matrix)
            {
                int first = v.Count; foreach (var p in m.vertices) v.Add(matrix.MultiplyPoint3x4(p)); uv.AddRange(m.uv); foreach (int n in m.triangles) t.Add(first + n);
            }
            public Mesh Mesh(string name)
            {
                var mesh = new Mesh { name = name, indexFormat = v.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
            }
        }
    }
}
