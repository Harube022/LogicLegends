using UnityEditor;
using UnityEngine;

namespace LogicLegends.Tests.Khezu.Editor
{
    [CustomEditor(typeof(KhezuAwakeningTestController))]
    public sealed class KhezuAwakeningTestEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var test = (KhezuAwakeningTestController)target;
            var hunt = test.GetComponent<LogicLegends.OpenArchive.Hunting.KhezuHuntingController>();
            EditorGUILayout.HelpBox(hunt == null ? "Phase 1 animation test. Enter Play Mode, then add mistakes. Rapid clicks queue every awakening stage. Reset cancels the queue and blends to Sleep." :
                "Phase 2: four shared mistakes awaken Khezu, then the host selects a player and begins hunting. Preview controls remain in the separate Phase 1 scene.", MessageType.Info);
            EditorGUILayout.LabelField("Mistakes requested", test.Mistakes + " / 4");
            EditorGUILayout.LabelField("Stage playing", test.PlayingStage.ToString());
            EditorGUILayout.LabelField("Animator state", test.CurrentState);
            EditorGUILayout.LabelField("Mode", test.IsPreviewing ? "Animation preview" : "Awakening sequence");
            using (new EditorGUI.DisabledScope(!Application.isPlaying || !test.enabled))
            {
                using (new EditorGUI.DisabledScope(test.Mistakes >= 4 && !test.IsPreviewing))
                    if (GUILayout.Button("Increase Mistakes (+1)")) test.IncreaseMistakeCount();
                if (GUILayout.Button("Reset Test — Sleep")) test.ResetTest();
                if (hunt != null)
                {
                    EditorGUILayout.LabelField("Hunt state", hunt.State.ToString());
                    EditorGUILayout.LabelField("Target", hunt.TargetName + " / " + hunt.TargetPlayerId);
                    EditorGUILayout.LabelField("HuntedPlayerIds", string.Join(", ", hunt.HuntedPlayerIds));
                    if (GUILayout.Button("Force Four Shared Mistakes")) hunt.Request(LogicLegends.OpenArchive.Hunting.KhezuCommand.ForceFourMistakes);
                    if (GUILayout.Button("Start / Resume Hunting")) hunt.Request(LogicLegends.OpenArchive.Hunting.KhezuCommand.StartHunting);
                    if (GUILayout.Button("Reset Current Hunt")) hunt.Request(LogicLegends.OpenArchive.Hunting.KhezuCommand.ResetHunt);
                    if (GUILayout.Button("Reset Full Hunting Rotation")) hunt.Request(LogicLegends.OpenArchive.Hunting.KhezuCommand.ResetRotation);
                }
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Preview existing animations", EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(hunt != null))
                    foreach (var name in test.AvailableAnimations)
                        if (GUILayout.Button("Preview " + name)) test.PreviewAnimation(name);
            }
            if (Application.isPlaying) Repaint();
        }
    }
}
