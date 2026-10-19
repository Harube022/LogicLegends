using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace OpenArchiveAuthoring
{
    /// <summary>Texture authoring only. Original model, collider and navigation assets stay intact.</summary>
    public static class OpenArchiveTreeBridgeArt
    {
        const string Map=OpenArchiveBuilder.Folder,Art=Map+"/Art",Meshes=Art+"/Meshes/TreeBridge";
        const string Vendor="Assets/CHEATCODE/SimpleNaturePack";
        static readonly string[] Scenes={Map+"/Scenes/MultiplayerGameplay.unity",Map+"/Scenes/OpenArchive_Explore.unity",Map+"/Testing/KhezuAwakening/OpenArchive_KhezuAwakening_Test.unity",Map+"/Testing/KhezuHunting/OpenArchive_KhezuHunting_Test.unity",Map+"/Testing/KhezuCinematic/OpenArchive_KhezuCinematic_Test.unity"};
        static readonly HashSet<string> WoodNames=new HashSet<string>{"Wooden_Bridge_Deck","Bridge_Railing_Module","Bridge_Approach_Ramps"};
        static readonly Dictionary<string,Mesh> Cache=new Dictionary<string,Mesh>();
        static Material trees,timber;static Texture2D palette;
        [Serializable] sealed class Check {public string path;public bool passed,posesPreserved,physicsPreserved,systemsPreserved,lodsPreserved,otherRenderersPreserved;public int trees,wood;}
        [Serializable] sealed class MeshCheck {public string name;public int vertices,components,leafVertices;public bool originalChannelsPreserved;}
        [Serializable] sealed class Report {public string checkedUtc;public bool passed;public List<Check> scenes=new List<Check>(),prefabs=new List<Check>();public List<MeshCheck> meshes=new List<MeshCheck>();public string limitations="Unity Editor visual and asset preservation checks. Android hardware and live multiplayer not profiled.";}
        static Report report;
        static string Pose(Transform t){return t.localPosition.ToString("R")+t.localRotation.ToString("R")+t.localScale.ToString("R")+t.gameObject.activeSelf+"|"+t.gameObject.layer+"|"+(int)GameObjectUtility.GetStaticEditorFlags(t.gameObject);}
        static bool IsTree(MeshFilter f){return f.name.StartsWith("Tree_")&&f.name.Contains("_LOD");}
        static bool Target(MeshFilter f){return IsTree(f)||WoodNames.Contains(f.name);}
        static string Key(Vector3 p){return Mathf.RoundToInt(p.x*10000)+","+Mathf.RoundToInt(p.y*10000)+","+Mathf.RoundToInt(p.z*10000);}
        static Mesh Source(Mesh mesh){
            if(!AssetDatabase.GetAssetPath(mesh).StartsWith(Meshes+"/"))return mesh;
            string name=mesh.name.Split(new[]{"__TreeBridge"},StringSplitOptions.None)[0];
            if(name.StartsWith("Tree_")){string model=name.Split(new[]{"_LOD"},StringSplitOptions.None)[0];return AssetDatabase.LoadAllAssetsAtPath(Vendor+"/Models/"+model+".fbx").OfType<Mesh>().Single(m=>m.name==name);}
            return AssetDatabase.LoadAssetAtPath<Mesh>(Map+"/Meshes/"+name+".asset");
        }
        static bool Leaf(Vector2 uv){var c=palette.GetPixelBilinear(uv.x,uv.y);return c.g>c.r*1.07f&&c.g>c.b*1.08f;}
        // Principal direction of each connected timber piece aligns grain even on diagonal braces.
        static Vector3 LongAxis(List<int> indices,Vector3[] v,Vector3 center){
            float xx=0,xy=0,xz=0,yy=0,yz=0,zz=0;foreach(int i in indices){var p=v[i]-center;xx+=p.x*p.x;xy+=p.x*p.y;xz+=p.x*p.z;yy+=p.y*p.y;yz+=p.y*p.z;zz+=p.z*p.z;}
            var axis=xx>=yy&&xx>=zz?Vector3.right:yy>=zz?Vector3.up:Vector3.forward;
            for(int n=0;n<20;n++){axis=new Vector3(xx*axis.x+xy*axis.y+xz*axis.z,xy*axis.x+yy*axis.y+yz*axis.z,xz*axis.x+yz*axis.y+zz*axis.z).normalized;}
            return axis.sqrMagnitude>.5f?axis:Vector3.right;
        }
        static List<List<int>> Components(Vector3[] v,int[] triangles){
            var parent=Enumerable.Range(0,v.Length).ToArray();Func<int,int> find=null;find=i=>parent[i]==i?i:parent[i]=find(parent[i]);
            Action<int,int> join=(a,b)=>{a=find(a);b=find(b);if(a!=b)parent[b]=a;};var weld=new Dictionary<string,int>();
            for(int i=0;i<v.Length;i++){int old;string key=Key(v[i]);if(weld.TryGetValue(key,out old))join(old,i);else weld[key]=i;}
            for(int i=0;i<triangles.Length;i+=3){join(triangles[i],triangles[i+1]);join(triangles[i],triangles[i+2]);}
            return Enumerable.Range(0,v.Length).GroupBy(i=>find(i)).Select(g=>g.ToList()).ToList();
        }
        static Mesh Textured(Mesh input,bool tree){
            var source=Source(input);if(source==null)throw new InvalidOperationException("Missing original mesh for "+input.name);string name=source.name+"__TreeBridge";Mesh ready;if(Cache.TryGetValue(name,out ready))return ready;
            var clone=Object.Instantiate(source);var v=source.vertices;var n=source.normals;var uv=source.uv;var detail=new Vector4[v.Length];int leaves=0,parts=0;
            if(tree){for(int i=0;i<v.Length;i++){
                bool leaf=Leaf(uv[i]);bool pine=source.name.StartsWith("Tree_02_")||source.name.StartsWith("Tree_05_");detail[i]=new Vector4(v[i].x,v[i].y,v[i].z,leaf?(pine?2:1):0);if(leaf)leaves++;
            }}else{
                var pieces=Components(v,source.triangles);parts=pieces.Count;
                foreach(var piece in pieces){var center=piece.Aggregate(Vector3.zero,(s,i)=>s+v[i])/piece.Count;var axis=LongAxis(piece,v,center);var side=Vector3.Cross(axis,Mathf.Abs(axis.y)<.85f?Vector3.up:Vector3.forward).normalized;var other=Vector3.Cross(axis,side).normalized;
                    float width=Mathf.Max(.05f,piece.Max(i=>Mathf.Abs(Vector3.Dot(v[i]-center,side)))*2),depth=Mathf.Max(.05f,piece.Max(i=>Mathf.Abs(Vector3.Dot(v[i]-center,other)))*2);float variation=.94f+Mathf.PerlinNoise(center.x*1.7f+42,center.y+center.z)*.12f;
                    foreach(int i in piece){bool cap=Mathf.Abs(Vector3.Dot(n[i],axis))>.85f;var cross=Vector3.Cross(n[i],axis);if(cross.sqrMagnitude<.05f)cross=side;else cross.Normalize();var p=v[i]-center;
                        detail[i]=cap?new Vector4(Vector3.Dot(p,side)/width+.5f,Vector3.Dot(p,other)/depth+.5f,1,variation):new Vector4(Vector3.Dot(p,axis)+center.x*.21f+center.z*.13f,Vector3.Dot(p,cross)+center.y*.31f,0,variation);
                    }
                }
            }
            clone.SetUVs(2,detail.ToList());clone.name=name;
            var check=new MeshCheck{name=name,vertices=v.Length,components=parts,leafVertices=leaves,originalChannelsPreserved=source.vertices.SequenceEqual(clone.vertices)&&source.triangles.SequenceEqual(clone.triangles)&&source.normals.SequenceEqual(clone.normals)&&source.uv.SequenceEqual(clone.uv)&&source.colors.SequenceEqual(clone.colors)};
            if(!check.originalChannelsPreserved)throw new InvalidOperationException("Geometry changed: "+name);report.meshes.Add(check);
            string path=Meshes+"/"+name+".asset";ready=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(ready==null){ready=clone;AssetDatabase.CreateAsset(ready,path);}else{ready.SetUVs(2,detail.ToList());Object.DestroyImmediate(clone);EditorUtility.SetDirty(ready);}
            Cache[name]=ready;return ready;
        }
        static Check Assign(GameObject root,string path){
            var all=root.GetComponentsInChildren<Transform>(true);var poses=all.ToDictionary(t=>t,t=>Pose(t));var physics=root.GetComponentsInChildren<Collider>(true).ToDictionary(c=>c,c=>EditorJsonUtility.ToJson(c));var systems=root.GetComponentsInChildren<MonoBehaviour>(true).Where(c=>c!=null).ToDictionary(c=>c,c=>EditorJsonUtility.ToJson(c));var lods=root.GetComponentsInChildren<LODGroup>(true).ToDictionary(c=>c,c=>EditorJsonUtility.ToJson(c));
            var other=root.GetComponentsInChildren<MeshFilter>(true).Where(f=>!Target(f)).ToDictionary(f=>f,f=>new{mesh=f.sharedMesh,mats=f.GetComponent<Renderer>().sharedMaterials});var check=new Check{path=path};
            foreach(var f in root.GetComponentsInChildren<MeshFilter>(true).Where(Target)){
                bool tree=IsTree(f);f.sharedMesh=Textured(f.sharedMesh,tree);f.GetComponent<Renderer>().sharedMaterial=tree?trees:timber;if(tree)check.trees++;else check.wood++;
            }
            check.posesPreserved=poses.All(p=>Pose(p.Key)==p.Value);check.physicsPreserved=physics.All(p=>EditorJsonUtility.ToJson(p.Key)==p.Value)&&root.GetComponentsInChildren<Collider>(true).Length==physics.Count;
            check.systemsPreserved=systems.All(p=>EditorJsonUtility.ToJson(p.Key)==p.Value);check.lodsPreserved=lods.All(p=>EditorJsonUtility.ToJson(p.Key)==p.Value);check.otherRenderersPreserved=other.All(p=>p.Key.sharedMesh==p.Value.mesh&&p.Key.GetComponent<Renderer>().sharedMaterials.SequenceEqual(p.Value.mats));
            check.passed=check.posesPreserved&&check.physicsPreserved&&check.systemsPreserved&&check.lodsPreserved&&check.otherRenderersPreserved;if(!check.passed)throw new InvalidOperationException("Texture preservation failed: "+JsonUtility.ToJson(check));return check;
        }
        static void Import(string name){var path=Art+"/Textures/"+name+".png";var im=(TextureImporter)AssetImporter.GetAtPath(path);im.sRGBTexture=false;im.mipmapEnabled=true;im.wrapMode=TextureWrapMode.Repeat;im.filterMode=FilterMode.Trilinear;im.anisoLevel=4;im.maxTextureSize=512;im.isReadable=false;var a=im.GetPlatformTextureSettings("Android");a.overridden=true;a.maxTextureSize=512;a.format=TextureImporterFormat.ASTC_6x6;im.SetPlatformTextureSettings(a);im.SaveAndReimport();}
        static Material Material(string name,string palettePath,int kind){var path=Art+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find("LogicLegends/OpenArchive/TreeTimber")){name=name};AssetDatabase.CreateAsset(m,path);}m.enableInstancing=true;m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(palettePath));m.SetTexture("_TimberMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"/Textures/Archive_TimberDetail.png"));m.SetTexture("_CanopyMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"/Textures/Archive_CanopyDetail.png"));m.SetFloat("_Kind",kind);m.SetFloat("_BarkStrength",.92f);m.SetFloat("_WoodStrength",.9f);m.SetFloat("_LeafStrength",.87f);m.SetFloat("_BarkBrightness",1.25f);m.SetFloat("_Relief",.012f);EditorUtility.SetDirty(m);return m;}
        [MenuItem("Tools/Logic Legends/Open Archive/Art/Apply tree bark, leaves and bridge timber")]
        public static string Apply(){
            OpenArchiveImprovements.RequireSavedEditor();string original=SceneManager.GetActiveScene().path;report=new Report{checkedUtc=DateTime.UtcNow.ToString("o")};Cache.Clear();Directory.CreateDirectory(Meshes);Import("Archive_TimberDetail");Import("Archive_CanopyDetail");
            palette=new Texture2D(2,2);ImageConversion.LoadImage(palette,File.ReadAllBytes(Vendor+"/Textures/NaturePackLite_Texture_01.png"));trees=Material("Archive_TexturedTrees",Vendor+"/Textures/NaturePackLite_Texture_01.png",0);timber=Material("Archive_TexturedBridgeWood",Map+"/Textures/OpenArchive_Palette.png",1);
            try{
                var prefabs=Enumerable.Range(1,5).Select(i=>Map+"/Prefabs/Nature/Archive_Tree_0"+i+".prefab").Concat(new[]{Map+"/Prefabs/Bridges/Bridge_Railing_Module.prefab",Map+"/Prefabs/Bridges/Bridge_Modular_10x24m.prefab"});
                foreach(string path in prefabs){var prefab=PrefabUtility.LoadPrefabContents(path);try{report.prefabs.Add(Assign(prefab,path));PrefabUtility.SaveAsPrefabAsset(prefab,path);}finally{PrefabUtility.UnloadPrefabContents(prefab);}}
                foreach(string path in Scenes){var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);var root=scene.GetRootGameObjects().Single(g=>g.name=="OpenArchive");report.scenes.Add(Assign(root,path));EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save "+path);}
                AssetDatabase.SaveAssets();report.passed=report.scenes.Count==5&&report.prefabs.Count==7&&report.meshes.Count==13&&report.scenes.All(c=>c.passed)&&report.prefabs.All(c=>c.passed)&&report.meshes.All(c=>c.originalChannelsPreserved);string json=JsonUtility.ToJson(report,true);File.WriteAllText(Art+"/Reports/TreeBridgePreservation.json",json);return json;
            }finally{Object.DestroyImmediate(palette);EditorSceneManager.OpenScene(original,OpenSceneMode.Single);}
        }
    }
}
