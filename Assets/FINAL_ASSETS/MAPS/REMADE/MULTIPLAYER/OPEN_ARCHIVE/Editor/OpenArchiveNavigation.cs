using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using RuntimeBuilder = UnityEngine.AI.NavMeshBuilder;

namespace OpenArchiveAuthoring
{
    /// <summary>Editor-only collider bake and acceptance checks; contains no movement or AI behaviour.</summary>
    public static class OpenArchiveNavigation
    {
        // Translate temporary query instances well away from any other loaded scene's navigation.
        // Saved data remains in the authored scene coordinates. Only instances owned here are removed.
        private static readonly Vector3 QueryOffset = new Vector3(20000f, 0f, 20000f);
        private static readonly string[] Directions = { "North", "South", "East", "West" };
        private static readonly Vector3[] Cardinal = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
        private static readonly string[] ExcludedGroups = { "Cliffs", "Rivers", "Ponds", "Waterfalls" };
        private static readonly Bounds BakeBounds = new Bounds(new Vector3(0f, 5f, 0f), new Vector3(224f, 70f, 224f));

        [MenuItem("Tools/Logic Legends/Open Archive/Rebake Existing Navigation And Validate")]
        public static void RebakeExisting()
        {
            OpenArchiveImprovements.RequireSavedEditor();
            var scene = SceneManager.GetActiveScene();
            if (scene.path != OpenArchiveBuilder.ScenePath)
                scene = EditorSceneManager.OpenScene(OpenArchiveBuilder.ScenePath, OpenSceneMode.Single);
            var root = scene.GetRootGameObjects().Single(g => g.name == "OpenArchive");
            var report = BuildAndValidate(root, OpenArchiveBuilder.Folder);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the navigation attachment.");
            Debug.Log("Existing Open Archive navigation validation: " + report.passed);
        }

        [Serializable]
        public sealed class Check
        {
            public string name;
            public bool passed;
            public string detail;
        }

        [Serializable]
        public sealed class Dataset
        {
            public string name;
            public string asset;
            public float radius;
            public float height;
            public float maximumClimb;
            public float maximumSlope;
            public float voxelSize;
            public int sources;
            public int navigationTriangles;
            public int checksPassed;
            public int checksFailed;
            public bool bridgesIncluded;
        }

        [Serializable]
        public sealed class Report
        {
            public bool passed;
            public string scene;
            public string generatedUtc;
            public int enabledNonTriggerColliders;
            public int excludedAreaSources;
            public int bridgeSources;
            public bool playerDataAttachedToScene;
            public string sceneAttachment;
            public List<Dataset> datasets = new List<Dataset>();
            public List<Check> checks = new List<Check>();
            public List<string> limitations = new List<string>();
        }

        /// <summary>
        /// Bakes two agent sizes and validates navigation, footprints, dry corridors, and bridge alternatives.
        /// Mutates only owned navigation assets and the supplied root scene's legacy navigation reference.
        /// Acceptance failures are reported rather than thrown, so the authored scene can still be reviewed.
        /// </summary>
        public static Report BuildAndValidate(GameObject root, string assetFolder)
        {
            if (root == null || !root.scene.IsValid() || !root.scene.isLoaded)
                throw new ArgumentException("A root in the authored, loaded Open Archive scene is required.");
            if (string.IsNullOrWhiteSpace(assetFolder) || !assetFolder.Replace('\\', '/').StartsWith("Assets/", StringComparison.Ordinal))
                throw new ArgumentException("Navigation assetFolder must be a project-relative Assets folder.");

            assetFolder = assetFolder.Replace('\\', '/').TrimEnd('/');
            Directory.CreateDirectory(assetFolder + "/Meshes");
            Directory.CreateDirectory(assetFolder + "/Previews");
            AssetDatabase.Refresh();
            Physics.SyncTransforms();
            var report = new Report { scene = root.scene.path, generatedUtc = DateTime.UtcNow.ToString("o") };
            var colliders = root.GetComponentsInChildren<Collider>(false)
                .Where(c => c.enabled && !c.isTrigger && c.gameObject.activeInHierarchy && !(c is CharacterController))
                .ToArray();
            report.enabledNonTriggerColliders = colliders.Length;
            if (colliders.Length == 0) throw new InvalidOperationException("The environment has no enabled physics colliders to bake.");

            var sources = GatherSources(root.transform, colliders, report);
            if (sources.Count == 0) throw new InvalidOperationException("Unity collected no navigation sources from the environment colliders.");
            var slots = FindSlots(root.transform);
            ValidateHierarchy(root.transform, slots, report);
            ValidatePhysicalLayout(root.transform, slots, colliders, report);

            NavMeshData playerData = null;
            foreach (bool bridges in new[] { true, false })
            {
                var selected = bridges ? sources : sources.Where(s => !UnderGroup(s.component != null ? s.component.transform : null, root.transform, "Bridges")).ToList();
                foreach (bool monster in new[] { false, true })
                {
                    var settings = Settings(monster);
                    string label = (monster ? "LargeMonster" : "Player") + (bridges ? "" : "_NoBridges");
                    var data = RuntimeBuilder.BuildNavMeshData(settings, selected, BakeBounds, Vector3.zero, Quaternion.identity);
                    if (data == null)
                    {
                        Add(report, label + ": build", false, "Unity did not produce a NavMeshData object.");
                        continue;
                    }
                    data.name = "OpenArchive_" + label;
                    bool retained = false;
                    try
                    {
                        var stats = new Dataset
                        {
                            name = label, radius = settings.agentRadius, height = settings.agentHeight,
                            maximumClimb = settings.agentClimb, maximumSlope = settings.agentSlope,
                            voxelSize = settings.voxelSize, sources = selected.Count, bridgesIncluded = bridges
                        };
                        int checkStart = report.checks.Count;
                        ValidateDataset(root.transform, slots, data, settings.agentTypeID, label, report, stats);
                        stats.checksPassed = report.checks.Skip(checkStart).Count(c => c.passed);
                        stats.checksFailed = report.checks.Count - checkStart - stats.checksPassed;
                        if (bridges)
                        {
                            stats.asset = assetFolder + "/Meshes/OpenArchive_" + (monster ? "LargeMonster" : "Player") + "NavMesh.asset";
                            data = SaveData(data, stats.asset, settings, selected);
                            retained = true;
                            if (!monster) playerData = data;
                        }
                        report.datasets.Add(stats);
                    }
                    finally { if (!retained && data != null) Object.DestroyImmediate(data); }
                }
            }

            if (playerData != null)
                report.playerDataAttachedToScene = AttachToScene(root.scene, playerData, out report.sceneAttachment);
            else report.sceneAttachment = "No player navigation data was generated.";
            Add(report, "Scene: player navigation data attached", report.playerDataAttachedToScene, report.sceneAttachment);
            if (!report.playerDataAttachedToScene)
                report.limitations.Add("The saved player NavMeshData needs a scene attachment or navigation surface before runtime queries can use it. " + report.sceneAttachment);
            report.limitations.Add("Navigation triangle counts are unavailable (-1): global NavMesh.CalculateTriangulation is intentionally avoided because repeated Editor bakes exposed a Unity 6 native crash. Nonempty bakes are verified with isolated NavMesh.SamplePosition and complete/direct route queries.");
            report.limitations.Add("Android hardware performance, networked play, and future monster animation/body clearance were not profiled by these Editor geometry checks.");
            report.passed = report.playerDataAttachedToScene && report.checks.All(c => c.passed) && report.datasets.Count == 4;
            AssetDatabase.SaveAssets();
            string reportPath = assetFolder + "/Previews/NavigationValidation.json";
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            AssetDatabase.ImportAsset(reportPath);
            return report;
        }

        private static NavMeshBuildSettings Settings(bool monster)
        {
            var value = NavMesh.GetSettingsByID(0);
            value.agentRadius = monster ? 2.55f : .8f;
            value.agentHeight = 5.5f;
            value.agentClimb = .7f;
            value.agentSlope = 40f;
            value.minRegionArea = 1f;
            value.overrideVoxelSize = true;
            value.voxelSize = .15f;
            value.overrideTileSize = true;
            value.tileSize = 128;
            return value;
        }

        private static List<NavMeshBuildSource> GatherSources(Transform root, Collider[] colliders, Report report)
        {
            var valid = new HashSet<int>(colliders.Select(c => c.GetInstanceID()));
            var collected = new List<NavMeshBuildSource>();
            RuntimeBuilder.CollectSources(root, ~0, NavMeshCollectGeometry.PhysicsColliders, 0,
                new List<NavMeshBuildMarkup>(), collected);
            var sources = new List<NavMeshBuildSource>();
            foreach (var original in collected)
            {
                if (original.component == null || !valid.Contains(original.component.GetInstanceID())) continue;
                var source = original;
                if (ExcludedGroups.Any(g => UnderGroup(source.component.transform, root, g)))
                {
                    source.area = 1; // Existing built-in Not Walkable area; no project-area changes.
                    report.excludedAreaSources++;
                }
                else source.area = 0;
                if (UnderGroup(source.component.transform, root, "Bridges")) report.bridgeSources++;
                sources.Add(source);
            }
            return sources;
        }

        private static NavMeshData SaveData(NavMeshData data, string path, NavMeshBuildSettings settings, List<NavMeshBuildSource> sources)
        {
            data.name = Path.GetFileNameWithoutExtension(path);
            var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
            if (existing == null) { AssetDatabase.CreateAsset(data, path); return data; }
            // Rebuild native navigation buffers through the supported API, preserving the asset GUID.
            // CopySerialized is unsafe for differently sized native mesh/navigation buffers.
            if (!RuntimeBuilder.UpdateNavMeshData(existing, settings, sources, BakeBounds))
                throw new InvalidOperationException("Unity could not update owned navigation asset: " + path);
            existing.name = data.name;
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(data);
            return existing;
        }

        private static bool AttachToScene(Scene scene, NavMeshData data, out string detail)
        {
            var previous = SceneManager.GetActiveScene();
            try
            {
                SceneManager.SetActiveScene(scene);
                var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEditor.AI.NavMeshBuilder")).FirstOrDefault(t => t != null);
                if (type == null) { detail = "The installed Editor does not expose legacy scene navigation settings."; return false; }
                var property = type.GetProperty("navMeshSettingsObject", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    ?? type.GetProperty("navMeshSettings", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                var holder = property != null ? property.GetValue(null, null) as Object : null;
                if (holder == null) { detail = "This Unity version did not expose the scene NavMesh settings object."; return false; }
                var serialized = new SerializedObject(holder);
                serialized.Update();
                var reference = serialized.FindProperty("m_NavMeshData");
                if (reference == null || reference.propertyType != SerializedPropertyType.ObjectReference)
                { detail = "Scene NavMesh settings did not expose m_NavMeshData; both baked assets were retained."; return false; }
                reference.objectReferenceValue = data;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                serialized.Update();
                bool attached = serialized.FindProperty("m_NavMeshData").objectReferenceValue == data;
                detail = attached
                    ? "Player NavMeshData assigned to this scene's legacy NavMeshSettings.m_NavMeshData. It auto-loads when the scene is saved and opened. The separately baked large-monster data is retained for future agent integration."
                    : "Unity did not retain the scene navigation reference; both baked assets were retained.";
                return attached;
            }
            catch (Exception e) { detail = "Legacy scene attachment unavailable: " + e.GetType().Name + ": " + e.Message; return false; }
            finally { if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); }
        }

        private static Transform[] FindSlots(Transform root)
        {
            var parent = root.Find("FutureStructureSlots");
            return parent == null ? Array.Empty<Transform>() : parent.Cast<Transform>().Where(t => t.name.StartsWith("StructureSlot_", StringComparison.Ordinal)).OrderBy(t => t.name).ToArray();
        }

        private static void ValidateHierarchy(Transform root, Transform[] slots, Report report)
        {
            var nest = root.Find("UniversalMonsterNest");
            Add(report, "Nest: central position", nest != null && new Vector2(nest.position.x, nest.position.z).magnitude < .05f, "UniversalMonsterNest must be at island XZ origin.");
            var entrances = nest == null ? Array.Empty<Transform>() : nest.Cast<Transform>().Where(t => t.name.StartsWith("Entrance_", StringComparison.Ordinal)).ToArray();
            Add(report, "Nest: exactly four primary entrance anchors", entrances.Length == 4 && Directions.All(d => entrances.Any(t => t.name == "Entrance_" + d)), "Required cardinal anchors: North, South, East, West.");
            if (nest != null)
            {
                for (int i = 0; i < Directions.Length; i++)
                {
                    var entrance = nest.Find("Entrance_" + Directions[i]);
                    bool correct = entrance != null && Vector3.Dot(entrance.forward, Cardinal[i]) > .99f && Vector2.Distance(new Vector2(entrance.position.x, entrance.position.z), new Vector2(Cardinal[i].x, Cardinal[i].z) * 15f) < .2f;
                    Add(report, "Nest: " + Directions[i] + " anchor position and outward facing", correct, "Anchor at radius 15, facing away from the nest.");
                }
                Add(report, "Nest: monster spawn anchor", nest.Find("MonsterSpawnPoint") != null, "Empty MonsterSpawnPoint is available for future integration.");
            }
            Add(report, "Slots: exactly ten empty anchors", slots.Length == 10 && slots.All(s => s.childCount == 0 && s.GetComponents<Component>().Length == 1), "Each future structure anchor must be an empty Transform.");
            bool spacing = slots.All(a => slots.All(b => a == b || Vector3.Distance(a.position, b.position) >= 24f));
            Add(report, "Slots: separated reserved areas", spacing, "All slot centres separated by at least 24 units, leaving space around 14 by 14 footprints.");
            var spawnArea = root.Find("PlayerSpawnArea");
            var spawns = spawnArea == null ? Array.Empty<Transform>() : spawnArea.Cast<Transform>().Where(t => t.name.StartsWith("PlayerSpawn_", StringComparison.Ordinal)).ToArray();
            Add(report, "Spawn: southern plaza and four anchors", spawnArea != null && Vector2.Distance(new Vector2(spawnArea.position.x, spawnArea.position.z), new Vector2(0, -83f)) < .5f && spawns.Length == 4, "Southern plaza centred at (0, -83), with four PlayerSpawn transforms.");
            Add(report, "Spawn: non-overlapping four-player capsules", spawns.Length == 4 && spawns.All(a => spawns.All(b => a == b || Vector3.Distance(a.position, b.position) >= 1.6f)), "Player collider diameter plus skin clearance is 1.6 units.");
        }

        private static void ValidatePhysicalLayout(Transform root, Transform[] slots, Collider[] colliders, Report report)
        {
            foreach (var slot in slots)
            {
                var bad = new List<string>();
                float lo = float.PositiveInfinity, hi = float.NegativeInfinity;
                for (int x = 0; x <= 14; x++) for (int z = 0; z <= 14; z++)
                {
                    Vector3 p = slot.position + slot.right * (x - 7f) + slot.forward * (z - 7f);
                    if (!FindSupport(root, colliders, p, out var hit)) { if (bad.Count < 3) bad.Add("unsupported " + p.ToString("F1")); continue; }
                    lo = Mathf.Min(lo, hit.point.y); hi = Mathf.Max(hi, hit.point.y);
                    if (Mathf.Abs(hit.point.y) > .25f || hit.normal.y < .999f)
                        if (bad.Count < 3) bad.Add("non-flat/obstructed " + hit.collider.name);
                }
                // An oriented physics query catches thin obstacles between the one-metre samples,
                // and continues to describe the reserved square correctly if an anchor is rotated.
                var owned = new HashSet<int>(colliders.Select(c => c.GetInstanceID()));
                var obstacles = Physics.OverlapBox(new Vector3(slot.position.x, 2.9f, slot.position.z),
                    new Vector3(7f, 2.7f, 7f), slot.rotation, ~0, QueryTriggerInteraction.Ignore);
                foreach (var c in obstacles)
                {
                    if (!owned.Contains(c.GetInstanceID()) || c.bounds.max.y <= .25f || UnderGroup(c.transform, root, "Terrain")) continue;
                    if (bad.Count < 3) bad.Add("obstacle in oriented footprint " + c.name);
                }
                bool flat = bad.Count == 0 && hi - lo <= .06f;
                Add(report, "Footprint: " + slot.name + " unobstructed flat 14x14", flat, flat ? "225 ground samples and oriented obstacle queries passed; height range " + (hi - lo).ToString("F3") + "m." : string.Join("; ", bad));
            }

            for (int i = 0; i < Cardinal.Length; i++)
            {
                var errors = new List<string>();
                Vector3 side = new Vector3(Cardinal[i].z, 0f, -Cardinal[i].x);
                for (float r = 14.5f; r <= 59f; r += .75f)
                    for (float lateral = -4.7f; lateral < 4.8f; lateral += .94f)
                    {
                        Vector3 p = Cardinal[i] * r + side * lateral;
                        if (!FindSupport(root, colliders, p, out var hit) || hit.point.y > 1.2f || hit.point.y < -.25f || hit.normal.y < .76f)
                            if (errors.Count < 3) errors.Add(p.ToString("F1"));
                    }
                Add(report, "Dry corridor: " + Directions[i] + " entrance and radial path", errors.Count == 0,
                    errors.Count == 0 ? "Ground support across 9.4m of the 10m route, from nest ramps through the main ring." : "Unsupported/obstructed samples: " + string.Join(", ", errors));
                ValidateCapsuleClearance(root, colliders, Cardinal[i], Directions[i], .8f, "Player", report);
                ValidateCapsuleClearance(root, colliders, Cardinal[i], Directions[i], 2.55f, "LargeMonster", report);
            }
            int failures = 0;
            for (int i = 0; i < 128; i++)
                for (float r = 49.3f; r <= 58.8f; r += .95f)
                {
                    float a = i * Mathf.PI * 2f / 128f;
                    var p = new Vector3(Mathf.Sin(a) * r, 0, Mathf.Cos(a) * r);
                    if (!FindSupport(root, colliders, p, out var hit) || Mathf.Abs(hit.point.y) > .25f || hit.normal.y < .95f) failures++;
                }
            Add(report, "Dry main ring: continuous full-width support", failures == 0, "128 angular sections across the 10m-wide main path; failed support samples: " + failures + ".");

            var spawnArea = root.Find("PlayerSpawnArea");
            if (spawnArea != null) foreach (Transform spawn in spawnArea)
            {
                if (!spawn.name.StartsWith("PlayerSpawn_", StringComparison.Ordinal)) continue;
                bool found = FindSupport(root, colliders, spawn.position, out var hit);
                float clearance = found ? spawn.position.y - 1f - hit.point.y : float.NaN;
                Add(report, "Spawn: " + spawn.name + " existing collider clearance", found && clearance >= .025f && clearance <= .3f,
                    "CharacterController bottom = rootY - 1; ground clearance " + clearance.ToString("F3") + "m.");
            }
        }

        private static bool FindSupport(Transform root, Collider[] colliders, Vector3 point, out RaycastHit best)
        {
            best = default;
            float closest = float.PositiveInfinity;
            // Remain above a 5.5m body on the raised nest while looking below the 7.3m arch lintels.
            // Roofs should not mask the walkable ground beneath them.
            var ray = new Ray(new Vector3(point.x, 6.6f, point.z), Vector3.down);
            foreach (var c in colliders)
            {
                if (ExcludedGroups.Any(g => UnderGroup(c.transform, root, g))) continue;
                if (point.x < c.bounds.min.x - .01f || point.x > c.bounds.max.x + .01f || point.z < c.bounds.min.z - .01f || point.z > c.bounds.max.z + .01f) continue;
                if (c.Raycast(ray, out var hit, 24f) && hit.distance < closest) { best = hit; closest = hit.distance; }
            }
            return !float.IsPositiveInfinity(closest);
        }

        private static void ValidateCapsuleClearance(Transform root, Collider[] colliders, Vector3 direction, string directionName,
            float radius, string agentName, Report report)
        {
            const float height = 5.5f;
            var owned = new HashSet<int>(colliders.Select(c => c.GetInstanceID()));
            var failures = new List<string>();
            int samples = 0;
            for (float r = 0f; r <= 59f; r += .5f)
            {
                samples++;
                Vector3 p = direction * r;
                if (!FindSupport(root, colliders, p, out var ground))
                { if (failures.Count < 3) failures.Add("no ground at r=" + r.ToString("F1")); continue; }
                if (ground.point.y < -.25f || ground.point.y > 1.2f)
                { if (failures.Count < 3) failures.Add("obstacle selected as support at r=" + r.ToString("F1")); continue; }
                float sole = ground.point.y + .04f;
                Vector3 lower = new Vector3(p.x, sole + radius, p.z);
                Vector3 upper = new Vector3(p.x, sole + height - radius, p.z);
                foreach (var obstacle in Physics.OverlapCapsule(lower, upper, radius, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (!owned.Contains(obstacle.GetInstanceID()) || obstacle.bounds.max.y <= sole + .015f) continue;
                    if (failures.Count < 3) failures.Add(obstacle.name + " at r=" + r.ToString("F1"));
                }
            }
            Add(report, "Capsule clearance: " + agentName + " " + directionName + " entrance and radial centreline", failures.Count == 0,
                failures.Count == 0 ? samples + " true capsule queries passed: radius " + radius.ToString("F2") + "m, height5.5m. Ground below the sole is ignored."
                    : string.Join("; ", failures));
        }

        private static void ValidateDataset(Transform root, Transform[] slots, NavMeshData data, int agentID, string label, Report report, Dataset stats)
        {
            var instance = NavMesh.AddNavMeshData(data, QueryOffset, Quaternion.identity);
            try
            {
                if (!instance.valid) { Add(report, label + ": temporary registration", false, "Unity rejected the isolated temporary data instance."); return; }
                // Avoid the global CalculateTriangulation native API. Repeated Unity 6 Editor bakes
                // with loaded legacy data can crash inside it; bounded queries prove this bake usable.
                stats.navigationTriangles = -1;
                var filter = new NavMeshQueryFilter { agentTypeID = agentID, areaMask = NavMesh.AllAreas };
                var nest = new Vector3(0, .8f, 0);
                var spawn = new Vector3(0, .15f, -83f);
                int supportedProbes = 0;
                var probes = new[] { nest, spawn, new Vector3(0, 0, 54), new Vector3(54, 0, 0), new Vector3(-54, 0, 0), new Vector3(0, 0, -54) };
                foreach (var point in probes)
                    if (NavMesh.SamplePosition(point + QueryOffset, out _, 1f, filter)) supportedProbes++;
                Add(report, label + ": nonempty navigation", supportedProbes == probes.Length,
                    supportedProbes + "/" + probes.Length + " independent samples found navigation at the nest, spawn, and four main-ring points. Triangle count unavailable; global triangulation is intentionally disabled.");
                for (int i = 0; i < Cardinal.Length; i++)
                {
                    Route(report, label + ": nest to " + Directions[i], nest, Cardinal[i] * 15f + Vector3.up * .8f, filter, 1f);
                    Route(report, label + ": " + Directions[i] + " independent exterior approach", Cardinal[i] * 25f, nest, filter, 1f);
                    StraightRoute(report, label + ": " + Directions[i] + " independent entrance corridor", Cardinal[i] * 25f, nest, filter, 1f);
                }
                Route(report, label + ": North through nest to South", new Vector3(0, 0, 25), new Vector3(0, 0, -25), filter, 1f, -1f, -1f, true);
                Route(report, label + ": East through nest to West", new Vector3(25, 0, 0), new Vector3(-25, 0, 0), filter, 1f, -1f, -1f, true);
                Route(report, label + ": spawn to nest", spawn, nest, filter, 1f);
                foreach (var slot in slots) Route(report, label + ": spawn to " + slot.name, spawn, new Vector3(slot.position.x, 0, slot.position.z), filter, 1f);
                if (stats.bridgesIncluded) for (int i = 0; i < 4; i++)
                {
                    float angle = (45f + 90f * i) * Mathf.Deg2Rad;
                    Vector3 direction = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
                    Vector3 midpoint = direction * 35.5f + Vector3.up * .25f;
                    StraightRoute(report, label + ": bridge " + (i + 1) + " inner bank to deck", direction * 22, midpoint, filter, 1f);
                    StraightRoute(report, label + ": bridge " + (i + 1) + " deck to outer bank", midpoint, direction * 49.5f, filter, 1f);
                }
                for (int i = 0; i < 16; i++)
                {
                    float a = i * Mathf.PI * 2 / 16;
                    float b = (i + 1) * Mathf.PI * 2 / 16;
                    var from = new Vector3(Mathf.Sin(a) * 54f, 0, Mathf.Cos(a) * 54f);
                    var to = new Vector3(Mathf.Sin(b) * 54f, 0, Mathf.Cos(b) * 54f);
                    Route(report, label + ": main ring segment " + (i + 1).ToString("00"), from, to, filter, .8f, 49f, 59f);
                }
                ValidateRingRaycasts(report, label, filter);
                // A second ring immediately outside the nest demonstrates an escape circuit without entering it.
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4f, b = (i + 1) * Mathf.PI / 4f;
                    Route(report, label + ": bypass nest segment " + (i + 1), new Vector3(Mathf.Sin(a) * 24f, 0, Mathf.Cos(a) * 24f),
                        new Vector3(Mathf.Sin(b) * 24f, 0, Mathf.Cos(b) * 24f), filter, 1f, 15.2f);
                }
                // Deep water in quadrant holes should not produce navigation; use points away from bridge centrelines.
                for (int i = 0; i < 4; i++)
                {
                    float a = (30f + i * 90f) * Mathf.Deg2Rad;
                    Vector3 p = new Vector3(Mathf.Sin(a) * 35f, 0, Mathf.Cos(a) * 35f);
                    bool found = NavMesh.SamplePosition(p + QueryOffset, out _, .65f, filter);
                    Add(report, label + ": deep water excluded quadrant " + (i + 1), !found, "Navigation absent at radius35, angle " + (30 + i * 90) + " degrees.");
                }
            }
            finally { if (instance.valid) instance.Remove(); }
        }

        private static void Route(Report report, string name, Vector3 start, Vector3 end, NavMeshQueryFilter filter, float sampleDistance,
            float minimumRadius = -1f, float maximumRadius = -1f, bool mustEnterNest = false)
        {
            bool from = NavMesh.SamplePosition(start + QueryOffset, out var a, sampleDistance, filter);
            bool to = NavMesh.SamplePosition(end + QueryOffset, out var b, sampleDistance, filter);
            if (!from || !to) { Add(report, name, false, "Missing endpoint navigation: " + (!from ? "start " : "") + (!to ? "end" : "") + "."); return; }
            var path = new NavMeshPath();
            bool connected = NavMesh.CalculatePath(a.position, b.position, filter, path) && path.status == NavMeshPathStatus.PathComplete;
            bool enteredNest = false;
            if (connected && (minimumRadius >= 0f || maximumRadius >= 0f || mustEnterNest))
            {
                // Inspect entire straight segments, not only corner vertices.
                var corners = path.corners;
                for (int i = 1; i < corners.Length && connected; i++)
                {
                    Vector3 x = corners[i - 1] - QueryOffset, y = corners[i] - QueryOffset;
                    int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(x, y)));
                    for (int s = 0; s <= steps; s++)
                    {
                        Vector3 p = Vector3.Lerp(x, y, s / (float)steps);
                        float radius = new Vector2(p.x, p.z).magnitude;
                        if (radius <= 12f) enteredNest = true;
                        if ((minimumRadius >= 0f && radius < minimumRadius) || (maximumRadius >= 0f && radius > maximumRadius))
                        { connected = false; break; }
                    }
                }
            }
            if (mustEnterNest && !enteredNest) connected = false;
            float length = 0;
            for (int i = 1; i < path.corners.Length; i++) length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            Add(report, name, connected, connected ? "Complete path, " + length.ToString("F1") + "m." + (mustEnterNest ? " Route passes within nest radius12." : "")
                : "No complete route satisfying the required nest crossing or annular bounds.");
        }

        private static void StraightRoute(Report report, string name, Vector3 start, Vector3 end, NavMeshQueryFilter filter, float sampleDistance)
        {
            bool from = NavMesh.SamplePosition(start + QueryOffset, out var a, sampleDistance, filter);
            bool to = NavMesh.SamplePosition(end + QueryOffset, out var b, sampleDistance, filter);
            var path = new NavMeshPath();
            bool clear = from && to && NavMesh.CalculatePath(a.position, b.position, filter, path) && path.status == NavMeshPathStatus.PathComplete;
            // A bounded path tests the actual traversable corridor across the ramp's height change.
            // Unity's 2D ray query can stop at a height-changing tile boundary with a zero normal
            // even when the complete path has only its two collinear endpoints.
            Vector3 line = end - start; line.y = 0;
            foreach (Vector3 corner in path.corners)
            {
                Vector3 relative = corner - QueryOffset - start; relative.y = 0;
                float along = Vector3.Dot(relative, line) / line.sqrMagnitude;
                float lateral = (relative - line * along).magnitude;
                if (along < -.03f || along > 1.03f || lateral > .5f) clear = false;
            }
            Add(report, name, clear, clear ? "Complete path remains within0.5m of this crossing centreline across height changes; an outside detour cannot satisfy this test."
                : "Missing navigation, incomplete path, or a detour outside this crossing corridor.");
        }

        private static void ValidateRingRaycasts(Report report, string label, NavMeshQueryFilter filter)
        {
            var failed = new List<int>();
            for (int i = 0; i < 128; i++)
            {
                float a = i * Mathf.PI * 2f / 128f, b = (i + 1) * Mathf.PI * 2f / 128f;
                var from = new Vector3(Mathf.Sin(a) * 54f, 0, Mathf.Cos(a) * 54f) + QueryOffset;
                var to = new Vector3(Mathf.Sin(b) * 54f, 0, Mathf.Cos(b) * 54f) + QueryOffset;
                bool foundFrom = NavMesh.SamplePosition(from, out var x, .6f, filter);
                bool foundTo = NavMesh.SamplePosition(to, out var y, .6f, filter);
                if (!foundFrom || !foundTo || NavMesh.Raycast(x.position, y.position, out _, filter)) failed.Add(i + 1);
            }
            Add(report, label + ": 128 direct main-ring segments", failed.Count == 0,
                failed.Count == 0 ? "All short NavMesh rays connect along radius54, within the49–59m annulus. No outside detour is accepted."
                    : "Blocked ring segments: " + string.Join(",", failed.Take(12)) + (failed.Count > 12 ? "..." : ""));
        }

        private static bool UnderGroup(Transform candidate, Transform root, string group)
        {
            for (var t = candidate; t != null && t != root; t = t.parent) if (t.name == group) return true;
            return false;
        }

        private static void Add(Report report, string name, bool passed, string detail)
        { report.checks.Add(new Check { name = name, passed = passed, detail = detail }); }
    }
}
