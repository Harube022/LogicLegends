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
using LogicLegends.Graph;

/// <summary>Four actual Game view clips. Normal touch, physics and validation; all effects are Editor-only.</summary>
public static class GraphHelpClipCapture
{
    const string Output="Library/StageHelpCaptures/", Evidence="Library/GraphHelpSequence.txt";
    public static readonly string[] Keys={"Graph","Graph_02_Read","Graph_03_Route","Graph_04_Degree"};
    public static readonly string[] Captions={
        "Tap the hand at the Book Statue to begin.",
        "Tap View Board to read the route task.",
        "Follow connected bridges. Confirm at the destination.",
        "Challenge 2: count the edges meeting the named vertex."
    };
    static GraphStageManager stage;static GraphChallengeData sameQuestion;
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
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
        stage=UnityEngine.Object.FindFirstObjectByType<GraphStageManager>();
        hand=stage.ui.host.Find("Gameplay_Interface/InteractButton").GetComponent<Button>();view=stage.BoardView.ViewButton;back=stage.BoardView.BackButton;
        submit=Button("Confirm");camera=Camera.main;brain=camera.GetComponent<CinemachineBrain>();worldCamera=GameObject.Find("CM_ThirdPersonCam").GetComponent<CinemachineCamera>();
        foreach(var b in UnityEngine.Object.FindObjectsByType<Behaviour>(FindObjectsSortMode.None))
            if(b is CinemachineInputAxisController||b is CinemachinePinchZoom||b is ThirdPersonCameraController)b.enabled=false;
        worldCamera.Follow=null;worldCamera.LookAt=null;brain.enabled=false;
        handScale=hand.transform.localScale.x;touch=InputSystem.AddDevice<Touchscreen>();finger=9700;
        BuildOverlay();Time.captureFramerate=24;crop=Full;
        File.WriteAllText(Evidence,"Actual offline Unity Game view. Normal hand/UI Input System touch, physical crossings, Retry Route, Confirm and degree selection. No Admin clearing or progress/reward writes.\n");
        try
        {
            sameQuestion=stage.Challenge;var start=stage.arena.TransformPoint(sameQuestion.Vertex(sameQuestion.start).position);
            Position(start+new Vector3(3,stage.spawnPoint.position.y-start.y,3),stage.bookStatue.position);
            SetCamera(start+new Vector3(15,15,-22),start+Vector3.up*2,55);
            Start(0);yield return FadeIn();yield return Walk(start+new Vector3(-.5f,.6f,-.5f),5);
            Face(stage.bookStatue.position);yield return ZoomButton(hand,1.45f);yield return Focus(ScreenRect((RectTransform)hand.transform,12),1.55f);Snapshot("01_Hand");
            yield return Tap((RectTransform)hand.transform);yield return Overview();yield return Until(()=>stage.State==GraphStageState.ACTIVE&&!stage.Moving,120,"normal book activation");
            AssertSame();Require(stage.GraphRevealed,"book reveals current graph");
            ResetView();yield return Frames(28);yield return Finish();ResetView();
            Log("C1 example: "+sameQuestion.objective+"; source="+stage.ActiveVariantIndex);

            SetCamera(start+new Vector3(12,14,-22),start+Vector3.up*2,54);
            Start(1);yield return FadeIn();yield return Walk(start+new Vector3(0,.6f,-4),5);Face(stage.BoardView.board.position);
            yield return ZoomButton(view,1.4f);yield return Focus(ScreenRect((RectTransform)view.transform,12),1.6f);brain.enabled=true;yield return Tap((RectTransform)view.transform);veil.SetHoles();yield return Overview();
            yield return Until(()=>stage.BoardView.IsOpen,35,"board opens without submitting");yield return Frames(55);
            veil.SetHoles(TextRect(stage.BoardView.instructions,18),TextRect(stage.BoardView.heading,12));
            Snapshot("02_Board");yield return Frames(65);AssertSame();Require(stage.Correct==0,"reading keeps progress unchanged");
            yield return Finish();ResetView();yield return Tap((RectTransform)back.transform);Log("Back requested: open="+stage.BoardView.IsOpen+", interactable="+back.interactable);
            yield return Until(()=>!stage.BoardView.IsOpen,100,"Back returns to gameplay");

            yield return Tap((RectTransform)Button("Retry").transform);
            yield return Until(()=>!stage.Moving,140,"staging Retry restores the start without discontinuous movement");
            RouteCamera(sameQuestion);Start(2);yield return FadeIn();
            var destination=stage.arena.TransformPoint(sameQuestion.Vertex(sameQuestion.destination).position);
            yield return Walk(Vector3.Lerp(start,destination,.35f)+Vector3.up,8);
            Cue("Retry Route returns to the start and clears your route.");
            var retry=Button("Retry");yield return ZoomButton(retry,1.25f);yield return Focus(ScreenRect((RectTransform)retry.transform,12),1.9f);yield return Tap((RectTransform)retry.transform);yield return Overview();ResetView();
            yield return Until(()=>!stage.Moving,130,"actual Retry Route finishes");
            Require(stage.Tracker.Route.Count==1,"Retry clears recorded route");AssertSame();
            Cue("Follow the required route. Confirm at its destination.");
            foreach(var id in GraphAnswerValidator.FindSolution(sameQuestion).Skip(1))yield return Walk(stage.arena.TransformPoint(sameQuestion.Vertex(id).position)+Vector3.up,8);
            Require(GraphAnswerValidator.Validate(sameQuestion,stage.Tracker.Route,null,stage.Tracker.IsContinuous&&stage.Tracker.AtVertex,out _),"actual full route is valid");
            Snapshot("03_Route");yield return ZoomButton(submit,1.25f);yield return Focus(ScreenRect((RectTransform)submit.transform,12),1.9f);yield return Tap((RectTransform)submit.transform);
            Time.timeScale=0;yield return Overview();ResetView();yield return Frames(14);Snapshot("03_Correct");yield return Finish();Time.timeScale=1;ResetView();
            yield return Until(()=>stage.State==GraphStageState.WAITING_FOR_ACTIVATION&&!stage.Moving,180,"question two prepared");
            Require(stage.Correct==1&&stage.ChallengeIndex==0&&stage.QuestionIndex==1,"normal Q1 advances to Q2 only");

            // Trim repetition outside the clips, but complete Q2 through normal walking and confirmation.
            yield return Tap((RectTransform)hand.transform);yield return Until(()=>stage.State==GraphStageState.ACTIVE&&!stage.Moving,140,"C1 Q2 activation");
            RouteCamera(stage.Challenge);
            foreach(var id in GraphAnswerValidator.FindSolution(stage.Challenge).Skip(1))yield return Walk(stage.arena.TransformPoint(stage.Challenge.Vertex(id).position)+Vector3.up,8);
            yield return Tap((RectTransform)submit.transform);
            yield return Until(()=>stage.ChallengeIndex==1&&stage.State==GraphStageState.WAITING_FOR_ACTIVATION,200,"C2 preparation");
            yield return Until(()=>stage.Introductions.IsOpen,100,"C2 introduction");stage.Introductions.Finish();
            yield return Until(()=>!stage.Introductions.IsOpen,100,"C2 intro cleanup");
            sameQuestion=stage.Challenge;start=stage.arena.TransformPoint(sameQuestion.Vertex(sameQuestion.start).position);
            SetCamera(start+new Vector3(0,18,-24),start+Vector3.up*3,55);brain.enabled=true;
            Start(3);yield return FadeIn();yield return Tap((RectTransform)hand.transform);
            yield return Until(()=>stage.QuestionView.IsOpen,160,"degree mode begins");
            yield return Until(()=>!stage.ui.DegreeQuestionEntering,120,"actual question finishes center-to-top entrance");yield return Frames(20);
            veil.SetHoles(ScreenRect(stage.ui.TutorialTarget("Objective"),12));
            yield return Focus(ScreenRect(stage.ui.TutorialTarget("Objective"),12),1.85f);yield return Frames(22);Snapshot("04_Question");
            yield return Overview();veil.SetHoles();yield return Frames(22);
            var numberArea=ScreenRect(stage.ui.TutorialTarget("Degree choices"),12);
            var actionArea=ScreenRect((RectTransform)submit.transform,12);
            var rightArea=ScreenRect((RectTransform)view.transform,12);
            var answerArea=Rect.MinMaxRect(Mathf.Min(numberArea.xMin,actionArea.xMin),actionArea.yMin,Mathf.Max(numberArea.xMax,rightArea.xMax),numberArea.yMax);
            veil.SetHoles(answerArea);yield return Focus(answerArea,1.9f);
            int expected=sameQuestion.Degree(sameQuestion.degreeVertex),other=(expected+1)%7;
            Cue("Choose a number. Clear Selection lets you change it.");
            var option=Button("Answer "+other);yield return ZoomButton(option,1.3f);yield return Tap((RectTransform)option.transform);option.transform.localScale=Vector3.one;veil.SetHoles(answerArea);
            Require(stage.Answer==other,"first number selected without submitting");
            yield return ZoomButton(Button("Retry"),1.2f);yield return Tap((RectTransform)Button("Retry").transform);Button("Retry").transform.localScale=Vector3.one;veil.SetHoles(answerArea);
            Require(!stage.Answer.HasValue,"Clear Selection removes only number");
            option=Button("Answer "+expected);yield return ZoomButton(option,1.3f);yield return Tap((RectTransform)option.transform);option.transform.localScale=Vector3.one;veil.SetHoles(answerArea);
            Require(stage.Answer==expected,"correct degree selected");Snapshot("04_Selected");
            Cue("Tap Confirm to check your selected degree.");yield return ZoomButton(submit,1.2f);yield return Tap((RectTransform)submit.transform);
            Time.timeScale=0;yield return Overview();ResetView();yield return Frames(14);Snapshot("04_Correct");yield return Finish();Time.timeScale=1;
            Require(stage.Correct==3&&stage.Mistakes==0,"three normal correct answers; no trial completion reward");
            File.WriteAllText(Output+Keys[0]+".json",JsonUtility.ToJson(new StageHelpVideoCatalog.Recording{scene="Graph",clips=Keys,
                source="Actual isolated Unity Game view. C1 Q1 consistent across Start/Read/Route; separately labeled C2 degree example. Real touches, normal traversal, Retry, Clear Selection and validation. Every clip ends with an encoded two-second fade. No saved progress or rewards changed.",
                cues=Captions.Select(c=>new StageHelpVideoCatalog.Cue{seconds=0,caption=c}).ToArray(),guides=guides.Select(g=>new StageHelpVideoCatalog.ClipGuide{cues=g.ToArray()}).ToArray()},true));
            Log("PASS four actual clips; normal C1 route and retry, same task, board and Back, C2 entrance/number change/Clear/Confirm, no mistakes or account writes.");
        }
        finally
        {
            encoder?.Dispose();encoder=null;Time.captureFramerate=0;Time.timeScale=1;
            if(pixels!=null)UnityEngine.Object.Destroy(pixels);if(zoomPixels!=null)UnityEngine.Object.Destroy(zoomPixels);
            if(zoomTexture!=null){zoomTexture.Release();UnityEngine.Object.Destroy(zoomTexture);}
            if(touch!=null&&touch.added)InputSystem.RemoveDevice(touch);if(overlay!=null)UnityEngine.Object.Destroy(overlay.gameObject);
        }
    }
    static void RouteCamera(GraphChallengeData graph)
    {
        var bounds=new Bounds(stage.arena.TransformPoint(graph.vertices[0].position),Vector3.zero);
        foreach(var v in graph.vertices)bounds.Encapsulate(stage.arena.TransformPoint(v.position));
        var target=bounds.center+Vector3.up*2;
        float distance=Mathf.Max(38,bounds.size.magnitude*.9f);
        SetCamera(target+new Vector3(distance*.65f,distance,-distance*.85f),target,53);
    }
    static Button Button(string name)=>stage.ui.host.GetComponentsInChildren<Button>(true).First(b=>b.name==name);
    static void ResetView()
    {
        crop=Full;veil.SetHoles();hand.transform.localScale=Vector3.one*handScale;
        foreach(var b in stage.ui.host.GetComponentsInChildren<Button>(true))if(b.name=="Retry"||b.name=="Confirm"||b.name=="View Board")b.transform.localScale=Vector3.one;
    }
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
        if(segment!=0)File.WriteAllText(Output+Keys[segment]+".json",JsonUtility.ToJson(new StageHelpVideoCatalog.Recording {scene="Graph",source="Actual offline Unity Game view",cues=guides[segment].ToArray()},true));
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
        var walk=new GameObject("Graph recording walk target").transform;walk.position=to;
        stage.LocalPlayer.BeginGuidedMovement(walk,speed,.20f);float until=Time.realtimeSinceStartup+30;
        while(Vector2.Distance(XZ(stage.LocalPlayer.transform.position),XZ(to))>.35f)
        {Require(Time.realtimeSinceStartup<until,"physical walk timeout at "+stage.LocalPlayer.transform.position);Require(stage.Tracker.IsContinuous,"continuous physical crossing: "+stage.Tracker.InterruptionReason);yield return Frames(1);}
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
        if(pixels!=null)File.WriteAllBytes("Library/GraphHelp_"+key+".png",(Mathf.Abs(crop.width-Screen.width)>1?zoomPixels:pixels).EncodeToPNG());
    }
    static void BuildOverlay()
    {
        overlay=new GameObject("Recording-only Graph presentation",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)).GetComponent<Canvas>();
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
