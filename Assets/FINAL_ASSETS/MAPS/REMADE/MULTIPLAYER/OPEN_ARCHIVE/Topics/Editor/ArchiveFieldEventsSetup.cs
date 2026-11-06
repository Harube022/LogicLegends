using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>Authors reusable field scenery against the actual saved road and ground geometry.</summary>
    public static class ArchiveFieldEventsSetup
    {
        public const string Folder=ArchiveTopicSetup.Folder+"/Environment/FieldEvents";
        public const string RootName="Archive_Field_Event_Presentation";
        public const string BeaconPrefabPath=Folder+"/Archive_Resonance_Beacon.prefab";
        [Serializable] public sealed class Report
        {
            public bool passed;
            public ArchiveResonanceBeaconView[] beacons;
            public ArchivePathDisturbanceView[] hazards;
            public Vector3[] beaconPositions,hazardPositions;
            public int beaconTriangles,collidersBefore,collidersAfter;
            public float minimumSafeLane=3;
            public string scope="Four colliderless resonance pylons and eight dry main-ring roadside footprints. One 3.5m by 7m margin leaves a separately sampled 3m paved bypass. No physics, navigation, academic progress, health or encounter state is authored.";
        }
        public static Report LastReport {get;private set;}
        struct Pose {public Vector3 position;public Quaternion rotation;}
        struct RoadTriangle {public Vector2 a,b,c;public Rect bounds;}

        [MenuItem("Tools/Logic Legends/Open Archive/Environment/Install Resonance Field Presentation")]
        public static Report Install()
        {
            var scene=SceneManager.GetActiveScene();
            if(Application.isPlaying||scene.isDirty||scene.path!="Assets/Scenes/MultiplayerGameplay.unity")
                throw new InvalidOperationException("Use the saved production MultiplayerGameplay scene outside Play Mode.");
            var report=Build(Object.FindFirstObjectByType<ArchiveTopicSession>(),null);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();return report;
        }

        /// <summary>Returns references for the existing state owner to wire. Does not create or modify a gameplay owner.</summary>
        public static Report Build(ArchiveTopicSession session,Transform parent)
        {
            if(Application.isPlaying)throw new InvalidOperationException("Author field-event presentation in stopped Edit Mode.");
            if(session==null||session.stations==null||session.stations.Length!=10)throw new InvalidOperationException("All ten existing topic stations are required.");
            var scene=session.gameObject.scene;
            var map=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="OpenArchive");
            if(map==null)throw new InvalidOperationException("The production OpenArchive map is required.");
            var environment=map.transform.Find("Environment");
            var ring=environment==null?null:environment.Find("MainPaths/Continuous_Main_Ring_10m");
            if(ring==null)throw new InvalidOperationException("The existing main ring road is required.");
            var roads=new Roads(ring.GetComponent<MeshFilter>());
            var placement=new ArchiveWayfindingPlacement(environment);
            var ground=new Ground(scene);
            var bridges=environment.Find("Bridges");
            var bridgeAnchors=bridges==null?Array.Empty<Transform>():bridges.Cast<Transform>().Where(t=>t.name.StartsWith("Bridge_Modular_",StringComparison.Ordinal)).ToArray();
            var old=parent!=null?parent.Find(RootName):scene.GetRootGameObjects().FirstOrDefault(g=>g.name==RootName)?.transform;
            var beacons=BeaconPoses(session,map.transform,placement,ground,bridgeAnchors,old);
            var hazards=HazardPoses(session,map.transform,roads,ground,bridgeAnchors);
            if(beacons.Count!=4||hazards.Count!=8)throw new InvalidOperationException("No fully supported clear field layout: "+beacons.Count+" beacons, "+hazards.Count+" disturbance sites. Existing scenery is unchanged.");
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var stone=AssetDatabase.LoadAssetAtPath<Material>(ArchiveTopicSetup.Folder+"/Art/Materials/Archive_Topic_Stone.mat");
            var brass=AssetDatabase.LoadAssetAtPath<Material>(ArchiveTopicSetup.Folder+"/Art/Materials/Archive_Topic_BrassInlay.mat");
            var shader=AssetDatabase.LoadAssetAtPath<Shader>(Folder+"/ArchiveFieldEnergy.shader");
            if(stone==null||brass==null||shader==null||!shader.isSupported||ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException("Existing stone/brass artwork and the supported field-energy shader are required.");
            var energy=Energy(shader);var crystal=Lit("Archive_Field_Crystal",new Color(.035f,.50f,.44f),new Color(.03f,.65f,.51f));
            var shards=Lit("Archive_Field_Shards",new Color(.18f,.07f,.28f),new Color(.16f,.035f,.23f));
            var prefab=BuildBeaconPrefab(stone,brass,crystal,energy,session.stations[0].font,out int triangles);
            int colliderCount=scene.GetRootGameObjects().Sum(g=>g.GetComponentsInChildren<Collider>(true).Length);
            if(old!=null)Object.DestroyImmediate(old.gameObject);
            var root=new GameObject(RootName).transform;root.SetParent(parent,false);root.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            // World-sized presentation remains independent of the map's 2.5 scale.
            if(parent!=null)root.localScale=new Vector3(1/Mathf.Max(.001f,Mathf.Abs(parent.lossyScale.x)),1/Mathf.Max(.001f,Mathf.Abs(parent.lossyScale.y)),1/Mathf.Max(.001f,Mathf.Abs(parent.lossyScale.z)));
            var beaconViews=new List<ArchiveResonanceBeaconView>();var hazardViews=new List<ArchivePathDisturbanceView>();
            for(int i=0;i<beacons.Count;i++)
            {
                var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,root);go.name="Resonance_Beacon_"+i;
                go.transform.SetPositionAndRotation(beacons[i].position,beacons[i].rotation);
                var view=go.GetComponent<ArchiveResonanceBeaconView>();view.id=i;beaconViews.Add(view);EditorUtility.SetDirty(view);
            }
            for(int i=0;i<hazards.Count;i++)
            {
                var go=new GameObject("Roadside_Disturbance_Site_"+i);go.transform.SetParent(root,false);go.transform.SetPositionAndRotation(hazards[i].position,hazards[i].rotation);
                var view=go.AddComponent<ArchivePathDisturbanceView>();view.id=i;view.halfWidth=1.75f;view.halfLength=3.5f;view.energyMaterial=energy;view.shardMaterial=shards;
                hazardViews.Add(view);EditorUtility.SetDirty(view);
            }
            var report=new Report{beacons=beaconViews.ToArray(),hazards=hazardViews.ToArray(),beaconPositions=beacons.Select(p=>p.position).ToArray(),
                hazardPositions=hazards.Select(p=>p.position).ToArray(),beaconTriangles=triangles,collidersBefore=colliderCount,
                collidersAfter=scene.GetRootGameObjects().Sum(g=>g.GetComponentsInChildren<Collider>(true).Length)};
            report.passed=report.collidersBefore==report.collidersAfter&&root.GetComponentsInChildren<Collider>(true).Length==0&&root.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true).Length==0;
            if(!report.passed)throw new InvalidOperationException("Field presentation unexpectedly changed scene collision.");
            LastReport=report;AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);return report;
        }

        static List<Pose> BeaconPoses(ArchiveTopicSession session,Transform map,ArchiveWayfindingPlacement placement,Ground ground,Transform[] bridges,Transform old)
        {
            var result=new List<Pose>();
            var renderers=session.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Renderer>(true))
                .Where(r=>r.enabled&&r.gameObject.activeInHierarchy&&r.bounds.size.x<18&&r.bounds.size.z<18&&(old==null||!r.transform.IsChildOf(old))).ToArray();
            for(int quadrant=0;quadrant<4;quadrant++)
            {
                bool found=false;
                foreach(float radius in new[]{62.5f,64f,65.5f,61.7f})
                {
                    if(found)break;
                    foreach(float offset in new[]{0f,6f,-6f,12f,-12f,18f,-18f,24f,-24f,30f,-30f})
                    {
                        float a=(quadrant*90+offset)*Mathf.Deg2Rad;
                        var proposed=map.TransformPoint(new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius));
                        if(session.stations.Any(s=>Horizontal(s.transform.position,proposed)<43)||NearBridge(proposed,bridges,18,43)||
                            !placement.ClearOfRoads(proposed,1.7f)||!placement.ClearOfGroundcover(proposed,1.55f)||
                            !ground.Sample(proposed,out var p)||!ground.Footprint(p,Quaternion.identity,1.32f,1.32f))continue;
                        if(!ArchiveWayfindingPlacement.ClearOfProps(p,1.5f))continue;
                        bool occupied=false;
                        foreach(var renderer in renderers)
                        {
                            if(renderer.bounds.max.y<p.y+.6f)continue;
                            var probe=p+Vector3.up*1.1f;
                            if((renderer.bounds.ClosestPoint(probe)-probe).sqrMagnitude<2.5f*2.5f){occupied=true;break;}
                        }
                        if(occupied||result.Any(pose=>Horizontal(pose.position,p)<30))continue;
                        var forward=map.position-p;forward.y=0;
                        result.Add(new Pose{position=p,rotation=Quaternion.LookRotation(forward,Vector3.up)});found=true;break;
                    }
                }
            }
            return result;
        }
        static List<Pose> HazardPoses(ArchiveTopicSession session,Transform map,Roads road,Ground ground,Transform[] bridges)
        {
            var result=new List<Pose>();
            for(int site=0;site<8;site++)
            {
                bool found=false;
                foreach(float offset in new[]{0f,3f,-3f,6f,-6f,9f,-9f,12f,-12f})
                {
                    if(found)break;
                    foreach(float radius in new[]{50.5f,51f,51.5f,52f})
                    {
                        float a=(22.5f+site*45+offset)*Mathf.Deg2Rad;
                        var radial=map.TransformDirection(new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))).normalized;
                        var forward=Vector3.Cross(radial,Vector3.up);var rotation=Quaternion.LookRotation(forward,Vector3.up);
                        var proposed=map.TransformPoint(new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius));
                        if(session.stations.Any(s=>Horizontal(s.transform.position,proposed)<46)||NearBridge(proposed,bridges,18,46)||
                            result.Any(pose=>Horizontal(pose.position,proposed)<24)||!ground.Sample(proposed,out var p)||!ground.Footprint(p,rotation,1.75f,3.5f))continue;
                        // Confirm the entire affected strip and an adjacent 3m-wide
                        // alternate lane lie on the original ring, not just grass.
                        bool paved=true;
                        foreach(float x in new[]{-1.75f,0,1.75f,2.15f,3.65f,5.15f})
                            foreach(float z in new[]{-3.5f,0,3.5f})
                            {
                                var sample=p+rotation*new Vector3(x,0,z);
                                if(!road.Contains(sample)||!ground.Sample(sample,out var support)||Mathf.Abs(support.y-p.y)>.15f){paved=false;break;}
                            }
                        if(!paved||!ArchiveWayfindingPlacement.ClearOfProps(p,2))continue;
                        result.Add(new Pose{position=p,rotation=rotation});found=true;break;
                    }
                }
            }
            return result;
        }
        static float Horizontal(Vector3 a,Vector3 b)=>new Vector2(a.x-b.x,a.z-b.z).magnitude;
        static bool NearBridge(Vector3 p,Transform[] bridges,float side,float along)
        {
            foreach(var bridge in bridges)
            {
                var delta=p-bridge.position;
                if(Mathf.Abs(Vector3.Dot(delta,bridge.right))<side&&Mathf.Abs(Vector3.Dot(delta,bridge.forward))<along)return true;
            }
            return false;
        }
        sealed class Ground
        {
            readonly Collider[] surfaces;
            public Ground(Scene scene)
            {
                surfaces=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Collider>(true)).Where(c=>c.enabled&&!c.isTrigger&&
                    (c.name=="Island_Grassland_WithRiverHoles"||c.name=="Island_Grassland"||c.name=="Smooth_Walkable_Collision"||c.name=="Nest_StoneFloor_30m"||c.name=="Nest_Four_Wide_Landings")).ToArray();
            }
            public bool Sample(Vector3 candidate,out Vector3 p)
            {
                p=candidate;float nearest=float.PositiveInfinity;bool found=false;
                var ray=new Ray(candidate+Vector3.up*8,Vector3.down);
                foreach(var surface in surfaces)
                    if(surface.Raycast(ray,out var hit,16)&&hit.normal.y>.9f&&Mathf.Abs(hit.point.y-candidate.y)<1&&hit.distance<nearest)
                    {nearest=hit.distance;p=hit.point+Vector3.up*.035f;found=true;}
                return found;
            }
            public bool Footprint(Vector3 center,Quaternion rotation,float x,float z)
            {
                foreach(float sx in new[]{-1f,0,1f})foreach(float sz in new[]{-1f,0,1f})
                    if(!Sample(center+rotation*new Vector3(sx*x,0,sz*z),out var p)||Mathf.Abs(p.y-center.y)>.15f)return false;
                return true;
            }
        }
        sealed class Roads
        {
            readonly List<RoadTriangle> triangles=new List<RoadTriangle>();
            public Roads(MeshFilter filter)
            {
                if(filter==null||filter.sharedMesh==null)throw new InvalidOperationException("The existing ring mesh is missing.");
                var vertices=filter.sharedMesh.vertices;var indices=filter.sharedMesh.triangles;
                for(int i=0;i<indices.Length;i+=3)
                {
                    Vector2 P(int index){var p=filter.transform.TransformPoint(vertices[index]);return new Vector2(p.x,p.z);}
                    var a=P(indices[i]);var b=P(indices[i+1]);var c=P(indices[i+2]);
                    var min=Vector2.Min(a,Vector2.Min(b,c));var max=Vector2.Max(a,Vector2.Max(b,c));
                    triangles.Add(new RoadTriangle{a=a,b=b,c=c,bounds=Rect.MinMaxRect(min.x,min.y,max.x,max.y)});
                }
            }
            static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
            public bool Contains(Vector3 position)
            {
                var p=new Vector2(position.x,position.z);
                foreach(var t in triangles)
                {
                    if(p.x<t.bounds.xMin-.001f||p.x>t.bounds.xMax+.001f||p.y<t.bounds.yMin-.001f||p.y>t.bounds.yMax+.001f)continue;
                    float a=Cross(t.b-t.a,p-t.a),b=Cross(t.c-t.b,p-t.b),c=Cross(t.a-t.c,p-t.c);
                    if(a>=-.001f&&b>=-.001f&&c>=-.001f||a<=.001f&&b<=.001f&&c<=.001f)return true;
                }
                return false;
            }
        }
        static Material Energy(Shader shader)
        {
            string path=Folder+"/Archive_Field_Energy.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(shader){name="Archive_Field_Energy",enableInstancing=true};AssetDatabase.CreateAsset(material,path);}
            material.SetColor("_Tint",Color.white);material.SetFloat("_Glow",1);EditorUtility.SetDirty(material);return material;
        }
        static Material Lit(string name,Color color,Color emission)
        {
            string path=Folder+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,enableInstancing=true};AssetDatabase.CreateAsset(material,path);}
            material.SetColor("_BaseColor",color);material.SetColor("_EmissionColor",emission);material.EnableKeyword("_EMISSION");
            material.SetFloat("_Smoothness",.42f);material.SetFloat("_Metallic",.18f);EditorUtility.SetDirty(material);return material;
        }
        static GameObject BuildBeaconPrefab(Material stone,Material brass,Material crystal,Material energy,TMP_FontAsset font,out int triangleCount)
        {
            var architecture=new Shape();var trim=new Shape();var core=new Shape();
            architecture.Prism(new Vector3(0,0,0),1.25f,1.16f,.23f,8);
            architecture.Prism(new Vector3(0,.23f,0),1.02f,.91f,.21f,8);
            architecture.Prism(new Vector3(0,.44f,0),.62f,.52f,1.58f,6);
            architecture.Prism(new Vector3(0,1.97f,0),.67f,.72f,.17f,6);
            foreach(float side in new[]{-1f,1f})
            {
                architecture.Box(new Vector3(side*.64f,2.29f,0),new Vector3(.24f,.72f,.47f),Quaternion.Euler(0,0,-side*17));
                trim.Box(new Vector3(side*.73f,2.55f,0),new Vector3(.25f,.11f,.49f),Quaternion.Euler(0,0,-side*17));
                // A raised open-book mark uses the same gold artwork as the topic courts.
                trim.Line(new Vector3(0,1.05f,.548f),new Vector3(side*.23f,1.18f,.548f),.028f);
                trim.Line(new Vector3(side*.23f,1.18f,.548f),new Vector3(side*.23f,1.54f,.548f),.028f);
                trim.Line(new Vector3(side*.23f,1.54f,.548f),new Vector3(0,1.41f,.548f),.028f);
            }
            trim.Line(new Vector3(0,1.05f,.548f),new Vector3(0,1.41f,.548f),.028f);
            trim.Prism(new Vector3(0,.405f,0),.93f,.91f,.055f,8);
            trim.Prism(new Vector3(0,.65f,0),.608f,.604f,.045f,6);
            trim.Prism(new Vector3(0,1.79f,0),.55f,.55f,.055f,6);
            trim.Prism(new Vector3(0,2.10f,0),.72f,.70f,.055f,6);
            core.Crystal(.37f,1.12f);
            var stoneMesh=architecture.Save("Archive_Beacon_Weathered_Stone");var trimMesh=trim.Save("Archive_Beacon_Gold_Inlay");var crystalMesh=core.Save("Archive_Beacon_Faceted_Emitter");
            triangleCount=(stoneMesh.triangles.Length+trimMesh.triangles.Length+crystalMesh.triangles.Length)/3;
            var root=new GameObject("Archive_Resonance_Beacon");var view=root.AddComponent<ArchiveResonanceBeaconView>();
            try
            {
                MeshRenderer Part(string name,Mesh mesh,Material material,Vector3 position)
                {
                    var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root.transform,false);go.transform.localPosition=position;
                    go.GetComponent<MeshFilter>().sharedMesh=mesh;var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=material;
                    r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=true;r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;return r;
                }
                var masonry=Part("Weathered_Stone_Pylon",stoneMesh,stone,Vector3.zero);var metal=Part("Gold_Book_Inlay_And_Collars",trimMesh,brass,Vector3.zero);
                view.crystalRenderer=Part("Floating_Faceted_Crystal",crystalMesh,crystal,new Vector3(0,2.1f,0));view.crystal=view.crystalRenderer.transform;
                view.bodyRenderers=new Renderer[]{masonry,metal};view.energyMaterial=energy;
                var label=new GameObject("Beacon_Status",typeof(TextMeshPro));label.transform.SetParent(root.transform,false);label.transform.localPosition=new Vector3(0,3.82f,0);
                view.status=label.GetComponent<TMP_Text>();view.status.font=font;view.status.fontSize=2.3f;view.status.rectTransform.sizeDelta=new Vector2(5.8f,.95f);
                string labelPath=Folder+"/Archive_Field_Label.mat";
                var labelMaterial=AssetDatabase.LoadAssetAtPath<Material>(labelPath);
                if(labelMaterial==null){labelMaterial=new Material(font.material){name="Archive_Field_Label"};AssetDatabase.CreateAsset(labelMaterial,labelPath);}
                labelMaterial.SetColor("_OutlineColor",new Color(.015f,.045f,.04f,1));labelMaterial.SetFloat("_OutlineWidth",.18f);
                EditorUtility.SetDirty(labelMaterial);view.status.fontSharedMaterial=labelMaterial;view.status.UpdateMeshPadding();
                view.status.alignment=TextAlignmentOptions.Center;view.status.color=new Color(.75f,1,.90f);view.status.richText=false;view.status.text="RESONANCE BEACON\nREADY  -  USE HAND";
                view.status.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
                return PrefabUtility.SaveAsPrefabAsset(root,BeaconPrefabPath);
            }
            finally{Object.DestroyImmediate(root);}
        }
        sealed class Shape
        {
            readonly List<Vector3> vertices=new List<Vector3>();readonly List<int> triangles=new List<int>();readonly List<Vector2> uv=new List<Vector2>();
            void Triangle(Vector3 a,Vector3 b,Vector3 c)
            {int i=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);uv.Add(new Vector2(a.x,a.y));uv.Add(new Vector2(b.x,b.y));uv.Add(new Vector2(c.x,c.y));triangles.Add(i);triangles.Add(i+1);triangles.Add(i+2);}
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d){Triangle(a,b,c);Triangle(a,c,d);}
            public void Prism(Vector3 bottom,float bottomRadius,float topRadius,float height,int sides)
            {
                for(int i=0;i<sides;i++)
                {
                    float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;
                    var p=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var q=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b));
                    var lo=bottom+p*bottomRadius;var lo2=bottom+q*bottomRadius;var hi=bottom+Vector3.up*height+p*topRadius;var hi2=bottom+Vector3.up*height+q*topRadius;
                    Quad(lo,hi,hi2,lo2);Triangle(bottom,lo,lo2);Triangle(bottom+Vector3.up*height,hi2,hi);
                }
            }
            public void Box(Vector3 center,Vector3 size,Quaternion rotation)
            {
                Vector3 P(float x,float y,float z)=>center+rotation*Vector3.Scale(new Vector3(x,y,z),size*.5f);
                Quad(P(-1,-1,-1),P(-1,1,-1),P(1,1,-1),P(1,-1,-1));Quad(P(1,-1,1),P(1,1,1),P(-1,1,1),P(-1,-1,1));
                Quad(P(-1,-1,1),P(-1,1,1),P(-1,1,-1),P(-1,-1,-1));Quad(P(1,-1,-1),P(1,1,-1),P(1,1,1),P(1,-1,1));
                Quad(P(-1,1,-1),P(-1,1,1),P(1,1,1),P(1,1,-1));Quad(P(-1,-1,1),P(-1,-1,-1),P(1,-1,-1),P(1,-1,1));
            }
            public void Line(Vector3 a,Vector3 b,float width)
            {var delta=b-a;Box((a+b)*.5f,new Vector3(width,width,delta.magnitude),Quaternion.LookRotation(delta.normalized,Vector3.up));}
            public void Crystal(float radius,float height)
            {
                for(int i=0;i<6;i++)
                {
                    float a=i*Mathf.PI/3,b=(i+1)*Mathf.PI/3;
                    var p=new Vector3(Mathf.Cos(a)*radius,height*.42f,Mathf.Sin(a)*radius);var q=new Vector3(Mathf.Cos(b)*radius,height*.42f,Mathf.Sin(b)*radius);
                    Triangle(p,Vector3.up*height,q);Triangle(q,Vector3.zero,p);
                }
            }
            public Mesh Save(string name)
            {
                string path=Folder+"/"+name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(mesh==null){mesh=new Mesh{name=name};AssetDatabase.CreateAsset(mesh,path);}
                mesh.Clear();mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);return mesh;
            }
        }
    }
}
