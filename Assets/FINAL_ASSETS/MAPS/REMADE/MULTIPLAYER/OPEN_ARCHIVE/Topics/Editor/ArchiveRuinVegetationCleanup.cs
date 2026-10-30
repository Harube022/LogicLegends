using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>
    /// Repairs the fern-like spikes on arch legs. These are failed mathematical
    /// carving projections, not ground vegetation. Original scenery is retained.
    /// </summary>
    public static class ArchiveRuinVegetationCleanup
    {
        const string Folder=ArchiveTopicSetup.Folder+"/Art/EnvironmentPolish";
        const string MeshName="Archive_Math_Carvings_SurfaceAligned";
        [Serializable] public sealed class Report
        {
            public bool passed,physicsPreserved,navigationPreserved,originalMeshPreserved;
            public int arches,sourceTriangles,keptStrokeQuads,rejectedStrokeQuads,outputTriangles,bakedFloatingTrianglesRemoved;
            public float largestOutputEdge;
            public string sourceMesh,outputMesh;
        }
        [Serializable] sealed class SourceRecord {public string path,guid;public long localId;}
        struct Hit {public Vector3 position,normal;}
        static string Physics(Transform map)=>string.Join("\n",map.GetComponentsInChildren<Collider>(true).OrderBy(c=>c.GetInstanceID())
            .Select(c=>c.GetInstanceID()+":"+EditorJsonUtility.ToJson(c)));
        static string Navigation(Transform map)=>string.Join("\n",map.GetComponentsInChildren<Component>(true)
            .Where(c=>c is UnityEngine.AI.NavMeshObstacle||c is UnityEngine.AI.NavMeshAgent).OrderBy(c=>c.GetInstanceID())
            .Select(c=>c.GetInstanceID()+":"+EditorJsonUtility.ToJson(c)));
        public static Report Apply(Transform map)
        {
            if(Application.isPlaying)throw new InvalidOperationException("Repair arch carving projections outside Play Mode.");
            if(map==null||map.name!="OpenArchive")throw new ArgumentException("The existing OpenArchive map is required.");
            var filter=map.Find("Environment/Art_Understory/Weathered_Math_Carvings")?.GetComponent<MeshFilter>();
            var nest=map.Find("UniversalMonsterNest");
            if(filter==null||filter.sharedMesh==null||nest==null)throw new InvalidOperationException("Existing arch carvings and monster nest are required.");
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            string sourceRecordPath=Folder+"/ArchCarvingSource.json";
            SourceRecord record;
            if(File.Exists(sourceRecordPath))record=JsonUtility.FromJson<SourceRecord>(File.ReadAllText(sourceRecordPath));
            else
            {
                if(AssetDatabase.GetAssetPath(filter.sharedMesh)==Folder+"/"+MeshName+".asset")throw new InvalidOperationException("Original carving source record is missing.");
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(filter.sharedMesh,out string guid,out long id);
                record=new SourceRecord{path=AssetDatabase.GetAssetPath(filter.sharedMesh),guid=guid,localId=id};
                File.WriteAllText(sourceRecordPath,JsonUtility.ToJson(record,true));
            }
            string sourcePath=AssetDatabase.GUIDToAssetPath(record.guid);
            if(string.IsNullOrEmpty(sourcePath))sourcePath=record.path;
            var original=AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<Mesh>().FirstOrDefault(m=>
            {AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m,out string _,out long id);return id==record.localId;});
            if(original==null)throw new InvalidOperationException("Original arch carving source is unavailable.");
            if(map.GetComponentsInChildren<MeshCollider>(true).Any(c=>c.sharedMesh==filter.sharedMesh||c.sharedMesh==original))
                throw new InvalidOperationException("Carving repair must never alter a collision mesh.");
            var report=new Report{sourceMesh=sourcePath,sourceTriangles=original.triangles.Length/3};
            string physics=Physics(map),navigation=Navigation(map),sourceHash=AssetDatabase.GetAssetDependencyHash(sourcePath).ToString();
            // The original gateway also has six thin rear-face decorations baked
            // into its mesh at a constant depth. They float beside the fractured
            // pillar silhouette. Replace only the render mesh, leaving its original
            // collision mesh and all structural masonry unchanged.
            var gatewayHashes=new Dictionary<string,string>();
            foreach(string variant in new[]{"A","B"})
            {
                string gatewayPath="Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Meshes/Den_Broken_Gateway_"+variant+".asset";
                var gateway=AssetDatabase.LoadAssetAtPath<Mesh>(gatewayPath);
                if(gateway==null)throw new InvalidOperationException("Original gateway render mesh is missing.");
                gatewayHashes.Add(gatewayPath,AssetDatabase.GetAssetDependencyHash(gatewayPath).ToString());
                string cleanPath=Folder+"/Archive_Gateway_NoFloatingStrokes"+(variant=="A"?"":"_B")+".asset";
                var clean=AssetDatabase.LoadAssetAtPath<Mesh>(cleanPath);
                var archFilters=nest.Cast<Transform>().Where(t=>t.name=="Arch_Wide").Select(t=>t.GetComponent<MeshFilter>()).Where(f=>f!=null&&(f.sharedMesh==gateway||(clean!=null&&f.sharedMesh==clean))).ToArray();
                if(archFilters.Length!=2)throw new InvalidOperationException("Unexpected gateway variant count; inspect before replacing geometry.");
                var gatewayVertices=gateway.vertices;var gatewayTriangles=gateway.triangles;var keptGateway=new List<int>();int removed=0;
                bool FloatingStroke(Vector3 p)=>Mathf.Abs(p.z+.98f)<.0001f&&p.y>2.5f&&p.y<4.8f&&Mathf.Abs(p.x)>5.7f&&Mathf.Abs(p.x)<7;
                for(int i=0;i<gatewayTriangles.Length;i+=3)
                {
                    if(FloatingStroke(gatewayVertices[gatewayTriangles[i]])&&FloatingStroke(gatewayVertices[gatewayTriangles[i+1]])&&FloatingStroke(gatewayVertices[gatewayTriangles[i+2]]))
                    {removed++;continue;}
                    keptGateway.AddRange(new[]{gatewayTriangles[i],gatewayTriangles[i+1],gatewayTriangles[i+2]});
                }
                if(removed!=24)throw new InvalidOperationException("Gateway decorative stroke layout changed; inspect before removing geometry.");
                if(clean==null){clean=UnityEngine.Object.Instantiate(gateway);clean.name="Archive_Gateway_NoFloatingStrokes_"+variant;AssetDatabase.CreateAsset(clean,cleanPath);}
                else EditorUtility.CopySerialized(gateway,clean);
                clean.SetTriangles(keptGateway,0);clean.RecalculateBounds();EditorUtility.SetDirty(clean);report.bakedFloatingTrianglesRemoved+=removed;
                foreach(var archFilter in archFilters){archFilter.sharedMesh=clean;EditorUtility.SetDirty(archFilter);}
            }
            var outputVertices=new List<Vector3>();var outputIndices=new List<int>();var outputColors=new List<Color>();
            var tint=new Color(.26f,.32f,.27f).linear;
            int symbol=0;
            foreach(var arch in nest.Cast<Transform>().Where(t=>t.name=="Arch_Wide"))
            {
                var archFilter=arch.GetComponent<MeshFilter>();if(archFilter==null||archFilter.sharedMesh==null)continue;
                report.arches++;var mesh=archFilter.sharedMesh;var vertices=mesh.vertices;var triangles=mesh.triangles;var bounds=mesh.bounds;
                var matrix=filter.transform.worldToLocalMatrix*arch.localToWorldMatrix;
                float x=bounds.min.x+bounds.size.x*.075f,y=bounds.min.y+bounds.size.y*.37f;
                var lines=new List<Vector2[]>();
                if(symbol%2==0)
                {
                    lines.Add(new[]{new Vector2(-.45f,.7f),new Vector2(0,-.7f),new Vector2(.45f,.7f)});
                    lines.Add(new[]{new Vector2(-.26f,.12f),new Vector2(.26f,.12f)});
                }
                else
                {
                    lines.Add(new[]{new Vector2(-.4f,.7f),new Vector2(.4f,.7f),new Vector2(.4f,-.7f),new Vector2(-.4f,-.7f)});
                    lines.Add(new[]{new Vector2(-.4f,0),new Vector2(.4f,0)});
                }
                foreach(var line in lines)for(int segment=1;segment<line.Length;segment++)
                {
                    var a=new Vector3(x+line[segment-1].x,y+line[segment-1].y,0);
                    var b=new Vector3(x+line[segment].x,y+line[segment].y,0);
                    var side=Vector3.Cross((b-a).normalized,Vector3.forward)*.026f;
                    int steps=Mathf.CeilToInt(Vector3.Distance(a,b)/.08f);
                    for(int step=0;step<steps;step++)
                    {
                        var from=Vector3.Lerp(a,b,(float)step/steps);var to=Vector3.Lerp(a,b,(float)(step+1)/steps);
                        var corners=new[]{from-side,to-side,to+side,from+side};var projected=new Hit[4];bool valid=true;
                        for(int corner=0;corner<4;corner++)if(!TryFrontSurface(vertices,triangles,corners[corner],out projected[corner])){valid=false;break;}
                        if(valid)
                        {
                            float spread=projected.Max(p=>p.position.z)-projected.Min(p=>p.position.z);
                            valid=spread<=.12f&&projected.All(p=>Vector3.Dot(p.normal,projected[0].normal)>.985f);
                        }
                        var points=new Vector3[4];
                        if(valid)
                        {
                            for(int i=0;i<4;i++)points[i]=matrix.MultiplyPoint3x4(projected[i].position+projected[i].normal*.014f);
                            float longest=0;for(int i=0;i<4;i++)for(int j=i+1;j<4;j++)longest=Mathf.Max(longest,Vector3.Distance(filter.transform.TransformPoint(points[i]),filter.transform.TransformPoint(points[j])));
                            valid=longest<.65f;
                            if(valid)report.largestOutputEdge=Mathf.Max(report.largestOutputEdge,longest);
                        }
                        if(!valid){report.rejectedStrokeQuads++;continue;}
                        int at=outputVertices.Count;outputVertices.AddRange(points);outputColors.AddRange(new[]{tint,tint,tint,tint});
                        // Stroke orientation is corrected after transforming the source face.
                        var expected=matrix.inverse.transpose.MultiplyVector(projected[0].normal).normalized;
                        if(Vector3.Dot(Vector3.Cross(points[1]-points[0],points[2]-points[0]),expected)>0)
                            outputIndices.AddRange(new[]{at,at+1,at+2,at,at+2,at+3});
                        else outputIndices.AddRange(new[]{at,at+2,at+1,at,at+3,at+2});
                        report.keptStrokeQuads++;
                    }
                }
                symbol++;
            }
            if(report.arches==0||report.keptStrokeQuads==0)throw new InvalidOperationException("No valid arch carving strokes could be projected.");
            string outputPath=Folder+"/"+MeshName+".asset";var output=AssetDatabase.LoadAssetAtPath<Mesh>(outputPath);
            if(output==null){output=new Mesh{name=MeshName};AssetDatabase.CreateAsset(output,outputPath);}
            output.Clear();output.indexFormat=outputVertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16;
            output.SetVertices(outputVertices);output.SetColors(outputColors);output.SetTriangles(outputIndices,0);output.uv=new Vector2[outputVertices.Count];
            output.RecalculateNormals();output.RecalculateBounds();EditorUtility.SetDirty(output);filter.sharedMesh=output;EditorUtility.SetDirty(filter);
            report.outputMesh=outputPath;report.outputTriangles=outputIndices.Count/3;
            report.physicsPreserved=physics==Physics(map);report.navigationPreserved=navigation==Navigation(map);
            report.originalMeshPreserved=sourceHash==AssetDatabase.GetAssetDependencyHash(sourcePath).ToString()&&gatewayHashes.All(pair=>pair.Value==AssetDatabase.GetAssetDependencyHash(pair.Key).ToString());
            report.passed=report.physicsPreserved&&report.navigationPreserved&&report.originalMeshPreserved&&report.largestOutputEdge<.65f;
            if(!report.passed)throw new InvalidOperationException("Arch carving preservation check failed.");
            AssetDatabase.SaveAssets();Directory.CreateDirectory("Temp/ArchiveRoadside");File.WriteAllText("Temp/ArchiveRoadside/ArchCarvings.json",JsonUtility.ToJson(report,true));
            return report;
        }
        static bool TryFrontSurface(Vector3[] vertices,int[] triangles,Vector3 p,out Hit hit)
        {
            hit=default;float nearest=float.NegativeInfinity;
            for(int i=0;i<triangles.Length;i+=3)
            {
                var a=vertices[triangles[i]];var b=vertices[triangles[i+1]];var c=vertices[triangles[i+2]];
                var normal=Vector3.Cross(b-a,c-a).normalized;if(normal.z<.2f)continue;
                float d=(b.y-c.y)*(a.x-c.x)+(c.x-b.x)*(a.y-c.y);if(Mathf.Abs(d)<.000001f)continue;
                float u=((b.y-c.y)*(p.x-c.x)+(c.x-b.x)*(p.y-c.y))/d;
                float v=((c.y-a.y)*(p.x-c.x)+(a.x-c.x)*(p.y-c.y))/d;
                if(u<-.00001f||v<-.00001f||u+v>1.00001f)continue;
                float z=a.z*u+b.z*v+c.z*(1-u-v);if(z<=nearest)continue;
                nearest=z;hit=new Hit{position=new Vector3(p.x,p.y,z),normal=normal};
            }
            return !float.IsNegativeInfinity(nearest);
        }
    }
}

