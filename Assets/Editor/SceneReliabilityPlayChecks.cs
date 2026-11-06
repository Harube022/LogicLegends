#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

/// <summary>Saved-scene regression: touch ownership, camera leases, rapid board exits and real capsule sweeps.</summary>
public static class SceneReliabilityPlayChecks
{
    public const string Report="Library/SceneReliabilityPlayChecks.txt";
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static Touchscreen touch;static int finger;
    static T Find<T>() where T:UnityEngine.Object=>UnityEngine.Object.FindFirstObjectByType<T>();
    static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,Hidden).GetValue(target);
    static void Check(bool pass,string text){if(!pass)throw new InvalidOperationException(text);File.AppendAllText(Report,"PASS "+text+"\n");}
    static Rect Bounds(RectTransform rect){var v=new Vector3[4];rect.GetWorldCorners(v);return Rect.MinMaxRect(v.Min(p=>p.x),v.Min(p=>p.y),v.Max(p=>p.x),v.Max(p=>p.y));}
    static void Move(Vector3 at)
    {var cc=Player.LocalInstance.GetComponent<CharacterController>();cc.enabled=false;Player.LocalInstance.transform.position=at;cc.enabled=true;Physics.SyncTransforms();MobileInputUI.FindJoystick()?.ResetJoystick();}
    static void Near(Transform target,float distance)
    {var p=target.position+target.forward*distance;p.y=Player.LocalInstance.transform.position.y;Move(p);}
    public static IEnumerator Run(string scene)
    {
        if(scene=="PRELIM"||!File.Exists(Report))File.WriteAllText(Report,"Isolated Editor Play Mode. Real Input System touch events and player capsule; no real accounts, rewards or progression writes. Android landscape orientation; on-device testing unavailable (no ADB devices).\n");
        touch=InputSystem.AddDevice<Touchscreen>();finger=800;
        try
        {
            yield return new WaitForSecondsRealtime(1);
            Check(!GameInput.Instance.GameplayInputBlocked&&Player.LocalInstance.enabled,scene+": preparation releases controls");
            yield return PointerOwnership(scene);
            yield return NestedCameras(scene);
            foreach(int size in new[]{2,4,6,12})
            {
                yield return Size(size);
                yield return GestureFiltering(scene);
                CheckControls(scene);
                yield return Board(scene,size==2);
            }
            yield return Size(6);
            yield return Capsule(scene);
            if(scene=="TruthTable")
            {
                yield return TruthTablePlatformStairsChecks.Run();
                foreach(string method in new[]{"Paths","Stairs"})
                    yield return (IEnumerator)typeof(TrialInteractionChecks).GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
            }
            if(scene=="RulesOfInference")yield return AltarStairs();
            File.AppendAllText(Report,"SCENE PASSED "+scene+"\n");
        }
        finally
        {MobileInputUI.FindJoystick()?.ResetJoystick();MobileLookInput.CancelAll();if(touch!=null)InputSystem.RemoveDevice(touch);Time.timeScale=1;BookMapMobileChecks.SetSize(6);}
    }
    static IEnumerator Size(int index)
    {
        BookMapMobileChecks.SetSize(index);int width=index==2?800:index==4?1280:index==6?1920:2960;int height=index==2?480:index==4?720:index==6?1080:1440;
        float until=Time.realtimeSinceStartup+6;
        do{yield return null;}while((Screen.width!=width||Screen.height!=height)&&Time.realtimeSinceStartup<until);
        Check(Screen.width==width&&Screen.height==height,"settled landscape "+width+"x"+height);
        yield return null;yield return null;yield return new WaitForSecondsRealtime(.2f);
    }
    static IEnumerator PointerOwnership(string scene)
    {
        var input=GameInput.Instance;var ui=MobileInputUI.FindJoystick();Check(ui!=null,scene+": configured joystick found");
        var rect=Field<RectTransform>(ui,"joystickBackground");var center=Bounds(rect).center;
        var first=new PointerEventData(EventSystem.current){pointerId=401,position=center+Vector2.right*Bounds(rect).width*.3f};
        var other=new PointerEventData(EventSystem.current){pointerId=402,position=center+Vector2.left*Bounds(rect).width*.3f};
        ui.OnPointerDown(first);Vector2 before=ui.CurrentInput;Check(before.x>.1f,scene+": first finger owns joystick");
        ui.OnPointerDown(other);ui.OnDrag(other);ui.OnPointerUp(other);
        Check(ui.CurrentInput==before,scene+": second finger cannot steal or release joystick");
        input.SetGameplayInputBlocked(true);Check(ui.CurrentInput==Vector2.zero,scene+": modal clears held joystick");
        input.SetGameplayInputBlocked(false);Check(input.GetMovementVectorNormalized()==Vector2.zero,scene+": closing modal cannot revive stale movement");
        ui.OnDrag(first);Check(ui.CurrentInput==Vector2.zero,scene+": old drag requires a new pointer down");
        ui.OnPointerDown(first);var go=ui.gameObject;go.SetActive(false);go.SetActive(true);
        Check(ui.CurrentInput==Vector2.zero&&input.GetMovementVectorNormalized()==Vector2.zero,scene+": hidden control clears drag without pointer-up");
        var look=Find<MobileLookInput>();if(look!=null)
        {
            look.OnPointerDown(first);other.position=first.position+Vector2.right*100;look.OnDrag(other);
            Check(MobileLookInput.LookDelta==Vector2.zero,scene+": look ignores another finger");
            input.SetGameplayInputBlocked(true);input.SetGameplayInputBlocked(false);first.position+=Vector2.right*100;look.OnDrag(first);
            Check(MobileLookInput.LookDelta==Vector2.zero,scene+": modal cancels look ownership");
        }
        yield return null;
    }
    static IEnumerator NestedCameras(string scene)
    {
        var brain=Camera.main.GetComponent<CinemachineBrain>();if(brain==null)yield break;
        var blend=brain.DefaultBlend;var custom=brain.CustomBlends;bool unscaled=brain.IgnoreTimeScale;
        for(int repeat=0;repeat<2;repeat++)
        {
            var a=PrelimTutorialCameraRig.Create("QA nested camera A");var b=PrelimTutorialCameraRig.Create("QA nested camera B");
            a.FinishReturn();
            Check(brain.IgnoreTimeScale&&brain.CustomBlends==null&&Mathf.Abs(brain.DefaultBlend.Time-PrelimUIPresentation.CameraTransitionSeconds)<.001f,scene+": out-of-order camera cleanup retains active transition settings");
            b.FinishReturn();yield return null;
            Check(brain.DefaultBlend.Time==blend.Time&&brain.DefaultBlend.Style==blend.Style&&brain.CustomBlends==custom&&brain.IgnoreTimeScale==unscaled&&!PrelimTutorialCameraRig.HasActiveView,scene+": final camera restores original blend and time mode");
        }
        int callbacks=0;var rig=PrelimTutorialCameraRig.Create("QA rapid return");rig.ReturnToGameplay(()=>callbacks++);rig.ReturnToGameplay(()=>callbacks++);rig.FinishReturn();rig.FinishReturn();yield return null;
        Check(callbacks==1&&!PrelimTutorialCameraRig.HasActiveView,scene+": rapid return runs cleanup once");
        yield return new WaitForSecondsRealtime(1);
    }
    static IEnumerator GestureFiltering(string scene)
    {
        var buttons=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
        var hand=buttons.First(b=>b.name=="InteractButton");var jump=buttons.First(b=>b.name=="JumpButton");
        Vector2 a=Bounds((RectTransform)hand.transform).center,b=Bounds((RectTransform)jump.transform).center;
        Check(!GameplayTouchRouting.IsLookSurface(a)&&!GameplayTouchRouting.IsLookSurface(b),scene+": hand and jump are not camera gesture surfaces");
        var zoom=UnityEngine.Object.FindObjectsByType<CinemachinePinchZoom>(FindObjectsSortMode.None).FirstOrDefault(z=>z.enabled&&CinemachineCore.IsLive(z.GetComponent<CinemachineCamera>()));
        var follow=zoom!=null?zoom.GetComponent<CinemachineThirdPersonFollow>():null;float distance=follow!=null?follow.CameraDistance:0;
        int one=++finger,two=++finger;
        Queue(one,UnityEngine.InputSystem.TouchPhase.Began,a);Queue(two,UnityEngine.InputSystem.TouchPhase.Began,b);yield return null;yield return null;
        Check(GameplayTouchRouting.CameraTouchCount==0,scene+": two simultaneous button touches do not pinch");
        Queue(one,UnityEngine.InputSystem.TouchPhase.Moved,a+Vector2.right*3);Queue(two,UnityEngine.InputSystem.TouchPhase.Moved,b-Vector2.right*3);yield return null;yield return null;
        Check(follow==null||follow.CameraDistance==distance,scene+": button touches preserve authored camera distance");
        var outside=new Vector2(Screen.width*.48f,Screen.height*.48f);
        Queue(one,UnityEngine.InputSystem.TouchPhase.Ended,outside);Queue(two,UnityEngine.InputSystem.TouchPhase.Ended,outside);yield return null;yield return null;
        MobileLookInput.CancelAll();
        Check(!GameInput.Instance.GameplayInputBlocked&&Player.LocalInstance.enabled,scene+": multitouch leaves gameplay usable");
        var lookPoints=new List<Vector2>();
        for(float x=.56f;x<.94f;x+=.08f)for(float y=.3f;y<.85f;y+=.12f)
        {var point=new Vector2(Screen.width*x,Screen.height*y);if(GameplayTouchRouting.IsLookSurface(point))lookPoints.Add(point);}
        Check(lookPoints.Count>=2,scene+": unobstructed camera look area remains available");
        a=lookPoints[0];b=lookPoints.Last();one=++finger;two=++finger;
        Queue(one,UnityEngine.InputSystem.TouchPhase.Began,a);Queue(two,UnityEngine.InputSystem.TouchPhase.Began,b);yield return null;yield return null;
        Check(GameplayTouchRouting.CameraTouchCount==2,scene+": genuine two-finger look gesture is recognized");
        Check(follow==null||follow.CameraDistance==distance,scene+": pinch start does not jump the camera");
        Queue(one,UnityEngine.InputSystem.TouchPhase.Moved,a-Vector2.right*8);Queue(two,UnityEngine.InputSystem.TouchPhase.Moved,b+Vector2.right*8);yield return null;yield return null;
        if(follow!=null)Check(Mathf.Abs(follow.CameraDistance-distance)>.001f&&Mathf.Abs(follow.CameraDistance-distance)<1,scene+": genuine pinch zoom is smooth and still functional");
        Queue(one,UnityEngine.InputSystem.TouchPhase.Ended,a);Queue(two,UnityEngine.InputSystem.TouchPhase.Ended,b);yield return null;yield return null;
        if(follow!=null)follow.CameraDistance=distance;MobileLookInput.CancelAll();
    }
    static void Queue(int id,UnityEngine.InputSystem.TouchPhase phase,Vector2 position)
        =>InputSystem.QueueStateEvent(touch,new TouchState{touchId=id,phase=phase,position=position});
    static void CheckControls(string scene)
    {
        var safe=Screen.safeArea;
        if(safe.width<1||safe.height<1||safe.xMax>Screen.width+1||safe.yMax>Screen.height+1)safe=new Rect(0,0,Screen.width,Screen.height);
        var joystick=MobileInputUI.FindJoystick();var controls=new List<RectTransform>{Field<RectTransform>(joystick,"joystickBackground")};
        foreach(var button in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
        {
            if(!button.IsInteractable())continue;
            if(button.name=="JumpButton"||button.name=="InteractButton")controls.Add((RectTransform)button.transform);
        }
        foreach(var control in controls)
        {
            var rect=Bounds(control);
            Check(rect.xMin>=safe.xMin-2&&rect.xMax<=safe.xMax+2&&rect.yMin>=safe.yMin-2&&rect.yMax<=safe.yMax+2,scene+": "+control.name+" fits mobile safe area at "+Screen.width+"x"+Screen.height+" bounds="+rect+" safe="+safe);
            Check(rect.width>=40&&rect.height>=40,scene+": "+control.name+" remains tappable");
        }
        for(int i=0;i<controls.Count;i++)for(int j=i+1;j<controls.Count;j++)Check(!Bounds(controls[i]).Overlaps(Bounds(controls[j])),scene+": mobile controls do not overlap");
    }
    sealed class BoardCase { public Button open,back;public Func<bool> showing;public Func<Bounds> frame;public Action prepare; }
    static BoardCase BoardFor(string scene)
    {
        if(scene=="Functions and Relations") {var s=Find<FunctionsRelationsStageManager>();return new BoardCase{open=s.ViewBoardButton,back=s.BoardView.backButton,showing=()=>s.BoardView.Viewing,frame=()=>s.BoardView.BoardBounds};}
        if(scene=="Formal vs. Informal Proofs") {var s=Find<ProofsLibraryStageController>();return new BoardCase{open=s.Presentation.ViewButton,back=s.Presentation.BackButton,showing=()=>s.Presentation.BoardView.Viewing,frame=()=>s.Presentation.BoardView.BoardBounds};}
        if(scene=="Direct&Indirect") {var s=Find<DirectProofsWorldGuidance>();return new BoardCase{open=s.ViewButton,back=s.BackButton,showing=()=>s.BoardView.Viewing,frame=()=>s.BoardView.BoardBounds,prepare=()=>Near(Field<Transform>(s,"boardArea"),6)};}
        if(scene=="Induction") {var s=Find<LogicLegends.Induction.InductionStageManager>();return new BoardCase{open=s.ui.ViewBoardButton,back=s.ui.BoardBackButton,showing=()=>s.boardView.Viewing,prepare=()=>Near(s.stations[0],6)};}
        if(scene=="Setssss") {var s=Find<SetssssBoardView>();return new BoardCase{open=Field<Button>(s,"viewBoardButton"),back=Field<Button>(s,"backButton"),showing=()=>s.IsViewingBoard,prepare=()=>Near(Field<Collider>(s,"spawnerCollider").transform,4)};}
        if(scene=="Graph") {var s=Find<LogicLegends.Graph.GraphStageManager>();return new BoardCase{open=s.BoardView.ViewButton,back=s.BoardView.BackButton,showing=()=>s.BoardView.IsOpen};}
        return null;
    }
    static IEnumerator Board(string scene,bool early)
    {
        var item=BoardFor(scene);
        if(item==null)
        {
            if(scene=="RulesOfInference")yield return InferenceBoard(early);
            if(scene=="Trees")yield return TreeBoard(early);
            yield break;
        }
        item.prepare?.Invoke();yield return new WaitForSecondsRealtime(1.5f);
        var camera=Camera.main;var position=camera.transform.position;var rotation=camera.transform.rotation;float fov=camera.fieldOfView;Rect viewport=camera.rect;
        bool enabled=Player.LocalInstance.enabled,blocked=GameInput.Instance.GameplayInputBlocked;
        yield return Tap(item.open);item.open.onClick.Invoke();
        Check(item.showing(),scene+": board opens once under rapid repeated input");
        if(!early)yield return new WaitForSecondsRealtime(1.2f);
        if(item.frame!=null&&!early)
        {
            var box=item.frame();
            foreach(int x in new[]{-1,1})foreach(int y in new[]{-1,1})foreach(int z in new[]{-1,1})
            {var point=camera.WorldToViewportPoint(box.center+Vector3.Scale(box.extents,new Vector3(x,y,z)));Check(point.z>camera.nearClipPlane&&point.z<camera.farClipPlane&&point.x>=-.03f&&point.x<=1.03f&&point.y>=-.03f&&point.y<=1.03f,scene+": complete board fits camera at "+Screen.width+"x"+Screen.height+" corner="+point);}
        }
        yield return Tap(item.back);item.back.onClick.Invoke();yield return new WaitForSecondsRealtime(1.2f);
        Check(!item.showing()&&GameInput.Instance.GameplayInputBlocked==blocked&&Player.LocalInstance.enabled==enabled,scene+": rapid Back restores controls and closes board");
        Check(camera.rect==viewport&&Mathf.Abs(camera.fieldOfView-fov)<.1f&&!PrelimTutorialCameraRig.HasActiveView,scene+": viewport, lens and temporary camera restore");
        Check(Vector3.Distance(position,camera.transform.position)<1.1f&&Quaternion.Angle(rotation,camera.transform.rotation)<4,scene+": gameplay camera returns to intended pose distance="+Vector3.Distance(position,camera.transform.position));
    }
    static IEnumerator InferenceBoard(bool early)
    {
        var stage=Find<LogicLegends.Inference.InferenceChallenge>();stage.OpenBoard(Player.LocalInstance);stage.OpenBoard(Player.LocalInstance);yield return null;
        Check(stage.IsBoardOpen,"RulesOfInference board opens once");
        if(early){Time.timeScale=0;yield return null;}else yield return new WaitForSecondsRealtime(1.2f);
        yield return Tap(stage.exploreButton);stage.exploreButton.onClick.Invoke();Time.timeScale=1;yield return new WaitForSecondsRealtime(1.1f);
        Check(!stage.IsBoardOpen&&Player.LocalInstance.enabled,"RulesOfInference Back works during entry / paused time and releases camera");
    }
    static IEnumerator TreeBoard(bool early)
    {
        var stage=Find<LogicLegends.Trees.TreeStageManager>();Near(stage.bookStatue,2);yield return new WaitForSecondsRealtime(1);
        stage.InteractBook();yield return null;Check(stage.BoardOpen,"Trees existing book camera opens");
        if(!early)yield return new WaitForSecondsRealtime(1.2f);
        // Back becomes eligible after its camera guard, then repeated touches must be harmless.
        yield return new WaitForSecondsRealtime(1);
        yield return Tap(stage.ui.BackButton);stage.ui.BackButton.onClick.Invoke();yield return new WaitForSecondsRealtime(1.2f);
        Check(!stage.BoardOpen&&Camera.main.rect==new Rect(0,0,1,1)&&!GameInput.Instance.GameplayInputBlocked&&Player.LocalInstance.enabled,"Trees Back restores full camera and controls");
    }
    static IEnumerator Capsule(string scene)
    {
        var player=Player.LocalInstance;var cc=player.GetComponent<CharacterController>();float speed=player.moveSpeed;
        yield return new WaitForSecondsRealtime(.5f);Vector3 initial=player.transform.position;
        Check(cc.enabled&&cc.detectCollisions&&cc.isGrounded,scene+": real player capsule is grounded and collision-enabled at "+initial);
        bool playerBefore=player.enabled;player.enabled=false;cc.Move(Vector3.up*3);cc.Move(Vector3.down*12);player.enabled=playerBefore;
        Check(player.transform.position.y>=initial.y-.2f,scene+": downward capsule sweep cannot pass through the floor");
        Move(initial);
        RaycastHit? wall=null;Vector3 direction=Vector3.zero;
        for(int angle=0;angle<24&&!wall.HasValue;angle++)
        {
            var d=Quaternion.Euler(0,angle*15,0)*Vector3.forward;
            foreach(var hit in Physics.RaycastAll(cc.bounds.center,d,35,~0,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance))
            {
                if(hit.collider==cc||hit.collider.attachedRigidbody!=null||Mathf.Abs(hit.normal.y)>.3f||hit.distance<2)continue;
                if(hit.collider.GetComponentInParent<GrabbableObject>()!=null)continue;
                if(System.Text.RegularExpressions.Regex.IsMatch(hit.collider.name,"tree|grass|flower|bush",System.Text.RegularExpressions.RegexOptions.IgnoreCase))continue;
                if(hit.collider.name=="Cylinder.077"||hit.collider.name=="TRUTIBOL"||System.Text.RegularExpressions.Regex.IsMatch(hit.collider.name,"stair|step|ramp",System.Text.RegularExpressions.RegexOptions.IgnoreCase))continue;
                if(hit.collider.bounds.max.y<cc.bounds.max.y+.5f)continue;
                wall=hit;direction=-hit.normal;direction.y=0;direction.Normalize();break;
            }
        }
        if(wall.HasValue)
        {
            var hit=wall.Value;var at=hit.point-direction*(cc.radius+2);at.y=initial.y;Move(at);yield return new WaitForSecondsRealtime(.2f);
            bool before=player.enabled;player.enabled=false;cc.Move(direction*8);player.enabled=before;
            float progress=Vector3.Dot(player.transform.position-at,direction);
            Check(progress<2.5f,scene+": capsule sweep cannot pass through solid "+hit.collider.name+" (travel="+progress+")");
            Move(initial);yield return new WaitForSecondsRealtime(.3f);
        }
        else File.AppendAllText(Report,"INFO "+scene+": no vertical solid within 35 units of spawn; open-platform geometry inspected separately.\n");
        Check(player.moveSpeed==speed,scene+": collision checks leave movement speed unchanged");
    }
    static IEnumerator AltarStairs()
    {
        var access=Find<LogicLegends.Inference.InferenceAltarAccess>();Check(access!=null&&access.OuterRadius>1&&!float.IsNaN(access.TopHeight),"RulesOfInference stair support builds from non-readable imported collision geometry: radius="+access?.OuterRadius+" top="+access?.TopHeight);
        var ramp=GameObject.Find("Altar Invisible Stair Ramp").GetComponent<MeshCollider>();var source=GameObject.Find("ALTAR").transform.Find("Cylinder.077").GetComponent<MeshCollider>();
        var player=Player.LocalInstance;var cc=player.GetComponent<CharacterController>();var camera=Camera.main;var brain=camera.GetComponent<CinemachineBrain>();bool brainBefore=brain.enabled;var rotation=camera.transform.rotation;brain.enabled=false;
        try
        {
            // Exercise the accessible approach and its oblique entries. The rear
            // faces contain visible tower walls / water, not walkable approaches.
            foreach(int angle in new[]{0,-20,20,90})
            {
                var outward=Quaternion.Euler(0,angle,0)*Vector3.forward;float start=access.OuterRadius-1;
                Vector3 point=access.Center+outward*start;RaycastHit floor;
                // The visible altar is not perfectly circular. Find this side's
                // actual tread instead of assuming the maximum radius fits every side.
                while(start>2 && !ramp.Raycast(new Ray(point+Vector3.up*20,Vector3.down),out floor,50))
                {start-=.5f;point=access.Center+outward*start;}
                Check(ramp.Raycast(new Ray(point+Vector3.up*20,Vector3.down),out floor,50),"altar outer tread exists at "+angle);
                Move(floor.point+Vector3.up*(cc.height*.5f-cc.center.y+.12f));camera.transform.rotation=Quaternion.LookRotation(-outward);yield return new WaitForSecondsRealtime(.3f);
                var joystick=MobileInputUI.FindJoystick();typeof(MobileInputUI).GetField("inputVector",Hidden).SetValue(joystick,Vector2.up);GameInput.Instance.SetMobileMovement(Vector2.up);
                float until=Time.realtimeSinceStartup+4;
                while(Vector2.Distance(new Vector2(player.transform.position.x,player.transform.position.z),new Vector2(access.Center.x,access.Center.z))>start*.65f&&Time.realtimeSinceStartup<until)yield return null;
                joystick.ResetJoystick();
                Check(cc.bounds.min.y>access.TopHeight-.2f,"altar stairs walkable without jumping from "+angle+" degrees; feet="+cc.bounds.min.y+" top="+access.TopHeight);
                point=access.Center+outward*(access.OuterRadius+3);
                Check(!ramp.Raycast(new Ray(point+Vector3.up*20,Vector3.down),out floor,50),"altar ramp does not create support over water outside stairs");
            }
        }
        finally {MobileInputUI.FindJoystick()?.ResetJoystick();brain.enabled=brainBefore;camera.transform.rotation=rotation;}
    }
    static IEnumerator Tap(Button button)
    {
        Check(button!=null&&button.gameObject.activeInHierarchy&&button.IsInteractable(),"available touch target "+button?.name);
        yield return null;Canvas.ForceUpdateCanvases();var point=Bounds((RectTransform)button.transform).center;
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);
        Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"unobstructed touch raycast "+button.name+" hits="+string.Join(",",hits.Take(3).Select(h=>h.gameObject.name)));
        int id=++finger;Queue(id,UnityEngine.InputSystem.TouchPhase.Began,point);yield return null;yield return null;Queue(id,UnityEngine.InputSystem.TouchPhase.Ended,point);yield return null;yield return null;
    }
}
#endif
