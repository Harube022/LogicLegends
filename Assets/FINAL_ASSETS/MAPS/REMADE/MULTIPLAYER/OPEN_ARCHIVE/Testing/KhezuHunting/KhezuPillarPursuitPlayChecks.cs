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
    /// <summary>Offline sweep of real standable nest surfaces, including both sides of the pillars.</summary>
    public sealed class KhezuPillarPursuitPlayChecks : MonoBehaviour
    {
        const string Folder="Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Testing/KhezuHunting";
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        [Serializable] sealed class Spot {public Vector3 position;public string surface;}
        [Serializable] sealed class Spots {public Spot[] points;}
        [Serializable] sealed class Check {
            public string surface,state,reason,upperSight;public Vector3 requested,player,monster;public float feet,surfaceGap,elapsed;
            public bool captured,stable,continuous;
        }
        [Serializable] sealed class Report {
            public string checkedUtc,scope="Offline: enabled original player on measured standable rock/ruin/torch surfaces. The actual monster navigates continuously; only the player is repositioned. No monster warp or collider bypass.";
            public bool passed,controlsRestored,heartsPreserved;public List<Check> checks=new List<Check>();
        }
        readonly Report report=new Report();
        string reportPath;KhezuHuntingController hunt;Player player;CharacterController capsule;
        public static void Begin(bool baseline=false){
            if(!Application.isPlaying||PhotonNetwork.InRoom)throw new InvalidOperationException("Use offline Play Mode.");
            if(FindFirstObjectByType<KhezuPillarPursuitPlayChecks>()!=null)throw new InvalidOperationException("Sweep already running.");
            var test=new GameObject("Khezu_Pillar_Pursuit_Checks").AddComponent<KhezuPillarPursuitPlayChecks>();
            test.reportPath=baseline?"Temp/KhezuPillarReachFollowup/Baseline.json":Folder+"/Reports/PillarPursuitPlayMode.json";
        }
        object Target(){
            var type=typeof(KhezuHuntingController).GetNestedType("Target",BindingFlags.NonPublic);var target=Activator.CreateInstance(type);
            type.GetField("transform").SetValue(target,player.transform);type.GetField("capsule").SetValue(target,capsule);return target;
        }
        float Feet(){return player.transform.TransformPoint(capsule.center).y-capsule.height*Mathf.Abs(player.transform.lossyScale.y)*.5f;}
        float Gap(){return (float)typeof(KhezuHuntingController).GetMethod("TargetSurfaceGap",Private).Invoke(hunt,new[]{Target()});}
        string Sight(){
            var center=player.transform.TransformPoint(capsule.center);string result="";
            foreach(var bone in hunt.GetComponentsInChildren<Transform>()){
                if(bone.name!="Head"&&bone.name!="Chest"&&bone.name!="UpperArm.L"&&bone.name!="UpperArm.R")continue;
                RaycastHit obstruction;bool hit=Physics.Linecast(bone.position,center,out obstruction,hunt.catchObstructionMask,QueryTriggerInteraction.Ignore);
                result+=bone.name+":"+(hit?obstruction.collider.name:"clear")+"; ";
            }return result;
        }
        void Place(Vector3 point){
            point.y-=(capsule.center.y-capsule.height*.5f)*player.transform.lossyScale.y;point.y+=capsule.skinWidth*Mathf.Abs(player.transform.lossyScale.y);
            capsule.enabled=false;player.transform.position=point;capsule.enabled=true;player.ResetMotionAfterTeleport();Physics.SyncTransforms();
        }
        void Save(){File.WriteAllText(reportPath,JsonUtility.ToJson(report,true));}
        IEnumerator Start(){
            yield return null;hunt=FindFirstObjectByType<KhezuHuntingController>();player=Player.LocalInstance;capsule=player.GetComponent<CharacterController>();
            var session=FindFirstObjectByType<OpenArchiveExplorationSession>();var cinema=hunt.GetComponent<KhezuCinematicJumpscareController>();var encounter=hunt.GetComponent<KhezuCaptureEncounterController>();var sinking=FindFirstObjectByType<OpenArchiveWaterSinking>();
            bool sessionEnabled=session.enabled,cinemaEnabled=cinema.enabled,encounterEnabled=encounter.enabled,sinkingEnabled=sinking.enabled,oldInput=GameInput.Instance.GameplayInputBlocked;
            var oldPosition=player.transform.position;var oldRotation=player.transform.rotation;int hearts=hunt.health.CurrentHealth;
            report.checkedUtc=DateTime.UtcNow.ToString("o");
            try{
                session.enabled=false;cinema.enabled=false;encounter.enabled=false;sinking.enabled=false;
                GameInput.Instance.SetGameplayInputBlocked(false);GameInput.Instance.SetMobileMovement(Vector2.zero);
                hunt.Request(KhezuCommand.Sleep);Place(new Vector3(0,1,140));hunt.Request(KhezuCommand.StartHunting);
                float deadline=Time.time+25;while(hunt.State!=KhezuHuntState.Hunting&&Time.time<deadline)yield return null;
                var spots=JsonUtility.FromJson<Spots>(File.ReadAllText(Folder+"/Fixtures/KhezuNestStandingSpots.json")).points;
                foreach(var spot in spots){
                    hunt.Request(KhezuCommand.ResetHunt);encounter.SynchronizeBarrierMovement();Place(spot.position);yield return new WaitForSeconds(.45f);
                    var initialPlayer=player.transform.position;float initialFeet=Feet();var previous=hunt.transform.position;var progress=previous;float started=Time.time,lastProgress=Time.time;
                    bool continuous=true;int serial=hunt.CaptureSerial;hunt.Request(KhezuCommand.StartHunting);deadline=Time.time+13;
                    while(hunt.State!=KhezuHuntState.Caught&&Time.time<deadline){
                        var current=hunt.transform.position;
                        continuous&=Vector3.Distance(previous,current)<hunt.normalPlayerRunSpeed*hunt.chaseSpeedMultiplier*Mathf.Max(.001f,Time.deltaTime)+2;
                        if(Vector3.Distance(current,progress)>.8f){progress=current;lastProgress=Time.time;}
                        if(Time.time-lastProgress>4)break;
                        previous=current;yield return null;
                    }
                    report.checks.Add(new Check{surface=spot.surface,requested=spot.position,player=player.transform.position,monster=hunt.transform.position,
                        feet=Feet(),surfaceGap=Gap(),elapsed=Time.time-started,state=hunt.State.ToString(),reason=hunt.Status,upperSight=Sight(),
                        captured=hunt.State==KhezuHuntState.Caught&&hunt.CaptureSerial==serial+1,
                        stable=Vector3.Distance(initialPlayer,player.transform.position)<.6f&&Mathf.Abs(Feet()-initialFeet)<.4f,continuous=continuous});Save();
                }
            }
            finally{
                hunt.Request(KhezuCommand.Sleep);encounter.SynchronizeBarrierMovement();capsule.enabled=false;player.transform.SetPositionAndRotation(oldPosition,oldRotation);capsule.enabled=true;player.ResetMotionAfterTeleport();
                session.enabled=sessionEnabled;cinema.enabled=cinemaEnabled;encounter.enabled=encounterEnabled;sinking.enabled=sinkingEnabled;GameInput.Instance.SetGameplayInputBlocked(oldInput);
                report.controlsRestored=player.enabled&&capsule.enabled&&!player.IsMovementLocked;report.heartsPreserved=hunt.health.CurrentHealth==hearts;
                report.passed=report.checks.Count>0&&report.checks.TrueForAll(x=>x.captured&&x.stable&&x.continuous)&&report.controlsRestored&&report.heartsPreserved;
                Save();Debug.Log("Khezu pillar sweep: "+report.checks.FindAll(x=>x.captured).Count+"/"+report.checks.Count+" captured");Destroy(gameObject);
            }
        }
    }
}
#endif
