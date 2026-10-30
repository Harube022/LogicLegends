using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LogicLegends.OpenArchive.Hunting;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static class ArchiveTopicSceneChecks
    {
        [Serializable] sealed class Result
        {
            public bool passed;public int stationCount,prefabCount,solidKitColliders,meshTriangles,navigationRoutes;
            public List<string> errors=new List<string>();public List<string> checks=new List<string>();
        }
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Check Kit Assets And Monster Access (Play Mode)")]
        public static void Validate()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Use Play Mode so the existing monster navigation data is loaded.");
            var session=ArchiveTopicSession.Current;if(session==null||!session.Ready)throw new InvalidOperationException("No active topic session.");
            var result=new Result{stationCount=session.stations.Length};
            if(result.stationCount!=10)result.errors.Add("Expected ten topic stations.");
            var loader=UnityEngine.Object.FindObjectsByType<KhezuNavigationData>(FindObjectsSortMode.None);
            var selection=UnityEngine.Object.FindFirstObjectByType<LogicLegends.Tests.Monsters.DevelopmentMonsterSelection>();
            var config=new SerializedObject(selection);int[] agents=new[]{"khezuProfile","rajangProfile"}.Select(name=>config.FindProperty(name).FindPropertyRelative("agentTypeId").intValue).Distinct().ToArray();
            if(agents.Length!=2||loader.Count(l=>l.data!=null)!=2)result.errors.Add("Expected both existing monster navigation profiles.");
            foreach(var station in session.stations)
            {
                if(!station.transform.parent.name.StartsWith("StructureSlot_"))result.errors.Add("Station is outside its reserved anchor: "+station.name);
                var solid=station.GetComponentsInChildren<Collider>(true).Where(c=>c.gameObject.activeInHierarchy&&c.enabled&&!c.isTrigger).ToArray();result.solidKitColliders+=solid.Length;
                var lecterns=solid.Where(c=>c.GetComponent<NavMeshObstacle>()!=null).ToArray();
                var crystalBases=solid.Where(c=>c.name=="Pillar_Dais"&&c.GetComponentInParent<ArchiveCrystalPillar>()!=null).ToArray();
                if(lecterns.Length!=1||crystalBases.Length!=(session.CrystalsEnabled?1:0)||solid.Except(lecterns).Except(crystalBases).Any())
                    result.errors.Add("Unexpected active collision in T"+(station.topic+1));
                foreach(int agent in agents)
                {
                    var filter=new NavMeshQueryFilter{agentTypeID=agent,areaMask=NavMesh.AllAreas};NavMeshHit origin;
                    if(!NavMesh.SamplePosition(session.hunting.transform.position,out origin,25,filter)){result.errors.Add("No nest navigation for "+agent);continue;}
                    foreach(Vector3 local in new[]{Vector3.zero,new Vector3(-5,0,-3),new Vector3(5,0,-3),new Vector3(0,0,6)})
                    {
                        Vector3 world=station.transform.TransformPoint(local);NavMeshHit destination;var path=new NavMeshPath();
                        bool complete=NavMesh.SamplePosition(world,out destination,7,filter)&&NavMesh.CalculatePath(origin.position,destination.position,filter,path)&&path.status==NavMeshPathStatus.PathComplete;
                        if(complete)result.navigationRoutes++;else result.errors.Add("Incomplete monster route: T"+(station.topic+1)+", agent "+agent+", point "+local);
                    }
                }
            }
            foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{ArchiveTopicSetup.Folder+"/Art/Prefabs"}))
            {
                result.prefabCount++;var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab)>0)result.errors.Add("Missing script: "+prefab.name);
                foreach(var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                    foreach(var material in renderer.sharedMaterials)
                        if(material==null||material.shader==null||!material.shader.isSupported)result.errors.Add("Unsupported/missing material in "+prefab.name);
            }
            foreach(string guid in AssetDatabase.FindAssets("t:Mesh",new[]{ArchiveTopicSetup.Folder+"/Art/Meshes"}))
            {
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(AssetDatabase.GUIDToAssetPath(guid));result.meshTriangles+=mesh.triangles.Length/3;
                if(mesh.vertexCount==0||mesh.normals.Length!=mesh.vertexCount||mesh.uv.Length!=mesh.vertexCount)result.errors.Add("Invalid mesh data: "+mesh.name);
            }
            result.checks.Add("Ten reserved anchors; active collision is limited to each lectern and its crystal dais. Inactive legacy relay props add no collision fences.");
            result.checks.Add("Existing Khezu and Rajang navigation data; four routes per topic and monster profile.");
            result.checks.Add("Reusable prefab scripts, supported materials, mesh normals and UVs.");
            result.passed=result.errors.Count==0;
            File.WriteAllText(ArchiveTopicSetup.Folder+"/Testing/Reports/SceneAndNavigation.json",JsonUtility.ToJson(result,true));
            Debug.Log("Archive topic assets/navigation: "+(result.passed?"Passed":"Failed")+", "+result.navigationRoutes+" routes.");
        }
    }
}
