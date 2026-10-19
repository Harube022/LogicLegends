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
    /// <summary>Explicit offline test of the former nest hiding pockets. Never attached to a saved scene.</summary>
    public sealed class KhezuLandApproachPlayChecks : MonoBehaviour
    {
        const string Reports="Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Testing/KhezuHunting/Reports";
        [Serializable] sealed class Check {public string name,detail;public bool passed;}
        [Serializable] sealed class Report {
            public string checkedUtc,scope="Real offline agent and original enabled Player. Only the test player is repositioned. Four former nest pockets must produce normal close-range captures, including obstruction checks. Two distant off-mesh land targets verify reachable-side routing. No agent teleport, catch-range increase, water-timing or player-system change. Live multiplayer unverified.";
            public bool passed;public List<Check> checks=new List<Check>();
        }
        readonly Report report=new Report();
        Player player;CharacterController capsule;Transform root;KhezuHuntingController hunt;
        public static void Begin(){
            if(!Application.isPlaying || PhotonNetwork.InRoom)throw new InvalidOperationException("Run offline Play Mode.");
            new GameObject("Khezu_Land_Approach_Play_Checks").AddComponent<KhezuLandApproachPlayChecks>();
        }
        void Record(string name,bool passed,string detail=""){report.checks.Add(new Check{name=name,passed=passed,detail=detail});}
        void Place(Vector3 point){
            RaycastHit floor;
            if(!Physics.Raycast(point+Vector3.up*30,Vector3.down,out floor,60,~(1<<7),QueryTriggerInteraction.Ignore))throw new InvalidOperationException("Missing ground.");
            point.y=floor.point.y-(capsule.center.y-capsule.height*.5f)*player.transform.lossyScale.y+capsule.skinWidth;
            capsule.enabled=false;player.transform.position=point;capsule.enabled=true;player.ResetMotionAfterTeleport();
        }
        static float Flat(Vector3 p)=>new Vector2(p.x,p.z).magnitude;
        IEnumerator Start(){
            yield return new WaitForSecondsRealtime(.6f);
            player=Player.LocalInstance;capsule=player.GetComponent<CharacterController>();hunt=FindFirstObjectByType<KhezuHuntingController>();root=GameObject.Find("OpenArchive").transform;
            var session=FindFirstObjectByType<OpenArchiveExplorationSession>();var cinema=hunt.GetComponent<KhezuCinematicJumpscareController>();var encounter=hunt.GetComponent<KhezuCaptureEncounterController>();
            var safety=FindFirstObjectByType<OpenArchiveWaterSinking>();var water=FindFirstObjectByType<OpenArchiveWater>();
            bool oldSession=session.enabled,oldCinema=cinema.enabled,oldEncounter=encounter.enabled,oldSafety=safety.enabled,oldBlock=GameInput.Instance.GameplayInputBlocked;
            var oldPosition=player.transform.position;var oldRotation=player.transform.rotation;report.checkedUtc=DateTime.UtcNow.ToString("o");
            try{
                session.enabled=false;cinema.enabled=false;encounter.enabled=false;safety.enabled=false;GameInput.Instance.SetGameplayInputBlocked(false);GameInput.Instance.SetMobileMovement(Vector2.zero);
                hunt.Request(KhezuCommand.Sleep);Place(root.TransformPoint(new Vector3(0,0,57)));hunt.Request(KhezuCommand.ForceFourMistakes);
                float deadline=Time.time+25;while(hunt.State!=KhezuHuntState.Hunting && Time.time<deadline)yield return null;
                Record("Awakens normally",hunt.State==KhezuHuntState.Hunting);
                for(int q=0;q<4;q++){
                    var point=root.TransformPoint(Quaternion.Euler(0,q*90,0)*new Vector3(12,0,11));Place(point);
                    if(hunt.State==KhezuHuntState.Caught)hunt.Request(KhezuCommand.StartHunting);
                    int capture=hunt.CaptureSerial;deadline=Time.time+13;int blocked=0;bool discontinuity=false;var previous=hunt.transform.position;
                    while(hunt.State!=KhezuHuntState.Caught && Time.time<deadline){
                        if(hunt.State==KhezuHuntState.NavigationBlocked)blocked++;
                        discontinuity|=Vector3.Distance(previous,hunt.transform.position)>hunt.normalPlayerRunSpeed*hunt.chaseSpeedMultiplier*Mathf.Max(Time.deltaTime,.001f)+2;
                        previous=hunt.transform.position;yield return null;
                    }
                    string name="Former nest pocket "+(q+1);
                    Record(name+": original close-range capture",hunt.State==KhezuHuntState.Caught && hunt.CaptureSerial==capture+1,"monster="+hunt.transform.position+", player="+player.transform.position);
                    Record(name+": no navigation freeze or monster teleport",blocked==0 && !discontinuity,"blockedFrames="+blocked);
                    Record(name+": clear line of sight at capture",hunt.State==KhezuHuntState.Caught && KhezuLandApproach.ClearLine(hunt.transform.position,player.transform,OpenArchiveWater.Feet(capsule).y,hunt.catchObstructionMask));
                }
                for(int i=0;i<2;i++){
                    // These are other valid dry positions outside the original short nearest-point radius.
                    Place(i==0?new Vector3(-185,0,-50):new Vector3(-100,0,-35));
                    if(hunt.State==KhezuHuntState.Caught)hunt.Request(KhezuCommand.StartHunting);
                    yield return new WaitForSeconds(.35f);var start=hunt.transform.position;int blocked=0;float end=Time.time+3;
                    while(Time.time<end){if(hunt.State==KhezuHuntState.NavigationBlocked)blocked++;yield return null;}
                    Record("Distant dry obstacle "+(i+1)+": reachable-side pursuit",!water.IsInWater(capsule) && hunt.State==KhezuHuntState.Hunting && blocked==0 && hunt.Agent.hasPath && hunt.Agent.pathStatus==NavMeshPathStatus.PathComplete && Flat(hunt.transform.position-start)>15);
                }
                Record("Original size, movement balance and catch reach retained",Mathf.Approximately(hunt.Agent.radius,9.7f) && Mathf.Approximately(hunt.transform.lossyScale.x,2.875f) && Mathf.Approximately(hunt.catchSurfaceDistance,.65f) && Mathf.Approximately(hunt.closeChaseSpeedMultiplier,1.04f));
            }
            finally{
                hunt.Request(KhezuCommand.Sleep);capsule.enabled=false;player.transform.SetPositionAndRotation(oldPosition,oldRotation);capsule.enabled=true;player.ResetMotionAfterTeleport();
                session.enabled=oldSession;cinema.enabled=oldCinema;encounter.enabled=oldEncounter;safety.enabled=oldSafety;GameInput.Instance.SetGameplayInputBlocked(oldBlock);
                report.passed=report.checks.Count>0 && report.checks.TrueForAll(c=>c.passed);Directory.CreateDirectory(Reports);File.WriteAllText(Reports+"/LandApproachPlayMode.json",JsonUtility.ToJson(report,true));
                Debug.Log("Khezu land approach: "+report.checks.FindAll(c=>c.passed).Count+"/"+report.checks.Count);Destroy(gameObject);
            }
        }
    }
}
#endif
