using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>A single reusable cosmetic draw buffer. Never creates physics or particle objects.</summary>
    internal sealed class ArchiveFieldMeshBuffer
    {
        readonly List<Vector3> vertices=new List<Vector3>(1800);
        readonly List<Color> colors=new List<Color>(1800);
        readonly List<int> triangles=new List<int>(2700);
        readonly Mesh mesh;
        public readonly MeshRenderer renderer;
        public int TriangleCount=>triangles.Count/3;
        public ArchiveFieldMeshBuffer(Transform parent,string name,Material material)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));
            go.transform.SetParent(parent,false);go.hideFlags=HideFlags.DontSave;
            mesh=new Mesh{name=name+"_ReusableMesh",hideFlags=HideFlags.DontSave};mesh.MarkDynamic();
            go.GetComponent<MeshFilter>().sharedMesh=mesh;renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode=MotionVectorGenerationMode.ForceNoMotion;renderer.enabled=false;
        }
        public void Begin(){vertices.Clear();colors.Clear();triangles.Clear();}
        public void Triangle(Vector3 a,Vector3 b,Vector3 c,Color tint)
        {
            int i=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);
            colors.Add(tint);colors.Add(tint);colors.Add(tint);triangles.Add(i);triangles.Add(i+1);triangles.Add(i+2);
        }
        public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color tint)
            =>Quad(a,b,c,d,tint,tint,tint,tint);
        public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color ca,Color cb,Color cc,Color cd)
        {
            int i=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);
            colors.Add(ca);colors.Add(cb);colors.Add(cc);colors.Add(cd);
            triangles.Add(i);triangles.Add(i+1);triangles.Add(i+2);triangles.Add(i);triangles.Add(i+2);triangles.Add(i+3);
        }
        public void Line(Vector3 a,Vector3 b,float width,Color color)
        {
            var delta=b-a;if(delta.sqrMagnitude<.000001f)return;
            var side=Vector3.Cross(Vector3.up,delta.normalized)*width*.5f;
            if(side.sqrMagnitude<.000001f)side=Vector3.right*width*.5f;
            Quad(a-side,a+side,b+side,b-side,color);
        }
        public void Ring(Vector3 center,float radius,float width,int segments,float turn,Color color,float fraction=1,bool broken=false,Quaternion? rotation=null)
        {
            var q=rotation??Quaternion.identity;
            for(int i=0;i<segments;i++)
            {
                if(i/(float)segments>=fraction||broken&&i%12>=9)continue;
                float a=i*Mathf.PI*2/segments+turn,b=(i+1)*Mathf.PI*2/segments+turn;
                var pa=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var pb=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b));
                Quad(center+q*(pa*(radius-width*.5f)),center+q*(pb*(radius-width*.5f)),
                    center+q*(pb*(radius+width*.5f)),center+q*(pa*(radius+width*.5f)),color);
            }
        }
        public void Shard(Vector3 center,float radius,float height,Quaternion rotation,Color color)
        {
            var tip=center+rotation*(Vector3.up*height);var bottom=center+rotation*(Vector3.down*height*.18f);
            for(int i=0;i<5;i++)
            {
                float a=i*Mathf.PI*2/5,b=(i+1)*Mathf.PI*2/5;
                var p=center+rotation*new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius);
                var q=center+rotation*new Vector3(Mathf.Cos(b)*radius,0,Mathf.Sin(b)*radius);
                Triangle(p,tip,q,color);Triangle(q,bottom,p,color*.72f);
            }
        }
        public void Commit(bool normals=false)
        {
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);
            if(normals)mesh.RecalculateNormals();mesh.RecalculateBounds();renderer.enabled=vertices.Count>0;
        }
        public void Hide(){if(renderer!=null)renderer.enabled=false;}
        public void Dispose()
        {
            if(renderer!=null)DestroyOwned(renderer.gameObject);
            if(mesh!=null)DestroyOwned(mesh);
        }
        static void DestroyOwned(Object item){if(Application.isPlaying)Object.Destroy(item);else Object.DestroyImmediate(item);}
    }
}
