#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

public static class ProofsHandVerification
{
    static Touchscreen touch;static int finger;
    [InitializeOnLoadMethod]static void Register(){EditorApplication.playModeStateChanged-=Cleanup;EditorApplication.playModeStateChanged+=Cleanup;}
    static void Cleanup(PlayModeStateChange state){if(state==PlayModeStateChange.EnteredEditMode&&touch!=null){InputSystem.RemoveDevice(touch);touch=null;}}
    public static void CheckBubbles(ProofsLibraryStageController stage,Transform near=null)
    {
        var indicators=stage.GetComponent<ProofsWorldInteractionIndicators>();
        if(indicators==null)throw new Exception("Missing shared hand indicators");
        indicators.Refresh();
        if(stage.PromptText.gameObject.activeSelf)throw new Exception("Redundant yellow instruction is visible");
        if(UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include,FindObjectsSortMode.None).Any(b=>b.name=="Proofs Interact"))throw new Exception("Dedicated interaction button was recreated");
        foreach(var entry in indicators.Bubbles)
        {
            bool expected=stage.IsIndicatorInteractionAvailable(entry.Key);
            if(entry.Value.gameObject.activeSelf!=expected)throw new Exception("Bubble availability differs from interaction state: "+entry.Key.name);
            if(!expected)continue;
            var color=entry.Value.Marker.color;
            if(color.r<.9f||color.g<.7f||color.b>.2f)throw new Exception("Bubble marker is not yellow");
            if(entry.Value.HandIcon.sprite==null||entry.Value.HandIcon.sprite!=stage.Presentation.HandButton.image.sprite)throw new Exception("Bubble does not use the actual hand icon");
            if(entry.Key==near&&(entry.Value.Expansion<.95f||entry.Value.Instruction.alpha<.9f))throw new Exception("Nearby hand instruction did not expand: "+entry.Key.name);
        }
    }
    public static IEnumerator Touch(Button button)
    {
        if(touch==null)touch=InputSystem.AddDevice<Touchscreen>();
        yield return null;Canvas.ForceUpdateCanvases();
        if(button==null||!button.gameObject.activeInHierarchy||!button.IsInteractable())throw new Exception("Hand touch is unavailable");
        var rect=(RectTransform)button.transform;var canvas=button.GetComponentInParent<Canvas>();
        Vector2 point=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,rect.TransformPoint(rect.rect.center));
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);
        float deadline=Time.unscaledTime+6;
        while(hits.Count>0&&hits[0].gameObject.name=="FadeScreen"&&Time.unscaledTime<deadline){yield return null;hits.Clear();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);}
        if(hits.Count==0||hits[0].gameObject.GetComponentInParent<Button>()!=button)throw new Exception("Hand touch blocked by "+(hits.Count==0?"no UI hit":hits[0].gameObject.name));
        int id=++finger;InputSystem.QueueStateEvent(touch,new TouchState{touchId=id,phase=UnityEngine.InputSystem.TouchPhase.Began,position=point});yield return null;yield return null;
        InputSystem.QueueStateEvent(touch,new TouchState{touchId=id,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=point});yield return null;yield return null;
    }
}
#endif
