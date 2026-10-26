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
public static class SetsHelpClipCapture
{
    const string Output="Library/StageHelpCaptures/", Evidence="Library/SetsHelpSequence.txt";
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    public static readonly string[] Keys={"Sets","Sets_02_Read","Sets_03_Collect","Sets_04_PlaceSubmit"};
    public static readonly string[] Captions={
        "Tap the hand button at the Book Statue to begin.",
        "Read the question on the board.",
        "Collect the element you need.",
        "Place the elements in the required regions, then tap Submit."
    };
    static SetssssChallengeOne stage;
    static SetsConditionData sameQuestion;
    static SetssssBoardView board;
    static Camera camera;
    static CinemachineBrain brain;
    static CinemachineCamera worldCamera;
    static Button hand,view,back,submit;
    static Canvas overlay;static RecordingVeil veil;static RecordingTouchRing ring;static Image fade;
    static Touchscreen touch;static MediaEncoder encoder;
    static Texture2D pixels,zoomPixels;static RenderTexture zoomTexture;static Rect crop;
    static readonly List<StageHelpVideoCatalog.Cue>[] guides=Enumerable.Range(0,4).Select(i=>new List<StageHelpVideoCatalog.Cue>()).ToArray();
    static int frames,segment,finger;static float handScale;
    static T Read<T>(object owner,string key)=>(T)owner.GetType().GetField(key,Private).GetValue(owner);
    static Rect Full=>new Rect(0,0,Screen.width,Screen.height);

    public static IEnumerator Record()
    {
        stage=UnityEngine.Object.FindFirstObjectByType<SetssssChallengeOne>();board=Read<SetssssBoardView>(stage,"boardView");
        hand=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b=>b.name=="InteractButton");
        view=Read<Button>(board,"viewBoardButton");back=Read<Button>(board,"backButton");submit=Read<Button>(stage,"submitButton");
        camera=Camera.main;brain=camera.GetComponent<CinemachineBrain>();worldCamera=GameObject.Find("CM_ThirdPersonCam").GetComponent<CinemachineCamera>();
        foreach(var input in UnityEngine.Object.FindObjectsByType<CinemachineInputAxisController>(FindObjectsSortMode.None))input.enabled=false;
        foreach(var input in UnityEngine.Object.FindObjectsByType<CinemachinePinchZoom>(FindObjectsSortMode.None))input.enabled=false;
        foreach(var input in UnityEngine.Object.FindObjectsByType<ThirdPersonCameraController>(FindObjectsSortMode.None))input.enabled=false;
        var book=Read<Transform>(stage,"bookStatue");var spawner=Read<Transform>(stage,"spawner");
        foreach(var animator in book.GetComponentsInChildren<Animator>(true))animator.enabled=false;
        foreach(var r in book.GetComponentsInChildren<Renderer>(true))if(r.name=="Idle turning page")r.enabled=false;
        worldCamera.Follow=null;worldCamera.LookAt=null;brain.enabled=false;
        handScale=hand.transform.localScale.x;touch=InputSystem.AddDevice<Touchscreen>();finger=7000;
        BuildOverlay();Time.captureFramerate=24;crop=Full;
        File.WriteAllText(Evidence,"Actual offline Game view, 24 fps. One unmodified randomized C1 question across four clips. Physical guided walking, real Input System hand/View Board/Back/Submit touches, natural drop and settled-position validation. Saved rewards/progress suppressed by shared harness.\n");
        try
        {
            Position(new Vector3(book.position.x,2.55f,book.position.z-8),book.position);
            SetCamera(book.position+new Vector3(7,8,-12),book.position+Vector3.up*2,55);
            yield return Frames(4);Start(0);yield return FadeIn();yield return Walk(new Vector3(book.position.x,2.55f,book.position.z-2.6f),5.5f);Face(book.position);yield return Frames(5);
            yield return ZoomButton(hand,1.5f);Snapshot("01_Hand");yield return Tap((RectTransform)hand.transform);
            Require(stage.Started&&stage.ChallengeNumber==1&&stage.QuestionIndex==0,"actual hand starts C1 Q1");
            sameQuestion=stage.ActiveCondition;Log("Same actual question: A={"+string.Join(",",sameQuestion.setA)+"}, B={"+string.Join(",",sameQuestion.setB)+"}");
            yield return Frames(28);yield return Finish();ResetView();

            // Trim the detour around the book between clips; show only the approach relevant to View Board.
            Position(new Vector3(spawner.position.x-5,2.55f,spawner.position.z-12),spawner.position);
            SetCamera(spawner.position+new Vector3(12,12,-20),spawner.position+Vector3.up*8,56);
            Start(1);yield return FadeIn();yield return Walk(new Vector3(spawner.position.x-3,2.55f,spawner.position.z-10),5);
            yield return Frames(4);Require(view.gameObject.activeInHierarchy,"View Board appears near spawner");
            yield return ZoomButton(view,1.4f);Snapshot("02_ViewBoard");yield return Tap((RectTransform)view.transform);veil.SetHoles();
            brain.enabled=true;yield return Frames(36);Require(board.IsViewingBoard,"actual View Board opens");
            brain.enabled=false;var text=Read<TextMeshPro>(stage,"boardText");
            yield return FrameBoard(text);veil.SetHoles(TextRect(text,24));yield return Frames(86);Snapshot("02_Question");yield return Finish();ResetView();
            yield return Tap((RectTransform)back.transform);Require(!board.IsViewingBoard&&!GameInput.Instance.GameplayInputBlocked,"Back restores interaction");

            int number=int.Parse(sameQuestion.setA.Concat(sameQuestion.setB).Single());
            var block=Read<GrabbableObject[]>(stage,"numberedBlocks").Single(b=>b.GetComponent<SetssssNumberedBlock>().Value==number);
            // Enlarge only the demonstrated element in this disposable test instance.
            var numberView=block.GetComponent<SetssssNumberedBlock>();
            typeof(SetssssNumberedBlock).GetField("minimumLabelPixels",Private).SetValue(numberView,54f);
            typeof(SetssssNumberedBlock).GetField("maximumLabelPixels",Private).SetValue(numberView,90f);
            typeof(SetssssNumberedBlock).GetField("labelClearance",Private).SetValue(numberView,2.5f);
            foreach(var button in new[]{view,Read<Button>(stage,"pickUpBlockButton"),Read<Button>(stage,"returnBlockButton")})
            {var group=button.gameObject.AddComponent<CanvasGroup>();group.alpha=0;group.blocksRaycasts=false;}
            var end=Ground(block.transform.position+Vector3.back*1.8f,10);var start=end+Vector3.left*.8f;
            Position(start,block.transform.position);SetCamera(block.transform.position+new Vector3(4,14,-6),block.transform.position+Vector3.up*2,50);
            Start(2);yield return FadeIn();yield return Walk(end,1);Face(block.transform.position);yield return Frames(6);
            yield return ZoomButton(hand,1.5f);Snapshot("03_Collect");yield return Tap((RectTransform)hand.transform);
            Require(Player.LocalInstance.GetHeldObject()==block,"actual hand collects required element");yield return Frames(30);yield return Finish();ResetView();

            var region=Read<Bounds>(stage,sameQuestion.setA.Length>0?"aCircleBounds":"bCircleBounds");
            Vector3 drop=region.center+Vector3.right*(sameQuestion.setA.Length>0?-4:4);
            Player.LocalInstance.transform.rotation=Quaternion.identity;
            Vector3 offset=Player.LocalInstance.GetHoldPoint().position-Player.LocalInstance.transform.position;
            var box=block.GetComponent<Collider>();Vector3 meshOffset=Player.LocalInstance.GetHoldPoint().rotation*Vector3.Scale(((BoxCollider)box).center,block.transform.lossyScale);
            Vector3 goal=drop-offset-meshOffset;goal.y=2.55f;
            Position(goal+Vector3.back*5,goal+Vector3.forward);SetCamera(drop+new Vector3(15,24,-28),drop,53);
            Start(3);yield return FadeIn();yield return Walk(goal,4);Player.LocalInstance.transform.rotation=Quaternion.identity;yield return Frames(8);
            Require(Player.LocalInstance.GetHeldObject()==block,"same held element reaches region");
            yield return ZoomButton(hand,1.4f);Snapshot("04_Drop");var before=block.transform.position;yield return Tap((RectTransform)hand.transform);ResetView();
            Require(Player.LocalInstance.GetHeldObject()==null,"hand is the actual drop control");
            int settle=0;while(!(bool)stage.GetType().GetMethod("BlocksAreSettled",Private).Invoke(stage,null)&&settle++<180)yield return Frames(1);
            Require((bool)stage.GetType().GetMethod("BlocksAreSettled",Private).Invoke(stage,null),"natural drop settles");yield return Frames(26);Snapshot("04_Placed");
            Require(submit.gameObject.activeInHierarchy&&ButtonText(submit)=="Submit","actual Submit available");
            yield return ZoomButton(submit,1.45f);yield return Focus(ScreenRect((RectTransform)submit.transform,16),1.35f);yield return Tap((RectTransform)submit.transform);ResetView();
            yield return Frames(36);Require(stage.QuestionIndex==1&&stage.ChallengeNumber==1,"complete required placement validates once and advances to Q2");
            Require(stage.Hud.Challenge.text.Contains("Question 2 of 2"),"HUD uses real question progress");
            Snapshot("04_Validated");yield return Frames(28);yield return Finish();
            File.WriteAllText(Output+Keys[0]+".json",JsonUtility.ToJson(new StageHelpVideoCatalog.Recording{scene="Setssss",clips=Keys,source="Actual isolated Unity Game view. One normal randomized C1 question across four clips. Physical walking and Input System UI touches; no direct answer placement. Natural hand drop and real settled-position submission. Recording-only stable cameras, spotlights and zoom. Each clip ends with a 48-frame two-second black fade. Saved progress/rewards suppressed.",cues=Captions.Select(c=>new StageHelpVideoCatalog.Cue{seconds=0,caption=c}).ToArray()},true));
            Log("PASS four actual gameplay clips; same question, required placement complete, normal validation and Question 2 of 2. No progression/reward writes.");
        }
        finally
        {
            encoder?.Dispose();encoder=null;Time.captureFramerate=0;Time.timeScale=1;
            if(pixels!=null)UnityEngine.Object.Destroy(pixels);if(zoomPixels!=null)UnityEngine.Object.Destroy(zoomPixels);
            if(zoomTexture!=null){zoomTexture.Release();UnityEngine.Object.Destroy(zoomTexture);}
            if(touch!=null&&touch.added)InputSystem.RemoveDevice(touch);if(overlay!=null)UnityEngine.Object.Destroy(overlay.gameObject);
        }
    }
    static IEnumerator FrameBoard(TMP_Text text)
    {
        // TMP's renderer bounds can still be zero on the frame its text changes.
        // Frame the stable physical board, then spotlight the freshly generated text.
        text.ForceMeshUpdate(true);
        var b=GameObject.Find("Board 1").GetComponent<Renderer>().bounds;var target=b.center;float fov=42;
        float distance=Mathf.Max(b.extents.y,b.extents.x/camera.aspect)/Mathf.Tan(fov*Mathf.Deg2Rad*.5f)*1.2f;
        var from=camera.transform.position;var rotation=camera.transform.rotation;var to=target+Vector3.back*Mathf.Max(8,distance);var end=Quaternion.LookRotation(target-to);
        for(int i=0;i<30;i++){float t=Mathf.SmoothStep(0,1,(i+1)/30f);camera.transform.SetPositionAndRotation(Vector3.Lerp(from,to,t),Quaternion.Slerp(rotation,end,t));camera.fieldOfView=Mathf.Lerp(camera.fieldOfView,fov,t);yield return Frames(1);}
    }
    static void ResetView(){crop=Full;veil.SetHoles();hand.transform.localScale=Vector3.one*handScale;view.transform.localScale=submit.transform.localScale=Vector3.one;}
    static void Start(int index)
    {
        segment=index;frames=0;crop=Full;fade.color=Color.black;guides[index].Clear();
        if(index==2)Cue(Captions[index]);
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
        yield return Frames(3);encoder.Dispose();encoder=null;
        Log(Keys[segment]+": "+frames+" frames, "+(frames/24f).ToString("F2")+" seconds; 48-frame / 2-second fade plus 3 black hold frames.");
        if(segment!=0)File.WriteAllText(Output+Keys[segment]+".json",JsonUtility.ToJson(new StageHelpVideoCatalog.Recording {scene="Setssss",source="Actual offline Unity Game view",cues=new[]{new StageHelpVideoCatalog.Cue{seconds=0,caption=Captions[segment]}}},true));
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
    static void AssertSame(){Require(ReferenceEquals(stage.ActiveCondition,sameQuestion),"same question identity across all clips");}
    static void Require(bool value,string text){if(!value)throw new InvalidOperationException(text);}
    static void Log(string text)=>File.AppendAllText(Evidence,text+"\n");
    static void Snapshot(string key)
    {
        if(pixels!=null)File.WriteAllBytes("Library/SetsHelp_"+key+".png",(Mathf.Abs(crop.width-Screen.width)>1?zoomPixels:pixels).EncodeToPNG());
    }
    static void BuildOverlay()
    {
        overlay=new GameObject("Recording-only Sets presentation",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)).GetComponent<Canvas>();
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
