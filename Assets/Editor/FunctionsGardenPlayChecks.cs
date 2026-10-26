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

/// <summary>Offline environmental checks run after the complete real-input onboarding test.</summary>
public static class FunctionsGardenPlayChecks
{
    const string Report="Library/FunctionsEnvironment/PlayChecks.txt";
    public static IEnumerator Run()
    {
        File.WriteAllText(Report,"Full movement/jump/hand/board/Domain/Range tutorial, practice removal, repeat and skip passed before these checks.\n");
        DressFunctionsGarden.Verify();
        var stage=UnityEngine.Object.FindFirstObjectByType<FunctionsRelationsStageManager>();
        var lamps=GameObject.Find(DressFunctionsGarden.RootName).GetComponentsInChildren<Light>();
        Check(RenderSettings.fog&&RenderSettings.fogStartDistance>=90,"Near gameplay stays outside the fog start distance.");
        foreach(var l in lamps)
        {
            foreach(var other in lamps)other.enabled=false;
            float off=SamplePool(l);l.enabled=true;float on=SamplePool(l);
            Check(on-off>.005f,l.transform.parent.name+" visibly illuminates ground: "+off.ToString("F3")+" → "+on.ToString("F3"));
        }
        foreach(var l in lamps)l.enabled=true;
        DressFunctionsGarden.Screenshot("PlayOverview",new Vector3(-188,68,845),new Vector3(-188,5,906),65);
        DressFunctionsGarden.Screenshot("PlayLamps",new Vector3(-216,7,864),new Vector3(-210,2,874),60);
        var hand=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b=>b.name=="InteractButton");
        Place(GameObject.Find("Book_Statue").transform.position+Vector3.back*3);yield return null;
        Tap(hand);yield return new WaitForSecondsRealtime(.5f);
        Check(stage.State==FunctionsRelationsState.ACTIVE,"Book hand interaction still starts the question.");
        var pair=stage.CurrentChallenge.requiredPairs[0];
        Place(Approach(stage.DomainTarget));yield return null;Tap(hand);yield return null;
        Check(stage.DomainButtons[0].gameObject.activeInHierarchy,"Domain remains reachable and selectable.");
        Tap(stage.DomainButtons[Array.IndexOf(stage.CurrentChallenge.domain,pair.domain)]);
        Place(Approach(stage.RangeTarget));yield return null;Tap(hand);yield return null;
        Check(stage.RangeButtons[0].gameObject.activeInHierarchy,"Range remains reachable after Domain selection.");
        Tap(stage.RangeButtons[Array.IndexOf(stage.CurrentChallenge.codomain,pair.codomain)]);yield return null;
        Check(stage.Mapping.Pairs.Contains(pair),"Real connection selection remains functional.");
        var presentation=UnityEngine.Object.FindFirstObjectByType<FunctionsRelationsPresentation>();
        Tap(presentation.Connections.RemoveButtons[pair]);yield return null;
        Check(!stage.Mapping.Pairs.Contains(pair),"Individual Remove remains functional.");stage.CloseDiamondChoices();
        Place(new Vector3(-188,2,908));yield return null;Tap(stage.ViewBoardButton);yield return new WaitForSecondsRealtime(1);
        Check(stage.BoardView.Viewing,"Board camera remains accessible.");
        foreach(var label in stage.WorldBoardLabels){label.ForceMeshUpdate(true);Check(!label.isTextOverflowing,"Board text fits: "+label.name);}
        foreach(int size in new[]{6,12,2})
        {
            BookMapMobileChecks.SetSize(size);yield return new WaitForSecondsRealtime(.6f);
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot("Library/FunctionsEnvironment/Board_"+Screen.width+"x"+Screen.height+".png");yield return null;
        }
        Tap(stage.BoardView.backButton);yield return new WaitForSecondsRealtime(1);
        Check(!stage.BoardView.Viewing&&Player.LocalInstance.enabled&&!GameInput.Instance.GameplayInputBlocked,"Back restores gameplay camera and control.");
        Check(stage.CorrectCount==0&&stage.MistakeCount==0,"Visual checks did not submit or change challenge progress.");
        File.AppendAllText(Report,"PASS ALL GARDEN PLAY MODE CHECKS. No saved progression or rewards written.\n");
    }
    static float SamplePool(Light light)
    {
        var go=new GameObject("Temporary Pool Probe");var camera=go.AddComponent<Camera>();
        var ground=light.transform.position-Vector3.up*5.4f;
        var inward=new Vector3(-188-ground.x,0,904-ground.z).normalized;
        var center=ground+inward*3;
        camera.transform.position=center+Vector3.up*13;camera.transform.LookAt(center,Vector3.forward);
        camera.orthographic=true;camera.orthographicSize=2;camera.farClipPlane=40;
        var rt=new RenderTexture(64,64,24);var prior=RenderTexture.active;
        try
        {
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var image=new Texture2D(64,64,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,64,64),0,0);image.Apply();
            var pixels=image.GetPixels();float result=pixels.Average(c=>.2126f*c.r+.7152f*c.g+.0722f*c.b);
            UnityEngine.Object.DestroyImmediate(image);return result;
        }
        finally{RenderTexture.active=prior;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
    }
    static Vector3 Approach(Transform target)
    {
        var b=target.Find("Station Climb Guard").GetComponent<Collider>().bounds;
        return new Vector3(b.center.x,2,b.min.z-3.2f);
    }
    static void Place(Vector3 position)
    {
        var cc=Player.LocalInstance.GetComponent<CharacterController>();bool enabled=cc.enabled;cc.enabled=false;
        Player.LocalInstance.transform.position=position;cc.enabled=enabled;Physics.SyncTransforms();
    }
    static void Tap(Button button)
    {
        Canvas.ForceUpdateCanvases();Check(button.gameObject.activeInHierarchy&&button.interactable,button.name+" available");
        var rect=(RectTransform)button.transform;
        var data=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
        Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,button.name+" mobile raycast unobstructed");
        ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerClickHandler);
    }
    static void Check(bool valid,string message)
    {if(!valid)throw new InvalidOperationException("Garden check: "+message);File.AppendAllText(Report,"PASS "+message+"\n");}
}
#endif
