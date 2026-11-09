#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LogicLegends.OpenArchive.Cinematics;
using LogicLegends.OpenArchive.Hunting;
using LogicLegends.OpenArchive.Topics;
using LogicLegends.OpenArchive.Water;
using LogicLegends.OpenArchiveTesting;
using LogicLegends.Tests.Monsters;
using Photon.Pun;
using UnityEngine;
using UnityEngine.AI;

namespace LogicLegends.Tests.Khezu
{
    /// <summary>Native, opt-in sprint pursuit reproduction. Drives the real capsule, never teleports a running runner.</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class MonsterOrbitPursuitPlayChecks : MonoBehaviour
    {
        [Serializable] public sealed class Result
        {
            public string monster,pattern;public bool caught,valid,warningSeen,coverWarningVisible;
            public float seconds,travelled,blockedSeconds,runnerBlockedSeconds,averageMonsterSpeed,stoppedSeconds,maximumRunnerStep;
            public int counters;
        }
        [Serializable] public sealed class Report
        {
            public string label,scope="Offline native Unity Play; original rig, capsule, NavMesh, movement up to the actual sprint limit and existing capture. Not an Android or remote-client test.";
            public bool finished;public List<Result> results=new List<Result>();public string error;
        }
        public static string Status {get;private set;}="Idle";
        readonly Report report=new Report();
        ArchiveTopicSession session;KhezuHuntingController hunt;Player player;CharacterController capsule;
        Vector3 fixedCenter;float radius,angle,travel,blocked,runnerBlocked,speedTotal,stopped,maxStep,orbitSign=1;int frames;bool driving,coverOrbit;
        string label;
        public static void Begin(string label)
        {
            if(!Application.isPlaying||PhotonNetwork.InRoom||FindFirstObjectByType<MonsterOrbitPursuitPlayChecks>()!=null)throw new InvalidOperationException("Use direct offline Play Mode.");
            var test=new GameObject("Monster_Orbit_Pursuit_QA").AddComponent<MonsterOrbitPursuitPlayChecks>();test.label=label;
        }
        void Save()
        {Directory.CreateDirectory("Temp/MonsterAntiKite");File.WriteAllText("Temp/MonsterAntiKite/"+label+".json",JsonUtility.ToJson(report,true));}
        void Update()
        {
            if(!driving||hunt.State==KhezuHuntState.Caught)return;
            float dt=Time.deltaTime;if(dt<=0)return;
            Vector3 center=fixedCenter;
            Vector3 before=player.transform.position;Vector3 radial=before-center;radial.y=0;
            Vector3 direction;
            if(coverOrbit)
            {
                var away=fixedCenter-hunt.GetComponent<BoxCollider>().bounds.center;away.y=0;away.Normalize();
                float delta=Vector3.SignedAngle(radial,away,Vector3.up)*Mathf.Deg2Rad;
                if(Mathf.Abs(delta)*radius<.08f)direction=Vector3.zero;
                else direction=(Vector3.Cross(Vector3.up,radial.normalized)*Mathf.Sign(delta)+radial.normalized*Mathf.Clamp((radius-radial.magnitude),-.7f,.7f)).normalized;
            }
            else
            {
                angle+=orbitSign*hunt.normalPlayerRunSpeed/radius*dt;
                Vector3 goal=fixedCenter+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius;
                direction=goal-before;direction.y=0;direction=direction.normalized;
            }
            if(direction.sqrMagnitude>.001f)player.transform.rotation=Quaternion.LookRotation(direction);
            float pace=coverOrbit?Mathf.Min(hunt.normalPlayerRunSpeed,Mathf.Abs(Vector3.SignedAngle(radial,fixedCenter-hunt.transform.position,Vector3.up))*Mathf.Deg2Rad*radius/Mathf.Max(.001f,dt)):hunt.normalPlayerRunSpeed;
            capsule.Move((direction*pace+Vector3.down*20)*dt);
            var horizontal=player.transform.position-before;horizontal.y=0;travel+=horizontal.magnitude;maxStep=Mathf.Max(maxStep,horizontal.magnitude/Mathf.Max(.001f,dt));
            if(!coverOrbit&&horizontal.magnitude<pace*dt*.85f)runnerBlocked+=dt;
            if(hunt.State==KhezuHuntState.NavigationBlocked)blocked+=dt;
            if(hunt.Agent.enabled&&hunt.Agent.isOnNavMesh){speedTotal+=hunt.Agent.velocity.magnitude;frames++;if(hunt.Agent.velocity.magnitude<1)stopped+=dt;}
        }
        void Place(Vector3 point)
        {capsule.enabled=false;player.transform.position=point;capsule.enabled=true;player.ResetMotionAfterTeleport();Physics.SyncTransforms();}
        Vector3 FindClearing()
        {
            var filter=new NavMeshQueryFilter{agentTypeID=hunt.Agent.agentTypeID,areaMask=hunt.Agent.areaMask};
            var candidates=new List<Vector3>();candidates.AddRange(session.stations.Select(s=>s.transform.position));
            var triangles=NavMesh.CalculateTriangulation();
            for(int i=0;i+2<triangles.indices.Length;i+=3)
            {
                var a=triangles.vertices[triangles.indices[i]];var b=triangles.vertices[triangles.indices[i+1]];var c=triangles.vertices[triangles.indices[i+2]];
                if(Vector3.Cross(b-a,c-a).magnitude>40)candidates.Add((a+b+c)/3);
            }
            foreach(var candidate in candidates.OrderBy(p=>(p-session.stations[0].transform.position).sqrMagnitude).Take(300))
            {
                if(!NavMesh.SamplePosition(candidate,out var center,2,filter))continue;
                bool clear=true;
                for(int i=0;i<24;i++)
                {
                    float a=i*Mathf.PI/12;var point=center.position+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*(radius+1);
                    if(!NavMesh.SamplePosition(point,out var hit,.25f,filter)||Mathf.Abs(hit.position.y-center.position.y)>.25f||NavMesh.Raycast(center.position,hit.position,out _,filter))
                    {clear=false;break;}
                }
                if(clear)return center.position;
            }
            throw new InvalidOperationException("Could not find an existing dry, unobstructed circle for this agent.");
        }
        Collider FindCover()
        {
            foreach(var tree in FindObjectsByType<BoxCollider>(FindObjectsSortMode.None).Where(c=>c.enabled&&!c.isTrigger&&c.name.StartsWith("Tree_")&&Mathf.Max(c.bounds.size.x,c.bounds.size.z)<3).OrderBy(c=>(c.transform.position-session.stations[0].transform.position).sqrMagnitude))
            {
                float r=new Vector2(tree.bounds.extents.x,tree.bounds.extents.z).magnitude+capsule.radius+.6f;bool clear=true;
                for(int i=0;i<24;i++)
                {
                    float a=i*Mathf.PI/12;var point=new Vector3(tree.bounds.center.x,0,tree.bounds.center.z)+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*r;
                    if(Physics.OverlapCapsule(point+Vector3.up*(capsule.radius+.25f),point+Vector3.up*(capsule.height-capsule.radius+.25f),capsule.radius*.98f,~0,QueryTriggerInteraction.Ignore).Any(c=>!c.transform.IsChildOf(player.transform)))
                    {clear=false;break;}
                }
                if(clear){radius=r;return tree;}
            }
            throw new InvalidOperationException("No unobstructed existing tree orbit found.");
        }
        IEnumerator Start()
        {
            yield return null;session=FindFirstObjectByType<ArchiveTopicSession>();hunt=session.hunting;player=Player.LocalInstance;capsule=player.GetComponent<CharacterController>();
            report.label=label;Save();
            while(ArchiveMatchCinematic.Covers(session))yield return null;
            var encounter=hunt.GetComponent<KhezuCaptureEncounterController>();var cinematic=hunt.GetComponent<KhezuCinematicJumpscareController>();
            var exploration=FindFirstObjectByType<OpenArchiveExplorationSession>();var sinking=FindFirstObjectByType<OpenArchiveWaterSinking>();
            bool oldPlayer=player.enabled,oldEncounter=encounter.enabled,oldCinematic=cinematic.enabled,oldExploration=exploration.enabled,oldSinking=sinking.enabled,oldInput=GameInput.Instance.GameplayInputBlocked;
            bool oldDefeat=session.suppressDefeatForVerification,oldThreat=session.worldProgression.suppressThreatsForVerification;
            var selector=session.worldProgression.selector;var selected=selector.Selected;var originalPosition=player.transform.position;var originalRotation=player.transform.rotation;
            session.suppressDefeatForVerification=true;session.worldProgression.suppressThreatsForVerification=true;
            player.enabled=encounter.enabled=cinematic.enabled=exploration.enabled=sinking.enabled=false;GameInput.Instance.SetGameplayInputBlocked(true);
            try
            {
                foreach(var kind in new[]{DevelopmentMonster.Rajang,DevelopmentMonster.Khezu})foreach(int pattern in label.Contains("cover")?new[]{2}:new[]{0,1,2})
                {
                    driving=false;hunt.Request(KhezuCommand.Sleep);encounter.SynchronizeBarrierMovement();selector.Select(kind);
                    radius=hunt.Agent.radius+capsule.radius*Mathf.Abs(player.transform.lossyScale.x)+3.25f;
                    coverOrbit=pattern==2;Vector3 center;
                    if(coverOrbit)
                    {
                        var tree=FindCover();fixedCenter=new Vector3(tree.bounds.center.x,0,tree.bounds.center.z);
                        var filter=new NavMeshQueryFilter{agentTypeID=hunt.Agent.agentTypeID,areaMask=hunt.Agent.areaMask};
                        if(!NavMesh.SamplePosition(fixedCenter+Vector3.forward*(hunt.Agent.radius+7),out var spot,4,filter))throw new InvalidOperationException("Cover approach is not navigable.");center=spot.position;
                    }
                    else center=FindClearing();hunt.Agent.enabled=true;hunt.Agent.Warp(center);hunt.transform.rotation=coverOrbit?Quaternion.LookRotation(fixedCenter-center):Quaternion.identity;
                    var body=hunt.GetComponent<BoxCollider>();if(!coverOrbit){fixedCenter=body.bounds.center;fixedCenter.y=center.y;}
                    float footOffset=player.transform.position.y-OpenArchiveWater.Feet(capsule).y;
                    Place(fixedCenter+(coverOrbit?Vector3.back:Vector3.right)*radius+Vector3.up*(footOffset+.05f));
                    var state=hunt.GetSnapshot();state.state=(int)KhezuHuntState.Ready;state.awake=true;state.pending=false;state.mistakes=state.stage=4;state.target=state.caught=0;state.animation="Idle";state.normalizedTime=0;
                    hunt.ApplySnapshot(state,true);hunt.SetPlayerEligibility(-1,true);hunt.Request(KhezuCommand.StartHunting);
                    travel=blocked=runnerBlocked=speedTotal=stopped=maxStep=0;frames=0;angle=0;orbitSign=pattern==1?-1:1;
                    int serial=hunt.CloseCounterSerial;double start=KhezuHuntingController.EncounterClock;
                    string patternName=coverOrbit?"keep tree between runner and monster":pattern==1?"counter-clockwise full sprint":"clockwise full sprint";
                    Status=kind+" "+patternName;driving=true;bool warningSeen=false,coverWarningVisible=false;
                    while(hunt.State!=KhezuHuntState.Caught&&KhezuHuntingController.EncounterClock-start<15)
                    {
                        if(hunt.CloseCounterPhase==MonsterCloseCounterPhase.Warning)
                        {
                            warningSeen=true;
                            var tell=hunt.transform.Find("Close_Counter_Warning/Counter_Around_Cover_Tell");
                            if(tell!=null&&tell.GetComponent<LineRenderer>().enabled&&!coverWarningVisible)
                            {coverWarningVisible=true;ScreenCapture.CaptureScreenshot("Temp/MonsterAntiKite/"+kind+"-cover-warning.png");}
                        }
                        yield return null;
                    }
                    driving=false;
                    report.results.Add(new Result{monster=kind.ToString(),pattern=patternName,caught=hunt.State==KhezuHuntState.Caught,
                        valid=blocked<.5f&&runnerBlocked<.5f&&(!coverOrbit||warningSeen&&coverWarningVisible),warningSeen=warningSeen,coverWarningVisible=coverWarningVisible,seconds=(float)(KhezuHuntingController.EncounterClock-start),travelled=travel,blockedSeconds=blocked,runnerBlockedSeconds=runnerBlocked,averageMonsterSpeed=frames>0?speedTotal/frames:0,stoppedSeconds=stopped,
                        counters=hunt.CloseCounterSerial-serial,maximumRunnerStep=maxStep});Save();
                    hunt.Request(KhezuCommand.Sleep);encounter.SynchronizeBarrierMovement();yield return null;
                }
                report.finished=true;
            }
            finally
            {
                driving=false;hunt.Request(KhezuCommand.Sleep);encounter.SynchronizeBarrierMovement();cinematic.Cancel();selector.Select(selected);
                Place(originalPosition);player.transform.rotation=originalRotation;
                player.enabled=oldPlayer;encounter.enabled=oldEncounter;cinematic.enabled=oldCinematic;exploration.enabled=oldExploration;sinking.enabled=oldSinking;
                GameInput.Instance.SetGameplayInputBlocked(oldInput);session.suppressDefeatForVerification=oldDefeat;session.worldProgression.suppressThreatsForVerification=oldThreat;
                Save();Status=report.finished?"Finished":"Interrupted";Destroy(gameObject);
            }
        }
    }
}
#endif
