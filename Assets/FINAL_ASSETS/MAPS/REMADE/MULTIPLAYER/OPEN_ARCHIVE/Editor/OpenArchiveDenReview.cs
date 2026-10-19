using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenArchiveAuthoring
{
    public static class OpenArchiveDenReview
    {
        [Serializable] public class Report
        {
            public bool passed, islandAndNetworkGeometryUnchanged, playerSpawnUnchanged, structureAnchorsUnchanged, originalObjectsRetained, originalAssetGuidsRetained;
            public bool sleepingGroundLevel, sleepingVolumeClear, monsterSpawnOnSleepingFloor, sourceHasNoGameplayScripts;
            public string scene, generatedUtc;
            public int originalObjects, retainedObjects, protectedMeshes, unchangedMeshes, originalAssets, retainedAssets, sleepingGroundSamples, sleepingObstacleTriangles;
            public float sleepingDiameter, sleepingFloorY, hollowDepth, sleepingMinimumY, sleepingMaximumY;
            public List<string> failedGeometry = new List<string>();
            public List<string> limitations = new List<string>();
        }

        [MenuItem("Tools/Logic Legends/Open Archive/Verify Den Redesign And Preserved Layout")]
        public static Report Validate()
        {
            OpenArchiveImprovements.RequireSavedEditor();
            var scene=SceneManager.GetActiveScene();
            if(scene.path!=OpenArchiveBuilder.ScenePath)throw new InvalidOperationException("Open the saved source environment first.");
            var root=scene.GetRootGameObjects().Single(g=>g.name=="OpenArchive").transform;
            var baseline=JsonUtility.FromJson<OpenArchiveDenRedesign.Baseline>(File.ReadAllText(OpenArchiveBuilder.Folder+"/Editor/Authoring/DenRedesignBaseline.json"));
            var ids=new HashSet<string>(root.GetComponentsInChildren<Transform>(true).Select(t=>GlobalObjectId.GetGlobalObjectIdSlow(t).ToString()));
            var report=new Report{scene=scene.path,generatedUtc=DateTime.UtcNow.ToString("o"),originalObjects=baseline.objectIDs.Length,retainedObjects=baseline.objectIDs.Count(ids.Contains),
                sleepingDiameter=17,sleepingFloorY=.25f,hollowDepth=.55f,sleepingMinimumY=float.PositiveInfinity,sleepingMaximumY=float.NegativeInfinity};
            report.originalObjectsRetained=report.originalObjects==report.retainedObjects;
            report.originalAssets=baseline.assetGUIDs.Length;
            report.retainedAssets=baseline.assetGUIDs.Count(s=>{var p=s.Split('|');return AssetDatabase.AssetPathToGUID(p[1])==p[0];});
            report.originalAssetGuidsRetained=report.originalAssets==report.retainedAssets;
            for(int i=0;i<baseline.protectedMeshPaths.Length;i++)
            {
                var t=root.Find(baseline.protectedMeshPaths[i]);report.protectedMeshes++;
                if(t!=null&&t.GetComponent<MeshFilter>()!=null&&OpenArchiveDenRedesign.GeometryHash(t.GetComponent<MeshFilter>().sharedMesh)==baseline.protectedMeshHashes[i])report.unchangedMeshes++;
                else report.failedGeometry.Add(baseline.protectedMeshPaths[i]);
            }
            report.islandAndNetworkGeometryUnchanged=report.protectedMeshes==report.unchangedMeshes;
            report.playerSpawnUnchanged=baseline.spawnTransforms.SequenceEqual(root.Find("PlayerSpawnArea").GetComponentsInChildren<Transform>(true).Select(OpenArchiveDenRedesign.TransformStamp));
            report.structureAnchorsUnchanged=baseline.slotTransforms.SequenceEqual(root.Find("FutureStructureSlots").GetComponentsInChildren<Transform>(true).Select(OpenArchiveDenRedesign.TransformStamp));
            Physics.SyncTransforms();var floor=root.Find("UniversalMonsterNest/Nest_StoneFloor_30m").GetComponent<MeshCollider>();
            int expected=0;
            for(float x=-8.5f;x<=8.5f;x+=.5f)for(float z=-8.5f;z<=8.5f;z+=.5f)
            {
                if(x*x+z*z>8.5f*8.5f+.001f)continue;expected++;
                if(!floor.Raycast(new Ray(new Vector3(x,5,z),Vector3.down),out var hit,6))continue;
                report.sleepingGroundSamples++;report.sleepingMinimumY=Mathf.Min(report.sleepingMinimumY,hit.point.y);report.sleepingMaximumY=Mathf.Max(report.sleepingMaximumY,hit.point.y);
            }
            report.sleepingGroundLevel=report.sleepingGroundSamples==expected&&Mathf.Abs(report.sleepingMinimumY-.25f)<.002f&&Mathf.Abs(report.sleepingMaximumY-.25f)<.002f;
            foreach(var f in root.Find("UniversalMonsterNest").GetComponentsInChildren<MeshFilter>())
            {
                if(f==floor.GetComponent<MeshFilter>())continue;
                var mesh=f.sharedMesh;var v=mesh.vertices;var t=mesh.triangles;var matrix=f.transform.localToWorldMatrix;
                for(int i=0;i<t.Length;i+=3)
                {
                    Vector3 a=matrix.MultiplyPoint3x4(v[t[i]]),b=matrix.MultiplyPoint3x4(v[t[i+1]]),c=matrix.MultiplyPoint3x4(v[t[i+2]]);
                    if(Mathf.Max(a.y,b.y,c.y)<=.30f||Mathf.Min(a.y,b.y,c.y)>8.5f)continue;
                    if(TriangleDistance(new Vector2(a.x,a.z),new Vector2(b.x,b.z),new Vector2(c.x,c.z))<8.5f-.02f)report.sleepingObstacleTriangles++;
                }
            }
            var colliders=new HashSet<Collider>(root.GetComponentsInChildren<Collider>().Where(c=>c.enabled&&!c.isTrigger));
            bool physicsClear=true;
            foreach(float x in new[]{-4.1f,0,4.1f})foreach(float z in new[]{-4.1f,0,4.1f})
            {
                Vector3 lower=new Vector3(x,.25f+.06f+2.55f,z),upper=new Vector3(x,.25f+.06f+5.5f-2.55f,z);
                if(Physics.OverlapCapsule(lower,upper,2.55f).Any(colliders.Contains))physicsClear=false;
            }
            report.sleepingVolumeClear=report.sleepingObstacleTriangles==0&&physicsClear;
            var spawn=root.Find("UniversalMonsterNest/MonsterSpawnPoint");
            report.monsterSpawnOnSleepingFloor=Vector3.Distance(spawn.position,new Vector3(0,.25f,0))<.001f;
            report.sourceHasNoGameplayScripts=root.GetComponentsInChildren<MonoBehaviour>(true).Length==0;
            report.passed=report.islandAndNetworkGeometryUnchanged&&report.playerSpawnUnchanged&&report.structureAnchorsUnchanged&&report.originalObjectsRetained&&report.originalAssetGuidsRetained
                &&report.sleepingGroundLevel&&report.sleepingVolumeClear&&report.monsterSpawnOnSleepingFloor&&report.sourceHasNoGameplayScripts;
            report.limitations.Add("Visual assessment is supported by actual aerial, nest detail, and player-eye renders; visual polish remains an artistic judgment.");
            report.limitations.Add("Sleeping volume checks cover a 17m diameter and up to 8m above ground, plus nine 5.5m tall large-body capsule probes. Future animated monster silhouettes still require integration checks.");
            report.limitations.Add("Android hardware frame time, memory, thermal behaviour, physical multitouch, and multiplayer were not tested.");
            string path=OpenArchiveBuilder.Folder+"/Previews/DenRedesignValidation.json";File.WriteAllText(path,JsonUtility.ToJson(report,true));AssetDatabase.ImportAsset(path);return report;
        }

        static float TriangleDistance(Vector2 a,Vector2 b,Vector2 c)
        {
            float s0=Cross(b-a,-a),s1=Cross(c-b,-b),s2=Cross(a-c,-c);
            if(Mathf.Abs(Cross(b-a,c-a))>.000001f&&((s0>=0&&s1>=0&&s2>=0)||(s0<=0&&s1<=0&&s2<=0)))return 0;
            return Mathf.Min(SegmentDistance(a,b),SegmentDistance(b,c),SegmentDistance(c,a));
        }
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        static float SegmentDistance(Vector2 a,Vector2 b)
        {
            Vector2 d=b-a;if(d.sqrMagnitude<.000001f)return a.magnitude;
            return (a+d*Mathf.Clamp01(Vector2.Dot(-a,d)/d.sqrMagnitude)).magnitude;
        }
    }
}
