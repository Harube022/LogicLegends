using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>One opaque road union. Original render meshes, collision and navigation are retained untouched.</summary>
    public static class ArchiveRoadSurfaceBuilder
    {
        const string Folder=ArchiveTopicSetup.Folder+"/Art/EnvironmentPolish";
        const string LedgerPath=Folder+"/RoadSourceLinks.json";
        const float Cell=8,Height=.065f,Shoulder=1.8f,MaxEdge=3;
        const double AreaEpsilon=.0000001;
        [Serializable] public sealed class Report
        {
            public bool passed;
            public int meshCount,beforeTriangles,afterTriangles,disabledRenderers,boundaryEdges,terrainSamples,terrainFallbacks;
            public float sourceProjectedArea,unifiedProjectedArea,removedOverlapArea,maxTriangleEdge;
            public string scope="Saved render-only union of the actual current main/secondary road faces. Original renderers are disabled; original meshes, transforms, colliders and navigation are not modified.";
        }
        [Serializable] sealed class Link {public string hierarchy,mesh;public bool rendererEnabled;}
        [Serializable] sealed class Ledger {public List<Link> sources=new List<Link>();}
        sealed class Face
        {
            public Vector2 a,b,c;public Rect bounds;
            public Face(Vector2 a,Vector2 b,Vector2 c)
            {
                if(Cross(b-a,c-a)<0){var swap=b;b=c;c=swap;}
                this.a=a;this.b=b;this.c=c;
                var min=Vector2.Min(a,Vector2.Min(b,c));var max=Vector2.Max(a,Vector2.Max(b,c));
                bounds=Rect.MinMaxRect(min.x,min.y,max.x,max.y);
            }
        }
        sealed class TerrainFace
        {
            public Vector2 a,b,c;public Color ca,cb,cc;public Rect bounds;
        }
        sealed class Edge {public Vector2 a,b;public Rect bounds;}
        static double Cross(Vector2 a,Vector2 b)=>(double)a.x*b.y-(double)a.y*b.x;
        static double Side(Vector2 p,Vector2 a,Vector2 b)=>Cross(b-a,p-a);
        static Vector2 XZ(Vector3 p)=>new Vector2(p.x,p.z);
        static long Key(int x,int y)=>((long)x<<32)|(uint)y;
        static int Grid(float coordinate)=>Mathf.FloorToInt(coordinate/Cell);
        static IEnumerable<long> Cells(Rect bounds)
        {for(int x=Grid(bounds.xMin);x<=Grid(bounds.xMax);x++)for(int y=Grid(bounds.yMin);y<=Grid(bounds.yMax);y++)yield return Key(x,y);}
        static Rect Expanded(Rect bounds,float amount)=>Rect.MinMaxRect(bounds.xMin-amount,bounds.yMin-amount,bounds.xMax+amount,bounds.yMax+amount);
        sealed class FaceGrid
        {
            readonly Dictionary<long,List<Face>> cells=new Dictionary<long,List<Face>>();
            public void Add(Face face)
            {foreach(long key in Cells(face.bounds)){if(!cells.TryGetValue(key,out var list)){list=new List<Face>();cells.Add(key,list);}list.Add(face);}}
            public List<Face> Candidates(Rect bounds)
            {
                var found=new HashSet<Face>();var result=new List<Face>();
                foreach(long key in Cells(bounds))if(cells.TryGetValue(key,out var list))foreach(var face in list)
                    if(found.Add(face)&&face.bounds.Overlaps(bounds))result.Add(face);
                return result;
            }
            public bool Contains(Vector2 point)
            {
                if(!cells.TryGetValue(Key(Grid(point.x),Grid(point.y)),out var list))return false;
                foreach(var face in list)
                    if(point.x>=face.bounds.xMin-.0001f&&point.x<=face.bounds.xMax+.0001f&&point.y>=face.bounds.yMin-.0001f&&point.y<=face.bounds.yMax+.0001f&&
                       Side(point,face.a,face.b)>=-.00001&&Side(point,face.b,face.c)>=-.00001&&Side(point,face.c,face.a)>=-.00001)return true;
                return false;
            }
        }
        static double Area(IList<Vector2> polygon)
        {
            if(polygon.Count<3)return 0;
            var origin=polygon[0];double area=0;
            for(int i=1;i<polygon.Count-1;i++)area+=Cross(polygon[i]-origin,polygon[i+1]-origin);
            return Math.Abs(area)*.5;
        }
        static List<Vector2> Clip(IList<Vector2> polygon,Vector2 a,Vector2 b,bool inside)
        {
            var result=new List<Vector2>(polygon.Count+2);
            for(int i=0;i<polygon.Count;i++)
            {
                var p=polygon[i];var q=polygon[(i+1)%polygon.Count];double dp=Side(p,a,b),dq=Side(q,a,b);
                bool pin=inside?dp>=0:dp<=0,qin=inside?dq>=0:dq<=0;
                if(pin)result.Add(p);
                if(pin!=qin)result.Add(Vector2.LerpUnclamped(p,q,(float)(dp/(dp-dq))));
            }
            return result;
        }
        static double Intersection(IList<Vector2> polygon,Face face)
        {
            var p=Clip(polygon,face.a,face.b,true);if(p.Count<3)return 0;
            p=Clip(p,face.b,face.c,true);if(p.Count<3)return 0;
            return Area(Clip(p,face.c,face.a,true));
        }
        static List<List<Vector2>> Subtract(List<Vector2> polygon,Face face)
        {
            var result=new List<List<Vector2>>();var current=polygon;var triangle=new[]{face.a,face.b,face.c};
            for(int edge=0;edge<3&&current.Count>=3;edge++)
            {
                var a=triangle[edge];var b=triangle[(edge+1)%3];
                var outside=Clip(current,a,b,false);
                if(outside.Count>=3&&Area(outside)>AreaEpsilon)result.Add(outside);
                current=Clip(current,a,b,true);
            }
            return result;
        }
        static int Priority(string name)
        {
            if(name.StartsWith("Radial_",StringComparison.Ordinal))return 0;
            if(name=="Continuous_Main_Ring_10m")return 1;
            if(name.StartsWith("Bridge_Dry_Approach_",StringComparison.Ordinal))return 2;
            if(name=="Secondary_Loops_And_SlotApproaches_6m")return 3;
            return 4;
        }
        static string Relative(Transform transform,Transform map)=>AnimationUtility.CalculateTransformPath(transform,map);
        static List<Face> ReadFaces(Mesh mesh,Transform transform)
        {
            var result=new List<Face>();var vertices=mesh.vertices;var triangles=mesh.triangles;
            for(int i=0;i<triangles.Length;i+=3)
            {
                var a=transform.TransformPoint(vertices[triangles[i]]);var b=transform.TransformPoint(vertices[triangles[i+1]]);var c=transform.TransformPoint(vertices[triangles[i+2]]);
                var normal=Vector3.Cross(b-a,c-a);
                if(normal.sqrMagnitude<1e-12f||normal.normalized.y<.95f||Mathf.Max(a.y,Mathf.Max(b.y,c.y))>=1)continue;
                var face=new Face(XZ(a),XZ(b),XZ(c));
                if(Area(new[]{face.a,face.b,face.c})>AreaEpsilon)result.Add(face);
            }
            return result;
        }
        static List<Edge> Boundary(IEnumerable<Face> triangles,FaceGrid coverage)
        {
            var edges=new List<Edge>();var unique=new HashSet<string>();
            string VertexKey(Vector2 p)=>Mathf.RoundToInt(p.x*1000)+","+Mathf.RoundToInt(p.y*1000);
            foreach(var triangle in triangles)
            {
                var points=new[]{triangle.a,triangle.b,triangle.c};
                for(int i=0;i<3;i++)
                {
                    var start=points[i];var finish=points[(i+1)%3];var delta=finish-start;
                    if(delta.sqrMagnitude<.000001f)continue;
                    // A long radial edge can cross the ring: its midpoint is internal
                    // while both ends remain an exterior verge. Test short spans so
                    // that the ring cannot hide the rest of that road's shoulder.
                    int spans=Mathf.Max(1,Mathf.CeilToInt(delta.magnitude/2));
                    var perpendicular=new Vector2(-delta.y,delta.x).normalized*.035f;
                    for(int span=0;span<spans;span++)
                    {
                        var a=Vector2.LerpUnclamped(start,finish,(float)span/spans);
                        var b=Vector2.LerpUnclamped(start,finish,(float)(span+1)/spans);var mid=(a+b)*.5f;
                        if(coverage.Contains(mid+perpendicular)==coverage.Contains(mid-perpendicular))continue;
                        string ka=VertexKey(a),kb=VertexKey(b);string key=string.CompareOrdinal(ka,kb)<0?ka+"|"+kb:kb+"|"+ka;
                        if(!unique.Add(key))continue;
                        var min=Vector2.Min(a,b);var max=Vector2.Max(a,b);
                        edges.Add(new Edge{a=a,b=b,bounds=Rect.MinMaxRect(min.x,min.y,max.x,max.y)});
                    }
                }
            }
            return edges;
        }
        sealed class EdgeGrid
        {
            readonly Dictionary<long,List<Edge>> cells=new Dictionary<long,List<Edge>>();
            public EdgeGrid(IEnumerable<Edge> edges)
            {foreach(var edge in edges)foreach(long key in Cells(Expanded(edge.bounds,Shoulder))){if(!cells.TryGetValue(key,out var list)){list=new List<Edge>();cells.Add(key,list);}list.Add(edge);}}
            public float Weight(Vector2 point)
            {
                float closest=Shoulder*Shoulder;
                if(cells.TryGetValue(Key(Grid(point.x),Grid(point.y)),out var list))foreach(var edge in list)
                {
                    var delta=edge.b-edge.a;float t=Mathf.Clamp01(Vector2.Dot(point-edge.a,delta)/delta.sqrMagnitude);
                    closest=Mathf.Min(closest,(point-edge.a-delta*t).sqrMagnitude);
                }
                return Mathf.Clamp01(Mathf.Sqrt(closest)/Shoulder);
            }
        }
        sealed class TerrainSampler
        {
            readonly Dictionary<long,List<TerrainFace>> cells=new Dictionary<long,List<TerrainFace>>();
            readonly Vector3[] positions;readonly Color[] colors;
            readonly Dictionary<long,Color> cache=new Dictionary<long,Color>();
            public int samples,fallbacks;
            public TerrainSampler(Transform map)
            {
                var filter=map.Find("Environment/Terrain/Island_Grassland_WithRiverHoles").GetComponent<MeshFilter>();
                var mesh=filter.sharedMesh;positions=mesh.vertices.Select(filter.transform.TransformPoint).ToArray();colors=mesh.colors;
                var triangles=mesh.triangles;
                for(int i=0;i<triangles.Length;i+=3)
                {
                    int a=triangles[i],b=triangles[i+1],c=triangles[i+2];var p=XZ(positions[a]);var q=XZ(positions[b]);var r=XZ(positions[c]);
                    if(Math.Abs(Cross(q-p,r-p))<.000001)continue;
                    var min=Vector2.Min(p,Vector2.Min(q,r));var max=Vector2.Max(p,Vector2.Max(q,r));
                    var face=new TerrainFace{a=p,b=q,c=r,ca=Read(a),cb=Read(b),cc=Read(c),bounds=Rect.MinMaxRect(min.x,min.y,max.x,max.y)};
                    foreach(long key in Cells(face.bounds)){if(!cells.TryGetValue(key,out var list)){list=new List<TerrainFace>();cells.Add(key,list);}list.Add(face);}
                }
            }
            Color Read(int index)=>colors.Length==positions.Length?colors[index]:new Color(.12f,.22f,.045f);
            public Color At(Vector2 point)
            {
                long pointKey=Key(Mathf.RoundToInt(point.x*1000),Mathf.RoundToInt(point.y*1000));
                if(cache.TryGetValue(pointKey,out var cached))return cached;
                samples++;
                if(cells.TryGetValue(Key(Grid(point.x),Grid(point.y)),out var list))foreach(var face in list)
                {
                    if(point.x<face.bounds.xMin-.001f||point.x>face.bounds.xMax+.001f||point.y<face.bounds.yMin-.001f||point.y>face.bounds.yMax+.001f)continue;
                    double denominator=Cross(face.b-face.a,face.c-face.a);
                    float u=(float)(Cross(point-face.a,face.c-face.a)/denominator),v=(float)(Cross(face.b-face.a,point-face.a)/denominator),w=1-u-v;
                    if(u<-.0001f||v<-.0001f||w<-.0001f)continue;
                    var color=face.ca*w+face.cb*u+face.cc*v;cache.Add(pointKey,color);return color;
                }
                fallbacks++;int nearest=0;float distance=float.MaxValue;
                for(int i=0;i<positions.Length;i++){float d=(XZ(positions[i])-point).sqrMagnitude;if(d<distance){distance=d;nearest=i;}}
                cached=Read(nearest);cache.Add(pointKey,cached);return cached;
            }
        }
        public static Report Build(Transform map,Transform parent,Material roadMaterial)
        {
            if(Application.isPlaying)throw new InvalidOperationException("Author the road union in stopped Edit Mode.");
            if(map==null||parent==null||roadMaterial==null)throw new ArgumentNullException("Existing map, decoration parent and shoulder material are required.");
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var ledger=File.Exists(LedgerPath)?JsonUtility.FromJson<Ledger>(File.ReadAllText(LedgerPath)):new Ledger();
            if(ledger==null)ledger=new Ledger();
            var environment=map.Find("Environment");var sources=new List<MeshFilter>();
            foreach(string group in new[]{"MainPaths","SecondaryPaths"})
                sources.AddRange(environment.Find(group).GetComponentsInChildren<MeshFilter>(true).Where(f=>f.gameObject.activeInHierarchy&&f.sharedMesh!=null&&f.GetComponent<MeshRenderer>()!=null&&
                    !f.name.StartsWith("Empty_Ground_",StringComparison.Ordinal)&&f.name!="Clearing_Buried_Foundation_Corners"));
            sources=sources.OrderBy(f=>Priority(f.name)).ThenBy(f=>f.name,StringComparer.Ordinal).ToList();
            var report=new Report();var coverage=new FaceGrid();var union=new List<Face>();
            foreach(var filter in sources)
            {
                var renderer=filter.GetComponent<MeshRenderer>();string hierarchy=Relative(filter.transform,map);
                var link=ledger.sources.FirstOrDefault(l=>l.hierarchy==hierarchy);
                if(link==null){link=new Link{hierarchy=hierarchy,mesh=AssetDatabase.GetAssetPath(filter.sharedMesh),rendererEnabled=renderer.enabled};ledger.sources.Add(link);}
                var source=AssetDatabase.LoadAssetAtPath<Mesh>(link.mesh);
                if(source==null)throw new InvalidOperationException("Original road source is missing: "+link.mesh);
                foreach(var face in ReadFaces(source,filter.transform))
                {
                    report.beforeTriangles++;report.sourceProjectedArea+=(float)Area(new[]{face.a,face.b,face.c});
                    var pieces=new List<List<Vector2>>{new List<Vector2>{face.a,face.b,face.c}};
                    foreach(var prior in coverage.Candidates(face.bounds))
                    {
                        if(pieces.Count==0)break;
                        var next=new List<List<Vector2>>();
                        foreach(var piece in pieces)
                            if(Intersection(piece,prior)>AreaEpsilon)next.AddRange(Subtract(piece,prior));else next.Add(piece);
                        pieces=next;
                    }
                    foreach(var piece in pieces)for(int i=1;i<piece.Count-1;i++)
                    {
                        if(Math.Abs(Cross(piece[i]-piece[0],piece[i+1]-piece[0]))<=AreaEpsilon*2)continue;
                        var accepted=new Face(piece[0],piece[i],piece[i+1]);union.Add(accepted);
                        report.unifiedProjectedArea+=(float)Area(new[]{accepted.a,accepted.b,accepted.c});
                    }
                    coverage.Add(face);
                }
            }
            if(union.Count==0)throw new InvalidOperationException("No valid road union faces.");
            var boundary=Boundary(union,coverage);var edges=new EdgeGrid(boundary);var terrain=new TerrainSampler(map);
            if(boundary.Count==0)throw new InvalidOperationException("The road union has no detected exterior boundary.");
            var old=parent.Find("Unified_Road_Surface");if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
            var root=new GameObject("Unified_Road_Surface").transform;root.SetParent(parent,false);root.position=Vector3.zero;root.rotation=Quaternion.identity;
            var positions=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var terrainColors=new List<Vector3>();var weights=new List<Color>();
            void Vertex(Vector2 point)
            {
                positions.Add(root.InverseTransformPoint(new Vector3(point.x,Height,point.y)));normals.Add(root.InverseTransformDirection(Vector3.up));uv.Add(point);
                float weight=edges.Weight(point);weights.Add(new Color(weight,0,0,1));var grass=terrain.At(point);terrainColors.Add(new Vector3(grass.r,grass.g,grass.b));
            }
            void Triangle(Vector2 a,Vector2 b,Vector2 c,int depth)
            {
                float ab=(b-a).sqrMagnitude,bc=(c-b).sqrMagnitude,ca=(a-c).sqrMagnitude;
                if(depth<14&&Mathf.Max(ab,Mathf.Max(bc,ca))>MaxEdge*MaxEdge+.0001f)
                {
                    if(ab>=bc&&ab>=ca){var mid=(a+b)*.5f;Triangle(a,mid,c,depth+1);Triangle(mid,b,c,depth+1);}
                    else if(bc>=ca){var mid=(b+c)*.5f;Triangle(a,b,mid,depth+1);Triangle(a,mid,c,depth+1);}
                    else{var mid=(c+a)*.5f;Triangle(a,b,mid,depth+1);Triangle(mid,b,c,depth+1);}
                    return;
                }
                report.maxTriangleEdge=Mathf.Max(report.maxTriangleEdge,Mathf.Sqrt(Mathf.Max(ab,Mathf.Max(bc,ca))));
                // XZ counter-clockwise maps to a downward 3D normal, so reverse it.
                Vertex(a);Vertex(c);Vertex(b);
            }
            foreach(var face in union)Triangle(face.a,face.b,face.c,0);
            string meshPath=Folder+"/Archive_Unified_Road_Surface.asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if(mesh==null){mesh=new Mesh{name="Archive_Unified_Road_Surface"};AssetDatabase.CreateAsset(mesh,meshPath);}
            else if(UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Include,FindObjectsSortMode.None).Any(c=>c.sharedMesh==mesh))
                throw new InvalidOperationException("Road render asset is unexpectedly used for collision.");
            mesh.Clear();mesh.indexFormat=positions.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16;
            mesh.SetVertices(positions);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetUVs(1,terrainColors);mesh.SetColors(weights);
            mesh.SetTriangles(Enumerable.Range(0,positions.Count).ToArray(),0);mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
            root.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;var draw=root.gameObject.AddComponent<MeshRenderer>();draw.sharedMaterial=roadMaterial;
            draw.shadowCastingMode=ShadowCastingMode.Off;draw.receiveShadows=true;draw.lightProbeUsage=LightProbeUsage.Off;draw.reflectionProbeUsage=ReflectionProbeUsage.Off;
            foreach(var filter in sources){var renderer=filter.GetComponent<MeshRenderer>();renderer.enabled=false;EditorUtility.SetDirty(renderer);report.disabledRenderers++;}
            report.meshCount=1;report.afterTriangles=positions.Count/3;report.boundaryEdges=boundary.Count;report.terrainSamples=terrain.samples;report.terrainFallbacks=terrain.fallbacks;
            report.removedOverlapArea=Mathf.Max(0,report.sourceProjectedArea-report.unifiedProjectedArea);
            report.passed=report.unifiedProjectedArea>0&&report.maxTriangleEdge<=MaxEdge+.002f;
            File.WriteAllText(LedgerPath,JsonUtility.ToJson(ledger,true));
            Directory.CreateDirectory("Temp/ArchiveRoads");File.WriteAllText("Temp/ArchiveRoads/Union.json",JsonUtility.ToJson(report,true));
            AssetDatabase.SaveAssets();return report;
        }
    }
}
