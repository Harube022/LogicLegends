using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static class ArchiveSharingChecks
    {
        [Serializable] sealed class Report
        {
            public bool passed;
            public string scope="Native saved MultiplayerGameplay. Explicit offline practice teammate and supplies; actual GIVE/USE button pointer events, authoritative quantities/health, used flashlight charge conservation, duplicate unique-tool rejection, distance/solid-wall/lock checks, original nest revival. No physical Android or second rendered client tested.";
            public List<string> checks=new List<string>(),errors=new List<string>();
        }
        static Report report;static IEnumerator flow;static double next;
        static ArchiveTopicSession s;static ArchiveRewardView v;static Player local,friend;
        static GameObject wall,lockOwner;static Vector3 oldPosition;static Quaternion oldRotation;static bool oldDefeat,oldThreats;
        public static string Status{get;private set;}="Idle";
        public static void Begin()
        {
            if(!Application.isPlaying||Photon.Pun.PhotonNetwork.InRoom||flow!=null)throw new InvalidOperationException("Use offline saved Play Mode.");
            s=ArchiveTopicSession.Current;v=ArchiveRewardView.Current;local=Player.LocalInstance;
            oldPosition=local.transform.position;oldRotation=local.transform.rotation;oldDefeat=s.suppressDefeatForVerification;oldThreats=s.worldProgression.suppressThreatsForVerification;
            s.suppressDefeatForVerification=s.worldProgression.suppressThreatsForVerification=true;
            report=new Report();Directory.CreateDirectory("Temp/ArchiveSharing");flow=Flow();next=0;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;
        }
        static void Log(string t,string stack,LogType kind){if(report.errors.Count<8&&(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert))report.errors.Add(t+"\n"+stack);}
        static void Save()=>File.WriteAllText("Temp/ArchiveSharing/Play.json",JsonUtility.ToJson(report,true));
        static void Check(bool ok,string label){report.checks.Add((ok?"PASS ":"FAIL ")+label);Save();if(!ok)throw new Exception(label);}
        static void Tick(){if(flow==null)return;EditorApplication.QueuePlayerLoopUpdate();try{if(EditorApplication.timeSinceStartup<next)return;if(!Application.isPlaying)throw new Exception("Play stopped");if(!flow.MoveNext()){Finish(true);return;}next=EditorApplication.timeSinceStartup+(flow.Current is float delay?delay:0);}catch(Exception e){report.errors.Add(Status+": "+e);Finish(false);}}
        static void Finish(bool ok)
        {
            EditorApplication.update-=Tick;Application.logMessageReceived-=Log;flow=null;
            if(Application.isPlaying&&s!=null)
            {
                if(lockOwner!=null)local.ReleaseMovementLock(lockOwner);
                if(wall!=null)UnityEngine.Object.Destroy(wall);if(lockOwner!=null)UnityEngine.Object.Destroy(lockOwner);
                s.RequestAdminTest(ArchiveAdminTestAction.ResetMatch);s.RequestAdminTest(ArchiveAdminTestAction.RemoveTeammate);
                Move(local,oldPosition);local.transform.rotation=oldRotation;s.suppressDefeatForVerification=oldDefeat;s.worldProgression.suppressThreatsForVerification=oldThreats;
            }
            report.passed=ok&&report.errors.Count==0;Status=report.passed?"Passed":"Failed";Save();
        }
        static void Move(Player player,Vector3 position)
        {var cc=player.GetComponent<CharacterController>();bool enabled=cc.enabled;cc.enabled=false;player.transform.position=position;player.ResetMotionAfterTeleport();cc.enabled=enabled;Physics.SyncTransforms();if(player==Player.LocalInstance)TeleportCameraSync.ResetAfterMove(player.transform,Vector3.zero);}
        static void Equip(int slot){if(s.EquippedItem(-1)!=slot)v.SelectHotbarSlot(slot);}
        static void Click(Button b)
        {
            Check(b!=null&&b.isActiveAndEnabled&&b.interactable,"Team action is visible and enabled");
            var p=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,b.transform.position),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);
            Check(hits.Count>0&&(hits[0].gameObject==b.gameObject||hits[0].gameObject.transform.IsChildOf(b.transform)),"Team action pointer reaches its intended button without HUD overlap");
            ExecuteEvents.Execute(b.gameObject,p,ExecuteEvents.pointerClickHandler);
        }
        static IEnumerator Flow()
        {
            Status="Practice teammate and partial-charge flashlight transfer";
            s.RequestAdminTest(ArchiveAdminTestAction.RemoveTeammate);s.Initialize(102992);ArchiveTopicUI.Current.Close();
            Check(s.RequestAdminTest(ArchiveAdminTestAction.SpawnTeammate),"Existing offline practice teammate spawns through Admin Lab");
            friend=s.ActorPlayer(-2);Move(friend,local.transform.position+local.transform.right*3);
            s.RequestAdminTest(ArchiveAdminTestAction.GrantAllItems,-1);var own=s.Inventory(-1);own.batterySeconds=137;own.batteryUpdatedAt=ArchiveTopicSession.Clock;yield return .3f;
            Equip(0);yield return .3f;v.SelectTeamItemTarget(-2);yield return .3f;
            Check(v.TargetActor==-2&&v.TeamItemPanel.gameObject.activeInHierarchy,"Nearby practice teammate is explicitly selected in the compact item panel");
            Check(s.CanGiveItem(-1,-2,ArchiveSurvivalItem.Flashlight,out _),"A clear nearby teammate can receive the selected flashlight");
            own.flashlightOn=true;own.batteryUpdatedAt=ArchiveTopicSession.Clock;yield return .2f;Click(v.GiveItemButton);yield return .65f;
            Check(s.CountItem(-1,ArchiveSurvivalItem.Flashlight)==0&&s.CountItem(-2,ArchiveSurvivalItem.Flashlight)==1&&s.EquippedItem(-1)==-1,
                "GIVE moves exactly one unique tool and clears the donor's exhausted equipped slot");
            Check(!own.flashlightOn&&!s.Inventory(-2).flashlightOn&&own.batterySeconds==0&&s.FlashlightRemaining(-2)>134&&s.FlashlightRemaining(-2)<=137,
                "A used flashlight transfers its actual remaining charge while both lights turn off; no fresh battery is invented");
            Check(!s.RequestGiveEquippedItem(-2)&&s.CountItem(-2,ArchiveSurvivalItem.Flashlight)==1,"The exhausted donor cannot repeat the transfer");
            s.RequestAdminTest(ArchiveAdminTestAction.GrantItem,-1,0);Equip(0);yield return .3f;v.SelectTeamItemTarget(-2);yield return .2f;
            Check(!s.CanGiveItem(-1,-2,ArchiveSurvivalItem.Flashlight,out _)&&!v.GiveItemButton.interactable,"An already-owned unique tool cannot be duplicated or gifted for extra charge");
            Status="Targeted medkit and battery support";
            s.RequestAdminTest(ArchiveAdminTestAction.SetHearts,-2,2);Equip(2);yield return .3f;v.SelectTeamItemTarget(-2);yield return .3f;int med=s.CountItem(-1,ArchiveSurvivalItem.Medkit);
            Click(v.UseOnTeammateButton);yield return .65f;
            Check(s.hunting.health.ActorHearts(-2)==3&&s.hunting.health.ActorHearts(-1)==3&&s.CountItem(-1,ArchiveSurvivalItem.Medkit)==med-1,
                "USE ON restores exactly one teammate heart and consumes one donor medkit without healing the wrong actor");
            Check(!s.RequestUseEquippedItem(-2)&&s.CountItem(-1,ArchiveSurvivalItem.Medkit)==med-1,"A full-health teammate cannot waste a second medkit");
            Equip(1);yield return .3f;v.SelectTeamItemTarget(-2);yield return .3f;int batteries=s.CountItem(-1,ArchiveSurvivalItem.Battery);Click(v.UseOnTeammateButton);yield return .65f;
            Check(Mathf.Abs(s.FlashlightRemaining(-2)-240)<.1f&&s.CountItem(-1,ArchiveSurvivalItem.Battery)==batteries-1,"USE ON fills the teammate's flashlight to four minutes and consumes one donor battery");
            Check(!s.RequestUseEquippedItem(-2)&&s.CountItem(-1,ArchiveSurvivalItem.Battery)==batteries-1,"A full teammate battery is rejected without consumption");
            Status="Physical reach, visibility and restrictions";
            Equip(2);yield return .3f;v.SelectTeamItemTarget(-2);yield return .25f;int donor=s.CountItem(-1,ArchiveSurvivalItem.Medkit);
            Move(friend,local.transform.position+local.transform.right*12);yield return .3f;
            Check(v.TargetActor==0&&!s.RequestGiveEquippedItem(-2)&&s.CountItem(-1,ArchiveSurvivalItem.Medkit)==donor,"An out-of-range target clears from UI and cannot receive a remote transfer");
            Move(friend,local.transform.position+local.transform.right*3);yield return .3f;v.SelectTeamItemTarget(-2);yield return .2f;
            var a=local.GetComponent<CharacterController>().bounds.center;var b=friend.GetComponent<CharacterController>().bounds.center;
            wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Sharing_Solid_Obstacle_QA";wall.transform.SetPositionAndRotation((a+b)*.5f,Quaternion.LookRotation(b-a));wall.transform.localScale=new Vector3(4,8,.3f);Physics.SyncTransforms();yield return .25f;
            Check(!s.RequestGiveEquippedItem(-2)&&s.CountItem(-1,ArchiveSurvivalItem.Medkit)==donor,"A solid obstacle blocks giving and preserves inventory");
            UnityEngine.Object.Destroy(wall);wall=null;yield return .25f;
            lockOwner=new GameObject("Sharing_Lock_QA");local.AcquireMovementLock(lockOwner,"Sharing restriction fixture",local.transform.position,150);yield return .2f;
            Check(!s.RequestGiveEquippedItem(-2)&&s.CountItem(-1,ArchiveSurvivalItem.Medkit)==donor,"A movement-locked donor cannot give supplies");
            local.ReleaseMovementLock(lockOwner);UnityEngine.Object.Destroy(lockOwner);lockOwner=null;yield return .3f;
            v.SelectTeamItemTarget(-2);yield return .2f;Click(v.GiveItemButton);yield return .65f;
            Check(s.CountItem(-1,ArchiveSurvivalItem.Medkit)==donor-1&&s.CountItem(-2,ArchiveSurvivalItem.Medkit)==1,"A clear transfer moves one consumable without changing the combined stack total");
            Equip(4);yield return .3f;v.SelectTeamItemTarget(-2);yield return .2f;int ammo=s.CountItem(-1,ArchiveSurvivalItem.TranquilizerAmmo);Click(v.GiveItemButton);yield return .65f;
            Check(s.CountItem(-2,ArchiveSurvivalItem.Tranquilizer)==1&&s.CountItem(-1,ArchiveSurvivalItem.Tranquilizer)==0&&s.CountItem(-1,ArchiveSurvivalItem.TranquilizerAmmo)==ammo&&s.CountItem(-2,ArchiveSurvivalItem.TranquilizerAmmo)==0,
                "Giving the gun transfers only the tool and never creates or moves free ammunition");
            Equip(5);yield return .3f;v.SelectTeamItemTarget(-2);yield return .2f;Click(v.GiveItemButton);yield return .65f;
            Check(s.CountItem(-1,ArchiveSurvivalItem.TranquilizerAmmo)==ammo-1&&s.CountItem(-2,ArchiveSurvivalItem.TranquilizerAmmo)==1,"A selected ammo gift moves exactly one dart to the teammate");
            ScreenCapture.CaptureScreenshot("Temp/ArchiveSharing/Team_Item_Actions.png");yield return .3f;
            Status="Existing nest revival";s.RequestAdminTest(ArchiveAdminTestAction.DownAtNest,-2);yield return .3f;
            Move(local,s.Downed(-2).anchor+Vector3.back*2.5f);Equip(3);yield return .3f;v.SelectTeamItemTarget(-2);yield return .3f;int kits=s.CountItem(-1,ArchiveSurvivalItem.RevivalKit);
            Click(v.UseOnTeammateButton);yield return .65f;
            Check(s.hunting.health.ActorHearts(-2)==1&&s.CountItem(-1,ArchiveSurvivalItem.RevivalKit)==kits-1&&s.Downed(-2)==null,
                "Targeted revival keeps the original nest rule and restores one heart using exactly one kit");
        }
    }
}
