using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static class ArchiveFieldMechanicsSetup
    {
        [MenuItem("Tools/Logic Legends/Open Archive/Environment/Install Field Gameplay and Beacons")]
        public static ArchiveFieldEventsSetup.Report Install()
        {
            var scene=SceneManager.GetActiveScene();
            if(Application.isPlaying||scene.isDirty||scene.path!="Assets/Scenes/MultiplayerGameplay.unity")
                throw new InvalidOperationException("Open the clean saved MultiplayerGameplay scene outside Play Mode.");
            var session=UnityEngine.Object.FindFirstObjectByType<ArchiveTopicSession>();
            var layout=ArchiveFieldEventsSetup.Build(session,null);
            var owner=session.GetComponent<ArchiveFieldEvents>();
            if(owner==null)owner=Undo.AddComponent<ArchiveFieldEvents>(session.gameObject);
            Undo.RecordObject(session,"Install shared field events");Undo.RecordObject(owner,"Bind field scenery");
            session.fieldEvents=owner;owner.session=session;owner.beacons=layout.beacons;owner.hazards=layout.hazards;
            EditorUtility.SetDirty(session);EditorUtility.SetDirty(owner);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            return layout;
        }
    }
}
