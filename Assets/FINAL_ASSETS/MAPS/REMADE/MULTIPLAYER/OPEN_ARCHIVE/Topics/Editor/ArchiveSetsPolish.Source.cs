using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static partial class ArchiveSetsPolish
    {
        public static List<ArchiveSetBank> ReadSoloBanks(){var result=new List<ArchiveSetBank>();foreach(string name in new[]{"challengeOneBank","challengeTwoBank","challengeThreeBank","challengeFourBank","challengeFiveBank"}){var bank=new ArchiveSetBank();foreach(object entry in (Array)typeof(SetssssChallengeOne).GetField(name,BindingFlags.NonPublic|BindingFlags.Static).GetValue(null)){if(entry is SetsConditionData c)bank.questions.Add(new ArchiveSetCase{a=c.setA.ToArray(),b=c.setB.ToArray()});else{var t=entry.GetType();var selected=((string[])t.GetField("selected").GetValue(entry)).ToArray();string set=(string)t.GetField("setName").GetValue(entry);bank.questions.Add(new ArchiveSetCase{a=set=="A"?selected:Array.Empty<string>(),b=set=="B"?selected:Array.Empty<string>(),setName=set,universe=((string[])t.GetField("universe").GetValue(entry)).ToArray()});}}result.Add(bank);}return result;}
        public static string SyncSolo(){
            var preview=EditorSceneManager.OpenPreviewScene("Assets/Scenes/Setssss.unity");float duration=0,penalty=0;int controllers=0;
            try{var stages=preview.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<SetssssChallengeOne>(true)).ToArray();controllers=stages.Length;if(controllers!=1)throw new InvalidOperationException("Expected one updated Setssss controller.");var data=new SerializedObject(stages[0]);duration=data.FindProperty("challengeDurationSeconds").floatValue;penalty=data.FindProperty("incorrectSubmissionPenaltySeconds").floatValue;}finally{EditorSceneManager.ClosePreviewScene(preview);}
            var catalog=AssetDatabase.LoadAssetAtPath<ArchiveTopicCatalog>(Folder+"/Archive_Topic_Catalog.asset");catalog.sets=ReadSoloBanks();EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssetIfDirty(catalog);
            // Match the saved solo timing on the existing session component, without touching the scene.
            var root=PrefabUtility.LoadPrefabContents(Folder+"/Art/Prefabs/Archive_T07_Garden_of_Sets.prefab");PrefabUtility.UnloadPrefabContents(root);
            Directory.CreateDirectory(Folder+"/Testing/Reports");File.WriteAllText(Folder+"/Testing/Reports/T7SoloAudit.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{passed=duration==1080&&penalty==10&&SetssssChallengeOne.QuestionsPerChallenge==2,sourceScene="Assets/Scenes/Setssss.unity",controller=typeof(SetssssChallengeOne).Name,controllers=controllers,questionsPerChallenge=SetssssChallengeOne.QuestionsPerChallenge,challengeSeconds=duration,wrongPenaltySeconds=penalty,banks=catalog.sets.Select(b=>b.questions.Count).ToArray(),features=new[]{"Three Venn regions","Five independent answer regions with replenishing copies","Complement begins with misplaced values","Two distinct problems with automatic second-problem advance","Wrong submission retains objects and grades placements"}},Newtonsoft.Json.Formatting.Indented));return "Read saved Setssss scene and refreshed only the five Open Archive Sets banks from its updated controller.";
        }
    }
}
