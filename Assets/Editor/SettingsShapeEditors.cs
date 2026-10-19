using UnityEditor;
using UnityEditor.UI;

[CustomEditor(typeof(SettingsTabFace)), CanEditMultipleObjects]
public sealed class SettingsTabFaceEditor : GraphicEditor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("cornerSize"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("edgeSoftness"));
        serializedObject.ApplyModifiedProperties();
    }
}

[CustomEditor(typeof(SettingsRoundedGraphic)), CanEditMultipleObjects]
public sealed class SettingsRoundedGraphicEditor : GraphicEditor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("surface"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("roundLeft"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("roundRight"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("pillShape"));
        using (new EditorGUI.DisabledScope(serializedObject.FindProperty("pillShape").boolValue))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("cornerRadius"));
        serializedObject.ApplyModifiedProperties();
    }
}
