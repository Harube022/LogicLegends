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
    /// <summary>Render-only natural surfaces and animated waterfall ribbons. Original physics assets stay intact.</summary>
    public static class OpenArchiveWatersideArt
    {
        const string Map=OpenArchiveBuilder.Folder,Art=Map+"/Art",Meshes=Art+"/Meshes/Waterside";
        static readonly string[] Scenes={Map+"/Scenes/MultiplayerGameplay.unity",Map+"/Scenes/OpenArchive_Explore.unity",Map+"/Testing/KhezuAwakening/OpenArchive_KhezuAwakening_Test.unity",Map+"/Testing/KhezuHunting/OpenArchive_KhezuHunting_Test.unity",Map+"/Testing/KhezuCinematic/OpenArchive_KhezuCinematic_Test.unity"};
        static readonly Dictionary<string,Mesh> cache=new Dictionary<string,Mesh>();
        static Material falls,foam;static GameObject splashPrefab;static Mesh foamQuad;
        [Serializable] sealed class Check {public string scene;public bool passed,posesPreserved,physicsPreserved,gameplayPreserved,waterMeshesPreserved;public int softenedMeshes,waterfallRenderers,splashSystems,maxSplashParticles;}
        [Serializable] sealed class Report {public string checkedUtc;public bool passed;public List<Check> scenes=new List<Check>();public string limitations="Desktop Unity URP visual/asset/physics preservation review. Android hardware performance and live multiplayer not tested.";}
        struct Endpoint {public Vector3 top,bottom,side;public float topHalf,bottomHalf;}
        static string Pose(Transform t){return t.localPosition.ToString("R")+t.localRotation.ToString("R")+t.localScale.ToString("R")+t.gameObject.activeSelf;}
        static bool Leaf(Vector2 uv){int cell=Mathf.FloorToInt(uv.x*8)+Mathf.FloorToInt(uv.y*8)*8;return (cell>=16&&cell<=21)||cell==54||cell==55;}
        static string Key(Vector3 v){return Mathf.RoundToInt(v.x*10000)+","+Mathf.RoundToInt(v.y*10000)+","+Mathf.RoundToInt(v.z*10000);}
        static Material Material(string name,string shader){string path=Art+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find(shader)){name=name};AssetDatabase.CreateAsset(m,path);}m.shader=Shader.Find(shader);m.enableInstancing=true;return m;}
        static Mesh SaveMesh(string name,Mesh mesh){string path=Meshes+"/"+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old!=null){if(name.EndsWith("__FlowRibbon")){old.Clear();old.vertices=mesh.vertices;old.uv=mesh.uv;old.colors=mesh.colors;old.normals=mesh.normals;old.triangles=mesh.triangles;old.bounds=mesh.bounds;EditorUtility.SetDirty(old);}Object.DestroyImmediate(mesh);return old;}mesh.name=name;AssetDatabase.CreateAsset(mesh,path);return mesh;}
        static Mesh Original(Mesh current){if(!AssetDatabase.GetAssetPath(current).StartsWith(Meshes+"/"))return current;string source=current.name.Split(new[]{"__"},StringSplitOptions.None)[0];return AssetDatabase.LoadAssetAtPath<Mesh>(Map+"/Meshes/"+source+".asset")??current;}
        static void ImportMask(string name){
            string path=Art+"/Textures/"+name+".png";var im=(TextureImporter)AssetImporter.GetAtPath(path);im.sRGBTexture=false;im.mipmapEnabled=true;im.wrapMode=TextureWrapMode.Repeat;im.filterMode=FilterMode.Trilinear;im.anisoLevel=2;im.maxTextureSize=256;im.isReadable=false;
            var android=im.GetPlatformTextureSettings("Android");android.overridden=true;android.maxTextureSize=256;android.format=TextureImporterFormat.ASTC_6x6;im.SetPlatformTextureSettings(android);im.SaveAndReimport();
        }
        static Material Natural(string name,int kind,float scale){
            var m=Material(name,"LogicLegends/OpenArchive/NaturalSurfaces");m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Map+"/Textures/OpenArchive_Palette.png"));m.SetTexture("_SurfaceMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"/Textures/Archive_NaturalSurfaceMasks.png"));
            m.SetFloat("_Kind",kind);m.SetFloat("_PaletteMix",kind==1?1:0);m.SetColor("_BaseColor",kind==1?Color.white:new Color(.53f,.56f,.53f));m.SetFloat("_Repeat",kind==1?1.15f:2.8f);m.SetFloat("_Detail",kind==1?.9f:.92f);m.SetFloat("_Relief",kind==1?0:.012f);m.SetFloat("_Moss",kind==2?.3f:.2f);m.SetFloat("_WaterLevel",-1.15f*scale);EditorUtility.SetDirty(m);return m;
        }
        static Mesh SoftMesh(Mesh source,bool plants){
            source=Original(source);string name=source.name+"__"+(plants?"SoftLeaf":"RiverStone");Mesh saved;if(cache.TryGetValue(name,out saved))return saved;
            var mesh=Object.Instantiate(source);var vertices=mesh.vertices;var uv=mesh.uv;var normals=mesh.normals;var oldColors=mesh.colors;var colors=new Color[vertices.Length];var triangles=mesh.triangles;var sums=new Dictionary<string,Vector3>();
            for(int t=0;t<triangles.Length;t+=3){int a=triangles[t],b=triangles[t+1],c=triangles[t+2];var n=Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]);foreach(int i in new[]{a,b,c}){if(plants&&!Leaf(uv[i]))continue;string key=Key(vertices[i]);Vector3 total;sums.TryGetValue(key,out total);sums[key]=total+n;}}
            for(int i=0;i<vertices.Length;i++){
                bool leaf=plants&&Leaf(uv[i]);Vector3 n;if((!plants||leaf)&&sums.TryGetValue(Key(vertices[i]),out n)&&n.sqrMagnitude>.000001f)normals[i]=n.normalized;
                float variation=.94f+Mathf.PerlinNoise(vertices[i].x*.45f+19,vertices[i].z*.45f+vertices[i].y*.13f)*.12f;
                if(plants){colors[i]=oldColors.Length==vertices.Length?oldColors[i]:Color.white;colors[i].a=leaf?1:0;if(leaf){uv[i]=new Vector2(1.5f/8,2.5f/8);colors[i]=new Color(variation,variation,variation,1);}}
                else{var old=oldColors.Length==vertices.Length?oldColors[i]:Color.white;bool moss=old.g>old.r*1.3f&&old.g>old.b*1.25f;colors[i]=moss?new Color(.55f,.76f,.42f,0):new Color(variation,variation,variation,0);}
            }
            mesh.normals=normals;mesh.colors=colors;mesh.uv=uv;mesh.RecalculateBounds();saved=SaveMesh(name,mesh);cache[name]=saved;return saved;
        }
        static void Ribbon(Endpoint e,List<Vector3> v,List<Vector2> uv,List<Color> colors,List<int> triangles){
            const int across=8,down=14;int first=v.Count;
            var outward=Vector3.Cross(Vector3.up,e.side).normalized;
            for(int y=0;y<=down;y++)for(int x=0;x<=across;x++){
                float t=(float)y/down,u=(float)x/across;var center=Vector3.Lerp(e.top,e.bottom,t)+outward*Mathf.Sin(t*Mathf.PI)*.055f;
                float half=Mathf.Lerp(e.topHalf,e.bottomHalf,t);v.Add(center+e.side*((u*2-1)*half));uv.Add(new Vector2(u,1-t));colors.Add(Color.black);
            }
            for(int y=0;y<down;y++)for(int x=0;x<across;x++){int a=first+y*(across+1)+x,b=a+1,c=a+across+1,d=c+1;triangles.AddRange(new[]{a,c,b,b,c,d});}
        }
        static List<Endpoint> Endpoints(Mesh source,bool small){
            var vs=source.vertices;var result=new List<Endpoint>();
            if(small){for(int i=0;i<4;i++){int s=i*12;result.Add(new Endpoint{top=(vs[s]+vs[s+1])*.5f,bottom=(vs[s+2]+vs[s+5])*.5f,side=(vs[s+1]-vs[s]).normalized,topHalf=Vector3.Distance(vs[s],vs[s+1])*.5f,bottomHalf=Vector3.Distance(vs[s+2],vs[s+5])*.5f});}}
            else{
                var n=source.normals;var vertical=Enumerable.Range(0,vs.Length).Where(i=>Mathf.Abs(n[i].y)<.5f).Select(i=>vs[i]).Distinct().ToArray();float top=vertical.Max(p=>p.y);var upper=vertical.Where(p=>p.y>top-.05f).ToArray();var lower=vertical.Where(p=>p.y<top-3).ToArray();
                var a=upper.Aggregate(Vector3.zero,(s,p)=>s+p)/upper.Length;var b=lower.Aggregate(Vector3.zero,(s,p)=>s+p)/lower.Length;
                var outward=new Vector3(a.x,0,a.z).normalized;var side=new Vector3(outward.z,0,-outward.x);
                // Let the visual sheet clear the uneven cliff columns instead of cutting through them.
                result.Add(new Endpoint{top=a+outward*2.4f,bottom=b+outward*1.25f,side=side,topHalf=(upper.Max(p=>Vector3.Dot(p-a,side))-upper.Min(p=>Vector3.Dot(p-a,side)))*.5f,bottomHalf=(lower.Max(p=>Vector3.Dot(p-b,side))-lower.Min(p=>Vector3.Dot(p-b,side)))*.5f});
            }return result;
        }
        static Mesh FallMesh(Mesh source,bool small){
            source=Original(source);string name=source.name+"__FlowRibbon";Mesh saved;if(cache.TryGetValue(name,out saved))return saved;
            var v=new List<Vector3>();var uv=new List<Vector2>();var colors=new List<Color>();var triangles=new List<int>();foreach(var end in Endpoints(source,small))Ribbon(end,v,uv,colors,triangles);
            if(!small){var oldV=source.vertices;var oldN=source.normals;var oldT=source.triangles;var end=Endpoints(source,false)[0];var outward=new Vector3(end.top.x,0,end.top.z).normalized;float lip=Vector3.Dot(end.top-outward*2.4f,outward);for(int t=0;t<oldT.Length;t+=3){int a=oldT[t];if(Mathf.Abs(oldN[a].y)<.5f||oldV[a].y<-.1f)continue;for(int i=0;i<3;i++){var p=oldV[oldT[t+i]];float stretch=Mathf.InverseLerp(lip-2,lip,Vector3.Dot(p,outward));p+=outward*(2.4f*stretch)+Vector3.up*.025f;triangles.Add(v.Count);v.Add(p);uv.Add(new Vector2(p.x*.35f,p.z*.35f));colors.Add(Color.red);}}}
            var mesh=new Mesh{name=name};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();saved=SaveMesh(name,mesh);cache[name]=saved;return saved;
        }
        static Mesh Quad(){
            var m=new Mesh();m.vertices=new[]{new Vector3(-1,0,-1),new Vector3(1,0,-1),new Vector3(-1,0,1),new Vector3(1,0,1)};m.uv=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one};m.triangles=new[]{0,2,1,1,2,3};m.RecalculateNormals();m.RecalculateBounds();return SaveMesh("WaterfallFoamQuad",m);
        }
        static GameObject PrepareSplash(){
            var prior=SceneManager.GetActiveScene();var stage=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(stage);
            try{
                var go=new GameObject("Waterfall_Impact_FX");var disk=new GameObject("Impact_Foam");disk.transform.SetParent(go.transform,false);disk.AddComponent<MeshFilter>().sharedMesh=foamQuad;var r=disk.AddComponent<MeshRenderer>();r.sharedMaterial=foam;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
                var drops=new GameObject("Water_Droplets");drops.transform.SetParent(go.transform,false);drops.transform.localRotation=Quaternion.Euler(-90,0,0);var ps=drops.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=ps.main;main.duration=1.4f;main.loop=true;main.playOnAwake=true;main.startLifetime=new ParticleSystem.MinMaxCurve(.28f,.55f);main.startSpeed=new ParticleSystem.MinMaxCurve(.5f,1.2f);main.startSize=new ParticleSystem.MinMaxCurve(.035f,.075f);main.startColor=new Color(.72f,.95f,.94f,1);main.maxParticles=10;main.gravityModifier=.25f;main.scalingMode=ParticleSystemScalingMode.Hierarchy;main.simulationSpace=ParticleSystemSimulationSpace.Local;
                var emission=ps.emission;emission.rateOverTime=14;var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=48;shape.radius=.3f;
                var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=AssetDatabase.LoadAllAssetsAtPath("Assets/FullOpaqueFire/Mesh/P_Octahedron.fbx").OfType<Mesh>().First();
                renderer.sharedMaterial=Material("Archive_WaterDroplets","Universal Render Pipeline/Particles/Unlit");renderer.sharedMaterial.SetColor("_BaseColor",new Color(.72f,.95f,.95f,1));EditorUtility.SetDirty(renderer.sharedMaterial);renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                return PrefabUtility.SaveAsPrefabAsset(go,Art+"/Prefabs/Archive_WaterfallImpact.prefab");
            }
            finally{SceneManager.SetActiveScene(prior);EditorSceneManager.CloseScene(stage,true);}
        }
        static void Prepare(){
            cache.Clear();Directory.CreateDirectory(Meshes);ImportMask("Archive_FlowMasks");ImportMask("Archive_NaturalSurfaceMasks");
            var flow=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"/Textures/Archive_FlowMasks.png");
            falls=Material("Archive_AnimatedCascades","LogicLegends/OpenArchive/WaterfallRibbon");falls.SetTexture("_FlowMap",flow);falls.SetColor("_DeepColor",new Color(.08f,.39f,.48f));falls.SetColor("_WaterColor",new Color(.31f,.72f,.76f));falls.SetColor("_FoamColor",new Color(.86f,.98f,.97f));falls.SetFloat("_Speed",.95f);falls.SetFloat("_Foam",.88f);falls.SetFloat("_Ripple",.012f);EditorUtility.SetDirty(falls);
            foam=Material("Archive_WaterfallImpactFoam","LogicLegends/OpenArchive/WaterfallFoam");foam.SetTexture("_FlowMap",flow);foam.SetColor("_Color",new Color(.80f,.96f,.95f,.8f));foam.SetFloat("_Speed",.9f);EditorUtility.SetDirty(foam);
            var water=AssetDatabase.LoadAssetAtPath<Material>(Map+"/Materials/Archive_TurquoiseWater.mat");water.SetTexture("_FlowMap",flow);water.SetFloat("_FlowSpeed",.45f);water.SetFloat("_FoamStrength",.65f);EditorUtility.SetDirty(water);
            foamQuad=Quad();splashPrefab=PrepareSplash();
        }
        [MenuItem("Tools/Logic Legends/Open Archive/Art/Apply natural rocks, foliage and waterfall polish")]
        public static string Apply(){
            OpenArchiveImprovements.RequireSavedEditor();string original=SceneManager.GetActiveScene().path;Prepare();var report=new Report{checkedUtc=DateTime.UtcNow.ToString("o")};
            try{foreach(string path in Scenes){
                var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);var root=scene.GetRootGameObjects().Single(g=>g.name=="OpenArchive").transform;float scale=root.lossyScale.x;
                var owned=root.Find("Environment/Waterside_FlowFX");var poses=root.GetComponentsInChildren<Transform>(true).Where(t=>owned==null||!t.IsChildOf(owned)).ToDictionary(t=>t,t=>Pose(t));var physics=root.GetComponentsInChildren<Collider>(true).ToDictionary(c=>c,c=>EditorJsonUtility.ToJson(c));
                var systems=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MonoBehaviour>(true)).Where(c=>c!=null).ToDictionary(c=>c,c=>EditorJsonUtility.ToJson(c));var agents=root.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true).ToDictionary(c=>c,c=>EditorJsonUtility.ToJson(c));
                var waterMeshes=root.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name.StartsWith("Turquoise_RiverPond_")).ToDictionary(f=>f,f=>f.sharedMesh);
                string suffix=scale>1.1f?"":"_SourceScale";var rock=Natural("Archive_NaturalRiverStone"+suffix,0,scale);var plant=Natural("Archive_SoftLeafClusters"+suffix,1,scale);var bank=Natural("Archive_WeatheredNaturalBank"+suffix,2,scale);
                int softened=0;foreach(var f in root.GetComponentsInChildren<MeshFilter>(true)){
                    string rel=AnimationUtility.CalculateTransformPath(f.transform,root);bool foliage=f.name.StartsWith("Vegetation_Cluster_")||f.name.StartsWith("Natural_Bank_Plants_")||f.name.StartsWith("Meadow_Edge_Vegetation_");
                    bool stone=rel.StartsWith("Environment/Rocks/")&&!f.name.Contains("CutStone");bool bankWall=f.name=="Riverbank_CutStone";
                    if(!foliage&&!stone&&!bankWall)continue;f.sharedMesh=SoftMesh(f.sharedMesh,foliage);f.GetComponent<Renderer>().sharedMaterial=foliage?plant:bankWall?bank:rock;softened++;
                }
                var group=root.Find("Environment/Waterside_FlowFX");if(group==null){group=new GameObject("Waterside_FlowFX").transform;group.SetParent(root.Find("Environment"),false);}
                foreach(var f in root.Find("Environment/Waterfalls").GetComponentsInChildren<MeshFilter>(true)){
                    var source=Original(f.sharedMesh);bool small=f.name=="Small_Bank_Cascades";var ends=Endpoints(source,small);f.sharedMesh=FallMesh(source,small);f.GetComponent<Renderer>().sharedMaterial=falls;f.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
                    for(int i=0;i<ends.Count;i++){
                        string name=f.name+"_Impact_"+i;var existing=group.Find(name);var fx=existing!=null?existing.gameObject:(GameObject)PrefabUtility.InstantiatePrefab(splashPrefab,scene);fx.name=name;fx.transform.SetParent(group,false);fx.transform.localPosition=root.InverseTransformPoint(f.transform.TransformPoint(ends[i].bottom))+Vector3.up*.035f;float size=small?.85f:2.8f;fx.transform.localScale=new Vector3(size,.8f,size);
                    }
                }
                var particles=group.GetComponentsInChildren<ParticleSystem>();var check=new Check{scene=path,posesPreserved=poses.All(p=>p.Key!=null&&Pose(p.Key)==p.Value),physicsPreserved=physics.All(p=>p.Key!=null&&EditorJsonUtility.ToJson(p.Key)==p.Value)&&physics.Count==root.GetComponentsInChildren<Collider>(true).Length,
                    gameplayPreserved=systems.All(p=>p.Key!=null&&EditorJsonUtility.ToJson(p.Key)==p.Value)&&agents.All(p=>p.Key!=null&&EditorJsonUtility.ToJson(p.Key)==p.Value),waterMeshesPreserved=waterMeshes.All(p=>p.Key.sharedMesh==p.Value),softenedMeshes=softened,waterfallRenderers=9,splashSystems=particles.Length,maxSplashParticles=particles.Sum(p=>p.main.maxParticles)};
                check.passed=check.posesPreserved&&check.physicsPreserved&&check.gameplayPreserved&&check.waterMeshesPreserved&&check.splashSystems==12&&check.maxSplashParticles<=120;
                if(!check.passed)throw new InvalidOperationException("Waterside preservation failed: "+JsonUtility.ToJson(check));EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);report.scenes.Add(check);
            }
            AssetDatabase.SaveAssets();report.passed=report.scenes.Count==5&&report.scenes.All(c=>c.passed);var json=JsonUtility.ToJson(report,true);File.WriteAllText(Art+"/Reports/WatersideArtPreservation.json",json);return json;
            }finally{EditorSceneManager.OpenScene(original,OpenSceneMode.Single);}
        }
    }
}
