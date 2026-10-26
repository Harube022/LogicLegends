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

/// <summary>Five real gameplay shots from one offline attempt. All framing and overlays are Editor-only.</summary>
public static class TruthTableHelpClipCapture
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    const string Output = "Library/StageHelpCaptures/";
    const string Evidence = "Library/TruthTableHelpSequence.txt";
    public static readonly string[] Keys = { "TruthTable", "TruthTable_02_Table", "TruthTable_03_House", "TruthTable_04_Inventory", "TruthTable_05_Submit" };
    public static readonly string[] Captions = {
        "Approach the Book Statue and tap the hand button to begin.",
        "Read the expression and check the P and Q values in each row.",
        "Answer a house quiz to open the door.",
        "Collect True and False blocks from open houses, then select them from your inventory.",
        "Fill each row with the correct truth value, then tap Submit Column."
    };
    static T Read<T>(object owner, string key) => (T)owner.GetType().GetField(key, Private).GetValue(owner);
    static Camera camera;
    static Canvas overlay;
    static RecordingVeil veil;
    static Image fade;
    static RecordingTouchRing ring;
    static Button hand;
    static Touchscreen touch;
    static MediaEncoder encoder;
    static Texture2D pixels;
    static TruthTableDoorInteraction doors;
    static DynamicLogicPuzzle table;
    static TruthTableChallengePhase phase;
    static TruthTableChallengeQuestions.Question question;
    static TruthBlock[] blocks;
    static int finger, segment, frames;
    static bool capturing;
    static float handScale;
    static CanvasGroup controls;

    public static IEnumerator Record()
    {
        doors=UnityEngine.Object.FindFirstObjectByType<TruthTableDoorInteraction>();
        table=UnityEngine.Object.FindObjectsByType<DynamicLogicPuzzle>(FindObjectsSortMode.None).First(p=>Read<TruthTableDoorInteraction>(p,"houseDoors")==doors);
        phase=Read<TruthTableChallengePhase>(table,"challengePhase");
        question=Read<TruthTableChallengeQuestions.Question[,]>(phase,"selected")[0,0];
        camera=Camera.main;
        var brain=camera.GetComponent<Unity.Cinemachine.CinemachineBrain>(); if(brain!=null)brain.enabled=false;
        hand=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b=>b.name=="InteractButton");
        handScale=hand.transform.localScale.x;
        controls=hand.transform.parent.gameObject.AddComponent<CanvasGroup>();
        touch=InputSystem.AddDevice<Touchscreen>(); finger=800;
        BuildOverlay();
        overlay.gameObject.AddComponent<TruthTableRecordingQuizZoom>().Doors=doors;
        Time.captureFramerate=24;
        File.WriteAllText(Evidence,"Actual Game view capture, 24 fps. One offline attempt; no account identity, backend writes or completion rewards.\nExpression: "+question.Expression+"; row outputs: "+string.Join(",",Enumerable.Range(0,4).Select(i=>question.EvaluateRow(i)?"T":"F"))+"\n");
        try
        {
            var book=doors.TutorialBook;
            var start=book.position+new Vector3(2.7f,1.5f,12);
            Position(start,Vector3.back);
            SetCamera(book.position+new Vector3(11,11,23),book.position+Vector3.up*4);
            camera.fieldOfView=48;
            yield return Frames(4);
            Start(0); yield return FadeIn();
            yield return Walk(book.position+new Vector3(2.5f,1.5f,2.5f),5);
            yield return ZoomHand();
            Snapshot("01_Hand");
            yield return Tap(hand);
            Require(doors.IsStageRunning,"real hand touch starts the nearby book");
            yield return Frames(28);
            yield return Finish();
            hand.transform.localScale=Vector3.one*handScale; veil.SetHoles();
            blocks=TruthBlock.ActiveBlocks.Where(b=>b.UsesHandBubble).OrderBy(b=>Vector3.Distance(b.transform.position,book.position)).ToArray();
            Require(blocks.Length==4,"exactly four real spawned blocks");
            foreach(var b in blocks)Log("Spawned "+b.GetInstanceID()+" "+b.value+" at "+b.transform.position+" house "+doors.HouseForBlock(b));
            yield return Frames(65); // Trim Start! and the ordinary pause between actions.

            controls.alpha=0;
            var avatar=Player.LocalInstance.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray();
            foreach(var r in avatar)r.enabled=false;
            AddInputContrast();
            var hiddenBubbles=UnityEngine.Object.FindObjectsByType<HandThoughtBubble>(FindObjectsSortMode.None).Select(b=>b.GetComponent<Canvas>()).Where(c=>c.enabled).ToArray();
            foreach(var c in hiddenBubbles)c.enabled=false;
            SetCamera(new Vector3(641.7f,154.5f,774),new Vector3(641.7f,153.3f,742.6f)); camera.fieldOfView=39;
            Start(1); yield return FadeIn();
            yield return CameraTo(new Vector3(641.7f,153.3f,766),new Vector3(641.7f,153.3f,742.6f),36);
            var inputs=new Bounds(new Vector3(640.7f,152.6f,746),new Vector3(7.1f,13.6f,.1f));
            var expression=Read<TMP_Text>(table,"col1HeaderLabel");
            veil.SetHoles(WorldRect(inputs,12),ScreenRect(expression.rectTransform,12));
            yield return Frames(100); Snapshot("02_Table"); yield return Finish();
            foreach(var r in avatar)r.enabled=true; foreach(var c in hiddenBubbles)c.enabled=true; controls.alpha=1; veil.SetHoles();

            int firstHouse=doors.HouseForBlock(blocks[0]);
            PlaceAtDoor(firstHouse,8);
            FrameDoor(firstHouse);
            Start(2); yield return FadeIn();
            yield return ApproachDoor(firstHouse);
            yield return ZoomHand();
            yield return Tap(hand); Require(doors.IsQuizOpen,"actual hand opens a closed house quiz");
            hand.transform.localScale=Vector3.one*handScale; veil.SetHoles();
            yield return Frames(50); Snapshot("03_Quiz");
            yield return AnswerQuiz(firstHouse);
            yield return Frames(26); Snapshot("03_OpenDoor"); yield return Finish();

            // Omit long travel and repeated quizzes. All doors still open through their real quiz callbacks.
            foreach(int house in blocks.Select(b=>doors.HouseForBlock(b)).Distinct().Where(h=>h!=firstHouse))
            {
                PlaceAtDoor(house,3);
                yield return Frames(4); yield return Tap(hand);
                Require(doors.IsQuizOpen,"remaining house quiz uses normal proximity and hand");
                yield return AnswerQuiz(house);
            }
            Require(Read<bool[]>(doors,"answered").Count(a=>a)==blocks.Select(b=>doors.HouseForBlock(b)).Distinct().Count(),"only required houses answered");
            Log("Repeated trips/quizzes trimmed; all required doors were unlocked through actual answer buttons.");

            Start(3);
            for(int i=0;i<blocks.Length;i++)
            {
                capturing=false; fade.color=Color.black; veil.SetHoles();
                var block=blocks[i]; int house=doors.HouseForBlock(block); var normal=DoorOutward(house);
                var at=block.transform.position;
                Position(Ground(at+normal*4.6f,at.y+1),-normal);
                SetCamera(at+normal*9+Vector3.Cross(Vector3.up,normal)*3+Vector3.up*5,at+Vector3.up*.6f); camera.fieldOfView=52;
                yield return Frames(3); capturing=true; yield return FadeIn();
                yield return ApproachBlock(block,normal);
                yield return ZoomHand(14);
                Require(doors.NearestAvailableBlock()==block,"shown block is the actual eligible hand target");
                yield return Tap(hand);
                Require(Slots().Any(s=>s.physicalBlocks.Contains(block)),"real hand collection stored the displayed block exactly once");
                Log("Collected "+block.GetInstanceID()+" through real hand; inventory "+InventoryText());
                hand.transform.localScale=Vector3.one*handScale; veil.SetHoles();
                yield return Frames(10); Snapshot("04_Collected_"+i);
                if(i<blocks.Length-1)yield return MiniFade();
            }
            var inventory=(RectTransform)Read<GameObject>(InventoryManager.Instance,"inventoryBarUI").transform;
            var inventoryFocusControls=UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None)
                .Where(r=>r.name=="Joystick"||r.name=="JumpButton"||r.name=="InteractButton")
                .Select(r=>r.gameObject.AddComponent<CanvasGroup>()).ToArray();
            foreach(var group in inventoryFocusControls)group.alpha=0;
            var invScale=inventory.localScale;
            var invPosition=inventory.anchoredPosition;
            for(int i=0;i<24;i++)
            {
                inventory.localScale=Vector3.Lerp(invScale,invScale*1.65f,Mathf.SmoothStep(0,1,(i+1)/24f));
                inventory.anchoredPosition=invPosition+Vector2.up*Mathf.Lerp(0,110,Mathf.SmoothStep(0,1,(i+1)/24f));
                veil.SetHoles(ScreenRect(inventory,14)); yield return Frames(1);
            }
            foreach(var value in new[]{true,false})
            {
                int slot=Array.FindIndex(Slots(),s=>!s.isEmpty&&s.blockValue==value);
                Require(slot>=0,"both True and False appear in real inventory");
                if(Read<int>(InventoryManager.Instance,"selectedSlotIndex")!=slot)yield return Tap(SlotButton(slot));
                yield return Frames(20);
            }
            Snapshot("04_Inventory");
            Require(Slots().Sum(s=>s.count)==4,"all four unique blocks were collected");
            yield return Finish(); inventory.localScale=invScale; inventory.anchoredPosition=invPosition; veil.SetHoles();
            foreach(var group in inventoryFocusControls)group.alpha=1;

            foreach(var c in UnityEngine.Object.FindObjectsByType<HandThoughtBubble>(FindObjectsSortMode.None).Select(b=>b.GetComponent<Canvas>()))c.enabled=false;
            Position(Ground(new Vector3(635.3f,0,746.2f),154),Vector3.back);
            yield return Frames(8);
            SetCamera(new Vector3(643.8f,153.4f,770),new Vector3(641,151.8f,743)); camera.fieldOfView=44;
            Start(4); yield return FadeIn();
            Player.LocalInstance.transform.rotation=Quaternion.Euler(0,180,0);
            for(int row=0;row<4;row++)
            {
                Require(phase.ColumnNumber==1,"same active column before submission");
                bool expected=question.EvaluateRow(row);
                int slot=Array.FindIndex(Slots(),s=>!s.isEmpty&&s.blockValue==expected);
                Require(slot>=0,"real inventory contains the value needed for row "+(row+1));
                if(Read<int>(InventoryManager.Instance,"selectedSlotIndex")!=slot)yield return Tap(SlotButton(slot));
                yield return Frames(6);
                var dropping=Slots()[slot].physicalBlocks.Last();
                veil.SetHoles(ScreenRect((RectTransform)hand.transform,12));
                yield return Tap(hand); veil.SetHoles();
                int elapsed=0;
                while(Read<int>(phase,"currentRow")!=row+1&&elapsed++<55)yield return Frames(1);
                Require(Read<int>(phase,"currentRow")==row+1&&dropping.IsPlacedInColumn,"real dropped block enters the column trigger for row "+(row+1));
                Log("Row "+(row+1)+" physically dropped "+dropping.GetInstanceID()+" "+expected+"; snapped by the normal DynamicLogicSlot trigger.");
                yield return Frames(15); Snapshot("05_Row_"+row);
            }
            var submit=Read<Button>(table,"submitColumnButton");
            Require(submit.gameObject.activeInHierarchy&&submit.interactable,"real Submit Column appears after four placements");
            veil.SetHoles(ScreenRect((RectTransform)submit.transform,14)); yield return Frames(28); Snapshot("05_Submit");
            yield return Tap(submit); veil.SetHoles();
            Require(phase.ColumnNumber==2&&phase.ChallengeNumber==1,"real validation advances exactly one column");
            Require(Read<TMP_Text>(table,"columnFeedbackText").text=="Correct","actual submission shows correct feedback");
            yield return Frames(28); Snapshot("05_Feedback");
            yield return Finish();
            Log("PASS: five actual clips, one expression until its real submission; four unique blocks, real quizzes, physical drops, exactly one correct column. No trial completion or reward.");
            var manifest=new StageHelpVideoCatalog.Recording {
                scene="TruthTable",clips=Keys,
                source="One continuous offline Unity gameplay test attempt. Actual hand/quiz/inventory/Submit touches and physical block drops. Long travel and repeated house quizzes omitted between focused shots. Recording-only fixed cameras, UI zoom, spotlights and P/Q legibility overlays. Each clip ends with a 48-frame (2-second) fade at 24 fps. No player account writes.",
                cues=Captions.Select(c=>new StageHelpVideoCatalog.Cue{seconds=0,caption=c}).ToArray()
            };
            File.WriteAllText(Output+Keys[0]+".json",JsonUtility.ToJson(manifest,true));
        }
        finally
        {
            encoder?.Dispose(); encoder=null;
            if(pixels!=null)UnityEngine.Object.Destroy(pixels);
            Time.captureFramerate=0;
            if(touch!=null&&touch.added)InputSystem.RemoveDevice(touch);
            if(overlay!=null)UnityEngine.Object.Destroy(overlay.gameObject);
        }
    }
    static void Start(int index)
    {
        segment=index;frames=0;fade.color=Color.black;capturing=true;
        // Use Unity's real Game view encoder directly so off-camera travel can be trimmed without advancing recorded frames.
        if(File.Exists(Output+Keys[index]+".json"))File.Delete(Output+Keys[index]+".json");
        encoder=new MediaEncoder(Path.GetFullPath(Output+Keys[index]+".mp4"),new VideoTrackAttributes {
            frameRate=new MediaRational(24),width=(uint)Screen.width,height=(uint)Screen.height,
            includeAlpha=false,bitRateMode=VideoBitrateMode.High});
        if(pixels==null)pixels=new Texture2D(Screen.width,Screen.height,TextureFormat.RGBA32,false);
    }
    static IEnumerator Frames(int count)
    {
        for(int i=0;i<count;i++)
        {
            yield return new WaitForEndOfFrame();
            if(encoder!=null&&capturing)
            {
                pixels.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);pixels.Apply();
                Require(encoder.AddFrame(pixels),"Unity encoded actual Game view frame"); frames++;
            }
        }
    }
    static IEnumerator Finish()
    {
        if(segment<4)Require(phase.ColumnNumber==1&&phase.ChallengeNumber==1&&Read<TruthTableChallengeQuestions.Question[,]>(phase,"selected")[0,0]==question,"expression and progression remain consistent");
        for(int i=0;i<48;i++){fade.color=new Color(0,0,0,(i+1)/48f);yield return Frames(1);}
        yield return Frames(3);
        encoder.Dispose();encoder=null;
        Log(Keys[segment]+": "+frames+" frames, "+(frames/24f).ToString("F2")+" seconds; final fade exactly 48 frames / 2 seconds, then 3 black hold frames.");
        if(segment!=0)File.WriteAllText(Output+Keys[segment]+".json",JsonUtility.ToJson(new StageHelpVideoCatalog.Recording {
            scene="TruthTable",source="Actual Unity gameplay, offline test attempt",cues=new[]{new StageHelpVideoCatalog.Cue{seconds=0,caption=Captions[segment]}}},true));
    }
    static IEnumerator FadeIn(){for(int i=0;i<8;i++){fade.color=new Color(0,0,0,1-(i+1)/8f);yield return Frames(1);}}
    static IEnumerator MiniFade(){for(int i=0;i<8;i++){fade.color=new Color(0,0,0,(i+1)/8f);yield return Frames(1);}}
    static IEnumerator ZoomHand(int count=28)
    {
        for(int i=0;i<count;i++)
        {
            hand.transform.localScale=Vector3.one*Mathf.Lerp(handScale,handScale*1.48f,Mathf.SmoothStep(0,1,(i+1)/(float)count));
            veil.SetHoles(ScreenRect((RectTransform)hand.transform,12));yield return Frames(1);
        }
        yield return Frames(7);
    }
    static IEnumerator AnswerQuiz(int house)
    {
        var assigned=Read<TruthTableDoorQuestionBank.Question[]>(doors,"assignedQuestions")[house];
        var buttons=Read<Button[]>(doors,"answerButtons");
        yield return Tap(buttons[assigned.correctChoice]);
        Require(Read<bool[]>(doors,"answered")[house],"real quiz answer recorded once");
        Log("House "+house+" real quiz: "+assigned.prompt+" -> "+assigned.choices[assigned.correctChoice]);
        float until=Time.realtimeSinceStartup+8;while(doors.IsQuizOpen&&Time.realtimeSinceStartup<until)yield return Frames(1);
        Require(!doors.IsQuizOpen&&Read<bool[]>(doors,"doorOpened")[house],"quiz feedback closes and the actual door finishes opening");
    }
    static IEnumerator ApproachDoor(int house){yield return Walk(DoorPoint(house,3),4.5f);}
    static IEnumerator ApproachBlock(TruthBlock block,Vector3 normal)
    {
        var target=new GameObject("Recording block approach").transform;target.position=block.transform.position+normal;
        Player.LocalInstance.BeginGuidedMovement(target,4.5f,.16f);
        int count=0;while(doors.NearestAvailableBlock()!=block&&count++<100)yield return Frames(1);
        Player.LocalInstance.EndGuidedMovement();UnityEngine.Object.Destroy(target.gameObject);
        Require(doors.NearestAvailableBlock()==block,"physical approach reaches the normal pickup range");
    }
    static Vector3 DoorOutward(int house){var n=table.transform.position-doors.GetTutorialDoor(house).bounds.center;n.y=0;return n.normalized;}
    static Vector3 DoorPoint(int house,float distance){var b=doors.GetTutorialDoor(house).bounds;return Ground(b.center+DoorOutward(house)*distance,b.min.y+1.6f);}
    static void PlaceAtDoor(int house,float distance){Position(DoorPoint(house,distance),-DoorOutward(house));}
    static void FrameDoor(int house){var b=doors.GetTutorialDoor(house).bounds;SetCamera(b.center+DoorOutward(house)*22+Vector3.up*6,b.center);camera.fieldOfView=48;}
    static Vector3 Ground(Vector3 point,float ceiling)
    {
        RaycastHit hit;if(Physics.Raycast(new Vector3(point.x,ceiling,point.z),Vector3.down,out hit,12,~0,QueryTriggerInteraction.Ignore))point.y=hit.point.y+.12f;
        else point.y=ceiling-2;
        return point;
    }
    static void Position(Vector3 point,Vector3 forward){PrelimTeleportTransition.MovePlayerImmediately(Player.LocalInstance.gameObject,point,Quaternion.LookRotation(forward));}
    static IEnumerator Walk(Vector3 to,float speed)
    {
        var target=new GameObject("Recording walk destination").transform;target.position=to;
        Player.LocalInstance.BeginGuidedMovement(target,speed,.16f);
        int limit=0;while(Vector2.Distance(XZ(Player.LocalInstance.transform.position),XZ(to))>.23f&&limit++<200)yield return Frames(1);
        Player.LocalInstance.EndGuidedMovement();UnityEngine.Object.Destroy(target.gameObject);
        Require(Vector2.Distance(XZ(Player.LocalInstance.transform.position),XZ(to))<.4f,"physical walk unobstructed to "+to+"; player "+Player.LocalInstance.transform.position);
    }
    static Vector2 XZ(Vector3 p)=>new Vector2(p.x,p.z);
    static InventorySlot[] Slots()=>Read<InventorySlot[]>(InventoryManager.Instance,"slots");
    static string InventoryText()=>string.Join(" ",Slots().Select(s=>s.isEmpty?"empty":(s.blockValue?"True":"False")+" x"+s.count));
    static Button SlotButton(int index)=>UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b=>b.name=="Slot_"+index);
    static void SetCamera(Vector3 at,Vector3 target)=>camera.transform.SetPositionAndRotation(at,Quaternion.LookRotation(target-at));
    static IEnumerator CameraTo(Vector3 at,Vector3 target,int count)
    {
        var from=camera.transform.position;var rotation=camera.transform.rotation;var end=Quaternion.LookRotation(target-at);
        for(int i=0;i<count;i++){float t=Mathf.SmoothStep(0,1,(i+1)/(float)count);camera.transform.SetPositionAndRotation(Vector3.Lerp(from,at,t),Quaternion.Slerp(rotation,end,t));yield return Frames(1);}
    }
    static IEnumerator Tap(Button button)
    {
        Canvas.ForceUpdateCanvases();yield return Frames(1);
        var point=ScreenRect((RectTransform)button.transform,0).center;
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);
        Require(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"actual touch raycast reaches "+button.name);
        var rt=(RectTransform)ring.transform;rt.anchoredPosition=point*(1280f/Screen.width)-new Vector2(640,360);rt.sizeDelta=Vector2.one*58;ring.color=new Color(1,.8f,.15f,.9f);
        int id=++finger;InputSystem.QueueStateEvent(touch,new TouchState{touchId=id,phase=UnityEngine.InputSystem.TouchPhase.Began,position=point});
        yield return Frames(3);
        InputSystem.QueueStateEvent(touch,new TouchState{touchId=id,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=point});
        for(int i=0;i<10;i++){rt.sizeDelta=Vector2.one*(58+i*5);ring.color=new Color(1,.8f,.15f,.8f*(1-i/9f));yield return Frames(1);}
    }
    static Rect ScreenRect(RectTransform target,float padding)
    {
        var canvas=target.GetComponentInParent<Canvas>();var cam=canvas!=null&&canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:camera;
        var corners=new Vector3[4];target.GetWorldCorners(corners);var p=corners.Select(v=>RectTransformUtility.WorldToScreenPoint(cam,v)).ToArray();
        return Rect.MinMaxRect(p.Min(v=>v.x)-padding,p.Min(v=>v.y)-padding,p.Max(v=>v.x)+padding,p.Max(v=>v.y)+padding);
    }
    static Rect WorldRect(Bounds b,float padding)
    {
        var p=new List<Vector3>();for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)p.Add(camera.WorldToScreenPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z))));
        return Rect.MinMaxRect(p.Min(v=>v.x)-padding,p.Min(v=>v.y)-padding,p.Max(v=>v.x)+padding,p.Max(v=>v.y)+padding);
    }
    static void AddInputContrast()
    {
        // Recording-only legibility overlays at the existing embossed P/Q inputs; values match EvaluateRow's TT, TF, FT, FF order.
        float[] ys={155.5f,153.1f,150.6f,148.1f};
        for(int col=0;col<2;col++)for(int row=-1;row<4;row++)
        {
            var label=new GameObject("Recording input contrast",typeof(TextMeshPro)).GetComponent<TextMeshPro>();
            label.font=Read<TMP_Text>(table,"col1HeaderLabel").font;label.fontSize=12;label.color=Color.white;label.alignment=TextAlignmentOptions.Center;
            label.outlineColor=Color.black;label.outlineWidth=.2f;
            label.text=row<0?(col==0?"P":"Q"):((col==0?row<2:row==0||row==2)?"T":"F");
            label.rectTransform.sizeDelta=new Vector2(3,2);
            label.transform.SetPositionAndRotation(new Vector3(col==0?642.0f:639.2f,row<0?157.7f:ys[row],746.1f),Quaternion.Euler(0,180,0));
        }
    }
    static void BuildOverlay()
    {
        overlay=new GameObject("Recording-only presentation",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)).GetComponent<Canvas>();
        overlay.renderMode=RenderMode.ScreenSpaceOverlay;overlay.sortingOrder=20000;
        var scaler=overlay.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);
        veil=NewRect("Recording spotlight").gameObject.AddComponent<RecordingVeil>();veil.raycastTarget=false;veil.color=new Color(.01f,.015f,.025f,.67f);
        var r=NewRect("Actual touch visualization");r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);ring=r.gameObject.AddComponent<RecordingTouchRing>();ring.raycastTarget=false;ring.color=Color.clear;
        fade=NewRect("Two-second fade").gameObject.AddComponent<Image>();fade.raycastTarget=false;fade.color=Color.black;
    }
    static RectTransform NewRect(string name)
    {
        var r=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer)).GetComponent<RectTransform>();r.SetParent(overlay.transform,false);
        r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;return r;
    }
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    static void Log(string text)=>File.AppendAllText(Evidence,text+"\n");
    static void Snapshot(string key)=>ScreenCapture.CaptureScreenshot("Library/TruthTableHelp_"+key+".png");
}

// The real quiz retains its actual buttons and listeners; only this test recording magnifies the card.
[DefaultExecutionOrder(32000)]
public sealed class TruthTableRecordingQuizZoom : MonoBehaviour
{
    public TruthTableDoorInteraction Doors;
    void LateUpdate()
    {
        if(Doors==null||!Doors.IsQuizOpen)return;
        var field=typeof(TruthTableDoorInteraction).GetField("quizCard",BindingFlags.Instance|BindingFlags.NonPublic);
        var card=field.GetValue(Doors) as RectTransform;
        if(card==null)return;
        var points=new Vector3[4];card.GetWorldCorners(points);
        var canvas=card.GetComponentInParent<Canvas>();
        var cam=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
        float height=Vector2.Distance(RectTransformUtility.WorldToScreenPoint(cam,points[0]),RectTransformUtility.WorldToScreenPoint(cam,points[1]));
        float width=Vector2.Distance(RectTransformUtility.WorldToScreenPoint(cam,points[0]),RectTransformUtility.WorldToScreenPoint(cam,points[3]));
        float zoom=Mathf.Min(1.55f,Mathf.Min((Screen.height-80)/height,(Screen.width-100)/width));
        card.localScale*=Mathf.Max(1,zoom);
    }
}

#endif
