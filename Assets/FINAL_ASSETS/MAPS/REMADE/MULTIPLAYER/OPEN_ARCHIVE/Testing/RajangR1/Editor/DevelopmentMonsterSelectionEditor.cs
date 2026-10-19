using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LogicLegends.Tests.Monsters.Editor
{
    [CustomEditor(typeof(DevelopmentMonsterSelection))]
    public sealed class DevelopmentMonsterSelectionEditor : UnityEditor.Editor
    {
        int preview;
        public override void OnInspectorGUI()
        {
            var settings = (DevelopmentMonsterSelection)target;
            EditorGUI.BeginChangeCheck();
            using (new EditorGUI.DisabledScope(Application.isPlaying && settings.HasSharedEncounter && !settings.CanSwap))
                DrawDefaultInspector();
            if (EditorGUI.EndChangeCheck()) ApplySelection(settings, settings.Selected);
            EditorGUILayout.HelpBox(settings.HasSharedEncounter ? "Shared encounter test. One controller owns hunting, capture, health outcomes and rotation while the active rig changes. Swap resets the hunt to Sleep in the nest and preserves health/history." : "R1 local animation test. Exactly one monster is active. Hunting, capture, cinematic and barrier controllers are absent from this scene. Animation previews remain stationary.", MessageType.Info);
            using (new EditorGUI.DisabledScope(settings.HasSharedEncounter && !settings.CanSwap))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Select Rajang")) ApplySelection(settings, DevelopmentMonster.Rajang);
                if (GUILayout.Button("Select Khezu")) ApplySelection(settings, DevelopmentMonster.Khezu);
            }
            var test = settings.ActiveTest;
            if (test == null) return;
            EditorGUILayout.LabelField("Selected monster", settings.Selected.ToString());
            EditorGUILayout.LabelField("Mistakes / stage", test.Mistakes + " / 4    |    " + test.PlayingStage);
            EditorGUILayout.LabelField("Animator state", test.CurrentState);
            using (new EditorGUI.DisabledScope(!Application.isPlaying || (settings.HasSharedEncounter && !settings.CanSwap)))
            {
                if (GUILayout.Button("Reset Rajang")) settings.ResetRajang();
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Play Sleep")) settings.PlaySleep();
                    if (GUILayout.Button("Play Idle")) settings.PlayIdle();
                }
                EditorGUILayout.LabelField("Shared awakening progression", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                    for (int stage = 1; stage <= 4; stage++)
                        if (GUILayout.Button("Mistake " + stage)) settings.TriggerMistake(stage);
                var clips = test.AvailableAnimations.OrderBy(n => n).ToArray();
                if (!settings.HasSharedEncounter && clips.Length > 0)
                {
                    preview = Mathf.Clamp(preview, 0, clips.Length - 1);
                    preview = EditorGUILayout.Popup("Existing animation", preview, clips);
                    if (GUILayout.Button("Preview " + clips[preview])) settings.Preview(clips[preview]);
                }
            }
            EditorGUILayout.HelpBox("Rajang has 13 existing clips. Run maps to Chase. Standalone Walk and Roar are unavailable; Full_Awaken includes a roar. No substitute clips were generated.", MessageType.None);
            if (settings.HasSharedEncounter && !settings.CanSwap) EditorGUILayout.HelpBox(settings.SwapBlockedReason, MessageType.Info);
            if (Application.isPlaying) Repaint();
        }
        static void ApplySelection(DevelopmentMonsterSelection settings, DevelopmentMonster monster)
        {
            if (settings.Khezu == null || settings.Rajang == null) return;
            Undo.RecordObjects(new Object[] { settings, settings.Khezu.gameObject, settings.Rajang.gameObject }.Distinct().ToArray(), "Select test monster");
            settings.Select(monster);
            if (!Application.isPlaying)
            {
                foreach (var test in new[] { settings.Khezu, settings.Rajang })
                    PrefabUtility.RecordPrefabInstancePropertyModifications(test.gameObject);
                EditorUtility.SetDirty(settings);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(settings.gameObject.scene);
            }
        }
    }
}
