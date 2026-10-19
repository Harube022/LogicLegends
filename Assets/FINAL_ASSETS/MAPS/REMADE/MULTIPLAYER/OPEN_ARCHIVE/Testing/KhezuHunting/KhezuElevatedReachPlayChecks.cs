#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using LogicLegends.OpenArchive.Cinematics;
using LogicLegends.OpenArchive.Hunting;
using LogicLegends.OpenArchive.Water;
using LogicLegends.OpenArchiveTesting;
using Photon.Pun;
using UnityEngine;

namespace LogicLegends.Tests.Khezu
{
    /// <summary>Explicit offline checks; no component is added to a saved scene or player prefab.</summary>
    public sealed class KhezuElevatedReachPlayChecks : MonoBehaviour
    {
        [Serializable] sealed class Check { public string name,detail;public bool passed; }
        [Serializable] sealed class Report {
            public string checkedUtc,scope="Offline Unity Editor: real agent and enabled player on nest rock footings, plus deterministic reach/occlusion fixtures. Live multiplayer and Android device testing unverified.";
            public bool passed;public List<Check> checks=new List<Check>();
        }
        readonly Report report=new Report();
        KhezuHuntingController hunt;Player player;CharacterController capsule;
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        public static void Begin(){
            if(!Application.isPlaying||PhotonNetwork.InRoom)throw new InvalidOperationException("Run in offline Play Mode.");
            new GameObject("Khezu_Elevated_Reach_Checks").AddComponent<KhezuElevatedReachPlayChecks>();
        }
        void Record(string name,bool passed,string detail=""){report.checks.Add(new Check{name=name,passed=passed,detail=detail});}
        object Target(Transform target,CharacterController cc){
            var type=typeof(KhezuHuntingController).GetNestedType("Target",BindingFlags.NonPublic);var instance=Activator.CreateInstance(type);
            type.GetField("transform").SetValue(instance,target);type.GetField("capsule").SetValue(instance,cc);return instance;
        }
        bool Reach(Transform target,CharacterController cc){
            Physics.SyncTransforms();var t=Target(target,cc);
            return (bool)typeof(KhezuHuntingController).GetMethod("CanReachElevatedTarget",Private).Invoke(hunt,new[]{t});
        }
        void Place(Vector3 point){
            point.y-=(capsule.center.y-capsule.height*.5f)*player.transform.lossyScale.y;
            point.y+=capsule.skinWidth*Mathf.Abs(player.transform.lossyScale.y);
            capsule.enabled=false;player.transform.position=point;capsule.enabled=true;player.ResetMotionAfterTeleport();Physics.SyncTransforms();
        }
        float Feet(){return player.transform.TransformPoint(capsule.center).y-capsule.height*Mathf.Abs(player.transform.lossyScale.y)*.5f;}
        float Gap(){return (float)typeof(KhezuHuntingController).GetMethod("TargetSurfaceGap",Private).Invoke(hunt,new[]{Target(player.transform,capsule)});}
        bool ClearFixtureView(CharacterController cc){
            var center=cc.transform.TransformPoint(cc.center);
            foreach(var bone in hunt.GetComponentsInChildren<Transform>()){
                if(bone.name!="Head"&&bone.name!="Chest"&&bone.name!="UpperArm.L"&&bone.name!="UpperArm.R")continue;
                if(Physics.CheckSphere(bone.position,.08f,hunt.catchObstructionMask,QueryTriggerInteraction.Ignore))continue;
                RaycastHit obstruction;
                if(!Physics.Linecast(bone.position,center,out obstruction,hunt.catchObstructionMask,QueryTriggerInteraction.Ignore))return true;
            }return false;
        }
        void Fixtures(){
            var dummy=new GameObject("Temporary_Reach_Capsule");dummy.layer=7;
            var cc=dummy.AddComponent<CharacterController>();cc.height=5.5f;cc.radius=.7f;cc.center=Vector3.up*2.75f;
            var body=hunt.GetComponent<BoxCollider>();float scale=Mathf.Abs(hunt.transform.lossyScale.x);
            var front=body.center;front.z+=body.size.z*.5f;
            var basePoint=hunt.transform.TransformPoint(front)+hunt.transform.forward*(.7f+hunt.catchSurfaceDistance+hunt.elevatedReachDistance*scale*.5f);
            basePoint.y=hunt.transform.position.y+4;dummy.transform.position=basePoint;
            GameObject wall=null,platform=null,support=null;
            try{
                Record("Nearby raised capsule is within upper-body reach",Reach(dummy.transform,cc));
                wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Temporary_Solid_Reach_Wall";
                var mid=Vector3.Lerp(hunt.transform.position,basePoint,.78f);mid.y=hunt.transform.position.y+9;
                wall.transform.SetPositionAndRotation(mid,hunt.transform.rotation);wall.transform.localScale=new Vector3(40,30,1);
                Record("Solid wall blocks every upper-body capture line",!Reach(dummy.transform,cc));
                wall.SetActive(false);
                dummy.transform.position=basePoint+Vector3.up*30;
                Record("Player above monster height cannot be captured",!Reach(dummy.transform,cc));
                dummy.transform.position=basePoint+hunt.transform.forward*12;
                Record("Distant ledge cannot be captured",!Reach(dummy.transform,cc));
                var high=basePoint;high.y=body.bounds.max.y-.1f;dummy.transform.position=high;
                platform=GameObject.CreatePrimitive(PrimitiveType.Cube);platform.name="Temporary_Solid_Overhead_Platform";
                platform.transform.position=high-Vector3.up*.3f;platform.transform.localScale=new Vector3(60,.5f,60);
                Record("Solid overhead platform blocks reach from below",!Reach(dummy.transform,cc));platform.SetActive(false);
                dummy.transform.position=basePoint;
                Record("Removing obstructions restores elevated reach",Reach(dummy.transform,cc));
                // Find a clear side so a nearby real pillar cannot confound this isolated
                // supported-versus-airborne comparison when the suite follows another hunt.
                Vector3 diagonal=Vector3.zero;bool clearFixture=false;
                foreach(float angle in new[]{70f,-70f,60f,-60f,75f,-75f}){
                    diagonal=hunt.transform.position+Quaternion.AngleAxis(angle,Vector3.up)*hunt.transform.forward*(hunt.Agent.radius+.7f+1.6f);
                    diagonal.y=hunt.transform.position.y+4;dummy.transform.position=diagonal;Physics.SyncTransforms();RaycastHit existing;
                    if(Physics.Raycast(diagonal+Vector3.up*.25f,Vector3.down,out existing,.75f,hunt.catchObstructionMask,QueryTriggerInteraction.Ignore))continue;
                    if(ClearFixtureView(cc)&&!Reach(dummy.transform,cc)){clearFixture=true;break;}
                }
                Record("An unsupported jump does not gain the wider turning footprint",clearFixture&&!Reach(dummy.transform,cc),"point="+diagonal);
                support=GameObject.CreatePrimitive(PrimitiveType.Cube);support.name="Temporary_Raised_Reach_Support";
                support.transform.position=diagonal-Vector3.up*.22f;support.transform.localScale=new Vector3(2.5f,.4f,2.5f);
                Record("Standing on a low support permits the turning footprint reach",clearFixture&&Reach(dummy.transform,cc),"point="+diagonal);
                Record("Ground catch distance and pursuit balance are preserved",Mathf.Approximately(hunt.catchSurfaceDistance,.65f)&&Mathf.Approximately(hunt.Agent.radius,9.7f)&&Mathf.Approximately(hunt.closeChaseSpeedMultiplier,1.04f)&&Mathf.Approximately(hunt.chaseSpeedMultiplier,1.3685f));
            } finally {Destroy(dummy);if(wall!=null)Destroy(wall);if(platform!=null)Destroy(platform);if(support!=null)Destroy(support);}
        }
        IEnumerator Start(){
            yield return null;hunt=FindFirstObjectByType<KhezuHuntingController>();player=Player.LocalInstance;capsule=player.GetComponent<CharacterController>();
            var root=GameObject.Find("OpenArchive").transform;
            var session=FindFirstObjectByType<OpenArchiveExplorationSession>();var cinema=hunt.GetComponent<KhezuCinematicJumpscareController>();var encounter=hunt.GetComponent<KhezuCaptureEncounterController>();var sinking=FindFirstObjectByType<OpenArchiveWaterSinking>();
            bool oldSession=session.enabled,oldCinema=cinema.enabled,oldEncounter=encounter.enabled,oldSinking=sinking.enabled,oldBlock=GameInput.Instance.GameplayInputBlocked;
            var oldPosition=player.transform.position;var oldRotation=player.transform.rotation;int hp=hunt.health.CurrentHealth;
            report.checkedUtc=DateTime.UtcNow.ToString("o");
            try{
                session.enabled=false;cinema.enabled=false;encounter.enabled=false;sinking.enabled=false;
                GameInput.Instance.SetGameplayInputBlocked(false);GameInput.Instance.SetMobileMovement(Vector2.zero);
                hunt.Request(KhezuCommand.Sleep);Place(root.TransformPoint(new Vector3(0,.3f,57)));hunt.Request(KhezuCommand.StartHunting);
                float deadline=Time.time+25;while(hunt.State!=KhezuHuntState.Hunting&&Time.time<deadline)yield return null;
                Record("Existing awakening sequence starts a real hunt",hunt.State==KhezuHuntState.Hunting);
                hunt.Request(KhezuCommand.ResetHunt);encounter.SynchronizeBarrierMovement();Fixtures();yield return null;
                for(int q=0;q<6;q++){
                    hunt.Request(KhezuCommand.ResetHunt);
                    encounter.SynchronizeBarrierMovement();
                    var point=Quaternion.Euler(0,q*90,0)*new Vector3(13.875f,0,-33);point.y=4.725714f;
                    if(q>=4){
                        point=root.TransformPoint(Quaternion.Euler(0,(q-4)*180,0)*new Vector3(12,0,11));
                        RaycastHit floor;if(!Physics.Raycast(point+Vector3.up*35,Vector3.down,out floor,60,~(1<<7),QueryTriggerInteraction.Ignore))throw new InvalidOperationException("Missing dry pocket ground.");
                        point=floor.point;
                    }
                    Place(point);yield return new WaitForSeconds(.3f);
                    var startPlayer=player.transform.position;float startFeet=Feet();int serial=hunt.CaptureSerial;
                    hunt.Request(KhezuCommand.StartHunting);deadline=Time.time+14;
                    bool teleport=false;var previous=hunt.transform.position;int blocked=0;
                    while(hunt.State!=KhezuHuntState.Caught&&Time.time<deadline){
                        teleport|=Vector3.Distance(previous,hunt.transform.position)>hunt.normalPlayerRunSpeed*hunt.chaseSpeedMultiplier*Mathf.Max(.001f,Time.deltaTime)+2;
                        if(hunt.State==KhezuHuntState.NavigationBlocked)blocked++;
                        previous=hunt.transform.position;yield return null;
                    }
                    float feet=Feet();string label=q<4?"Nest rock footing "+(q+1):"Ground pocket "+(q-3);
                    Record(label+": real close capture",hunt.State==KhezuHuntState.Caught&&hunt.CaptureSerial==serial+1&&Mathf.Abs(feet-startFeet)<.4f&&(q>=4||startFeet>3)&&Vector3.Distance(startPlayer,point+Vector3.up*(startPlayer.y-point.y))<.15f,"feet="+feet+", initialFeet="+startFeet+", surfaceGap="+Gap()+", player="+player.transform.position+", monster="+hunt.transform.position);
                    Record(label+": continuous reachable-side pursuit",!teleport&&blocked==0,"blockedFrames="+blocked);
                }
                hunt.Request(KhezuCommand.ResetHunt);encounter.SynchronizeBarrierMovement();
                Record("Tests do not change hearts or player control components",hunt.health.CurrentHealth==hp&&player.enabled&&capsule.enabled&&!player.IsMovementLocked);
            }
            finally{
                hunt.Request(KhezuCommand.Sleep);encounter.SynchronizeBarrierMovement();capsule.enabled=false;player.transform.SetPositionAndRotation(oldPosition,oldRotation);capsule.enabled=true;player.ResetMotionAfterTeleport();
                session.enabled=oldSession;cinema.enabled=oldCinema;encounter.enabled=oldEncounter;sinking.enabled=oldSinking;GameInput.Instance.SetGameplayInputBlocked(oldBlock);
                report.passed=report.checks.Count>0&&report.checks.TrueForAll(x=>x.passed);
                File.WriteAllText("Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Testing/KhezuHunting/Reports/ElevatedReachPlayMode.json",JsonUtility.ToJson(report,true));
                Debug.Log("Khezu elevated reach: "+report.checks.FindAll(x=>x.passed).Count+"/"+report.checks.Count);Destroy(gameObject);
            }
        }
    }
}
#endif
