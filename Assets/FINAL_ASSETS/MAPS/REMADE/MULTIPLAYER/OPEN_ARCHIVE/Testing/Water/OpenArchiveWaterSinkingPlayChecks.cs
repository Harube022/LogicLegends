#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using LogicLegends.OpenArchive.Hunting;
using LogicLegends.OpenArchive.Water;
using LogicLegends.OpenArchiveTesting;
using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.Tests.Water
{
    public sealed class OpenArchiveWaterSinkingPlayChecks : MonoBehaviour
    {
        const string Reports = "Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Testing/Water/Reports";
        [Serializable] sealed class Check {public string name, detail; public bool passed;}
        [Serializable] sealed class Sample {public float seconds, depth, rootY, modelY;}
        [Serializable] sealed class Report {
            public string checkedUtc, scope = "Offline Editor using the original enabled Player and GameInput. Saved grace, sinking and recovery settings are exercised in real time; shorter Inspector values are used only for additional cases, then restored. Character visuals lower while the original capsule stays on the riverbed, retaining movement, jumping and bridge clearance. No health or network protocol changes. Live multiplayer not verified.";
            public bool passed; public List<Check> checks = new List<Check>(); public List<Sample> sinking = new List<Sample>();
        }
        readonly Report report = new Report();
        Player player; CharacterController capsule; Transform root, visual;
        OpenArchiveWater water; OpenArchiveWaterSinking safety;
        int jumps;
        public static void Begin() {
            if(!Application.isPlaying || PhotonNetwork.InRoom) throw new InvalidOperationException("Use offline Play Mode.");
            new GameObject("Water_Sinking_Play_Checks").AddComponent<OpenArchiveWaterSinkingPlayChecks>();
        }
        void Record(string name,bool pass,string detail=""){report.checks.Add(new Check{name=name,passed=pass,detail=detail});}
        static Vector3 Polar(float a,float r){a*=Mathf.Deg2Rad;return new Vector3(Mathf.Sin(a)*r,0,Mathf.Cos(a)*r);}
        void Jumped(){jumps++;}
        void Input(Vector3 direction){
            direction.y=0;var f=Camera.main.transform.forward;f.y=0;f.Normalize();var r=Camera.main.transform.right;r.y=0;r.Normalize();
            GameInput.Instance.SetMobileMovement(new Vector2(Vector3.Dot(direction.normalized,r),Vector3.Dot(direction.normalized,f)));
        }
        void Place(Vector3 point,bool bed){
            Input(Vector3.zero);float surface;var origin=point+Vector3.up*30;
            if(bed && water.SampleSurface(point,out surface))origin.y=surface+.1f;
            RaycastHit floor;if(!Physics.Raycast(origin,Vector3.down,out floor,70,~(1<<7),QueryTriggerInteraction.Ignore))throw new InvalidOperationException("Missing support.");
            point.y=floor.point.y-(capsule.center.y-capsule.height*.5f)*player.transform.lossyScale.y+capsule.skinWidth;
            capsule.enabled=false;player.transform.position=point;capsule.enabled=true;player.ResetMotionAfterTeleport();
        }
        float Bank(){float lo=23,hi=35.5f,y;for(int i=0;i<24;i++){float m=(lo+hi)*.5f;if(water.SampleSurface(root.TransformPoint(Polar(30,m)),out y))hi=m;else lo=m;}return hi;}
        IEnumerator Start(){
            yield return new WaitForSecondsRealtime(.6f);
            player=Player.LocalInstance;capsule=player.GetComponent<CharacterController>();root=GameObject.Find("OpenArchive").transform;
            water=FindFirstObjectByType<OpenArchiveWater>();safety=water.GetComponent<OpenArchiveWaterSinking>();visual=player.GetComponentInChildren<PlayerAnimator>().transform;
            var session=FindFirstObjectByType<OpenArchiveExplorationSession>();var h=FindFirstObjectByType<KhezuHuntingController>();
            var oldPosition=player.transform.position;var oldRotation=player.transform.rotation;var originalVisual=visual.localPosition;
            bool oldSession=session.enabled,oldBlock=GameInput.Instance.GameplayInputBlocked,oldHunting=h.enabled;float oldScale=Time.timeScale;
            float oldGrace=safety.safeWaterSeconds,oldSink=safety.sinkingSeconds,oldRecovery=safety.dryRecoverySeconds;
            player.OnJumpStarted+=Jumped;report.checkedUtc=DateTime.UtcNow.ToString("o");
            try{
                session.enabled=false;GameInput.Instance.SetGameplayInputBlocked(false);Time.timeScale=1;h.Request(KhezuCommand.Sleep);
                Record("Saved default timing",oldGrace==5 && oldSink==4 && oldRecovery==5);
                // Finish awakening first so this check does not depend on the grace period exceeding the wake clips.
                h.Request(KhezuCommand.ForceFourMistakes);float awakeDeadline=Time.time+25;
                while(h.State!=KhezuHuntState.Hunting && Time.time<awakeDeadline)yield return null;
                // Isolate recovery physics from a legitimate capture immediately on reaching the bank.
                h.enabled=false;var nav=h.GetComponent<UnityEngine.AI.NavMeshAgent>();if(nav.enabled&&nav.isOnNavMesh)nav.isStopped=true;
                Place(root.TransformPoint(Polar(30,35.5f)),true);yield return new WaitForSeconds(.25f);
                float originalRootY=player.transform.position.y;int returns=safety.ForcedReturnCount;
                yield return new WaitForSeconds(2);
                Record("Standing in water consumes grace without immediately sinking",safety.LocalWaterSeconds>2 && safety.LocalSinkProgress==0 && safety.LocalVisualDepth<.001f);
                Record("Warning is visible and does not intercept controls",safety.WarningVisible && Array.TrueForAll(GameObject.Find("OpenArchive_Water_Warning").GetComponentsInChildren<Graphic>(),g=>!g.raycastTarget));
                float elapsed=safety.LocalWaterSeconds;Time.timeScale=0;yield return new WaitForSecondsRealtime(.35f);
                Record("Pause freezes exposure without invalid visual offsets",Mathf.Abs(safety.LocalWaterSeconds-elapsed)<.001f && !float.IsNaN(safety.LocalVisualDepth));Time.timeScale=1;
                player.enabled=false;elapsed=safety.LocalWaterSeconds;yield return new WaitForSeconds(.3f);
                Record("Disabled player is not drowned during cinematic control",Mathf.Abs(safety.LocalWaterSeconds-elapsed)<.001f && visual.localPosition==originalVisual);player.enabled=true;
                float next=0,deadline=Time.time+16;
                while(safety.ForcedReturnCount==returns && Time.time<deadline){
                    if(safety.LocalWaterSeconds>=next){next=safety.LocalWaterSeconds+.25f;report.sinking.Add(new Sample{seconds=safety.LocalWaterSeconds,depth=safety.LocalVisualDepth,rootY=player.transform.position.y,modelY=visual.position.y});}
                    if(safety.LocalSinkProgress>.45f && !File.Exists(Reports+"/SinkingProgress.png"))ScreenCapture.CaptureScreenshot(Reports+"/SinkingProgress.png");
                    yield return null;
                }
                bool gradual=report.sinking.Exists(s=>s.seconds>oldGrace && s.depth>.1f) && report.sinking.Exists(s=>s.seconds>oldGrace+oldSink*.65f && s.depth>1);
                bool fixedCapsule=report.sinking.TrueForAll(s=>Mathf.Abs(s.rootY-originalRootY)<.1f);
                Record("Character gradually lowers after the configured grace period",gradual);
                Record("Existing capsule remains supported throughout the sinking visual",fixedCapsule);
                Record("Full submersion produces exactly one forced return",safety.ForcedReturnCount==returns+1);
                int huntSerial=h.HuntSerial,target=h.TargetPlayerId,captures=h.CaptureSerial;
                yield return new WaitForSeconds(.3f);
                var delta=player.transform.position-root.TransformPoint(Polar(30,35.5f));delta.y=0;
                Record("Return uses a nearby dry bank, restores movement and preserves exhaustion",delta.magnitude<=safety.maximumShoreReturnDistance+1 && capsule.isGrounded && !water.IsInWater(capsule) && visual.localPosition==originalVisual && player.MovementSpeedMultiplier==1 && player.enabled && safety.LocalWaterSeconds>=oldGrace+oldSink);
                Record("Shore recovery preserves the active hunt identity and target",huntSerial>0 && target==-1 && h.HuntSerial==huntSerial && h.TargetPlayerId==target && h.CaptureSerial==captures && (h.State==KhezuHuntState.Hunting || h.State==KhezuHuntState.NavigationBlocked));
                h.enabled=true;
                h.Request(KhezuCommand.ResetHunt);
                safety.safeWaterSeconds=100;
                float bank=Bank();var shelf=root.TransformPoint(Polar(30,bank+1.2f/root.lossyScale.x));
                Place(shelf,true);yield return new WaitForSeconds(.5f);elapsed=safety.LocalWaterSeconds;int jumpBefore=jumps;
                GameInput.Instance.MobileJump();float end=Time.time+1.1f;bool aboveWater=false;float y;
                while(Time.time<end){if(water.SampleSurface(OpenArchiveWater.Feet(capsule),out y))aboveWater|=OpenArchiveWater.Feet(capsule).y>y+.2f;yield return null;}
                Record("Bunny hopping above the water does not reset exposure",jumps>jumpBefore && aboveWater && safety.LocalWaterSeconds>elapsed+.9f);
                // Fresh partial exposure at the same jumpable shelf.
                safety.enabled=false;yield return null;safety.enabled=true;safety.safeWaterSeconds=1;safety.sinkingSeconds=10;
                yield return new WaitForSeconds(3.2f);
                Record("Sinking still occurs on shallow shelves",safety.LocalVisualDepth>.2f && water.IsInWater(capsule));
                jumpBefore=jumps;GameInput.Instance.MobileJump();end=Time.time+3;var dry=root.TransformPoint(Polar(30,bank-2));bool arrived=false;
                while(Time.time<end){delta=dry-player.transform.position;delta.y=0;if(delta.magnitude<.3f){arrived=true;break;}Input(delta);yield return null;}Input(Vector3.zero);
                yield return new WaitForSeconds(.35f);
                Record("Original jump can escape while already partially sinking",arrived && jumps>jumpBefore && capsule.isGrounded && OpenArchiveWater.Feet(capsule).y>-.25f && safety.LocalVisualDepth<.1f);
                Record("Brief dry contact does not instantly refill grace",safety.LocalWaterSeconds>0);
                yield return new WaitForSeconds(oldRecovery+.1f);
                Record("Configured time on solid land fully recovers",safety.LocalWaterSeconds==0 && safety.LocalVisualDepth<.01f && !safety.WarningVisible);
                var bridge=root.Find("Environment/Bridges").GetChild(0);safety.sinkingSeconds=1;
                returns=safety.ForcedReturnCount;Place(bridge.position,false);yield return new WaitForSeconds(3);
                Record("Standing on a bridge above water never charges the timer",safety.LocalWaterSeconds==0 && safety.ForcedReturnCount==returns && visual.localPosition==originalVisual);
                safety.sinkingSeconds=10;Place(root.TransformPoint(Polar(30,35.5f)),true);yield return new WaitForSeconds(3);
                Record("Second water visit starts a fresh grace period",safety.LocalWaterSeconds>2.8f && safety.LocalWaterSeconds<3.3f && safety.LocalVisualDepth>.05f);
                safety.enabled=false;yield return null;
                Record("Disabling the component cleans up sinking and warning UI",visual.localPosition==originalVisual && !safety.WarningVisible);
                safety.enabled=true;yield return new WaitForSeconds(2.6f);session.ReturnToSpawn();yield return new WaitForSeconds(.3f);
                Record("Existing return-to-spawn clears exposure and model offset",safety.LocalWaterSeconds==0 && safety.LocalVisualDepth==0 && visual.localPosition==originalVisual);
            }
            finally{
                Time.timeScale=oldScale;Input(Vector3.zero);player.enabled=true;player.OnJumpStarted-=Jumped;
                safety.enabled=false;safety.safeWaterSeconds=oldGrace;safety.sinkingSeconds=oldSink;safety.dryRecoverySeconds=oldRecovery;
                capsule.enabled=false;player.transform.SetPositionAndRotation(oldPosition,oldRotation);capsule.enabled=true;player.ResetMotionAfterTeleport();
                safety.enabled=true;session.enabled=oldSession;GameInput.Instance.SetGameplayInputBlocked(oldBlock);h.Request(KhezuCommand.Sleep);h.enabled=oldHunting;
                report.passed=report.checks.Count>0 && report.checks.TrueForAll(c=>c.passed);Directory.CreateDirectory(Reports);
                File.WriteAllText(Reports+"/SinkingPlayMode.json",JsonUtility.ToJson(report,true));Debug.Log("Water sinking checks: "+report.checks.FindAll(c=>c.passed).Count+"/"+report.checks.Count);Destroy(gameObject);
            }
        }
    }
}
#endif
