#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LogicLegends.Induction;
using TMPro;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Unity.Cinemachine;

/// <summary>Six actual gameplay recordings. All cameras, zooms and spotlights exist only in this Editor session.</summary>
public static class InductionHelpClipCapture
{
    const string Output="Library/StageHelpCaptures/", Evidence="Library/InductionHelpSequence.txt";
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    public static readonly string[] Keys={"Induction","Induction_02_Board","Induction_03_Collect","Induction_04_ReturnAndPlace","Induction_05_FullProof","Induction_06_Submit"};
    public static readonly string[] Captions={
        "Approach the Book Statue and tap the hand button to start.",
        "Tap View Board to read the current proof task.",
        "Choose a formula scroll that helps complete the proof.",
        "Follow the arrow to place your scroll, or tap Return Scroll to return it.",
        "Complete the base case, inductive hypothesis, and inductive step to build the full proof.",
        "Tap Submit to check your complete answer."
    };
    static InductionStageManager stage;
    static InductionUI ui;
    static InductionChallenge sameQuestion;
    static Camera camera;
    static CinemachineBrain brain;
    static CinemachineCamera worldCamera;
    static Button hand,view;
    static Canvas overlay;
    static RecordingVeil veil;
    static RecordingTouchRing ring;
    static Image fade;
    static TMP_Text exampleBadge;
    static Touchscreen touch;
    static MediaEncoder encoder;
    static Texture2D pixels,zoomPixels;
    static RenderTexture zoomTexture;
    static Rect crop;
    static readonly List<StageHelpVideoCatalog.Cue>[] guides=Enumerable.Range(0,6).Select(i=>new List<StageHelpVideoCatalog.Cue>()).ToArray();
    static int frames,segment,finger;
    static float handScale;
    static T Read<T>(object owner,string key)=>(T)owner.GetType().GetField(key,Private).GetValue(owner);
    static Rect Full=>new Rect(0,0,Screen.width,Screen.height);

    public static IEnumerator Record()
    {
        stage=InductionStageManager.Instance;ui=stage.ui;
        hand=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b=>b.name=="InteractButton");view=ui.ViewBoardButton;
        camera=Camera.main;brain=camera.GetComponent<CinemachineBrain>();worldCamera=brain.ActiveVirtualCamera as CinemachineCamera;
        Require(worldCamera!=null,"gameplay virtual camera available");
        foreach(var input in UnityEngine.Object.FindObjectsByType<CinemachineInputAxisController>(FindObjectsSortMode.None))input.enabled=false;
        foreach(var input in UnityEngine.Object.FindObjectsByType<CinemachinePinchZoom>(FindObjectsSortMode.None))input.enabled=false;
        foreach(var input in UnityEngine.Object.FindObjectsByType<ThirdPersonCameraController>(FindObjectsSortMode.None))input.enabled=false;
        foreach(var a in stage.book.GetComponentsInChildren<Animator>(true))a.enabled=false;
        foreach(var r in stage.book.GetComponentsInChildren<Renderer>(true))if(r.name=="Idle turning page")r.enabled=false;
        worldCamera.Follow=null;worldCamera.LookAt=null;brain.enabled=false;
        handScale=hand.transform.localScale.x;touch=InputSystem.AddDevice<Touchscreen>();finger=2600;
        BuildOverlay();BuildBadge();Time.captureFramerate=24;crop=Full;
        File.WriteAllText(Evidence,"Actual Unity Game view, 24 fps. Offline transport and completion persistence suppression. Normal Input System touches, real CharacterController walking, catalogue collection, return, placement and Submit. C1 is continuous across clips 1–4; clips 5–6 explicitly introduce the C5 full-proof example.\n");
        try
        {
            var book=stage.book.position;
            Position(Ground(book+Vector3.back*7,33),book);
            SetCamera(book+new Vector3(5,6,-9),book+Vector3.up*1.7f,58);
            exampleBadge.text="CHALLENGE 1 — BASE CASE";yield return Frames(4);
            Start(0);yield return FadeIn();yield return Walk(book+Vector3.back*2.6f,5);Face(book);yield return Frames(5);
            Require(stage.Target==stage.book&&stage.State==InductionState.WaitingForBook,"waiting book targeted by normal proximity");
            yield return ZoomButton(hand,1.5f);Snapshot("01_Hand");yield return Tap((RectTransform)hand.transform);
            Require(stage.State==InductionState.Active&&stage.ChallengeIndex==0&&stage.Correct==0&&stage.timer.Running,"actual hand starts C1 and countdown");
            sameQuestion=stage.Challenge;Log("C1 theorem: "+sameQuestion.theorem);yield return Frames(28);yield return Finish();ResetView();

            var approach=Front(stage.stations[0],2.3f);
            SetCamera(new Vector3(-117,36,257),new Vector3(-106,31,271),58);
            Start(1);yield return FadeIn();
            // Walk around the solid statue rather than directly through its plinth.
            yield return Walk(Ground(book+new Vector3(-5,0,-2.6f),33),5.5f);
            yield return Walk(approach,5.5f);yield return Frames(4);
            Require(view.gameObject.activeInHierarchy&&ButtonText(view)=="View Board","nearby real View Board offered");
            yield return ZoomButton(view,1.45f);Snapshot("02_ViewBoard");yield return OpenBoard();view.transform.localScale=Vector3.one;
            var question=Union(TextRect(stage.boardTitle,18),TextRect(stage.boardFormula,18));
            veil.SetHoles(question);yield return Focus(question,1.3f);yield return Frames(82);Snapshot("02_Theorem");
            var instructions=TextRect(stage.boardInstructions,22);veil.SetHoles(instructions);yield return Focus(instructions,1.4f);yield return Frames(96);Snapshot("02_Instructions");
            yield return Finish();ResetView();yield return Tap((RectTransform)ui.BoardBackButton.transform);
            while(stage.boardView.Viewing)yield return Frames(1);
            Require(!stage.Reading&&stage.Correct==0&&ReferenceEquals(sameQuestion,stage.Challenge),"Back preserves the current question");

            Position(Ground(stage.scrollStand.position+new Vector3(0,0,-7),33),stage.scrollStand.position);
            SetCamera(stage.scrollStand.position+new Vector3(6,6,-9),stage.scrollStand.position+Vector3.up*1.8f,56);
            Start(2);yield return FadeIn();yield return Walk(stage.scrollStand.position+Vector3.back*2.8f,5);Face(stage.scrollStand.position);yield return Frames(5);
            var selected=stage.Tablets.Single(t=>t.Data.id==sameQuestion.answers["Base:1"]);
            yield return Collect(selected,true);yield return Frames(24);yield return Finish();ResetView();

            SetCamera(new Vector3(-99,42,249),new Vector3(-99,30.5f,270),59);
            Start(3);yield return FadeIn();Require(stage.Held==selected&&ButtonText(ui.ReturnButton)=="Return Scroll","carried scroll offers Return Scroll");
            yield return ZoomButton(ui.ReturnButton,1.35f);Snapshot("04_Return");yield return Tap((RectTransform)ui.ReturnButton.transform);
            Require(stage.Held==null&&selected.AtStand&&stage.slots.All(s=>s.Tablet==null),"Return restores exactly the same scroll to its stand");
            ui.ReturnButton.transform.localScale=Vector3.one;veil.SetHoles();yield return Overview();yield return Frames(18);
            // Recollect the same scroll through the catalogue; returning never duplicates it.
            yield return Collect(selected,false);yield return Overview();
            var baseSlot=stage.slots.Single(s=>s.key=="Base:1");yield return ApproachSlot(baseSlot,true);
            Require(stage.Target==baseSlot&&stage.altarIndicators.Arrows[0].gameObject.activeSelf,"real active Base Case arrow and placement target");
            yield return ZoomButton(ui.InteractButton,1.2f);Snapshot("04_Place");yield return Tap((RectTransform)ui.InteractButton.transform);ResetView();
            Require(baseSlot.Tablet==selected&&stage.Held==null&&stage.Correct==0,"placement is real and not submitted");
            yield return Frames(36);yield return Finish();

            // Trim repetitive preparation between examples, using the same normal UI flow.
            ResetView();yield return Tap((RectTransform)ui.SubmitButton.transform);
            Require(stage.Correct==1&&stage.State==InductionState.WaitingForBook,"C1 submits normally between examples");
            for(int c=1;c<4;c++){yield return StartCurrent();yield return FillCurrent();yield return Tap((RectTransform)ui.SubmitButton.transform);Require(stage.Correct==c+1,"normal intermediate completion "+(c+1));}
            yield return StartCurrent();sameQuestion=stage.Challenge;Require(stage.ChallengeIndex==4&&stage.Correct==4,"separate C5 example reached through normal submissions");
            // Trim the ordinary Start/Correct notices before the explanatory overview.
            yield return new WaitForSecondsRealtime(5.2f);
            Log("C5 theorem: "+sameQuestion.theorem);exampleBadge.text="CHALLENGE 5 — FULL PROOF EXAMPLE";
            Position(Ground(new Vector3(-99,29,257),33),new Vector3(-99,29,272));
            SetCamera(new Vector3(-99,43,248),new Vector3(-99,32,272),59);stage.altarIndicators.Refresh();yield return Frames(3);
            Start(4);yield return FadeIn();Require(stage.altarIndicators.Arrows.All(a=>a.gameObject.activeSelf),"all three C5 stations indicated");
            yield return Frames(30);Snapshot("05_AllStations");
            for(int i=0;i<3;i++)
            {
                var title=stage.stations[i].GetComponentsInChildren<TMP_Text>().Single(t=>t.name=="Station Label");
                var region=Union(TextRect(title,20),WorldRect(stage.stations[i].GetComponent<Renderer>().bounds,22));
                veil.SetHoles(region);yield return Focus(region,1.35f);yield return Frames(58);Snapshot("05_Station"+i);
            }
            veil.SetHoles();yield return Overview();yield return Frames(22);yield return Finish();
            ResetView();yield return FillCurrent();Require(stage.CanSubmit&&stage.slots.Where(s=>s.gameObject.activeSelf).All(s=>s.Tablet!=null),"all C5 proof positions filled through real catalogue and placement controls");

            exampleBadge.text="CHALLENGE 5 — FULL PROOF ARRANGED";
            Position(Ground(new Vector3(-99,29,257),33),new Vector3(-99,29,272));
            SetCamera(new Vector3(-99,40,249),new Vector3(-99,32,272),59);
            Start(5);yield return FadeIn();yield return Frames(48);Require(ButtonText(ui.SubmitButton)=="Submit","actual answer check uses Submit");
            yield return ZoomButton(ui.SubmitButton,1.5f);yield return Focus(ScreenRect((RectTransform)ui.SubmitButton.transform,18),1.45f);Snapshot("06_Submit");
            yield return Tap((RectTransform)ui.SubmitButton.transform);veil.SetHoles();crop=Full;
            Require(stage.Correct==5&&stage.Mistakes==0&&stage.State==InductionState.StageComplete,"normal Submit validates the full proof exactly once");
            var result=Union(TextRect(ui.Result.Heading,20),TextRect(ui.Result.Summary,20));veil.SetHoles(result);
            yield return Focus(result,1.3f);yield return Frames(96);Snapshot("06_Feedback");yield return Finish();
            File.WriteAllText(Output+Keys[0]+".json",JsonUtility.ToJson(new StageHelpVideoCatalog.Recording {scene="Induction",clips=Keys,
                source="Actual offline Unity Game view. C1 clips 1–4; separate labeled C5 full-proof example clips 5–6. Real CharacterController movement and Input System UI interactions, no direct answer placement. Repetitive between-example preparation trimmed. Recording-only stable camera, zoom, spotlight and touch visualization; 48-frame two-second black fade after each clip. Backend and reward writes suppressed; variant history restored.",
                cues=Captions.Select(c=>new StageHelpVideoCatalog.Cue{seconds=0,caption=c}).ToArray()},true));
            Log("PASS all six recordings; five normal challenge submissions, no mistakes, no saved progression/rewards.");
        }
        finally
        {
            encoder?.Dispose();encoder=null;Time.captureFramerate=0;Time.timeScale=1;
            if(pixels!=null)UnityEngine.Object.Destroy(pixels);if(zoomPixels!=null)UnityEngine.Object.Destroy(zoomPixels);
            if(zoomTexture!=null){zoomTexture.Release();UnityEngine.Object.Destroy(zoomTexture);}
            if(touch!=null&&touch.added)InputSystem.RemoveDevice(touch);if(overlay!=null)UnityEngine.Object.Destroy(overlay.gameObject);
        }
    }
    static void ResetView(){crop=Full;veil.SetHoles();hand.transform.localScale=Vector3.one*handScale;ui.InteractButton.transform.localScale=ui.ReturnButton.transform.localScale=ui.SubmitButton.transform.localScale=Vector3.one;}
    static Vector3 Front(Transform station,float padding){var b=station.GetComponent<Renderer>().bounds;return Ground(new Vector3(b.center.x,29,b.min.z-padding),33);}
    static IEnumerator StartCurrent()
    {
        Position(Ground(stage.book.position+Vector3.back*2.6f,33),stage.book.position);yield return Frames(2);yield return Tap((RectTransform)hand.transform);
        Require(stage.State==InductionState.Active,"book starts the next challenge");
    }
    static IEnumerator FillCurrent()
    {
        foreach(var answer in stage.Challenge.answers)
        {
            Position(Ground(stage.scrollStand.position+Vector3.back*2.8f,33),stage.scrollStand.position);yield return Frames(2);
            var tablet=stage.Tablets.Single(t=>t.Data.id==answer.Value);yield return Collect(tablet,false);
            var slot=stage.slots.Single(s=>s.key==answer.Key);yield return ApproachSlot(slot,false);
            yield return Tap((RectTransform)ui.InteractButton.transform);Require(slot.Tablet==tablet&&stage.Held==null,"normal placement "+slot.key);
        }
    }
    static IEnumerator Collect(InductionTablet tablet,bool explain)
    {
        Face(stage.scrollStand.position);yield return Frames(3);Require(stage.Target==stage.scrollStand,"normal stand proximity");
        if(explain)yield return ZoomButton(hand,1.45f);yield return Tap((RectTransform)hand.transform);ResetView();
        Require(stage.BrowsingScrolls&&stage.Reading&&ui.Collection.activeInHierarchy,"hand opens the real scroll catalogue");
        var button=ui.Collection.GetComponentsInChildren<Button>().Single(b=>b.name=="Scroll Choice "+tablet.Data.id);
        var scroller=button.GetComponentInParent<ScrollRect>();Canvas.ForceUpdateCanvases();
        float y=Mathf.Clamp(-((RectTransform)button.transform).anchoredPosition.y,0,Mathf.Max(0,scroller.content.rect.height-scroller.viewport.rect.height));
        var from=scroller.content.anchoredPosition;int duration=explain?18:2;
        for(int i=0;i<duration;i++){scroller.content.anchoredPosition=Vector2.Lerp(from,new Vector2(0,y),(i+1)/(float)duration);yield return Frames(1);}
        if(explain){veil.SetHoles(ScreenRect((RectTransform)button.transform,10));yield return Focus(ScreenRect((RectTransform)button.transform,12),1.2f);yield return Frames(34);Snapshot("03_Choices");}
        yield return Tap((RectTransform)button.transform);veil.SetHoles();
        Require(ui.GetScrollButton.IsInteractable()&&ButtonText(ui.GetScrollButton)=="Get This Scroll","selected exact formula is collectible");
        if(explain){var formula=Read<TMP_Text>(ui,"selectedFormula");Require(formula.text==tablet.Data.expression,"catalogue formula matches gameplay data");yield return Focus(TextRect(formula,20),1.5f);yield return Frames(54);yield return ZoomButton(ui.GetScrollButton,1.2f);Snapshot("03_GetScroll");}
        yield return Tap((RectTransform)ui.GetScrollButton.transform);ui.GetScrollButton.transform.localScale=Vector3.one;ResetView();
        Require(stage.Held==tablet&&!tablet.AtStand&&!stage.Reading,"real Get This Scroll collects exactly once");
    }
    static IEnumerator ApproachSlot(InductionSlot slot,bool walking)
    {
        var station=stage.stations.First(s=>slot.transform.IsChildOf(s));var b=station.GetComponent<Renderer>().bounds;
        var side=Ground(new Vector3(slot.transform.position.x<=b.center.x?b.min.x-1.25f:b.max.x+1.25f,29,slot.transform.position.z),33);
        if(walking){yield return Walk(new Vector3(side.x,side.y,b.min.z-2.3f),6);yield return Walk(side,5);}
        else Position(side,slot.transform.position);
        Face(slot.transform.position);yield return Frames(3);Require(stage.Target==slot&&ui.InteractButton.gameObject.activeInHierarchy,"actual target "+slot.key+" instead of "+stage.Target);
    }
    static Rect WorldRect(Bounds b,float padding)
    {
        var points=new List<Vector3>();for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)points.Add(camera.WorldToScreenPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z))));
        return Rect.MinMaxRect(points.Min(p=>p.x)-padding,points.Min(p=>p.y)-padding,points.Max(p=>p.x)+padding,points.Max(p=>p.y)+padding);
    }
    static void BuildBadge()
    {
        var rt=NewRect("Labeled separate gameplay example");rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(.5f,1);rt.anchoredPosition=new Vector2(0,-14);rt.sizeDelta=new Vector2(650,46);
        exampleBadge=rt.gameObject.AddComponent<TextMeshProUGUI>();exampleBadge.font=StageUITheme.HeadingFont;exampleBadge.fontSize=28;exampleBadge.alignment=TextAlignmentOptions.Center;exampleBadge.color=new Color(1,.92f,.65f);exampleBadge.outlineColor=Color.black;exampleBadge.outlineWidth=.2f;exampleBadge.raycastTarget=false;
        fade.transform.SetAsLastSibling();
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
        if(segment!=0)File.WriteAllText(Output+Keys[segment]+".json",JsonUtility.ToJson(new StageHelpVideoCatalog.Recording {scene="Induction",source="Actual offline Unity Game view",cues=new[]{new StageHelpVideoCatalog.Cue{seconds=0,caption=Captions[segment]}}},true));
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
        int count=0;while((!stage.boardView.Viewing||stage.boardView.Transitioning)&&count++<180)yield return Frames(1);
        Require(stage.boardView.Viewing&&!stage.boardView.Transitioning,"normal View Board camera completes its smooth readable framing");
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
    static void AssertSame(){Require(ReferenceEquals(stage.Challenge,sameQuestion),"same question identity across all clips");}
    static void Require(bool value,string text){if(!value)throw new InvalidOperationException(text);}
    static void Log(string text)=>File.AppendAllText(Evidence,text+"\n");
    static void Snapshot(string key)
    {
        if(pixels!=null)File.WriteAllBytes("Library/InductionHelp_"+key+".png",(Mathf.Abs(crop.width-Screen.width)>1?zoomPixels:pixels).EncodeToPNG());
    }
    static void BuildOverlay()
    {
        overlay=new GameObject("Recording-only induction presentation",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)).GetComponent<Canvas>();
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
