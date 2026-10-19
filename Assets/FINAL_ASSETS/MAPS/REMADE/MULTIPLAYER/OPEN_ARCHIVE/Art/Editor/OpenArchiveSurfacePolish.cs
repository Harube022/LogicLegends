using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace OpenArchiveAuthoring
{
    /// <summary>Render-only overlap repair and native VFX. No gameplay components or collision changes.</summary>
    public static class OpenArchiveSurfacePolish
    {
        const string Map=OpenArchiveBuilder.Folder;
        const string Art=OpenArchiveSurfaceUpgrade.Folder;
        const string MeshFolder=Art+"/Meshes/";
        static GameObject fire,spawn;
        [Serializable] public class Check {
            public string scene;public bool passed,transformsPreserved,collidersPreserved,gameplayPreserved;
            public int torches,particleSystems,maxParticles;public float floorOverlapBefore,floorOverlapAfter;
        }
        [Serializable] public class Report{public bool passed;public string checkedUtc;public List<Check> scenes=new List<Check>();}
        static readonly string[] Scenes={Map+"/Scenes/MultiplayerGameplay.unity",Map+"/Scenes/OpenArchive_Explore.unity",Map+"/Testing/KhezuAwakening/OpenArchive_KhezuAwakening_Test.unity",Map+"/Testing/KhezuHunting/OpenArchive_KhezuHunting_Test.unity",Map+"/Testing/KhezuCinematic/OpenArchive_KhezuCinematic_Test.unity"};
        static string Pose(Transform t)=>t.localPosition.ToString("R")+t.localRotation.ToString("R")+t.localScale.ToString("R")+t.gameObject.activeSelf;
        static string Json(Component c)=>EditorJsonUtility.ToJson(c);

        [MenuItem("Tools/Logic Legends/Open Archive/Art/Apply grass, fire, spawn and surface repair")]
        public static string Apply(){
            OpenArchiveImprovements.RequireSavedEditor();string original=SceneManager.GetActiveScene().path;
            foreach(var folder in new[]{Art+"/Prefabs",Art+"/Reports",MeshFolder})Directory.CreateDirectory(folder);
            PrepareGrass();PrepareEffects();
            var floor=AssetDatabase.LoadAssetAtPath<Mesh>(Map+"/Meshes/Nest_StoneFloor_30m.asset");
            var clipped=BuildFloor(floor);var torchMesh=BuildTorch();
            var report=new Report{checkedUtc=DateTime.UtcNow.ToString("o")};
            try{foreach(string path in Scenes){
                var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);var root=scene.GetRootGameObjects().Single(g=>g.name=="OpenArchive").transform;
                var poses=root.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t,t=>Pose(t));
                var colliders=root.GetComponentsInChildren<Collider>(true).ToDictionary(c=>c,c=>Json(c));
                var systems=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MonoBehaviour>(true)).Where(c=>c!=null).ToDictionary(c=>c,c=>Json(c));
                var agents=root.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true).ToDictionary(c=>c,c=>Json(c));
                var nest=root.Find("UniversalMonsterNest");nest.Find("Nest_StoneFloor_30m").GetComponent<MeshFilter>().sharedMesh=clipped;
                RaiseOverlay(nest,"Den_Compacted_Sleeping_Bed",.020f,false);
                RaiseOverlay(nest,"Den_Buried_Tiles_And_Rubble",.035f,true);
                RaiseOverlay(nest,"Den_Carved_Claw_Scars",.043f,false);
                foreach(var t in root.GetComponentsInChildren<Transform>().Where(t=>t.name=="Torch_Pedestal").ToArray()){
                    t.GetComponent<MeshFilter>().sharedMesh=torchMesh;
                    if(t.Find("Torch_FireFX")==null){var fx=(GameObject)PrefabUtility.InstantiatePrefab(fire,scene);fx.name="Torch_FireFX";fx.transform.SetParent(t,false);fx.transform.localPosition=new Vector3(0,2.88f,0);}
                }
                var area=root.Find("PlayerSpawnArea");if(area.Find("Spawn_WelcomeFX")==null){var fx=(GameObject)PrefabUtility.InstantiatePrefab(spawn,scene);fx.name="Spawn_WelcomeFX";fx.transform.SetParent(area,false);fx.transform.localPosition=new Vector3(0,.24f,0);}
                var particles=root.GetComponentsInChildren<ParticleSystem>(true);
                var check=new Check{scene=path,
                    transformsPreserved=poses.All(p=>p.Key!=null&&Pose(p.Key)==p.Value),
                    collidersPreserved=colliders.All(p=>p.Key!=null&&Json(p.Key)==p.Value)&&colliders.Count==root.GetComponentsInChildren<Collider>(true).Length,
                    gameplayPreserved=systems.All(p=>p.Key!=null&&Json(p.Key)==p.Value)&&agents.All(p=>p.Key!=null&&Json(p.Key)==p.Value),
                    torches=root.GetComponentsInChildren<Transform>().Count(t=>t.name=="Torch_Pedestal"&&t.Find("Torch_FireFX")!=null),
                    particleSystems=particles.Length,maxParticles=particles.Sum(p=>p.main.maxParticles),floorOverlapBefore=OverlapArea(floor),floorOverlapAfter=OverlapArea(clipped)};
                check.passed=check.transformsPreserved&&check.collidersPreserved&&check.gameplayPreserved&&check.torches==8&&check.floorOverlapAfter<.0001f&&root.Find("FutureStructureSlots").childCount==10;
                if(!check.passed)throw new InvalidOperationException("Surface polish preservation failed: "+JsonUtility.ToJson(check));
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);report.scenes.Add(check);
            }
            AssetDatabase.SaveAssets();report.passed=report.scenes.All(c=>c.passed);string json=JsonUtility.ToJson(report,true);File.WriteAllText(Art+"/Reports/FireSpawnSurfaceRepair.json",json);return json;
            }finally{EditorSceneManager.OpenScene(original,OpenSceneMode.Single);}
        }

        static void PrepareGrass(){
            string path=Art+"/Textures/Archive_GrassAlbedo.png";var im=(TextureImporter)AssetImporter.GetAtPath(path);im.sRGBTexture=true;im.mipmapEnabled=true;im.wrapMode=TextureWrapMode.Repeat;im.filterMode=FilterMode.Trilinear;im.anisoLevel=4;im.maxTextureSize=512;im.isReadable=false;
            var a=im.GetPlatformTextureSettings("Android");a.overridden=true;a.maxTextureSize=512;a.format=TextureImporterFormat.ASTC_6x6;im.SetPlatformTextureSettings(a);im.SaveAndReimport();
            var material=AssetDatabase.LoadAssetAtPath<Material>(Map+"/Materials/Archive_Meadow.mat");material.SetTexture("_GrassMap",AssetDatabase.LoadAssetAtPath<Texture2D>(path));material.SetVector("_GrassReference",new Vector4(.073633f,.141184f,.026107f,1));material.SetFloat("_GrassTextureStrength",.94f);material.SetFloat("_TileMeters",3.8f);EditorUtility.SetDirty(material);
        }
        static Material Material(string name,string shader){string path=Art+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find(shader)){name=name};AssetDatabase.CreateAsset(m,path);}m.enableInstancing=true;return m;}
        static ParticleSystem Particles(Transform parent,string name,Mesh mesh,Material material,int max,float rate,float life,float size){
            var go=new GameObject(name);go.transform.SetParent(parent,false);var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.duration=3;main.loop=true;main.prewarm=true;main.playOnAwake=true;main.maxParticles=max;main.startLifetime=life;main.startSpeed=0;main.startSize=size;main.scalingMode=ParticleSystemScalingMode.Hierarchy;main.simulationSpace=ParticleSystemSimulationSpace.Local;main.cullingMode=ParticleSystemCullingMode.Automatic;
            var emission=ps.emission;emission.rateOverTime=rate;
            var shape=ps.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Cone;shape.radius=.20f;shape.angle=6;shape.rotation=new Vector3(-90,0,0);
            var velocity=ps.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.Local;velocity.x=new ParticleSystem.MinMaxCurve(-.08f,.08f);velocity.y=new ParticleSystem.MinMaxCurve(.65f,1.1f);velocity.z=new ParticleSystem.MinMaxCurve(-.08f,.08f);
            var scale=ps.sizeOverLifetime;scale.enabled=true;scale.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.4f),new Keyframe(.18f,1),new Keyframe(1,0)));
            var r=go.GetComponent<ParticleSystemRenderer>();r.renderMode=ParticleSystemRenderMode.Mesh;r.mesh=mesh;r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.enableGPUInstancing=true;
            return ps;
        }
        static void PrepareEffects(){
            var particleMaterial=Material("Archive_FacetedFlameParticles","Universal Render Pipeline/Particles/Unlit");
            particleMaterial.SetColor("_BaseColor",Color.white);particleMaterial.SetFloat("_Surface",0);particleMaterial.SetFloat("_Blend",0);particleMaterial.SetFloat("_ZWrite",1);particleMaterial.SetFloat("_SrcBlend",1);particleMaterial.SetFloat("_DstBlend",0);particleMaterial.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");particleMaterial.renderQueue=2000;EditorUtility.SetDirty(particleMaterial);
            var mesh=AssetDatabase.LoadAllAssetsAtPath("Assets/FullOpaqueFire/Mesh/P_Octahedron.fbx").OfType<Mesh>().First();
            var preview=EditorSceneManager.NewPreviewScene();
            try{
                var go=new GameObject("Torch_FireFX");SceneManager.MoveGameObjectToScene(go,preview);
                var ps=Particles(go.transform,"Flames",mesh,particleMaterial,14,20,.65f,.23f);var main=ps.main;main.startSize3D=true;main.startSizeX=new ParticleSystem.MinMaxCurve(.16f,.25f);main.startSizeY=new ParticleSystem.MinMaxCurve(.30f,.52f);main.startSizeZ=main.startSizeX;main.startRotation=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);
                var color=ps.colorOverLifetime;color.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(new Color(1.8f,1.25f,.14f),0),new GradientColorKey(new Color(1.5f,.40f,.025f),.45f),new GradientColorKey(new Color(.85f,.09f,.005f),1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(1,1)});color.color=gradient;
                var embers=Particles(go.transform,"Rising_Embers",mesh,particleMaterial,8,4,1.7f,.025f);main=embers.main;main.startColor=new Color(1.8f,.5f,.035f);
                fire=PrefabUtility.SaveAsPrefabAsset(go,Art+"/Prefabs/Archive_TorchFire.prefab");Object.DestroyImmediate(go);
                var welcome=new GameObject("Spawn_WelcomeFX");SceneManager.MoveGameObjectToScene(welcome,preview);
                for(int i=0;i<2;i++){
                    string name=i==0?"Archive_SpawnHalo_Outer":"Archive_SpawnHalo_Inner";var mat=Material(name,"LogicLegends/OpenArchive/SpawnHalo");mat.SetColor("_Tint",i==0?new Color(.12f,1.6f,.48f,.43f):new Color(.12f,1.2f,1.0f,.33f));mat.SetFloat("_Spin",i==0?8:-11);mat.SetFloat("_Pulse",i==0?1.4f:.95f);EditorUtility.SetDirty(mat);
                    var ring=new GameObject("Rotating_Welcome_Ring_"+i,typeof(MeshFilter),typeof(MeshRenderer));ring.transform.SetParent(welcome.transform,false);ring.transform.localPosition=Vector3.up*i*.06f;ring.GetComponent<MeshFilter>().sharedMesh=Ring(name,i==0?3.25f:2.65f,i==0?.075f:.05f);var r=ring.GetComponent<MeshRenderer>();r.sharedMaterial=mat;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
                }
                var motes=Particles(welcome.transform,"Welcome_Motes",mesh,particleMaterial,24,8,2.5f,.045f);main=motes.main;main.startColor=new Color(.25f,1.6f,.65f);var shape=motes.shape;shape.shapeType=ParticleSystemShapeType.Circle;shape.radius=3;shape.radiusThickness=.35f;shape.rotation=new Vector3(-90,0,0);
                var vel=motes.velocityOverLifetime;vel.y=new ParticleSystem.MinMaxCurve(.25f,.65f);
                spawn=PrefabUtility.SaveAsPrefabAsset(welcome,Art+"/Prefabs/Archive_SpawnWelcome.prefab");Object.DestroyImmediate(welcome);
            }finally{EditorSceneManager.ClosePreviewScene(preview);}
        }
        static Mesh Ring(string name,float radius,float width){
            string path=MeshFolder+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing!=null)return existing;
            var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();
            for(int i=0;i<96;i++){if(i%16>=13)continue;float a=i*Mathf.PI/48,b=(i+1)*Mathf.PI/48;int n=v.Count;foreach(var p in new[]{new Vector2(a,radius-width),new Vector2(a,radius+width),new Vector2(b,radius+width),new Vector2(b,radius-width)})v.Add(new Vector3(Mathf.Cos(p.x)*p.y,0,Mathf.Sin(p.x)*p.y));uv.AddRange(new[]{new Vector2(0,0),new Vector2(0,1),new Vector2(1,1),new Vector2(1,0)});t.AddRange(new[]{n,n+2,n+1,n,n+3,n+2});}
            var mesh=new Mesh{name=name};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        static Mesh BuildTorch(){
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Map+"/Prefabs/Decorations/Torch_Pedestal.prefab").GetComponent<MeshFilter>().sharedMesh;
            string path=MeshFolder+"Torch_Pedestal_NoStaticFlame.asset";var result=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(result!=null)return result;
            result=Object.Instantiate(source);result.name="Torch_Pedestal_NoStaticFlame";var indices=source.triangles;var uv=source.uv;var kept=new List<int>();
            for(int i=0;i<indices.Length;i+=3){int tile=Mathf.FloorToInt(uv[indices[i]].x*8)+Mathf.FloorToInt(uv[indices[i]].y*8)*8;if(tile<32)kept.AddRange(new[]{indices[i],indices[i+1],indices[i+2]});}
            result.triangles=kept.ToArray();result.RecalculateBounds();AssetDatabase.CreateAsset(result,path);return result;
        }
        static void RaiseOverlay(Transform nest,string name,float rise,bool onlyFlat){
            var f=nest.Find(name).GetComponent<MeshFilter>();string path=MeshFolder+name+"_StableVisual.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null){var source=AssetDatabase.LoadAssetAtPath<Mesh>(Map+"/Meshes/"+name+".asset");mesh=Object.Instantiate(source);mesh.name=name+"_StableVisual";var v=mesh.vertices;var move=new HashSet<int>();
                if(onlyFlat){var triangles=mesh.triangles;for(int i=0;i<triangles.Length;i+=3){int a=triangles[i],b=triangles[i+1],c=triangles[i+2];var center=(v[a]+v[b]+v[c])/3;float lo=Mathf.Min(v[a].y,Mathf.Min(v[b].y,v[c].y)),hi=Mathf.Max(v[a].y,Mathf.Max(v[b].y,v[c].y));float offset=lo-OpenArchiveDenRedesign.FloorY(center);if(hi-lo<.002f&&offset>=.001f&&offset<.04f){move.Add(a);move.Add(b);move.Add(c);}}}
                else for(int i=0;i<v.Length;i++)move.Add(i);foreach(int i in move)v[i].y+=rise;mesh.vertices=v;mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);
            }f.sharedMesh=mesh;
        }

        struct Vertex{
            public Vector3 p,n;public Color c;public Vector2 uv;
            public static Vertex Lerp(Vertex a,Vertex b,float t)=>new Vertex{p=Vector3.Lerp(a.p,b.p,t),n=Vector3.Lerp(a.n,b.n,t).normalized,c=Color.Lerp(a.c,b.c,t),uv=Vector2.Lerp(a.uv,b.uv,t)};
        }
        static readonly Rect[] Landings={new Rect(-5,12.5f,10,2.5f),new Rect(-5,-15,10,2.5f),new Rect(12.5f,-5,2.5f,10),new Rect(-15,-5,2.5f,10)};
        static float Side(Vertex v,Rect r,int plane)=>plane==0?v.p.x-r.xMin:plane==1?r.xMax-v.p.x:plane==2?v.p.z-r.yMin:r.yMax-v.p.z;
        static List<Vertex> Clip(List<Vertex> polygon,Rect rect,int plane,bool inside){
            var result=new List<Vertex>();if(polygon.Count==0)return result;
            for(int i=0;i<polygon.Count;i++){var a=polygon[i];var b=polygon[(i+1)%polygon.Count];float da=Side(a,rect,plane),db=Side(b,rect,plane);bool ia=inside?da>=0:da<=0,ib=inside?db>=0:db<=0;if(ia)result.Add(a);if(ia!=ib)result.Add(Vertex.Lerp(a,b,da/(da-db)));}return result;
        }
        static List<List<Vertex>> Subtract(List<Vertex> polygon,Rect rect){
            var result=new List<List<Vertex>>();var current=polygon;
            for(int i=0;i<4&&current.Count>=3;i++){var outside=Clip(current,rect,i,false);if(outside.Count>=3)result.Add(outside);current=Clip(current,rect,i,true);}return result;
        }
        static Mesh BuildFloor(Mesh source){
            string path=MeshFolder+"NestFloor_Visual_EntranceCutouts.asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing!=null)return existing;
            var v=source.vertices;var c=source.colors;var normals=source.normals;var uv=source.uv;var indices=source.triangles;var points=new List<Vector3>();var colors=new List<Color>();var ns=new List<Vector3>();var uvs=new List<Vector2>();var tris=new List<int>();
            for(int i=0;i<indices.Length;i+=3){var polygon=new List<Vertex>();for(int k=0;k<3;k++){int j=indices[i+k];polygon.Add(new Vertex{p=v[j],c=c[j],n=normals[j],uv=uv[j]});}
                var pieces=new List<List<Vertex>>{polygon};foreach(var rect in Landings)pieces=pieces.SelectMany(p=>Subtract(p,rect)).ToList();
                foreach(var p in pieces)for(int k=1;k<p.Count-1;k++){var tri=new[]{p[0],p[k],p[k+1]};if(Vector3.Cross(tri[1].p-tri[0].p,tri[2].p-tri[0].p).sqrMagnitude<.00000001f)continue;foreach(var x in tri){tris.Add(points.Count);points.Add(x.p);colors.Add(x.c);ns.Add(x.n);uvs.Add(x.uv);}}
            }
            var mesh=new Mesh{name="NestFloor_Visual_EntranceCutouts"};mesh.SetVertices(points);mesh.SetColors(colors);mesh.SetNormals(ns);mesh.SetUVs(0,uvs);mesh.SetTriangles(tris,0);mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        public static float OverlapArea(Mesh mesh){
            var vertices=mesh.vertices;var t=mesh.triangles;float area=0;
            foreach(var rect in Landings)for(int i=0;i<t.Length;i+=3){var p=new List<Vertex>{new Vertex{p=vertices[t[i]]},new Vertex{p=vertices[t[i+1]]},new Vertex{p=vertices[t[i+2]]}};for(int n=0;n<4;n++)p=Clip(p,rect,n,true);for(int n=1;n<p.Count-1;n++)area+=Mathf.Abs(Vector3.Cross(p[n].p-p[0].p,p[n+1].p-p[0].p).y)*.5f;}return area;
        }
    }
}
