#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Shared by the offline, saved-scene book checks. No submissions or rewards.</summary>
public static class BookMapMobileChecks
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    const string Report="Library/BookStatuePlayChecks.txt";
    static readonly Type View=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
    public static int GetSize() => (int)View.GetProperty("selectedSizeIndex",Flags).GetValue(EditorWindow.GetWindow(View));
    public static void SetSize(int size)
    {
        View.GetProperty("selectedSizeIndex",Flags).SetValue(EditorWindow.GetWindow(View),size);
        Crystal.SafeArea.Sim=size==12?Crystal.SafeArea.SimDevice.Pixel3XL_LSR:size==3?Crystal.SafeArea.SimDevice.iPhoneX:Crystal.SafeArea.SimDevice.None;
    }
    public static IEnumerator Run(string scene,BookStatuePresentation book)
    {
        Application.runInBackground=true;
        var map=UnityEngine.Object.FindObjectsByType<ExpandableMinimap>(FindObjectsSortMode.None).FirstOrDefault(m=>m.gameObject.activeInHierarchy);
        var icon=book.Bubble.GetComponentsInChildren<Image>(true).First(i=>i.name=="Actual hand button icon");
        var hand=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b=>b.name=="InteractButton");
        Check(icon.sprite==hand.image.sprite,"bubble uses the actual hand icon");
        foreach(int size in new[]{6,12,3})
        {
            SetSize(size);yield return new WaitForSecondsRealtime(.4f);yield return new WaitForEndOfFrame();
            foreach(var text in book.Bubble.GetComponentsInChildren<TMP_Text>())
            {text.ForceMeshUpdate();Check(!text.isTextOverflowing,"bubble text fits: "+scene+" "+text.text);}
            Check(book.Bubble.GetComponentsInChildren<TMP_Text>(true).Count(t=>t.name=="Book Interaction Indicator")==1,"one book prompt");
            foreach(var button in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
                Check(!button.gameObject.activeInHierarchy||!button.interactable||button.GetComponentInChildren<TMP_Text>()==null||button.GetComponentInChildren<TMP_Text>().text!="Interact to start","no clickable book-start button");
            if(scene=="PRELIM"||scene=="Truth_Table") CheckHud(scene,map);
            if(map!=null)
            {
                var parent=map.transform.parent;var scale=map.transform.localScale;
                CheckRaycast(map.gameObject);
                var data=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left};
                ExecuteEvents.Execute(map.gameObject,data,ExecuteEvents.pointerClickHandler);
                yield return new WaitForSecondsRealtime(.2f);yield return new WaitForEndOfFrame();
                Check(map.IsOpen&&GameInput.Instance.GameplayInputBlocked&&!Player.LocalInstance.enabled&&Time.timeScale==0,"map opens and pauses controls");
                var safe=(RectTransform)GameObject.Find("Expanded Map/Map Safe Area").transform;
                Check(Vector2.Distance(ScreenRect(map.Panel).center,ScreenRect(safe).center)<2,"full map centered");
                Check(Contains(ScreenRect(safe),ScreenRect(map.Panel)),"full map fits safe area");
                var cameraMap=UnityEngine.Object.FindFirstObjectByType<TruthTableMinimap>();
                if(cameraMap!=null)
                {
                    var camera=Field<Camera>(cameraMap,"minimapCamera");var bounds=Field<Bounds>(cameraMap,"playableBounds");
                    foreach(var corner in new[]{bounds.min,bounds.max})
                    {var p=camera.WorldToViewportPoint(corner);Check(p.x>=0&&p.x<=1&&p.y>=0&&p.y<=1,"overview includes entire playable floor");}
                    Check(camera.targetTexture.width==1024,"expanded map uses readable high-resolution texture");
                }
                bool required=book.InteractionRequired;GameInput.Instance.MobileInteract();
                Check(book.InteractionRequired==required,"map taps cannot activate book");
                yield return Capture(scene+"_FullMap");
                var close=GameObject.Find("Expanded Map/Map Safe Area/Close Expanded Map").GetComponent<Button>();CheckRaycast(close.gameObject);
                close.onClick.Invoke();yield return new WaitForSecondsRealtime(.4f);yield return new WaitForEndOfFrame();
                Check(!map.IsOpen&&map.transform.parent==parent&&map.transform.localScale==scale,"minimap dock restored");
                Check(!GameInput.Instance.GameplayInputBlocked&&Player.LocalInstance.enabled&&Time.timeScale==1,"map closes and restores controls");
            }
            yield return Capture(scene+"_DockAndBubble");
        }
        // Exercise bubble contraction and safe expansion independently of page pose.
        var at=Player.LocalInstance.transform.position;var rotation=Player.LocalInstance.transform.rotation;
        var controller = Player.LocalInstance.GetComponent<CharacterController>();
        controller.enabled = false; // Keep the proximity probe from falling off graph platforms.
        PrelimTeleportTransition.MovePlayerImmediately(Player.LocalInstance.gameObject,at+Vector3.back*150,rotation);
        yield return new WaitForSecondsRealtime(.35f);
        Check(!book.PlayerInRange&&book.Expansion<.05f,"bubble contracts outside interaction range");
        hand.onClick.Invoke();Check(book.InteractionRequired,"out-of-range hand cannot activate book");
        PrelimTeleportTransition.MovePlayerImmediately(Player.LocalInstance.gameObject,at,rotation);
        controller.enabled = true;
        yield return new WaitForSecondsRealtime(.45f);
        Check(book.PlayerInRange&&book.Expansion>.99f,"bubble expands on re-entry");
        var anchor=book.IndicatorAnchor.localPosition;
        float until=Time.unscaledTime+3.3f;
        while(Time.unscaledTime<until)
        {
            Check(book.Bubble.gameObject.activeInHierarchy&&book.IndicatorAnchor.localPosition==anchor,"page flips cannot hide or move bubble");
            yield return null;
        }
        SetSize(6);
        File.AppendAllText(Report,scene+": PASS hand-only bubble, actual icon, near/far animation and page independence; "+(map!=null?"compact/full map, safe area, Close, input and timer restoration":"HUD")+" at 1920x1080, 2960x1440, 720x1280.\n");
    }
    static void CheckHud(string scene,ExpandableMinimap map)
    {
        var hud=(RectTransform)StageJourneyUI.Instance.transform.Find("SafeArea/CurrentStageHud");
        Check(Contains(ScreenRect((RectTransform)hud.parent),ScreenRect(hud)),"HUD fits safe area");
        foreach(var text in hud.GetComponentsInChildren<TMP_Text>()){text.ForceMeshUpdate();Check(!text.isTextOverflowing,"HUD text fits: "+text.text);}
        if(scene!="Truth_Table")return;
        Check(map!=null&&ScreenRect(hud).yMax<ScreenRect(map.Panel).yMin,"Truth Table HUD below minimap");
        Check(Mathf.Abs(ScreenRect(hud).xMax-ScreenRect(map.Panel).xMax)<2,"Truth Table HUD aligned with minimap");
        var clock=UnityEngine.Object.FindFirstObjectByType<TruthTableStageClock>();var notice=Field<PrelimChallengeCompletionNotice>(clock,"completionNotice");
        notice.ShowWaiting(1,2);Canvas.ForceUpdateCanvases();
        // Layout normally runs at LateUpdate; invoke it here for the same-frame assertion.
        typeof(PrelimChallengeCompletionNotice).GetMethod("LateUpdate",Flags).Invoke(notice,null);
        var panel=Field<RectTransform>(notice,"panel");var message=Field<TMP_Text>(notice,"message");message.ForceMeshUpdate();
        Check(!panel.GetComponent<Image>().enabled&&!message.isTextOverflowing,"transparent completion message fits");
        Check(ScreenRect(panel).yMax<ScreenRect(hud).yMin&&Mathf.Abs(ScreenRect(panel).width-ScreenRect(hud).width)<2,"completion message below HUD with matching width");
        Check(Contains(ScreenRect((RectTransform)hud.parent),ScreenRect(panel)),"completion message fits safe area");
    }
    static void CheckRaycast(GameObject target)
    {
        var data=new PointerEventData(EventSystem.current){position=ScreenRect((RectTransform)target.transform).center};
        var results=new List<RaycastResult>();EventSystem.current.RaycastAll(data,results);
        Check(results.Count>0&&(results[0].gameObject==target||results[0].gameObject.transform.IsChildOf(target.transform)),"click reaches "+target.name+"; hit="+string.Join(",",results.Take(3).Select(r=>r.gameObject.name)));
    }
    static Rect ScreenRect(RectTransform rect){var c=new Vector3[4];rect.GetWorldCorners(c);return Rect.MinMaxRect(c[0].x,c[0].y,c[2].x,c[2].y);}
    static bool Contains(Rect a,Rect b)=>b.xMin>=a.xMin-1&&b.yMin>=a.yMin-1&&b.xMax<=a.xMax+1&&b.yMax<=a.yMax+1;
    static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,Flags).GetValue(target);
    static void Check(bool valid,string message){if(!valid)throw new InvalidOperationException(message);}
    static IEnumerator Capture(string name)
    {
        Directory.CreateDirectory("Temp/BookMapQA");yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot("Temp/BookMapQA/"+Screen.width+"x"+Screen.height+"_"+name+".png");yield return null;
    }
}
#endif
