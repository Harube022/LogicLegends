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
public static class FormalHelpClipCapture
{
    const string Output="Library/StageHelpCaptures/", Evidence="Library/FormalHelpSequence.txt";
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    public static readonly string[] Keys={"FormalProofs","FormalProofs_02_Board","FormalProofs_03_Scroll","FormalProofs_04_Placement","FormalProofs_05_Submission"};
    public static readonly string[] Captions={
        "Approach the Book Statue and tap the hand button to begin.",
        "Tap View Board to read the current proof task.",
        "Read the proof, then tap Get This Scroll to collect it.",
        "Choose the altar that matches the proof style and place the scroll.",
        "Return to the Book Statue and submit your completed answer."
    };
    static ProofsLibraryStageController stage;
    static ProofsStageUI ui;
    static ProofChallengeData sameQuestion;
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
    static T Read<T>(object owner,string key)=>(T)owner.GetType().GetField(key,Private).GetValue(owner);
    static Rect Full=>new Rect(0,0,Screen.width,Screen.height);

    public static IEnumerator Record()
    {
        stage=UnityEngine.Object.FindFirstObjectByType<ProofsLibraryStageController>();ui=stage.Presentation;
        var book=stage.BookTarget;
        // Keep the decorative page turn still in this test capture; the imported animated
        // proxy can have extreme bounds. Static book pages and all interaction logic remain.
        foreach(var animator in book.GetComponentsInChildren<Animator>(true))animator.enabled=false;
        foreach(var renderer in book.GetComponentsInChildren<Renderer>(true))
            if(renderer.name=="Idle turning page")renderer.enabled=false;
        hand=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b=>b.name=="InteractButton");view=ui.ViewButton;
        camera=Camera.main;brain=camera.GetComponent<CinemachineBrain>();worldCamera=brain.ActiveVirtualCamera as CinemachineCamera;
        Require(worldCamera!=null,"gameplay virtual camera is available");
        foreach(var input in UnityEngine.Object.FindObjectsByType<CinemachineInputAxisController>(FindObjectsSortMode.None))input.enabled=false;
        foreach(var input in UnityEngine.Object.FindObjectsByType<CinemachinePinchZoom>(FindObjectsSortMode.None))input.enabled=false;
        foreach(var input in UnityEngine.Object.FindObjectsByType<ThirdPersonCameraController>(FindObjectsSortMode.None))input.enabled=false;
        worldCamera.Follow=null;worldCamera.LookAt=null;brain.enabled=false;
        handScale=hand.transform.localScale.x;touch=InputSystem.AddDevice<Touchscreen>();finger=1600;
        BuildOverlay();Time.captureFramerate=24;crop=Full;
        File.WriteAllText(Evidence,"Actual Game view capture; offline test account transport; 24 fps. Physical walks and real Input System touches. One randomized classification question, both actual scrolls. No saved progression or rewards.\n");
        try
        {
            Position(Ground(book.position+new Vector3(-3,0,-6),5),book.position);
            SetCamera(book.position+new Vector3(4,6,-7),book.position+new Vector3(0,2,0),58);
            yield return Frames(4);Start(0);yield return FadeIn();
            yield return Walk(book.position+new Vector3(0,0,-2.5f),5);Face(book.position);yield return Frames(6);
            Require(stage.IsBookTargeted&&!stage.Started,"normal proximity targets waiting book");
            yield return ZoomButton(hand,1.5f);Snapshot("01_Hand");yield return Tap((RectTransform)hand.transform);
            Require(stage.Started&&stage.ChallengeNumber==1&&stage.QuestionNumber==1&&stage.CorrectCount==0,"real hand starts first question");
            sameQuestion=Read<ProofChallengeData>(stage,"activeChallenge");
            Require(sameQuestion.kind==ProofChallengeKind.SortScrolls&&sameQuestion.scrolls.Length==2,"first question has two scrolls to classify");
            Log("Question: "+sameQuestion.title+"; instructions: "+sameQuestion.instructions);
            foreach(var scroll in sameQuestion.scrolls)Log(scroll.id+": "+scroll.title+"; formal="+scroll.isFormal+"; "+scroll.content);
            yield return Frames(26);yield return Finish();hand.transform.localScale=Vector3.one*handScale;veil.SetHoles();

            Start(1);yield return FadeIn();yield return ZoomButton(view,1.45f);Snapshot("02_ViewBoard");
            yield return OpenBoard();view.transform.localScale=Vector3.one;
            veil.SetHoles(TextRect(stage.BoardInstructions,24));yield return Focus(TextRect(stage.BoardInstructions,34),1.35f);
            yield return Frames(144);Snapshot("02_BoardTask");yield return Finish();
            crop=Full;veil.SetHoles();yield return Tap((RectTransform)ui.BackButton.transform);
            while(ui.BoardView.Viewing)yield return Frames(1);
            Require(stage.Started&&stage.CorrectCount==0,"board reading never submits");

            var source=stage.ScrollTargets[0].transform;
            var sourceAt=source.position+Vector3.right*2.2f;
            Position(Ground(sourceAt+new Vector3(2,0,-4),5),source.position);
            SetCamera(source.position+new Vector3(8,6,-8),source.position+Vector3.up*2.3f,57);
            Start(2);yield return FadeIn();yield return Walk(sourceAt,5);Face(source.position);yield return Frames(6);
            Require(ui.InteractionButton.gameObject.activeInHierarchy&&ButtonText(ui.InteractionButton)=="Interact","real scroll interaction control available");
            yield return ZoomButton(ui.InteractionButton,1.5f);Snapshot("03_Interact");yield return Tap((RectTransform)ui.InteractionButton.transform);
            ui.InteractionButton.transform.localScale=Vector3.one;veil.SetHoles();
            Require(stage.HasReaderOpen&&stage.ReadingScroll==0&&stage.ReaderBody.text==sameQuestion.scrolls[0].content,"real centered reader uses exact scroll data");
            yield return ShowReader(0);Snapshot("03_ReadScroll");
            Require(ButtonText(stage.CollectButton)=="Get This Scroll","actual collection label");
            yield return Focus(ScreenRect((RectTransform)stage.CollectButton.transform,22),1.5f);
            veil.SetHoles(ScreenRect((RectTransform)stage.CollectButton.transform,14));yield return Frames(24);Snapshot("03_GetScroll");
            yield return Tap((RectTransform)stage.CollectButton.transform);veil.SetHoles();crop=Full;
            Require(!stage.HasReaderOpen&&stage.CarriedScroll==0&&stage.Placement(0)==-1,"Get This Scroll collects once without placement");
            yield return Frames(28);yield return Finish();

            // One steady overview covers both real altar placements. No answer is submitted here.
            SetCamera(new Vector3(0,9,-17),new Vector3(0,2.6f,1),60);
            Start(3);yield return FadeIn();yield return PlaceCarried(0);
            var second=stage.ScrollTargets[1].transform;
            yield return Walk(second.position+Vector3.left*2.2f,6);Face(second.position);yield return Frames(6);
            Require(ui.InteractionButton.gameObject.activeInHierarchy,"second scroll's actual interaction is available");
            veil.SetHoles(ScreenRect((RectTransform)ui.InteractionButton.transform,14));yield return Tap((RectTransform)ui.InteractionButton.transform);veil.SetHoles();
            Require(stage.HasReaderOpen&&stage.ReaderBody.text==sameQuestion.scrolls[1].content,"same question's second proof is read");
            yield return ShowReader(1);
            yield return Focus(ScreenRect((RectTransform)stage.CollectButton.transform,22),1.5f);
            veil.SetHoles(ScreenRect((RectTransform)stage.CollectButton.transform,14));yield return Frames(18);
            yield return Tap((RectTransform)stage.CollectButton.transform);veil.SetHoles();
            Require(stage.CarriedScroll==1,"second scroll is collected through actual UI");
            yield return Overview();
            yield return PlaceCarried(1);
            Require(stage.CorrectCount==0&&stage.Started&&Enumerable.Range(0,2).All(i=>stage.Placement(i)==(sameQuestion.scrolls[i].isFormal?0:1)),"both real placements are complete and ungraded");
            yield return Frames(30);Snapshot("04_BothPlaced");yield return Finish();

            // Trim only the long return; retain the actual final walk and book interaction.
            Position(Ground(book.position+new Vector3(0,0,7),5),book.position);
            SetCamera(book.position+new Vector3(4,6,-7),book.position+new Vector3(0,2,1),58);
            Start(4);yield return FadeIn();yield return Walk(book.position+new Vector3(0,0,2.5f),5);Face(book.position);yield return Frames(6);
            Require(stage.IsBookTargeted&&stage.PromptText.text.Contains("Confirm"),"book's current hand interaction confirms the whole arrangement");
            veil.SetHoles(ScreenRect((RectTransform)hand.transform,14),ScreenRect(stage.PromptText.rectTransform,10));yield return Frames(22);
            yield return ZoomButton(hand,1.5f);Snapshot("05_ConfirmHand");yield return Tap((RectTransform)hand.transform);
            Require(stage.CorrectCount==1&&!stage.Started&&stage.BoardInstructions.text.Contains("QUESTION COMPLETE"),"actual book validation accepts the completed answer exactly once");
            hand.transform.localScale=Vector3.one*handScale;veil.SetHoles();
            // Hold actual feedback before its normal next-question reset; recording session only.
            Time.timeScale=0;yield return OpenBoard();
            veil.SetHoles(TextRect(stage.BoardInstructions,24));yield return Focus(TextRect(stage.BoardInstructions,32),1.5f);
            yield return Frames(144);Snapshot("05_Validated");
            yield return Finish();
            Require(stage.CorrectCount==1&&!stage.Finished&&stage.ChallengeNumber==1,"recording never completes a trial or awards coins");
            Log("PASS: same question and two scrolls; actual reader, collection, two Place Scroll actions and one hand confirmation. No direct gameplay callbacks or saved progress/rewards.");
            File.WriteAllText(Output+Keys[0]+".json",JsonUtility.ToJson(new StageHelpVideoCatalog.Recording {scene="Formal vs. Informal Proofs",clips=Keys,
                source="Actual offline Unity Game view, one randomized question and both scrolls. Physical walks and Input System touch invoke normal book, reader, collection, placement and validation. Stable recording-only cameras, close-ups and spotlights; 48-frame two-second black fade per clip; account writes suppressed.",
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
    static IEnumerator ShowReader(int index)
    {
        yield return Frames(3);veil.SetHoles(ScreenRect((RectTransform)stage.ReaderPanel.transform,10));
        yield return Focus(ScreenRect((RectTransform)stage.ReaderPanel.transform,12),1.25f);
        yield return Frames(18);
        // Fill the Help video's small mobile viewport with the actual proof text.
        // The full parchment and its controls remain visible before and after this close-up.
        var scroll=Read<ScrollRect>(ui,"readerScroll");
        if(scroll.content.rect.height<=scroll.viewport.rect.height+1)
            yield return Focus(TextRect(stage.ReaderBody,20),2.2f);
        int hold=Mathf.CeilToInt(Mathf.Clamp(1+sameQuestion.scrolls[index].content.Split(' ').Length/3f,3,8)*24);
        yield return Frames(hold);
        if(scroll.content.rect.height>scroll.viewport.rect.height+1)
        {
            for(int i=0;i<36;i++){scroll.verticalNormalizedPosition=1-(i+1)/36f;yield return Frames(1);}
            yield return Frames(48);
        }
    }
    static IEnumerator PlaceCarried(int index)
    {
        var altar=stage.AltarTargets[sameQuestion.scrolls[index].isFormal?0:1];
        yield return Walk(altar.position+new Vector3(0,0,-2.6f),5.5f);Face(altar.position);yield return Frames(5);
        Require(stage.CarriedScroll==index&&ui.InteractionButton.gameObject.activeInHierarchy&&ButtonText(ui.InteractionButton)=="Place Scroll","actual nearby Place Scroll control");
        yield return Focus(ScreenRect((RectTransform)ui.InteractionButton.transform,16),1.5f);
        veil.SetHoles(ScreenRect((RectTransform)ui.InteractionButton.transform,14));yield return Frames(26);Snapshot("04_Place"+index);
        yield return Tap((RectTransform)ui.InteractionButton.transform);veil.SetHoles();
        Require(stage.CarriedScroll==-1&&stage.Placement(index)==(sameQuestion.scrolls[index].isFormal?0:1)&&stage.CorrectCount==0,"Place Scroll places, never validates");
        yield return Overview();yield return Frames(18);Log("Placed "+sameQuestion.scrolls[index].id+" on "+altar.name+" using Place Scroll.");
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
        if(segment!=0)File.WriteAllText(Output+Keys[segment]+".json",JsonUtility.ToJson(new StageHelpVideoCatalog.Recording {scene="Formal vs. Informal Proofs",source="Actual offline Unity Game view",cues=new[]{new StageHelpVideoCatalog.Cue{seconds=0,caption=Captions[segment]}}},true));
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
    static void AssertSame(){Require(ReferenceEquals(Read<ProofChallengeData>(stage,"activeChallenge"),sameQuestion),"same question identity across all clips");}
    static void Require(bool value,string text){if(!value)throw new InvalidOperationException(text);}
    static void Log(string text)=>File.AppendAllText(Evidence,text+"\n");
    static void Snapshot(string key)
    {
        if(pixels!=null)File.WriteAllBytes("Library/FormalHelp_"+key+".png",(Mathf.Abs(crop.width-Screen.width)>1?zoomPixels:pixels).EncodeToPNG());
    }
    static void BuildOverlay()
    {
        overlay=new GameObject("Recording-only formal proof presentation",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)).GetComponent<Canvas>();
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
