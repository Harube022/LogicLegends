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
    /// <summary>Visual changes only. Never rebuilds terrain, bridges, navigation or encounters.</summary>
    public static class OpenArchiveSurfaceUpgrade
    {
        public const string Folder=OpenArchiveBuilder.Folder+"/Art";
        static Material atlas,meadow,nature,stone,cliff,wood,earth,moss,falls,paving,pavingVertex,forest;
        static readonly string[] Scenes={
            OpenArchiveBuilder.Folder+"/Scenes/MultiplayerGameplay.unity",
            OpenArchiveBuilder.Folder+"/Scenes/OpenArchive_Explore.unity",
            OpenArchiveBuilder.Folder+"/Testing/KhezuAwakening/OpenArchive_KhezuAwakening_Test.unity",
            OpenArchiveBuilder.Folder+"/Testing/KhezuHunting/OpenArchive_KhezuHunting_Test.unity",
            OpenArchiveBuilder.Folder+"/Testing/KhezuCinematic/OpenArchive_KhezuCinematic_Test.unity"};
        [Serializable] public class SceneCheck {
            public string scene;public bool passed,transformsUnchanged,collidersUnchanged,gameplayComponentsUnchanged,meshReferencesUnchanged;
            public int renderers,slots,entrances,newDecorationTriangles,missingShaders;
        }
        [Serializable] public class Report {
            public string checkedUtc;public bool passed;public List<SceneCheck> scenes=new List<SceneCheck>();
            public string limitations="Editor visual and preservation checks; real Android GPU/frame-time and live multi-client transport require device testing. No navigation assets are rebaked.";
        }
        static string Fingerprint(Component c)=>EditorJsonUtility.ToJson(c);
        static string Pose(Transform t)=>JsonUtility.ToJson(new PoseData{p=t.localPosition,q=t.localRotation,s=t.localScale,active=t.gameObject.activeSelf,layer=t.gameObject.layer,tag=t.tag});
        [Serializable] class PoseData{public Vector3 p,s;public Quaternion q;public bool active;public int layer;public string tag;}
        static Transform Root(Scene s)=>s.GetRootGameObjects().Single(g=>g.name=="OpenArchive").transform;

        [MenuItem("Tools/Logic Legends/Open Archive/Art/Apply surface and daylight upgrade")]
        public static string Apply()
        {
            OpenArchiveImprovements.RequireSavedEditor();
            string original=SceneManager.GetActiveScene().path;
            var report=new Report{checkedUtc=DateTime.UtcNow.ToString("o")};
            if(QualitySettings.names[QualitySettings.GetQualityLevel()]=="Very Low"&&QualitySettings.shadows==ShadowQuality.Disable)QualitySettings.shadows=ShadowQuality.HardOnly;
            Prepare();
            try{
                foreach(string path in Scenes.Where(File.Exists)){
                    var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);var root=Root(scene);
                    var transforms=root.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t,t=>Pose(t));
                    var colliders=root.GetComponentsInChildren<Collider>(true).ToDictionary(c=>c,c=>Fingerprint(c));
                    // Include systems outside OpenArchive too: player rigs, UI, camera and encounter adapters.
                    var systems=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MonoBehaviour>(true)).Where(c=>c!=null).ToDictionary(c=>c,c=>Fingerprint(c));
                    var agents=root.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true).ToDictionary(c=>c,c=>Fingerprint(c));
                    var meshes=root.GetComponentsInChildren<MeshFilter>(true).ToDictionary(f=>f,f=>f.sharedMesh);
                    foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true)){
                        if(renderer.sharedMaterial==null)continue;
                        string name=renderer.sharedMaterial.name;
                        string rel=AnimationUtility.CalculateTransformPath(renderer.transform,root);
                        bool vertex=name=="Archive_Meadow"||name=="Archive_WeatheredStone_Vertex";
                        if(!vertex && name!="Archive_Atlas")continue;
                        if(rel.Contains("/Waterfalls/"))renderer.sharedMaterial=falls;
                        else if(rel.Contains("Compacted_Sleeping_Bed")||rel.Contains("Nest_StoneFloor"))renderer.sharedMaterial=earth;
                        else if(rel.Contains("/MainPaths/")||(rel.Contains("/SecondaryPaths/")&&!rel.Contains("Empty_Ground"))||rel.Contains("Spawn_StonePlaza")||rel.Contains("RampSurface_")||rel.Contains("Four_Ramp_")||rel.Contains("Nest_Four_Wide"))renderer.sharedMaterial=vertex?pavingVertex:paving;
                        else if(rel.Contains("Moss"))renderer.sharedMaterial=moss;
                        else if(rel.Contains("/Bridges/")&&!vertex)renderer.sharedMaterial=wood;
                        else if(rel.Contains("/Cliffs/"))renderer.sharedMaterial=cliff;
                        else if(vertex && (rel.Contains("Grassland")||rel.Contains("Hummocks")||rel.Contains("Empty_Ground")))renderer.sharedMaterial=meadow;
                        else if(vertex)renderer.sharedMaterial=stone;
                        // Remaining palette-based architecture keeps its original color cells.
                    }
                    AddUnderstory(root);
                    AddForestFloor(root);
                    Daylight(root);
                    var check=new SceneCheck{scene=path,
                        transformsUnchanged=transforms.All(p=>p.Key!=null&&Pose(p.Key)==p.Value),
                        collidersUnchanged=colliders.All(p=>p.Key!=null&&Fingerprint(p.Key)==p.Value)&&colliders.Count==root.GetComponentsInChildren<Collider>(true).Length,
                        gameplayComponentsUnchanged=systems.All(p=>p.Key!=null&&Fingerprint(p.Key)==p.Value)&&agents.All(p=>p.Key!=null&&Fingerprint(p.Key)==p.Value),
                        meshReferencesUnchanged=meshes.All(p=>p.Key!=null&&p.Key.sharedMesh==p.Value),
                        slots=root.Find("FutureStructureSlots").childCount,
                        entrances=root.Find("UniversalMonsterNest").Cast<Transform>().Count(t=>t.name.StartsWith("Entrance_")),
                        renderers=root.GetComponentsInChildren<Renderer>(true).Length,
                        newDecorationTriangles=root.Find("Environment/Art_Understory").GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3),
                        missingShaders=root.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Count(m=>m==null||m.shader==null||!m.shader.isSupported||ShaderUtil.ShaderHasError(m.shader))};
                    check.passed=check.transformsUnchanged&&check.collidersUnchanged&&check.gameplayComponentsUnchanged&&check.meshReferencesUnchanged&&check.slots==10&&check.entrances==4&&check.missingShaders==0;
                    if(!check.passed)throw new InvalidOperationException("Visual pass preservation failed: "+JsonUtility.ToJson(check));
                    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);report.scenes.Add(check);
                }
                AssetDatabase.SaveAssets();report.passed=report.scenes.Count==5&&report.scenes.All(s=>s.passed);
                File.WriteAllText(Folder+"/Reports/ScenePreservation.json",JsonUtility.ToJson(report,true));
                return JsonUtility.ToJson(report,true);
            }finally{if(!string.IsNullOrEmpty(original))EditorSceneManager.OpenScene(original,OpenSceneMode.Single);}
        }
        static Material Variant(string name,bool vertex,float style,float repeat,float strength,float mossAmount){
            string path=Folder+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("LogicLegends/OpenArchive/DetailedSurface")){name=name};AssetDatabase.CreateAsset(m,path);}
            Configure(m,vertex,style,repeat,strength,mossAmount);return m;
        }
        static void Configure(Material m,bool vertex,float style,float repeat,float strength,float mossAmount){
            var palette=AssetDatabase.LoadAssetAtPath<Texture2D>(OpenArchiveBuilder.Folder+"/Textures/OpenArchive_Palette.png");
            m.shader=Shader.Find("LogicLegends/OpenArchive/DetailedSurface");m.enableInstancing=true;
            m.SetTexture("_DetailAtlas",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Textures/Archive_SurfaceDetailAtlas.png"));
            m.SetTexture("_MacroMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Textures/Archive_GroundVariation.png"));
            if(m.name!="Archive_NatureAtlas")m.SetTexture("_BaseMap",vertex?null:palette);
            m.SetColor("_BaseColor",Color.white);m.SetFloat("_VertexColor",vertex?1:0);m.SetFloat("_SurfaceStyle",style);
            m.SetFloat("_TileMeters",repeat);m.SetFloat("_DetailStrength",strength);m.SetFloat("_MossAmount",mossAmount);m.SetFloat("_MossHeight",5);
            m.SetFloat("_EmissionStrength",m.name=="Archive_Atlas"?1:0);EditorUtility.SetDirty(m);
        }
        static void Prepare(){
            foreach(var p in new[]{Folder+"/Textures/Archive_SurfaceDetailAtlas.png",Folder+"/Textures/Archive_GroundVariation.png"}){
                var importer=(TextureImporter)AssetImporter.GetAtPath(p);importer.sRGBTexture=false;importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Repeat;
                importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=2;importer.isReadable=false;importer.maxTextureSize=p.Contains("Atlas")?1024:128;
                importer.textureCompression=TextureImporterCompression.Compressed;
                var android=importer.GetPlatformTextureSettings("Android");android.overridden=true;android.maxTextureSize=importer.maxTextureSize;android.format=TextureImporterFormat.ASTC_6x6;android.compressionQuality=50;importer.SetPlatformTextureSettings(android);importer.SaveAndReimport();
            }
            atlas=AssetDatabase.LoadAssetAtPath<Material>(OpenArchiveBuilder.Folder+"/Materials/Archive_Atlas.mat");Configure(atlas,false,1,4.5f,.8f,.27f);
            meadow=AssetDatabase.LoadAssetAtPath<Material>(OpenArchiveBuilder.Folder+"/Materials/Archive_Meadow.mat");Configure(meadow,true,0,3.2f,1,0);meadow.SetColor("_BaseColor",new Color(.94f,1,.89f));
            nature=AssetDatabase.LoadAssetAtPath<Material>(OpenArchiveBuilder.Folder+"/Materials/Archive_NatureAtlas.mat");Configure(nature,false,5,5,.2f,0);
            stone=Variant("Archive_WeatheredStone_Vertex",true,1,4,.9f,.4f);
            cliff=Variant("Archive_LayeredCliff",false,2,7,.8f,.5f);
            wood=Variant("Archive_WornBridgeWood",false,4,5,.65f,.05f);
            earth=Variant("Archive_NestDarkEarth",true,3,3.4f,.85f,0);earth.SetColor("_BaseColor",new Color(.83f,.73f,.60f));
            moss=Variant("Archive_MossAndStone",true,1,3.5f,.75f,.75f);
            paving=Variant("Archive_WarmStonePaving",false,6,6,.72f,.08f);
            pavingVertex=Variant("Archive_WarmStonePaving_Vertex",true,6,6,.72f,.08f);
            forest=Variant("Archive_ForestFloorDetails",true,5,3,.3f,0);
            var water=AssetDatabase.LoadAssetAtPath<Material>(OpenArchiveBuilder.Folder+"/Materials/Archive_TurquoiseWater.mat");
            water.SetColor("_DeepColor",new Color(.025f,.47f,.59f));water.SetColor("_ShoreColor",new Color(.20f,.72f,.68f));water.SetColor("_RippleColor",new Color(.48f,.88f,.83f));
            water.SetColor("_FoamColor",new Color(.73f,.94f,.86f));water.SetFloat("_FoamStrength",.53f);water.SetFloat("_FlowSpeed",.55f);water.SetFloat("_WaveHeight",.024f);water.enableInstancing=true;EditorUtility.SetDirty(water);
            string fallsPath=Folder+"/Materials/Archive_AnimatedCascades.mat";falls=AssetDatabase.LoadAssetAtPath<Material>(fallsPath);
            if(falls==null){falls=new Material(water){name="Archive_AnimatedCascades"};AssetDatabase.CreateAsset(falls,fallsPath);}
            falls.SetFloat("_VerticalFlow",1);falls.SetFloat("_FoamStrength",.3f);falls.SetColor("_DeepColor",new Color(.22f,.65f,.76f));falls.SetColor("_ShoreColor",new Color(.38f,.79f,.82f));EditorUtility.SetDirty(falls);
        }
        static void Daylight(Transform root){
            var sun=root.Find("Lighting/Warm_Daylight").GetComponent<Light>();sun.color=new Color(1,.945f,.84f);sun.intensity=1.25f;sun.shadows=LightShadows.Soft;sun.shadowStrength=.63f;
            RenderSettings.sun=sun;RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.62f,.75f,.86f);RenderSettings.ambientEquatorColor=new Color(.48f,.55f,.43f);RenderSettings.ambientGroundColor=new Color(.28f,.30f,.23f);
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogColor=new Color(.61f,.77f,.80f);RenderSettings.fogDensity=.00115f;
        }
        static void AddUnderstory(Transform root){
            if(root.Find("Environment/Art_Understory")!=null)return;
            var group=new GameObject("Art_Understory");group.transform.SetParent(root.Find("Environment"),false);
            var pieces=new List<CombineInstance>();var rng=new System.Random(619);var slots=root.Find("FutureStructureSlots").Cast<Transform>().Select(t=>root.InverseTransformPoint(t.position)).ToArray();
            var trees=root.Find("Environment/Trees").Cast<Transform>().Where(t=>t.name.StartsWith("Archive_Tree")).ToArray();
            string[] models={"Flowers_01","Flowers_02","Mushroom_01","Mushroom_02"};
            var source=models.Select(n=>AssetDatabase.LoadAllAssetsAtPath("Assets/CHEATCODE/SimpleNaturePack/Models/"+n+".fbx").OfType<Mesh>().OrderBy(m=>m.vertexCount).First()).ToArray();
            int count=0;
            foreach(var tree in trees){
                if(count>=48)break;
                Vector3 center=root.InverseTransformPoint(tree.position);
                for(int k=0;k<3;k++){
                    float a=(float)rng.NextDouble()*Mathf.PI*2,r=1.4f+(float)rng.NextDouble()*2;
                    Vector3 p=center+new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);p.y=0;
                    float radius=new Vector2(p.x,p.z).magnitude;
                    if(radius<43||Mathf.Abs(p.x)<8||Mathf.Abs(p.z)<8||(radius>51&&radius<66)||slots.Any(s=>Mathf.Abs(s.x-p.x)<9&&Mathf.Abs(s.z-p.z)<9)||OpenArchiveImprovements.WaterField(p)<3)continue;
                    RaycastHit hit;Vector3 world=root.TransformPoint(p);
                    if(!Physics.Raycast(world+Vector3.up*35,Vector3.down,out hit,60,~0,QueryTriggerInteraction.Ignore))continue;
                    p=root.InverseTransformPoint(hit.point);if(p.y<-.05f||p.y>.5f)continue;
                    int i=count%4;var mesh=source[i];float height=.20f+(float)rng.NextDouble()*.22f;
                    float scale=height/Mathf.Max(.1f,mesh.bounds.size.y);
                    p.y-=mesh.bounds.min.y*scale;
                    pieces.Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.TRS(p,Quaternion.Euler(0,a*Mathf.Rad2Deg,0),Vector3.one*scale)});count++;
                }
            }
            if(pieces.Count==0)throw new InvalidOperationException("No safe understory positions found.");
            var combined=new Mesh{name="Archive_SparseFlowersAndMushrooms"};combined.CombineMeshes(pieces.ToArray(),true,true);
            string meshPath=Folder+"/Meshes/Archive_SparseFlowersAndMushrooms.asset";
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if(existing!=null){Object.DestroyImmediate(combined);combined=existing;}else AssetDatabase.CreateAsset(combined,meshPath);
            var go=new GameObject("Sparse_Flowers_And_Mushrooms",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(group.transform,false);
            go.GetComponent<MeshFilter>().sharedMesh=combined;var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=nature;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;
            GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.BatchingStatic|StaticEditorFlags.OccludeeStatic);
        }

        // Opaque, low-poly silhouettes; no colliders, alpha cards, lights or runtime scripts.
        static void AddForestFloor(Transform root){
            var parent=root.Find("Environment/Art_Understory");var old=parent.Find("Ferns_Roots_And_Leaves");
            if(old==null||old.GetComponent<MeshFilter>().sharedMesh.vertexCount==0){
            var geo=new ArtGeometry();var rng=new System.Random(620);
            var terrain=root.Find("Environment/Terrain/Island_Grassland_WithRiverHoles").GetComponent<MeshCollider>();
            var slots=root.Find("FutureStructureSlots").Cast<Transform>().Select(t=>root.InverseTransformPoint(t.position)).ToArray();
            int clusters=0;
            foreach(var tree in root.Find("Environment/Trees").Cast<Transform>().Where(t=>t.name.StartsWith("Archive_Tree"))){
                var p=root.InverseTransformPoint(tree.position);float radius=new Vector2(p.x,p.z).magnitude;
                if(clusters>=28)break;
                if(radius<43||Mathf.Abs(p.x)<9||Mathf.Abs(p.z)<9||(radius>50&&radius<67)||slots.Any(s=>Mathf.Abs(s.x-p.x)<10&&Mathf.Abs(s.z-p.z)<10)||OpenArchiveImprovements.WaterField(p)<4)continue;
                RaycastHit ground;if(!terrain.Raycast(new Ray(root.TransformPoint(p)+Vector3.up*35,Vector3.down),out ground,60))continue;
                p=root.InverseTransformPoint(ground.point);if(p.y<-.05f||p.y>.3f)continue;
                // Tapered roots lie against the existing trunk and ground.
                for(int k=0;k<3;k++){
                    float a=k*Mathf.PI*2/3+clusters;var dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var side=Vector3.Cross(Vector3.up,dir);
                    Vector3 near=p+dir*.35f+Vector3.up*.12f,far=p+dir*(1.6f+(float)rng.NextDouble()*.5f)+Vector3.up*.035f;
                    geo.Tri(near-side*.10f,far,near+Vector3.up*.14f,new Color(.28f,.19f,.10f));
                    geo.Tri(near+Vector3.up*.14f,far,near+side*.10f,new Color(.38f,.28f,.14f));
                }
                var fern=p+new Vector3(1.7f,.03f,.7f);
                for(int k=0;k<7;k++){
                    float a=k*Mathf.PI*2/7;var dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var side=Vector3.Cross(Vector3.up,dir);
                    var end=fern+dir*.58f+Vector3.up*.22f;
                    geo.Tri(fern-side*.015f,end,fern+side*.015f,new Color(.24f,.40f,.11f));
                    for(int leaf=1;leaf<=4;leaf++){
                        float t=leaf/5f;var mid=Vector3.Lerp(fern,end,t)+Vector3.up*Mathf.Sin(t*Mathf.PI)*.14f;
                        float width=(1-t)*.24f;
                        foreach(float sign in new[]{-1f,1f}){
                            var tip=mid+side*sign*width-dir*.05f;
                            geo.Tri(mid-dir*.075f,tip,mid+dir*.075f,new Color(.30f,.47f,.16f));
                            geo.Tri(mid+dir*.075f,tip,mid-dir*.075f,new Color(.30f,.47f,.16f));
                        }
                    }
                }
                for(int k=0;k<10;k++){
                    float a=(float)rng.NextDouble()*Mathf.PI*2;var dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var side=Vector3.Cross(Vector3.up,dir);
                    var center=p+dir*(.7f+(float)rng.NextDouble()*1.5f)+Vector3.up*.025f;
                    var color=Color.Lerp(new Color(.52f,.31f,.09f),new Color(.65f,.54f,.17f),(float)rng.NextDouble());
                    geo.Tri(center-dir*.09f,center+side*.04f,center+dir*.10f,color);geo.Tri(center-dir*.09f,center+dir*.10f,center-side*.04f,color);
                }
                clusters++;
            }
            SaveArtMesh(parent,"Ferns_Roots_And_Leaves",geo,forest);
            }
            // Muted geometric carvings on the existing arch legs; +0.02m surface offset.
            var runes=new ArtGeometry();int symbol=0;
            foreach(var arch in root.Find("UniversalMonsterNest").Cast<Transform>().Where(t=>t.name=="Arch_Wide")){
                var mf=arch.GetComponent<MeshFilter>();var b=mf.sharedMesh.bounds;var matrix=root.worldToLocalMatrix*arch.localToWorldMatrix;
                float x=b.min.x+b.size.x*.075f,z=b.max.z+.02f,y=b.min.y+b.size.y*.37f;
                var lines=new List<Vector2[]>();
                if(symbol%2==0){lines.Add(new[]{new Vector2(-.45f,.7f),new Vector2(0,-.7f),new Vector2(.45f,.7f)});lines.Add(new[]{new Vector2(-.26f,.12f),new Vector2(.26f,.12f)});}
                else{lines.Add(new[]{new Vector2(-.4f,.7f),new Vector2(.4f,.7f),new Vector2(.4f,-.7f),new Vector2(-.4f,-.7f)});lines.Add(new[]{new Vector2(-.4f,0),new Vector2(.4f,0)});}
                foreach(var line in lines)for(int n=1;n<line.Length;n++){
                    Vector3 a=new Vector3(x+line[n-1].x,y+line[n-1].y,z),c=new Vector3(x+line[n].x,y+line[n].y,z);var side=Vector3.Cross((c-a).normalized,Vector3.forward)*.026f;
                    var tint=new Color(.26f,.32f,.27f);int steps=Mathf.CeilToInt(Vector3.Distance(a,c)/.10f);
                    for(int step=0;step<steps;step++){
                        var from=Vector3.Lerp(a,c,(float)step/steps);var to=Vector3.Lerp(a,c,(float)(step+1)/steps);
                        Vector3 v0=FrontSurface(mf.sharedMesh,from-side),v1=FrontSurface(mf.sharedMesh,to-side),v2=FrontSurface(mf.sharedMesh,to+side),v3=FrontSurface(mf.sharedMesh,from+side);
                        runes.Tri(matrix.MultiplyPoint3x4(v0),matrix.MultiplyPoint3x4(v2),matrix.MultiplyPoint3x4(v1),tint);runes.Tri(matrix.MultiplyPoint3x4(v0),matrix.MultiplyPoint3x4(v3),matrix.MultiplyPoint3x4(v2),tint);
                    }
                }
                symbol++;
            }
            SaveArtMesh(parent,"Weathered_Math_Carvings",runes,stone);
        }
        static void SaveArtMesh(Transform parent,string name,ArtGeometry geo,Material material){
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Meshes/"+name+".asset");
            if(mesh==null){mesh=geo.Make(name);AssetDatabase.CreateAsset(mesh,Folder+"/Meshes/"+name+".asset");}
            else if(mesh.vertexCount==0||name=="Weathered_Math_Carvings"){geo.Write(mesh);EditorUtility.SetDirty(mesh);}
            var existing=parent.Find(name);var go=existing!=null?existing.gameObject:new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));if(existing==null)go.transform.SetParent(parent,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;
            var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.BatchingStatic|StaticEditorFlags.OccludeeStatic);
        }
        sealed class ArtGeometry{
            readonly List<Vector3> vertices=new List<Vector3>();readonly List<Color> colors=new List<Color>();readonly List<int> indices=new List<int>();
            public void Tri(Vector3 a,Vector3 b,Vector3 c,Color color){int start=vertices.Count;vertices.AddRange(new[]{a,b,c});colors.AddRange(new[]{color.linear,color.linear,color.linear});indices.AddRange(new[]{start,start+1,start+2});}
            public Mesh Make(string name){var m=new Mesh{name=name};Write(m);return m;}
            public void Write(Mesh m){m.Clear();m.SetVertices(vertices);m.SetColors(colors);m.SetTriangles(indices,0);m.uv=new Vector2[vertices.Count];m.RecalculateNormals();m.RecalculateBounds();}
        }
        static Vector3 FrontSurface(Mesh mesh,Vector3 p){
            // Project the carved strokes onto the actual fractured pillar facets.
            var v=mesh.vertices;var triangles=mesh.triangles;float front=float.NegativeInfinity;
            for(int i=0;i<triangles.Length;i+=3){
                var a=v[triangles[i]];var b=v[triangles[i+1]];var c=v[triangles[i+2]];
                float d=(b.y-c.y)*(a.x-c.x)+(c.x-b.x)*(a.y-c.y);if(Mathf.Abs(d)<.00001f)continue;
                float u=((b.y-c.y)*(p.x-c.x)+(c.x-b.x)*(p.y-c.y))/d;
                float w=((c.y-a.y)*(p.x-c.x)+(a.x-c.x)*(p.y-c.y))/d;
                if(u>=-.0001f&&w>=-.0001f&&u+w<=1.0001f)front=Mathf.Max(front,a.z*u+b.z*w+c.z*(1-u-w));
            }
            if(!float.IsNegativeInfinity(front))p.z=front+.012f;return p;
        }

        public static void Capture(string name,Vector3 p,Vector3 target,bool orthographic=false,float size=62){
            Directory.CreateDirectory(Folder+"/Previews");var go=new GameObject("Temporary_Art_Review");var camera=go.AddComponent<Camera>();camera.enabled=false;
            camera.transform.position=p;camera.transform.LookAt(target);camera.orthographic=orthographic;camera.orthographicSize=size;camera.fieldOfView=62;camera.nearClipPlane=.15f;camera.farClipPlane=1000;
            var rt=RenderTexture.GetTemporary(1440,900,24,RenderTextureFormat.ARGB32);var previous=RenderTexture.active;var pixels=new Texture2D(1440,900,TextureFormat.RGB24,false);
            bool async=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
            try{camera.targetTexture=rt;camera.Render();camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1440,900),0,0);pixels.Apply();File.WriteAllBytes(Folder+"/Previews/"+name+".png",pixels.EncodeToPNG());}
            finally{camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(pixels);Object.DestroyImmediate(go);ShaderUtil.allowAsyncCompilation=async;}
        }
        [MenuItem("Tools/Logic Legends/Open Archive/Art/Render upgraded scene")]
        public static void Review(){
            var root=Root(SceneManager.GetActiveScene());float s=root.lossyScale.x;
            Capture("Archive_Afternoon",root.TransformPoint(new Vector3(0,7.5f,-82)),root.TransformPoint(new Vector3(0,4,-10)));
            Capture("Archive_Nest",root.TransformPoint(new Vector3(22,14,-24)),root.TransformPoint(new Vector3(0,1.3f,0)));
            Capture("Archive_StoneAndWater",root.TransformPoint(new Vector3(37,5,-42)),root.TransformPoint(new Vector3(24,0,-27)));
            Capture("Archive_Aerial",root.TransformPoint(new Vector3(80,200,-150)),root.position,true,108*s);
        }
    }
}

