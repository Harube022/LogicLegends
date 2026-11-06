using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>Low, grounded ruins in the grass outside the nest's diagonal walls.</summary>
    public static class ArchiveNestFrontageBuilder
    {
        public const string RootName="Nest_Outer_Frontage_Dressing";
        const string Folder=ArchiveEnvironmentPolishSetup.ArtFolder;
        public static int LastGroups {get;private set;}
        public static int LastTriangles {get;private set;}
        public static int LastRenderers {get;private set;}

        public static Transform Build(ArchiveTopicSession session,Transform parent,Material stone,Material trim,Material foliage)
        {
            if(Application.isPlaying)throw new InvalidOperationException("Dress the nest frontage in stopped Edit Mode.");
            if(session==null||parent==null||stone==null||trim==null||foliage==null)throw new ArgumentNullException("Existing session, parent and materials are required.");
            var nest=GameObject.Find("OpenArchive/UniversalMonsterNest")?.transform;
            if(nest==null)throw new InvalidOperationException("The existing UniversalMonsterNest is required.");
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var old=parent.Find(RootName);if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
            var root=new GameObject(RootName).transform;root.SetParent(parent,false);
            var masonry=new Shape(root);var inlay=new Shape(root);var leaves=new Shape(root);
            LastGroups=LastTriangles=LastRenderers=0;
            var center=nest.position;
            for(int quadrant=0;quadrant<4;quadrant++)
            {
                float angle=(45+quadrant*90+(quadrant%2==0?-2:2))*Mathf.Deg2Rad;
                var outward=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                var tangent=Vector3.Cross(Vector3.up,outward);
                var facing=Quaternion.LookRotation(outward);
                // These four arrangements occupy the grass between the quarter wall
                // and the secondary ring, well outside the full-width cardinal lanes.
                var anchor=center+outward*(44.7f+(quadrant%2)*.35f);
                var tablet=Ground(anchor-tangent*3.0f,center.y);
                masonry.Block(tablet+Vector3.up*.14f,new Vector3(3.8f,.37f,2.15f),facing,quadrant);
                masonry.Block(tablet+Vector3.up*.44f,new Vector3(3.05f,.25f,1.80f),facing,quadrant+4);
                var slabPose=facing*Quaternion.Euler(13+quadrant*2,0,(quadrant%2==0?-1:1)*4);
                var slabCenter=tablet+Vector3.up*1.28f;
                var slabScale=new Vector3(1.18f,1.60f,1.25f);
                masonry.Tablet(slabCenter,slabPose,slabScale);
                // Worn fragments of a set/graph inscription; no light, label, or
                // puzzle affordance competes with the actual Book Statues.
                Vector3 Rune(float x,float y)=>slabCenter+slabPose*Vector3.Scale(new Vector3(x,y,.145f),slabScale);
                for(int i=0;i<13;i++)
                {
                    if(i==3||i==9)continue;
                    float a=i*Mathf.PI*2/16,b=(i+1)*Mathf.PI*2/16;
                    inlay.Line(Rune(Mathf.Cos(a)*.34f,Mathf.Sin(a)*.34f),Rune(Mathf.Cos(b)*.34f,Mathf.Sin(b)*.34f),.028f);
                }
                inlay.Line(Rune(-.67f,-.22f),Rune(-.47f,.12f),.025f);
                inlay.Line(Rune(-.47f,.12f),Rune(-.18f,-.08f),.025f);
                for(int i=0;i<3;i++)
                {
                    float x=i==0?-.67f:i==1?-.47f:-.18f,y=i==1?.12f:i==0?-.22f:-.08f;
                    inlay.Rock(Rune(x,y),new Vector3(.052f,.052f,.035f),17+i);
                }
                var brokenBlock=Ground(anchor+tangent*5.8f-outward*.60f,center.y);
                masonry.Block(brokenBlock+Vector3.up*.29f,new Vector3(2.65f,.66f,1.45f),facing*Quaternion.Euler(0,18+quadrant*7,0),quadrant+11);
                masonry.Block(brokenBlock+tangent*.42f+Vector3.up*.78f,new Vector3(1.42f,.40f,1.10f),facing*Quaternion.Euler(0,-7+quadrant*6,0),quadrant+15);
                var smallRock=Ground(anchor+tangent*8.0f+outward*.43f,center.y);
                masonry.Rock(smallRock+Vector3.up*.28f,new Vector3(1.65f,.65f,1.20f),120+quadrant*13);
                var largeRock=Ground(anchor-tangent*7.5f-outward*.35f,center.y);
                masonry.Rock(largeRock+Vector3.up*.38f,new Vector3(2.10f,.87f,1.53f),213+quadrant*17);
                var fallenSlab=Ground(anchor-tangent*8.0f+outward*1.00f,center.y);
                masonry.Block(fallenSlab+Vector3.up*.10f,new Vector3(1.60f,.28f,1.05f),facing*Quaternion.Euler(0,-24-quadrant*5,0),quadrant+24);
                Fern(leaves,Ground(anchor+tangent*4.4f+outward*1.35f,center.y),1.40f,quadrant*27+15);
                Fern(leaves,Ground(anchor-tangent*5.5f+outward*1.40f,center.y),1.25f,quadrant*31-18);
                Fern(leaves,Ground(anchor+tangent*8.4f-outward*.75f,center.y),1.05f,quadrant*21+42);
                Tussock(leaves,Ground(anchor+tangent*7.2f-outward*1.3f,center.y),.90f,quadrant*49);
                Tussock(leaves,Ground(anchor-tangent*8.5f-outward*.65f,center.y),.82f,quadrant*37+15);
                LastGroups++;
            }
            var shapes=new[]{masonry,inlay,leaves};
            var names=new[]{"Weathered_Frontage_Ruins","Broken_Mathematical_Inscriptions","Fern_And_Tussock_Pockets"};
            var materials=new[]{stone,trim,foliage};
            for(int i=0;i<shapes.Length;i++)
            {
                // Check the complete mesh footprint, not only a prop's centre.
                foreach(var vertex in shapes[i].WorldVertices)
                {
                    var p=vertex-center;float radius=new Vector2(p.x,p.z).magnitude;
                    if(Mathf.Abs(p.x)<18||Mathf.Abs(p.z)<18||radius<39||radius>49||p.y>2.4f)
                        throw new InvalidOperationException("Nest frontage escaped its diagonal grass reserve: "+vertex);
                }
                var mesh=shapes[i].Save("Nest_Frontage_"+names[i]);
                var go=new GameObject(names[i],typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);
                go.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=materials[i];
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;
                renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                LastRenderers++;LastTriangles+=mesh.triangles.Length/3;
            }
            AssetDatabase.SaveAssets();return root;
        }

        static Vector3 Ground(Vector3 point,float expectedY)
        {
            var hits=Physics.RaycastAll(new Vector3(point.x,expectedY+2.5f,point.z),Vector3.down,6,~(1<<7),QueryTriggerInteraction.Ignore)
                .Where(h=>h.normal.y>.85f&&Mathf.Abs(h.point.y-expectedY)<.35f&&h.collider.GetComponentInParent<Player>()==null)
                .OrderBy(h=>h.distance).ToArray();
            if(hits.Length==0)throw new InvalidOperationException("No low grass support for nest frontage at "+point);
            // Bases sit slightly below the terrain, without hovering on a raised wall.
            point.y=hits[0].point.y-.035f;return point;
        }
        static void Fern(Shape shape,Vector3 center,float scale,float yaw)
        {
            var rotation=Quaternion.Euler(0,yaw,0);
            for(int i=0;i<5;i++)
            {
                float a=i*72*Mathf.Deg2Rad;var heading=rotation*new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                var side=Vector3.Cross(Vector3.up,heading);var end=center+heading*1.08f*scale+Vector3.up*.62f*scale;
                shape.Line(center,end,.022f*scale);
                for(int j=1;j<6;j++)
                {
                    float u=j/6f;var p=Vector3.Lerp(center,end,u);float width=Mathf.Sin(u*Mathf.PI)*.28f*scale;
                    foreach(int sign in new[]{-1,1})
                    {
                        var tip=p+side*(width*sign)-heading*.12f*scale+Vector3.up*.045f*scale;
                        shape.Leaf(p-heading*.10f*scale,tip,p+heading*.09f*scale);
                    }
                }
            }
        }
        static void Tussock(Shape shape,Vector3 center,float scale,float yaw)
        {
            for(int i=0;i<9;i++)
            {
                float angle=(yaw+i*137.5f)*Mathf.Deg2Rad;var d=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));var side=Vector3.Cross(Vector3.up,d);
                var p=center+d*(.08f+(i%3)*.035f)*scale;var tip=p+d*(.28f+(i%3)*.09f)*scale+Vector3.up*(.63f+(i%4)*.08f)*scale;
                shape.Leaf(p-side*.055f*scale,tip,p+side*.055f*scale);
            }
        }
        sealed class Shape
        {
            readonly Transform root;readonly List<Vector3> vertices=new List<Vector3>();readonly List<int> indices=new List<int>();
            public IEnumerable<Vector3> WorldVertices=>vertices.Select(root.TransformPoint);
            public Shape(Transform owner){root=owner;}
            int Vertex(Vector3 p){vertices.Add(root.InverseTransformPoint(p));return vertices.Count-1;}
            void Triangle(Vector3 a,Vector3 b,Vector3 c){int n=Vertex(a);Vertex(b);Vertex(c);indices.AddRange(new[]{n,n+1,n+2});}
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d){Triangle(a,b,c);Triangle(a,c,d);}
            public void Leaf(Vector3 a,Vector3 tip,Vector3 b){Triangle(a,tip,b);Triangle(b,tip,a);}
            public void Block(Vector3 center,Vector3 size,Quaternion rotation,int variation)
            {
                float x=size.x*.5f,z=size.z*.5f,bevel=Mathf.Min(.15f,Mathf.Min(x,z)*.28f),h=size.y*.5f;
                var ring=new[]{new Vector3(-x+bevel,0,-z),new Vector3(x-bevel,0,-z),new Vector3(x,0,-z+bevel),new Vector3(x,0,z-bevel),new Vector3(x-bevel,0,z),new Vector3(-x+bevel,0,z),new Vector3(-x,0,z-bevel),new Vector3(-x,0,-z+bevel)};
                for(int i=0;i<ring.Length;i++)
                {
                    int j=(i+1)%ring.Length;float hi=h-(i==(variation%8)?h*.28f:0),hj=h-(j==(variation%8)?h*.28f:0);
                    var a=center+rotation*(ring[i]-Vector3.up*h);var b=center+rotation*(ring[j]-Vector3.up*h);
                    var c=center+rotation*(ring[i]*.94f+Vector3.up*hi);var d=center+rotation*(ring[j]*.94f+Vector3.up*hj);
                    Quad(b,a,c,d);Triangle(center+rotation*Vector3.up*h,d,c);
                }
            }
            public void Tablet(Vector3 center,Quaternion rotation,Vector3 scale)
            {
                var outline=new[]{new Vector2(-1.05f,-.48f),new Vector2(1.03f,-.48f),new Vector2(1.08f,.18f),new Vector2(.88f,.43f),new Vector2(.28f,.46f),new Vector2(.12f,.35f),new Vector2(-.24f,.47f),new Vector2(-1.01f,.36f)};
                Vector3 P(Vector2 v,float z)=>center+rotation*Vector3.Scale(new Vector3(v.x,v.y,z),scale);
                for(int i=0;i<outline.Length;i++)
                {
                    int j=(i+1)%outline.Length;var a=outline[i];var b=outline[j];
                    Quad(P(a,-.13f),P(b,-.13f),P(b,.13f),P(a,.13f));
                    Triangle(center+rotation*Vector3.forward*(.13f*scale.z),P(a,.13f),P(b,.13f));
                    Triangle(center-rotation*Vector3.forward*(.13f*scale.z),P(b,-.13f),P(a,-.13f));
                }
            }
            public void Rock(Vector3 center,Vector3 size,int seed)
            {
                const int sides=12;int start=vertices.Count;var random=new System.Random(seed);
                for(int ring=0;ring<3;ring++)for(int i=0;i<sides;i++)
                {
                    float a=i*Mathf.PI*2/sides+seed*.17f,jitter=.88f+(float)random.NextDouble()*.20f;
                    float radius=ring==0?.78f:ring==1?1:.62f,y=ring==0?-.5f:ring==1?-.04f:.40f+(i%3)*.03f;
                    Vertex(center+Vector3.Scale(new Vector3(Mathf.Cos(a)*radius*jitter,y,Mathf.Sin(a)*radius*jitter),size));
                }
                int cap=Vertex(center+Vector3.up*size.y*.48f);
                for(int i=0;i<sides;i++)
                {
                    int j=(i+1)%sides;
                    for(int ring=0;ring<2;ring++){int a=start+ring*sides+i,b=start+ring*sides+j,c=start+(ring+1)*sides+i,d=start+(ring+1)*sides+j;indices.AddRange(new[]{b,a,c,b,c,d});}
                    indices.AddRange(new[]{cap,start+2*sides+j,start+2*sides+i});
                }
            }
            public void Line(Vector3 a,Vector3 b,float width)
            {
                var direction=b-a;if(direction.sqrMagnitude<.000001f)return;
                Block((a+b)*.5f,new Vector3(width,width,direction.magnitude+width*.25f),Quaternion.LookRotation(direction),0);
            }
            public Mesh Save(string name)
            {
                string path=Folder+"/"+name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(mesh==null){mesh=new Mesh{name=name};AssetDatabase.CreateAsset(mesh,path);}
                mesh.Clear();mesh.indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16;
                mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
                var normals=mesh.normals;var uv=new List<Vector2>(vertices.Count);
                for(int i=0;i<vertices.Count;i++)
                {
                    var p=root.TransformPoint(vertices[i]);var n=root.TransformDirection(normals[i]);var a=new Vector3(Mathf.Abs(n.x),Mathf.Abs(n.y),Mathf.Abs(n.z));
                    uv.Add((a.y>=a.x&&a.y>=a.z?new Vector2(p.x,p.z):a.x>=a.z?new Vector2(p.z,p.y):new Vector2(p.x,p.y))*.5f);
                }
                mesh.SetUVs(0,uv);EditorUtility.SetDirty(mesh);return mesh;
            }
        }
    }
}
