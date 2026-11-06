#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;

/// <summary>Real PRELIM camera, mobile look surface, trap, menu and all twelve local skins; no account writes.</summary>
public static class HammerCameraPlayChecks
{
    const string Report="Library/HammerCameraChecks.txt";
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    const BindingFlags Static=BindingFlags.Static|BindingFlags.NonPublic;
    static Touchscreen touch;static int finger=1700;
    static T Find<T>()where T:Object=>Object.FindFirstObjectByType<T>();
    static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Hidden).GetValue(o);
    static void Check(bool pass,string message){if(!pass)throw new InvalidOperationException(message);File.AppendAllText(Report,"PASS "+message+"\n");}
    static void Backend(string field,object value)=>typeof(TrialBackendClient).GetField(field,Static).SetValue(null,value);
    public static IEnumerator Run()
    {
        File.WriteAllText(Report,"Saved PRELIM scene, real mobile look/EventSystem and hammer; simulated touch, offline account/reward isolation.\n");
        var input=GameInput.Instance;var player=Player.LocalInstance;var cc=player.GetComponent<CharacterController>();
        var priorSettings=InputSystem.settings;var settings=Object.Instantiate(priorSettings);settings.hideFlags=HideFlags.HideAndDontSave;
        settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;InputSystem.settings=settings;
        touch=InputSystem.AddDevice<Touchscreen>();
        var oldTransport=typeof(TrialBackendClient).GetField("ValidationTransport",Static).GetValue(null);
        Backend("ValidationUid","hammer-camera-qa");Backend("ValidationEmail","sngbdsa137@gmail.com");
        Backend("ValidationTransport",(Func<string,string,Task<string>>)((p,j)=>Task.FromResult(p=="/trials/access"?JsonUtility.ToJson(new TrialBackendClient.Access{uid="hammer-camera-qa",admin=true}):"{}")));
        AdminAuthorization.Refresh();AdminAuthorization.Tick();
        var preview=player.GetComponent<AdminCharacterOverride>();if(preview==null)preview=player.gameObject.AddComponent<AdminCharacterOverride>();
        var trap=Object.FindObjectsByType<DynamicDoorTrigger>(FindObjectsSortMode.None).First();
        var collider=trap.GetComponent<Collider>();bool collision=collider!=null&&collider.enabled;if(collider!=null)collider.enabled=false;
        var origin=player.transform.position;var rotation=player.transform.rotation;
        var target=player.GetComponentInChildren<CameraTargetController>();var menu=Find<LevelMenu>();
        int jumps=0,interactions=0;EventHandler jump=(s,e)=>jumps++;EventHandler interact=(s,e)=>interactions++;
        input.OnJumpAction+=jump;input.OnInteractAction+=interact;
        try
        {
            yield return Wait(()=>!PrelimTutorialCameraRig.HasActiveView&&GameplayTouchRouting.CameraInputAllowed,"gameplay camera ready",5);
            Check(target!=null&&target.enabled&&Camera.main!=null&&Find<MobileLookInput>()!=null,"normal camera and mobile look surface present");
            Check(menu!=null,"actual Settings menu present");
            for(int skin=0;skin<12;skin++)
            {
                Check(preview.Select(skin),"select "+AdminCharacterOverride.Catalog.skins[skin].name);yield return null;
                string name=preview.SelectedName;
                var delta=origin-player.transform.position;cc.enabled=false;player.transform.SetPositionAndRotation(origin,rotation);cc.enabled=true;player.ResetMotionAfterTeleport();
                TeleportCameraSync.ResetAfterMove(player.transform,delta);target.SetViewAngles(12,rotation.eulerAngles.y);yield return null;yield return null;
                Check(!input.GameplayInputBlocked&&!input.HasGameplayActionBlock&&player.enabled,"clean input before "+name);
                yield return Swipe(target,true,name+" before hit");
                var cameraStart=Camera.main.transform.position;var playerStart=player.transform.position;
                trap.StartCoroutine((IEnumerator)typeof(DynamicDoorTrigger).GetMethod("HammerTrapSequence",Hidden).Invoke(trap,new object[]{player.gameObject}));
                Check(input.HasGameplayActionBlock&&input.GameplayActionsBlocked&&!input.GameplayInputBlocked&&player.enabled,name+": stun owns actions without modal/camera lock");
                yield return new WaitForSeconds(Field<float>(trap,"knockbackDelay")+.02f);
                Check(player.GetComponent<PlayerCarryRig>().ActiveSkin.GetComponent<PlayerAnimator>().IsStunned,name+": impact starts Stun");
                yield return Swipe(target,true,name+" during knockback");
                yield return new WaitForSeconds(.3f);
                Check(Vector3.Distance(playerStart,player.transform.position)>.1f&&Vector3.Distance(cameraStart,Camera.main.transform.position)>.1f,name+": rendered camera follows real knockback");
                var brain=Camera.main.GetComponent<CinemachineBrain>();
                if(brain!=null&&brain.enabled)
                {
                    var view=brain.ActiveVirtualCamera as CinemachineVirtualCameraBase;
                    Check(view!=null&&view.Follow!=null&&(view.Follow==player.transform||view.Follow.IsChildOf(player.transform)),name+": active camera retains local-player follow target");
                }
                var still=player.transform.position;int j=jumps,k=interactions;
                var joystick=MobileInputUI.FindJoystick();var data=new PointerEventData(EventSystem.current){pointerId=9999,position=new Vector2(Screen.width*.15f,Screen.height*.2f)};
                joystick.OnPointerDown(data);joystick.OnDrag(data);input.SetMobileMovement(Vector2.up);input.MobileJump();input.MobileInteract();player.OnRun();
                yield return new WaitForSeconds(.15f);
                Check(input.GetMovementVectorNormalized()==Vector2.zero&&joystick.CurrentInput==Vector2.zero&&jumps==j&&interactions==k&&!player.IsRunning()&&Vector2.Distance(new Vector2(still.x,still.z),new Vector2(player.transform.position.x,player.transform.position.z))<.02f,name+": joystick, jump, hand and run cannot move or interact during recovery");
                yield return Swipe(target,true,name+" during recovery");
                if(skin==0||skin==11)
                {
                    float pausedAt=Time.time;menu.OpenPause();yield return null;
                    Check(input.HasGameplayActionBlock&&input.GameplayInputBlocked&&!GameplayTouchRouting.CameraInputAllowed,"Settings owns its intentional camera lock over stun");
                    yield return Swipe(target,false,"Settings over stun");
                    Check(Time.time-pausedAt<.03f,"Settings pauses the stun timer");menu.ClosePause();yield return null;yield return null;
                    Check(input.HasGameplayActionBlock&&!input.GameplayInputBlocked&&player.enabled&&GameplayTouchRouting.CameraInputAllowed,"Settings close restores look without releasing stun actions");
                    yield return Swipe(target,true,"look resumes with stun still active");
                    var rig=PrelimTutorialCameraRig.Create("QA intentional tutorial camera");
                    Check(!GameplayTouchRouting.CameraInputAllowed,"tutorial camera still rejects free look");yield return Swipe(target,false,"tutorial view over stun");rig.FinishReturn();
                    yield return Wait(()=>GameplayTouchRouting.CameraInputAllowed,"tutorial camera returns",3);
                    Check(input.HasGameplayActionBlock,"tutorial camera cleanup preserves remaining stun");
                }
                yield return Wait(()=>!Field<bool>(trap,"isProcessingTrap"),"stun finishes",7);
                Check(!input.HasGameplayActionBlock&&!input.GameplayActionsBlocked&&!input.GameplayInputBlocked&&player.enabled&&GameplayTouchRouting.CameraInputAllowed,name+": recovery releases only stun ownership");
                yield return Swipe(target,true,name+" after recovery");
            }
            // Aborting while Settings is open must not release that menu's lock.
            trap.StartCoroutine((IEnumerator)typeof(DynamicDoorTrigger).GetMethod("HammerTrapSequence",Hidden).Invoke(trap,new object[]{player.gameObject}));
            yield return new WaitForSeconds(.4f);menu.OpenPause();trap.enabled=false;yield return null;
            Check(!input.HasGameplayActionBlock&&input.GameplayInputBlocked&&!GameplayTouchRouting.CameraInputAllowed,"trap abort cleans its owner and preserves Settings");
            menu.ClosePause();trap.enabled=true;yield return null;
            Check(!input.GameplayActionsBlocked&&player.enabled&&GameplayTouchRouting.CameraInputAllowed,"closing Settings after abort restores normal camera/actions");
            File.AppendAllText(Report,"ALL TWELVE SKINS / MOBILE LOOK / FOLLOW / MODAL LOCKS / RECOVERY CHECKS PASSED. Editor only; physical Android remains unverified.\n");
        }
        finally
        {
            menu?.ClosePause();trap.enabled=true;if(collider!=null)collider.enabled=collision;
            input.ReleaseGameplayActionBlock(trap);input.OnJumpAction-=jump;input.OnInteractAction-=interact;
            preview.RestoreEquipped();MobileLookInput.CancelAll();if(touch!=null)InputSystem.RemoveDevice(touch);InputSystem.settings=priorSettings;Object.Destroy(settings);
            Backend("ValidationUid",null);Backend("ValidationEmail",null);typeof(TrialBackendClient).GetField("ValidationTransport",Static).SetValue(null,oldTransport);AdminAuthorization.Refresh();
        }
    }
    static IEnumerator Swipe(CameraTargetController target,bool allowed,string context)
    {
        Canvas.ForceUpdateCanvases();Vector2 point=Vector2.zero;bool found=false;
        for(float x=.58f;x<.9f&&!found;x+=.08f)for(float y=.32f;y<.76f;y+=.1f)
        {var p=new Vector2(Screen.width*x,Screen.height*y);if(GameplayTouchRouting.IsLookSurface(p)){point=p;found=true;break;}}
        // A modal may cover every look raycast; inject a real finger over the
        // normal right-hand look area and verify that its rotation stays blocked.
        if(!found){Check(!allowed,context+": normal look surface must be tappable");point=new Vector2(Screen.width*.68f,Screen.height*.5f);}
        var before=target.transform.rotation;int id=++finger;
        Queue(id,UnityEngine.InputSystem.TouchPhase.Began,point);yield return null;yield return null;
        Queue(id,UnityEngine.InputSystem.TouchPhase.Moved,point+new Vector2(55,-12));yield return null;yield return null;
        Queue(id,UnityEngine.InputSystem.TouchPhase.Ended,point+new Vector2(55,-12));yield return null;yield return null;
        float angle=Quaternion.Angle(before,target.transform.rotation);
        Check(allowed?angle>1:angle<.05f,context+": mobile swipe "+(allowed?"rotates":"is blocked")+" ("+angle.ToString("F2")+" degrees)");
    }
    static void Queue(int id,UnityEngine.InputSystem.TouchPhase phase,Vector2 position)=>InputSystem.QueueStateEvent(touch,new TouchState{touchId=id,phase=phase,position=position});
    static IEnumerator Wait(Func<bool> condition,string message,float seconds)
    {float by=Time.realtimeSinceStartup+seconds;while(!condition()){if(Time.realtimeSinceStartup>by)throw new InvalidOperationException("Timeout: "+message);yield return null;}}
}
#endif
