using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static class ArchiveHotbarChecks
    {
        [Serializable]sealed class Report{public bool passed;public string scope="Offline native Editor Play: explicit Admin supply/chest fixtures, actual hotbar button pointer events, authoritative equip/use, actual existing rig/hand props, flashlight and reward previews. Physical Android touch is not verified.";public List<string> checks=new List<string>(),errors=new List<string>();}
        static Report report;static IEnumerator flow;static double next;static ArchiveTopicSession s;
        static bool originalInputBlock;static Keyboard testKeyboard,originalKeyboard;
        public static string Status{get;private set;}="Idle";
        public static void Begin(){if(!Application.isPlaying||Photon.Pun.PhotonNetwork.InRoom||flow!=null)throw new Exception("Use fresh offline Play");s=ArchiveTopicSession.Current;originalInputBlock=GameInput.Instance.GameplayInputBlocked;report=new Report();flow=Flow();EditorApplication.update+=Tick;Application.logMessageReceived+=Log;next=0;}
        static void Save(){Directory.CreateDirectory("Temp/ArchiveHotbar");File.WriteAllText("Temp/ArchiveHotbar/Play.json",JsonUtility.ToJson(report,true));}
        static void Check(bool test,string label){report.checks.Add((test?"PASS ":"FAIL ")+label);Save();if(!test)throw new Exception(label);}
        static void Log(string text,string stack,LogType type){if(report.errors.Count<8&&(type==LogType.Error||type==LogType.Exception||type==LogType.Assert))report.errors.Add(text+"\n"+stack);}
        static void Tick(){if(flow==null)return;EditorApplication.QueuePlayerLoopUpdate();try{if(EditorApplication.timeSinceStartup<next)return;if(!Application.isPlaying)throw new Exception("Play stopped");if(!flow.MoveNext()){Finish(true);return;}next=EditorApplication.timeSinceStartup+(flow.Current is float seconds?seconds:0);}catch(Exception e){report.errors.Add(Status+": "+e);Finish(false);}}
        static void Finish(bool success){EditorApplication.update-=Tick;Application.logMessageReceived-=Log;flow=null;if(testKeyboard!=null){InputSystem.RemoveDevice(testKeyboard);testKeyboard=null;}if(originalKeyboard!=null&&originalKeyboard.added)originalKeyboard.MakeCurrent();if(GameInput.Instance!=null)GameInput.Instance.SetGameplayInputBlocked(originalInputBlock);report.passed=success&&report.errors.Count==0;Status=report.passed?"Passed":"Failed";Save();}
        static void Click(Button b)
        {
            Check(b!=null&&b.isActiveAndEnabled&&b.interactable,"Hotbar target is active and interactable");
            var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,b.transform.position),button=PointerEventData.InputButton.Left};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            Check(hits.Count>0&&(hits[0].gameObject==b.gameObject||hits[0].gameObject.transform.IsChildOf(b.transform)),"Real hotbar target is not covered by another HUD control");
            ExecuteEvents.Execute(b.gameObject,pointer,ExecuteEvents.pointerClickHandler);
        }
        static IEnumerator Flow()
        {
            Status="Collected models and real slot selection";s.Initialize(128871);s.hunting.Request(Hunting.KhezuCommand.Sleep);ArchiveTopicUI.Current.Close();
            var view=ArchiveRewardView.Current;var held=s.GetComponent<ArchiveEquippedItems>();
            yield return .25f;
            Check(!view.RequestVisibleForQA()&&Enumerable.Range(0,6).All(n=>!view.HotbarButton(n).gameObject.activeSelf),
                "A new empty inventory displays no holders or inventory backplates");
            Check(view.itemPrefabs.Length==6&&view.itemPrefabs.All(p=>p!=null)&&view.itemIcons.All(p=>p!=null),"All six supplies have saved reusable 3D models and matching inventory icons");
            Check(view.itemPrefabs.All(p=>p.GetComponentsInChildren<Collider>(true).Length==0&&p.GetComponentsInChildren<Rigidbody>(true).Length==0),"Held previews cannot collide with or push gameplay objects");
            s.RequestAdminTest(ArchiveAdminTestAction.GrantAllItems,-1);yield return .25f;
            var counts=s.Inventory(-1).items.ToArray();var player=Player.LocalInstance;var original=player.transform.rotation;
            Check(s.EquippedItem(-1)==-1&&held.VisibleModelCount==0,"Collected supplies enter the bottom bar without being consumed or used");
            for(int i=0;i<6;i++)
            {
                Click(view.HotbarButton(i));yield return .25f;
                Check(s.EquippedItem(-1)==i&&held.VisibleItem(-1)==i&&held.VisibleModelCount==1,"Slot "+(i+1)+" selects exactly one matching 3D hand item");
                Check(Enumerable.Range(0,6).All(n=>view.HotbarSelectionBorder(n).gameObject.activeSelf==(n==i))&&
                    !view.HotbarSelectionBorder(i).GetComponent<UnityEngine.UI.Graphic>().raycastTarget,
                    "Slot "+(i+1)+" has exactly one persistent full selection border without intercepting input");
                Canvas.ForceUpdateCanvases();var borderMesh=view.HotbarSelectionBorder(i).GetComponent<CanvasRenderer>()?.GetMesh();
                Check(borderMesh!=null&&borderMesh.vertexCount>20,"Slot "+(i+1)+" actually renders its hollow highlighted border mesh");
                Check(s.Inventory(-1).items.SequenceEqual(counts),"Selecting "+((ArchiveSurvivalItem)i)+" preserves every inventory stack");
                Check(Quaternion.Angle(player.transform.rotation,original)<.1f&&player.GetHeldObject()==null,"Equipping supplies preserves the player root and academic pickup channel");
                if(i==0||i==4)
                {
                    var wrist=player.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Bone.006.R_end");
                    var model=held.EquippedModel(-1).transform;var wristRotation=Quaternion.Inverse(wrist.rotation)*model.rotation;
                    var lookControl=player.GetComponentInChildren<CameraTargetController>();var before=lookControl.transform.eulerAngles;
                    foreach(float yaw in new[]{-85f,85f,180f})
                    {
                        lookControl.SetViewAngles(22,player.transform.eulerAngles.y+yaw);yield return .3f;
                        Check(Quaternion.Angle(wristRotation,Quaternion.Inverse(wrist.rotation)*model.rotation)<.2f,
                            ((ArchiveSurvivalItem)i)+" stays rigid in the hand during camera orbit "+yaw);
                    }
                    lookControl.SetViewAngles(Mathf.DeltaAngle(0,before.x),before.y);yield return .2f;
                    ScreenCapture.CaptureScreenshot("Temp/ArchiveHotbar/Stable_Hand_"+((ArchiveSurvivalItem)i)+".png");yield return .15f;
                }
            }
            Click(view.HotbarButton(5));yield return .25f;
            Check(s.EquippedItem(-1)==-1&&held.VisibleModelCount==0,"Tapping the selected slot again puts the item away");
            Check(Enumerable.Range(0,6).All(n=>!view.HotbarSelectionBorder(n).gameObject.activeSelf),"Unequip clears every highlighted border");
            s.RequestAdminTest(ArchiveAdminTestAction.SetHearts,-1,2);s.Inventory(-1).items[(int)ArchiveSurvivalItem.Medkit]=1;
            Click(view.HotbarButton(2));yield return .25f;
            Check(s.hunting.health.CurrentHealth==2&&s.CountItem(-1,ArchiveSurvivalItem.Medkit)==1,"Medkit selection alone does not heal or consume it");
            GameInput.Instance.SetGameplayInputBlocked(true);
            view.UseEquippedButton.onClick.Invoke();view.SelectHotbarSlot(0);
            originalKeyboard=Keyboard.current;testKeyboard=InputSystem.AddDevice<Keyboard>("Hotbar_Chat_Input_QA");
            InputSystem.QueueStateEvent(testKeyboard,new KeyboardState(Key.Digit1,Key.F));yield return .25f;
            Check(s.EquippedItem(-1)==(int)ArchiveSurvivalItem.Medkit&&s.hunting.health.CurrentHealth==2&&s.CountItem(-1,ArchiveSurvivalItem.Medkit)==1,
                "Chat-style input blocking prevents number/F events and stale slot/Use callbacks from selecting or consuming items");
            Check(!view.RequestVisibleForQA(),"Blocked gameplay input hides the supply controls");
            InputSystem.RemoveDevice(testKeyboard);testKeyboard=null;if(originalKeyboard!=null&&originalKeyboard.added)originalKeyboard.MakeCurrent();
            GameInput.Instance.SetGameplayInputBlocked(false);yield return .25f;
            Click(view.UseEquippedButton);yield return .5f;
            Check(s.hunting.health.CurrentHealth==3&&s.CountItem(-1,ArchiveSurvivalItem.Medkit)==0&&s.EquippedItem(-1)==-1&&held.VisibleModelCount==0,"Separate Use restores one heart and clears an exhausted medkit slot and model");
            Check(!view.HotbarButton(2).gameObject.activeSelf,"Consuming the last medkit removes its entire holder, including its frame and label");
            var remaining=Enumerable.Range(0,6).Where(n=>s.CountItem(-1,(ArchiveSurvivalItem)n)>0).Select(n=>(RectTransform)view.HotbarButton(n).transform).ToArray();
            Check(remaining.Length==5&&remaining.Zip(remaining.Skip(1),(a,b)=>Mathf.Abs(a.anchorMax.x-b.anchorMin.x)<.001f).All(v=>v)&&
                Mathf.Abs(remaining[0].anchorMin.x+remaining.Last().anchorMax.x-1)<.001f,
                "After depletion, remaining holders stay centered and adjacent without blank slots");
            Click(view.HotbarButton(0));yield return .25f;Click(view.UseEquippedButton);yield return .4f;
            Check(s.Inventory(-1).flashlightOn&&UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Any(l=>l.name=="Actor_-1_Flashlight"&&l.enabled),"The equipped 3D flashlight produces a real light after explicit Use");
            var look=player.GetComponentInChildren<CameraTargetController>();var originalAngles=look.transform.eulerAngles;
            look.SetViewAngles(12,player.transform.eulerAngles.y+180);yield return .4f;
            var lamp=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).First(l=>l.name=="Actor_-1_Flashlight");
            var torch=held.EquippedModel(-1).transform;
            Check(Vector3.Dot(Camera.main.transform.forward,torch.forward)<-.5f&&Vector3.Dot(lamp.transform.forward,torch.forward)>.999f&&
                Vector3.Distance(lamp.transform.position,torch.TransformPoint(new Vector3(0,0,.72f)))<.05f,
                "Front-orbit camera keeps the actual light beam aligned to the visible flashlight lens");
            look.SetViewAngles(originalAngles.x,originalAngles.y);yield return .2f;
            Click(view.HotbarButton(4));yield return .25f;
            Check(!s.Inventory(-1).flashlightOn&&held.VisibleItem(-1)==4,"Switching to the gun puts away and turns off the flashlight");
            ScreenCapture.CaptureScreenshot("Temp/ArchiveHotbar/Hotbar_Equipped_Gun.png");yield return .15f;
            Status="Actual item preview above the chest";
            Check(s.RequestAdminTest(ArchiveAdminTestAction.SpawnChest,0,(int)ArchiveSurvivalItem.RevivalKit),"Admin exposes a known chest reward fixture for visual inspection");yield return 2.1f;
            var preview=view.ChestPreview(0);
            Check(preview!=null&&preview.activeInHierarchy&&preview.GetComponentsInChildren<Renderer>().Length>0,"Unclaimed chest displays the actual rotating 3D reward instead of only text");
            ScreenCapture.CaptureScreenshot("Temp/ArchiveHotbar/Chest_3D_Reward.png");yield return .15f;
            Check(s.RequestChest(0),"The previewed chest is claimed through normal owner and distance validation");yield return .25f;
            Check(!preview.activeSelf&&s.CountItem(-1,ArchiveSurvivalItem.RevivalKit)>0,"Claim removes the floating reward and stores the real item quantity in the hotbar");
            var lockOwner=new GameObject("Hotbar_QA_Lock");player.AcquireMovementLock(lockOwner,"Presentation cleanup fixture",player.transform.position,150);yield return .25f;
            Check(held.VisibleModelCount==0&&!view.RequestVisibleForQA(),"Capture-style movement restrictions hide held equipment and the hotbar");
            player.ReleaseMovementLock(lockOwner);UnityEngine.Object.Destroy(lockOwner);yield return .25f;
            Check(held.VisibleModelCount==1,"Equipment returns after the temporary restriction ends");
            s.RequestEquipItem(-1);yield return .25f;Check(held.VisibleModelCount==0,"Final unequip leaves no orphaned held models");
            s.RequestAdminTest(ArchiveAdminTestAction.GrantAllItems,-1);yield return .25f;
            Check(Enumerable.Range(0,6).All(n=>view.HotbarButton(n).gameObject.activeInHierarchy),
                "Reacquiring a consumed item restores its existing holder and all six slots remain usable");
        }
    }
}
