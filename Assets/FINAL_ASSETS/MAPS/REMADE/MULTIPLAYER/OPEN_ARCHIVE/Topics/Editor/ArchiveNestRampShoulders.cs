using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>Broken retaining masonry beside the existing nest ramps; presentation only.</summary>
    public static class ArchiveNestRampShoulders
    {
        public const string RootName="Nest_Weathered_Ramp_Shoulders";
        public static int LastRampCount {get;private set;}
        public static int LastStoneCount {get;private set;}
        public static int LastTriangles {get;private set;}

        public static Transform Build(Transform nest,Transform parent,Material stone)
        {
            if(Application.isPlaying||nest==null||parent==null||stone==null)
                throw new InvalidOperationException("Dress the existing nest ramps in stopped Edit Mode.");
            var ramps=nest.GetComponentsInChildren<MeshFilter>().Where(f=>f.name.StartsWith("RampSurface_",StringComparison.Ordinal)).ToArray();
            if(ramps.Length!=4)throw new InvalidOperationException("The four existing nest ramps are required.");
            var old=parent.Find(RootName);if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
            var root=new GameObject(RootName).transform;root.SetParent(parent,false);
            var shape=new Shape(root);var random=new System.Random(473201);
            LastRampCount=LastStoneCount=LastTriangles=0;
            float Random(float min,float max)=>Mathf.Lerp(min,max,(float)random.NextDouble());
            foreach(var ramp in ramps.OrderBy(r=>r.name,StringComparer.Ordinal))
            {
                var points=ramp.sharedMesh.vertices.Select(ramp.transform.TransformPoint).ToArray();
                var direction=ramp.GetComponent<Renderer>().bounds.center-nest.position;direction.y=0;direction.Normalize();
                var across=Vector3.Cross(Vector3.up,direction);
                float inner=points.Min(p=>Vector3.Dot(p-nest.position,direction));
                float outer=points.Max(p=>Vector3.Dot(p-nest.position,direction));
                float halfWidth=points.Max(p=>Mathf.Abs(Vector3.Dot(p-nest.position,across)));
                float top=points.Max(p=>p.y),bottom=points.Min(p=>p.y);
                float Surface(float distance)=>Mathf.Lerp(top,bottom,Mathf.InverseLerp(inner,outer,distance));
                foreach(float side in new[]{-1f,1f})
                {
                    var outward=across*side;
                    float start=inner+.20f;int course=0;
                    while(start<outer-.35f)
                    {
                        float end=Mathf.Min(start+Random(1.7f,2.8f),outer-.15f);
                        float thickness=Random(.8f,1.65f);
                        float edge=halfWidth-.025f;
                        float cut=Random(.13f,.25f);
                        // Each stone has a different outer face and clipped corners.
                        // Its inner rim meets the old slab; the full walking width stays open.
                        var outline=new[]{new Vector2(edge+cut,start),new Vector2(edge+thickness-cut,start),
                            new Vector2(edge+thickness,start+cut),new Vector2(edge+thickness,end-cut),
                            new Vector2(edge+thickness-cut,end),new Vector2(edge+cut,end),
                            new Vector2(edge,end-cut),new Vector2(edge,start+cut)};
                        var cap=new Vector3[8];var foot=new Vector3[8];
                        float chip=Random(-.055f,.055f);
                        for(int i=0;i<outline.Length;i++)
                        {
                            var p=nest.position+outward*outline[i].x+direction*outline[i].y;
                            float ground=Ground(p,nest.position.y);
                            float lip=Mathf.Lerp(.035f,chip,Mathf.InverseLerp(edge,edge+thickness,outline[i].x));
                            cap[i]=new Vector3(p.x,Mathf.Max(ground+.045f,Surface(outline[i].y)+lip),p.z);
                            foot[i]=new Vector3(p.x,ground-.09f,p.z);
                        }
                        shape.Solid(cap,foot);LastStoneCount++;
                        // Half-buried fallen chips soften the ground seam without
                        // creating another continuous curb or a false walkable ramp.
                        if(course++%2==1)
                        {
                            var p=nest.position+outward*(halfWidth+thickness+.42f)+direction*((start+end)*.5f);
                            p.y=Ground(p,nest.position.y)-.025f;
                            shape.Chip(p,Random(.45f,.8f),Random(.18f,.32f),Random(.35f,.6f),Random(-35,35));
                            LastStoneCount++;
                        }
                        start=end+Random(.055f,.13f);
                    }
                }
                LastRampCount++;
            }
            var mesh=shape.Save("Nest_Ramp_Battered_Masonry");LastTriangles=mesh.triangles.Length/3;
            var go=new GameObject("Irregular_Retaining_Stones_And_Buried_Chips",typeof(MeshFilter),typeof(MeshRenderer));
            go.transform.SetParent(root,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=stone;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;
            renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            return root;
        }
        static float Ground(Vector3 point,float expected)
        {
            var hits=Physics.RaycastAll(new Vector3(point.x,expected+1,point.z),Vector3.down,3,~(1<<7),QueryTriggerInteraction.Ignore)
                .Where(h=>h.normal.y>.85f&&Mathf.Abs(h.point.y-expected)<.3f&&h.collider.GetComponentInParent<Player>()==null)
                .OrderBy(h=>h.distance).ToArray();
            if(hits.Length==0)throw new InvalidOperationException("No ground beside the nest ramp at "+point);
            return hits[0].point.y;
        }
        sealed class Shape
        {
            readonly Transform root;readonly List<Vector3> vertices=new List<Vector3>();readonly List<int> triangles=new List<int>();
            public Shape(Transform root){this.root=root;}
            void Face(Vector3 a,Vector3 b,Vector3 c,Vector3 outward)
            {
                if(Vector3.Dot(Vector3.Cross(b-a,c-a),outward)<0){var swap=b;b=c;c=swap;}
                int n=vertices.Count;vertices.Add(root.InverseTransformPoint(a));vertices.Add(root.InverseTransformPoint(b));vertices.Add(root.InverseTransformPoint(c));
                triangles.AddRange(new[]{n,n+1,n+2});
            }
            public void Solid(Vector3[] cap,Vector3[] foot)
            {
                var center=cap.Aggregate(Vector3.zero,(a,b)=>a+b)/cap.Length;
                var baseCenter=foot.Aggregate(Vector3.zero,(a,b)=>a+b)/foot.Length;
                for(int i=0;i<cap.Length;i++)
                {
                    int j=(i+1)%cap.Length;var outward=(cap[i]+cap[j])*.5f-center;outward.y=0;
                    Face(center,cap[i],cap[j],Vector3.up);Face(baseCenter,foot[j],foot[i],Vector3.down);
                    Face(foot[i],foot[j],cap[j],outward);Face(foot[i],cap[j],cap[i],outward);
                }
            }
            public void Chip(Vector3 p,float x,float height,float z,float angle)
            {
                var rotation=Quaternion.Euler(0,angle,0);var cap=new Vector3[8];var foot=new Vector3[8];
                for(int i=0;i<8;i++)
                {
                    float a=i*Mathf.PI/4;var offset=rotation*new Vector3(Mathf.Cos(a)*x,0,Mathf.Sin(a)*z);
                    foot[i]=p+offset;cap[i]=p+offset*.75f+Vector3.up*(height*(i%3==0?.8f:1));
                }
                Solid(cap,foot);
            }
            public Mesh Save(string name)
            {
                string folder=ArchiveEnvironmentPolishSetup.ArtFolder;Directory.CreateDirectory(folder);
                string path=folder+"/"+name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(mesh==null){mesh=new Mesh{name=name};AssetDatabase.CreateAsset(mesh,path);}
                mesh.Clear();mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
                var uv=new List<Vector2>(vertices.Count);var normals=mesh.normals;
                for(int i=0;i<vertices.Count;i++)
                {
                    var p=root.TransformPoint(vertices[i]);var n=root.TransformDirection(normals[i]);
                    uv.Add((Mathf.Abs(n.y)>.7f?new Vector2(p.x,p.z):Mathf.Abs(n.x)>Mathf.Abs(n.z)?new Vector2(p.z,p.y):new Vector2(p.x,p.y))*.4f);
                }
                mesh.SetUVs(0,uv);EditorUtility.SetDirty(mesh);return mesh;
            }
        }
    }
}
