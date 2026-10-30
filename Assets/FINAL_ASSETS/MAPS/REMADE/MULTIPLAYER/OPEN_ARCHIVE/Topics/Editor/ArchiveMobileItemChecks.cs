using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LogicLegends.OpenArchive.Hunting;
using LogicLegends.Tests.Monsters;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static class ArchiveMobileItemChecks
    {
        [Serializable]sealed class Report
        {
            public bool passed;
            public string scope="Native offline saved MultiplayerGameplay. Real circular mobile button pointer events, 240-second battery, actual camera-reticle/server-origin shots, moving pooled 3D dart, existing Sleep clips on both Rajang and Khezu at exact hit root anchors, normal wake and preserved hunt counters. Test supplies/awake states are explicit fixtures. Physical Android/two rendered clients are unverified.";
            public List<string> checks=new List<string>(),errors=new List<string>();
        }
        static Report report;static IEnumerator flow;static double next;
        static ArchiveTopicSession s;static ArchiveRewardView v;static DevelopmentMonsterSelection selector;static DevelopmentMonster original;
        static bool oldDefeat,oldThreats;
        public static string Status{get;private set;}="Idle";
        public static void Begin()
        {
            if(!Application.isPlaying||Photon.Pun.PhotonNetwork.InRoom||flow!=null)throw new InvalidOperationException("Use fresh offline saved Play Mode.");
            s=ArchiveTopicSession.Current;v=ArchiveRewardView.Current;selector=s.worldProgression.selector;original=selector.Selected;
            oldDefeat=s.suppressDefeatForVerification;oldThreats=s.worldProgression.suppressThreatsForVerification;
            s.suppressDefeatForVerification=s.worldProgression.suppressThreatsForVerification=true;
            Directory.CreateDirectory("Temp/ArchiveMobileItems");report=new Report();flow=Flow();next=0;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;
        }
        static void Check(bool ok,string text){report.checks.Add((ok?"PASS ":"FAIL ")+text);Save();if(!ok)throw new Exception(text);}
        static void Save()=>File.WriteAllText("Temp/ArchiveMobileItems/Play.json",JsonUtility.ToJson(report,true));
        static void Log(string text,string stack,LogType type){if(report.errors.Count<8&&(type==LogType.Error||type==LogType.Exception||type==LogType.Assert))report.errors.Add(text+"\n"+stack);}
        static void Tick()
        {
            if(flow==null)return;EditorApplication.QueuePlayerLoopUpdate();
            try{if(EditorApplication.timeSinceStartup<next)return;if(!Application.isPlaying)throw new Exception("Play stopped");if(!flow.MoveNext()){Finish(true);return;}next=EditorApplication.timeSinceStartup+(flow.Current is float d?d:0);}
            catch(Exception e){report.errors.Add(Status+": "+e);Finish(false);}
        }
        static void Finish(bool ok)
        {
            EditorApplication.update-=Tick;Application.logMessageReceived-=Log;flow=null;
            if(s!=null&&Application.isPlaying)
            {
                s.State.survival.sedations.Clear();s.hunting.HoldTranquilized(false);s.RequestAdminTest(ArchiveAdminTestAction.ResetMatch);selector.Select(original);
                s.suppressDefeatForVerification=oldDefeat;s.worldProgression.suppressThreatsForVerification=oldThreats;
            }
            report.passed=ok&&report.errors.Count==0;Status=report.passed?"Passed":"Failed";Save();
        }
        static Rect ScreenRect(RectTransform r)
        {var corners=new Vector3[4];r.GetWorldCorners(corners);return Rect.MinMaxRect(corners[0].x,corners[0].y,corners[2].x,corners[2].y);}
        static void Click()
        {
            var b=v.UseEquippedButton;Check(b.isActiveAndEnabled&&b.interactable,"Circular item button is active and usable");
            var p=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,b.transform.position),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);
            Check(hits.Count>0&&(hits[0].gameObject==b.gameObject||hits[0].gameObject.transform.IsChildOf(b.transform)),"Actual circular action pointer reaches its target without covering Hand or Jump");
            ExecuteEvents.Execute(b.gameObject,p,ExecuteEvents.pointerClickHandler);
        }
        static bool AimAtExistingBody(KhezuHuntingController monster)
        {
            var body=monster.GetComponent<BoxCollider>();var filter=new NavMeshQueryFilter{agentTypeID=monster.Agent.agentTypeID,areaMask=monster.Agent.areaMask};
            var player=Player.LocalInstance;var cc=player.GetComponent<CharacterController>();
            foreach(float radius in new[]{28f,24f,18f})for(int i=0;i<32;i++)
            {
                float angle=i*Mathf.PI/16;Vector3 desired=monster.transform.position+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius;
                if(!NavMesh.SamplePosition(desired,out var nav,3,filter))continue;
                float foot=player.transform.position.y-LogicLegends.OpenArchive.Water.OpenArchiveWater.Feet(cc).y;
                cc.enabled=false;player.transform.SetPositionAndRotation(nav.position+Vector3.up*(foot+.05f),Quaternion.LookRotation(monster.transform.position-nav.position));player.ResetMotionAfterTeleport();cc.enabled=true;Physics.SyncTransforms();
                TeleportCameraSync.ResetAfterMove(player.transform,Vector3.zero);
                Vector3 origin=cc.bounds.center+Vector3.up*cc.bounds.extents.y*.45f;
                var hit=Physics.RaycastAll(origin,(body.bounds.center-origin).normalized,ArchiveTopicSession.TranquilizerRange,~0,QueryTriggerInteraction.Collide)
                    .Where(h=>!h.collider.transform.IsChildOf(player.transform)&&(!h.collider.isTrigger||h.collider.GetComponentInParent<KhezuHuntingController>()!=null)).OrderBy(h=>h.distance).FirstOrDefault();
                if(hit.collider==null||hit.collider.GetComponentInParent<KhezuHuntingController>()!=monster)continue;
                var direction=body.bounds.center-Camera.main.transform.position;var euler=Quaternion.LookRotation(direction).eulerAngles;
                float pitch=Mathf.DeltaAngle(0,euler.x);player.GetComponentInChildren<CameraTargetController>().SetViewAngles(pitch,euler.y);
                return true;
            }
            return false;
        }
        static IEnumerator Flow()
        {
            s.Initialize(102981);ArchiveTopicUI.Current.Close();s.RequestAdminTest(ArchiveAdminTestAction.GrantAllItems,-1);yield return .3f;
            var follow=UnityEngine.Object.FindFirstObjectByType<Unity.Cinemachine.CinemachineThirdPersonFollow>();
            Vector3 originalShoulder=follow.ShoulderOffset;float originalSide=follow.CameraSide;
            Check(v.dartSound!=null&&v.dartSound.name=="Archive_Tranquilizer_Dry_Pneumatic_Shot"&&v.dartSound.channels==1&&v.dartSound.length<.5f,
                "Saved gun uses the short mono dry pneumatic and mechanical report instead of the water throw clip");
            v.SelectHotbarSlot(0);yield return .25f;
            var rect=ScreenRect(v.MobileItemActionRect);var hand=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b=>b.name=="InteractButton");var jump=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b=>b.name=="JumpButton");
            Check(v.HasCircularItemAction&&Mathf.Abs(rect.width-rect.height)<2&&rect.height>Screen.height*.13f,
                "Item action is a separate large circular mobile icon like the existing Hand control");
            Check(!rect.Overlaps(ScreenRect((RectTransform)hand.transform))&&!rect.Overlaps(ScreenRect((RectTransform)jump.transform))&&Screen.safeArea.Contains(rect.min)&&Screen.safeArea.Contains(rect.max),
                "Circular action fits the safe area beside Hand and below Jump without overlap");
            Check(ArchiveTopicSession.BatteryRefill==240&&ArchiveTopicSession.FlashlightCapacity==240&&Mathf.Abs(s.FlashlightRemaining(-1)-240)<.1f&&v.SelectedActionHint.Contains("04:00"),
                "A fresh flashlight and each replacement battery provide four minutes, displayed as 04:00");
            Click();yield return .5f;Check(s.Inventory(-1).flashlightOn&&s.FlashlightRemaining(-1)>238,"Circular flashlight button turns on real charge drain");
            for(int i=0;i<40&&!v.UseEquippedButton.interactable;i++)yield return .05f;
            Click();yield return .5f;Check(!s.Inventory(-1).flashlightOn,"The same circular icon switches the light off");
            yield return .3f;ScreenCapture.CaptureScreenshot("Temp/ArchiveMobileItems/Circular_Item_Action.png");yield return .2f;

            foreach(var kind in new[]{DevelopmentMonster.Rajang,DevelopmentMonster.Khezu})
            {
                Status=kind+" actual shot and sleep at impact";
                s.State.survival.sedations.Clear();s.hunting.HoldTranquilized(false);selector.Select(kind);yield return .3f;
                var m=s.hunting;var snap=m.GetSnapshot();snap.state=(int)KhezuHuntState.Ready;snap.target=snap.caught=0;snap.pending=false;snap.awake=true;snap.mistakes=snap.stage=4;snap.animation="Idle";snap.animationSpeed=1;m.ApplySnapshot(snap,true);m.SetPlayerEligibility(-1,false);yield return .2f;
                Check(AimAtExistingBody(m),kind+" fixture has a clear real actor-origin body ray");yield return .5f;
                var body=m.GetComponent<BoxCollider>();var camera=Camera.main;var euler=Quaternion.LookRotation(body.bounds.center-camera.transform.position).eulerAngles;
                Player.LocalInstance.GetComponentInChildren<CameraTargetController>().SetViewAngles(Mathf.DeltaAngle(0,euler.x),euler.y);yield return .3f;
                s.RequestEquipItem((int)ArchiveSurvivalItem.Tranquilizer);yield return .65f;
                Check(follow.ShoulderOffset.x>originalShoulder.x+.8f,
                    kind+" equipped gun shifts the existing camera over the shoulder to clear the avatar from the reticle");
                Vector3 origin=m.transform.position;Quaternion rotation=m.transform.rotation;int mistakes=m.Mistakes;int stage=m.GetSnapshot().stage;int darts=s.CountItem(-1,ArchiveSurvivalItem.TranquilizerAmmo);int shot=s.State.survival.shotSerial;
                // Freeze the camera's reticle on the actual collider at the command frame;
                // the orbit camera may still be damping after the explicit test teleport.
                Camera.main.transform.LookAt(body.bounds.center);
                Click();yield return .035f;
                var fx=s.GetComponent<ArchiveTranquilizerVfx>();
                Check(s.State.survival.shotSerial==shot+1&&s.State.survival.shotHit&&m.IsTranquilized&&s.CountItem(-1,ArchiveSurvivalItem.TranquilizerAmmo)==darts-1,
                    kind+" actual circular FIRE hits the existing monster and spends exactly one dart");
                Check(v.GetComponents<AudioSource>().Any(a=>a.isPlaying),kind+" accepted shot plays the saved sound through its actual AudioSource");
                Check(fx!=null&&fx.LastVisualSerial==s.State.survival.shotSerial&&fx.LastVisualHit&&fx.VisibleProjectileCount>0&&fx.LastProjectileRenderer.GetComponent<MeshFilter>().sharedMesh.vertexCount>20,
                    kind+" accepted shot produces a visible 3D dart rather than an instantaneous line");
                ScreenCapture.CaptureScreenshot("Temp/ArchiveMobileItems/"+kind+"_Dart_In_Flight.png");
                Vector3 first=fx.LastProjectilePosition;yield return .055f;
                Check(Vector3.Distance(first,fx.LastProjectilePosition)>.1f&&fx.LastFlightProgress>0,
                    kind+" projectile advances from the real barrel toward the validated hit");
                Check(fx.PoolCapacity==4&&fx.GetComponentsInChildren<Collider>(true).Length==0,
                    "Dart FX stays in a four-shot pool and never adds duplicate collision or gameplay physics");
                yield return .6f;
                Check(m.IsTranquilizedSleepPose&&m.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Sleep")&&!m.GetComponent<Animator>().IsInTransition(0),
                    kind+" blends into its existing settled Sleep animation");
                Check(Vector3.Distance(origin,m.transform.position)<.03f&&Quaternion.Angle(rotation,m.transform.rotation)<.2f&&m.Mistakes==mistakes&&m.GetSnapshot().stage==stage&&m.GetSnapshot().animation=="Sleep",
                    kind+" sleeps at the exact shot location without nest teleport, rotation or awakening reset");
                ScreenCapture.CaptureScreenshot("Temp/ArchiveMobileItems/"+kind+"_Sleeping_On_Spot.png");yield return .2f;
                var effect=s.State.survival.sedations.First(e=>e.encounterId==m.encounterId);effect.until=ArchiveTopicSession.Clock-.01;yield return .7f;
                Check(!m.IsTranquilized&&Mathf.Abs(m.GetComponent<Animator>().speed-1)<.01f&&!m.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Sleep"),
                    kind+" exits Sleep normally when the original sedation checkpoint expires");
                m.Request(KhezuCommand.Sleep);
            }
            s.RequestEquipItem(-1);yield return .6f;
            Check(Vector3.Distance(follow.ShoulderOffset,originalShoulder)<.02f&&Mathf.Abs(follow.CameraSide-originalSide)<.001f,
                "Unequipping the gun restores the original camera shoulder and side");
        }
    }
}
