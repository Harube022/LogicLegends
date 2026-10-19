using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenArchiveAuthoring
{
    /// <summary>Static collision around the outer drop. Does not rebuild the map or its navigation.</summary>
    public static class OpenArchiveEdgeSafety
    {
        const string Map=OpenArchiveBuilder.Folder;
        const string PrefabPath=Map+"/Prefabs/Terrain/OpenArchive_OuterEdgeSafety.prefab";
        const int Segments=96;
        const float Radius=98.5f,Thickness=1,Bottom=-12,Top=32;
        static readonly string[] Scenes={Map+"/Scenes/MultiplayerGameplay.unity",Map+"/Scenes/OpenArchive_Explore.unity",Map+"/Testing/KhezuAwakening/OpenArchive_KhezuAwakening_Test.unity",Map+"/Testing/KhezuHunting/OpenArchive_KhezuHunting_Test.unity",Map+"/Testing/KhezuCinematic/OpenArchive_KhezuCinematic_Test.unity"};
        [Serializable] sealed class SceneCheck {
            public string scene;public bool passed,transformsPreserved,originalCollidersPreserved,gameplayPreserved,interiorWaterClear,spawnClear,reservedSpacesClear,cameraIgnoresBoundary,playerCollides;
            public int boundaryColliders,coverageChecks,coverageMisses;public float innerRadiusWorld,topWorld;
        }
        [Serializable] sealed class Report {public string checkedUtc;public bool passed;public List<SceneCheck> scenes=new List<SceneCheck>();}
        static Vector3 Polar(float angle,float radius){angle*=Mathf.Deg2Rad;return new Vector3(Mathf.Sin(angle)*radius,0,Mathf.Cos(angle)*radius);}
        static string Pose(Transform t){return t.localPosition.ToString("R")+t.localRotation.ToString("R")+t.localScale.ToString("R")+t.gameObject.activeSelf;}

        static GameObject PreparePrefab(){
            var previous=SceneManager.GetActiveScene();var staging=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            SceneManager.SetActiveScene(staging);
            try{
                var root=new GameObject("OuterEdgeSafety");root.layer=2;
                float length=2*Radius*Mathf.Tan(Mathf.PI/Segments)+.2f;
                for(int i=0;i<Segments;i++){
                    var go=new GameObject("Edge_Blocker_"+i.ToString("000"));go.layer=2;go.transform.SetParent(root.transform,false);
                    float angle=i*360f/Segments;go.transform.localPosition=Polar(angle,Radius);go.transform.localRotation=Quaternion.Euler(0,angle,0);
                    var box=go.AddComponent<BoxCollider>();box.isTrigger=false;box.center=Vector3.up*((Top+Bottom)*.5f);box.size=new Vector3(length,Top-Bottom,Thickness);
                }
                return PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
            }
            finally{SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(staging,true);}
        }

        [MenuItem("Tools/Logic Legends/Open Archive/Safety/Apply outer cliff collision boundary")]
        public static string Apply(){
            OpenArchiveImprovements.RequireSavedEditor();string original=SceneManager.GetActiveScene().path;
            Directory.CreateDirectory(Map+"/Testing/EdgeSafety/Reports");var prefab=PreparePrefab();var report=new Report{checkedUtc=DateTime.UtcNow.ToString("o")};
            try{
                foreach(string path in Scenes){
                    var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);var root=scene.GetRootGameObjects().Single(g=>g.name=="OpenArchive").transform;
                    var poses=root.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t,t=>Pose(t));
                    var colliders=root.GetComponentsInChildren<Collider>(true).ToDictionary(c=>c,c=>EditorJsonUtility.ToJson(c));
                    var systems=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MonoBehaviour>(true)).Where(c=>c!=null).ToDictionary(c=>c,c=>EditorJsonUtility.ToJson(c));
                    var agents=root.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true).ToDictionary(c=>c,c=>EditorJsonUtility.ToJson(c));
                    var cliffs=root.Find("Environment/Cliffs");var boundary=cliffs.Find("OuterEdgeSafety");
                    if(boundary==null){var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);instance.name="OuterEdgeSafety";instance.transform.SetParent(cliffs,false);boundary=instance.transform;}
                    Physics.SyncTransforms();float scale=Mathf.Abs(root.lossyScale.x);
                    var check=new SceneCheck{scene=path,transformsPreserved=poses.All(p=>p.Key!=null&&Pose(p.Key)==p.Value),
                        originalCollidersPreserved=colliders.All(p=>p.Key!=null&&EditorJsonUtility.ToJson(p.Key)==p.Value),
                        gameplayPreserved=systems.All(p=>p.Key!=null&&EditorJsonUtility.ToJson(p.Key)==p.Value)&&agents.All(p=>p.Key!=null&&EditorJsonUtility.ToJson(p.Key)==p.Value),
                        boundaryColliders=boundary.GetComponentsInChildren<Collider>().Length,innerRadiusWorld=(Radius-Thickness*.5f)*scale,topWorld=root.TransformPoint(Vector3.up*Top).y,
                        playerCollides=!Physics.GetIgnoreLayerCollision(7,2)};
                    foreach(float y in new[]{.3f,24f})for(int i=0;i<1440;i++){
                        var direction=root.TransformDirection(Polar(i*.25f,1));var origin=root.TransformPoint(Polar(i*.25f,94)+Vector3.up*y);RaycastHit hit;
                        bool blocked=Physics.CapsuleCast(origin+Vector3.up*.7f,origin+Vector3.up*4.8f,.7f,direction,out hit,12*scale,1<<2,QueryTriggerInteraction.Ignore)&&hit.transform.IsChildOf(boundary);
                        check.coverageChecks++;if(!blocked)check.coverageMisses++;
                    }
                    check.interiorWaterClear=root.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name.StartsWith("Turquoise_RiverPond_")).All(f=>f.sharedMesh.vertices.All(v=>{var p=root.InverseTransformPoint(f.transform.TransformPoint(v));return new Vector2(p.x,p.z).magnitude<Radius-Thickness;}));
                    var plaza=root.Find("PlayerSpawnArea/Spawn_StonePlaza_18m").GetComponent<MeshFilter>();
                    check.spawnClear=plaza.sharedMesh.vertices.All(v=>{var p=root.InverseTransformPoint(plaza.transform.TransformPoint(v));return new Vector2(p.x,p.z).magnitude<Radius-Thickness;});
                    check.reservedSpacesClear=root.Find("FutureStructureSlots").GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("StructureSlot_")).All(t=>{var p=root.InverseTransformPoint(t.position);return new Vector2(p.x,p.z).magnitude+10<Radius-Thickness;});
                    check.cameraIgnoresBoundary=systems.Keys.Where(c=>c.GetType().Name=="CinemachineDeoccluder").All(c=>(((LayerMask)c.GetType().GetField("CollideAgainst").GetValue(c)).value&(1<<2))==0);
                    check.passed=check.transformsPreserved&&check.originalCollidersPreserved&&check.gameplayPreserved&&check.boundaryColliders==Segments&&check.coverageMisses==0&&check.interiorWaterClear&&check.spawnClear&&check.reservedSpacesClear&&check.cameraIgnoresBoundary&&check.playerCollides;
                    if(!check.passed)throw new InvalidOperationException("Edge boundary validation failed: "+JsonUtility.ToJson(check));
                    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);report.scenes.Add(check);
                }
                AssetDatabase.SaveAssets();report.passed=report.scenes.Count==Scenes.Length&&report.scenes.All(c=>c.passed);
                string json=JsonUtility.ToJson(report,true);File.WriteAllText(Map+"/Testing/EdgeSafety/Reports/SceneEdgeSafety.json",json);return json;
            }
            finally{EditorSceneManager.OpenScene(original,OpenSceneMode.Single);}
        }
    }
}
