using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using LogicLegends.OpenArchive.Hunting;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static class ArchiveMonsterSignalsSetup
    {
        public const string Folder="Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Topics/Art/MonsterSignals";
        public static Material Bubble=>AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Archive_SleepBubble.mat");
        public static Material Alert=>AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Archive_AwakeningAlert.mat");
        [MenuItem("Tools/Logic Legends/Open Archive/Monsters/Install Sleep And Awakening Signals")]
        public static void Install()
        {
            var scene=SceneManager.GetActiveScene();
            if(Application.isPlaying||scene.isDirty||scene.path!="Assets/Scenes/MultiplayerGameplay.unity")throw new InvalidOperationException("Use saved MultiplayerGameplay in Edit Mode.");
            Directory.CreateDirectory(Folder);
            var shader=Shader.Find("LogicLegends/OpenArchive/MonsterSignal");if(shader==null)throw new InvalidOperationException("Import the URP monster signal shader first.");
            var bubble=Material("Archive_SleepBubble",shader,0);var alert=Material("Archive_AwakeningAlert",shader,1);
            foreach(var monster in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<KhezuHuntingController>(true)))
            {
                var presentation=monster.GetComponent<ArchiveMonsterStateSignals>();
                if(presentation==null)presentation=Undo.AddComponent<ArchiveMonsterStateSignals>(monster.gameObject);
                Undo.RecordObject(presentation,"Configure monster sleep/awakening signals");
                presentation.monster=monster;presentation.bubbleMaterial=bubble;presentation.alertMaterial=alert;
                EditorUtility.SetDirty(presentation);
            }
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
        static Material Material(string name,Shader shader,float kind)
        {
            string path=Folder+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}else material.shader=shader;
            material.SetFloat("_Kind",kind);material.SetFloat("_Opacity",1);EditorUtility.SetDirty(material);return material;
        }
    }
}
