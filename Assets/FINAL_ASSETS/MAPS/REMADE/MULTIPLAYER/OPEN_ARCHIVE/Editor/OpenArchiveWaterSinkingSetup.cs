using System;
using System.IO;
using System.Linq;
using LogicLegends.OpenArchive.Water;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenArchiveAuthoring
{
    public static class OpenArchiveWaterSinkingSetup
    {
        public static void Configure(OpenArchiveWater water, Transform root)
        {
            var sinking = water.GetComponent<OpenArchiveWaterSinking>();
            if (sinking == null) sinking = water.gameObject.AddComponent<OpenArchiveWaterSinking>();
            sinking.returnSpawn = root.Find("PlayerSpawnArea/PlayerSpawn_01");
            if (sinking.returnSpawn == null) throw new InvalidOperationException("Missing original player spawn.");
            EditorUtility.SetDirty(sinking);
        }
        [MenuItem("Tools/Logic Legends/Open Archive/Install Timed Water Sinking")]
        public static void Apply()
        {
            OpenArchiveImprovements.RequireSavedEditor();
            var folder = OpenArchiveBuilder.Folder;
            var active = SceneManager.GetActiveScene().path;
            var paths = new[] {OpenArchiveBuilder.ScenePath, folder+"/Scenes/OpenArchive_Explore.unity",
                folder+"/Testing/KhezuAwakening/OpenArchive_KhezuAwakening_Test.unity",
                folder+"/Testing/KhezuHunting/OpenArchive_KhezuHunting_Test.unity",
                folder+"/Testing/KhezuCinematic/OpenArchive_KhezuCinematic_Test.unity"};
            try {
                foreach(var path in paths) {
                    var scene = EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
                    var root = scene.GetRootGameObjects().Single(g=>g.name=="OpenArchive").transform;
                    Configure(root.GetComponentInChildren<OpenArchiveWater>(),root);
                    EditorSceneManager.MarkSceneDirty(scene);
                    if(!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save "+path);
                }
                AssetDatabase.SaveAssets();
            }
            finally {if(SceneManager.GetActiveScene().path!=active) EditorSceneManager.OpenScene(active,OpenSceneMode.Single);}
        }
    }
}
