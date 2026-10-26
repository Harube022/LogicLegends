#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Unity.Cinemachine;

/// <summary>Actual gameplay, normal touch interactions and natural physics; recording effects stay Editor-only.</summary>
public static class FunctionsRelationsHelpClipCapture
{
    const string Output="Library/StageHelpCaptures/", Evidence="Library/FunctionsHelpSequence.txt";
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    public static readonly string[] Keys={"FunctionsRelations","FunctionsRelations_02_Read","FunctionsRelations_03_Connect","FunctionsRelations_04_Submit"};
    public static readonly string[] Captions={
        "Tap the hand at the Book Statue to begin.",
        "Tap View Board to read the question.",
        "Choose an input at Domain, then an output at Range.",
        "Submit your completed connections."
    };
    static FunctionsRelationsStageManager stage;
    static MappingChallengeData sameQuestion;
    static Camera camera;static CinemachineBrain brain;static CinemachineCamera worldCamera;
    static Button hand,view,back,submit;
    static Canvas overlay;static RecordingVeil veil;static RecordingTouchRing ring;static Image fade;
    static Touchscreen touch;static MediaEncoder encoder;
    static Texture2D pixels,zoomPixels;static RenderTexture zoomTexture;static Rect crop;
    static readonly List<StageHelpVideoCatalog.Cue>[] guides=Enumerable.Range(0,4).Select(i=>new List<StageHelpVideoCatalog.Cue>()).ToArray();
    static int frames,segment,finger;static float handScale;
    static Rect Full=>new Rect(0,0,Screen.width,Screen.height);
    public static IEnumerator Record()
    {
        stage=UnityEngine.Object.FindFirstObjectByType<FunctionsRelationsStageManager>();
        hand=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b=>b.name=="InteractButton");
        view=stage.ViewBoardButton;back=stage.BoardView.backButton;submit=stage.SubmitButton;
        camera=Camera.main;brain=camera.GetComponent<CinemachineBrain>();worldCamera=GameObject.Find("CM_ThirdPersonCam").GetComponent<CinemachineCamera>();
        foreach(var c in UnityEngine.Object.FindObjectsByType<Behaviour>(FindObjectsSortMode.None))
            if(c is CinemachineInputAxisController||c is CinemachinePinchZoom||c is ThirdPersonCameraController)c.enabled=false;
        worldCamera.Follow=null;worldCamera.LookAt=null;brain.enabled=false;
        handScale=hand.transform.localScale.x;touch=InputSystem.AddDevice<Touchscreen>();finger=9600;
        BuildOverlay();Time.captureFramerate=24;crop=Full;
        File.WriteAllText(Evidence,"Actual offline Game view, 24 fps. One normal C1 question. Real Input System touches, hand-only stations, normal pair removal and dedicated Submit. Saved rewards and progression suppressed by shared harness.\n");
        try
        {
            var book=stage.BookTarget;
            Position(Ground(book.position+Vector3.back*8,15),book.position);
            SetCamera(book.position+new Vector3(9,9,-15),book.position+Vector3.up*2,55);
            Start(0);yield return FadeIn();yield return Walk(Ground(book.position+Vector3.back*3,15),6);Face(book.position);
            yield return ZoomButton(hand,1.5f);Snapshot("01_Hand");yield return Tap((RectTransform)hand.transform);
            Require(stage.State==FunctionsRelationsState.ACTIVE&&stage.QuestionIndex==0,"hand starts actual C1 Q1");
            sameQuestion=stage.CurrentChallenge;Log("Consistent question: "+string.Join(", ",sameQuestion.requiredPairs));
            yield return Frames(30);yield return Finish();ResetView();

            var board=stage.BoardView.informationBoard;
            Position(Ground(board.position+Vector3.back*11,15),board.position);
            SetCamera(board.position+new Vector3(10,12,-21),board.position+Vector3.up*7,54);
            Start(1);yield return FadeIn();yield return Walk(Ground(board.position+Vector3.back*8,15),6);
            Require(view.gameObject.activeInHierarchy,"View Board available in reading area");
            yield return ZoomButton(view,1.4f);yield return Tap((RectTransform)view.transform);veil.SetHoles();
            yield return Frames(28);Require(stage.IsBoardOpen,"actual board view opens");
            yield return FrameBoard(stage.BoardView.boardInstructions);
            veil.SetHoles(Union(TextRect(stage.WorldBoardLabels[1],20),TextRect(stage.WorldBoardLabels[2],20)));
            Snapshot("02_Board");yield return Frames(80);yield return Finish();ResetView();
            Log("Before Back: viewing="+stage.BoardView.Viewing+" transition="+stage.BoardView.Transitioning+" scale="+Time.timeScale);
            yield return Tap((RectTransform)back.transform);
            int closing=0;float backBy=Time.realtimeSinceStartup+4;
            while(stage.IsBoardOpen&&Time.realtimeSinceStartup<backBy){closing++;yield return Frames(1);}
            Log("After Back: viewing="+stage.BoardView.Viewing+" transition="+stage.BoardView.Transitioning+" frames="+closing);
            Require(!stage.IsBoardOpen,"Back restores actual gameplay");

            var pairs=sameQuestion.requiredPairs;var ui=UnityEngine.Object.FindFirstObjectByType<FunctionsRelationsPresentation>();
            var approach=FunctionsRelationsPlayVerification.StationApproach(stage.DomainTarget);
            Position(approach+Vector3.back*1.5f,stage.DomainTarget.position);
            StationCamera(stage.DomainTarget);Start(2);yield return FadeIn();yield return Walk(approach,6);
            Cue("Tap the hand at Domain. Choose an input.");
            yield return Input(pairs[0],true);
            Snapshot("03_ArrowRange");yield return Travel(stage.RangeTarget);
            Cue("Follow the arrow to Range. Choose an available output.");yield return OutputChoice(pairs[0],true);
            Snapshot("03_Connected");yield return Frames(20);
            // A second pair makes it clear that Remove is independent of creation order.
            yield return Travel(stage.DomainTarget);yield return Input(pairs[1],false);
            yield return Travel(stage.RangeTarget);yield return OutputChoice(pairs[1],false);
            Cue("In Range, remove any connection using its own button.");
            crop=Full;veil.SetHoles(ScreenRect(ui.Connections.Panel,12));yield return Focus(ScreenRect(ui.Connections.Panel,12),2.15f);yield return Frames(24);
            var remove=ui.Connections.RemoveButtons[pairs[0]];
            yield return ZoomButton(remove,1.08f);yield return Tap((RectTransform)remove.transform);yield return Frames(18);
            Require(!stage.Mapping.Pairs.Contains(pairs[0])&&stage.Mapping.Pairs.Contains(pairs[1]),"remove older pair while keeping newer connection");
            Snapshot("03_RemovedOlder");yield return Finish();ResetView();

            // Trim repetitive station travel between clips. Every remaining pair still uses normal hand/choice UI.
            foreach(var pair in pairs)if(!stage.Mapping.Pairs.Contains(pair))
            {
                Position(FunctionsRelationsPlayVerification.StationApproach(stage.DomainTarget),stage.DomainTarget.position);yield return Input(pair,false);
                Position(FunctionsRelationsPlayVerification.StationApproach(stage.RangeTarget),stage.RangeTarget.position);yield return OutputChoice(pair,false);
            }
            Require(MappingRuleEvaluator.ExactPairs(stage.Mapping.Snapshot(),pairs),"all required connections are complete before Submit");
            Position(Ground(new Vector3(-188,2,901),15),board.position);SetCamera(new Vector3(-188,18,879),new Vector3(-188,6,906),56);
            Start(3);yield return FadeIn();veil.SetHoles(ScreenRect(ui.Connections.Panel,12));yield return Frames(26);
            yield return ZoomButton(submit,1.5f);yield return Focus(ScreenRect((RectTransform)submit.transform,16),1.35f);
            Require(submit.GetComponentInChildren<TMP_Text>().text=="Submit","dedicated Submit label");
            yield return Tap((RectTransform)submit.transform);ResetView();yield return Frames(18);Snapshot("04_Correct");
            yield return Frames(32);Require(stage.ChallengeIndex==0&&stage.QuestionIndex==1&&stage.State==FunctionsRelationsState.WAITING_FOR_ACTIVATION,"question one advances only to waiting question two");
            Snapshot("04_Question2");yield return Frames(20);yield return Finish();
            File.WriteAllText(Output+Keys[0]+".json",JsonUtility.ToJson(new StageHelpVideoCatalog.Recording{scene="Functions and Relations",clips=Keys,source="Actual isolated Unity Game view. One normal randomized C1 question. Stable recording-only cameras, physical guided movement, real hand/UI touches, individual pair removal and dedicated Submit. Every clip ends in an encoded two-second fade. No account progress or rewards written.",cues=Captions.Select(c=>new StageHelpVideoCatalog.Cue{seconds=0,caption=c}).ToArray(),guides=guides.Select(g=>new StageHelpVideoCatalog.ClipGuide{cues=g.ToArray()}).ToArray()},true));
            Log("PASS four actual clips, same question, hand-first input/output, exact older-pair removal, complete normal validation and Question 2 of 2.");
        }
        finally
        {
            encoder?.Dispose();encoder=null;Time.captureFramerate=0;Time.timeScale=1;
            if(pixels!=null)UnityEngine.Object.Destroy(pixels);if(zoomPixels!=null)UnityEngine.Object.Destroy(zoomPixels);
            if(zoomTexture!=null){zoomTexture.Release();UnityEngine.Object.Destroy(zoomTexture);}
            if(touch!=null&&touch.added)InputSystem.RemoveDevice(touch);if(overlay!=null)UnityEngine.Object.Destroy(overlay.gameObject);
        }
    }
    static IEnumerator Input(MappingPair pair,bool highlight)
    {
        if(highlight)yield return ZoomButton(hand,1.4f);
        yield return Tap((RectTransform)hand.transform);ResetView();yield return Frames(5);
        var button=stage.DomainButtons[Array.IndexOf(sameQuestion.domain,pair.domain)];
        if(highlight){veil.SetHoles(ScreenRect((RectTransform)button.transform.parent,10));yield return Focus(ScreenRect((RectTransform)button.transform.parent,10),1.35f);yield return Frames(25);}
        yield return Tap((RectTransform)button.transform);if(highlight)yield return Overview();ResetView();yield return Frames(6);
        Require(stage.SelectedDomain==pair.domain,"real input choice");
    }
    static IEnumerator OutputChoice(MappingPair pair,bool highlight)
    {
        if(highlight)yield return ZoomButton(hand,1.4f);
        yield return Tap((RectTransform)hand.transform);ResetView();yield return Frames(5);
        var button=stage.RangeButtons[Array.IndexOf(sameQuestion.codomain,pair.codomain)];
        if(highlight){veil.SetHoles(ScreenRect((RectTransform)button.transform.parent,10));yield return Focus(ScreenRect((RectTransform)button.transform.parent,10),1.35f);yield return Frames(25);}
        yield return Tap((RectTransform)button.transform);if(highlight)yield return Overview();ResetView();yield return Frames(6);
        Require(stage.Mapping.Pairs.Contains(pair)&&stage.SelectedDomain==null,"real output choice completes connection");
    }
    static IEnumerator Travel(Transform target)
    {
        var ui=UnityEngine.Object.FindFirstObjectByType<FunctionsRelationsPresentation>();
        if(ui.rangePanel.gameObject.activeInHierarchy)yield return Tap((RectTransform)ui.rangePanel.Find("Close Choices"));
        ResetView();var center=new Vector3(-188.5f,5.8f,906);
        yield return CameraTo(center+new Vector3(0,14,-24),center,56);
        yield return Walk(FunctionsRelationsPlayVerification.StationApproach(target),10);
        yield return CameraTo(StationPosition(target),StationLook(target),54);
    }
    static Vector3 StationLook(Transform t){var b=t.Find("Object").GetComponent<Renderer>().bounds;return b.center+Vector3.up*1.5f;}
    static Vector3 StationPosition(Transform t)=>StationLook(t)+new Vector3(8,5,-16);
    static void StationCamera(Transform t)=>SetCamera(StationPosition(t),StationLook(t),54);
    static IEnumerator CameraTo(Vector3 position,Vector3 target,float fov)
    {
        brain.enabled=false;var start=camera.transform.position;var rotation=camera.transform.rotation;float old=camera.fieldOfView;
        for(int i=0;i<20;i++){float t=Mathf.SmoothStep(0,1,(i+1)/20f);camera.transform.SetPositionAndRotation(Vector3.Lerp(start,position,t),Quaternion.Slerp(rotation,Quaternion.LookRotation(target-position),t));camera.fieldOfView=Mathf.Lerp(old,fov,t);yield return Frames(1);}
    }
    static IEnumerator FrameBoard(TMP_Text text)
    {
        // TMP's renderer bounds can still be zero on the frame its text changes.
        // Frame the stable physical board, then spotlight the freshly generated text.
        text.ForceMeshUpdate(true);
        var b=stage.BoardView.BoardBounds;var target=b.center;float fov=42;
        float distance=Mathf.Max(b.extents.y,b.extents.x/camera.aspect)/Mathf.Tan(fov*Mathf.Deg2Rad*.5f)*1.12f+b.extents.z;
        var from=camera.transform.position;var rotation=camera.transform.rotation;var to=target+Vector3.back*Mathf.Max(8,distance);var end=Quaternion.LookRotation(target-to);
        for(int i=0;i<30;i++){float t=Mathf.SmoothStep(0,1,(i+1)/30f);camera.transform.SetPositionAndRotation(Vector3.Lerp(from,to,t),Quaternion.Slerp(rotation,end,t));camera.fieldOfView=Mathf.Lerp(camera.fieldOfView,fov,t);yield return Frames(1);}
    }
    static void ResetView(){crop=Full;veil.SetHoles();hand.transform.localScale=Vector3.one*handScale;view.transform.localScale=submit.transform.localScale=Vector3.one;}
    static void Start(int index)
    {
        Time.captureFramerate=24;
        segment=index;frames=0;crop=Full;fade.color=Color.black;guides[index].Clear();
        Cue(Captions[index]);
        if(File.Exists(Output+Keys[index]+".json"))File.Delete(Output+Keys[index]+".json");
        encoder=new MediaEncoder(Path.GetFullPath(Output+Keys[index]+".mp4"),new VideoTrackAttributes {
            frameRate=new MediaRational(24),width=(uint)Screen.width,height=(uint)Screen.height,includeAlpha=false,bitRateMode=VideoBitrateMode.High});
        if(pixels==null)
        {
            pixels=new Texture2D(Screen.width,Screen.height,TextureFormat.RGBA32,false);
            zoomPixels=new Texture2D(Screen.width,Screen.height,TextureFormat.RGBA32,false);
            zoomTexture=new RenderTexture(Screen.width,Screen.height,0,RenderTextureFormat.ARGB32);zoomTexture.Create();
        }
    }
    static IEnumerator Frames(int count)
    {
        for(int i=0;i<count;i++)
        {
            yield return new WaitForEndOfFrame();
            if(encoder==null)continue;
            pixels.ReadPixels(Full,0,0);pixels.Apply();
            Texture2D frame=pixels;
            if(Mathf.Abs(crop.width-Screen.width)>1||Mathf.Abs(crop.height-Screen.height)>1)
            {
                var prior=RenderTexture.active;
                Graphics.Blit(pixels,zoomTexture,new Vector2(crop.width/Screen.width,crop.height/Screen.height),new Vector2(crop.x/Screen.width,crop.y/Screen.height));
                RenderTexture.active=zoomTexture;zoomPixels.ReadPixels(Full,0,0);zoomPixels.Apply();RenderTexture.active=prior;frame=zoomPixels;
            }
            Require(encoder.AddFrame(frame),"encode actual Game view");frames++;
        }
    }
    static IEnumerator Finish()
    {
        if(sameQuestion!=null&&segment<3)AssertSame();
        for(int i=0;i<48;i++){fade.color=new Color(0,0,0,(i+1)/48f);yield return Frames(1);}
        yield return Frames(3);encoder.Dispose();encoder=null;Time.captureFramerate=0;
        Log(Keys[segment]+": "+frames+" frames, "+(frames/24f).ToString("F2")+" seconds; 48-frame / 2-second fade plus 3 black hold frames.");
        if(segment!=0)File.WriteAllText(Output+Keys[segment]+".json",JsonUtility.ToJson(new StageHelpVideoCatalog.Recording {scene="Functions and Relations",source="Actual offline Unity Game view",cues=guides[segment].ToArray()},true));
    }
    static IEnumerator FadeIn(){for(int i=0;i<8;i++){fade.color=new Color(0,0,0,1-(i+1)/8f);yield return Frames(1);}}
    static IEnumerator Focus(Rect target,float maxZoom)
    {
        float zoom=Mathf.Max(1,Mathf.Min(maxZoom,Mathf.Min(Screen.width/(target.width+40),Screen.height/(target.height+40))));
        var size=new Vector2(Screen.width/zoom,Screen.height/zoom);var center=target.center;
        center.x=Mathf.Clamp(center.x,size.x*.5f,Screen.width-size.x*.5f);center.y=Mathf.Clamp(center.y,size.y*.5f,Screen.height-size.y*.5f);
        yield return CropTo(new Rect(center-size*.5f,size));
    }
    static IEnumerator Overview()=>CropTo(Full);
    static IEnumerator CropTo(Rect to)
    {
        var from=crop;for(int i=0;i<24;i++){float t=Mathf.SmoothStep(0,1,(i+1)/24f);crop=new Rect(Vector2.Lerp(from.position,to.position,t),Vector2.Lerp(from.size,to.size,t));yield return Frames(1);}
    }
    static IEnumerator ZoomButton(Button button,float scale)
    {
        var rt=(RectTransform)button.transform;var original=rt.localScale;
        for(int i=0;i<28;i++){rt.localScale=Vector3.Lerp(original,original*scale,Mathf.SmoothStep(0,1,(i+1)/28f));veil.SetHoles(ScreenRect(rt,14));yield return Frames(1);}yield return Frames(7);
    }
    static IEnumerator Walk(Vector3 to,float speed)
    {
        var walk=new GameObject("Recording walk target").transform;walk.position=to;Player.LocalInstance.BeginGuidedMovement(walk,speed,.16f);
        int count=0,still=0;float previous=float.MaxValue;
        while(Vector2.Distance(XZ(Player.LocalInstance.transform.position),XZ(to))>.24f&&count++<180)
        {
            yield return Frames(1);
            float distance=Vector2.Distance(XZ(Player.LocalInstance.transform.position),XZ(to));
            still=previous-distance<.01f?still+1:0;previous=distance;
            // A solid plinth can stop the capsule a few centimeters early. Trim that pause.
            if(still>=8&&distance<.65f)break;
        }
        Player.LocalInstance.EndGuidedMovement();UnityEngine.Object.Destroy(walk.gameObject);
        Require(Vector2.Distance(XZ(Player.LocalInstance.transform.position),XZ(to))<.65f,"physical walk reaches interaction approach "+to+"; player "+Player.LocalInstance.transform.position);
    }
    static Vector2 XZ(Vector3 p)=>new Vector2(p.x,p.z);
    static Vector3 Ground(Vector3 point,float ceiling)
    {
        var hits=Physics.RaycastAll(new Vector3(point.x,ceiling,point.z),Vector3.down,40,~0,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance);
        foreach(var hit in hits)if(hit.normal.y>.6f&&hit.collider.GetComponentInParent<Player>()==null&&!hit.collider.name.Contains("Ocean"))
            return new Vector3(point.x,hit.point.y+.12f,point.z);
        throw new InvalidOperationException("No solid recording approach ground at "+point);
    }
    static void Position(Vector3 at,Vector3 look)=>PrelimTeleportTransition.MovePlayerImmediately(Player.LocalInstance.gameObject,at,Quaternion.LookRotation(new Vector3(look.x-at.x,0,look.z-at.z)));
    static void Face(Vector3 target){var d=target-Player.LocalInstance.transform.position;d.y=0;Player.LocalInstance.transform.rotation=Quaternion.LookRotation(d);}
    static void SetCamera(Vector3 at,Vector3 target,float fov)
    {
        brain.enabled=false;camera.transform.SetPositionAndRotation(at,Quaternion.LookRotation(target-at));camera.fieldOfView=fov;
        worldCamera.ForceCameraPosition(camera.transform.position,camera.transform.rotation);
        var lens=worldCamera.Lens;lens.FieldOfView=fov;worldCamera.Lens=lens;
    }
    static IEnumerator Tap(RectTransform target)
    {
        Canvas.ForceUpdateCanvases();yield return Frames(1);
        Vector2 at=ScreenRect(target,0).center;
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=at},hits);
        Require(hits.Count>0&&(hits[0].gameObject.transform==target||hits[0].gameObject.transform.IsChildOf(target)),"touch raycast reaches "+target.name+" instead of "+(hits.Count>0?hits[0].gameObject.name:"nothing"));
        var rt=(RectTransform)ring.transform;rt.anchoredPosition=at*(1280f/Screen.width)-new Vector2(640,360);rt.sizeDelta=Vector2.one*58;ring.color=new Color(1,.8f,.15f,.9f);
        int id=++finger;InputSystem.QueueStateEvent(touch,new TouchState{touchId=id,phase=UnityEngine.InputSystem.TouchPhase.Began,position=at});yield return Frames(3);
        InputSystem.QueueStateEvent(touch,new TouchState{touchId=id,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=at});
        for(int i=0;i<10;i++){rt.sizeDelta=Vector2.one*(58+i*5);ring.color=new Color(1,.8f,.15f,.8f*(1-i/9f));yield return Frames(1);}
    }
    static Rect ScreenRect(RectTransform target,float padding)
    {
        var canvas=target.GetComponentInParent<Canvas>();var cam=canvas!=null&&canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:camera;
        var corners=new Vector3[4];target.GetWorldCorners(corners);var p=corners.Select(v=>RectTransformUtility.WorldToScreenPoint(cam,v)).ToArray();
        return Rect.MinMaxRect(p.Min(v=>v.x)-padding,p.Min(v=>v.y)-padding,p.Max(v=>v.x)+padding,p.Max(v=>v.y)+padding);
    }
    static Rect TextRect(TMP_Text text,float padding)
    {
        text.ForceMeshUpdate();var bounds=text.textBounds;
        var canvas=text.GetComponentInParent<Canvas>();var cam=canvas!=null&&canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:camera;
        var points=new List<Vector2>();
        for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)
            points.Add(RectTransformUtility.WorldToScreenPoint(cam,text.transform.TransformPoint(bounds.center+Vector3.Scale(bounds.extents,new Vector3(x,y,0)))));
        return Rect.MinMaxRect(points.Min(p=>p.x)-padding,points.Min(p=>p.y)-padding,points.Max(p=>p.x)+padding,points.Max(p=>p.y)+padding);
    }
    static Rect Union(Rect a,Rect b)=>Rect.MinMaxRect(Mathf.Min(a.xMin,b.xMin),Mathf.Min(a.yMin,b.yMin),Mathf.Max(a.xMax,b.xMax),Mathf.Max(a.yMax,b.yMax));
    static string ButtonText(Button b)=>b.GetComponentInChildren<TMP_Text>().text;
    static void Cue(string text){guides[segment].Add(new StageHelpVideoCatalog.Cue{seconds=frames/24f,caption=text});Log("Caption "+segment+" @ "+(frames/24f).ToString("F2")+": "+text);}
    static void AssertSame(){Require(ReferenceEquals(stage.CurrentChallenge,sameQuestion),"same question identity across all clips");}
    static void Require(bool value,string text){if(!value)throw new InvalidOperationException(text);}
    static void Log(string text)=>File.AppendAllText(Evidence,text+"\n");
    static void Snapshot(string key)
    {
        if(pixels!=null)File.WriteAllBytes("Library/FunctionsHelp_"+key+".png",(Mathf.Abs(crop.width-Screen.width)>1?zoomPixels:pixels).EncodeToPNG());
    }
    static void BuildOverlay()
    {
        overlay=new GameObject("Recording-only Functions presentation",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)).GetComponent<Canvas>();
        overlay.renderMode=RenderMode.ScreenSpaceOverlay;overlay.sortingOrder=20000;
        var scaler=overlay.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);
        veil=NewRect("Recording spotlight").gameObject.AddComponent<RecordingVeil>();veil.raycastTarget=false;veil.color=new Color(.01f,.015f,.025f,.67f);
        var rt=NewRect("Actual touch visualization");rt.anchorMin=rt.anchorMax=new Vector2(.5f,.5f);ring=rt.gameObject.AddComponent<RecordingTouchRing>();ring.raycastTarget=false;ring.color=Color.clear;
        fade=NewRect("Two-second fade").gameObject.AddComponent<Image>();fade.raycastTarget=false;fade.color=Color.black;
    }
    static RectTransform NewRect(string name)
    {
        var rt=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer)).GetComponent<RectTransform>();rt.SetParent(overlay.transform,false);
        rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;return rt;
    }
}

#endif
