using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LogicLegends.OpenArchive.Hunting;
using LogicLegends.OpenArchive.Water;
using LogicLegends.Tests.Khezu.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace OpenArchiveAuthoring
{
    public static class OpenArchiveWaterSetup
    {
        const string Folder = OpenArchiveBuilder.Folder;
        const string Effects = Folder + "/Water/Assets";
        public const string Reports = Folder + "/Testing/Water/Reports";
        [Serializable] public class Report {
            public string checkedUtc;
            public List<string> savedScenes = new List<string>();
            public float slowdown = .85f, playerHeight = 5.5f, shallowBedWorldDepth = 3;
            public string geometry = "Riverbeds follow the original four water polygons. Sloped shelves lead to jumpable banks; deeper beds provide 0.8m capsule headroom under existing bridges. Existing bridge meshes, heights, railings and path clearances retained.";
            public bool sourceNavigationPassed;
        }
        [MenuItem("Tools/Logic Legends/Open Archive/Set Up Walkable Water And Splashes")]
        public static void Apply()
        {
            OpenArchiveImprovements.RequireSavedEditor();
            Directory.CreateDirectory(Effects); Directory.CreateDirectory(Reports); AssetDatabase.Refresh();
            var active = SceneManager.GetActiveScene().path;
            var splash = BuildSplash();
            var impact = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Assets/Sounds Effects/Small Sound Kit/Magic/Mgc_Water_Impact_01.wav");
            var step = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Assets/Sounds Effects/Small Sound Kit/Magic/Mgc_Water_Hit_Short_01.wav");
            if (impact == null || step == null) throw new InvalidOperationException("Existing water audio is missing.");
            var paths = new[] { OpenArchiveBuilder.ScenePath, Folder + "/Scenes/OpenArchive_Explore.unity",
                Folder + "/Testing/KhezuAwakening/OpenArchive_KhezuAwakening_Test.unity", KhezuHuntingSetup.ScenePath,
                Folder + "/Testing/KhezuCinematic/OpenArchive_KhezuCinematic_Test.unity" };
            var report = new Report {checkedUtc = DateTime.UtcNow.ToString("o")};
            try
            {
                foreach (var path in paths)
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    var root = scene.GetRootGameObjects().Single(g => g.name == "OpenArchive").transform;
                    float scale = root.lossyScale.y;
                    var surfaces = root.GetComponentsInChildren<MeshFilter>().Where(f => f.name.StartsWith("Turquoise_RiverPond_", StringComparison.Ordinal)).OrderBy(f => f.name).ToArray();
                    if (surfaces.Length != 4) throw new InvalidOperationException("Expected four existing water surfaces.");
                    var waterRoot = root.Find("Environment/Rivers");
                    var t = waterRoot.Find("Water_Interactions");
                    if (t == null) { var go = new GameObject("Water_Interactions"); go.transform.SetParent(waterRoot, false); t = go.transform; }
                    var water = t.GetComponent<OpenArchiveWater>(); if (water == null) water = t.gameObject.AddComponent<OpenArchiveWater>();
                    water.surfaces = surfaces; water.splashPrefab = splash; water.landingSound = impact; water.wadingSound = step; water.movementMultiplier = .85f;
                    OpenArchiveWaterSinkingSetup.Configure(water, root);
                    foreach (var surface in surfaces)
                    {
                        string name = "Walkable_Riverbed_" + surface.name.Substring(surface.name.Length - 1);
                        var child = t.Find(name);
                        if (child == null) { var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider)); go.transform.SetParent(t, false); child = go.transform; }
                        var mesh = Bed(surface.sharedMesh, scale, name + "_S" + Mathf.RoundToInt(scale * 100));
                        child.GetComponent<MeshFilter>().sharedMesh = mesh;
                        child.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/Archive_Atlas.mat");
                        child.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                        child.GetComponent<MeshCollider>().sharedMesh = null; child.GetComponent<MeshCollider>().sharedMesh = mesh;
                    }
                    // The existing visible bank walls also stop walking underneath the island's thin ground mesh.
                    var bank = root.Find("Environment/Rocks/Riverbank_CutStone");
                    var bankCollider = bank.GetComponent<MeshCollider>(); if (bankCollider == null) bankCollider = bank.gameObject.AddComponent<MeshCollider>();
                    bankCollider.sharedMesh = bank.GetComponent<MeshFilter>().sharedMesh;
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (path == OpenArchiveBuilder.ScenePath)
                        report.sourceNavigationPassed = OpenArchiveNavigation.BuildAndValidate(root.gameObject, Folder).passed;
                    if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save " + path);
                    report.savedScenes.Add(path);
                }
                var current = EditorSceneManager.OpenScene(paths[4], OpenSceneMode.Single);
                var monster = Object.FindFirstObjectByType<KhezuHuntingController>();
                Object.FindFirstObjectByType<KhezuNavigationData>().data = KhezuHuntingSetup.Bake(GameObject.Find("OpenArchive"), monster.Agent);
                EditorSceneManager.SaveScene(current); AssetDatabase.SaveAssets();
            }
            finally { if (!string.IsNullOrEmpty(active) && SceneManager.GetActiveScene().path != active) EditorSceneManager.OpenScene(active, OpenSceneMode.Single); }
            File.WriteAllText(Reports + "/WaterSetup.json", JsonUtility.ToJson(report, true));
        }
        sealed class Edge { public Vector3 a, b; public int count; }
        static string Key(Vector3 p) => Mathf.RoundToInt(p.x * 10000) + "," + Mathf.RoundToInt(p.z * 10000);
        static Mesh Bed(Mesh surface, float scale, string name)
        {
            var v = surface.vertices; var triangles = surface.triangles;
            var edges = new Dictionary<string, Edge>();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                var a = v[triangles[i]]; var b = v[triangles[i + 1]]; var c = v[triangles[i + 2]];
                if (Vector3.Cross(b - a, c - a).sqrMagnitude < .0000001f) continue;
                foreach (var pair in new[] {new[] {a,b}, new[] {b,c}, new[] {c,a}})
                {
                    string ka = Key(pair[0]), kb = Key(pair[1]);
                    string key = string.CompareOrdinal(ka,kb) < 0 ? ka + "|" + kb : kb + "|" + ka;
                    Edge edge; if (!edges.TryGetValue(key, out edge)) edges[key] = edge = new Edge {a = pair[0], b = pair[1]}; edge.count++;
                }
            }
            var boundary = edges.Values.Where(e => e.count == 1).ToArray();
            float shallow = 3 / scale, deep = 6.3f / scale + .21f, slopeWidth = (deep - shallow) * 1.6f;
            Func<Vector3, Vector3> lower = p => {
                float distance = float.MaxValue;
                foreach (var edge in boundary) {
                    var line = edge.b - edge.a; var offset = p - edge.a; line.y = offset.y = 0;
                    float along = Mathf.Clamp01(Vector3.Dot(offset, line) / Mathf.Max(.000001f, line.sqrMagnitude));
                    distance = Mathf.Min(distance, (offset - line * along).magnitude);
                }
                p.y = -Mathf.Lerp(shallow, deep, Mathf.SmoothStep(0, 1, Mathf.Clamp01(distance / slopeWidth))); return p;
            };
            var vertices = new List<Vector3>(); var indices = new List<int>(); var uv = new List<Vector2>();
            Action<Vector3,Vector3,Vector3,int> add = null;
            add = (a,b,c,levels) => {
                if (Vector3.Cross(b-a,c-a).sqrMagnitude < .0000001f) return;
                if (levels > 0) { var ab = (a+b)*.5f; var bc = (b+c)*.5f; var ca = (c+a)*.5f;
                    add(a,ab,ca,levels-1); add(ab,b,bc,levels-1); add(ca,bc,c,levels-1); add(ab,bc,ca,levels-1); return; }
                int start = vertices.Count; vertices.Add(lower(a)); vertices.Add(lower(b)); vertices.Add(lower(c));
                indices.AddRange(new[] {start,start+1,start+2});
                for (int j = 0; j < 3; j++) uv.Add(new Vector2(.5625f, .0625f));
            };
            for (int i = 0; i < triangles.Length; i += 3) add(v[triangles[i]],v[triangles[i+1]],v[triangles[i+2]],2);
            var path = Effects + "/" + name + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { mesh = new Mesh {name = name}; AssetDatabase.CreateAsset(mesh,path); }
            mesh.Clear(); mesh.indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices); mesh.SetUVs(0,uv); mesh.SetTriangles(indices,0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh); return mesh;
        }
        static Material Material(string name, string shaderName, bool transparent)
        {
            var shader = Shader.Find(shaderName); if (shader == null) throw new InvalidOperationException("Missing " + shaderName);
            var path = Effects + "/" + name + ".mat"; var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material,path); }
            material.shader = shader; material.SetColor("_BaseColor",Color.white);
            if (transparent) {
                material.SetFloat("_Surface",1); material.SetFloat("_SrcBlend",5); material.SetFloat("_DstBlend",10); material.SetFloat("_ZWrite",0);
                material.SetFloat("_Cull",0); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.SetOverrideTag("RenderType","Transparent"); material.renderQueue = 3000;
            }
            EditorUtility.SetDirty(material); return material;
        }
        static OpenArchiveSplash BuildSplash()
        {
            var dropletMat = Material("Water_Droplets","Universal Render Pipeline/Particles/Unlit",true);
            // The particle shader reads LineRenderer vertex alpha; ordinary URP Unlit does not.
            var rippleMat = Material("Water_Ripples","Universal Render Pipeline/Particles/Unlit",true);
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Effects + "/Water_Droplet.asset");
            if (mesh == null) {
                mesh = new Mesh {name="LowPoly_Water_Droplet"};
                mesh.vertices = new[] {Vector3.up*.6f,Vector3.down*.5f,Vector3.right*.4f,Vector3.left*.4f,Vector3.forward*.4f,Vector3.back*.4f};
                mesh.triangles = new[] {0,4,2,0,2,5,0,5,3,0,3,4,1,2,4,1,5,2,1,3,5,1,4,3}; mesh.RecalculateNormals(); mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh,Effects + "/Water_Droplet.asset");
            }
            var go = new GameObject("OpenArchive_WaterSplash");
            try {
                var effect = go.AddComponent<OpenArchiveSplash>();
                var particles = new GameObject("LowPoly_Droplets"); particles.transform.SetParent(go.transform,false);
                var ps = particles.AddComponent<ParticleSystem>(); ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main; main.loop = false; main.playOnAwake = false; main.duration = 1; main.maxParticles = 48;
                main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = 1.2f; main.startLifetime = .8f;
                var emission = ps.emission; emission.enabled = false;
                var color = ps.colorOverLifetime; color.enabled = true;
                var gradient = new Gradient(); gradient.SetKeys(new[] {new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[] {new GradientAlphaKey(1,0),new GradientAlphaKey(1,.6f),new GradientAlphaKey(0,1)}); color.color = gradient;
                var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.renderMode = ParticleSystemRenderMode.Mesh; renderer.mesh = mesh; renderer.sharedMaterial = dropletMat; renderer.shadowCastingMode = ShadowCastingMode.Off;
                var ring = new GameObject("Surface_Ripple"); ring.transform.SetParent(go.transform,false);
                var line = ring.AddComponent<LineRenderer>(); line.sharedMaterial = rippleMat; line.useWorldSpace = false; line.loop = true;
                line.positionCount = 48; line.alignment = LineAlignment.View; line.widthMultiplier = .1f; line.enabled = false; line.shadowCastingMode = ShadowCastingMode.Off;
                var source = go.AddComponent<AudioSource>(); source.playOnAwake = false; source.spatialBlend = .65f; source.dopplerLevel = 0;
                source.minDistance = 8; source.maxDistance = 45; source.rolloffMode = AudioRolloffMode.Linear; AudioVolumeSettings.Route(source,GameAudioChannel.SoundFX);
                effect.droplets = ps; effect.ripple = line; effect.sound = source;
                return PrefabUtility.SaveAsPrefabAsset(go,Effects + "/OpenArchive_WaterSplash.prefab").GetComponent<OpenArchiveSplash>();
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
