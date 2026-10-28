#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Scene-only garden dressing. Never edits imported models or gameplay components.</summary>
public static class DressFunctionsGarden
{
    const string Folder="Assets/Materials/FunctionsRelations/Environment";
    const string Nature="Assets/CHEATCODE/SimpleNaturePack/Prefabs/";
    public const string RootName="GardenAtmosphereDecorations";
    public static void Apply()
    {
        var scene=SceneManager.GetActiveScene();
        if(EditorApplication.isPlayingOrWillChangePlaymode||scene.name!="Functions and Relations")
            throw new InvalidOperationException("Open Functions and Relations in Edit Mode.");
        if(GameObject.Find(RootName)!=null)throw new InvalidOperationException("Garden dressing already exists; inspect it before rebuilding.");
        var manager=UnityEngine.Object.FindFirstObjectByType<FunctionsRelationsStageManager>();
        string gameplay=EditorJsonUtility.ToJson(manager);
        var tower=GameObject.Find("Border").transform.Find("Tower").GetComponent<Renderer>();
        var stone=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/RuinsMapBorder/Materials/Border_Stone.mat");
        tower.sharedMaterials=Enumerable.Repeat(stone,tower.sharedMaterials.Length).ToArray();
        PrefabUtility.RecordPrefabInstancePropertyModifications(tower);
        // Leave nearby teaching content unaffected; only distant architecture enters the haze.
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;
        RenderSettings.fogColor=new Color(.40f,.49f,.55f);RenderSettings.fogStartDistance=90;RenderSettings.fogEndDistance=245;

        var root=new GameObject(RootName).transform;
        var lamps=new GameObject("Warm Trail Lamps").transform;lamps.SetParent(root,false);
        var plants=new GameObject("Border Meadow Patches").transform;plants.SetParent(root,false);
        var stage=GameObject.Find("Stage");
        var source=FunctionsRelationsWorldSetup.CopyMesh(stage.GetComponent<MeshFilter>().sharedMesh);
        // Sample terrain triangles only, ignoring floating arches, foliage and the repaired stone path.
        var groundMesh=new Mesh{indexFormat=IndexFormat.UInt32};groundMesh.vertices=source.vertices;
        groundMesh.SetTriangles(source.GetTriangles(0),0);groundMesh.RecalculateBounds();
        var probe=new GameObject("Temporary Terrain Sampler").AddComponent<MeshCollider>();
        probe.transform.SetPositionAndRotation(stage.transform.position,stage.transform.rotation);probe.transform.localScale=stage.transform.lossyScale;probe.sharedMesh=groundMesh;
        try
        {
            Physics.SyncTransforms();
            var lampAsset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FINAL_ASSETS/STREETLAMP.fbx");
            var core=lampAsset.GetComponentInChildren<Renderer>().sharedMaterials.First(m=>m.name=="Lamp Core");
            var glow=CloneMaterial(core,"Garden_Lamp_Core");
            glow.EnableKeyword("_EMISSION");glow.SetColor("_EmissionColor",new Color(1f,.65f,.23f)*1.8f);
            glow.SetFloat("_Smoothness",.12f);
            var locations=new[]{new Vector2(-232,875),new Vector2(-145,875),new Vector2(-235,906),new Vector2(-142,906),
                new Vector2(-226,940),new Vector2(-151,940),new Vector2(-210,870),new Vector2(-168,870)};
            int number=0;
            foreach(var point in locations)
            {
                var ground=Ground(probe,point);
                var lamp=(GameObject)PrefabUtility.InstantiatePrefab(lampAsset,lamps);
                lamp.name="Trail Lamp "+(++number);Fit(lamp.transform,ground,6.2f);
                var renderer=lamp.GetComponentInChildren<Renderer>();
                renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>m.name=="Lamp Core"?glow:m).ToArray();
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                ArtOnly(lamp);
                var light=new GameObject("Warm Ground Pool").AddComponent<Light>();light.transform.SetParent(lamp.transform,true);
                light.transform.position=ground+Vector3.up*5.4f;light.transform.rotation=Quaternion.Euler(90,0,0);
                light.type=LightType.Spot;light.color=new Color(1f,.73f,.39f);light.intensity=20;
                light.range=17;light.spotAngle=112;light.innerSpotAngle=65;light.shadows=LightShadows.None;
                light.renderMode=LightRenderMode.ForcePixel;light.bounceIntensity=0;
            }
            var atlas=CloneMaterial(AssetDatabase.LoadAssetAtPath<Material>("Assets/CHEATCODE/SimpleNaturePack/Materials/SimpleNaturePack_Texture_01.mat"),"Garden_Foliage_Instanced");
            atlas.enableInstancing=true;
            var random=new System.Random(8419);int count=0;
            for(int x=-240;x<=-138;x+=6)for(int z=860;z<=948;z+=6)
            {
                if(x>-230&&x<-148&&z>866&&z<938)continue;
                var point=new Vector2(x+(float)random.NextDouble()*2,z+(float)random.NextDouble()*2);
                if(NearGameplay(point,manager))continue;
                Vector3 bottom=Ground(probe,point);
                if(Physics.OverlapSphere(bottom+Vector3.up*.65f,.9f,~0,QueryTriggerInteraction.Ignore)
                    .Any(c=>c!=probe&&c is BoxCollider&&c.enabled))continue;
                string kind=count%5==0?"Mushroom_0"+(1+count%2):count%3==0?"Flowers_0"+(1+count%2):"Grass_0"+(1+count%2);
                var art=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Nature+kind+".prefab"),plants);
                art.name=kind+" Meadow "+(++count);art.transform.rotation=Quaternion.Euler(0,random.Next(360),0);
                float height=kind.StartsWith("Mushroom")?.45f:kind.StartsWith("Flowers")?.85f:.7f;
                Fit(art.transform,bottom,height*(.8f+(float)random.NextDouble()*.4f));ArtOnly(art);
                foreach(var r in art.GetComponentsInChildren<Renderer>(true))
                {
                    r.sharedMaterials=Enumerable.Repeat(atlas,r.sharedMaterials.Length).ToArray();r.shadowCastingMode=ShadowCastingMode.Off;
                    r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                }
            }
        }
        finally{UnityEngine.Object.DestroyImmediate(probe.gameObject);UnityEngine.Object.DestroyImmediate(groundMesh);UnityEngine.Object.DestroyImmediate(source);}
        AddEntryMeadows();
        if(EditorJsonUtility.ToJson(manager)!=gameplay)throw new InvalidOperationException("Gameplay data changed.");
        Verify();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    }
    static bool NearGameplay(Vector2 p,FunctionsRelationsStageManager manager)
    {
        var targets=new[]{manager.DomainTarget,manager.RangeTarget,GameObject.Find("Book_Statue").transform,
            GameObject.Find("InformationBoard").transform,GameObject.Find("FR_SpawnPoint").transform};
        return targets.Any(t=>Vector2.Distance(p,new Vector2(t.position.x,t.position.z))<8);
    }
    public static void AddEntryMeadows()
    {
        var root=GameObject.Find(RootName).transform;
        if(root.Find("Entry Flower Beds")!=null)return;
        var beds=new GameObject("Entry Flower Beds").transform;beds.SetParent(root,false);
        var terrain=GameObject.Find("Stage").GetComponent<Collider>();
        var atlas=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Garden_Foliage_Instanced.mat");
        int index=0;
        // Small, deliberately visible beds beside the entry route, leaving its center completely open.
        foreach(float x in new[]{-201f,-176f})foreach(float z in new[]{862f,866f})
        foreach(string kind in new[]{"Grass_01","Flowers_02","Mushroom_01"})
        {
            float offset=kind=="Grass_01"?-1:kind=="Flowers_02"?0:1;
            Vector3 bottom=Ground(terrain,new Vector2(x+offset,z));
            var art=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Nature+kind+".prefab"),beds);
            art.name=kind+" Entry "+(++index);art.transform.rotation=Quaternion.Euler(0,index*47,0);
            Fit(art.transform,bottom,kind=="Mushroom_01"?.55f:.8f);ArtOnly(art);
            foreach(var r in art.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterials=Enumerable.Repeat(atlas,r.sharedMaterials.Length).ToArray();r.shadowCastingMode=ShadowCastingMode.Off;
                r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            }
        }
    }
    static Material CloneMaterial(Material source,string name)
    {
        string path=Folder+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null){material=new Material(source){name=name};AssetDatabase.CreateAsset(material,path);}return material;
    }
    static Vector3 Ground(Collider probe,Vector2 point)
    {
        RaycastHit hit;if(!probe.Raycast(new Ray(new Vector3(point.x,60,point.y),Vector3.down),out hit,100))
            throw new InvalidOperationException("No terrain beneath decoration at "+point);return hit.point+Vector3.down*.015f;
    }
    static void Fit(Transform t,Vector3 bottom,float height)
    {
        var b=ShareProofChamberDecorations.BoundsOf(t);t.localScale*=height/b.size.y;
        b=ShareProofChamberDecorations.BoundsOf(t);t.position+=bottom-new Vector3(b.center.x,b.min.y,b.center.z);
        PrefabUtility.RecordPrefabInstancePropertyModifications(t);
    }
    static void ArtOnly(GameObject go)
    {
        foreach(var c in go.GetComponentsInChildren<Collider>(true)){c.enabled=false;PrefabUtility.RecordPrefabInstancePropertyModifications(c);}
        foreach(var r in go.GetComponentsInChildren<Renderer>(true))GameObjectUtility.SetStaticEditorFlags(r.gameObject,StaticEditorFlags.BatchingStatic|StaticEditorFlags.OccludeeStatic);
    }
    public static void Verify()
    {
        var root=GameObject.Find(RootName);var renderers=root.GetComponentsInChildren<Renderer>(true);
        if(root.GetComponentsInChildren<Collider>(true).Any(c=>c.enabled)||root.GetComponentsInChildren<MonoBehaviour>(true).Length!=0)
            throw new InvalidOperationException("Dressing must contain no gameplay logic or active collisions.");
        foreach(var r in renderers)foreach(var m in r.sharedMaterials)if(m==null||m.shader==null||!m.shader.isSupported)
            throw new InvalidOperationException("Unsupported material on "+r.name);
        var lights=root.GetComponentsInChildren<Light>();if(lights.Length!=8||lights.Any(l=>l.shadows!=LightShadows.None))
            throw new InvalidOperationException("Expected eight inexpensive, shadow-free ground lights.");
        Directory.CreateDirectory("Library/FunctionsEnvironment");
        File.WriteAllText("Library/FunctionsEnvironment/Setup.txt","Tower uses existing URP Border_Stone. Linear fog: 90–245 m.\n"+
            "Decorations: "+root.name+"\nFoliage clusters: "+(root.transform.Find("Border Meadow Patches").childCount+(root.transform.Find("Entry Flower Beds")!=null?root.transform.Find("Entry Flower Beds").childCount:0))+"\nRenderers including LODs: "+renderers.Length+
            "\nEight functional spotlights; no real-time shadows, colliders, particles or gameplay scripts. Source materials and prefabs untouched.\n");
    }
    public static void Screenshot(string file,Vector3 position,Vector3 target,float fov=60)
    {
        var go=new GameObject("Temporary Garden Camera");var cam=go.AddComponent<Camera>();
        cam.transform.position=position;cam.transform.LookAt(target);cam.fieldOfView=fov;cam.farClipPlane=350;
        var rt=new RenderTexture(1280,800,24);var previous=RenderTexture.active;
        try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;var tex=new Texture2D(1280,800,TextureFormat.RGB24,false);
            tex.ReadPixels(new Rect(0,0,1280,800),0,0);tex.Apply();File.WriteAllBytes("Library/FunctionsEnvironment/"+file+".png",tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);}
        finally{RenderTexture.active=previous;cam.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
    }
}
#endif
