using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>Seats each decorative shoreline stone on real support without moving its combined mesh or any collider.</summary>
    public static class ArchiveShoreRockGrounding
    {
        const string Folder=ArchiveTopicSetup.Folder+"/Art/EnvironmentPolish";
        const float Embed=.12f;
        const float RequiredSupport=.8f;
        [Serializable] public sealed class Report
        {
            public bool passed,physicsPreserved,navigationPreserved,originalMeshPreserved;
            public int components,alreadySupported,seatedOnShore,shiftedToShore,seatedOnBed,unsupported,verticesMoved;
            public int overhangsDetected,overhangsCorrected,extendedShoreSearches;
            public float maximumHorizontalShift,maximumVerticalShift;
            public string sourceMesh,outputMesh;
            public Vector3[] unsupportedPositions,overhangPositions,movedFromPositions,movedToPositions;
        }
        [Serializable] sealed class SourceRecord {public string path,guid;public long localId;}
        sealed class Rock
        {
            public readonly List<int> vertices=new List<int>();public readonly List<int> triangles=new List<int>();
            public Bounds bounds;
        }
        static string PhysicsSnapshot(Transform map)=>string.Join("\n",map.GetComponentsInChildren<Collider>(true).OrderBy(c=>c.GetInstanceID())
            .Select(c=>c.GetInstanceID()+":"+EditorJsonUtility.ToJson(c)+":"+c.transform.localToWorldMatrix));
        static string NavigationSnapshot(Transform map)=>string.Join("\n",map.GetComponentsInChildren<Component>(true)
            .Where(c=>c is UnityEngine.AI.NavMeshObstacle||c is UnityEngine.AI.NavMeshAgent).OrderBy(c=>c.GetInstanceID())
            .Select(c=>c.GetInstanceID()+":"+EditorJsonUtility.ToJson(c)));
        public static Report Apply(Transform map)
        {
            if(Application.isPlaying)throw new InvalidOperationException("Ground shore scenery outside Play Mode.");
            if(map==null||map.name!="OpenArchive")throw new ArgumentException("Existing OpenArchive map required.");
            var filter=map.Find("Environment/Rocks/Riverbank_RockClusters")?.GetComponent<MeshFilter>();
            var terrain=map.Find("Environment/Terrain/Island_Grassland_WithRiverHoles")?.GetComponent<Collider>();
            if(filter==null||filter.sharedMesh==null||terrain==null)throw new InvalidOperationException("Existing shoreline stones and terrain collider required.");
            if(filter.GetComponent<Collider>()!=null)throw new InvalidOperationException("Only decorative, noncolliding riverbank clusters may move.");
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            string suffix=filter.gameObject.scene.name.Replace(' ','_');string ledgerPath=Folder+"/ShoreRockSource_"+suffix+".json";
            SourceRecord record;
            if(File.Exists(ledgerPath))record=JsonUtility.FromJson<SourceRecord>(File.ReadAllText(ledgerPath));
            else
            {
                if(AssetDatabase.GetAssetPath(filter.sharedMesh).StartsWith(Folder+"/Shore_Rocks_Seated_",StringComparison.Ordinal))throw new InvalidOperationException("Original shoreline rock ledger is missing.");
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(filter.sharedMesh,out string guid,out long localId);
                record=new SourceRecord{path=AssetDatabase.GetAssetPath(filter.sharedMesh),guid=guid,localId=localId};
                File.WriteAllText(ledgerPath,JsonUtility.ToJson(record,true));
            }
            string sourcePath=AssetDatabase.GUIDToAssetPath(record.guid);if(string.IsNullOrEmpty(sourcePath))sourcePath=record.path;
            var source=AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<Mesh>().FirstOrDefault(m=>
            {AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m,out string _,out long localId);return localId==record.localId;});
            if(source==null)throw new InvalidOperationException("Original shoreline rock mesh is unavailable.");
            string physics=PhysicsSnapshot(map),navigation=NavigationSnapshot(map),sourceHash=AssetDatabase.GetAssetDependencyHash(sourcePath).ToString();
            var report=new Report{sourceMesh=sourcePath};var original=source.vertices;
            var world=original.Select(filter.transform.TransformPoint).ToArray();var result=(Vector3[])original.Clone();
            var rocks=Components(source,world);report.components=rocks.Count;
            var beds=map.GetComponentsInChildren<Collider>(true).Where(c=>c.name.StartsWith("Walkable_Riverbed_",StringComparison.Ordinal)).ToArray();
            var water=map.GetComponentsInChildren<Renderer>(true).Where(r=>r.name.StartsWith("Turquoise_RiverPond_",StringComparison.Ordinal)).ToArray();
            float waterLevel=water.Length>0?water.Average(r=>r.bounds.center.y):-2.875f;
            var roads=new ArchiveWayfindingPlacement(map.Find("Environment"));
            var unsupportedPositions=new List<Vector3>();var overhangPositions=new List<Vector3>();
            var movedFromPositions=new List<Vector3>();var movedToPositions=new List<Vector3>();
            foreach(var rock in rocks)
            {
                if(rock.bounds.size.y<.04f||rock.bounds.size.x>12||rock.bounds.size.z>12){report.unsupported++;unsupportedPositions.Add(rock.bounds.center);continue;}
                var shift=Vector3.zero;float gap;
                bool supported=LandGap(rock,world,terrain,shift,out gap,out float support);
                bool overhang=support<RequiredSupport;
                if(overhang){report.overhangsDetected++;overhangPositions.Add(rock.bounds.center);}
                if(supported)
                {
                    if(gap<=.045f){report.alreadySupported++;continue;}
                    shift.y=-gap-Embed;report.seatedOnShore++;
                }
                else
                {
                    bool found=false;
                    // A decorative pebble just beyond a stepped bank is moved to the
                    // nearest supported shore, not sunk to a deep riverbed out of sight.
                    for(float radius=.5f;radius<=6.001f&&!found;radius+=.5f)
                        for(int angle=0;angle<16;angle++)
                        {
                            float phase=angle*Mathf.PI/8;var delta=new Vector3(Mathf.Cos(phase)*radius,0,Mathf.Sin(phase)*radius);
                            var center=rock.bounds.center+delta;
                            float clearance=Mathf.Max(.15f,Mathf.Max(rock.bounds.extents.x,rock.bounds.extents.z)+.15f);
                            if(!roads.ClearOfRoads(center,clearance)||!ArchiveWayfindingPlacement.ClearOfProps(center,clearance)||!TerrainHeight(terrain,center,out _))continue;
                            if(!LandGap(rock,world,terrain,delta,out gap,out _))continue;
                            shift=delta+Vector3.up*(-gap-Embed);found=true;report.shiftedToShore++;
                            if(radius>4.001f)report.extendedShoreSearches++;
                            if(overhang)report.overhangsCorrected++;
                            break;
                        }
                    if(!found)
                    {
                        if(BedGap(rock,world,beds,out gap)&&gap>.045f&&rock.bounds.max.y-gap-Embed>waterLevel+.08f)
                        {shift.y=-gap-Embed;report.seatedOnBed++;}
                        else{report.unsupported++;unsupportedPositions.Add(rock.bounds.center);continue;}
                    }
                }
                if(shift.sqrMagnitude<.000001f){report.alreadySupported++;continue;}
                foreach(int vertex in rock.vertices)result[vertex]=filter.transform.InverseTransformPoint(world[vertex]+shift);
                movedFromPositions.Add(rock.bounds.center);movedToPositions.Add(rock.bounds.center+shift);
                report.verticesMoved+=rock.vertices.Count;
                report.maximumHorizontalShift=Mathf.Max(report.maximumHorizontalShift,new Vector2(shift.x,shift.z).magnitude);
                report.maximumVerticalShift=Mathf.Max(report.maximumVerticalShift,Mathf.Abs(shift.y));
            }
            report.unsupportedPositions=unsupportedPositions.ToArray();report.overhangPositions=overhangPositions.ToArray();
            report.movedFromPositions=movedFromPositions.ToArray();report.movedToPositions=movedToPositions.ToArray();
            string outputPath=Folder+"/Shore_Rocks_Seated_"+suffix+".asset";var output=AssetDatabase.LoadAssetAtPath<Mesh>(outputPath);
            if(output==null){output=new Mesh{name="Shore_Rocks_Seated_"+suffix};AssetDatabase.CreateAsset(output,outputPath);}
            Copy(source,output,result);filter.sharedMesh=output;PrefabUtility.RecordPrefabInstancePropertyModifications(filter);EditorUtility.SetDirty(filter);EditorUtility.SetDirty(output);
            report.outputMesh=outputPath;report.physicsPreserved=physics==PhysicsSnapshot(map);report.navigationPreserved=navigation==NavigationSnapshot(map);
            report.originalMeshPreserved=sourceHash==AssetDatabase.GetAssetDependencyHash(sourcePath).ToString();
            report.passed=report.physicsPreserved&&report.navigationPreserved&&report.originalMeshPreserved&&output.vertexCount==source.vertexCount;
            if(!report.passed)throw new InvalidOperationException("Shore rock preservation verification failed.");
            AssetDatabase.SaveAssets();Directory.CreateDirectory("Temp/ArchiveRoadside");File.WriteAllText("Temp/ArchiveRoadside/ShoreRockGrounding.json",JsonUtility.ToJson(report,true));return report;
        }
        static bool TerrainHeight(Collider terrain,Vector3 point,out float height)
        {
            float top=terrain.bounds.max.y+4;var ray=new Ray(new Vector3(point.x,top,point.z),Vector3.down);
            if(terrain.Raycast(ray,out var hit,top-terrain.bounds.min.y+8)&&hit.normal.y>.6f){height=hit.point.y;return true;}
            height=0;return false;
        }
        static IEnumerable<Vector3> Samples(Bounds bounds)
        {
            // Sample near the actual hull perimeter as well as its centre. A lone
            // supported heel must not validate a broad ledge protruding over water.
            for(int x=-2;x<=2;x++)for(int z=-2;z<=2;z++)yield return bounds.center+new Vector3(x*bounds.extents.x*.4f,0,z*bounds.extents.z*.4f);
        }
        static bool LandGap(Rock rock,Vector3[] vertices,Collider terrain,Vector3 delta,out float gap,out float support)
        {
            gap=float.PositiveInfinity;int valid=0,supported=0;
            foreach(var sample in Samples(rock.bounds))
            {
                if(!Bottom(rock,vertices,sample,out float bottom))continue;
                valid++;
                if(!TerrainHeight(terrain,sample+delta,out float floor))continue;
                supported++;
                gap=Mathf.Min(gap,bottom-floor);
            }
            support=valid>0?(float)supported/valid:0;
            return valid>=5&&support>=RequiredSupport&&!float.IsPositiveInfinity(gap);
        }
        static bool BedGap(Rock rock,Vector3[] vertices,Collider[] beds,out float gap)
        {
            gap=float.PositiveInfinity;
            foreach(var sample in Samples(rock.bounds))
            {
                if(!Bottom(rock,vertices,sample,out float bottom))continue;
                foreach(var bed in beds)
                {
                    float top=rock.bounds.max.y+3;var ray=new Ray(new Vector3(sample.x,top,sample.z),Vector3.down);
                    if(bed.Raycast(ray,out var hit,top-bed.bounds.min.y+3)&&hit.normal.y>.5f)gap=Mathf.Min(gap,bottom-hit.point.y);
                }
            }
            return !float.IsPositiveInfinity(gap);
        }
        static bool Bottom(Rock rock,Vector3[] vertices,Vector3 point,out float bottom)
        {
            bottom=float.PositiveInfinity;
            for(int i=0;i<rock.triangles.Count;i+=3)
            {
                var a=vertices[rock.triangles[i]];var b=vertices[rock.triangles[i+1]];var c=vertices[rock.triangles[i+2]];
                float d=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);if(Mathf.Abs(d)<.0000001f)continue;
                float u=((b.z-c.z)*(point.x-c.x)+(c.x-b.x)*(point.z-c.z))/d;
                float v=((c.z-a.z)*(point.x-c.x)+(a.x-c.x)*(point.z-c.z))/d;
                if(u<-.00001f||v<-.00001f||u+v>1.00001f)continue;
                bottom=Mathf.Min(bottom,a.y*u+b.y*v+c.y*(1-u-v));
            }
            return !float.IsPositiveInfinity(bottom);
        }
        static List<Rock> Components(Mesh mesh,Vector3[] vertices)
        {
            var parent=Enumerable.Range(0,vertices.Length).ToArray();
            int Root(int a){while(parent[a]!=a){parent[a]=parent[parent[a]];a=parent[a];}return a;}
            void Join(int a,int b){a=Root(a);b=Root(b);if(a!=b)parent[b]=a;}
            var welded=new Dictionary<string,int>();
            for(int i=0;i<vertices.Length;i++)
            {
                var p=vertices[i];string key=Mathf.RoundToInt(p.x*10000)+":"+Mathf.RoundToInt(p.y*10000)+":"+Mathf.RoundToInt(p.z*10000);
                if(welded.TryGetValue(key,out int prior))Join(i,prior);else welded.Add(key,i);
            }
            var indices=mesh.triangles;
            for(int i=0;i<indices.Length;i+=3){Join(indices[i],indices[i+1]);Join(indices[i],indices[i+2]);}
            var rocks=new Dictionary<int,Rock>();
            for(int i=0;i<vertices.Length;i++){int root=Root(i);if(!rocks.TryGetValue(root,out var rock)){rock=new Rock{bounds=new Bounds(vertices[i],Vector3.zero)};rocks.Add(root,rock);}rock.vertices.Add(i);rock.bounds.Encapsulate(vertices[i]);}
            for(int i=0;i<indices.Length;i+=3)rocks[Root(indices[i])].triangles.AddRange(new[]{indices[i],indices[i+1],indices[i+2]});
            return rocks.Values.ToList();
        }
        static void Copy(Mesh source,Mesh output,Vector3[] vertices)
        {
            output.Clear();output.indexFormat=source.indexFormat;output.vertices=vertices;
            if(source.HasVertexAttribute(VertexAttribute.Normal))output.normals=source.normals;
            if(source.HasVertexAttribute(VertexAttribute.Tangent))output.tangents=source.tangents;
            if(source.HasVertexAttribute(VertexAttribute.Color))output.colors=source.colors;
            for(int channel=0;channel<8;channel++)
            {
                var attribute=(VertexAttribute)((int)VertexAttribute.TexCoord0+channel);if(!source.HasVertexAttribute(attribute))continue;
                int dimensions=source.GetVertexAttributeDimension(attribute);
                if(dimensions<=2){var values=new List<Vector2>();source.GetUVs(channel,values);output.SetUVs(channel,values);}
                else if(dimensions==3){var values=new List<Vector3>();source.GetUVs(channel,values);output.SetUVs(channel,values);}
                else{var values=new List<Vector4>();source.GetUVs(channel,values);output.SetUVs(channel,values);}
            }
            output.subMeshCount=source.subMeshCount;
            for(int i=0;i<source.subMeshCount;i++)output.SetIndices(source.GetIndices(i),source.GetTopology(i),i,false);
            output.RecalculateBounds();
        }
    }
}
