using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LogicLegends.OpenArchive.Hunting;
using LogicLegends.Tests.Monsters;
using Photon.Pun;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>Explicit offline fixtures on both existing rigs and their real baked navigation. Never attached to a saved scene.</summary>
    public static class ArchiveGuardianPersonalityChecks
    {
        [Serializable] sealed class Report
        {
            public bool passed;
            public string scope="Saved MultiplayerGameplay offline Editor Play. Both existing shared guardian profiles run the real authoritative personality, destination and presentation methods over native frames on their baked NavMeshes. A temporary capsule, explicit guardian QA placement and checkpoint fixtures isolate timing/interruptions; the user avatar is never moved. Physical Android and independently rendered remote clients remain unverified.";
            public List<string> checks=new List<string>(),errors=new List<string>();
        }
        [Serializable] sealed class HealthCopy {public HealthManager.ActorHealth[] actors;}
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        const int FixtureActor=-991;
        static Report report;
        static IEnumerator flow;
        static double nextTick;
        public static string Status {get;private set;}="Idle";

        static object Call(object owner,string method,params object[] arguments)
        {
            var member=owner.GetType().GetMethod(method,Private)??throw new MissingMethodException(owner.GetType().Name,method);
            try{return member.Invoke(owner,arguments);}catch(TargetInvocationException exception){throw exception.InnerException??exception;}
        }
        static T Field<T>(object owner,string name)=>(T)(owner.GetType().GetField(name,Private)??throw new MissingFieldException(owner.GetType().Name,name)).GetValue(owner);
        static void SetProperty(object owner,string name,object value)=>(owner.GetType().GetProperty(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)??throw new MissingMemberException(name)).SetValue(owner,value);
        static KhezuSnapshot Copy(KhezuSnapshot value)=>JsonUtility.FromJson<KhezuSnapshot>(JsonUtility.ToJson(value));
        static string Health(KhezuHuntingController hunt)=>JsonUtility.ToJson(new HealthCopy{actors=hunt.health.ExportActorHealth()});
        static void Check(bool condition,string label)
        {report.checks.Add((condition?"PASS ":"FAIL ")+label);if(!condition)throw new InvalidOperationException(label);}

        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test Guardian Personalities (Offline Play Mode)")]
        public static void Run()
        {
            var session=ArchiveTopicSession.Current;
            if(flow!=null)throw new InvalidOperationException("Guardian personality checks are already running.");
            if(!Application.isPlaying||PhotonNetwork.InRoom||session==null||!session.Ready||session.hunting==null||
                !session.hunting.Initialized||session.gameObject.scene.path!="Assets/Scenes/MultiplayerGameplay.unity")
                throw new InvalidOperationException("Use fresh saved MultiplayerGameplay offline Play with the session and primary guardian initialized.");
            var hunt=session.hunting;
            if(session.TeamWon||session.TeamDefeated||session.NightEntranceActive||hunt.IsTranquilized||hunt.CarryingRescueActor||hunt.EntranceHeld||
                hunt.State==KhezuHuntState.Caught||hunt.State==KhezuHuntState.ReturningToNest||hunt.State==KhezuHuntState.AttackRecovery)
                throw new InvalidOperationException("Finish the current capture, rescue, sleep hold, night entrance or terminal result before running the offline fixture.");
            if(session.worldProgression==null||session.worldProgression.selector==null||!session.worldProgression.selector.HasSharedEncounter||hunt.health==null)
                throw new InvalidOperationException("The saved scene's shared guardian selector, world progression and authoritative health are required.");
            report=new Report();Status="Preparing real guardian profiles";flow=Checks(session);nextTick=0;
            EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=PlayState;
        }
        static void Tick()
        {
            if(flow==null)return;
            EditorApplication.QueuePlayerLoopUpdate();
            if(EditorApplication.timeSinceStartup<nextTick)return;
            try
            {
                if(!Application.isPlaying)throw new InvalidOperationException("Play Mode stopped during personality verification.");
                if(!flow.MoveNext()){Finish();return;}
                nextTick=EditorApplication.timeSinceStartup+(flow.Current is float delay?delay:0);
            }
            catch(Exception exception){report.errors.Add(Status+": "+exception);Finish();}
        }
        static void PlayState(PlayModeStateChange change)
        {if(change==PlayModeStateChange.ExitingPlayMode&&flow!=null){report.errors.Add("Play Mode interrupted.");Finish();}}
        static void Finish()
        {
            var previous=flow;flow=null;EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=PlayState;
            try{(previous as IDisposable)?.Dispose();}catch(Exception exception){report.errors.Add("Fixture restore: "+exception);}
            report.passed=report.errors.Count==0&&report.checks.Count>0;Status=report.passed?"Passed":"Failed";
            Directory.CreateDirectory("Temp/ArchiveFieldEvents");
            File.WriteAllText("Temp/ArchiveFieldEvents/GuardianPersonality.json",JsonUtility.ToJson(report,true));
            Debug.Log("Guardian personality native checks: "+Status+" ("+report.checks.Count+").");
        }

        static object Target(GameObject dummy)
        {
            var type=typeof(KhezuHuntingController).GetNestedType("Target",BindingFlags.NonPublic);
            var target=Activator.CreateInstance(type);type.GetField("transform").SetValue(target,dummy.transform);
            type.GetField("capsule").SetValue(target,dummy.GetComponent<CharacterController>());type.GetField("name").SetValue(target,"QA route capsule");
            return target;
        }
        static KhezuSnapshot HuntingFixture(KhezuHuntingController hunt,Vector3 position)
        {
            var snapshot=hunt.GetSnapshot();snapshot.state=(int)KhezuHuntState.Hunting;snapshot.position=position;
            snapshot.animation="Idle";snapshot.animationSpeed=1;snapshot.normalizedTime=0;snapshot.mistakes=4;snapshot.stage=4;
            snapshot.target=FixtureActor;snapshot.caught=0;snapshot.awake=true;snapshot.pending=false;
            snapshot.encounterPhase=0;snapshot.rescueActor=0;snapshot.rescueAtNest=false;snapshot.rescuePickedUp=false;
            snapshot.counterPhase=0;snapshot.counterTarget=0;snapshot.counterCooldownUntil=0;
            snapshot.personalityPhase=0;snapshot.personalityTarget=0;snapshot.personalityStartedAt=0;snapshot.personalityCooldownUntil=0;
            return snapshot;
        }
        static bool PlaceFixture(KhezuHuntingController hunt,GameObject dummy,object target,out Vector3 anchor,out Vector3 side)
        {
            anchor=side=Vector3.zero;var agent=hunt.Agent;
            var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
            // Reuse the authored road shoulders: the production map is scaled
            // 2.5 and unscaled inner-map coordinates may all land in the river.
            var field=UnityEngine.Object.FindFirstObjectByType<ArchiveFieldEvents>();
            var origins=(field!=null?field.hazards.Where(v=>v!=null).Select(v=>v.transform.position+v.transform.right*(v.halfWidth+3.5f)):Enumerable.Empty<Vector3>())
                .Concat(new[]{Vector3.zero,new Vector3(25,0,30),new Vector3(60,0,40),new Vector3(55,0,95),new Vector3(0,0,-70),new Vector3(-44,0,104)}).ToArray();
            foreach(var origin in origins)
            {
                if(!NavMesh.SamplePosition(origin,out var start,12,filter))continue;
                agent.enabled=true;if(!agent.Warp(start.position))continue;
                foreach(float distance in new[]{24f,28f,32f,36f,40f,46f})for(int direction=0;direction<16;direction++)
                {
                    Vector3 forward=Quaternion.Euler(0,direction*22.5f,0)*Vector3.forward;
                    if(!NavMesh.SamplePosition(start.position+forward*distance,out var end,2,filter)||Mathf.Abs(end.position.y-start.position.y)>.75f)continue;
                    if(NavMesh.Raycast(start.position,end.position,out var edge,filter))continue;
                    var path=new NavMeshPath();
                    if(!NavMesh.CalculatePath(start.position,end.position,filter,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
                    hunt.transform.rotation=Quaternion.LookRotation(forward);dummy.transform.position=end.position+Vector3.up*.05f;Physics.SyncTransforms();
                    float gap=(float)Call(hunt,"TargetSurfaceGap",target);
                    if(gap<18||gap>27||!KhezuLandApproach.ClearLine(start.position,dummy.transform,dummy.transform.position.y,hunt.catchObstructionMask))continue;
                    Vector3 tangent=Vector3.Cross(Vector3.up,forward);
                    bool room=true;
                    foreach(int sign in new[]{-1,1})
                    {
                        Vector3 point=end.position+tangent*sign*4;
                        if(!NavMesh.SamplePosition(point,out var margin,.6f,filter)||Vector3.Distance(margin.position,point)>.6f)room=false;
                    }
                    if(!room)continue;
                    var fixture=HuntingFixture(hunt,start.position);fixture.rotation=hunt.transform.rotation;hunt.ApplySnapshot(fixture,true);
                    agent.enabled=true;if(!agent.Warp(start.position))continue;
                    agent.isStopped=false;if(!agent.SetPath(path))continue;
                    anchor=dummy.transform.position;side=tangent;return true;
                }
            }
            return false;
        }

        static IEnumerator Checks(ArchiveTopicSession session)
        {
            var hunt=session.hunting;var world=session.worldProgression;var selector=world.selector;
            var original=hunt.GetSnapshot();var originalProfile=selector.Selected;
            bool oldSessionEnabled=session.enabled,oldHuntEnabled=hunt.enabled,oldWorldEnabled=world.enabled;
            bool oldThreat=world.suppressThreatsForVerification,oldDefeat=session.suppressDefeatForVerification;
            bool oldPersonality=hunt.huntingPersonalityEnabled,oldSuppression=hunt.HuntingPersonalitySuppressed,oldRajang=hunt.rajangCounterProfile;
            var secondary=world.secondary;bool secondaryActive=secondary!=null&&secondary.gameObject.activeSelf;
            var fieldOwner=UnityEngine.Object.FindFirstObjectByType<ArchiveFieldEvents>();bool fieldEnabled=fieldOwner!=null&&fieldOwner.enabled;
            var dummy=new GameObject("Temporary_Guardian_Personality_Capsule"){layer=7,hideFlags=HideFlags.DontSave};
            var capsule=dummy.AddComponent<CharacterController>();capsule.height=2;capsule.center=Vector3.up;capsule.radius=.8f;
            var target=Target(dummy);Vector3 playerPosition=Player.LocalInstance.transform.position;
            try
            {
                session.enabled=false;world.enabled=false;world.suppressThreatsForVerification=true;session.suppressDefeatForVerification=true;
                if(fieldOwner!=null)fieldOwner.enabled=false;
                if(secondaryActive)secondary.gameObject.SetActive(false);
                hunt.enabled=false;hunt.huntingPersonalityEnabled=true;hunt.HuntingPersonalitySuppressed=false;hunt.rajangCounterProfile=false;
                foreach(var profile in new[]{DevelopmentMonster.Rajang,DevelopmentMonster.Khezu})
                {
                    Status="Native "+profile+" warning, pressure and recovery";
                    hunt.ApplyCommand(KhezuCommand.Sleep);Check(selector.CanSwap,profile+": the existing selector permits this explicit offline rig fixture");
                    selector.Select(profile);Check(selector.Selected==profile,profile+": the existing authored visual, collider and navigation profile is selected");
                    hunt.CancelHuntingPersonality();
                    Check(PlaceFixture(hunt,dummy,target,out var anchor,out var side),profile+": a real reachable dry corridor supplies the QA chase fixture");
                    yield return .05f;
                    var baseline=hunt.GetSnapshot();string health=Health(hunt);int capture=baseline.captureSerial;
                    var hunted=baseline.hunted.ToArray();int serial=baseline.personalitySerial;
                    bool warning=false,active=false,recovery=false,complete=false,route=false,aimStable=true;
                    Vector3 lockedAim=Vector3.zero;double began=KhezuHuntingController.EncounterClock,limit=EditorApplication.timeSinceStartup+8;
                    float nextDestination=0,maxVelocity=0,maxPrediction=0,ordinary=hunt.normalPlayerRunSpeed*1.1f,cap=hunt.normalPlayerRunSpeed*hunt.chaseSpeedMultiplier;
                    double lastPredictionRefresh=-1;bool boundedSpeed=true;
                    int initialTarget=hunt.TargetPlayerId;Vector3 previousPose=hunt.transform.position;float maxStep=0;
                    while(EditorApplication.timeSinceStartup<limit)
                    {
                        double now=KhezuHuntingController.EncounterClock;
                        dummy.transform.position=anchor+side*Mathf.Sin((float)(now-began)*1.5f)*2;Physics.SyncTransforms();
                        if(Time.time>=nextDestination){Call(hunt,"UpdateDestination",target);nextDestination=Time.time+.1f;}
                        Call(hunt,"UpdateHuntingPersonality",target);
                        hunt.Agent.speed=(float)Call(hunt,"PersonalityPursuitSpeed",ordinary);
                        Call(hunt,"UpdateHuntingPersonalityPresentation");
                        maxVelocity=Mathf.Max(maxVelocity,hunt.Agent.velocity.magnitude);
                        maxStep=Mathf.Max(maxStep,Vector3.Distance(previousPose,hunt.transform.position));previousPose=hunt.transform.position;
                        var phase=hunt.HuntingPersonalityPhase;
                        if(phase==MonsterHuntingPersonalityPhase.Warning&&!warning)
                        {
                            warning=true;Check(hunt.HuntingPersonalitySerial==serial+1&&hunt.Agent.speed<ordinary,profile+": real Master update begins a readable warning with reduced pursuit speed");
                            var line=Field<LineRenderer>(hunt,"personalityWarning");var color=line.sharedMaterial.GetColor("_BaseColor");
                            Check(line.enabled&&line.positionCount==21,profile+": native presentation creates one bounded world-space pursuit tell");
                            Check(profile==DevelopmentMonster.Rajang?color.r>.95f&&color.g>.45f&&color.b<.1f:color.r>.95f&&Mathf.Abs(color.g-48/255f)<.01f&&Mathf.Abs(color.b-48/255f)<.01f,
                                profile+": tell uses "+(profile==DevelopmentMonster.Rajang?"orange/gold":"crimson #FF3030"));
                            var saved=hunt.GetSnapshot();var copy=Copy(saved);hunt.ApplySnapshot(copy,false);var roundtrip=hunt.GetSnapshot();
                            if(roundtrip.personalityPhase!=saved.personalityPhase||roundtrip.personalitySerial!=saved.personalitySerial||
                                roundtrip.personalityTarget!=saved.personalityTarget||Math.Abs(roundtrip.personalityStartedAt-saved.personalityStartedAt)>=.002||
                                Math.Abs(roundtrip.personalityCooldownUntil-saved.personalityCooldownUntil)>=.002||Vector3.Distance(roundtrip.personalityAimPoint,saved.personalityAimPoint)>=.001f)
                            {
                                Directory.CreateDirectory("Temp/ArchiveFieldEvents");
                                File.WriteAllText("Temp/ArchiveFieldEvents/GuardianSnapshotBefore.json",JsonUtility.ToJson(saved,true));
                                File.WriteAllText("Temp/ArchiveFieldEvents/GuardianSnapshotAfter.json",JsonUtility.ToJson(roundtrip,true));
                            }
                            Check(roundtrip.personalityPhase==saved.personalityPhase&&roundtrip.personalitySerial==saved.personalitySerial&&
                                roundtrip.personalityTarget==saved.personalityTarget&&Math.Abs(roundtrip.personalityStartedAt-saved.personalityStartedAt)<.002&&
                                Math.Abs(roundtrip.personalityCooldownUntil-saved.personalityCooldownUntil)<.002&&Vector3.Distance(roundtrip.personalityAimPoint,saved.personalityAimPoint)<.001f,
                                profile+": public JSON snapshot roundtrip preserves tell identity, aim and original timing");
                        }
                        if(profile==DevelopmentMonster.Khezu&&phase==MonsterHuntingPersonalityPhase.Warning)
                        {
                            double refresh=Field<double>(hunt,"nextPersonalityAimAt");
                            if(refresh!=lastPredictionRefresh)
                            {maxPrediction=Mathf.Max(maxPrediction,Vector3.Distance(hunt.HuntingPersonalityAimPoint,dummy.transform.position));lastPredictionRefresh=refresh;}
                        }
                        if(phase==MonsterHuntingPersonalityPhase.Active)
                        {
                            if(!active)
                            {
                                active=true;lockedAim=hunt.HuntingPersonalityAimPoint;
                                Check(profile==DevelopmentMonster.Rajang?hunt.Agent.speed>ordinary&&hunt.Agent.speed<=cap+.001f:Mathf.Abs(hunt.Agent.speed-ordinary)<.001f,
                                    profile+": active pressure uses its distinct bounded speed behavior");
                            }
                            if(profile==DevelopmentMonster.Khezu)
                            {
                                aimStable&=Vector3.Distance(lockedAim,hunt.HuntingPersonalityAimPoint)<.001f;
                                object[] arguments={target,null};
                                if((bool)Call(hunt,"TryHuntingPersonalityRoute",arguments))
                                {var path=(NavMeshPath)arguments[1];route|=path!=null&&path.status==NavMeshPathStatus.PathComplete;}
                            }
                        }
                        if(phase==MonsterHuntingPersonalityPhase.Recovery&&!recovery)
                        {recovery=true;Check(hunt.Agent.speed<ordinary,profile+": completed pressure yields a slower recovery interval");}
                        boundedSpeed&=hunt.Agent.speed<=cap+.001f;
                        if(warning&&active&&recovery&&phase==MonsterHuntingPersonalityPhase.None){complete=true;break;}
                        yield return .035f;
                    }
                    Check(warning&&active&&recovery&&complete,profile+": natural native frames complete warning, active pressure and recovery without retriggering");
                    if(profile==DevelopmentMonster.Khezu)
                    {
                        Check(maxPrediction<=5.05f,"Khezu's actual warning samples never lead the QA runner by more than five metres");
                        Check(route&&aimStable,"Khezu uses a complete real NavMesh pressure route and the committed active aim never homes after a turn");
                    }
                    Check(boundedSpeed,profile+": every phase remains within the existing pursuit speed ceiling");
                    Check(maxVelocity<=cap+.8f&&maxStep<cap*.3f+1,profile+": observed native navigation respects the chase ceiling and remains continuous after the labeled fixture placement");
                    var completed=hunt.GetSnapshot();double total=profile==DevelopmentMonster.Rajang?3.75:4.3;
                    Check(completed.personalityCooldownUntil-completed.personalityStartedAt>=total+10.99&&completed.personalityCooldownUntil>KhezuHuntingController.EncounterClock,
                        profile+": one shared cooldown retains at least eleven seconds after the complete ability window");
                    serial=hunt.HuntingPersonalitySerial;
                    Call(hunt,"UpdateHuntingPersonality",target);
                    Check(hunt.HuntingPersonalityPhase==MonsterHuntingPersonalityPhase.None&&hunt.HuntingPersonalitySerial==serial,profile+": the earned cooldown prevents an immediate repeated warning");
                    Check(hunt.TargetPlayerId==initialTarget&&hunt.CaptureSerial==capture&&hunt.CaughtPlayerId==0&&hunted.SequenceEqual(hunt.GetSnapshot().hunted)&&Health(hunt)==health,
                        profile+": the full ability preserves target/no-repeat rotation, capture serial and authoritative hearts");
                    Interruptions(hunt,target,profile);
                }
                Check(Vector3.Distance(Player.LocalInstance.transform.position,playerPosition)<.001f,"All guardian fixtures leave the user avatar at its original position");
            }
            finally
            {
                if(hunt!=null)
                {
                    hunt.CancelHuntingPersonality();SetProperty(hunt,"EntranceHeld",false);
                    if(hunt.IsTranquilized)hunt.HoldTranquilized(false);
                    hunt.ApplyCommand(KhezuCommand.Sleep);selector.Select(originalProfile);
                    hunt.huntingPersonalityEnabled=oldPersonality;hunt.HuntingPersonalitySuppressed=oldSuppression;hunt.rajangCounterProfile=oldRajang;
                    hunt.ApplySnapshot(original,true);hunt.AuthorityChanged();hunt.enabled=oldHuntEnabled;
                }
                if(secondary!=null)secondary.gameObject.SetActive(secondaryActive);
                if(fieldOwner!=null)fieldOwner.enabled=fieldEnabled;
                if(world!=null){world.suppressThreatsForVerification=oldThreat;world.enabled=oldWorldEnabled;}
                if(session!=null){session.suppressDefeatForVerification=oldDefeat;session.enabled=oldSessionEnabled;}
                if(dummy!=null)UnityEngine.Object.Destroy(dummy);
            }
        }

        static void Interruptions(KhezuHuntingController hunt,object target,DevelopmentMonster profile)
        {
            var ordinary=HuntingFixture(hunt,hunt.transform.position);ordinary.rotation=hunt.transform.rotation;
            ordinary.personalityPhase=1;ordinary.personalitySerial=hunt.HuntingPersonalitySerial+1;ordinary.personalityTarget=FixtureActor;
            ordinary.personalityStartedAt=KhezuHuntingController.EncounterClock;ordinary.personalityCooldownUntil=ordinary.personalityStartedAt+20;
            ordinary.personalityAimPoint=hunt.transform.position+hunt.transform.forward*20;
            string health=Health(hunt);int captures=hunt.CaptureSerial;var hunted=hunt.GetSnapshot().hunted.ToArray();
            void Restore(){hunt.HuntingPersonalitySuppressed=false;hunt.ApplySnapshot(Copy(ordinary),true);}
            void Canceled(string label)
            {
                Check(hunt.HuntingPersonalityPhase==MonsterHuntingPersonalityPhase.None&&Math.Abs(hunt.HuntingPersonalityCooldownUntil-ordinary.personalityCooldownUntil)<.002,
                    profile+": "+label+" cancels the tell while retaining its earned cooldown");
            }
            Restore();hunt.HuntingPersonalitySuppressed=true;Call(hunt,"UpdateHuntingPersonality",target);Canceled("beacon suppression");
            Restore();Call(hunt,"StopNavigation");Canceled("the existing navigation stop");
            foreach(var state in new[]{KhezuHuntState.Caught,KhezuHuntState.ReturningToNest,KhezuHuntState.NestSleeping,KhezuHuntState.CarryingToNest})
            {
                Restore();var interrupted=Copy(ordinary);interrupted.state=(int)state;interrupted.captureAnchorValid=true;
                interrupted.caught=state==KhezuHuntState.Caught?FixtureActor:0;interrupted.rescueActor=state==KhezuHuntState.CarryingToNest?FixtureActor:0;
                hunt.ApplySnapshot(interrupted,false);Canceled(state.ToString());
            }
            Restore();hunt.HoldNightEntrance(.3f);Canceled("the actual night-entrance hold");SetProperty(hunt,"EntranceHeld",false);
            Restore();hunt.HoldTranquilized(true);Canceled("the actual tranquilizer hold");hunt.HoldTranquilized(false);
            Restore();var counter=Copy(ordinary);counter.counterPhase=1;counter.counterTarget=FixtureActor;counter.counterAim=Vector3.forward;
            counter.counterStartedAt=KhezuHuntingController.EncounterClock;hunt.ApplySnapshot(counter,false);Canceled("the existing close counter");
            Restore();hunt.AuthorityChanged();Canceled("authority handoff");
            Check(hunt.CaptureSerial==captures&&hunted.SequenceEqual(hunt.GetSnapshot().hunted)&&Health(hunt)==health,
                profile+": all interruption fixtures preserve no-repeat rotation, capture serial and hearts");
            hunt.CancelHuntingPersonality();
        }
    }
}
