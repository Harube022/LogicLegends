using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace OpenArchiveAuthoring
{
    /// <summary>Remove redundant visible faces. Original collider and navigation geometry is never edited.</summary>
    public static class OpenArchiveFlickerRepair
    {
        const string Map=OpenArchiveBuilder.Folder,Art=Map+"/Art",Folder=Art+"/Meshes/StableSurfaces";
        static readonly string[] Scenes={Map+"/Scenes/MultiplayerGameplay.unity",Map+"/Scenes/OpenArchive_Explore.unity",Map+"/Testing/KhezuAwakening/OpenArchive_KhezuAwakening_Test.unity",Map+"/Testing/KhezuHunting/OpenArchive_KhezuHunting_Test.unity",Map+"/Testing/KhezuCinematic/OpenArchive_KhezuCinematic_Test.unity"};
        static readonly HashSet<string> SelfNames=new HashSet<string>{"MainRing_StonePaving","Radial_North_10m","Radial_South_10m","Radial_East_10m","Radial_West_10m","Clearing_Buried_Foundation_Corners","Reclaimed_Boulder_Moss","Green_Spawn_Glyph","Ancient_Gold_Rune","Ferns_Roots_And_Leaves"};
        static readonly Dictionary<string,string> CrossNames=new Dictionary<string,string>{{"Spawn_StonePlaza_18m","Spawn_Arch_Footings"},{"Nest_StoneFloor_30m","Arch_Stone_Footings"},{"Nest_Continuous_Quarter_Wall_Plinths","Den_Rim_Moss"}};
        static readonly Dictionary<string,Mesh> Cache=new Dictionary<string,Mesh>();
        [Serializable] sealed class Link {public string name,mesh;}
        [Serializable] sealed class Links {public List<Link> sources=new List<Link>();}
        [Serializable] sealed class Edit {public string name;public int beforeTriangles,afterTriangles,clippedTriangles;public float removedProjectedArea;}
        [Serializable] sealed class Check {public string path;public bool passed,posesPreserved,physicsPreserved,gameplayPreserved,lodsPreserved,materialsPreserved;public int bridgeModules,surfaces;public List<Edit> edits=new List<Edit>();}
        [Serializable] sealed class Report {public string checkedUtc;public bool passed;public List<Check> scenes=new List<Check>();public Check bridgePrefab;public string limits="Editor and offline Play Mode. Original LOD settings, camera settings, 2x MSAA, pipeline and shadow atlas retained. Android hardware and live multiplayer not tested.";}
        static Links links;
        struct Vertex {
            public Vector3 p,q,n;public Color color;public Vector4 tangent,u0,u1,u2,u3;
            public static Vertex Lerp(Vertex a,Vertex b,float t){return new Vertex{p=Vector3.LerpUnclamped(a.p,b.p,t),q=Vector3.LerpUnclamped(a.q,b.q,t),n=Vector3.LerpUnclamped(a.n,b.n,t).normalized,color=Color.LerpUnclamped(a.color,b.color,t),tangent=Vector4.LerpUnclamped(a.tangent,b.tangent,t),u0=Vector4.LerpUnclamped(a.u0,b.u0,t),u1=Vector4.LerpUnclamped(a.u1,b.u1,t),u2=Vector4.LerpUnclamped(a.u2,b.u2,t),u3=Vector4.LerpUnclamped(a.u3,b.u3,t)};}
        }
        sealed class Face {public Vertex[] v;public Vector3 n;public float d;public int axis;public Rect bounds;}
        static Matrix4x4 Relative(Transform t,Transform root){var result=Matrix4x4.identity;while(t!=root){if(t==null)throw new InvalidOperationException("Outside repair root.");result=Matrix4x4.TRS(t.localPosition,t.localRotation,t.localScale)*result;t=t.parent;}return result;}
        static Vector2 Plane(Vector3 p,int axis){return axis==0?new Vector2(p.y,p.z):axis==1?new Vector2(p.x,p.z):new Vector2(p.x,p.y);}
        static float Side(Vector2 p,Vector2 a,Vector2 b){return (b.x-a.x)*(p.y-a.y)-(b.y-a.y)*(p.x-a.x);}
        static float Area(List<Vertex> p,int axis){float a=0;for(int i=0;i<p.Count;i++){var x=Plane(p[i].q,axis);var y=Plane(p[(i+1)%p.Count].q,axis);a+=x.x*y.y-y.x*x.y;}return Mathf.Abs(a)*.5f;}
        static List<Vertex> Clip(List<Vertex> p,Vector2 a,Vector2 b,bool inside,int axis){
            var result=new List<Vertex>();for(int i=0;i<p.Count;i++){var x=p[i];var y=p[(i+1)%p.Count];float dx=Side(Plane(x.q,axis),a,b),dy=Side(Plane(y.q,axis),a,b);bool ix=inside?dx>=0:dx<=0,iy=inside?dy>=0:dy<=0;if(ix)result.Add(x);if(ix!=iy)result.Add(Vertex.Lerp(x,y,dx/(dx-dy)));}return result;
        }
        static Vertex[] CounterClockwise(Face face){var p=face.v;if(Side(Plane(p[2].q,face.axis),Plane(p[0].q,face.axis),Plane(p[1].q,face.axis))<0)return new[]{p[0],p[2],p[1]};return p;}
        static float Intersection(List<Vertex> p,Face clip){var current=p;var v=CounterClockwise(clip);for(int i=0;i<3&&current.Count>=3;i++)current=Clip(current,Plane(v[i].q,clip.axis),Plane(v[(i+1)%3].q,clip.axis),true,clip.axis);return current.Count<3?0:Area(current,clip.axis);}
        static List<List<Vertex>> Subtract(List<Vertex> polygon,Face clip){
            var result=new List<List<Vertex>>();var current=polygon;var v=CounterClockwise(clip);
            for(int i=0;i<3&&current.Count>=3;i++){var a=Plane(v[i].q,clip.axis);var b=Plane(v[(i+1)%3].q,clip.axis);var outside=Clip(current,a,b,false,clip.axis);if(outside.Count>=3&&Area(outside,clip.axis)>.0000002f)result.Add(outside);current=Clip(current,a,b,true,clip.axis);}return result;
        }
        static Face MakeFace(Vertex a,Vertex b,Vertex c){var n=Vector3.Cross(b.q-a.q,c.q-a.q);if(n.sqrMagnitude<1e-12f)return null;n.Normalize();int nx=Mathf.RoundToInt(Mathf.Abs(n.x)*1000),ny=Mathf.RoundToInt(Mathf.Abs(n.y)*1000),nz=Mathf.RoundToInt(Mathf.Abs(n.z)*1000);int axis=nx>=ny&&nx>=nz?0:ny>=nz?1:2;var p=new[]{Plane(a.q,axis),Plane(b.q,axis),Plane(c.q,axis)};float loX=p.Min(v=>v.x),loY=p.Min(v=>v.y),hiX=p.Max(v=>v.x),hiY=p.Max(v=>v.y);return new Face{v=new[]{a,b,c},n=n,d=Vector3.Dot(n,a.q),axis=axis,bounds=Rect.MinMaxRect(loX,loY,hiX,hiY)};}
        static string NormalKey(Vector3 n){return Mathf.RoundToInt(n.x*1000)+","+Mathf.RoundToInt(n.y*1000)+","+Mathf.RoundToInt(n.z*1000);}
        sealed class Ledger {
            readonly Dictionary<string,List<Face>> planes=new Dictionary<string,List<Face>>();
            readonly HashSet<string> exact=new HashSet<string>();
            string VertexKey(Vector3 p){return Mathf.RoundToInt(p.x*1000)+","+Mathf.RoundToInt(p.y*1000)+","+Mathf.RoundToInt(p.z*1000);}
            string TriangleKey(Face f){var points=f.v.Select(v=>VertexKey(v.q)).ToArray();int inversions=0;for(int i=0;i<3;i++)for(int j=i+1;j<3;j++)if(string.CompareOrdinal(points[i],points[j])>0)inversions++;Array.Sort(points,StringComparer.Ordinal);return string.Join("|",points)+"|"+(inversions%2);}
            public bool Contains(Face f){return exact.Contains(TriangleKey(f));}
            string Key(Vector3 n,int d){return NormalKey(n)+"|"+d;}
            public void Add(Face f){exact.Add(TriangleKey(f));string key=Key(f.n,Mathf.RoundToInt(f.d*500));List<Face> list;if(!planes.TryGetValue(key,out list)){list=new List<Face>();planes.Add(key,list);}list.Add(f);}
            public IEnumerable<Face> Candidates(Face face,float tolerance){int d=Mathf.RoundToInt(face.d*500);for(int i=-2;i<=2;i++){List<Face> list;if(!planes.TryGetValue(Key(face.n,d+i),out list))continue;foreach(var f in list){if(Vector3.Dot(face.n,f.n)<.99999f||Mathf.Abs(Vector3.Dot(face.n,f.v[0].q)-face.d)>tolerance)continue;if(face.bounds.xMin>=f.bounds.xMax-1e-6f||face.bounds.xMax<=f.bounds.xMin+1e-6f||face.bounds.yMin>=f.bounds.yMax-1e-6f||face.bounds.yMax<=f.bounds.yMin+1e-6f)continue;yield return f;}}}
        }
        static Vertex[] Vertices(Mesh source,Matrix4x4 matrix){var p=source.vertices;var n=source.normals;var color=source.colors;var tangent=source.tangents;var uv=new List<Vector4>[4];for(int j=0;j<4;j++){uv[j]=new List<Vector4>();source.GetUVs(j,uv[j]);}var result=new Vertex[p.Length];for(int i=0;i<p.Length;i++)result[i]=new Vertex{p=p[i],q=matrix.MultiplyPoint3x4(p[i]),n=n.Length==p.Length?n[i]:Vector3.up,color=color.Length==p.Length?color[i]:Color.white,tangent=tangent.Length==p.Length?tangent[i]:Vector4.zero,u0=uv[0].Count==p.Length?uv[0][i]:Vector4.zero,u1=uv[1].Count==p.Length?uv[1][i]:Vector4.zero,u2=uv[2].Count==p.Length?uv[2][i]:Vector4.zero,u3=uv[3].Count==p.Length?uv[3][i]:Vector4.zero};return result;}
        static IEnumerable<Face> Faces(Mesh source,Matrix4x4 matrix,bool flat){var v=Vertices(source,matrix);var t=source.triangles;for(int i=0;i<t.Length;i+=3){var f=MakeFace(v[t[i]],v[t[i+1]],v[t[i+2]]);if(f!=null&&(!flat||(f.n.y>.9999f&&f.v.Max(x=>x.q.y)-f.v.Min(x=>x.q.y)<.0002f)))yield return f;}}
        static bool Surface(string name){return SelfNames.Contains(name)||CrossNames.ContainsKey(name)||name.StartsWith("Cliff_Waterfall_");}
        static Mesh Original(MeshFilter filter){var link=links.sources.FirstOrDefault(l=>l.name==filter.name);if(link==null)throw new InvalidOperationException("Missing source map for "+filter.name);var m=AssetDatabase.LoadAssetAtPath<Mesh>(link.mesh);if(m==null)throw new InvalidOperationException("Missing source mesh "+link.mesh);return m;}
        static string Hash(List<Vertex> vertices){var text=new StringBuilder();foreach(var v in vertices)text.Append(v.p.ToString("F4")).Append(v.u2.ToString("F4"));using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-","").Substring(0,12);}
        static Mesh Save(Mesh source,List<Vertex> vertices,string name){
            Mesh cached;if(Cache.TryGetValue(name,out cached))return cached;string path=Folder+"/"+name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);bool create=mesh==null;if(create)mesh=new Mesh{name=name};else mesh.Clear();mesh.indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16;mesh.SetVertices(vertices.Select(v=>v.p).ToList());mesh.SetNormals(vertices.Select(v=>v.n).ToList());
            if(source.HasVertexAttribute(VertexAttribute.Color))mesh.SetColors(vertices.Select(v=>v.color).ToList());if(source.HasVertexAttribute(VertexAttribute.Tangent))mesh.SetTangents(vertices.Select(v=>v.tangent).ToList());
            for(int channel=0;channel<4;channel++){var attr=(VertexAttribute)((int)VertexAttribute.TexCoord0+channel);if(!source.HasVertexAttribute(attr))continue;var data=vertices.Select(v=>channel==0?v.u0:channel==1?v.u1:channel==2?v.u2:v.u3).ToList();int dim=source.GetVertexAttributeDimension(attr);if(dim==2)mesh.SetUVs(channel,data.Select(v=>new Vector2(v.x,v.y)).ToList());else if(dim==3)mesh.SetUVs(channel,data.Select(v=>new Vector3(v.x,v.y,v.z)).ToList());else mesh.SetUVs(channel,data);}
            mesh.SetTriangles(Enumerable.Range(0,vertices.Count).ToArray(),0);mesh.RecalculateBounds();if(create)AssetDatabase.CreateAsset(mesh,path);else EditorUtility.SetDirty(mesh);Cache[name]=mesh;return mesh;
        }
        static Mesh Clean(Mesh source,Matrix4x4 matrix,Ledger ledger,bool flat,string name,Check check){
            if(source.subMeshCount!=1)throw new InvalidOperationException("Expected one submesh: "+source.name);var all=Vertices(source,matrix);var indices=source.triangles;var output=new List<Vertex>();var edit=new Edit{name=name,beforeTriangles=indices.Length/3};
            for(int t=0;t<indices.Length;t+=3){var face=MakeFace(all[indices[t]],all[indices[t+1]],all[indices[t+2]]);if(face==null)continue;
                if(flat&&(face.n.y<.9999f||face.v.Max(v=>v.q.y)-face.v.Min(v=>v.q.y)>.0002f)){output.AddRange(face.v);continue;}
                var pieces=new List<List<Vertex>>{face.v.ToList()};float before=Area(pieces[0],face.axis);
                if(ledger.Contains(face)){edit.clippedTriangles++;edit.removedProjectedArea+=before;continue;}
                foreach(var prior in ledger.Candidates(face,flat?.003f:.00025f).ToArray()){
                    if(pieces.Count==0)break;var next=new List<List<Vertex>>();foreach(var poly in pieces){if(Intersection(poly,prior)>.0000005f)next.AddRange(Subtract(poly,prior));else next.Add(poly);}pieces=next;
                }
                float after=pieces.Sum(p=>Area(p,face.axis));if(before-after>.000001f){edit.clippedTriangles++;edit.removedProjectedArea+=before-after;}
                foreach(var poly in pieces)for(int i=1;i<poly.Count-1;i++){if(Vector3.Cross(poly[i].p-poly[0].p,poly[i+1].p-poly[0].p).sqrMagnitude<1e-12f)continue;output.Add(poly[0]);output.Add(poly[i]);output.Add(poly[i+1]);}
                ledger.Add(face);
            }
            edit.afterTriangles=output.Count/3;check.edits.Add(edit);return Save(source,output,name+"__NoFlicker_"+Hash(output));
        }
        static void Bridges(Transform bridges,Check check){
            var source=AssetDatabase.LoadAssetAtPath<Mesh>(Art+"/Meshes/TreeBridge/Bridge_Railing_Module__TreeBridge.asset");
            foreach(Transform bridge in bridges){var rails=bridge.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name=="Bridge_Railing_Module").ToArray();var ledger=new Ledger();foreach(var filter in rails){filter.sharedMesh=Clean(source,Relative(filter.transform,bridge),ledger,false,"Bridge_Railing",check);check.bridgeModules++;}}
        }
        static void OneBridge(Transform bridge,Check check){var source=AssetDatabase.LoadAssetAtPath<Mesh>(Art+"/Meshes/TreeBridge/Bridge_Railing_Module__TreeBridge.asset");var ledger=new Ledger();foreach(var f in bridge.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name=="Bridge_Railing_Module")){f.sharedMesh=Clean(source,Relative(f.transform,bridge),ledger,false,"Bridge_Railing",check);check.bridgeModules++;}}
        sealed class Snapshot {
            readonly Dictionary<Transform,string> poses;readonly Dictionary<Collider,string> physics;readonly Dictionary<MonoBehaviour,string> systems;readonly Dictionary<LODGroup,string> lods;readonly Dictionary<Renderer,Material[]> materials;readonly Dictionary<UnityEngine.AI.NavMeshAgent,string> agents;
            public Snapshot(GameObject root){poses=root.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t,t=>Pose(t));physics=root.GetComponentsInChildren<Collider>(true).ToDictionary(c=>c,c=>EditorJsonUtility.ToJson(c));systems=root.GetComponentsInChildren<MonoBehaviour>(true).Where(c=>c!=null).ToDictionary(c=>c,c=>EditorJsonUtility.ToJson(c));lods=root.GetComponentsInChildren<LODGroup>(true).ToDictionary(c=>c,c=>EditorJsonUtility.ToJson(c));materials=root.GetComponentsInChildren<Renderer>(true).ToDictionary(r=>r,r=>r.sharedMaterials);agents=root.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true).ToDictionary(c=>c,c=>EditorJsonUtility.ToJson(c));}
            public void Validate(GameObject root,Check check){check.posesPreserved=poses.All(p=>p.Key!=null&&Pose(p.Key)==p.Value);check.physicsPreserved=physics.All(p=>p.Key!=null&&EditorJsonUtility.ToJson(p.Key)==p.Value)&&root.GetComponentsInChildren<Collider>(true).Length==physics.Count;check.gameplayPreserved=systems.All(p=>p.Key!=null&&EditorJsonUtility.ToJson(p.Key)==p.Value)&&agents.All(p=>p.Key!=null&&EditorJsonUtility.ToJson(p.Key)==p.Value);check.lodsPreserved=lods.All(p=>p.Key!=null&&EditorJsonUtility.ToJson(p.Key)==p.Value);check.materialsPreserved=materials.All(p=>p.Key!=null&&p.Key.sharedMaterials.SequenceEqual(p.Value));check.passed=check.posesPreserved&&check.physicsPreserved&&check.gameplayPreserved&&check.lodsPreserved&&check.materialsPreserved;if(!check.passed)throw new InvalidOperationException("Flicker repair preservation failed: "+JsonUtility.ToJson(check));}
        }
        static string Pose(Transform t){return t.localPosition.ToString("R")+t.localRotation.ToString("R")+t.localScale.ToString("R")+t.gameObject.activeSelf+"|"+t.gameObject.layer+"|"+(int)GameObjectUtility.GetStaticEditorFlags(t.gameObject);}
        static void PrepareSources(){string path=Art+"/Authoring/FlickerSources.json";links=File.Exists(path)?JsonUtility.FromJson<Links>(File.ReadAllText(path)):new Links();var root=GameObject.Find("OpenArchive");foreach(var f in root.GetComponentsInChildren<MeshFilter>(true).Where(f=>Surface(f.name))){if(links.sources.Any(l=>l.name==f.name))continue;string mesh=AssetDatabase.GetAssetPath(f.sharedMesh);if(mesh.StartsWith(Folder+"/"))throw new InvalidOperationException("Original source link is missing for "+f.name);links.sources.Add(new Link{name=f.name,mesh=mesh});}File.WriteAllText(path,JsonUtility.ToJson(links,true));}
        [MenuItem("Tools/Logic Legends/Open Archive/Art/Repair overlapping faces and flicker")]
        public static string Apply(){
            OpenArchiveImprovements.RequireSavedEditor();string original=SceneManager.GetActiveScene().path;Directory.CreateDirectory(Folder);Cache.Clear();PrepareSources();var report=new Report{checkedUtc=DateTime.UtcNow.ToString("o")};string prefabPath=Map+"/Prefabs/Bridges/Bridge_Modular_10x24m.prefab";
            try{
                var prefab=PrefabUtility.LoadPrefabContents(prefabPath);try{var snapshot=new Snapshot(prefab);var check=new Check{path=prefabPath};OneBridge(prefab.transform,check);snapshot.Validate(prefab,check);PrefabUtility.SaveAsPrefabAsset(prefab,prefabPath);report.bridgePrefab=check;}finally{PrefabUtility.UnloadPrefabContents(prefab);}
                foreach(string path in Scenes){var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);var root=scene.GetRootGameObjects().Single(g=>g.name=="OpenArchive");var snapshot=new Snapshot(root);var check=new Check{path=path};Bridges(root.transform.Find("Environment/Bridges"),check);var filters=root.GetComponentsInChildren<MeshFilter>(true);
                    foreach(var f in filters.Where(f=>Surface(f.name))){var ledger=new Ledger();string cover;if(CrossNames.TryGetValue(f.name,out cover)){foreach(var covering in filters.Where(v=>v.name==cover))foreach(var face in Faces(covering.sharedMesh,Relative(covering.transform,root.transform),true))ledger.Add(face);}f.sharedMesh=Clean(Original(f),Relative(f.transform,root.transform),ledger,true,f.name,check);check.surfaces++;}
                    snapshot.Validate(root,check);EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save "+path);report.scenes.Add(check);
                }
                AssetDatabase.SaveAssets();report.passed=report.bridgePrefab.passed&&report.scenes.Count==5&&report.scenes.All(s=>s.passed);File.WriteAllText(Art+"/Reports/FlickerGeometryRepair.json",JsonUtility.ToJson(report,true));return "passed="+report.passed+", scenes="+report.scenes.Count+", bridge modules per scene="+report.scenes[0].bridgeModules+", surfaces per scene="+report.scenes[0].surfaces+", edited render meshes="+Cache.Count;
            }finally{EditorSceneManager.OpenScene(original,OpenSceneMode.Single);}
        }
    }
}
