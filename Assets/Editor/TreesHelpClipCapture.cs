#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Unity.Cinemachine;
using LogicLegends.Trees;

/// <summary>Two actual Game view clips. Normal touch, physics and validation; all effects are Editor-only.</summary>
public static class TreesHelpClipCapture
{
    const string Output="Library/StageHelpCaptures/", Evidence="Library/TreesHelpSequence.txt";
    public static readonly string[] Keys={"Trees","Trees_02_Board"};
    public static readonly string[] Captions={"Approach the Book Statue and tap the hand button to begin.","Read the tree question."};
    static TreeStageManager stage;static TreeChallengeData sameQuestion;
    static Camera camera;static CinemachineBrain brain;static CinemachineCamera worldCamera;
    static Button hand,view,back,submit;
    static Canvas overlay;static RecordingVeil veil;static RecordingTouchRing ring;static Image fade;
    static Touchscreen touch;static MediaEncoder encoder;
    static Texture2D pixels,zoomPixels;static RenderTexture zoomTexture;static Rect crop;
    static readonly List<StageHelpVideoCatalog.Cue>[] guides=Enumerable.Range(0,2).Select(i=>new List<StageHelpVideoCatalog.Cue>()).ToArray();
    static int frames,segment,finger;static float handScale;
    static Rect Full=>new Rect(0,0,Screen.width,Screen.height);
    public static IEnumerator Record()
    {
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
        stage=UnityEngine.Object.FindFirstObjectByType<TreeStageManager>();sameQuestion=stage.Challenge;
        hand=stage.ui.gameplayControls.transform.Find("InteractButton").GetComponent<Button>();back=stage.ui.BackButton;submit=stage.ui.SubmitButton;
        camera=Camera.main;brain=camera.GetComponent<CinemachineBrain>();worldCamera=GameObject.Find("CM_ThirdPersonCam").GetComponent<CinemachineCamera>();
        foreach(var b in UnityEngine.Object.FindObjectsByType<Behaviour>(FindObjectsSortMode.None))
            if(b is CinemachineInputAxisController||b is CinemachinePinchZoom||b is ThirdPersonCameraController)b.enabled=false;
        worldCamera.Follow=null;worldCamera.LookAt=null;brain.enabled=false;
        handScale=hand.transform.localScale.x;touch=InputSystem.AddDevice<Touchscreen>();finger=9800;
        BuildOverlay();Time.captureFramerate=24;crop=Full;
        File.WriteAllText(Evidence,"Actual offline Game view. Real physical walk, hand touch, InformationBoard node selection and Submit. No account writes.\n");
        try
        {
            Position(new Vector3(1.5f,stage.spawnPoint.position.y,-5f),stage.bookStatue.position);
            SetCamera(new Vector3(5,7,-9),stage.bookStatue.position+Vector3.up*1.2f,54);
            Start(0);yield return FadeIn();yield return Walk(stage.bookStatue.position+new Vector3(2.8f,stage.spawnPoint.position.y,-2.1f),4);
            Face(stage.bookStatue.position);yield return ZoomButton(hand,1.45f);yield return Focus(ScreenRect((RectTransform)hand.transform,12),1.55f);Snapshot("01_Hand");
            brain.enabled=true;yield return Tap((RectTransform)hand.transform);yield return Overview();ResetView();
            yield return Until(()=>stage.CanSolve,140,"hand opens live board and starts clock");AssertSame();yield return Frames(16);yield return Finish();
            Start(1);yield return FadeIn();
            veil.SetHoles(TextRect(stage.ui.QuestionText,18));yield return Focus(TextRect(stage.ui.QuestionText,28),2.4f);yield return Frames(34);Snapshot("02_Question");
            Cue("Nodes are vertices. Branches connect parents to children.");var nodeRects=stage.ui.BoardCanvas.GetComponentsInChildren<Button>().Where(b=>b.name.StartsWith("Board Node ")).Select(b=>ScreenRect((RectTransform)b.transform,24)).ToArray();
            Rect diagramRect=nodeRects[0];foreach(var nr in nodeRects.Skip(1))diagramRect=Union(diagramRect,nr);
            veil.SetHoles(diagramRect);yield return Focus(diagramRect,1.6f);yield return Frames(36);
            string answer=stage.Challenge.nodes.First(n=>TreeAnswerValidator.Validate(stage.Challenge,stage.board.Edges,n.id,null,out _)).id;
            var choice=stage.ui.BoardCanvas.GetComponentsInChildren<Button>().First(b=>b.name=="Board Node "+answer);
            Cue(stage.Challenge.identification==TreeIdentification.Root?"The root has no parent. Tap the requested node.":stage.Challenge.identification==TreeIdentification.Leaf?"A leaf has no children. Tap the requested node.":"Follow the parent–child links. Tap the requested node.");
            veil.SetHoles(ScreenRect((RectTransform)choice.transform,20));yield return Focus(Union(TextRect(stage.ui.QuestionText,15),ScreenRect((RectTransform)choice.transform,30)),1.8f);yield return Frames(25);yield return Tap((RectTransform)choice.transform);yield return Frames(18);Require(stage.board.Nodes.First(n=>n.id==answer).label.color==StageUITheme.ReadingInk,"selected label contrasts against gold node");Snapshot("02_Selected");
            Cue("Tap Submit to check. Back keeps your work.");yield return ZoomButton(submit,1.12f);yield return Focus(ScreenRect((RectTransform)submit.transform,16),3f);yield return Tap((RectTransform)submit.transform);
            Time.timeScale=0;submit.transform.localScale=Vector3.one;var result=stage.ui.BoardCanvas.GetComponentsInChildren<TMP_Text>().First(t=>t.name=="Board selection and result");veil.SetHoles(TextRect(result,20));yield return Focus(TextRect(result,30),1.8f);yield return Frames(30);Snapshot("02_Correct");yield return Finish();Time.timeScale=1;
            Require(stage.Correct==1&&stage.Mistakes==0,"one ordinary correct answer, no trial completion or reward");AssertSame();
            File.WriteAllText(Output+Keys[0]+".json",JsonUtility.ToJson(new StageHelpVideoCatalog.Recording {scene="Trees",clips=Keys,
                source="Actual isolated Unity Game view. Same question across both clips; normal hand touch, node selection and Submit. Each segment ends with an encoded two-second fade. No player progress or reward writes.",
                cues=Captions.Select(c=>new StageHelpVideoCatalog.Cue{seconds=0,caption=c}).ToArray(),guides=guides.Select(g=>new StageHelpVideoCatalog.ClipGuide{cues=g.ToArray()}).ToArray()},true));
            Log("PASS both actual gameplay clips, stable camera, mobile touch, consistent tree and question, normal validation; 48-frame fades.");
        }
        finally
        {
            encoder?.Dispose();encoder=null;Time.captureFramerate=0;Time.timeScale=1;
            if(pixels!=null)UnityEngine.Object.Destroy(pixels);if(zoomPixels!=null)UnityEngine.Object.Destroy(zoomPixels);
            if(zoomTexture!=null){zoomTexture.Release();UnityEngine.Object.Destroy(zoomTexture);}
            if(touch!=null&&touch.added)InputSystem.RemoveDevice(touch);if(overlay!=null)UnityEngine.Object.Destroy(overlay.gameObject);
        }
    }
    static void ResetView()
    {crop=Full;veil.SetHoles();hand.transform.localScale=Vector3.one*handScale;submit.transform.localScale=Vector3.one;}
    static IEnumerator Until(Func<bool> condition,int limit,string label)
    {float until=Time.realtimeSinceStartup+Mathf.Max(5,limit/12f);while(!condition()){Require(Time.realtimeSinceStartup<until,"timed out: "+label);yield return Frames(1);}}
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
            float frameBegan=Time.realtimeSinceStartup;
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
            // Keep unscaled camera/UI animations at the recorded 24 fps in the Editor.
            // This throttle is recording-only and never ships in the player build.
            float spare=1f/24-(Time.realtimeSinceStartup-frameBegan);
            if(spare>0)System.Threading.Thread.Sleep(Mathf.CeilToInt(spare*1000));
        }
    }
    static IEnumerator Finish()
    {

        for(int i=0;i<48;i++){fade.color=new Color(0,0,0,(i+1)/48f);yield return Frames(1);}
        yield return Frames(3);encoder.Dispose();encoder=null;Time.captureFramerate=0;
        Log(Keys[segment]+": "+frames+" frames, "+(frames/24f).ToString("F2")+" seconds; 48-frame / 2-second fade plus 3 black hold frames.");
        if(segment!=0)File.WriteAllText(Output+Keys[segment]+".json",JsonUtility.ToJson(new StageHelpVideoCatalog.Recording {scene="Trees",source="Actual offline Unity Game view",cues=guides[segment].ToArray()},true));
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
        var walk=new GameObject("Trees recording walk target").transform;walk.position=to;
        stage.LocalPlayer.BeginGuidedMovement(walk,speed,.20f);float until=Time.realtimeSinceStartup+30;
        while(Vector2.Distance(XZ(stage.LocalPlayer.transform.position),XZ(to))>.35f)
        {Require(Time.realtimeSinceStartup<until,"physical walk timeout at "+stage.LocalPlayer.transform.position);yield return Frames(1);}
        stage.LocalPlayer.EndGuidedMovement();UnityEngine.Object.Destroy(walk.gameObject);
        Require(Vector2.Distance(XZ(stage.LocalPlayer.transform.position),XZ(to))<.65f,"physical walk reaches "+to);
    }
    static Vector2 XZ(Vector3 p)=>new Vector2(p.x,p.z);
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
    static void AssertSame(){Require(ReferenceEquals(stage.Challenge,sameQuestion),"same question identity across all clips");}
    static void Require(bool value,string text){if(!value)throw new InvalidOperationException(text);}
    static void Log(string text)=>File.AppendAllText(Evidence,text+"\n");
    static void Snapshot(string key)
    {
        if(pixels!=null)File.WriteAllBytes("Library/TreesHelp_"+key+".png",(Mathf.Abs(crop.width-Screen.width)>1?zoomPixels:pixels).EncodeToPNG());
    }
    static void BuildOverlay()
    {
        overlay=new GameObject("Recording-only Trees presentation",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)).GetComponent<Canvas>();
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
