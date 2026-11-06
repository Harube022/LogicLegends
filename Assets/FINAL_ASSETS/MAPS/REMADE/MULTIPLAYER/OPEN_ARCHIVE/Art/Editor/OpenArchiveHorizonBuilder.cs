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

namespace LogicLegends.OpenArchive.Art.Editor
{
    /// <summary>Render-only scenery outside the existing playable island. All geometry is baked in Edit Mode.</summary>
    public static class OpenArchiveHorizonBuilder
    {
        public const string Folder="Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Art/Horizon";
        public const string RootName="Archive_Distant_Archipelago";
        const string ScenePath="Assets/Scenes/MultiplayerGameplay.unity";
        const string PrefabPath=Folder+"/Archive_Distant_Archipelago.prefab";
        static readonly Color Rock=new Color(.39f,.48f,.47f),HighRock=new Color(.53f,.61f,.62f),Moss=new Color(.29f,.45f,.30f);
        static readonly Color Leaves=new Color(.19f,.36f,.25f),Stone=new Color(.53f,.60f,.56f),Gold=new Color(.56f,.49f,.29f);

        [Serializable] public sealed class Report
        {
            public bool passed;
            public string scene,scope="Eight opaque, combined scenery chunks; distant islands, submerged rock bases, tree silhouettes and ruined sanctuaries. Edit-time generated meshes only; no gameplay components, physics, navigation, lights, particles or animation.";
            public int chunks,triangles,vertices,collidersBefore,collidersAfter,navObstaclesBefore,navObstaclesAfter;
            public float seaHeight,minimumVertexRadius,maximumVertexRadius,gameplayCameraFar;
            public string[] errors;
        }

        [MenuItem("Tools/Logic Legends/Open Archive/Environment/Build Distant Archipelago")]
        public static Report Install()
        {
            var scene=SceneManager.GetActiveScene();
            if(Application.isPlaying||scene.isDirty||scene.path!=ScenePath)
                throw new InvalidOperationException("Open the clean saved MultiplayerGameplay scene outside Play Mode.");
            var roots=scene.GetRootGameObjects();
            var sea=roots.SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>(true)).FirstOrDefault(r=>r.name=="Perimeter_Sea");
            if(sea==null)throw new InvalidOperationException("The existing perimeter sea is required.");
            float seaY=sea.bounds.center.y;
            int colliders=roots.Sum(g=>g.GetComponentsInChildren<Collider>(true).Length);
            int obstacles=roots.Sum(g=>g.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true).Length);
            Directory.CreateDirectory(Folder+"/Meshes");AssetDatabase.Refresh();
            var shader=AssetDatabase.LoadAssetAtPath<Shader>(Folder+"/ArchiveHorizon.shader");
            if(shader==null||!shader.isSupported||ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("The distant scenery URP shader must compile first.");
            var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Archive_Horizon_Landscape.mat");
            if(material==null){material=new Material(shader){name="Archive_Horizon_Landscape",enableInstancing=true};AssetDatabase.CreateAsset(material,Folder+"/Archive_Horizon_Landscape.mat");}
            material.SetColor("_Tint",Color.white);material.SetFloat("_Haze",.48f);material.SetFloat("_NightHaze",.70f);EditorUtility.SetDirty(material);
            var surface=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Art/Textures/Archive_NaturalSurfaceMasks.png");
            if(surface==null||surface.mipmapCount<2)throw new InvalidOperationException("The existing mipmapped natural surface mask is required.");
            material.SetTexture("_SurfaceMap",surface);material.SetFloat("_SurfaceScale",.035f);material.SetFloat("_DetailStrength",.65f);
            var previewScene=EditorSceneManager.NewPreviewScene();
            var temporary=new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(temporary,previewScene);
            try
            {
                var random=new System.Random(1052026);
                float[] angles={9,56,103,150,197,244,291,338};
                float[] radii={468,500,475,515,485,505,470,500};
                float[] heights={64,104,52,118,72,88,57,106};
                for(int i=0;i<angles.Length;i++)
                {
                    float angle=angles[i]*Mathf.Deg2Rad;
                    var center=new Vector3(Mathf.Sin(angle)*radii[i],seaY,Mathf.Cos(angle)*radii[i]);
                    var mesh=new Geometry();float width=Range(random,76,110),depth=Range(random,45,69);
                    int seed=170+i*31;
                    var rotation=Quaternion.Euler(0,angles[i]+90+Range(random,-12,12),0);
                    var land=new Landform(width,depth,heights[i],seed);
                    AddIsland(mesh,land,Vector3.zero,Quaternion.identity);
                    int terrainIndices=mesh.IndexCount;
                    var sanctuaryAnchor=mesh.HighestPoint(terrainIndices);
                    int planted=0;
                    for(int attempt=0;attempt<85&&planted<28;attempt++)
                    {
                        float u=(attempt%2==0?-.31f:.30f)+Range(random,-.30f,.30f),v=Range(random,-.48f,.48f);
                        if((i==1||i==5)&&new Vector2(u*width-sanctuaryAnchor.x,v*depth-sanctuaryAnchor.z).sqrMagnitude<24*24)continue;
                        if(!mesh.GroundHeight(u*width,v*depth,terrainIndices,out float h,out float up)||h<9||up<.57f)continue;
                        var position=new Vector3(u*width,h,v*depth);
                        AddTree(mesh,position,Range(random,6.5f,12.5f),planted%3!=0,random);planted++;
                    }
                    // Seaward rock stacks add a nearer layer between the large islands.
                    var outward=Quaternion.Inverse(rotation)*new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle));
                    for(int stack=0;stack<2;stack++)
                    {
                        var point=-outward*(radii[i]-Range(random,350,390))+Vector3.Cross(Vector3.up,outward)*(stack==0?-39:31);
                        var rock=new Landform(Range(random,12,20),Range(random,11,17),Range(random,26,48),seed+stack+200);
                        AddIsland(mesh,rock,point,Quaternion.Euler(0,Range(random,0,360),0),true);
                    }
                    if(i==1||i==5)AddSanctuary(mesh,sanctuaryAnchor,i==1);
                    if(i==3)AddSeaArch(mesh,-outward*84+Vector3.Cross(Vector3.up,outward)*115,Quaternion.LookRotation(outward));
                    var asset=SaveMesh(mesh,"Archive_Horizon_Sector_"+(i+1));
                    var child=new GameObject("Horizon_"+(i+1)+"_"+(i==1?"Sunken_Observatory":i==5?"Lost_Archive":i==3?"Stone_Gateway":"Forested_Isles"),typeof(MeshFilter),typeof(MeshRenderer));
                    child.transform.SetParent(temporary.transform,false);child.transform.SetPositionAndRotation(center,rotation);
                    child.GetComponent<MeshFilter>().sharedMesh=asset;
                    var renderer=child.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                    renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;renderer.motionVectorGenerationMode=MotionVectorGenerationMode.ForceNoMotion;
                    GameObjectUtility.SetStaticEditorFlags(child,StaticEditorFlags.BatchingStatic);
                }
                if(temporary.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3)>20000)
                    throw new InvalidOperationException("The scenery triangle budget must pass before replacing the saved setup.");
                var prefab=PrefabUtility.SaveAsPrefabAsset(temporary,PrefabPath);
                if(prefab==null)throw new InvalidOperationException("Distant scenery prefab was not saved.");
                var existing=roots.FirstOrDefault(g=>g.name==RootName);if(existing!=null)Object.DestroyImmediate(existing);
                var installed=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);installed.name=RootName;installed.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
                var report=Verify();report.collidersBefore=colliders;report.navObstaclesBefore=obstacles;
                report.passed&=report.collidersAfter==colliders&&report.navObstaclesAfter==obstacles;
                File.WriteAllText(Folder+"/HorizonVerification.json",JsonUtility.ToJson(report,true));AssetDatabase.ImportAsset(Folder+"/HorizonVerification.json");
                if(!report.passed)throw new InvalidOperationException("Distant scenery verification failed: "+string.Join("; ",report.errors??Array.Empty<string>()));
                return report;
            }
            finally{Object.DestroyImmediate(temporary);EditorSceneManager.ClosePreviewScene(previewScene);}
        }

        public static Report Verify()
        {
            var scene=SceneManager.GetActiveScene();var root=scene.GetRootGameObjects().FirstOrDefault(g=>g.name==RootName);
            var report=new Report{scene=scene.path,minimumVertexRadius=float.MaxValue};var errors=new List<string>();
            if(root==null)throw new InvalidOperationException("The authored archipelago is missing.");
            var filters=root.GetComponentsInChildren<MeshFilter>();report.chunks=filters.Length;
            foreach(var filter in filters)
            {
                var mesh=filter.sharedMesh;report.vertices+=mesh.vertexCount;report.triangles+=mesh.triangles.Length/3;
                foreach(var point in mesh.vertices)
                {
                    var world=filter.transform.TransformPoint(point);float radius=new Vector2(world.x,world.z).magnitude;
                    report.minimumVertexRadius=Mathf.Min(report.minimumVertexRadius,radius);report.maximumVertexRadius=Mathf.Max(report.maximumVertexRadius,radius);
                    if(!float.IsFinite(world.x)||!float.IsFinite(world.y)||!float.IsFinite(world.z))errors.Add("Non-finite mesh vertex.");
                }
            }
            var sea=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>(true)).First(r=>r.name=="Perimeter_Sea");report.seaHeight=sea.bounds.center.y;
            report.collidersAfter=scene.GetRootGameObjects().Sum(g=>g.GetComponentsInChildren<Collider>(true).Length);
            report.navObstaclesAfter=scene.GetRootGameObjects().Sum(g=>g.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true).Length);
            var camera=Camera.main;report.gameplayCameraFar=camera==null?0:camera.farClipPlane;
            if(filters.Length!=8||report.triangles>20000)errors.Add("Unexpected scenery count or triangle budget.");
            if(report.minimumVertexRadius<285||report.maximumVertexRadius>650)errors.Add("Scenery must stay beyond the protected playable perimeter and within the authored horizon radius.");
            if(root.GetComponentsInChildren<MonoBehaviour>(true).Length!=0||root.GetComponentsInChildren<Collider>(true).Length!=0||root.GetComponentsInChildren<Light>(true).Length!=0||root.GetComponentsInChildren<ParticleSystem>(true).Length!=0)errors.Add("The background must remain render-only.");
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())
                if(renderer.sharedMaterials.Length!=1||renderer.shadowCastingMode!=ShadowCastingMode.Off||ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader))errors.Add("Unexpected rendering cost or shader error.");
            report.errors=errors.Distinct().ToArray();report.passed=errors.Count==0;return report;
        }

        static float Range(System.Random random,float a,float b)=>Mathf.Lerp(a,b,(float)random.NextDouble());
        sealed class Landform
        {
            public readonly float width,depth,height;readonly int seed;
            public Landform(float width,float depth,float height,int seed){this.width=width;this.depth=depth;this.height=height;this.seed=seed;}
            public float Edge(float angle)=>1+.095f*Mathf.Sin(angle*3+seed)+.055f*Mathf.Cos(angle*7+seed*.4f);
            public float Height(float u,float v)
            {
                float radius=Mathf.Sqrt(u*u+v*v);float envelope=Mathf.Pow(Mathf.Clamp01(1-radius*radius),.55f);
                float peak=Mathf.Max(Mathf.Exp(-((u+.38f)*(u+.38f)*5+(v-.13f)*(v-.13f)*7)),
                    .85f*Mathf.Exp(-((u-.35f)*(u-.35f)*7+(v+.14f)*(v+.14f)*9)));
                float noise=Mathf.PerlinNoise(u*2.3f+seed,v*2.3f+seed*.73f);
                return -4+height*envelope*(.42f+.58f*peak+.16f*noise+.045f*Mathf.Sin(u*11+v*7+seed));
            }
        }
        static void AddIsland(Geometry mesh,Landform land,Vector3 center,Quaternion rotation,bool stack=false)
        {
            int segments=stack?16:32;float[] rings=stack?new[]{.18f,.50f,.83f,1}:new[]{.15f,.36f,.58f,.78f,.92f,1};
            int middle=mesh.Vertex(center+rotation*new Vector3(0,land.Height(0,0),0),stack?Rock:HighRock);
            var previous=new int[segments];
            for(int ring=0;ring<rings.Length;ring++)
            {
                var current=new int[segments];
                for(int i=0;i<segments;i++)
                {
                    float angle=i*Mathf.PI*2/segments,r=rings[ring],edge=land.Edge(angle);
                    float u=Mathf.Cos(angle)*r,v=Mathf.Sin(angle)*r,h=land.Height(u,v);
                    if(stack)h=r<1?land.height*(.80f+.13f*Mathf.PerlinNoise(u*2+land.width,v*2+land.depth)-r*.12f):-4;
                    var p=new Vector3(u*land.width*edge,h,v*land.depth*edge);
                    Color tint=stack?Color.Lerp(Rock,HighRock,Mathf.Clamp01(h/land.height)*.60f):
                        h<7?Color.Lerp(Rock,HighRock,.16f):Color.Lerp(Moss,HighRock,Mathf.SmoothStep(.24f,.80f,h/land.height));
                    tint*=.92f+.08f*Mathf.PerlinNoise(u*2.3f+land.width,v*2.3f+land.depth);tint.a=1;
                    current[i]=mesh.Vertex(center+rotation*p,tint);
                }
                for(int i=0;i<segments;i++)
                {
                    int j=(i+1)%segments;
                    if(ring==0)mesh.Indices(middle,current[j],current[i]);
                    else{mesh.Indices(previous[i],previous[j],current[j]);mesh.Indices(previous[i],current[j],current[i]);}
                }
                previous=current;
            }
            int bottom=mesh.Vertex(center+Vector3.down*28,Rock*.7f);
            for(int i=0;i<segments;i++)mesh.Indices(bottom,previous[i],previous[(i+1)%segments]);
            // Thin opaque shoreline accents are outside the rock footprint at the
            // actual existing sea height, not transparent cards or floating islands.
            for(int i=0;i<segments;i++)
            {
                if(i%8>3)continue;
                float a=i*Mathf.PI*2/segments,b=(i+.65f)*Mathf.PI*2/segments;
                Vector3 P(float angle,float radius)=>center+rotation*new Vector3(Mathf.Cos(angle)*land.width*land.Edge(angle)*radius,.12f,Mathf.Sin(angle)*land.depth*land.Edge(angle)*radius);
                mesh.Quad(P(a,1.001f),P(b,1.001f),P(b,1.013f),P(a,1.013f),new Color(.34f,.60f,.61f));
            }
        }
        static void AddTree(Geometry mesh,Vector3 ground,float size,bool broadleaf,System.Random random)
        {
            mesh.Prism(ground-Vector3.up*.45f,size*.075f,size*.038f,size*.67f,5,new Color(.26f,.30f,.23f));
            var tint=Color.Lerp(Leaves,Moss,Range(random,0,.42f));
            if(!broadleaf)
            {
                for(int i=0;i<2;i++)mesh.Cone(ground+Vector3.up*(size*(.26f+i*.29f)),size*(.40f-i*.10f),size*.62f,6,tint*(1+i*.06f));
            }
            else
            {
                mesh.Crown(ground+new Vector3(0,size*.79f,0),new Vector3(size*.52f,size*.47f,size*.44f),tint);
                mesh.Crown(ground+new Vector3(size*.27f,size*.67f,0),new Vector3(size*.36f,size*.33f,size*.34f),tint*.94f);
            }
        }
        static void AddSanctuary(Geometry mesh,Vector3 ridge,bool observatory)
        {
            // Place the foundation on the actual baked ridge rather than the
            // analytical valley height, so the ruins cannot be buried or float.
            var center=ridge+Vector3.up*.65f;
            mesh.Prism(center-Vector3.up*38,19,18,38.2f,12,Rock);mesh.Prism(center,18,17.5f,.65f,12,Stone);
            int count=observatory?8:6;
            for(int i=0;i<count;i++)
            {
                float angle=i*Mathf.PI*2/count;var p=center+new Vector3(Mathf.Sin(angle)*14,.65f,Mathf.Cos(angle)*14);
                float height=i%3==1?7:15;
                mesh.Prism(p,1.8f,1.35f,height,6,Stone);mesh.Prism(p,2.4f,2.35f,1.1f,6,Stone*.83f);
                if(height>10){mesh.Prism(p+Vector3.up*(height-1),2,2,.6f,6,Gold);mesh.Prism(p+Vector3.up*height,2.3f,2.5f,1.2f,6,Stone);}
            }
            if(observatory)
            {
                mesh.Arch(center+Vector3.up*15,16,2.1f,1.8f,Quaternion.Euler(0,45,0),Stone,0,143);
                mesh.Arch(center+Vector3.up*15,16,2.1f,1.8f,Quaternion.Euler(0,135,0),Stone,20,150);
            }
            else
            {
                mesh.Arch(center+Vector3.up*15,14,2.4f,2.6f,Quaternion.Euler(0,45,0),Stone,0,160);
                mesh.Box(center+new Vector3(-12,2,-8),new Vector3(8,4,3),Quaternion.Euler(0,18,0),Rock);
            }
        }
        static void AddSeaArch(Geometry mesh,Vector3 origin,Quaternion rotation)
        {
            for(int side=-1;side<=1;side+=2)
            {
                var p=origin+rotation*new Vector3(side*17,-9,0);
                mesh.Prism(p,10,6.8f,43,7,Rock);
            }
            mesh.Arch(origin+Vector3.up*31,17,7,11,rotation,Rock,0,180);
        }
        static Mesh SaveMesh(Geometry source,string name)
        {
            string path=Folder+"/Meshes/"+name+".asset";var target=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            var built=source.Build(name);
            if(target==null){AssetDatabase.CreateAsset(built,path);return built;}
            // Keep asset references stable while refreshing Unity's native/GPU
            // mesh buffers as well as its serialized data on a rebuild.
            target.Clear();target.SetVertices(built.vertices);target.SetColors(built.colors);target.SetTriangles(built.triangles,0);
            target.SetNormals(built.normals);target.bounds=built.bounds;target.UploadMeshData(false);
            Object.DestroyImmediate(built);EditorUtility.SetDirty(target);return target;
        }
        sealed class Geometry
        {
            readonly List<Vector3> vertices=new List<Vector3>();readonly List<Color> colors=new List<Color>();readonly List<int> triangles=new List<int>();
            public int IndexCount=>triangles.Count;
            public int Vertex(Vector3 point,Color color){int index=vertices.Count;vertices.Add(point);colors.Add(color.linear);return index;}
            public Vector3 HighestPoint(int indexLimit)
            {
                Vector3 highest=Vector3.down*10000;
                for(int i=0;i<indexLimit;i++)if(vertices[triangles[i]].y>highest.y)highest=vertices[triangles[i]];
                return highest;
            }
            public bool GroundHeight(float x,float z,int indexLimit,out float height,out float up)
            {
                height=float.NegativeInfinity;up=0;
                for(int i=0;i<indexLimit;i+=3)
                {
                    var a=vertices[triangles[i]];var b=vertices[triangles[i+1]];var c=vertices[triangles[i+2]];
                    float d=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);if(Mathf.Abs(d)<.00001f)continue;
                    float u=((b.z-c.z)*(x-c.x)+(c.x-b.x)*(z-c.z))/d;
                    float v=((c.z-a.z)*(x-c.x)+(a.x-c.x)*(z-c.z))/d;
                    if(u<-.0001f||v<-.0001f||u+v>1.0001f)continue;
                    float y=u*a.y+v*b.y+(1-u-v)*c.y;
                    if(y<=height)continue;height=y;up=Vector3.Cross(b-a,c-a).normalized.y;
                }
                return float.IsFinite(height);
            }
            public void Indices(int a,int b,int c){triangles.Add(a);triangles.Add(b);triangles.Add(c);}
            public void Triangle(Vector3 a,Vector3 b,Vector3 c,Color color){int start=Vertex(a,color);Vertex(b,color);Vertex(c,color);Indices(start,start+1,start+2);}
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color){Triangle(a,b,c,color);Triangle(a,c,d,color);}
            public void Prism(Vector3 bottom,float lower,float upper,float height,int sides,Color color)
            {
                for(int i=0;i<sides;i++)
                {
                    float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;
                    var p=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var q=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b));
                    Quad(bottom+p*lower,bottom+Vector3.up*height+p*upper,bottom+Vector3.up*height+q*upper,bottom+q*lower,color);
                    Triangle(bottom,bottom+p*lower,bottom+q*lower,color);Triangle(bottom+Vector3.up*height,bottom+Vector3.up*height+q*upper,bottom+Vector3.up*height+p*upper,color);
                }
            }
            public void Cone(Vector3 bottom,float radius,float height,int sides,Color color)
            {
                for(int i=0;i<sides;i++)
                {
                    float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;
                    var p=bottom+new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius);var q=bottom+new Vector3(Mathf.Cos(b)*radius,0,Mathf.Sin(b)*radius);
                    Triangle(p,bottom+Vector3.up*height,q,color);Triangle(bottom,p,q,color);
                }
            }
            public void Crown(Vector3 center,Vector3 size,Color color)
            {
                var top=center+Vector3.up*size.y;var bottom=center-Vector3.up*size.y*.7f;
                for(int i=0;i<7;i++)
                {
                    float a=i*Mathf.PI*2/7,b=(i+1)*Mathf.PI*2/7;
                    var p=center+new Vector3(Mathf.Cos(a)*size.x,0,Mathf.Sin(a)*size.z);var q=center+new Vector3(Mathf.Cos(b)*size.x,0,Mathf.Sin(b)*size.z);
                    Triangle(p,top,q,color);Triangle(q,bottom,p,color*.87f);
                }
            }
            public void Box(Vector3 center,Vector3 size,Quaternion rotation,Color color)
            {
                Vector3 P(float x,float y,float z)=>center+rotation*Vector3.Scale(new Vector3(x,y,z),size*.5f);
                Quad(P(-1,-1,-1),P(-1,1,-1),P(1,1,-1),P(1,-1,-1),color);Quad(P(1,-1,1),P(1,1,1),P(-1,1,1),P(-1,-1,1),color);
                Quad(P(-1,-1,1),P(-1,1,1),P(-1,1,-1),P(-1,-1,-1),color);Quad(P(1,-1,-1),P(1,1,-1),P(1,1,1),P(1,-1,1),color);
                Quad(P(-1,1,-1),P(-1,1,1),P(1,1,1),P(1,1,-1),color);Quad(P(-1,-1,1),P(-1,-1,-1),P(1,-1,-1),P(1,-1,1),color);
            }
            public void Arch(Vector3 center,float radius,float thickness,float depth,Quaternion rotation,Color color,float from,float to)
            {
                int segments=Mathf.CeilToInt((to-from)/15);
                Vector3 P(float angle,float r,float z)=>center+rotation*new Vector3(Mathf.Cos(angle)*r,Mathf.Sin(angle)*r,z);
                for(int i=0;i<segments;i++)
                {
                    float a=Mathf.Lerp(from,to,(float)i/segments)*Mathf.Deg2Rad,b=Mathf.Lerp(from,to,(float)(i+1)/segments)*Mathf.Deg2Rad;
                    float inner=radius-thickness*.5f,outer=radius+thickness*.5f,z=depth*.5f;
                    Quad(P(a,inner,-z),P(b,inner,-z),P(b,outer,-z),P(a,outer,-z),color);
                    Quad(P(a,outer,z),P(b,outer,z),P(b,inner,z),P(a,inner,z),color);
                    Quad(P(a,inner,z),P(b,inner,z),P(b,inner,-z),P(a,inner,-z),color*.9f);
                    Quad(P(a,outer,-z),P(b,outer,-z),P(b,outer,z),P(a,outer,z),color);
                    if(i==0)Quad(P(a,inner,z),P(a,inner,-z),P(a,outer,-z),P(a,outer,z),color);
                    if(i==segments-1)Quad(P(b,inner,-z),P(b,inner,z),P(b,outer,z),P(b,outer,-z),color);
                }
            }
            public Mesh Build(string name){var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;}
        }
    }
}
