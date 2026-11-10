#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using LogicLegends.Graph;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Unity.Collections;

/// <summary>Idempotent scene-local lighting; original FBX, materials and shared prefabs stay intact.</summary>
public static class GraphAtmosphereSetup
{
    const string MaterialFolder="Assets/Materials/GraphAtmosphere";

    [MenuItem("Logic Legends/Graph/Apply Chamber Atmosphere")]
    public static void Apply()
    {
        var scene=SceneManager.GetActiveScene();
        if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/Graph.unity")
            throw new InvalidOperationException("Open Graph in Edit Mode before applying its atmosphere.");
        Configure(scene);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    public static string Configure(Scene scene)
    {
        if(scene.path!="Assets/Scenes/Graph.unity"||SceneManager.GetActiveScene()!=scene)
            throw new InvalidOperationException("Graph must be the active scene so its own RenderSettings are edited.");
        var roots=scene.GetRootGameObjects();
        var chamber=roots.FirstOrDefault(r=>r.name=="Chamber1");
        if(chamber==null)throw new InvalidOperationException("Graph chamber model missing.");
        var all=roots.SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
        var holder=roots.FirstOrDefault(r=>r.name=="Graph Chamber Atmosphere");
        if(holder==null){holder=new GameObject("Graph Chamber Atmosphere");SceneManager.MoveGameObjectToScene(holder,scene);}
        var effect=holder.GetComponent<GraphChamberAtmosphere>();
        if(effect==null)effect=holder.AddComponent<GraphChamberAtmosphere>();

        var existing=all.Select(t=>t.GetComponent<Light>()).Where(l=>l!=null).ToArray();
        var torches=all.Where(t=>t.gameObject.activeInHierarchy&&t.GetComponent<Light>()==null&&
            t.name.IndexOf("torch",StringComparison.OrdinalIgnoreCase)>=0&&t.GetComponentsInChildren<Renderer>().Length>0)
            .Where(t=>!Ancestors(t).Any(a=>a.name.IndexOf("torch",StringComparison.OrdinalIgnoreCase)>=0)).ToArray();
        var warm=new List<Light>();
        foreach(var torch in torches)
        {
            var bounds=TutorialWorldBounds.Stable(torch);
            var point=new Vector3(bounds.center.x,Mathf.Lerp(bounds.min.y,bounds.max.y,.82f),bounds.center.z);
            // Prefer the prefab's authored flame-position light over a second nearby pool.
            var light=torch.GetComponentsInChildren<Light>(true).FirstOrDefault(l=>l.type==LightType.Point||l.type==LightType.Spot);
            if(light!=null)point=light.transform.position;
            else light=existing.Where(l=>!warm.Contains(l)&&(l.type==LightType.Point||l.type==LightType.Spot)&&l.name.IndexOf("torch",StringComparison.OrdinalIgnoreCase)>=0)
                .OrderBy(l=>Vector3.SqrMagnitude(l.transform.position-point)).FirstOrDefault();
            if(light!=null&&Vector3.Distance(light.transform.position,point)>12)light=null;
            if(light==null)light=Fixture(holder.transform,"Torch Pool "+warm.Count,point,new Color(1,.62f,.28f),effect.torchIntensity,effect.torchRange);
            else
            {
                light.transform.position=point;
                ConfigureLight(light,new Color(1,.62f,.28f),effect.torchIntensity,effect.torchRange);
                PrefabUtility.RecordPrefabInstancePropertyModifications(light.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(light);
            }
            var inward=new Vector3(-point.x,0,40-point.z).normalized;
            light.type=LightType.Spot;light.spotAngle=100;light.innerSpotAngle=65;
            light.transform.rotation=Quaternion.LookRotation(Vector3.down+inward*.35f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(light.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(light);
            warm.Add(light);
        }
        // The old chamber also had separate warm lights beside already-lit torch prefabs.
        // Retain those objects, but avoid illuminating each fixture twice.
        foreach(var light in existing.Where(l=>l.gameObject.activeInHierarchy&&l.name=="Torch warm light"&&!warm.Contains(l)))
        {
            if(torches.Length>0){light.enabled=false;EditorUtility.SetDirty(light);continue;}
            ConfigureLight(light,new Color(1,.62f,.28f),effect.torchIntensity,effect.torchRange);warm.Add(light);
        }
        effect.torchLights=warm.ToArray();

        System.IO.Directory.CreateDirectory(MaterialFolder);AssetDatabase.Refresh();
        var surfaces=new List<GraphChamberAtmosphere.CrystalSurface>();
        var blueLights=new List<Light>();
        var positions=new List<Vector3>();
        foreach(var renderer in all.Select(t=>t.GetComponent<MeshRenderer>()).Where(r=>r!=null&&r.gameObject.activeInHierarchy))
        {
            var materials=renderer.sharedMaterials;bool changed=false;
            for(int slot=0;slot<materials.Length;slot++)
            {
                var source=materials[slot];if(source==null)continue;
                string path=MaterialFolder+"/BlueCrystalGlow.mat";
                if(source.name.IndexOf("GraphChamber_CrystalGlow",StringComparison.OrdinalIgnoreCase)<0&&AssetDatabase.GetAssetPath(source)!=path)continue;
                var glow=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(glow==null)
                {
                    glow=new Material(source){name="GraphChamber_CrystalGlow"};
                    if(!glow.HasProperty("_EmissionColor"))
                    {
                        var template=Resources.Load<Material>("Settings/SoloOpaqueLit");
                        if(template==null)throw new InvalidOperationException("Existing SoloOpaqueLit template missing.");
                        glow.shader=template.shader;
                    }
                    glow.EnableKeyword("_EMISSION");glow.SetColor("_EmissionColor",new Color(.025f,.55f,1.1f));
                    if(glow.HasProperty("_Smoothness"))glow.SetFloat("_Smoothness",.28f);
                    AssetDatabase.CreateAsset(glow,path);
                }
                materials[slot]=glow;changed=true;
                surfaces.Add(new GraphChamberAtmosphere.CrystalSurface{renderer=renderer,materialIndex=slot});
                // Chamber1 combines the entire room. Its renderer bounds cannot
                // locate a crystal; read only the blue submesh's connected pieces.
                var clusters=new List<Bounds>();
                foreach(var piece in CrystalPieces(renderer,slot))
                {
                    int group=clusters.FindIndex(b=>Vector3.Distance(b.center,piece.center)<12);
                    if(group<0)clusters.Add(piece);
                    else{var bounds=clusters[group];bounds.Encapsulate(piece);clusters[group]=bounds;}
                }
                foreach(var cluster in clusters)
                    positions.Add(new Vector3(cluster.center.x,Mathf.Lerp(cluster.min.y,cluster.max.y,.25f),cluster.center.z));
            }
            if(changed){renderer.sharedMaterials=materials;PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);}
        }
        for(int i=0;i<positions.Count;i++)
        {
            string name="Blue Crystal Pool "+i;var child=holder.transform.Find(name);
            var light=child==null?Fixture(holder.transform,name,positions[i],new Color(.13f,.43f,1),effect.crystalIntensity,effect.crystalRange):child.GetComponent<Light>();
            light.transform.position=positions[i];ConfigureLight(light,new Color(.13f,.43f,1),effect.crystalIntensity,effect.crystalRange);blueLights.Add(light);
        }
        if(surfaces.Count==0)throw new InvalidOperationException("No Graph crystal submesh found. Preserve the existing effect references and inspect the chamber material assignments.");
        effect.crystalLights=blueLights.ToArray();effect.crystalSurfaces=surfaces.ToArray();

        const string bloomPath=MaterialFolder+"/GraphCrystalBloom.asset";
        var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(bloomPath);
        if(profile==null){profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,bloomPath);}
        if(!profile.TryGet<Bloom>(out var bloom))
        {bloom=profile.Add<Bloom>(true);AssetDatabase.AddObjectToAsset(bloom,profile);}
        bloom.intensity.Override(.10f);bloom.threshold.Override(.9f);bloom.scatter.Override(.4f);
        bloom.clamp.Override(1.5f);bloom.highQualityFiltering.Override(false);
        bloom.downscale.Override(BloomDownscaleMode.Quarter);bloom.maxIterations.Override(4);
        var glowObject=holder.transform.Find("Restrained Crystal Bloom");
        if(glowObject==null){glowObject=new GameObject("Restrained Crystal Bloom").transform;glowObject.SetParent(holder.transform,false);}
        glowObject.gameObject.layer=LayerMask.NameToLayer("Ignore Raycast");
        var volume=glowObject.GetComponent<Volume>();if(volume==null)volume=glowObject.gameObject.AddComponent<Volume>();
        volume.isGlobal=true;volume.priority=20;volume.sharedProfile=profile;effect.bloomVolume=volume;
        EditorUtility.SetDirty(bloom);EditorUtility.SetDirty(profile);

        RenderSettings.ambientMode=AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=new Color(.37f,.41f,.49f);
        RenderSettings.ambientEquatorColor=new Color(.26f,.30f,.37f);
        RenderSettings.ambientGroundColor=new Color(.17f,.19f,.25f);
        RenderSettings.reflectionIntensity=.45f;
        foreach(var light in existing.Where(l=>l.type==LightType.Directional))
        {light.intensity=.60f;light.color=new Color(.75f,.83f,1);EditorUtility.SetDirty(light);}
        EditorUtility.SetDirty(effect);AssetDatabase.SaveAssets();
        return "Graph atmosphere: "+effect.torchLights.Length+" warm fixtures, "+surfaces.Count+" emissive crystal surfaces, "+blueLights.Count+" blue pools; shadowless local lights; ambient gradient preserved for overhead readability.";
    }
    static IEnumerable<Transform> Ancestors(Transform t){for(var p=t.parent;p!=null;p=p.parent)yield return p;}
    public static List<Bounds> CrystalPieces(MeshRenderer renderer,int slot)
    {
        var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;
        using(var data=MeshUtility.AcquireReadOnlyMeshData(mesh))
        using(var vertices=new NativeArray<Vector3>(mesh.vertexCount,Allocator.Temp))
        using(var indices=new NativeArray<int>(data[0].GetSubMesh(slot).indexCount,Allocator.Temp))
        {
            data[0].GetVertices(vertices);data[0].GetIndices(indices,slot,true);
            var parent=Enumerable.Range(0,vertices.Length).ToArray();
            var weld=new Dictionary<Vector3Int,int>();var used=new HashSet<int>();
            int Root(int index){while(parent[index]!=index){parent[index]=parent[parent[index]];index=parent[index];}return index;}
            void Join(int a,int b){parent[Root(a)]=Root(b);}
            foreach(int index in indices)
            {
                used.Add(index);var key=Vector3Int.RoundToInt(vertices[index]*10000);
                if(weld.TryGetValue(key,out int other))Join(index,other);else weld.Add(key,index);
            }
            for(int i=0;i+2<indices.Length;i+=3){Join(indices[i],indices[i+1]);Join(indices[i],indices[i+2]);}
            var groups=new Dictionary<int,Bounds>();
            foreach(int index in used)
            {
                int root=Root(index);var point=renderer.transform.TransformPoint(vertices[index]);
                if(groups.TryGetValue(root,out var bounds)){bounds.Encapsulate(point);groups[root]=bounds;}
                else groups.Add(root,new Bounds(point,Vector3.zero));
            }
            return groups.Values.OrderBy(b=>b.center.x).ThenBy(b=>b.center.z).ToList();
        }
    }
    static Light Fixture(Transform parent,string name,Vector3 point,Color color,float intensity,float range)
    {
        var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.position=point;
        var light=go.AddComponent<Light>();ConfigureLight(light,color,intensity,range);return light;
    }
    static void ConfigureLight(Light light,Color color,float intensity,float range)
    {
        light.enabled=true;light.type=LightType.Point;light.color=color;light.intensity=intensity;light.range=range;
        light.shadows=LightShadows.None;light.lightmapBakeType=LightmapBakeType.Realtime;
        light.renderMode=LightRenderMode.Auto;EditorUtility.SetDirty(light);
    }
}
#endif
