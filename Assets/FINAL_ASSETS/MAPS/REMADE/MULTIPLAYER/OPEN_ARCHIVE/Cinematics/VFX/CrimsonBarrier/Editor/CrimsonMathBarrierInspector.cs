using LogicLegends.OpenArchive.Cinematics.VFX;
using UnityEditor;
using UnityEngine;

namespace LogicLegends.OpenArchive.Editor
{
    [CustomEditor(typeof(CrimsonMathBarrierVfx))]
    public sealed class CrimsonMathBarrierInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI(){
            DrawDefaultInspector();var vfx=(CrimsonMathBarrierVfx)target;
            if(vfx.style!=null){
                EditorGUILayout.Space();EditorGUILayout.LabelField("Shared visual style",EditorStyles.boldLabel);
                var style=new SerializedObject(vfx.style);style.Update();var iterator=style.GetIterator();
                bool first=true;while(iterator.NextVisible(first)){first=false;if(iterator.name=="m_Script")continue;EditorGUILayout.PropertyField(iterator,true);}style.ApplyModifiedProperties();
            }
            EditorGUILayout.HelpBox("Presentation only. The existing encounter controller must own movement locking, challenge buttons and authoritative outcomes.",MessageType.Info);
            if(GUILayout.Button("Preview formed barrier"))vfx.Preview(2);
            if(GUILayout.Button("Preview energy rising"))vfx.Preview(.4f);
            if(GUILayout.Button("Preview breaking runes"))vfx.Preview(2,.55f);
            if(Application.isPlaying && GUILayout.Button("Play dissolve"))vfx.Dissolve();
        }
    }
}
