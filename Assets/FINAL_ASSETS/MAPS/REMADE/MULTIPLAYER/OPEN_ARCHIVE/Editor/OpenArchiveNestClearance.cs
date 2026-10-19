using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LogicLegends.OpenArchive.Hunting;
using LogicLegends.Tests.Khezu.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LogicLegends.OpenArchive.Editor
{
    /// <summary>Moves existing nest decorations onto the stone rim; no meshes or colliders are removed.</summary>
    public static class OpenArchiveNestClearance
    {
        const string Folder="Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE";
        [Serializable] sealed class Entry {public string scene;public int moved;public bool otherTransformsPreserved;}
        [Serializable] sealed class Report {public string checkedUtc;public List<Entry> scenes=new List<Entry>();}
        static bool Decoration(Transform t)=>t.name=="Pillar_Broken" || t.name=="Torch_Pedestal";
        static int Adjust(Transform nest){
            int count=0;
            foreach(Transform t in nest){
                if(!Decoration(t))continue;
                var p=t.localPosition;float radius=t.name=="Pillar_Broken"?13.8f:14.2f;
                var direction=new Vector3(p.x,0,p.z).normalized;
                t.localPosition=direction*radius+Vector3.up*p.y;
                PrefabUtility.RecordPrefabInstancePropertyModifications(t);count++;
            }
            if(count!=8)throw new InvalidOperationException("Expected four existing pillars and four torch pedestals.");
            return count;
        }
        public static void ApplyAll(){
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
            var original=SceneManager.GetActiveScene();if(original.isDirty)throw new InvalidOperationException("Save open scene changes first.");
            string originalPath=original.path;
            var report=new Report{checkedUtc=DateTime.UtcNow.ToString("o")};
            string[] scenes={"Scenes/MultiplayerGameplay.unity","Scenes/OpenArchive_Explore.unity","Testing/KhezuAwakening/OpenArchive_KhezuAwakening_Test.unity","Testing/KhezuHunting/OpenArchive_KhezuHunting_Test.unity","Testing/KhezuCinematic/OpenArchive_KhezuCinematic_Test.unity"};
            foreach(var relative in scenes){
                var scene=EditorSceneManager.OpenScene(Folder+"/"+relative);var root=scene.GetRootGameObjects().Single(g=>g.name=="OpenArchive").transform;
                var nest=root.Find("UniversalMonsterNest");
                var before=root.GetComponentsInChildren<Transform>(true).Where(t=>t.parent!=nest || !Decoration(t)).ToDictionary(t=>t,t=>t.localToWorldMatrix);
                int count=Adjust(nest);
                bool preserved=before.All(pair=>pair.Key.localToWorldMatrix==pair.Value);
                if(!preserved)throw new InvalidOperationException("Unexpected transform change outside the eight decorations.");
                report.scenes.Add(new Entry{scene=relative,moved=count,otherTransformsPreserved=preserved});
                EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed.");
            }
            // Keep both reusable nest assemblies consistent with the saved scenes.
            foreach(var name in new[]{"UniversalMonsterNest","UniversalMonsterNest_Den"}){
                string path=Folder+"/Prefabs/Architecture/"+name+".prefab";var contents=PrefabUtility.LoadPrefabContents(path);
                try{Adjust(contents.transform);PrefabUtility.SaveAsPrefabAsset(contents,path);}finally{PrefabUtility.UnloadPrefabContents(contents);}
            }
            EditorSceneManager.OpenScene(originalPath);
            var environment=GameObject.Find("OpenArchive");var hunting=UnityEngine.Object.FindFirstObjectByType<KhezuHuntingController>();
            if(hunting==null)throw new InvalidOperationException("Run from the hunting or cinematic test scene to rebake its shared NavMesh.");
            UnityEngine.Object.FindFirstObjectByType<KhezuNavigationData>().data=KhezuHuntingSetup.Bake(environment,hunting.Agent);
            AssetDatabase.SaveAssets();
            File.WriteAllText(KhezuHuntingSetup.Folder+"/Reports/NestClearance.json",JsonUtility.ToJson(report,true));
        }
    }
}
