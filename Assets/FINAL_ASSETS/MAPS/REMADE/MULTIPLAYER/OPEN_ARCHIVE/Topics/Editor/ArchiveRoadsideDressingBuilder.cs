using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>Broken verge masonry and small plant drifts, outside the existing walking surfaces.</summary>
    public static class ArchiveRoadsideDressingBuilder
    {
        const string Folder=ArchiveTopicSetup.Folder+"/Art/EnvironmentPolish";
        public const string RootName="Roadside_Dressing";
        const float Footprint=3.35f;
        [Serializable] public sealed class Report
        {
            public int clusters,renderers,triangles,skippedCandidates;
            public List<Vector3> placementPoints=new List<Vector3>();
            public string scope="Editor-authored scenery only. Discontinuous low stones and plant drifts stay outside original paved travel lanes, topic courts, bridges and solid props. No collision, lighting, particles or gameplay state is added.";
        }
        public static Report LastReport {get;private set;}
        public static Vector3[] LastPlacementPoints=>LastReport==null?Array.Empty<Vector3>():LastReport.placementPoints.ToArray();

        public static Transform Build(ArchiveTopicSession session,Transform parent,Material stone,Material trim,Material foliage)
        {
            if(Application.isPlaying)throw new InvalidOperationException("Author roadside scenery in stopped Edit Mode.");
            if(session==null||parent==null||stone==null||trim==null||foliage==null)throw new ArgumentNullException("Existing scene and scenery materials are required.");
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var old=parent.Find(RootName);if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
            var root=new GameObject(RootName).transform;root.SetParent(parent,false);
            var map=GameObject.Find("OpenArchive");if(map==null)throw new InvalidOperationException("OpenArchive map is missing.");
            var environment=map.transform.Find("Environment");var placement=new ArchiveWayfindingPlacement(environment);
            var bridges=environment.Find("Bridges");
            var bridgeAnchors=bridges==null?Array.Empty<Transform>():bridges.Cast<Transform>().Where(t=>t.name.StartsWith("Bridge_Modular_",StringComparison.Ordinal)).ToArray();
            var ownedScenery=parent.GetComponentsInChildren<Renderer>(true).Where(r=>r.gameObject.activeInHierarchy&&r.bounds.size.x<35&&r.bounds.size.z<35).ToArray();
            Shape rock=new Shape(root),metal=new Shape(root),leaves=new Shape(root),flowers=new Shape(root);
            var report=new Report();LastReport=report;

            bool Place(Vector3 proposed,Vector3 outward,int seed)
            {
                if(report.clusters>=28)return false;
                if(new Vector2(proposed.x,proposed.z).magnitude<58||
                    session.stations.Any(s=>new Vector2(s.transform.position.x-proposed.x,s.transform.position.z-proposed.z).magnitude<44+Footprint)||
                    (new Vector2(proposed.x,proposed.z+210)).magnitude<34||
                    report.placementPoints.Any(p=>(p-proposed).sqrMagnitude<13*13))
                {report.skippedCandidates++;return false;}
                foreach(var bridge in bridgeAnchors)
                {
                    var delta=proposed-bridge.position;
                    if(Mathf.Abs(Vector3.Dot(delta,bridge.right))<22&&Mathf.Abs(Vector3.Dot(delta,bridge.forward))<55)
                    {report.skippedCandidates++;return false;}
                }
                if(!placement.ClearOfRoads(proposed,Footprint+.25f)||!placement.ClearOfGroundcover(proposed,Footprint+.3f)||
                    !ArchiveWayfindingPlacement.Ground(proposed,0,out var ground)||!ArchiveWayfindingPlacement.ClearOfProps(ground,Footprint))
                {report.skippedCandidates++;return false;}
                foreach(var renderer in ownedScenery)
                {
                    var point=ground;point.y=renderer.bounds.center.y;
                    if((renderer.bounds.ClosestPoint(point)-point).sqrMagnitude<(Footprint+1)*(Footprint+1))
                    {report.skippedCandidates++;return false;}
                }
                // Confirm the full drift rests on the same dry shoulder, rather than
                // projecting foliage over a river lip from one supported center point.
                foreach(var offset in new[]{Vector3.right*2.8f,Vector3.left*2.8f,Vector3.forward*2.8f,Vector3.back*2.8f})
                    if(!ArchiveWayfindingPlacement.Ground(ground+offset,ground.y,out var edge)||Mathf.Abs(edge.y-ground.y)>.22f)
                    {report.skippedCandidates++;return false;}
                outward.y=0;outward.Normalize();var rotation=Quaternion.LookRotation(outward,Vector3.up);
                foreach(var shape in new[]{rock,metal,leaves,flowers})shape.Frame(ground,rotation);
                Compose(rock,metal,leaves,flowers,seed);
                report.placementPoints.Add(ground);report.clusters++;return true;
            }

            // Handfuls of broken stones follow a scalloped rhythm around the ring.
            // Alternating radius and angle offsets avoid another continuous straight kerb.
            for(int i=0;i<48&&report.clusters<20;i++)
            {
                float a=(i*7.5f+3.2f+(i%3-1)*.9f)*Mathf.Deg2Rad;
                float radius=152.2f+(i%4)*1.05f;
                var p=new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius);
                Place(p,new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)),313+i*17);
            }
            // Staggered verge pockets beside the four wide radial avenues.
            for(int direction=0;direction<4&&report.clusters<28;direction++)
            {
                float a=direction*Mathf.PI*.5f;var forward=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a));var side=Vector3.Cross(Vector3.up,forward);
                foreach(float distance in new[]{79f,104f,123f,167f,190f})
                    foreach(int sign in new[]{-1,1})
                    {
                        float lateral=17.4f+((direction+sign+4)%3)*.65f;
                        Place(forward*(distance+(sign<0?3.2f:-2.4f))+side*(lateral*sign),side*sign,907+direction*23+(int)distance+sign);
                    }
            }
            var bloom=FlowerMaterial();
            var buffers=new[]{rock,metal,leaves,flowers};var materials=new[]{stone,trim,foliage,bloom};
            var names=new[]{"Broken_Verge_Masonry","Weathered_Brass_Joints","Small_Grass_And_Fern_Drifts","Soft_Ivory_Flowers"};
            for(int i=0;i<buffers.Length;i++)
            {
                var mesh=buffers[i].Save("Roadside_"+names[i]);report.triangles+=mesh.triangles.Length/3;
                if(mesh.vertexCount==0)continue;
                var go=new GameObject(names[i],typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);
                go.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=materials[i];
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;
                renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                report.renderers++;
            }
            Directory.CreateDirectory("Temp/ArchiveRoadside");File.WriteAllText("Temp/ArchiveRoadside/Dressing.json",JsonUtility.ToJson(report,true));
            AssetDatabase.SaveAssets();return root;
        }
        static void Compose(Shape stone,Shape metal,Shape leaves,Shape flowers,int seed)
        {
            var random=new System.Random(seed);float Range(float a,float b)=>Mathf.Lerp(a,b,(float)random.NextDouble());
            int count=3+seed%3;
            for(int i=0;i<count;i++)
            {
                float x=(i-(count-1)*.5f)*1.02f+Range(-.15f,.15f);
                var p=new Vector3(x,.095f+Range(-.02f,.015f),-.55f+Range(-.27f,.24f));
                float yaw=Range(-13,13);var rotation=Quaternion.Euler(Range(-3,3),yaw,Range(-2,2));
                stone.Stone(p,new Vector3(Range(.75f,1.02f),Range(.16f,.23f),Range(.42f,.62f)),rotation);
                if(i==1&&seed%3==0)metal.Bar(p+rotation*new Vector3(-.22f,.13f,0),p+rotation*new Vector3(.22f,.13f,0),.023f);
            }
            for(int clump=0;clump<4;clump++)
            {
                var origin=new Vector3(-1.9f+clump*1.16f+Range(-.18f,.18f),.025f,.35f+Range(-.2f,.4f));
                for(int leaf=0;leaf<5;leaf++)
                {
                    float angle=leaf*2.39996f+clump*.47f;float length=Range(.35f,.68f);
                    var direction=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));var side=Vector3.Cross(Vector3.up,direction);
                    leaves.Quad(origin,origin+direction*length*.35f+side*.11f+Vector3.up*length*.63f,
                        origin+direction*length*.78f+Vector3.up*length*.78f,origin+direction*length*.35f-side*.11f+Vector3.up*length*.63f,true);
                }
                // A few narrow grass blades break the outline without forming a wall.
                for(int blade=0;blade<3;blade++)
                {
                    var basePoint=origin+new Vector3(Range(-.2f,.2f),0,Range(-.12f,.12f));
                    leaves.Triangle(basePoint-Vector3.right*.025f,basePoint+Vector3.up*Range(.33f,.59f)+Vector3.forward*.08f,basePoint+Vector3.right*.025f,true);
                }
            }
            if(seed%3!=1)for(int i=0;i<5;i++)
            {
                float angle=i*2.39996f;var p=new Vector3(.95f+Mathf.Cos(angle)*.35f,.025f,.8f+Mathf.Sin(angle)*.3f);float height=Range(.23f,.35f);
                leaves.Bar(p,p+Vector3.up*height,.012f);var center=p+Vector3.up*height;
                for(int petal=0;petal<5;petal++)
                {
                    float a=petal*Mathf.PI*2/5+i;var d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var side=Vector3.Cross(Vector3.up,d);
                    flowers.Quad(center,center+d*.07f+side*.035f+Vector3.up*.012f,center+d*.13f,center+d*.07f-side*.035f+Vector3.up*.012f,true);
                }
            }
        }
        static Material FlowerMaterial()
        {
            string path=Folder+"/Roadside_Soft_Ivory.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Roadside_Soft_Ivory",enableInstancing=true};AssetDatabase.CreateAsset(material,path);}
            material.SetColor("_BaseColor",new Color(.88f,.79f,.49f));material.SetFloat("_Surface",0);material.SetFloat("_Smoothness",.12f);material.renderQueue=2000;EditorUtility.SetDirty(material);return material;
        }
        sealed class Shape
        {
            readonly Transform root;readonly List<Vector3> vertices=new List<Vector3>();readonly List<int> indices=new List<int>();
            Matrix4x4 frame;
            public Shape(Transform owner){root=owner;frame=root.worldToLocalMatrix;}
            public void Frame(Vector3 position,Quaternion rotation)=>frame=root.worldToLocalMatrix*Matrix4x4.TRS(position,rotation,Vector3.one);
            public void Triangle(Vector3 a,Vector3 b,Vector3 c,bool twoSided=false)
            {
                int n=vertices.Count;vertices.Add(frame.MultiplyPoint3x4(a));vertices.Add(frame.MultiplyPoint3x4(b));vertices.Add(frame.MultiplyPoint3x4(c));indices.AddRange(new[]{n,n+1,n+2});
                if(twoSided){n=vertices.Count;vertices.Add(frame.MultiplyPoint3x4(c));vertices.Add(frame.MultiplyPoint3x4(b));vertices.Add(frame.MultiplyPoint3x4(a));indices.AddRange(new[]{n,n+1,n+2});}
            }
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,bool twoSided=false)
            {Triangle(a,b,c,twoSided);Triangle(a,c,d,twoSided);}
            public void Stone(Vector3 center,Vector3 size,Quaternion rotation)
            {
                float x=size.x*.5f,z=size.z*.5f,cut=Mathf.Min(x,z)*.32f,h=size.y*.5f;
                var ring=new[]{new Vector3(-x+cut,0,-z),new Vector3(x-cut,0,-z),new Vector3(x,0,-z+cut),new Vector3(x,0,z-cut),
                    new Vector3(x-cut,0,z),new Vector3(-x+cut,0,z),new Vector3(-x,0,z-cut),new Vector3(-x,0,-z+cut)};
                for(int i=0;i<8;i++)
                {
                    int j=(i+1)%8;
                    var a=center+rotation*(ring[i]-Vector3.up*h);var b=center+rotation*(ring[j]-Vector3.up*h);
                    var c=center+rotation*(ring[i]*.90f+Vector3.up*h);var d=center+rotation*(ring[j]*.90f+Vector3.up*h);
                    Quad(b,a,c,d);Triangle(center+rotation*Vector3.up*h,d,c);Triangle(center-rotation*Vector3.up*h,a,b);
                }
            }
            public void Bar(Vector3 from,Vector3 to,float width)
            {
                var delta=to-from;if(delta.sqrMagnitude<.000001f)return;
                var side=Vector3.Cross(delta.normalized,Mathf.Abs(delta.normalized.y)>.9f?Vector3.forward:Vector3.up).normalized*width;
                var up=Vector3.Cross(side.normalized,delta.normalized)*width;
                Quad(from-side,to-side,to+side,from+side);Quad(from-up,to-up,to+up,from+up);
            }
            public Mesh Save(string name)
            {
                string path=Folder+"/"+name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(mesh==null){mesh=new Mesh{name=name};AssetDatabase.CreateAsset(mesh,path);}
                mesh.Clear();mesh.indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16;mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
                var normals=mesh.normals;var uv=new List<Vector2>(vertices.Count);
                for(int i=0;i<vertices.Count;i++){var p=vertices[i];var n=normals[i];var a=new Vector3(Mathf.Abs(n.x),Mathf.Abs(n.y),Mathf.Abs(n.z));uv.Add((a.y>=a.x&&a.y>=a.z?new Vector2(p.x,p.z):a.x>=a.z?new Vector2(p.z,p.y):new Vector2(p.x,p.y))*.65f);}
                mesh.SetUVs(0,uv);EditorUtility.SetDirty(mesh);return mesh;
            }
        }
    }
}
