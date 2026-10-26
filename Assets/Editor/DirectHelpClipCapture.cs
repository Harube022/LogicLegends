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

/// <summary>One real proof classification question; recording-only stable cameras and digital close-ups.</summary>
public static class DirectHelpClipCapture
{
    const string Output="Library/StageHelpCaptures/", Evidence="Library/DirectHelpSequence.txt";
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    public static readonly string[] Keys={"DirectProofs","DirectProofs_02_Board","DirectProofs_03_Selection","DirectProofs_04_Ordering","DirectProofs_05_Submission"};
    public static readonly string[] Captions={
        "Approach the Book Statue and tap the hand button to begin.",
        "Tap View Board to read the current proof task.",
        "Approach your chosen altar and tap the hand button to select your answer.",
        "Arrange the proof steps in a logically valid order.",
        "Return to the Book Statue and tap the hand button to submit your answer."
    };
    static DirectIndirectProofStageController stage;
    static DirectProofsWorldGuidance ui;
    static TrialProofChallenge sameQuestion;
    static Camera camera;
    static CinemachineBrain brain;
    static CinemachineCamera worldCamera;
    static Button hand,view;
    static Canvas overlay;
    static RecordingVeil veil;
    static RecordingTouchRing ring;
    static Image fade;
    static Touchscreen touch;
    static MediaEncoder encoder;
    static Texture2D pixels,zoomPixels;
    static RenderTexture zoomTexture;
    static Rect crop;
    static readonly List<StageHelpVideoCatalog.Cue>[] guides=Enumerable.Range(0,5).Select(i=>new List<StageHelpVideoCatalog.Cue>()).ToArray();
    static int frames,segment,finger;
    static float handScale;
    static TMP_Text exampleBadge;
    static TrialProofChallenge Current => Read<TrialProofChallenge>(stage,"activeChallenge");
    static TMP_Text BoardText => stage.InformationBoard.GetComponent<TMP_Text>();
    static T Read<T>(object owner,string key)=>(T)owner.GetType().GetField(key,Private).GetValue(owner);
    static Rect Full=>new Rect(0,0,Screen.width,Screen.height);

    public static IEnumerator Record()
    {
        stage=UnityEngine.Object.FindFirstObjectByType<DirectIndirectProofStageController>();ui=stage.WorldGuidance;
        var book=stage.Book;
        foreach(var animator in book.GetComponentsInChildren<Animator>(true))animator.enabled=false;
        foreach(var renderer in book.GetComponentsInChildren<Renderer>(true))if(renderer.name=="Idle turning page")renderer.enabled=false;
        hand=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b=>b.name=="InteractButton");view=ui.ViewButton;
        camera=Camera.main;brain=camera.GetComponent<CinemachineBrain>();worldCamera=brain.ActiveVirtualCamera as CinemachineCamera;
        Require(worldCamera!=null,"gameplay virtual camera is available");
        foreach(var input in UnityEngine.Object.FindObjectsByType<CinemachineInputAxisController>(FindObjectsSortMode.None))input.enabled=false;
        foreach(var input in UnityEngine.Object.FindObjectsByType<CinemachinePinchZoom>(FindObjectsSortMode.None))input.enabled=false;
        foreach(var input in UnityEngine.Object.FindObjectsByType<ThirdPersonCameraController>(FindObjectsSortMode.None))input.enabled=false;
        worldCamera.Follow=null;worldCamera.LookAt=null;brain.enabled=false;
        handScale=hand.transform.localScale.x;touch=InputSystem.AddDevice<Touchscreen>();finger=1800;
        BuildOverlay();Time.captureFramerate=24;crop=Full;
        File.WriteAllText(Evidence,"Actual Game view; offline test transport; no saved progression or rewards. 24fps; real physical walking and Input System touches. Clips 1,2,3,5 share a randomized Challenge 1 question. Clip 4 is explicitly labeled as a separate Challenge 5 ordering example, reached through normal offline gameplay.\n");
        try
        {
            Position(Ground(book.position+new Vector3(1,0,7),8),book.position);
            SetCamera(book.position+new Vector3(5,6,10),book.position+Vector3.up*1.8f,57);
            yield return Frames(4);Start(0);yield return FadeIn();
            yield return Walk(book.position+Vector3.forward*2,4.8f);Face(book.position);yield return Frames(8);
            Require(stage.State==DirectIndirectProofStageController.TrialState.WaitingForActivation&&!stage.GuidanceBlocksGameplay,"normal waiting book");
            yield return ZoomButton(hand,1.5f);Snapshot("01_Hand");yield return Tap((RectTransform)hand.transform);
            Require(stage.State==DirectIndirectProofStageController.TrialState.Active&&stage.ChallengeNumber==1&&stage.CorrectCount==0,"hand starts first question");
            sameQuestion=Current;Log("Shared question: "+sameQuestion.title+"; "+sameQuestion.problem+"; correct method "+sameQuestion.correctMethod);
            yield return Frames(36);yield return Finish();hand.transform.localScale=Vector3.one*handScale;veil.SetHoles();

            // Cut only under black. Walk into the real main-board interaction area.
            Position(Ground(new Vector3(56.7f,0,58),8),new Vector3(56.7f,0,48));
            SetCamera(new Vector3(65,9,64),new Vector3(56.7f,4,43),59);
            Start(1);yield return FadeIn();yield return Walk(new Vector3(56.7f,1.6f,49),5.5f);yield return Frames(5);
            Require(view.gameObject.activeInHierarchy,"View Board appears inside its actual interaction area");
            yield return ZoomButton(view,1.45f);Snapshot("02_ViewBoard");yield return OpenBoard();view.transform.localScale=Vector3.one;
            veil.SetHoles(TextRect(BoardText,24));yield return Focus(TextRect(BoardText,32),1.5f);
            yield return Frames(144);Snapshot("02_Board");yield return Finish();
            crop=Full;veil.SetHoles();yield return Tap((RectTransform)ui.BackButton.transform);
            while(ui.BoardView.Viewing)yield return Frames(1);
            Require(stage.CorrectCount==0,"board reading never submits");

            int target=sameQuestion.correctMethod==TrialProofMethod.Direct?0:1;Vector3 altar=AltarCenter(target);
            Position(Ground(altar+new Vector3(0,0,9),8),altar);
            SetCamera(new Vector3(56.7f,11,66),new Vector3(56.7f,3.6f,45.5f),61);
            yield return Frames(8);Start(2);yield return FadeIn();
            Require(ui.Arrows.Take(2).All(a=>a.gameObject.activeSelf),"both choices show arrows before any selection");Snapshot("03_AllChoices");
            yield return Walk(altar+Vector3.forward*3.7f,5);Face(altar);yield return Frames(5);
            yield return ZoomButton(hand,1.5f);yield return Tap((RectTransform)hand.transform);
            hand.transform.localScale=Vector3.one*handScale;veil.SetHoles();
            Require(stage.SelectedMethod==(int)sameQuestion.correctMethod&&stage.CorrectCount==0,"hand selects a method without submitting");
            yield return Frames(12);Require(ui.Arrows.All(a=>!a.gameObject.activeSelf),"all selection arrows disappear after the chosen altar");
            Snapshot("03_AllArrowsHidden");yield return Frames(48);yield return Finish();

            // Record the matching submission before advancing this same question.
            Position(Ground(book.position+new Vector3(0,0,-7),8),book.position);
            SetCamera(book.position+new Vector3(6,6,10),book.position+Vector3.up*1.4f,58);
            Start(4);yield return FadeIn();yield return Walk(book.position+Vector3.back*2,5);Face(book.position);yield return Frames(5);
            yield return ZoomButton(hand,1.5f);Snapshot("05_SubmitHand");yield return Tap((RectTransform)hand.transform);
            Require(stage.CorrectCount==1&&stage.State==DirectIndirectProofStageController.TrialState.ChallengeComplete&&BoardText.text.Contains("COMPLETE"),"normal book hand validates once and shows actual feedback");
            hand.transform.localScale=Vector3.one*handScale;veil.SetHoles();Time.timeScale=0;
            // Keep the actual success feedback readable before its normal .9s next-question reset.
            yield return MoveCamera(new Vector3(56.7f,8,45),new Vector3(56.7f,7.5f,32),52,30);
            veil.SetHoles(TextRect(BoardText,24));yield return Focus(TextRect(BoardText,32),1.6f);
            yield return Frames(132);Snapshot("05_Feedback");yield return Finish();
            AssertSame();sameQuestion=null;crop=Full;veil.SetHoles();Time.timeScale=1;yield return Frames(32);

            // Reach the separate ordering example using normal real UI actions, off camera.
            while(stage.ChallengeNumber<5)
            {
                yield return Ready();
                Position(Ground(book.position+Vector3.forward*2,8),book.position);yield return Frames(2);yield return Tap((RectTransform)hand.transform);
                Require(stage.State==DirectIndirectProofStageController.TrialState.Active,"offline preparation starts actual randomized question");
                int at=stage.ChallengeNumber<=2?0:stage.ChallengeNumber==3?1:2;
                if(stage.ChallengeNumber==1)at=Current.correctMethod==TrialProofMethod.Direct?0:1;
                Position(Ground(AltarCenter(at)+Vector3.forward*3.7f,8),AltarCenter(at));yield return Frames(2);yield return Tap((RectTransform)hand.transform);
                if(stage.ChallengeNumber>1){yield return Frames(10);yield return Arrange();yield return CloseOrdering();}
                Position(Ground(book.position+Vector3.forward*2,8),book.position);yield return Frames(2);int before=stage.CorrectCount;yield return Tap((RectTransform)hand.transform);
                Require(stage.CorrectCount==before+1,"normal preparation proof validated");yield return Frames(30);
            }
            yield return Ready();Position(Ground(book.position+Vector3.forward*2,8),book.position);yield return Frames(2);yield return Tap((RectTransform)hand.transform);
            Require(stage.ChallengeNumber==5&&Current.kind==TrialProofKind.ChooseAndArrange,"Challenge 5 combines strategy selection with proof ordering; Challenge 4 uses the same ordering surface");
            Log("Separate ordering example: "+Current.title+"; "+Current.problem);sameQuestion=Current;
            altar=AltarCenter(2);Position(Ground(altar+Vector3.forward*9,8),altar);
            SetCamera(new Vector3(64,9,59),altar+Vector3.up*2,59);
            exampleBadge.text="SEPARATE EXAMPLE · CHALLENGE 5\nChoose a strategy and arrange the proof";exampleBadge.gameObject.SetActive(true);
            Start(3);yield return FadeIn();yield return Walk(altar+Vector3.forward*3.7f,5);Face(altar);yield return Frames(5);
            yield return ZoomButton(hand,1.5f);yield return Tap((RectTransform)hand.transform);hand.transform.localScale=Vector3.one*handScale;veil.SetHoles();
            yield return Frames(15);Require(stage.OrderingBoard.IsOpen,"actual hand opens proof ordering interface");
            veil.SetHoles(ScreenRect(stage.OrderingBoard.Panel,12));yield return Frames(48);Snapshot("04_Ordering");
            yield return Arrange();yield return Frames(48);Snapshot("04_Arranged");
            yield return CloseOrdering();veil.SetHoles();yield return Frames(30);yield return Finish();
            Require(!stage.Finished&&stage.CorrectCount==8&&stage.MistakeCount==0,"recording never completes the trial or awards coins");
            Log("PASS: normal hand start/selection/submission, actual View Board and touch swaps. All arrows disappear on selection. Separate labeled Challenge 5 example. Offline account writes disabled.");
            File.WriteAllText(Output+Keys[0]+".json",JsonUtility.ToJson(new StageHelpVideoCatalog.Recording {scene="Direct&Indirect",clips=Keys,
                source="Actual offline Unity Game view. Clips 1,2,3,5: same randomized Challenge 1 question. Clip 4: labeled separate Challenge 5 strategy/ordering example reached through normal UI gameplay. Physical walks, Input System touch and drag; recording-only stable camera/spotlights; 48-frame two-second fade per clip; persistence and rewards suppressed.",
                cues=Captions.Select(c=>new StageHelpVideoCatalog.Cue{seconds=0,caption=c}).ToArray()},true));
        }
        finally
        {
            encoder?.Dispose();encoder=null;Time.captureFramerate=0;Time.timeScale=1;
            if(pixels!=null)UnityEngine.Object.Destroy(pixels);if(zoomPixels!=null)UnityEngine.Object.Destroy(zoomPixels);
            if(zoomTexture!=null){zoomTexture.Release();UnityEngine.Object.Destroy(zoomTexture);}
            if(touch!=null&&touch.added)InputSystem.RemoveDevice(touch);
            if(overlay!=null)UnityEngine.Object.Destroy(overlay.gameObject);
        }
    }
    static IEnumerator Ready()
    {
        if(stage.Guide.IsIntroduction){stage.Guide.Skip();yield return Frames(24);}
        Require(stage.State==DirectIndirectProofStageController.TrialState.WaitingForActivation&&!stage.GuidanceBlocksGameplay,"next offline question waits for book");
    }
    static Vector3 AltarCenter(int target)=>(Vector3)typeof(DirectIndirectProofStageController).GetMethod("AltarCenter",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{stage.Altars[target]});
    static IEnumerator CloseOrdering()
    {
        yield return Tap((RectTransform)stage.OrderingBoard.Panel.GetComponentsInChildren<Button>().Single(b=>b.name=="Close Altar Board").transform);
        Require(!stage.OrderingBoard.IsOpen,"Back to Room preserves arranged draft");
    }
    static IEnumerator Arrange()
    {
        var order=Current.acceptedOrders[0].tabletIds;
        if(Current.kind==TrialProofKind.ChooseAndArrange)
        {
            var strategies=Read<Button[]>(stage.OrderingBoard,"strategyButtons");
            if(encoder!=null)veil.SetHoles(ScreenRect((RectTransform)strategies[(int)Current.correctMethod].transform,12));
            yield return Tap((RectTransform)strategies[(int)Current.correctMethod].transform);
        }
        for(int row=0;row<order.Length;row++)
        {
            int from=Array.IndexOf(stage.OrderingBoard.CurrentOrder,order[row]);
            if(from!=row)yield return Drag(from,row);
        }
        if(!Read<bool>(stage,"stepsReordered")){yield return Drag(0,1);yield return Drag(0,1);}
        Require(Current.IsCorrect(stage.SelectedMethod,stage.OrderingBoard.CurrentOrder),"actual UI drag makes a valid proof");
        if(encoder!=null)veil.SetHoles(ScreenRect(stage.OrderingBoard.Panel,12));
    }
    static IEnumerator Drag(int from,int to)
    {
        var cards=Read<ProofStepDragCard[]>(stage.OrderingBoard,"cards");
        Vector2 start=ScreenRect((RectTransform)cards[from].transform,0).center,end=ScreenRect((RectTransform)cards[to].transform,0).center;
        if(encoder!=null){veil.SetHoles(Union(ScreenRect((RectTransform)cards[from].transform,10),ScreenRect((RectTransform)cards[to].transform,10)));yield return Frames(18);}
        var rt=(RectTransform)ring.transform;int id=++finger;
        InputSystem.QueueStateEvent(touch,new TouchState{touchId=id,phase=UnityEngine.InputSystem.TouchPhase.Began,position=start});yield return Frames(3);
        for(int i=1;i<=18;i++)
        {
            Vector2 at=Vector2.Lerp(start,end,i/18f);rt.anchoredPosition=at*(1280f/Screen.width)-new Vector2(640,360);rt.sizeDelta=Vector2.one*54;ring.color=new Color(1,.8f,.15f,.9f);
            InputSystem.QueueStateEvent(touch,new TouchState{touchId=id,phase=UnityEngine.InputSystem.TouchPhase.Moved,position=at});yield return Frames(1);
        }
        InputSystem.QueueStateEvent(touch,new TouchState{touchId=id,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=end});yield return Frames(8);ring.color=Color.clear;
    }
    static IEnumerator MoveCamera(Vector3 to,Vector3 look,float fov,int count)
    {
        var from=camera.transform.position;var rotation=camera.transform.rotation;var end=Quaternion.LookRotation(look-to);float initial=camera.fieldOfView;
        for(int i=0;i<count;i++){float t=Mathf.SmoothStep(0,1,(i+1)/(float)count);camera.transform.SetPositionAndRotation(Vector3.Lerp(from,to,t),Quaternion.Slerp(rotation,end,t));camera.fieldOfView=Mathf.Lerp(initial,fov,t);yield return Frames(1);}
    }
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
        if(sameQuestion!=null)AssertSame();
        for(int i=0;i<48;i++){fade.color=new Color(0,0,0,(i+1)/48f);yield return Frames(1);}
        yield return Frames(3);encoder.Dispose();encoder=null;
        Log(Keys[segment]+": "+frames+" frames, "+(frames/24f).ToString("F2")+" seconds; 48-frame / 2-second fade plus 3 black hold frames.");
        if(segment!=0)File.WriteAllText(Output+Keys[segment]+".json",JsonUtility.ToJson(new StageHelpVideoCatalog.Recording {scene="Direct&Indirect",source="Actual offline Unity Game view",cues=new[]{new StageHelpVideoCatalog.Cue{seconds=0,caption=Captions[segment]}}},true));
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
    static IEnumerator OpenBoard()
    {
        yield return Tap((RectTransform)view.transform);veil.SetHoles();
        int count=0;while((!ui.BoardView.Viewing||ui.BoardView.Transitioning)&&count++<180)yield return Frames(1);
        Require(ui.BoardView.Viewing&&!ui.BoardView.Transitioning,"normal View Board camera completes its smooth readable framing");
    }
    static IEnumerator Walk(Vector3 to,float speed)
    {
        var walk=new GameObject("Recording walk target").transform;walk.position=to;Player.LocalInstance.BeginGuidedMovement(walk,speed,.16f);
        int count=0;while(Vector2.Distance(XZ(Player.LocalInstance.transform.position),XZ(to))>.24f&&count++<240)yield return Frames(1);
        Player.LocalInstance.EndGuidedMovement();UnityEngine.Object.Destroy(walk.gameObject);
        Require(Vector2.Distance(XZ(Player.LocalInstance.transform.position),XZ(to))<.45f,"physical walk reaches "+to+"; player "+Player.LocalInstance.transform.position);
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
    static void AssertSame(){Require(ReferenceEquals(Read<TrialProofChallenge>(stage,"activeChallenge"),sameQuestion),"same question identity across all clips");}
    static void Require(bool value,string text){if(!value)throw new InvalidOperationException(text);}
    static void Log(string text)=>File.AppendAllText(Evidence,text+"\n");
    static void Snapshot(string key)
    {
        if(pixels!=null)File.WriteAllBytes("Library/DirectHelp_"+key+".png",(Mathf.Abs(crop.width-Screen.width)>1?zoomPixels:pixels).EncodeToPNG());
    }
    static void BuildOverlay()
    {
        overlay=new GameObject("Recording-only direct proof presentation",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)).GetComponent<Canvas>();
        overlay.renderMode=RenderMode.ScreenSpaceOverlay;overlay.sortingOrder=20000;
        var scaler=overlay.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);
        veil=NewRect("Recording spotlight").gameObject.AddComponent<RecordingVeil>();veil.raycastTarget=false;veil.color=new Color(.01f,.015f,.025f,.67f);
        var rt=NewRect("Actual touch visualization");rt.anchorMin=rt.anchorMax=new Vector2(.5f,.5f);ring=rt.gameObject.AddComponent<RecordingTouchRing>();ring.raycastTarget=false;ring.color=Color.clear;
        exampleBadge=NewRect("Separate proof-ordering example").gameObject.AddComponent<TextMeshProUGUI>();
        exampleBadge.raycastTarget=false;exampleBadge.font=StageUITheme.ReadingFont;exampleBadge.fontSize=26;exampleBadge.fontStyle=FontStyles.Bold;
        exampleBadge.alignment=TextAlignmentOptions.TopLeft;exampleBadge.color=new Color(1,.88f,.42f);exampleBadge.outlineWidth=.2f;exampleBadge.outlineColor=Color.black;
        exampleBadge.rectTransform.anchorMin=exampleBadge.rectTransform.anchorMax=new Vector2(0,1);exampleBadge.rectTransform.pivot=new Vector2(0,1);
        exampleBadge.rectTransform.anchoredPosition=new Vector2(28,-18);exampleBadge.rectTransform.sizeDelta=new Vector2(810,85);exampleBadge.gameObject.SetActive(false);
        fade=NewRect("Two-second fade").gameObject.AddComponent<Image>();fade.raycastTarget=false;fade.color=Color.black;
    }
    static RectTransform NewRect(string name)
    {
        var rt=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer)).GetComponent<RectTransform>();rt.SetParent(overlay.transform,false);
        rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;return rt;
    }
}

#endif
