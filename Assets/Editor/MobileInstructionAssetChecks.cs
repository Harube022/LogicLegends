#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Repair authored player copy through Unity serialization; never changes input bindings.</summary>
public static class MobileInstructionAssetChecks
{
    public static readonly Regex DesktopInstruction = new Regex(@"\b(?:WASD|keyboard|mouse|spacebar|click(?:ing)?|press\s+(?:E|Space|Esc|Enter))\b|\[E\]|\(E\)", RegexOptions.IgnoreCase);
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Run the authored copy repair in Edit Mode with a saved scene.");
        var original=SceneManager.GetActiveScene().path;
        var report=new System.Text.StringBuilder("Authored mobile instructions; Unity text components only; input bindings unchanged.\n");
        try
        {
            string prefab="Assets/Resources/StageUI/PrelimHelpBook.prefab";
            var root=PrefabUtility.LoadPrefabContents(prefab);
            try { if(Repair(root,report,prefab)>0) PrefabUtility.SaveAsPrefabAsset(root,prefab); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var scenes=new[]{"Induction","PRELIM","Trees","TruthTable"}.Select(s=>"Assets/Scenes/"+s+".unity")
                .Concat(AssetDatabase.FindAssets("t:Scene",new[]{"Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE"}).Select(AssetDatabase.GUIDToAssetPath));
            foreach(var path in scenes)
            {
                var scene=EditorSceneManager.OpenScene(path);int changed=0;
                foreach(var obj in scene.GetRootGameObjects())changed+=Repair(obj,report,path);
                if(changed>0)EditorSceneManager.SaveScene(scene);
            }
        }
        finally { EditorSceneManager.OpenScene(original);File.WriteAllText("Library/MobileInstructionAssetChecks.txt",report.ToString()); }
    }
    static string Mobile(string value)=>value.Replace("Joystick or WASD","Use the joystick")
        .Replace("WASD / joystick: move   |   Right-side drag: look   |   Space / jump button: jump","Joystick: move   |   Drag the right side: look   |   Jump button: jump");
    static int Repair(GameObject root,System.Text.StringBuilder report,string path)
    {
        int changes=0;
        foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            string replacement=Mobile(text.text??"");if(replacement==text.text)continue;
            var so=new SerializedObject(text);so.FindProperty("m_text").stringValue=replacement;so.ApplyModifiedPropertiesWithoutUndo();
            report.AppendLine(path+" :: "+text.name);changes++;
        }
        foreach(var text in root.GetComponentsInChildren<UnityEngine.UI.Text>(true))
        {
            string replacement=Mobile(text.text??"");if(replacement==text.text)continue;
            var so=new SerializedObject(text);so.FindProperty("m_Text").stringValue=replacement;so.ApplyModifiedPropertiesWithoutUndo();
            report.AppendLine(path+" :: "+text.name);changes++;
        }
        return changes;
    }
    public static void VerifyVisibleCopy(string scene)
    {
        foreach(var text in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            if(DesktopInstruction.IsMatch(text.text??""))throw new InvalidOperationException("Desktop instruction in "+scene+" :: "+text.name+" :: "+text.text);
    }
}
#endif
