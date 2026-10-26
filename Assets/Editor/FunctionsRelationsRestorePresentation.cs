#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Restore original art and copy PRELIM's authored touch-control transforms, retaining bindings.</summary>
public static class FunctionsRelationsRestorePresentation
{
    [Serializable] public sealed class ControlLayout
    {
        public Vector2 min,max,pivot,position,size;
        public Vector3 scale,rotation;
        public ControlLayout(RectTransform r){min=r.anchorMin;max=r.anchorMax;pivot=r.pivot;position=r.anchoredPosition;size=r.sizeDelta;scale=r.localScale;rotation=r.localEulerAngles;}
        public void Apply(RectTransform r){r.anchorMin=min;r.anchorMax=max;r.pivot=pivot;r.anchoredPosition=position;r.sizeDelta=size;r.localScale=scale;r.localEulerAngles=rotation;}
        public bool Matches(RectTransform r)=>Vector2.Distance(min,r.anchorMin)<.01f&&Vector2.Distance(max,r.anchorMax)<.01f&&Vector2.Distance(pivot,r.pivot)<.01f&&Vector2.Distance(position,r.anchoredPosition)<.01f&&Vector2.Distance(size,r.sizeDelta)<.01f&&Vector3.Distance(scale,r.localScale)<.01f&&Vector3.Distance(rotation,r.localEulerAngles)<.01f;
    }
    public static void Apply()
    {
        var scene=SceneManager.GetActiveScene();
        if(EditorApplication.isPlayingOrWillChangePlaymode||scene.name!="Functions and Relations")throw new InvalidOperationException("Open the saved Functions and Relations scene in Edit Mode.");
        var manager=UnityEngine.Object.FindFirstObjectByType<FunctionsRelationsStageManager>();string saved=EditorJsonUtility.ToJson(manager);
        var decorations=GameObject.Find("StageBorderDecorations");if(decorations!=null)Undo.DestroyObjectImmediate(decorations);
        if(GameObject.Find("StageCollisionGuards")==null||GameObject.Find("Grounded Stone Path")==null)throw new InvalidOperationException("Functional world fixes must remain.");
        var reference=EditorSceneManager.OpenPreviewScene("Assets/Scenes/PRELIM.unity");
        try
        {
            var source=reference.GetRootGameObjects().Single(g=>g.name=="Canvas 1");var target=GameObject.Find("Canvas 1");
            var from=source.GetComponentsInChildren<RectTransform>(true);var to=target.GetComponentsInChildren<RectTransform>(true);
            foreach(string name in new[]{"SafeArea","Gameplay_Interface","Joystick","JumpButton","InteractButton","SettingsButton"})
            {
                var src=from.Single(r=>r.name==name);var dst=to.Single(r=>r.name==name);var layout=new ControlLayout(src);
                Undo.RecordObject(dst,"Match PRELIM controls");layout.Apply(dst);PrefabUtility.RecordPrefabInstancePropertyModifications(dst);
                SessionState.SetString("Functions.PrelimLayout."+name,JsonUtility.ToJson(layout));
                if(name=="Joystick")foreach(var child in src.GetComponentsInChildren<RectTransform>(true).Where(c=>c!=src))
                {var relative=AnimationUtility.CalculateTransformPath(child,src);var r=dst.Find(relative) as RectTransform;if(r!=null){new ControlLayout(child).Apply(r);PrefabUtility.RecordPrefabInstancePropertyModifications(r);}}
            }
            var srcScaler=source.GetComponent<CanvasScaler>();var dstScaler=target.GetComponent<CanvasScaler>();
            dstScaler.uiScaleMode=srcScaler.uiScaleMode;dstScaler.referenceResolution=srcScaler.referenceResolution;dstScaler.screenMatchMode=srcScaler.screenMatchMode;dstScaler.matchWidthOrHeight=srcScaler.matchWidthOrHeight;
            EditorUtility.SetDirty(dstScaler);PrefabUtility.RecordPrefabInstancePropertyModifications(dstScaler);
            var srcSafe=new SerializedObject(source.GetComponentInChildren<Crystal.SafeArea>(true));var dstSafe=new SerializedObject(target.GetComponentInChildren<Crystal.SafeArea>(true));
            foreach(string field in new[]{"ConformX","ConformY","Logging"})dstSafe.FindProperty(field).boolValue=srcSafe.FindProperty(field).boolValue;
            dstSafe.ApplyModifiedPropertiesWithoutUndo();
        }
        finally{EditorSceneManager.ClosePreviewScene(reference);}
        var ui=UnityEngine.Object.FindFirstObjectByType<FunctionsRelationsPresentation>();
        ui.rangePanel.Find("Heading").GetComponent<TMPro.TMP_Text>().text="RANGE";
        if(saved!=EditorJsonUtility.ToJson(manager))throw new InvalidOperationException("Restoration changed gameplay references.");
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        System.IO.File.WriteAllText("Library/FunctionsRelationsRestoration.txt","Removed StageBorderDecorations and its recent art/terrain apron. Original Stage and Trees models retained. Functional path, tree/altar guards and four perimeter boundaries retained. Copied exact PRELIM control anchors, pivots, positions, sizes, scales and safe-area/scaler settings.\n");
    }
}
#endif
