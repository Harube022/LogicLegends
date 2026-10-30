using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>Small authored bank gardens outside the actual bridge and path footprints.</summary>
    public static class ArchiveBridgeDressingBuilder
    {
        const string Folder=ArchiveTopicSetup.Folder+"/Art/EnvironmentPolish";
        const float FootprintRadius=2.45f;
        public static int LastClusterCount {get;private set;}
        public static void Build(ArchiveTopicSession session,Transform parent,Material stone,Material trim,Material foliage,Material glow)
        {
            if(Application.isPlaying)throw new InvalidOperationException("Dress the saved bridge banks outside Play Mode.");
            if(session==null||parent==null)throw new ArgumentNullException("session/parent");
            var old=parent.Find("Bridge_Bank_Dressing");if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
            var root=new GameObject("Bridge_Bank_Dressing").transform;root.SetParent(parent,false);
            var map=GameObject.Find("OpenArchive");var bridges=map==null?null:map.transform.Find("Environment/Bridges");
            LastClusterCount=0;if(bridges==null)return;
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var placement=new ArchiveWayfindingPlacement(map.transform.Find("Environment"));
            var architecture=new Shape();var inlay=new Shape();var plants=new Shape();var flowers=new Shape();
            // Weathered, chamfered stone. The low stack and capped marker share a single mesh.
            architecture.Block(new Vector3(0,.13f,0),new Vector3(1.55f,.26f,1.02f),.14f);
            architecture.Block(new Vector3(0,.39f,0),new Vector3(1.24f,.28f,.83f),.12f);
            architecture.Column(new Vector3(0,1.17f,0),.42f,.34f,1.30f,8);
            architecture.Block(new Vector3(0,1.91f,0),new Vector3(1.03f,.23f,.82f),.13f);
            architecture.Block(new Vector3(0,2.11f,0),new Vector3(.75f,.17f,.61f),.10f);
            architecture.Block(new Vector3(-1.12f,.12f,-.52f),new Vector3(.86f,.24f,.64f),.17f,18);
            architecture.Block(new Vector3(.97f,.10f,-.87f),new Vector3(.72f,.20f,.53f),.15f,-14);
            architecture.Block(new Vector3(-.76f,.08f,.90f),new Vector3(.63f,.16f,.55f),.13f,32);
            // Thin architectural collars and a geometric book-rune on the water-facing cap.
            inlay.Column(new Vector3(0,.57f,0),.446f,.43f,.065f,8);
            inlay.Column(new Vector3(0,1.76f,0),.395f,.38f,.055f,8);
            foreach(float side in new[]{-1f,1f})
            {
                inlay.Line(new Vector3(0,1.12f,.365f),new Vector3(side*.22f,1.22f,.365f),.024f);
                inlay.Line(new Vector3(side*.22f,1.22f,.365f),new Vector3(side*.22f,1.49f,.365f),.024f);
                inlay.Line(new Vector3(side*.22f,1.49f,.365f),new Vector3(0,1.40f,.365f),.024f);
            }
            inlay.Line(new Vector3(0,1.12f,.365f),new Vector3(0,1.40f,.365f),.024f);
            // The composition is low and broad. Fern leaflets form real silhouettes,
            // avoiding another large, opaque bush covering the approach.
            Fern(plants,new Vector3(-1.12f,.08f,-.87f),.88f,26);
            Fern(plants,new Vector3(.98f,.06f,-.53f),.72f,-31);
            Fern(plants,new Vector3(.48f,.05f,1.13f),.58f,48);
            for(int i=0;i<7;i++)
            {
                float a=i*2.399963f;var p=new Vector3(-.68f+Mathf.Cos(a)*.43f,.06f,.91f+Mathf.Sin(a)*.34f);
                float height=.34f+(i%3)*.07f;plants.Line(p,p+Vector3.up*height,.016f);
                Flower(flowers,p+Vector3.up*height,.095f+(i%2)*.018f,i*31);
            }
            var stoneMesh=architecture.Save("Bridge_Bank_Weathered_Marker");
            var inlayMesh=inlay.Save("Bridge_Bank_Book_Inlay");
            var plantMesh=plants.Save("Bridge_Bank_Fern_Garden");
            var flowerMesh=flowers.Save("Bridge_Bank_Lavender_Flowers");
            var flowerMaterial=FlowerMaterial();
            int index=0;
            foreach(Transform bridge in bridges)
            {
                if(!bridge.name.StartsWith("Bridge_Modular_",StringComparison.Ordinal))continue;
                index++;
                var deck=bridge.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f=>f.name=="Wooden_Bridge_Deck");
                if(deck==null||deck.sharedMesh==null)continue;
                var localBounds=BridgeBounds(bridge,deck);
                float halfWidth=Mathf.Max(Mathf.Abs(localBounds.min.x),Mathf.Abs(localBounds.max.x))*Mathf.Abs(bridge.lossyScale.x);
                float end=localBounds.max.z*Mathf.Abs(bridge.lossyScale.z);
                foreach(int side in new[]{-1,1})
                {
                    bool found=false;Vector3 point=default;
                    // The shoulder is selected from actual deck dimensions and tested
                    // against path triangles, rather than guessing from an island AABB.
                    foreach(float along in new[]{end+5,end+3,end+7,end+1})
                    {
                        foreach(float off in new[]{halfWidth+6,halfWidth+8,halfWidth+10,halfWidth+12})
                        {
                            var candidate=bridge.position+bridge.forward*along+bridge.right*(side*off);
                            if(!placement.ClearOfRoads(candidate,FootprintRadius+.6f)||!placement.ClearOfGroundcover(candidate,FootprintRadius+.4f)||!Supported(candidate,bridge.position.y,out var floor)||
                                !ArchiveWayfindingPlacement.ClearOfProps(candidate,FootprintRadius)||!ClearOfSigns(candidate,parent))continue;
                            candidate.y=floor+.025f;
                            bool corners=true;
                            foreach(var offset in new[]{Vector3.right*1.7f,Vector3.left*1.7f,Vector3.forward*1.7f,Vector3.back*1.7f})
                                if(!Supported(candidate+offset,bridge.position.y,out var edge)||Mathf.Abs(edge-floor)>.22f){corners=false;break;}
                            if(!corners)continue;
                            point=candidate;found=true;break;
                        }
                        if(found)break;
                    }
                    if(!found)continue;
                    var group=new GameObject("Bridge_"+index+"_"+(side<0?"Left":"Right")+"_Bank_Garden").transform;
                    group.SetParent(root,false);group.position=point;
                    group.rotation=Quaternion.LookRotation(-bridge.right*side,Vector3.up);
                    Part(group,"Weathered_Bank_Marker",stoneMesh,stone);
                    Part(group,"Book_Rune_And_Collars",inlayMesh,trim);
                    Part(group,"Fern_And_Leaf_Composition",plantMesh,foliage);
                    Part(group,"Quiet_Lavender_Flowers",flowerMesh,flowerMaterial);
                    LastClusterCount++;
                }
            }
            AssetDatabase.SaveAssets();
        }
        static Bounds BridgeBounds(Transform bridge,MeshFilter filter)
        {
            var b=filter.sharedMesh.bounds;var points=new List<Vector3>();
            foreach(int x in new[]{-1,1})foreach(int y in new[]{-1,1})foreach(int z in new[]{-1,1})
                points.Add(bridge.InverseTransformPoint(filter.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z)))));
            var bounds=new Bounds(points[0],Vector3.zero);foreach(var point in points)bounds.Encapsulate(point);return bounds;
        }
        static bool Supported(Vector3 point,float expected,out float height)
        {
            height=expected;
            var hits=Physics.RaycastAll(new Vector3(point.x,expected+2,point.z),Vector3.down,5,~(1<<7),QueryTriggerInteraction.Ignore)
                .Where(h=>h.normal.y>.85f&&Mathf.Abs(h.point.y-expected)<.35f&&h.collider.GetComponentInParent<Player>()==null)
                .OrderBy(h=>h.distance).ToArray();
            if(hits.Length==0)return false;height=hits[0].point.y;return true;
        }
        static bool ClearOfSigns(Vector3 point,Transform parent)
        {
            foreach(var transform in parent.GetComponentsInChildren<Transform>(true))
            {
                if(!transform.name.Contains("Route_Choice")&&!transform.name.Contains("Path_Waymark"))continue;
                foreach(var renderer in transform.GetComponentsInChildren<Renderer>(true))
                {
                    var bounds=renderer.bounds;var level=point;level.y=bounds.center.y;
                    if((bounds.ClosestPoint(level)-level).sqrMagnitude<(FootprintRadius+3)*(FootprintRadius+3))return false;
                }
            }
            return true;
        }
        static void Part(Transform parent,string name,Mesh mesh,Material material)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;
            renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
        }
        static Material FlowerMaterial()
        {
            const string name="Archive_Bank_Lavender";var path=Folder+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,enableInstancing=true};AssetDatabase.CreateAsset(material,path);}
            material.SetColor("_BaseColor",new Color(.59f,.49f,.76f));material.SetFloat("_Smoothness",.12f);material.SetFloat("_Surface",0);material.renderQueue=2000;EditorUtility.SetDirty(material);return material;
        }
        static void Fern(Shape shape,Vector3 center,float scale,float yaw)
        {
            var rotation=Quaternion.Euler(0,yaw,0);
            for(int i=0;i<5;i++)
            {
                float a=i*72*Mathf.Deg2Rad;var heading=rotation*new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                var side=Vector3.Cross(Vector3.up,heading);var end=center+heading*.86f*scale+Vector3.up*.56f*scale;
                shape.Line(center,end,.022f*scale);
                for(int j=1;j<6;j++)
                {
                    float u=j/6f;var p=Vector3.Lerp(center,end,u);float width=Mathf.Sin(u*Mathf.PI)*.27f*scale;
                    foreach(int sign in new[]{-1,1})
                    {
                        var tip=p+side*(width*sign)-heading*.12f*scale+Vector3.up*.06f*scale;
                        shape.Leaf(p-heading*.10f*scale,tip,p+heading*.09f*scale);
                    }
                }
            }
        }
        static void Flower(Shape shape,Vector3 center,float size,float yaw)
        {
            for(int i=0;i<5;i++)
            {
                float a=(yaw+i*72)*Mathf.Deg2Rad;var d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var side=Vector3.Cross(Vector3.up,d);
                shape.Quad(center,center+d*size+side*size*.42f+Vector3.up*.025f,center+d*size*1.5f+Vector3.up*.045f,center+d*size-side*size*.42f+Vector3.up*.025f,true);
            }
        }
        sealed class Shape
        {
            readonly List<Vector3> vertices=new List<Vector3>();readonly List<int> indices=new List<int>();
            void Triangle(Vector3 a,Vector3 b,Vector3 c){int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);indices.AddRange(new[]{n,n+1,n+2});}
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,bool twoSided=false)
            {Triangle(a,b,c);Triangle(a,c,d);if(twoSided){Triangle(c,b,a);Triangle(d,c,a);}}
            public void Leaf(Vector3 a,Vector3 tip,Vector3 b)
            {var mid=(a+b)*.5f;var normal=Vector3.Cross(tip-a,b-a).normalized;Quad(a,tip, b,mid-normal*.035f,true);}
            public void Column(Vector3 center,float lower,float upper,float height,int sides)
            {
                for(int i=0;i<sides;i++)
                {
                    float a=(i+.5f)*Mathf.PI*2/sides,b=(i+1.5f)*Mathf.PI*2/sides;
                    var p=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var q=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b));var h=Vector3.up*height*.5f;
                    Quad(center+q*lower-h,center+p*lower-h,center+p*upper+h,center+q*upper+h);
                    Triangle(center+h,center+q*upper+h,center+p*upper+h);Triangle(center-h,center+p*lower-h,center+q*lower-h);
                }
            }
            public void Block(Vector3 center,Vector3 size,float bevel,float yaw=0)
            {
                var rotation=Quaternion.Euler(0,yaw,0);float x=size.x*.5f,z=size.z*.5f,b=Mathf.Min(bevel,Mathf.Min(x,z)*.45f);
                var ring=new[]{new Vector3(-x+b,0,-z),new Vector3(x-b,0,-z),new Vector3(x,0,-z+b),new Vector3(x,0,z-b),new Vector3(x-b,0,z),new Vector3(-x+b,0,z),new Vector3(-x,0,z-b),new Vector3(-x,0,-z+b)};
                for(int i=0;i<ring.Length;i++)
                {
                    int j=(i+1)%ring.Length;var lo=center+rotation*(ring[i]-Vector3.up*size.y*.5f);var next=center+rotation*(ring[j]-Vector3.up*size.y*.5f);
                    var hi=center+rotation*(ring[i]*.96f+Vector3.up*size.y*.5f);var highNext=center+rotation*(ring[j]*.96f+Vector3.up*size.y*.5f);
                    Quad(next,lo,hi,highNext);Triangle(center+Vector3.up*size.y*.5f,highNext,hi);
                }
            }
            public void Line(Vector3 a,Vector3 b,float width)
            {
                var d=b-a;if(d.sqrMagnitude<.000001f)return;var q=Quaternion.LookRotation(d);var c=(a+b)*.5f;
                Vector3 P(float x,float y,float z)=>c+q*new Vector3(x*width,y*width,z*d.magnitude);
                Quad(P(.5f,-.5f,-.5f),P(-.5f,-.5f,-.5f),P(-.5f,.5f,-.5f),P(.5f,.5f,-.5f));
                Quad(P(-.5f,-.5f,.5f),P(.5f,-.5f,.5f),P(.5f,.5f,.5f),P(-.5f,.5f,.5f));
                Quad(P(-.5f,-.5f,-.5f),P(-.5f,-.5f,.5f),P(-.5f,.5f,.5f),P(-.5f,.5f,-.5f));
                Quad(P(.5f,-.5f,.5f),P(.5f,-.5f,-.5f),P(.5f,.5f,-.5f),P(.5f,.5f,.5f));
                Quad(P(-.5f,.5f,.5f),P(.5f,.5f,.5f),P(.5f,.5f,-.5f),P(-.5f,.5f,-.5f));
            }
            public Mesh Save(string name)
            {
                var path=Folder+"/"+name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(mesh==null){mesh=new Mesh{name=name};AssetDatabase.CreateAsset(mesh,path);}
                mesh.Clear();mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();var normals=mesh.normals;var uv=new List<Vector2>();
                for(int i=0;i<vertices.Count;i++){var p=vertices[i];var n=normals[i];var a=new Vector3(Mathf.Abs(n.x),Mathf.Abs(n.y),Mathf.Abs(n.z));uv.Add((a.y>=a.x&&a.y>=a.z?new Vector2(p.x,p.z):a.x>=a.z?new Vector2(p.z,p.y):new Vector2(p.x,p.y))*.65f);}
                mesh.SetUVs(0,uv);EditorUtility.SetDirty(mesh);return mesh;
            }
        }
    }
}
