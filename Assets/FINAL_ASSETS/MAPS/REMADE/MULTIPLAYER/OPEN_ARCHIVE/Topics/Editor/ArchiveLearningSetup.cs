using System;
using System.Linq;
using LogicLegends.OpenArchive.Cinematics;
using LogicLegends.Tests.Monsters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static class ArchiveLearningSetup
    {
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Install Learning Support In Current Scene")]
        public static void Install()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Install in Edit Mode.");
            var session = UnityEngine.Object.FindFirstObjectByType<ArchiveTopicSession>();
            var topics = UnityEngine.Object.FindFirstObjectByType<ArchiveTopicUI>();
            if (session == null || session.hunting == null || topics == null) throw new InvalidOperationException("Open the existing topic gameplay scene first.");
            var encounter = session.hunting.GetComponent<KhezuCaptureEncounterController>();
            if (encounter == null) throw new InvalidOperationException("The existing encounter is required.");
            var all = UnityEngine.Object.FindObjectsByType<ArchiveLearningUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (all.Length > 1) throw new InvalidOperationException("Resolve duplicate learning interfaces before installing.");
            var ui = all.FirstOrDefault();
            if (ui == null)
            {
                var root = new GameObject("Archive_Learning_Interface", typeof(RectTransform));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, session.gameObject.scene);
                ui = root.AddComponent<ArchiveLearningUI>();
            }
            ui.session = session; ui.topicUI = topics;
            ui.readingFont = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(ArchiveTopicSetup.Folder + "/Art/Archive_T7_Reading.asset");
            if (ui.readingFont == null) throw new InvalidOperationException("The existing mathematical reading font is required.");
            session.enableBarrierMath = true; session.learningEncounter = encounter;
            encounter.developmentTestOutcomes = false;
            var selector = UnityEngine.Object.FindFirstObjectByType<DevelopmentMonsterSelection>();
            if (selector != null) encounter.inferenceBarrier = selector.Selected == DevelopmentMonster.Rajang;
            EditorUtility.SetDirty(ui); EditorUtility.SetDirty(session); EditorUtility.SetDirty(encounter);
            EditorSceneManager.MarkSceneDirty(session.gameObject.scene);
            EditorSceneManager.SaveScene(session.gameObject.scene);
            Debug.Log("Learning support saved: original encounter, original question banks, one topic owner and one local learning UI.");
        }
    }
}
