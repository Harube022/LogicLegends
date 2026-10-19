#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using LogicLegends.OpenArchive.Cinematics;
using LogicLegends.OpenArchive.Hunting;
using LogicLegends.OpenArchive.Water;
using LogicLegends.OpenArchiveTesting;
using Photon.Pun;
using UnityEngine;
using UnityEngine.AI;

namespace LogicLegends.Tests.Khezu
{
    /// <summary>Explicit offline Editor check. Only the test player is repositioned; the monster always follows real navigation.</summary>
    public sealed class KhezuWaterPursuitPlayChecks : MonoBehaviour
    {
        const string Reports="Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Testing/KhezuHunting/Reports";
        [Serializable] sealed class Check {public string name, detail; public bool passed;}
        [Serializable] sealed class Sample {public string leg, state;public float seconds,speed,x,z,remaining;public bool shoreline,path;}
        [Serializable] sealed class Report {
            public string checkedUtc,scope="Offline real NavMeshAgent and original Player/GameInput in the cinematic test scene. Water sinking is temporarily disabled for extended stationary-water probes, then restored. No monster teleport, navigation bake or player-system change. Live multiplayer not verified.";
            public bool passed;public List<Check> checks=new List<Check>();public List<Sample> samples=new List<Sample>();
        }
        readonly Report report=new Report();
        Player player;CharacterController capsule;Transform root;OpenArchiveWater water;KhezuHuntingController hunt;
        int serial,captures;
        public static void Begin(){
            if(!Application.isPlaying || PhotonNetwork.InRoom)throw new InvalidOperationException("Use offline Play Mode.");
            new GameObject("Khezu_Water_Pursuit_Play_Checks").AddComponent<KhezuWaterPursuitPlayChecks>();
        }
        void Record(string name,bool pass,string detail=""){report.checks.Add(new Check{name=name,passed=pass,detail=detail});}
        static Vector3 Polar(float a,float r){a*=Mathf.Deg2Rad;return new Vector3(Mathf.Sin(a)*r,0,Mathf.Cos(a)*r);}
        static float Flat(Vector3 p){return new Vector2(p.x,p.z).magnitude;}
        void Input(Vector3 direction){
            direction.y=0;var f=Camera.main.transform.forward;f.y=0;f.Normalize();var r=Camera.main.transform.right;r.y=0;r.Normalize();
            GameInput.Instance.SetMobileMovement(new Vector2(Vector3.Dot(direction.normalized,r),Vector3.Dot(direction.normalized,f)));
        }
        void Place(Vector3 point,bool bed){
            Input(Vector3.zero);float surface;var origin=point+Vector3.up*30;
            if(bed && water.SampleSurface(point,out surface))origin.y=surface+.1f;
            RaycastHit floor;if(!Physics.Raycast(origin,Vector3.down,out floor,70,~(1<<7),QueryTriggerInteraction.Ignore))throw new InvalidOperationException("Missing player support.");
            point.y=floor.point.y-(capsule.center.y-capsule.height*.5f)*player.transform.lossyScale.y+capsule.skinWidth;
            capsule.enabled=false;player.transform.position=point;capsule.enabled=true;player.ResetMotionAfterTeleport();
        }
        float Bank(float angle){float lo=23,hi=35.5f,y;for(int i=0;i<24;i++){float m=(lo+hi)*.5f;if(water.SampleSurface(root.TransformPoint(Polar(angle,m)),out y))hi=m;else lo=m;}return hi;}
        IEnumerator Probe(string name,float seconds,bool expectWater){
            float started=Time.time,next=0,travel=0,slow=0,longest=0;int wet=0,blocked=0,missing=0,destinations=0;var previous=hunt.transform.position;var lastDestination=Vector3.positiveInfinity;
            while(Time.time-started<seconds){
                float elapsed=Time.time-started;var agent=hunt.Agent;var now=hunt.transform.position;
                travel+=Flat(now-previous);previous=now;
                bool path=agent.enabled && agent.isOnNavMesh && agent.hasPath && agent.pathStatus==NavMeshPathStatus.PathComplete && !agent.isStopped;
                if(elapsed>.4f){
                    if(hunt.FollowingWaterTarget)wet++;
                    if(hunt.State!=KhezuHuntState.Hunting)blocked++;
                    if(!path)missing++;
                    float speed=agent.enabled && agent.isOnNavMesh?agent.velocity.magnitude:0;
                    slow=speed<.5f?slow+Time.deltaTime:0;longest=Mathf.Max(longest,slow);
                }
                if(elapsed>=next){
                    next=elapsed+.2f;
                    if(path && Flat(agent.destination-lastDestination)>3){destinations++;lastDestination=agent.destination;}
                    report.samples.Add(new Sample{leg=name,seconds=elapsed,state=hunt.State.ToString(),speed=agent.enabled && agent.isOnNavMesh?agent.velocity.magnitude:0,x=now.x,z=now.z,
                        shoreline=hunt.FollowingWaterTarget,path=path,remaining=path?agent.remainingDistance:0});
                }
                yield return null;
            }
            Record(name+": complete pursuit with no blocked state",blocked==0 && missing==0,"blockedFrames="+blocked+", missingPathFrames="+missing);
            Record(name+": keeps moving",travel>seconds*7 && longest<1,"travel="+travel+", longestStop="+longest+", destinations="+destinations);
            if(expectWater)Record(name+": uses shoreline pursuit",wet>0 && water.IsInWater(capsule));
            Record(name+": target and hunt preserved without false capture",hunt.HuntSerial==serial && hunt.TargetPlayerId==-1 && hunt.CaptureSerial==captures);
        }
        IEnumerator Start(){
            yield return new WaitForSecondsRealtime(.6f);
            player=Player.LocalInstance;capsule=player.GetComponent<CharacterController>();root=GameObject.Find("OpenArchive").transform;
            water=FindFirstObjectByType<OpenArchiveWater>();hunt=FindFirstObjectByType<KhezuHuntingController>();
            var safety=water.GetComponent<OpenArchiveWaterSinking>();var session=FindFirstObjectByType<OpenArchiveExplorationSession>();
            var cinema=hunt.GetComponent<KhezuCinematicJumpscareController>();var encounter=hunt.GetComponent<KhezuCaptureEncounterController>();
            bool oldSafety=safety.enabled,oldSession=session.enabled,oldCinema=cinema.enabled,oldEncounter=encounter.enabled,oldBlocked=GameInput.Instance.GameplayInputBlocked;
            var oldPosition=player.transform.position;var oldRotation=player.transform.rotation;report.checkedUtc=DateTime.UtcNow.ToString("o");
            try{
                session.enabled=false;safety.enabled=false;cinema.enabled=false;encounter.enabled=false;GameInput.Instance.SetGameplayInputBlocked(false);
                hunt.Request(KhezuCommand.Sleep);Place(root.TransformPoint(new Vector3(0,0,57)),false);
                hunt.Request(KhezuCommand.ForceFourMistakes);float deadline=Time.time+25;
                while(hunt.State!=KhezuHuntState.Hunting && Time.time<deadline)yield return null;
                serial=hunt.HuntSerial;captures=hunt.CaptureSerial;Record("Awakens into an existing land chase",hunt.State==KhezuHuntState.Hunting);
                for(int i=0;i<4;i++){
                    Place(root.TransformPoint(Polar(30+i*90,35.5f)),true);yield return new WaitForSeconds(.3f);
                    yield return Probe("Pond "+(i+1),10,true);
                }
                // Extended water stay verifies that reaching the bank does not end movement.
                yield return Probe("Stationary water target",9,true);
                var movementStart=player.transform.position;var destinationBefore=hunt.Agent.destination;
                var movingGoal=root.TransformPoint(Polar(308,35.5f));deadline=Time.time+2;
                bool wetThroughout=true;
                while(Time.time<deadline && Flat(movingGoal-player.transform.position)>.5f){
                    Input(movingGoal-player.transform.position);wetThroughout&=water.IsInWater(capsule);yield return null;
                }
                Input(Vector3.zero);yield return new WaitForSeconds(.3f);
                Record("Follows the moving original player within water",Flat(player.transform.position-movementStart)>8 && wetThroughout && hunt.FollowingWaterTarget && Flat(hunt.Agent.destination-destinationBefore)>3);
                yield return Probe("Moved water target",4,true);
                var bridge=root.Find("Environment/Bridges").GetChild(3);Place(bridge.position,true);yield return new WaitForSeconds(.3f);
                yield return Probe("Under bridge",7,true);
                float bank=Bank(30);Place(root.TransformPoint(Polar(30,bank+1.2f/root.lossyScale.x)),true);yield return new WaitForSeconds(.3f);
                var dry=root.TransformPoint(Polar(30,bank-4));GameInput.Instance.MobileJump();deadline=Time.time+3;
                while(Time.time<deadline && Flat(dry-player.transform.position)>.4f){Input(dry-player.transform.position);yield return null;}
                Input(Vector3.zero);yield return new WaitForSeconds(.3f);
                Record("Original jump escapes the water to dry ground",!water.IsInWater(capsule) && capsule.isGrounded && OpenArchiveWater.Feet(capsule).y>-.3f);
                Record("Land pursuit resumes immediately",!hunt.FollowingWaterTarget && hunt.State==KhezuHuntState.Hunting && hunt.Agent.enabled && hunt.Agent.hasPath);
                deadline=Time.time+15;while(hunt.State!=KhezuHuntState.Caught && Time.time<deadline)yield return null;
                Record("Normal land catch still works after leaving water",hunt.State==KhezuHuntState.Caught && hunt.CaptureSerial==captures+1 && hunt.HuntSerial==serial);
                // Exercise the saved anti-camping timer with this new pursuit, without changing its timings.
                hunt.Request(KhezuCommand.StartHunting);serial=hunt.HuntSerial;captures=hunt.CaptureSerial;
                safety.enabled=true;Place(root.TransformPoint(Polar(120,35.5f)),true);int returns=safety.ForcedReturnCount;
                deadline=Time.time+11;bool continued=true;
                while(safety.ForcedReturnCount==returns && Time.time<deadline){
                    if(safety.LocalWaterSeconds>1)continued&=hunt.State==KhezuHuntState.Hunting && hunt.FollowingWaterTarget;
                    yield return null;
                }
                Record("Configured 5/4/5 sinking still returns a camping player during pursuit",continued && safety.ForcedReturnCount==returns+1 && safety.safeWaterSeconds==5 && safety.sinkingSeconds==4 && safety.dryRecoverySeconds==5);
                yield return new WaitForSeconds(.4f);
                Record("Hunt follows the returned player without a fabricated catch",hunt.HuntSerial==serial && hunt.TargetPlayerId==-1 && hunt.CaptureSerial==captures && hunt.State==KhezuHuntState.Hunting && !hunt.FollowingWaterTarget && hunt.Agent.hasPath);
            }
            finally{
                Input(Vector3.zero);hunt.Request(KhezuCommand.Sleep);capsule.enabled=false;player.transform.SetPositionAndRotation(oldPosition,oldRotation);capsule.enabled=true;player.ResetMotionAfterTeleport();
                safety.enabled=oldSafety;session.enabled=oldSession;cinema.enabled=oldCinema;encounter.enabled=oldEncounter;GameInput.Instance.SetGameplayInputBlocked(oldBlocked);
                report.passed=report.checks.Count>0 && report.checks.TrueForAll(c=>c.passed);Directory.CreateDirectory(Reports);File.WriteAllText(Reports+"/WaterPursuitPlayMode.json",JsonUtility.ToJson(report,true));
                Debug.Log("Khezu water pursuit: "+report.checks.FindAll(c=>c.passed).Count+"/"+report.checks.Count);Destroy(gameObject);
            }
        }
    }
}
#endif
