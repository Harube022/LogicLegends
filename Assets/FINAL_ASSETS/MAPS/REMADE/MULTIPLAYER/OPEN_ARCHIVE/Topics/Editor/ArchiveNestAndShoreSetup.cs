using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static class ArchiveNestAndShoreSetup
    {
        [Serializable] public sealed class Report
        {
            public bool passed,physicsPreserved,navigationPreserved,monsterSetupPreserved;
            public ArchiveApproachFoundations.Report foundations;
            public ArchiveNestSanctuaryBuilder.Report sanctuary;
            public ArchiveShoreRockGrounding.Report shoreline;
        }
        static Component[] Components(Scene scene)=>scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Component>(true)).Where(c=>c!=null).ToArray();
        static string Physics(Scene scene)=>string.Join("\n",Components(scene).OfType<Collider>().OrderBy(c=>c.GetInstanceID())
            .Select(c=>c.GetInstanceID()+":"+EditorJsonUtility.ToJson(c)+":"+c.transform.localToWorldMatrix));
        static string Navigation(Scene scene)=>string.Join("\n",Components(scene).Where(c=>c is UnityEngine.AI.NavMeshAgent||c is UnityEngine.AI.NavMeshObstacle||c is Hunting.KhezuNavigationData)
            .OrderBy(c=>c.GetInstanceID()).Select(c=>c.GetInstanceID()+":"+EditorJsonUtility.ToJson(c)));
        static string Monsters(Scene scene)=>string.Join("\n",Components(scene).Where(c=>c is Hunting.KhezuHuntingController||c is Transform t&&t.name=="MonsterSpawnPoint")
            .OrderBy(c=>c.GetInstanceID()).Select(c=>c.GetInstanceID()+":"+EditorJsonUtility.ToJson(c)+":"+c.transform.localToWorldMatrix));
        public static Report Build(ArchiveTopicSession session,Transform parent,Material stone,Material trim,Material glow)
        {
            var scene=SceneManager.GetActiveScene();
            if(Application.isPlaying||scene.path!="Assets/Scenes/MultiplayerGameplay.unity")throw new InvalidOperationException("Use stopped production MultiplayerGameplay.");
            string physics=Physics(scene),navigation=Navigation(scene),monsters=Monsters(scene);
            var map=GameObject.Find("OpenArchive").transform;
            var result=new Report();
            result.foundations=ArchiveApproachFoundations.Build(map,parent,stone);
            ArchiveNestSanctuaryBuilder.Build(session,parent,stone,trim,glow);result.sanctuary=ArchiveNestSanctuaryBuilder.LastReport;
            result.shoreline=ArchiveShoreRockGrounding.Apply(map);
            result.physicsPreserved=physics==Physics(scene);result.navigationPreserved=navigation==Navigation(scene);result.monsterSetupPreserved=monsters==Monsters(scene);
            result.passed=result.foundations.passed&&result.sanctuary.passed&&result.shoreline.passed&&result.physicsPreserved&&result.navigationPreserved&&result.monsterSetupPreserved;
            Directory.CreateDirectory("Temp/ArchiveNestFinish");File.WriteAllText("Temp/ArchiveNestFinish/Install.json",JsonUtility.ToJson(result,true));
            if(!result.passed)throw new InvalidOperationException("Nest finish changed existing physics, navigation or encounter setup.");
            return result;
        }
        [MenuItem("Tools/Logic Legends/Open Archive/Environment/Improve Nest And Ground Shore Rocks")]
        public static void Install()=>Apply();
        public static Report Apply()
        {
            var scene=SceneManager.GetActiveScene();
            if(Application.isPlaying||scene.isDirty||scene.path!="Assets/Scenes/MultiplayerGameplay.unity")throw new InvalidOperationException("Use the saved production MultiplayerGameplay scene outside Play Mode.");
            var session=UnityEngine.Object.FindFirstObjectByType<ArchiveTopicSession>();var parent=GameObject.Find(ArchiveEnvironmentPolishSetup.RootName).transform;
            string folder=ArchiveTopicSetup.Folder+"/Art/";
            var stone=AssetDatabase.LoadAssetAtPath<Material>(folder+"Materials/Archive_Topic_Stone.mat");
            var trim=AssetDatabase.LoadAssetAtPath<Material>(folder+"Materials/Archive_Topic_BrassInlay.mat");
            var glow=AssetDatabase.LoadAssetAtPath<Material>(ArchiveEnvironmentPolishSetup.ArtFolder+"/Archive_Environment_Rune.mat");
            var result=Build(session,parent,stone,trim,glow);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();return result;
        }
    }
}
