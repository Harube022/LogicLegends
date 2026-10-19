using LogicLegends.OpenArchive.Hunting;
using UnityEditor;
using UnityEngine;

namespace LogicLegends.Tests.Khezu.Editor
{
    [CustomEditor(typeof(KhezuHuntTestControls))]
    public sealed class KhezuHuntingTestEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var test = (KhezuHuntTestControls)target;
            if (test.monster == null) return;
            EditorGUILayout.LabelField("State", test.monster.State.ToString());
            EditorGUILayout.LabelField("Target", test.monster.TargetName + " / " + test.monster.TargetPlayerId);
            EditorGUILayout.LabelField("HuntedPlayerIds", string.Join(", ", test.monster.HuntedPlayerIds));
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("Force four shared mistakes")) test.ForceFourMistakes();
                if (GUILayout.Button("Start / resume hunting")) test.StartHunting();
                if (GUILayout.Button("Reset current hunt (keep rotation)")) test.ResetCurrentHunt();
                if (GUILayout.Button("Reset full hunting rotation")) test.ResetFullRotation();
                if (GUILayout.Button("Return to sleep here (keep rotation)")) test.ReturnToSleep();
            }
            if (Application.isPlaying) Repaint();
        }
    }
}
