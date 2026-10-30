using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>Closes the exposed sides and undersides of the existing thin approach surfaces.</summary>
    public static class ArchiveApproachFoundations
    {
        public const string RootName="Solid_Approach_Foundations";
        const string Folder=ArchiveEnvironmentPolishSetup.ArtFolder;
        [Serializable] public sealed class Report
        {
            public bool passed;
            public int surfaces,exteriorEdges,triangles,renderers,newColliders;
            public string scope="Render-only masonry sides and underside caps. Original walking surfaces, transforms, colliders and navigation stay unchanged.";
        }
        public static Report LastReport {get;private set;}
        sealed class Edge {public Vector3 a,b;public int count;}
        static string Key(Vector3 p)=>Mathf.RoundToInt(p.x*1000)+","+Mathf.RoundToInt(p.y*1000)+","+Mathf.RoundToInt(p.z*1000);
        public static Report Build(Transform map,Transform parent,Material stone)
        {
            if(Application.isPlaying||map==null||parent==null||stone==null)throw new InvalidOperationException("Author foundations in the existing stopped scene.");
            var old=parent.Find(RootName);if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
            var root=new GameObject(RootName).transform;root.SetParent(parent,false);
            var report=new Report();LastReport=report;
            var vertices=new List<Vector3>();var indices=new List<int>();var uv=new List<Vector2>();
            var nest=map.Find("UniversalMonsterNest");
            var sources=nest.GetComponentsInChildren<MeshFilter>().Where(f=>f.name.StartsWith("RampSurface_",StringComparison.Ordinal)||f.name=="Nest_Four_Wide_Landings"||f.name=="Nest_StoneFloor_30m")
                .Concat(map.Find("Environment/Bridges").GetComponentsInChildren<MeshFilter>().Where(f=>f.name=="Bridge_Approach_Ramps")).ToArray();
            void Tri(Vector3 a,Vector3 b,Vector3 c)
            {
                int n=vertices.Count;var normal=Vector3.Cross(b-a,c-a).normalized;
                foreach(var p in new[]{a,b,c})
                {
                    vertices.Add(root.InverseTransformPoint(p));
                    uv.Add(Mathf.Abs(normal.y)>.8f?new Vector2(p.x,p.z)*.45f:Mathf.Abs(normal.x)>Mathf.Abs(normal.z)?new Vector2(p.z,p.y)*.45f:new Vector2(p.x,p.y)*.45f);
                }
                indices.AddRange(new[]{n,n+1,n+2});
            }
            foreach(var source in sources)
            {
                var positions=source.sharedMesh.vertices.Select(source.transform.TransformPoint).ToArray();var triangles=source.sharedMesh.triangles;
                var edges=new Dictionary<string,Edge>();int topFaces=0;
                void Edge(Vector3 a,Vector3 b)
                {
                    string ka=Key(a),kb=Key(b);string key=string.CompareOrdinal(ka,kb)<0?ka+"|"+kb:kb+"|"+ka;
                    if(edges.TryGetValue(key,out var edge))edge.count++;else edges.Add(key,new Edge{a=a,b=b,count=1});
                }
                for(int i=0;i<triangles.Length;i+=3)
                {
                    var a=positions[triangles[i]];var b=positions[triangles[i+1]];var c=positions[triangles[i+2]];
                    if(Vector3.Cross(b-a,c-a).normalized.y<.2f)continue;
                    topFaces++;Edge(a,b);Edge(b,c);Edge(c,a);
                    // A real underside also remains visible from a low riverbank camera.
                    var ba=new Vector3(a.x,-.18f,a.z);var bb=new Vector3(b.x,-.18f,b.z);var bc=new Vector3(c.x,-.18f,c.z);
                    if(Vector3.Cross(bb-ba,bc-ba).sqrMagnitude>.0000001f)Tri(ba,bc,bb);
                }
                if(topFaces==0)throw new InvalidOperationException("No upward walking faces in "+source.name);
                foreach(var edge in edges.Values.Where(e=>e.count==1))
                {
                    var bottomA=new Vector3(edge.a.x,-.18f,edge.a.z);var bottomB=new Vector3(edge.b.x,-.18f,edge.b.z);
                    // Reuse the oriented top boundary so the wall faces away from the slab.
                    Tri(edge.b,edge.a,bottomA);Tri(edge.b,bottomA,bottomB);report.exteriorEdges++;
                }
                report.surfaces++;
            }
            string path=Folder+"/Archive_Solid_Approach_Foundations.asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null){mesh=new Mesh{name="Archive_Solid_Approach_Foundations"};AssetDatabase.CreateAsset(mesh,path);}
            mesh.Clear();mesh.indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16;
            mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
            var go=new GameObject("Weathered_Masonry_Sides_And_Undersides",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=stone;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;
            report.triangles=indices.Count/3;report.renderers=1;report.newColliders=root.GetComponentsInChildren<Collider>(true).Length;
            report.passed=report.surfaces==10&&report.exteriorEdges>0&&report.newColliders==0;
            Directory.CreateDirectory("Temp/ArchiveNestFinish");File.WriteAllText("Temp/ArchiveNestFinish/Foundations.json",JsonUtility.ToJson(report,true));
            if(!report.passed)throw new InvalidOperationException("Unexpected approach surface layout.");
            AssetDatabase.SaveAssets();return report;
        }
    }
}
