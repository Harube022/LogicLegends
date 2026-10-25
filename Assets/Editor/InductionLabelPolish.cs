#if UNITY_EDITOR
using System;
using System.Linq;
using LogicLegends.Induction;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class InductionLabelPolish
{
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="Induction")throw new InvalidOperationException("Open Induction in Edit Mode.");
        var stage=UnityEngine.Object.FindFirstObjectByType<InductionStageManager>();
        foreach(var station in stage.stations)
        {
            var label=station.GetComponentsInChildren<TMP_Text>(true).Single(t=>t.name=="Station Label");
            var old=label.GetComponent<FaceCamera>();if(old!=null)UnityEngine.Object.DestroyImmediate(old);
            var anchor=label.GetComponent<InductionWorldLabel>()??label.gameObject.AddComponent<InductionWorldLabel>();
            anchor.anchor=station.GetComponent<Renderer>();anchor.clearance=3.2f;
            anchor.horizontalOffset=new Vector3(0,0,-anchor.anchor.bounds.extents.z-.3f);
            label.font=StageUITheme.HeadingFont;label.fontStyle=FontStyles.Bold;
            label.color=new Color(1,.94f,.73f);label.outlineColor=new Color(.025f,.025f,.04f,1);label.outlineWidth=.17f;
            label.rectTransform.sizeDelta=new Vector2(8.6f,1.25f);label.rectTransform.pivot=new Vector2(.5f,.5f);
            label.transform.localScale=Vector3.one;label.enableAutoSizing=true;label.fontSize=label.fontSizeMax=6.5f;label.fontSizeMin=5;
            label.textWrappingMode=TextWrappingModes.NoWrap;label.overflowMode=TextOverflowModes.Overflow;
            InductionWorldLabel.PlaceAbove(label,anchor.anchor.bounds,anchor.clearance,anchor.horizontalOffset);
            EditorUtility.SetDirty(label);EditorUtility.SetDirty(anchor);
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }
}
#endif
