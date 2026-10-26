#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LogicLegends.Inference;
using TMPro;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Unity.Cinemachine;

/// <summary>One real inference challenge; recording-only stable cameras and digital close-ups.</summary>
public static class InferenceHelpClipCapture
{
    const string Output="Library/StageHelpCaptures/", Evidence="Library/InferenceHelpSequence.txt";
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    public static readonly string[] Keys={"RulesOfInference","RulesOfInference_02_Board","RulesOfInference_03_Premises","RulesOfInference_04_Crystal","RulesOfInference_05_Conclusion"};
    public static readonly string[] Captions={
        "Approach the Book Statue and tap the hand button to begin.",
        "Approach the altar and tap View Board to read the challenge.",
        "Premises are statements accepted as true.",
        "Find the matching Rule Crystal, answer its pickup quiz, and collect it.",
        "Return to the altar, place your Rule Crystal, and check the conclusion."
    };
    static InferenceChallenge stage;
    static InferenceCrystalQuiz quiz;
    static InferenceAltarTrigger altar;
    static InferenceQuestion sameQuestion;
    static InferenceCrystal crystal;
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
    static Vector3 altarPoint;
    static T Read<T>(object owner,string key)=>(T)owner.GetType().GetField(key,Private).GetValue(owner);
    static Rect Full=>new Rect(0,0,Screen.width,Screen.height);

    public static IEnumerator Record()
    {
        stage=UnityEngine.Object.FindFirstObjectByType<InferenceChallenge>();
        altar=UnityEngine.Object.FindFirstObjectByType<InferenceAltarTrigger>();
        quiz=stage.GetComponent<InferenceCrystalQuiz>();
        var book=UnityEngine.Object.FindFirstObjectByType<InferenceBookStart>().transform;
        hand=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b=>b.name=="InteractButton");
        view=Read<Button>(altar,"viewBoardButton");
        camera=Camera.main;brain=camera.GetComponent<CinemachineBrain>();
        worldCamera=brain.ActiveVirtualCamera as CinemachineCamera;
        Require(worldCamera!=null,"current gameplay virtual camera is available");
        foreach(var input in UnityEngine.Object.FindObjectsByType<CinemachineInputAxisController>(FindObjectsSortMode.None))input.enabled=false;
        foreach(var input in UnityEngine.Object.FindObjectsByType<CinemachinePinchZoom>(FindObjectsSortMode.None))input.enabled=false;
        foreach(var input in UnityEngine.Object.FindObjectsByType<ThirdPersonCameraController>(FindObjectsSortMode.None))input.enabled=false;
        worldCamera.Follow=null;worldCamera.LookAt=null;brain.enabled=false;
        handScale=hand.transform.localScale.x;
        touch=InputSystem.AddDevice<Touchscreen>();finger=1000;
        BuildOverlay();overlay.gameObject.AddComponent<InferenceRecordingQuizZoom>().Quiz=quiz;
        Time.captureFramerate=24;crop=Full;
        File.WriteAllText(Evidence,"Actual Game view capture; offline test account transport; 24 fps. Normal challenge callbacks, real touch UI, physical walks. No saved progression or rewards.\n");
        try
        {
            Position(Ground(book.position+new Vector3(4,0,11),book.position.y+6),book.position);
            // Frame from the clear trail side, outside the nearby tree canopies.
            SetCamera(book.position+new Vector3(-8.5f,9,14),book.position+new Vector3(2,3,3),55);
            yield return Frames(4);
            Start(0);yield return FadeIn();
            yield return Walk(book.position+new Vector3(2,0,3.1f),5.5f);
            Face(book.position);yield return Frames(4);
            yield return ZoomButton(hand,1.5f);Snapshot("01_Hand");yield return Tap(hand.transform as RectTransform);
            Require(stage.HasStarted&&stage.SolvedRounds==0,"real hand starts exactly the first challenge");
            yield return Frames(26);yield return Finish();
            hand.transform.localScale=Vector3.one*handScale;veil.SetHoles();
            yield return Frames(65);

            altarPoint=altar.GetComponent<Collider>().bounds.center+Vector3.forward*1.5f;
            Position(Ground(altarPoint+Vector3.forward*11,altarPoint.y+4),altarPoint);
            SetCamera(altarPoint+new Vector3(14,13,28),altarPoint+Vector3.up*1.5f,50);
            Start(1);yield return FadeIn();
            yield return Walk(altarPoint,5.5f);yield return Frames(6);
            Require(view.gameObject.activeInHierarchy&&ButtonText(view)=="View Board","altar exposes the real View Board button");
            yield return ZoomButton(view,1.65f);Snapshot("02_ViewBoard");
            yield return OpenBoard();
            sameQuestion=stage.Question;
            Require(sameQuestion!=null&&stage.Stage==InferencePuzzleStage.CompletingPremises,"normal board opening draws the first real question");
            Log("Question "+sameQuestion.Id+"; rule "+sameQuestion.Rule.ruleName+" ("+sameQuestion.Rule.abbreviation+"); conclusion: "+stage.dragPuzzle.Puzzle.Conclusion);
            Log("Premises: "+string.Join(" | ",stage.dragPuzzle.Puzzle.Premises.Select(row=>string.Join(" ",row.Select(p=>p.Text)))));
            Log("Expected blank words: "+string.Join(" | ",stage.dragPuzzle.Puzzle.CorrectPlacements));
            yield return Frames(30);Snapshot("02_OpenBoard");yield return Finish();
            view.transform.localScale=Vector3.one;

            Start(2);yield return FadeIn();
            var premiseArea=stage.board.GetComponentsInChildren<RectTransform>(true).First(r=>r.name=="Theme_PremisesScroll");
            var premiseTitle=stage.board.GetComponentsInChildren<RectTransform>(true).First(r=>r.name=="Theme_PremisesTitle");
            veil.SetHoles(ScreenRect(premiseArea,10),ScreenRect(premiseTitle,10));
            var sentenceBounds=TextRect(premiseTitle.GetComponent<TMP_Text>(),14);
            foreach(var text in stage.dragPuzzle.premises.GetComponentsInChildren<TMP_Text>())sentenceBounds=Union(sentenceBounds,TextRect(text,14));
            foreach(var zone in stage.dragPuzzle.Zones)sentenceBounds=Union(sentenceBounds,ScreenRect((RectTransform)zone.transform,14));
            yield return Focus(sentenceBounds,1.9f);
            yield return Frames(64);Snapshot("03_Premises");
            Cue("Fill the blanks using words from the word bank. Tap a word, then its blank.");
            yield return Overview();
            var words=stage.dragPuzzle.Words.ToList();
            for(int i=0;i<stage.dragPuzzle.Zones.Count;i++)
            {
                string expected=stage.dragPuzzle.Puzzle.CorrectPlacements[i];
                var word=words.First(w=>w.Word==expected);words.Remove(word);var zone=stage.dragPuzzle.Zones[i];
                veil.SetHoles(ScreenRect((RectTransform)word.transform,14),ScreenRect((RectTransform)zone.transform,14));
                yield return Frames(12);yield return Tap((RectTransform)word.transform);
                Require(DraggableWord.Selected==word,"actual word tile tap selects the intended word");
                yield return Tap((RectTransform)zone.transform);
                Require(zone.PlacedWord==word&&zone.Value==expected,"actual blank tap places the selected word");
                Log("Actual touch placed blank "+(i+1)+": "+zone.Value);yield return Frames(10);
            }
            Cue("Tap Check argument to validate the completed premises.");
            veil.SetHoles(ScreenRect((RectTransform)stage.validateButton.transform,14));yield return Frames(22);
            yield return Tap((RectTransform)stage.validateButton.transform);
            Require(stage.Stage==InferencePuzzleStage.FindingDiamond&&stage.dragPuzzle.PremisesCompleted,"real Check argument validates and locks the completed premises");
            Require(stage.Crystals.Count==9,"all nine actual Rule Crystals spawned");
            crystal=stage.Crystals.Single(c=>c.GetComponent<RuleDiamond>().Abbreviation==sameQuestion.Rule.abbreviation);
            Cue("Rule Crystals represent inference rules. Find the rule matching the premises.");
            var search=stage.board.GetComponentsInChildren<TMP_Text>(true).First(t=>t.name=="Theme_BankStatus");
            yield return Frames(2);veil.SetHoles(ScreenRect((RectTransform)search.transform.parent,8));
            yield return Focus(ScreenRect((RectTransform)search.transform.parent,12),2.1f);
            yield return Frames(78);Snapshot("03_CrystalInstructions");
            Cue("The conclusion follows from the premises when the correct inference rule is applied.");
            var conclusion=(RectTransform)stage.dragPuzzle.conclusion.label.transform.parent;
            veil.SetHoles(ScreenRect(conclusion,12));yield return Focus(ScreenRect(conclusion,18),1.4f);
            yield return Frames(64);Snapshot("03_ConclusionArea");
            Require(!stage.dragPuzzle.conclusion.Revealed,"conclusion remains hidden before crystal validation");
            yield return Overview();veil.SetHoles();yield return Finish();
            crop=Full;yield return Tap((RectTransform)stage.exploreButton.transform);yield return Frames(26);brain.enabled=false;
            AssertSame();

            var towardAltar=altarPoint-crystal.transform.position;towardAltar.y=0;towardAltar.Normalize();
            var origin=crystal.transform.position;
            Log("Matching tower crystal "+origin+" pickup bounds "+crystal.GetComponent<SphereCollider>().bounds);
            Position(Ground(origin+towardAltar*11,origin.y+8),origin);
            SetCamera(origin+towardAltar*18+Vector3.Cross(Vector3.up,towardAltar)*7+Vector3.up*7,origin+Vector3.up,48);
            yield return Frames(5);
            Start(3);yield return FadeIn();
            yield return ApproachCrystal(crystal,towardAltar);
            Require(crystal.label.enabled&&crystal.label.alpha>.9f&&crystal.label.text.Contains(sameQuestion.Rule.ruleName),"nearby matching rule name is visible");
            Snapshot("04_TowerName");yield return ZoomButton(hand,1.5f);yield return Tap((RectTransform)hand.transform);
            Require(quiz.IsOpen&&!quiz.IsPractice&&!quiz.Answered,"actual hand opens the first-time gameplay pickup quiz");
            hand.transform.localScale=Vector3.one*handScale;veil.SetHoles();
            yield return Frames(66);Snapshot("04_Quiz");
            yield return Tap((RectTransform)quiz.AnswerButtons[quiz.Current.CorrectChoice].transform);
            Require(quiz.Answered&&quiz.FeedbackLabel.text.StartsWith("Correct"),"real shuffled-choice answer is correct");
            Log("Pickup quiz: "+quiz.Current.Question.Prompt+" -> "+quiz.Current.Question.Choices[quiz.Current.ChoiceOrder[quiz.Current.CorrectChoice]]);
            yield return Frames(48);
            veil.SetHoles(ScreenRect((RectTransform)quiz.CollectButton.transform,14),ScreenRect(quiz.FeedbackLabel.rectTransform,10));
            yield return Frames(24);Snapshot("04_Collect");
            Require(ButtonText(quiz.CollectButton)=="Collect Rule Crystal","record the current collection label");
            yield return Tap((RectTransform)quiz.CollectButton.transform);veil.SetHoles();
            Require(!quiz.IsOpen&&Player.LocalInstance.GetHeldObject()==crystal.GetComponent<GrabbableObject>()&&crystal.PickupQuizCompleted,"normal collection carries the same quiz-completed crystal");
            yield return Frames(28);Snapshot("04_Carrying");yield return Finish();
            AssertSame();

            // Cut the long return trip, retaining a real final approach with the same carried object.
            Position(Ground(altarPoint+Vector3.forward*10,altarPoint.y+4),altarPoint);
            SetCamera(altarPoint+new Vector3(14,13,28),altarPoint+Vector3.up*1.5f,50);
            Start(4);yield return FadeIn();yield return Walk(altarPoint,5.5f);yield return Frames(6);
            Require(view.gameObject.activeInHierarchy&&ButtonText(view)=="Submit to Altar","carrying the same crystal exposes Submit to Altar");
            yield return ZoomButton(view,1.65f);Snapshot("05_SubmitToAltar");yield return OpenBoard();
            Require(stage.SolvedRounds==0&&!stage.dragPuzzle.conclusion.Revealed&&Player.LocalInstance.GetHeldObject()==crystal.GetComponent<GrabbableObject>(),"Submit to Altar only opens the board; no automatic validation");
            view.transform.localScale=Vector3.one;
            Require(ButtonText(stage.placeButton)=="Place Rule Crystal"&&stage.placeButton.interactable,"current placement control is enabled");
            veil.SetHoles(ScreenRect((RectTransform)stage.placeButton.transform,14));
            yield return Focus(ScreenRect((RectTransform)stage.placeButton.transform,30),1.6f);yield return Frames(30);Snapshot("05_Place");
            yield return Tap((RectTransform)stage.placeButton.transform);veil.SetHoles();
            Require(stage.IsBoardOpen&&stage.SolvedRounds==1&&stage.PlacedCrystal==crystal&&stage.dragPuzzle.Solved&&stage.dragPuzzle.conclusion.Revealed,"normal Place Rule Crystal validates once and keeps the solved board open");
            Require(stage.dragPuzzle.conclusion.label.text.Contains(stage.dragPuzzle.Puzzle.Conclusion),"actual revealed conclusion matches the same argument");
            var revealed=(RectTransform)stage.dragPuzzle.conclusion.label.transform.parent;
            yield return Focus(TextRect(stage.dragPuzzle.conclusion.label,30),2.2f);
            veil.SetHoles(ScreenRect(revealed,14));
            int reading=Mathf.CeilToInt(Mathf.Clamp(2+stage.dragPuzzle.Puzzle.Conclusion.Split(' ').Length/3f,4,8)*24);
            yield return Frames(reading);Snapshot("05_RevealedConclusion");
            yield return Overview();veil.SetHoles();yield return Frames(22);yield return Finish();
            Require(stage.IsBoardOpen&&!stage.PuzzleCompleted&&stage.SolvedRounds==1,"end on the readable board; entire trial is not completed");
            Log("PASS: all five clips use question "+sameQuestion.Id+", rule "+sameQuestion.Rule.ruleName+" and its actual conclusion. One real premise check, one real pickup quiz/collection, one normal crystal validation. Board remained open; no trial completion or reward.");
            var manifest=new StageHelpVideoCatalog.Recording {scene="RulesOfInference",clips=Keys,
                source="One actual offline Unity test challenge. Physical walks and Input System touch events invoke normal book, board, word/blank, quiz, collection and placement controls. Long travel omitted. Stable recording-only cameras, UI magnification, spotlights and digital board close-ups. Every clip ends with 48 fade frames (2 seconds) at 24 fps. No saved progression or rewards.",
                cues=Captions.Select(c=>new StageHelpVideoCatalog.Cue{seconds=0,caption=c}).ToArray(),
                guides=guides.Select(c=>new StageHelpVideoCatalog.ClipGuide{cues=c.ToArray()}).ToArray()};
            File.WriteAllText(Output+Keys[0]+".json",JsonUtility.ToJson(manifest,true));
        }
        finally
        {
            encoder?.Dispose();encoder=null;Time.captureFramerate=0;
            if(pixels!=null)UnityEngine.Object.Destroy(pixels);if(zoomPixels!=null)UnityEngine.Object.Destroy(zoomPixels);
            if(zoomTexture!=null){zoomTexture.Release();UnityEngine.Object.Destroy(zoomTexture);}
            if(touch!=null&&touch.added)InputSystem.RemoveDevice(touch);
            if(overlay!=null)UnityEngine.Object.Destroy(overlay.gameObject);
        }
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
        if(segment!=0)File.WriteAllText(Output+Keys[segment]+".json",JsonUtility.ToJson(new StageHelpVideoCatalog.Recording {scene="RulesOfInference",source="Actual offline Unity Game view",cues=new[]{new StageHelpVideoCatalog.Cue{seconds=0,caption=Captions[segment]}}},true));
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
        worldCamera.ForceCameraPosition(camera.transform.position,camera.transform.rotation);
        var lens=worldCamera.Lens;lens.FieldOfView=camera.fieldOfView;worldCamera.Lens=lens;
        brain.enabled=true;
        yield return Tap((RectTransform)view.transform);veil.SetHoles();
        float until=Time.realtimeSinceStartup+10;
        while((!stage.IsBoardOpen||stage.boardInput.alpha<.99f||!stage.boardInput.interactable)&&Time.realtimeSinceStartup<until)yield return Frames(1);
        Require(stage.IsBoardOpen&&stage.boardInput.interactable&&stage.boardInput.alpha>.99f,"normal camera transition reveals interactive board");
    }
    static IEnumerator ApproachCrystal(InferenceCrystal target,Vector3 from)
    {
        var walk=new GameObject("Recording crystal approach").transform;walk.position=target.transform.position+from*3;
        Player.LocalInstance.BeginGuidedMovement(walk,5,.16f);int count=0;
        while(InferenceCrystalInteraction.Active.AvailableCrystal!=target&&count++<180)yield return Frames(1);
        Player.LocalInstance.EndGuidedMovement();UnityEngine.Object.Destroy(walk.gameObject);
        Require(InferenceCrystalInteraction.Active.AvailableCrystal==target,"physical approach reaches normal crystal pickup range");
        yield return Frames(12);
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
        foreach(var hit in hits)if(hit.normal.y>.6f&&hit.collider.GetComponentInParent<Player>()==null&&hit.collider.GetComponentInParent<InferenceCrystal>()==null&&!hit.collider.name.Contains("Ocean"))
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
    static void AssertSame(){Require(ReferenceEquals(stage.Question,sameQuestion),"same question identity across all clips");}
    static void Require(bool value,string text){if(!value)throw new InvalidOperationException(text);}
    static void Log(string text)=>File.AppendAllText(Evidence,text+"\n");
    static void Snapshot(string key)
    {
        if(pixels!=null)File.WriteAllBytes("Library/InferenceHelp_"+key+".png",(Mathf.Abs(crop.width-Screen.width)>1?zoomPixels:pixels).EncodeToPNG());
    }
    static void BuildOverlay()
    {
        overlay=new GameObject("Recording-only inference presentation",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)).GetComponent<Canvas>();
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

// Only the recording session adds this component; gameplay quiz layout and callbacks are untouched.
[DefaultExecutionOrder(32000)]
public sealed class InferenceRecordingQuizZoom : MonoBehaviour
{
    public InferenceCrystalQuiz Quiz;
    void LateUpdate()
    {
        if(Quiz==null||!Quiz.IsOpen)return;
        var card=typeof(InferenceCrystalQuiz).GetField("card",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(Quiz) as RectTransform;
        card.localScale=Vector3.one;
        var canvas=card.GetComponentInParent<Canvas>();float scale=canvas.scaleFactor;
        float zoom=Mathf.Min(1.35f,Mathf.Min((Screen.height-120)/(card.rect.height*scale),(Screen.width-100)/(card.rect.width*scale)));
        card.localScale=Vector3.one*zoom;card.anchoredPosition=new Vector2(0,-60/scale);
    }
}
#endif
