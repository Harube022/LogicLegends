using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LogicLegends.OpenArchive.Hunting;
using Photon.Pun;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>Opt-in native requests against fixture supplies. Never grants production items.</summary>
    public static class ArchiveSupplyChecks
    {
        [Serializable] sealed class Report
        {
            public bool passed;
            public string scope="Offline Editor Play Mode. Supply quantities and missing-heart states are explicit test fixtures; all item uses explicitly equip the owned slot before the actual authoritative RequestUseEquippedItem path. Short-charge depletion, standalone ammo rejection, zero-ammo rejection, physical missed shots, and monster body rays observe actual supply effects and sedation expiry/immunity. Native touch and physical Android are not verified.";
            public List<string> checks=new List<string>();
            public List<string> errors=new List<string>();
        }
        static Report report;
        static IEnumerator flow;
        static ArchiveTopicSession session;
        static ArchiveSurvivalInventory inventory;
        static KhezuHuntingController monster;
        static double next;
        static bool previousDefeatSuppression,previousWorldSuppression;
        static Vector3 previousPlayerPosition;
        static Quaternion previousPlayerRotation;
        static HealthManager.ActorHealth[] previousHealth;
        public static string Status {get;private set;}="Idle";
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test Survival Supplies (Offline Play Mode)")]
        public static void Begin()
        {
            if(!Application.isPlaying||PhotonNetwork.InRoom||flow!=null)throw new InvalidOperationException("Use offline Play Mode with no running supply check.");
            session=ArchiveTopicSession.Current;
            if(session==null||!session.Ready||Player.LocalInstance==null)throw new InvalidOperationException("The saved multiplayer scene/player must be ready.");
            previousDefeatSuppression=session.suppressDefeatForVerification;session.suppressDefeatForVerification=true;
            previousWorldSuppression=session.worldProgression!=null&&session.worldProgression.suppressThreatsForVerification;
            if(session.worldProgression!=null)session.worldProgression.suppressThreatsForVerification=true;
            previousPlayerPosition=Player.LocalInstance.transform.position;previousPlayerRotation=Player.LocalInstance.transform.rotation;
            previousHealth=session.hunting.health.ExportActorHealth();
            report=new Report();flow=Flow();next=0;Status="Initialize supply fixtures";
            EditorApplication.update+=Tick;Application.logMessageReceived+=Log;Save();
        }
        static void Check(bool value,string name)
        {report.checks.Add((value?"PASS ":"FAIL ")+name);Save();if(!value)throw new InvalidOperationException(name);}
        static void Log(string message,string stack,LogType type)
        {if(report.errors.Count<12&&(type==LogType.Error||type==LogType.Exception||type==LogType.Assert))report.errors.Add(message+"\n"+stack);}
        static void Tick()
        {
            if(flow==null)return;EditorApplication.QueuePlayerLoopUpdate();
            try
            {
                if(!Application.isPlaying)throw new InvalidOperationException("Play Mode stopped during supply verification.");
                if(EditorApplication.timeSinceStartup<next)return;
                if(!flow.MoveNext()){Finish(true);return;}
                next=EditorApplication.timeSinceStartup+(flow.Current is float seconds?seconds:0);
            }
            catch(Exception e){report.errors.Add(Status+": "+e);Finish(false);}
        }
        static void Save(){Directory.CreateDirectory("Temp/ArchiveSurvival");File.WriteAllText("Temp/ArchiveSurvival/Supplies.json",JsonUtility.ToJson(report,true));}
        static void Finish(bool success)
        {
            EditorApplication.update-=Tick;Application.logMessageReceived-=Log;flow=null;
            if(session!=null&&Application.isPlaying)
            {
                session.suppressDefeatForVerification=previousDefeatSuppression;
                if(session.worldProgression!=null)session.worldProgression.suppressThreatsForVerification=previousWorldSuppression;
                session.State.survival.sedations.Clear();
                foreach(var hunt in KhezuHuntingController.ActiveMonsters){hunt.HoldTranquilized(false);hunt.SetPlayerEligibility(-1,true);hunt.Request(KhezuCommand.Sleep);}
                if(previousHealth!=null)session.hunting.health.ApplyActorHealthSnapshot(previousHealth);
                if(inventory!=null)inventory.flashlightOn=false;
                MovePlayer(previousPlayerPosition,previousPlayerRotation);
            }
            report.passed=success&&report.errors.Count==0;Status=report.passed?"Passed":"Failed";Save();
            Debug.Log("Open Archive supply verification: "+Status+", "+report.checks.Count+" checks.");
        }
        static void FixtureHealth(int hearts)
        {
            var old=session.hunting.health.ExportActorHealth().FirstOrDefault(h=>h.actor==-1);
            session.hunting.health.ApplyActorHealthSnapshot(new[]{new HealthManager.ActorHealth{actor=-1,hearts=hearts,lastPenaltySerial=old?.lastPenaltySerial??0}});
        }
        static void FixtureItem(ArchiveSurvivalItem item,int count){inventory.items[(int)item]=count;inventory.nextUseAt=0;}
        static bool UseFromHotbar(ArchiveSurvivalItem item)
        {
            if(session.EquippedItem(-1)!=(int)item&&!session.RequestEquipItem((int)item))return false;
            return session.RequestUseEquippedItem();
        }
        static void MovePlayer(Vector3 point,Quaternion rotation)
        {
            var player=Player.LocalInstance;if(player==null)return;
            var controller=player.GetComponent<CharacterController>();Vector3 delta=point-player.transform.position;
            if(GameInput.Instance!=null)GameInput.Instance.SetMobileMovement(Vector2.zero);
            controller.enabled=false;player.transform.SetPositionAndRotation(point,rotation);controller.enabled=true;player.ResetMotionAfterTeleport();
            Physics.SyncTransforms();TeleportCameraSync.ResetAfterMove(player.transform,delta);
        }
        static Vector3 ShotOrigin()
        {var capsule=Player.LocalInstance.GetComponent<CharacterController>();return capsule.bounds.center+Vector3.up*capsule.bounds.extents.y*.45f;}
        static KhezuHuntingController RayTarget(Vector3 origin,Vector3 direction)
        {
            foreach(var hit in Physics.RaycastAll(origin,direction,ArchiveTopicSession.TranquilizerRange,~0,QueryTriggerInteraction.Collide).OrderBy(h=>h.distance))
            {
                if(hit.collider.transform.IsChildOf(Player.LocalInstance.transform))continue;
                var hunt=hit.collider.GetComponentInParent<KhezuHuntingController>();
                if(hit.collider.isTrigger&&hunt==null)continue;
                return hunt;
            }
            return null;
        }
        static bool PositionForActualBodyRay()
        {
            var body=monster.GetComponent<BoxCollider>();if(body==null||!body.enabled||Camera.main==null)return false;
            var filter=new NavMeshQueryFilter{agentTypeID=monster.Agent.agentTypeID,areaMask=monster.Agent.areaMask};
            foreach(float radius in new[]{18f,24f,14f})for(int i=0;i<32;i++)
            {
                float angle=i*Mathf.PI/16;var desired=monster.transform.position+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius;
                if(!NavMesh.SamplePosition(desired,out var nav,3,filter))continue;
                var player=Player.LocalInstance;var capsule=player.GetComponent<CharacterController>();
                float footOffset=player.transform.position.y-LogicLegends.OpenArchive.Water.OpenArchiveWater.Feet(capsule).y;
                MovePlayer(nav.position+Vector3.up*(footOffset+.05f),Quaternion.LookRotation(monster.transform.position-nav.position,Vector3.up));
                var direction=body.bounds.center-ShotOrigin();if(direction.sqrMagnitude<1||direction.magnitude>30)continue;
                if(RayTarget(ShotOrigin(),direction.normalized)!=monster)continue;
                Camera.main.transform.rotation=Quaternion.LookRotation(body.bounds.center-Camera.main.transform.position,Vector3.up);return true;
            }
            return false;
        }
        static bool FireAtActualBody()
        {
            Physics.SyncTransforms();var body=monster.GetComponent<BoxCollider>();var direction=body.bounds.center-ShotOrigin();
            Camera.main.transform.rotation=Quaternion.LookRotation(body.bounds.center-Camera.main.transform.position,Vector3.up);
            return UseFromHotbar(ArchiveSurvivalItem.Tranquilizer);
        }
        static bool FireAtPhysicalGround()
        {
            Physics.SyncTransforms();
            var hit=Physics.RaycastAll(ShotOrigin(),Vector3.down,ArchiveTopicSession.TranquilizerRange,~0,QueryTriggerInteraction.Collide)
                .Where(h=>!h.collider.transform.IsChildOf(Player.LocalInstance.transform)&&!h.collider.isTrigger).OrderBy(h=>h.distance).FirstOrDefault();
            if(hit.collider==null||hit.collider.GetComponentInParent<KhezuHuntingController>()!=null)return false;
            // A real non-monster collider stops this deliberately missed dart. The aim is
            // supplied through the same camera/equip/use path as ordinary local firing.
            Camera.main.transform.rotation=Quaternion.LookRotation(hit.point-Camera.main.transform.position,Vector3.up);
            return UseFromHotbar(ArchiveSurvivalItem.Tranquilizer);
        }
        static IEnumerator Flow()
        {
            session.Initialize(611053);ArchiveTopicUI.Current.Close();
            foreach(var hunt in KhezuHuntingController.ActiveMonsters){hunt.Request(KhezuCommand.Sleep);hunt.SetPlayerEligibility(-1,false);}
            monster=session.hunting;yield return .15f;
            session.hunting.health.RegisterActors(new[]{-1});FixtureHealth(session.hunting.health.MaximumHealth);
            inventory=new ArchiveSurvivalInventory{actor=-1,batteryUpdatedAt=ArchiveTopicSession.Clock};session.State.survival.players.Add(inventory);
            Check(ArchiveRewardView.Current!=null,"Saved scene installs the actual supply presentation");
            Status="Medkits use authoritative shared health";
            FixtureItem(ArchiveSurvivalItem.Medkit,2);
            Check(!UseFromHotbar(ArchiveSurvivalItem.Medkit)&&session.CountItem(-1,ArchiveSurvivalItem.Medkit)==2,"Full-health use is rejected without consuming a medkit");
            int maximum=session.hunting.health.MaximumHealth;Check(session.hunting.health.DamageAuthoritativeActor(-1),"Fixture creates exactly one missing heart through the health owner");
            Check(UseFromHotbar(ArchiveSurvivalItem.Medkit)&&session.hunting.health.ActorHearts(-1)==maximum&&session.CountItem(-1,ArchiveSurvivalItem.Medkit)==1,"One medkit restores exactly one heart and consumes exactly one item");
            yield return .5f;
            FixtureHealth(0);int medkits=session.CountItem(-1,ArchiveSurvivalItem.Medkit);
            Check(!UseFromHotbar(ArchiveSurvivalItem.Medkit)&&session.CountItem(-1,ArchiveSurvivalItem.Medkit)==medkits,"A zero-heart player cannot self-revive with a medkit or consume it");
            FixtureHealth(maximum);yield return .1f;
            Check(session.CanAct(-1),"Restoring the test fixture releases its health movement restriction");
            Status="Battery compatibility, capacity and real runtime drain";
            FixtureItem(ArchiveSurvivalItem.Battery,3);
            Check(!UseFromHotbar(ArchiveSurvivalItem.Battery)&&session.CountItem(-1,ArchiveSurvivalItem.Battery)==3,"Battery without a flashlight is preserved");
            FixtureItem(ArchiveSurvivalItem.Flashlight,1);inventory.batterySeconds=10;inventory.batteryUpdatedAt=ArchiveTopicSession.Clock;
            Check(UseFromHotbar(ArchiveSurvivalItem.Battery)&&Mathf.Abs(inventory.batterySeconds-240)<.01f&&session.CountItem(-1,ArchiveSurvivalItem.Battery)==2,"One battery fills exactly four minutes (240 seconds) and consumes one item");
            yield return .5f;inventory.batterySeconds=220;
            Check(UseFromHotbar(ArchiveSurvivalItem.Battery)&&Mathf.Abs(inventory.batterySeconds-240)<.01f&&session.CountItem(-1,ArchiveSurvivalItem.Battery)==1,"Charge caps at one four-minute battery, never stacks above 240 seconds");
            yield return .5f;
            Check(!UseFromHotbar(ArchiveSurvivalItem.Battery)&&session.CountItem(-1,ArchiveSurvivalItem.Battery)==1,"A full battery cannot waste another battery item");
            inventory.batterySeconds=12;inventory.batteryUpdatedAt=ArchiveTopicSession.Clock;inventory.nextUseAt=0;
            Check(UseFromHotbar(ArchiveSurvivalItem.Flashlight),"Flashlight turns on through the actual item request");yield return .55f;
            Check(UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Any(l=>l.name=="Actor_-1_Flashlight"&&l.enabled&&l.type==LightType.Spot&&l.shadows==LightShadows.None),"Flashlight output is a real enabled lightweight spotlight");
            float initial=12;double litSeconds=.55;
            for(int i=0;i<5;i++)
            {
                Check(UseFromHotbar(ArchiveSurvivalItem.Flashlight)&&!inventory.flashlightOn,"Rapid toggle off "+(i+1)+" is accepted");yield return .5f;
                Check(UseFromHotbar(ArchiveSurvivalItem.Flashlight)&&inventory.flashlightOn,"Rapid toggle on "+(i+1)+" is accepted");yield return .55f;litSeconds+=.55;
            }
            Check(initial-session.FlashlightRemaining(-1)>2&&initial-session.FlashlightRemaining(-1)>litSeconds-.65,"Frequent toggles cannot reset or evade more than two seconds of actual charge drain");
            UseFromHotbar(ArchiveSurvivalItem.Flashlight);yield return .5f;
            Status="Depleted flashlight and passive ammunition";
            inventory.batterySeconds=.18f;inventory.batteryUpdatedAt=ArchiveTopicSession.Clock;inventory.nextUseAt=0;
            Check(UseFromHotbar(ArchiveSurvivalItem.Flashlight)&&inventory.flashlightOn,"A short remaining charge can turn on through the actual equipped item");yield return 1.35f;
            Check(!inventory.flashlightOn&&session.FlashlightRemaining(-1)==0&&
                !UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Any(l=>l.name=="Actor_-1_Flashlight"&&l.enabled),
                "Real charge depletion automatically turns off the flashlight and its spotlight");
            Check(!UseFromHotbar(ArchiveSurvivalItem.Flashlight)&&!inventory.flashlightOn&&session.CountItem(-1,ArchiveSurvivalItem.Flashlight)==1,
                "An empty flashlight cannot turn on or consume the retained tool");
            FixtureItem(ArchiveSurvivalItem.TranquilizerAmmo,3);
            Check(!UseFromHotbar(ArchiveSurvivalItem.TranquilizerAmmo)&&session.CountItem(-1,ArchiveSurvivalItem.TranquilizerAmmo)==3,
                "Standalone ammo use is rejected without consuming automatically loaded darts");
            Status="Actual monster body hit and eight-second sedation";
            var snapshot=monster.GetSnapshot();snapshot.state=(int)KhezuHuntState.Ready;snapshot.target=snapshot.caught=0;snapshot.pending=false;snapshot.awake=true;
            snapshot.mistakes=snapshot.stage=4;snapshot.animation="Idle";snapshot.animationSpeed=1;monster.ApplySnapshot(snapshot,true);monster.SetPlayerEligibility(-1,false);
            Check(monster.CanTranquilize&&PositionForActualBodyRay(),"Fixture aims from the avatar at the existing monster body collider along a clear physical ray");
            FixtureItem(ArchiveSurvivalItem.Tranquilizer,1);FixtureItem(ArchiveSurvivalItem.TranquilizerAmmo,0);int hearts=session.hunting.health.ActorHearts(-1);
            int beforeShot=session.State.survival.shotSerial;
            Check(!UseFromHotbar(ArchiveSurvivalItem.Tranquilizer)&&session.State.survival.shotSerial==beforeShot&&
                session.CountItem(-1,ArchiveSurvivalItem.TranquilizerAmmo)==0&&session.CountItem(-1,ArchiveSurvivalItem.Tranquilizer)==1,
                "A gun with no darts rejects Fire without inventing a shot or consuming equipment");
            FixtureItem(ArchiveSurvivalItem.TranquilizerAmmo,5);
            Check(FireAtPhysicalGround()&&session.State.survival.shotSerial==beforeShot+1&&!session.State.survival.shotHit&&
                session.State.survival.shotEnd.y<session.State.survival.shotOrigin.y,
                "A deliberate physical ground hit is a real missed dart rather than a monster hit");
            Check(session.CountItem(-1,ArchiveSurvivalItem.TranquilizerAmmo)==4&&session.hunting.health.ActorHearts(-1)==hearts&&
                !monster.IsTranquilized&&session.State.survival.sedations.Count==0,
                "A missed dart consumes exactly one round without heart damage or monster sedation");yield return .5f;
            Check(FireAtActualBody()&&session.State.survival.shotHit&&monster.IsTranquilized,"A real ray hit sedates the existing monster");
            Check(session.CountItem(-1,ArchiveSurvivalItem.TranquilizerAmmo)==3&&session.hunting.health.ActorHearts(-1)==hearts,"A shot consumes one dart and deals no heart damage");
            var effect=session.State.survival.sedations.First(s=>s.encounterId==monster.encounterId);
            Check(Math.Abs(effect.until-session.State.survival.shotAt-8)<.1&&Math.Abs(effect.immuneUntil-effect.until-20)<.1,"Sedation lasts eight seconds followed by twenty seconds of immunity");
            yield return 7.0f;Check(monster.IsTranquilized&&monster.IsTranquilizedSleepPose&&
                monster.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Sleep")&&
                Mathf.Abs(monster.GetComponent<Animator>().speed-1)<.01f&&Vector3.Distance(monster.transform.position,monster.TranquilizedAnchorPosition)<.03f,
                "Monster loops the actual Sleep pose at the shot location while its eight-second sedation is active");
            yield return 1.3f;
            Check(!monster.IsTranquilized&&Mathf.Abs(monster.GetComponent<Animator>().speed-1)<.01f,"Sedation expires automatically and restores normal animation speed");
            double firstUntil=effect.until;
            Check(FireAtActualBody()&&!session.State.survival.shotHit&&!monster.IsTranquilized&&Math.Abs(effect.until-firstUntil)<.001,"An immediate follow-up cannot extend sedation during wake-up immunity");
            Check(session.CountItem(-1,ArchiveSurvivalItem.TranquilizerAmmo)==2,"A resisted shot still consumes exactly one fired dart");
            Status="Twenty-second post-wake immunity expiration";
            yield return Mathf.Max(.1f,(float)(effect.immuneUntil-ArchiveTopicSession.Clock)+.15f);
            Check(FireAtActualBody()&&session.State.survival.shotHit&&monster.IsTranquilized&&session.CountItem(-1,ArchiveSurvivalItem.TranquilizerAmmo)==1,"A fresh dart works after the complete twenty-second wake-up immunity");
            Check(session.hunting.health.ActorHearts(-1)==hearts,"Supply and sedation checks preserve player hearts");
            session.State.survival.sedations.Clear();monster.HoldTranquilized(false);monster.SetPlayerEligibility(-1,true);
            monster.Request(KhezuCommand.StartHunting);yield return .4f;
            Check(monster.State==KhezuHuntState.Hunting||monster.State==KhezuHuntState.NavigationBlocked,"Existing hunting controller resumes normal pursuit after sedation");
        }
    }
}
