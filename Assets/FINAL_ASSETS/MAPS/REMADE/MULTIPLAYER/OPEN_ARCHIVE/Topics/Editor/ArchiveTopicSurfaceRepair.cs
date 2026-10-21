using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>Trim decorative approach paving out of the ten playable clearing footprints.</summary>
    public static class ArchiveTopicSurfaceRepair
    {
        const string Map="Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE";
        const float Radius=9;
        struct Vertex
        {
            public Vector3 p,n;public Color color;public Vector4 tangent;public Vector4[] uv;
            public static Vertex Lerp(Vertex a,Vertex b,float t)
            {
                var result=new Vertex{p=Vector3.LerpUnclamped(a.p,b.p,t),n=Vector3.LerpUnclamped(a.n,b.n,t).normalized,color=Color.LerpUnclamped(a.color,b.color,t),tangent=Vector4.LerpUnclamped(a.tangent,b.tangent,t),uv=new Vector4[4]};
                for(int i=0;i<4;i++)result.uv[i]=Vector4.LerpUnclamped(a.uv[i],b.uv[i],t);return result;
            }
        }
        [Serializable] sealed class Report
        {
            public bool passed,physicsPreserved,scenePreserved;public int topics,beforeTriangles,afterTriangles;public float removedPavingArea;
            public string scope="Decorative approach render mesh only: trim paving from all ten 9 m clearing footprints (22.5 m at the current scene scale). Terrain, colliders, NavMesh, anchors and gameplay remain unchanged.";
            public string checkedUtc;
        }
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Repair Topic Ground Overlap")]
        public static string Apply()
        {
            if(Application.isPlaying||SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Use a saved scene outside Play Mode.");
            var stations=Object.FindObjectsByType<ArchiveTopicStation>(FindObjectsSortMode.None).OrderBy(s=>s.topic).ToArray();
            if(stations.Length!=10)throw new InvalidOperationException("Expected all ten adapted topic stations.");
            var filter=GameObject.Find("OpenArchive").transform.Find("Environment/SecondaryPaths/Secondary_Loops_And_SlotApproaches_6m").GetComponent<MeshFilter>();
            var mesh=filter.sharedMesh;
            if(AssetDatabase.GetAssetPath(mesh)!=Map+"/Meshes/Secondary_Loops_And_SlotApproaches_6m.asset"||mesh.subMeshCount!=1)throw new InvalidOperationException("Unexpected approach mesh.");
            var colliders=Object.FindObjectsByType<Collider>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            if(colliders.OfType<MeshCollider>().Any(c=>c.sharedMesh==mesh))throw new InvalidOperationException("Approach repair must not edit collision geometry.");
            var physics=colliders.Select(c=>EditorJsonUtility.ToJson(c)).ToArray();
            var positions=mesh.vertices;var normals=mesh.normals;var colors=mesh.colors;var tangents=mesh.tangents;
            var uv=new List<Vector4>[4];for(int i=0;i<4;i++){uv[i]=new List<Vector4>();mesh.GetUVs(i,uv[i]);}
            var source=new Vertex[positions.Length];
            for(int i=0;i<source.Length;i++)source[i]=new Vertex{p=positions[i],n=normals.Length==source.Length?normals[i]:Vector3.up,color=colors.Length==source.Length?colors[i]:Color.white,tangent=tangents.Length==source.Length?tangents[i]:Vector4.zero,uv=Enumerable.Range(0,4).Select(c=>uv[c].Count==source.Length?uv[c][i]:Vector4.zero).ToArray()};
            var circles=stations.Select(s=>filter.transform.InverseTransformPoint(s.transform.position)).Select(c=>Enumerable.Range(0,48).Select(i=>new Vector2(c.x+Mathf.Cos(i*Mathf.PI/24)*Radius,c.z+Mathf.Sin(i*Mathf.PI/24)*Radius)).ToArray()).ToArray();
            var output=new List<Vertex>();var indices=mesh.triangles;float beforeArea=0,afterArea=0;
            for(int i=0;i<indices.Length;i+=3)
            {
                var pieces=new List<List<Vertex>>{new List<Vertex>{source[indices[i]],source[indices[i+1]],source[indices[i+2]]}};beforeArea+=Area(pieces[0]);
                foreach(var circle in circles)
                {
                    var next=new List<List<Vertex>>();
                    foreach(var piece in pieces)
                    {
                        if(!Overlaps(piece,circle)){next.Add(piece);continue;}
                        var current=piece;
                        for(int edge=0;edge<circle.Length&&current.Count>=3;edge++)
                        {
                            var outside=Clip(current,circle[edge],circle[(edge+1)%circle.Length],false);
                            if(outside.Count>=3&&Area(outside)>.000001f)next.Add(outside);
                            current=Clip(current,circle[edge],circle[(edge+1)%circle.Length],true);
                        }
                    }
                    pieces=next;
                }
                foreach(var piece in pieces)
                {
                    afterArea+=Area(piece);
                    for(int j=1;j<piece.Count-1;j++)if(Vector3.Cross(piece[j].p-piece[0].p,piece[j+1].p-piece[0].p).sqrMagnitude>1e-12f){output.Add(piece[0]);output.Add(piece[j]);output.Add(piece[j+1]);}
                }
            }
            bool hasNormals=mesh.HasVertexAttribute(VertexAttribute.Normal),hasColors=mesh.HasVertexAttribute(VertexAttribute.Color),hasTangents=mesh.HasVertexAttribute(VertexAttribute.Tangent);
            int[] dimensions=Enumerable.Range(0,4).Select(c=>mesh.HasVertexAttribute((VertexAttribute)((int)VertexAttribute.TexCoord0+c))?mesh.GetVertexAttributeDimension((VertexAttribute)((int)VertexAttribute.TexCoord0+c)):0).ToArray();
            mesh.Clear();mesh.indexFormat=output.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16;mesh.SetVertices(output.Select(v=>v.p).ToList());
            if(hasNormals)mesh.SetNormals(output.Select(v=>v.n).ToList());if(hasColors)mesh.SetColors(output.Select(v=>v.color).ToList());if(hasTangents)mesh.SetTangents(output.Select(v=>v.tangent).ToList());
            for(int c=0;c<4;c++){int channel=c;if(dimensions[c]==2)mesh.SetUVs(c,output.Select(v=>new Vector2(v.uv[channel].x,v.uv[channel].y)).ToList());else if(dimensions[c]==3)mesh.SetUVs(c,output.Select(v=>(Vector3)v.uv[channel]).ToList());else if(dimensions[c]==4)mesh.SetUVs(c,output.Select(v=>v.uv[channel]).ToList());}
            mesh.SetTriangles(Enumerable.Range(0,output.Count).ToArray(),0);mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);AssetDatabase.SaveAssetIfDirty(mesh);
            var report=new Report{topics=10,beforeTriangles=indices.Length/3,afterTriangles=output.Count/3,removedPavingArea=beforeArea-afterArea,physicsPreserved=colliders.Select(c=>EditorJsonUtility.ToJson(c)).SequenceEqual(physics),scenePreserved=!SceneManager.GetActiveScene().isDirty,checkedUtc=DateTime.UtcNow.ToString("o")};
            report.passed=report.physicsPreserved&&report.scenePreserved;
            File.WriteAllText(ArchiveTopicSetup.Folder+"/Testing/Reports/TopicGroundRepair.json",JsonUtility.ToJson(report,true));
            if(!report.passed)throw new InvalidOperationException("Surface preservation check failed.");
            return JsonUtility.ToJson(report);
        }
        static Vector2 XZ(Vertex v)=>new Vector2(v.p.x,v.p.z);
        static float Side(Vector2 p,Vector2 a,Vector2 b)=>(b.x-a.x)*(p.y-a.y)-(b.y-a.y)*(p.x-a.x);
        static float Area(List<Vertex> p){double a=0;for(int i=0;i<p.Count;i++){var x=XZ(p[i]);var y=XZ(p[(i+1)%p.Count]);a+=(double)x.x*y.y-(double)y.x*x.y;}return (float)Math.Abs(a*.5);}
        static bool Overlaps(List<Vertex> p,Vector2[] polygon)
        {
            float loX=p.Min(v=>v.p.x),hiX=p.Max(v=>v.p.x),loY=p.Min(v=>v.p.z),hiY=p.Max(v=>v.p.z);
            return loX<polygon.Max(v=>v.x)&&hiX>polygon.Min(v=>v.x)&&loY<polygon.Max(v=>v.y)&&hiY>polygon.Min(v=>v.y);
        }
        static List<Vertex> Clip(List<Vertex> p,Vector2 a,Vector2 b,bool inside)
        {
            var result=new List<Vertex>();
            for(int i=0;i<p.Count;i++)
            {
                var x=p[i];var y=p[(i+1)%p.Count];float dx=Side(XZ(x),a,b),dy=Side(XZ(y),a,b);bool ix=inside?dx>=0:dx<=0,iy=inside?dy>=0:dy<=0;
                if(ix)result.Add(x);if(ix!=iy)result.Add(Vertex.Lerp(x,y,dx/(dx-dy)));
            }
            return result;
        }
    }
}
