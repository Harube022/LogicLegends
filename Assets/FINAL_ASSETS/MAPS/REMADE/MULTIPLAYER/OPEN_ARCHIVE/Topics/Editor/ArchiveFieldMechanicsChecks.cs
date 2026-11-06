using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LogicLegends.OpenArchive.Hunting;
using Photon.Pun;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>Opt-in checks of the saved multiplayer scene. Presentation never grants gameplay state.</summary>
    public static class ArchiveFieldMechanicsChecks
    {
        [Serializable] sealed class Report
        {
            public bool passed;
            public string scope="Saved MultiplayerGameplay offline Editor Play. Exercises real field presentation, authoritative validation and checkpoint persistence using explicit practice fixtures. Physical Android, remote touch rendering and production currency are not claimed.";
            public List<string> checks=new List<string>(),errors=new List<string>();
        }
        public static string Status {get;private set;}="Idle";
        static IEnumerator nativeFlow;
        static double nativeNext;
        static Report nativeReport;
        static ArchiveTopicSession nativeSession;
        static ArchiveFieldEvents nativeOwner;
        static bool previousDisturbances,previousOwnerEnabled,previousDefeatSuppression,previousWorldSuppression;
        static bool spawnedPracticeActor;
        static Vector3 previousPlayerPosition;
        static Quaternion previousPlayerRotation;
        static GameObject modifierFixture;
        static readonly BindingFlags PrivateInstance=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Check(Report report,bool condition,string label)
        {
            report.checks.Add((condition?"PASS ":"FAIL ")+label);
            if(!condition)throw new InvalidOperationException(label);
        }
        static ArchiveTopicSession RequireSession()
        {
            var session=ArchiveTopicSession.Current;
            if(!Application.isPlaying||PhotonNetwork.InRoom||session==null||!session.Ready||
                session.gameObject.scene.path!="Assets/Scenes/MultiplayerGameplay.unity")
                throw new InvalidOperationException("Use offline Play Mode from the saved MultiplayerGameplay scene.");
            return session;
        }
        static void Save(Report report,string file)
        {
            report.passed=report.errors.Count==0;Status=report.passed?"Passed":"Failed";
            Directory.CreateDirectory("Temp/ArchiveFieldEvents");
            File.WriteAllText("Temp/ArchiveFieldEvents/"+file,JsonUtility.ToJson(report,true));
            Debug.Log("Archive field checks: "+Status+" ("+report.checks.Count+").");
        }

        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test Field Presentation (Offline Play Mode)")]
        public static void RunPresentation()
        {
            var session=RequireSession();var report=new Report();Status="Checking presentation";
            var beacons=UnityEngine.Object.FindObjectsByType<ArchiveResonanceBeaconView>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            var zones=UnityEngine.Object.FindObjectsByType<ArchivePathDisturbanceView>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            string state=JsonUtility.ToJson(session.State);float speed=Player.LocalInstance.moveSpeed;
            int navigation=UnityEngine.Object.FindObjectsByType<NavMeshObstacle>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
            try
            {
                Check(report,beacons.Length==4&&zones.Length==8,"Saved scene contains four beacons and eight bounded path disturbances");
                Check(report,beacons.Select(b=>b.id).Distinct().Count()==4&&zones.Select(z=>z.id).Distinct().Count()==8,"Authored beacons and disturbance sites have unique IDs");
                foreach(var view in beacons)
                {
                    CheckCosmetic(report,view.gameObject,"Beacon "+view.id);
                    for(int phase=0;phase<3;phase++)
                    {
                        view.Present(phase,12,.5f,5,true,true);
                        Check(report,view.CurrentState==phase&&view.Visible&&view.ActiveRendererCount>0&&view.ActiveRendererCount<=6,
                            "Beacon "+view.id+" presents charge state "+phase+" with bounded renderers");
                        Check(report,view.CosmeticElementCount<=9,"Beacon "+view.id+" bounds its orbiting accents");
                    }
                    view.Clear();Check(report,!view.Visible&&view.ActiveRendererCount==0,"Beacon "+view.id+" clears its visible presentation");
                }
                foreach(var view in zones)
                {
                    CheckCosmetic(report,view.gameObject,"Disturbance "+view.id);
                    for(int kind=0;kind<3;kind++)
                    {
                        view.Present(1,kind,.5f,5,true,true);
                        Check(report,view.Visible&&view.CurrentPhase==1&&view.CurrentKind==kind&&view.TriangleCount>0,
                            "Site "+view.id+" warns visibly before effect kind "+kind);
                        view.Present(2,kind,.5f,5,true,true);
                        Check(report,view.Visible&&view.CurrentPhase==2&&view.ActiveRendererCount>0&&view.ActiveRendererCount<=3&&view.TriangleCount<6000,
                            "Site "+view.id+" renders effect kind "+kind+" within mesh and renderer bounds");
                        view.Present(3,kind,1,5,true,true);
                        Check(report,!view.Visible&&view.ActiveRendererCount==0,"Site "+view.id+" fully dissolves kind "+kind);
                    }
                    view.Present(2,0,.5f,5,false,true);
                    Check(report,!view.Visible,"Site "+view.id+" culls distant disturbance geometry");
                }
                Check(report,JsonUtility.ToJson(session.State)==state&&Player.LocalInstance.moveSpeed==speed,
                    "Presentation alone leaves all shared gameplay, health, rewards and serialized movement speed unchanged");
                Check(report,UnityEngine.Object.FindObjectsByType<NavMeshObstacle>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==navigation,
                    "Presentation does not add a navigation obstacle");
            }
            catch(Exception exception){report.errors.Add(exception.ToString());}
            finally
            {
                foreach(var view in beacons)if(view!=null)view.Clear();
                foreach(var view in zones)if(view!=null)view.Clear();
                Save(report,"Presentation.json");
            }
        }
        static void CheckCosmetic(Report report,GameObject root,string label)
        {
            Check(report,root.GetComponentsInChildren<Collider>(true).Length==0&&
                root.GetComponentsInChildren<NavMeshObstacle>(true).Length==0&&
                root.GetComponentsInChildren<Rigidbody>(true).Length==0&&
                root.GetComponentsInChildren<ParticleSystem>(true).Length==0,
                label+" is finite mesh presentation with no collider, rigidbody, navigation obstacle or particles");
        }

        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test Field Scheduling (Offline Play Mode)")]
        public static void RunScheduling()
        {
            var session=RequireSession();var report=new Report();var original=session.State;
            bool enabled=session.enabled;float next=(float)typeof(ArchiveTopicSession).GetField("nextFieldTick",PrivateInstance).GetValue(session);
            var property=typeof(ArchiveTopicSession).GetProperty("State");
            var tick=typeof(ArchiveTopicSession).GetMethod("TickFieldEvents",PrivateInstance);
            var nextTick=typeof(ArchiveTopicSession).GetField("nextFieldTick",PrivateInstance);
            bool Tick(){nextTick.SetValue(session,0f);return (bool)tick.Invoke(session,null);}
            try
            {
                session.enabled=false;
                var fixture=JsonUtility.FromJson<ArchiveTeamState>(JsonUtility.ToJson(original));property.SetValue(session,fixture);
                fixture.fieldEvents=new ArchiveFieldEventState();fixture.exit.opened=fixture.exit.won=false;fixture.survival.defeated=false;
                fixture.worldTime.phase=0;
                fixture.matchTime.phase=ArchiveMatchTimePhase.Active;fixture.matchTime.startedAt=ArchiveTopicSession.Clock;
                fixture.matchTime.deadline=ArchiveTopicSession.Clock+2400;
                foreach(var station in fixture.stations)station.phase=0;
                Tick();Check(report,fixture.fieldEvents.site<0,"A fresh match has no disturbance before topic completion");
                foreach(int completed in new[]{1,2,4,6,8})
                {
                    for(int i=0;i<10;i++)fixture.stations[i].phase=i<completed?2:0;
                    var state=fixture.fieldEvents;int serial=state.serial;
                    state.site=-1;state.nextAllowedAt=0;Tick();
                    if(completed==1){Check(report,state.site<0&&state.serial==serial,"One solved topic does not trigger an early disturbance");continue;}
                    Check(report,state.site>=0&&state.serial==serial+1&&state.triggeredMilestone==completed/2,
                        completed+" solved topics trigger exactly one authoritative roadside event");
                    Check(report,state.activeAt-state.warnedAt==ArchiveFieldRules.WarningSeconds&&state.endsAt-state.activeAt==ArchiveFieldRules.ActiveSeconds,
                        completed+"-topic event starts with the complete warning and finite active interval");
                    Check(report,ArchiveFieldEvents.Horizontal(Player.LocalInstance.transform.position,session.fieldEvents.Site(state.site).transform.position)>=12,
                        completed+"-topic event leaves distance from the current player");
                    int accepted=state.serial;Tick();Check(report,state.serial==accepted,"The running event is not duplicated by another host tick");
                    state.site=-1;state.nextAllowedAt=ArchiveTopicSession.Clock+75;Tick();
                    Check(report,state.site<0&&state.serial==accepted,"Cooldown cannot restart the same completed milestone");
                }
                var fields=fixture.fieldEvents;
                fields.site=session.fieldEvents.hazards[0].id;fields.endsAt=ArchiveTopicSession.Clock-3;Tick();
                Check(report,fields.site<0,"An expired site clears after its finite dissolve interval");
                fields.site=session.fieldEvents.hazards[0].id;fixture.exit.opened=true;Tick();
                Check(report,fields.site<0,"Opening the escape portal clears roadside slowdown events");
                fixture.exit.opened=false;fields.site=session.fieldEvents.hazards[0].id;fixture.survival.defeated=true;Tick();
                Check(report,fields.site<0,"Team defeat clears any active roadside event");
                fixture.survival.defeated=false;fields.site=session.fieldEvents.hazards[0].id;fixture.exit.won=true;Tick();
                Check(report,fields.site<0,"Team victory clears any active roadside event");
                Check(report,fixture.mistakes.Count==original.mistakes.Count&&fixture.rewards.adminUsed==original.rewards.adminUsed,
                    "Scheduling itself does not add mistakes or alter reward eligibility");
            }
            catch(Exception exception){report.errors.Add(exception.ToString());}
            finally{property.SetValue(session,original);nextTick.SetValue(session,next);session.enabled=enabled;Save(report,"Scheduling.json");}
        }

        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test Field Rules And Routes (Offline Play Mode)")]
        public static void RunRulesAndGeometry()
        {
            var session=RequireSession();var report=new Report();Status="Checking rules and routes";
            try
            {
                var owner=UnityEngine.Object.FindFirstObjectByType<ArchiveFieldEvents>();
                Check(report,owner!=null&&owner.session==session&&owner.hazards.Length==8&&owner.beacons.Length==4,
                    "One field owner references the actual multiplayer session and all authored sites");
                var timed=new ArchiveFieldEventState{site=0,warnedAt=10,activeAt=13,endsAt=31};
                Check(report,ArchiveFieldRules.Phase(timed,9.99)==0&&ArchiveFieldRules.Phase(timed,10)==1&&ArchiveFieldRules.Phase(timed,12.99)==1,
                    "Three full warning seconds precede an active disturbance");
                Check(report,ArchiveFieldRules.Phase(timed,13)==2&&ArchiveFieldRules.Phase(timed,30.99)==2&&ArchiveFieldRules.Phase(timed,31)==3,
                    "The finite eighteen-second active phase ends exactly at its shared deadline");
                Check(report,ArchiveFieldRules.Phase(timed,32.99)==3&&ArchiveFieldRules.Phase(timed,33)==0&&ArchiveFieldRules.Phase(null,15)==0,
                    "Dissolution ends after two seconds and absent state stays hidden");
                var copy=JsonUtility.FromJson<ArchiveFieldEventState>(JsonUtility.ToJson(timed));
                Check(report,ArchiveFieldRules.Phase(copy,14)==2&&copy.activeAt==13&&copy.endsAt==31,
                    "Checkpoint serialization preserves phase ownership and exact timestamps");
                foreach(var zone in owner.hazards)
                {
                    Check(report,zone!=null&&owner.Site(zone.id)==zone&&Mathf.Abs(zone.transform.lossyScale.x-1)<.001f&&Mathf.Abs(zone.transform.lossyScale.z-1)<.001f,
                        "Site "+zone.id+" keeps world-sized bounds independently of the map's scale");
                    Vector3 Point(float x,float y,float z)=>zone.transform.TransformPoint(new Vector3(x,y,z));
                    Check(report,ArchiveFieldRules.Contains(zone,Point(0,0,0))&&
                        ArchiveFieldRules.Contains(zone,Point(zone.halfWidth-.01f,0,zone.halfLength-.01f))&&
                        !ArchiveFieldRules.Contains(zone,Point(zone.halfWidth+.02f,0,0))&&
                        !ArchiveFieldRules.Contains(zone,Point(0,0,zone.halfLength+.02f))&&
                        !ArchiveFieldRules.Contains(zone,Point(0,1.5f,0))&&
                        !ArchiveFieldRules.Contains(zone,Point(0,-1.5f,0)),
                        "Site "+zone.id+" limits the effect to its oriented horizontal footprint and supported height");
                    bool bypass=true;
                    foreach(float x in new[]{2.15f,3.65f,5.15f})foreach(float z in new[]{-3.5f,0,3.5f})
                    {
                        var point=Point(x,0,z);
                        bool supported=Physics.RaycastAll(point+Vector3.up*2,Vector3.down,4,~(1<<7),QueryTriggerInteraction.Ignore)
                            .Any(hit=>hit.normal.y>.85f&&Mathf.Abs(hit.point.y-zone.transform.position.y)<.3f&&
                                hit.collider.GetComponentInParent<Player>()==null&&hit.collider.GetComponentInParent<KhezuHuntingController>()==null);
                        bypass&=supported&&!ArchiveFieldRules.Contains(zone,point);
                    }
                    Check(report,bypass,"Site "+zone.id+" leaves a supported three-metre bypass outside its complete footprint");
                    Check(report,session.stations.All(station=>ArchiveFieldEvents.Horizontal(station.transform.position,zone.transform.position)>45),
                        "Site "+zone.id+" stays clear of all topic interaction areas");
                }
                var selection=UnityEngine.Object.FindFirstObjectByType<LogicLegends.Tests.Monsters.DevelopmentMonsterSelection>();
                int[] agents={selection.KhezuEncounterProfile.agentTypeId,selection.RajangEncounterProfile.agentTypeId};
                Check(report,agents.Distinct().Count()==2,"Both existing guardian navigation profiles are available");
                foreach(int agent in agents)
                {
                    var filter=new NavMeshQueryFilter{agentTypeID=agent,areaMask=NavMesh.AllAreas};
                    Check(report,NavMesh.SamplePosition(session.hunting.monsterSpawnPoint.position,out var origin,20,filter),
                        "Guardian profile "+agent+" has its original nest navigation");
                    foreach(var beacon in owner.beacons)
                    {
                        var route=new NavMeshPath();
                        bool reachable=NavMesh.SamplePosition(beacon.transform.position,out var destination,3,filter)&&
                            Mathf.Abs(destination.position.y-beacon.transform.position.y)<1&&
                            NavMesh.CalculatePath(origin.position,destination.position,filter,route)&&route.status==NavMeshPathStatus.PathComplete;
                        Check(report,reachable,"Guardian profile "+agent+" has a complete baked route to beacon "+beacon.id);
                    }
                }
            }
            catch(Exception exception){report.errors.Add(exception.ToString());}
            finally{Save(report,"RulesAndGeometry.json");}
        }

        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test Field Authority And Movement (Offline Play Mode)")]
        public static void RunNative()
        {
            if(nativeFlow!=null)throw new InvalidOperationException("Field authority checks are already running.");
            nativeSession=RequireSession();nativeOwner=nativeSession.fieldEvents;
            if(nativeOwner==null||nativeOwner.hazards.Length!=8||nativeOwner.beacons.Length!=4)
                throw new InvalidOperationException("Install the saved field-event owner and presentation first.");
            nativeReport=new Report();Status="Starting native field fixtures";
            previousDisturbances=nativeOwner.disturbancesEnabled;previousOwnerEnabled=nativeOwner.enabled;
            previousDefeatSuppression=nativeSession.suppressDefeatForVerification;
            previousWorldSuppression=nativeSession.worldProgression!=null&&nativeSession.worldProgression.suppressThreatsForVerification;
            previousPlayerPosition=Player.LocalInstance.transform.position;previousPlayerRotation=Player.LocalInstance.transform.rotation;
            spawnedPracticeActor=nativeSession.ActorPlayer(-2)==null;
            nativeFlow=NativeChecks();nativeNext=0;
            EditorApplication.update+=NativeTick;
        }
        static void NativeTick()
        {
            if(nativeFlow==null)return;EditorApplication.QueuePlayerLoopUpdate();
            if(EditorApplication.timeSinceStartup<nativeNext)return;
            try
            {
                if(!Application.isPlaying)throw new InvalidOperationException("Play Mode ended during field verification.");
                if(!nativeFlow.MoveNext()){FinishNative();return;}
                nativeNext=EditorApplication.timeSinceStartup+(nativeFlow.Current is float wait?wait:0);
            }
            catch(Exception exception){nativeReport.errors.Add(Status+": "+exception);FinishNative();}
        }
        static void FinishNative()
        {
            EditorApplication.update-=NativeTick;nativeFlow=null;
            if(modifierFixture!=null)
            {
                Player.LocalInstance?.RemoveMovementSpeedModifier(modifierFixture);
                UnityEngine.Object.Destroy(modifierFixture);modifierFixture=null;
            }
            if(nativeSession!=null&&Application.isPlaying)
            {
                nativeSession.hunting.HoldTranquilized(false);
                nativeSession.RequestAdminTest(ArchiveAdminTestAction.ReleaseCapture);
                nativeSession.RequestAdminTest(ArchiveAdminTestAction.ResetMatch);
                if(spawnedPracticeActor)nativeSession.RequestAdminTest(ArchiveAdminTestAction.RemoveTeammate);
                nativeSession.suppressDefeatForVerification=previousDefeatSuppression;
                if(nativeSession.worldProgression!=null)nativeSession.worldProgression.suppressThreatsForVerification=previousWorldSuppression;
                if(nativeOwner!=null){nativeOwner.disturbancesEnabled=previousDisturbances;nativeOwner.enabled=previousOwnerEnabled;}
                var player=Player.LocalInstance;
                if(player!=null)
                {
                    var controller=player.GetComponent<CharacterController>();bool enabled=controller!=null&&controller.enabled;
                    if(enabled)controller.enabled=false;
                    player.transform.SetPositionAndRotation(previousPlayerPosition,previousPlayerRotation);player.ResetMotionAfterTeleport();
                    if(enabled)controller.enabled=true;
                }
            }
            Save(nativeReport,"Native.json");
        }
        static void MoveFeet(Player player,Vector3 feet)
        {
            var controller=player.GetComponent<CharacterController>();
            float offset=player.transform.position.y-ArchiveFieldEvents.Feet(player).y;
            bool enabled=controller!=null&&controller.enabled;if(enabled)controller.enabled=false;
            player.transform.position=feet+Vector3.up*(offset+.04f);player.ResetMotionAfterTeleport();
            if(enabled)controller.enabled=true;
        }
        static void UpdateOwner()=>typeof(ArchiveFieldEvents).GetMethod("Update",PrivateInstance).Invoke(nativeOwner,null);
        static ArchiveCommand BeaconCommand(int id,int actor=-1)
        {
            var session=nativeSession;
            var previous=session.State.actors.FirstOrDefault(record=>record.actor==actor);
            var receipt=session.State.fieldEvents.beacons.FirstOrDefault(beacon=>beacon.id==id);
            var command=new ArchiveCommand{topic=0,action=ArchiveAction.ActivateBeacon,a=id,b=receipt?.serial??0,sequence=(previous?.lastSequence??0)+1};
            typeof(ArchiveTopicSession).GetMethod("StampCommandContext",PrivateInstance).Invoke(session,new object[]{command});return command;
        }
        static void HuntingFixture(KhezuHuntingController monster,Vector3 point,int target)
        {
            var snapshot=JsonUtility.FromJson<KhezuSnapshot>(JsonUtility.ToJson(monster.GetSnapshot()));
            snapshot.state=(int)KhezuHuntState.Hunting;snapshot.awake=true;snapshot.pending=false;snapshot.target=target;snapshot.caught=0;
            snapshot.position=point;snapshot.counterPhase=0;snapshot.rescueActor=0;snapshot.personalityPhase=0;
            monster.ApplySnapshot(snapshot,true);
            if(monster.Agent.enabled)monster.Agent.Warp(point);
            typeof(KhezuHuntingController).GetMethod("RefreshPlayers",PrivateInstance).Invoke(monster,null);
        }
        static string HealthReceipt(KhezuHuntingController monster)=>string.Join(",",monster.health.ExportActorHealth()
            .OrderBy(item=>item.actor).Select(item=>item.actor+":"+item.hearts+":"+item.lastPenaltySerial));
        static IEnumerator NativeChecks()
        {
            var report=nativeReport;var session=nativeSession;var owner=nativeOwner;var player=Player.LocalInstance;var monster=session.hunting;
            session.RequestAdminTest(ArchiveAdminTestAction.ResetMatch);session.RequestAdminTest(ArchiveAdminTestAction.StartMatchClock);
            session.suppressDefeatForVerification=true;
            if(session.worldProgression!=null)session.worldProgression.suppressThreatsForVerification=true;
            owner.disturbancesEnabled=false;owner.enabled=true;
            if(spawnedPracticeActor)Check(report,session.RequestAdminTest(ArchiveAdminTestAction.SpawnTeammate),"Existing offline practice teammate supplies a second authenticated actor");
            session.RequestAdminTest(ArchiveAdminTestAction.SetHearts,-1,monster.health.MaximumHealth);
            session.RequestAdminTest(ArchiveAdminTestAction.SetHearts,-2,monster.health.MaximumHealth);
            monster.Request(KhezuCommand.Sleep);yield return .12f;
            Status="Warning and movement composition";
            var state=session.State.fieldEvents;var zone=owner.hazards[0];float serializedSpeed=player.moveSpeed;
            MoveFeet(player,zone.transform.position);
            state.site=zone.id;state.serial++;state.warnedAt=ArchiveTopicSession.Clock;state.activeAt=state.warnedAt+3;state.endsAt=state.activeAt+18;
            UpdateOwner();Check(report,!owner.LocalSlowed,"A real player inside the warning footprint keeps full movement");
            state.warnedAt=ArchiveTopicSession.Clock-4;state.activeAt=ArchiveTopicSession.Clock-1;state.endsAt=ArchiveTopicSession.Clock+10;
            UpdateOwner();Check(report,owner.LocalSlowed&&Mathf.Abs(player.MovementSpeedMultiplier-ArchiveFieldRules.SlowMultiplier)<.001f,
                "The active footprint applies the existing owner-scoped player slowdown");
            modifierFixture=new GameObject("Field_Check_Existing_Modifier_Fixture");player.SetMovementSpeedModifier(modifierFixture,.5f);
            UpdateOwner();Check(report,Mathf.Abs(player.MovementSpeedMultiplier-.5f)<.001f,"A stronger existing movement effect composes without multiplying the penalty");
            owner.enabled=false;
            Check(report,!owner.LocalSlowed&&Mathf.Abs(player.MovementSpeedMultiplier-.5f)<.001f,"Disabling the field owner removes only its own modifier");
            player.RemoveMovementSpeedModifier(modifierFixture);owner.enabled=true;
            MoveFeet(player,zone.transform.TransformPoint(new Vector3(zone.halfWidth+2,0,0)));UpdateOwner();
            Check(report,!owner.LocalSlowed&&player.MovementSpeedMultiplier>.99f,"The clear side of the same road remains free of the disturbance penalty");
            MoveFeet(player,zone.transform.position);UpdateOwner();
            Check(report,owner.LocalSlowed,"Returning to the active footprint applies one finite penalty");
            state.endsAt=ArchiveTopicSession.Clock-.1;UpdateOwner();
            Check(report,!owner.LocalSlowed&&player.MovementSpeedMultiplier>.99f&&player.moveSpeed==serializedSpeed,
                "The active deadline removes the penalty without changing serialized walk speed");
            state.site=-1;

            Status="Real authoritative beacon requests";
            var beacon=owner.beacons[0];MoveFeet(player,beacon.transform.position+beacon.transform.forward*2);
            Check(report,!session.CanUseBeacon(-1,beacon.id,out _),"An asleep guardian cannot be woken or bypassed by a beacon");
            var filter=new NavMeshQueryFilter{agentTypeID=monster.Agent.agentTypeID,areaMask=monster.Agent.areaMask};
            Check(report,NavMesh.SamplePosition(beacon.transform.position,out var beaconNav,3,filter),"The actual beacon shoulder is on the original baked navigation");
            Vector3 inward=-new Vector3(beacon.transform.position.x,0,beacon.transform.position.z).normalized;
            bool found=false;Vector3 origin=Vector3.zero,teammatePoint=Vector3.zero;
            foreach(float distance in new[]{25f,30f,20f})
            {
                if(!NavMesh.SamplePosition(beacon.transform.position+inward*distance,out var start,4,filter)||
                    !NavMesh.SamplePosition(start.position+inward*55,out var remote,8,filter))continue;
                var route=new NavMeshPath();if(!NavMesh.CalculatePath(start.position,beaconNav.position,filter,route)||route.status!=NavMeshPathStatus.PathComplete)continue;
                origin=start.position;teammatePoint=remote.position;found=true;break;
            }
            Check(report,found,"A real baked route positions an awake guardian within hearing distance");
            MoveFeet(session.ActorPlayer(-2),teammatePoint);HuntingFixture(monster,origin,-2);
            Check(report,session.CanUseBeacon(-1,beacon.id,out _),"A nearby living actor may signal an awake reachable hunting guardian");
            MoveFeet(player,beacon.transform.position+Vector3.up*4);Check(report,!session.CanUseBeacon(-1,beacon.id,out _),"An actor above a beacon cannot activate through another floor");
            MoveFeet(player,beacon.transform.position+Vector3.forward*12);Check(report,!session.CanUseBeacon(-1,beacon.id,out _),"A distant actor cannot activate the world beacon");
            MoveFeet(player,beacon.transform.position+beacon.transform.forward*2);
            int revision=session.State.revision;var command=BeaconCommand(beacon.id);command.matchId="stale-field-match";
            Check(report,!session.Accept(-1,command)&&session.State.revision==revision,"A stale-match beacon packet cannot mutate the session");
            command=BeaconCommand(beacon.id);command.b++;
            Check(report,!session.Accept(-1,command),"A stale or invented beacon serial cannot activate a new charge");
            command=BeaconCommand(beacon.id);command.contextVersion=0;
            Check(report,!session.Accept(-1,command),"A context-free command cannot trigger shared diversion");
            Check(report,!session.Accept(-999,BeaconCommand(beacon.id,-999))&&!session.Accept(-1,BeaconCommand(999)),"Unknown actors and authored IDs are rejected");
            double deadline=session.State.matchTime.deadline;int mistakes=session.State.mistakes.Count,solved=session.State.solved.Count;
            string topics=JsonUtility.ToJson(session.State.stations[0]),rewards=JsonUtility.ToJson(session.State.rewards),health=HealthReceipt(monster);
            int[] rotation=monster.HuntedPlayerIds.OrderBy(id=>id).ToArray();
            command=BeaconCommand(beacon.id);int previousSerial=state.beacons.First(receipt=>receipt.id==beacon.id).serial;
            Check(report,session.Accept(-1,command),"The real host validator accepts one authenticated beacon activation");
            var receipt=state.beacons.First(item=>item.id==beacon.id);
            Check(report,state.activeBeacon==beacon.id&&state.beaconActor==-1&&state.beaconMonster==0&&receipt.serial==previousSerial+1&&
                Math.Abs(state.beaconEndsAt-state.beaconStartedAt-ArchiveFieldRules.BeaconSeconds)<.002&&
                Math.Abs(receipt.cooldownUntil-state.beaconStartedAt-ArchiveFieldRules.BeaconCooldownSeconds)<.002&&state.globalBeaconCooldownUntil==receipt.cooldownUntil,
                "One snapshot owns the finite diversion, original activator, serial and global cooldown");
            revision=session.State.revision;
            Check(report,!session.Accept(-1,command)&&session.State.revision==revision,"Replaying the accepted packet cannot extend or charge the beacon twice");
            Check(report,owner.beacons.All(view=>!session.CanUseBeacon(-1,view.id,out _)),"An active diversion prevents a second or chained beacon across the map");
            var checkpoint=JsonUtility.FromJson<ArchiveTeamState>(JsonUtility.ToJson(session.State));
            Check(report,checkpoint.fieldEvents.activeBeacon==beacon.id&&checkpoint.fieldEvents.beaconActor==-1&&
                Math.Abs(checkpoint.fieldEvents.beaconEndsAt-state.beaconEndsAt)<.002&&Math.Abs(checkpoint.fieldEvents.globalBeaconCooldownUntil-state.globalBeaconCooldownUntil)<.002,
                "Late-join and handoff checkpoints preserve exact diversion and cooldown timestamps");
            yield return 1.1f;
            Check(report,monster.IsBeaconDiverted&&monster.HuntingPersonalitySuppressed&&monster.Agent.enabled&&monster.Agent.hasPath&&
                ArchiveFieldEvents.Horizontal(monster.Agent.destination,beacon.transform.position)<4,
                "After its audible warning the existing guardian follows a real path to the beacon");
            Check(report,monster.TargetPlayerId==-2&&monster.HuntedPlayerIds.OrderBy(id=>id).SequenceEqual(rotation),"Diversion preserves the original hunt target and fair rotation");
            Check(report,session.State.matchTime.deadline==deadline&&session.State.mistakes.Count==mistakes&&session.State.solved.Count==solved&&
                JsonUtility.ToJson(session.State.stations[0])==topics&&JsonUtility.ToJson(session.State.rewards)==rewards&&
                HealthReceipt(monster)==health,
                "Beacon use grants no puzzle progress, extra match time, health, participation credit or currency");
            monster.HoldTranquilized(true);yield return .2f;
            Check(report,!monster.IsBeaconDiverted&&state.activeBeacon<0&&!session.CanUseBeacon(-1,beacon.id,out _),"Sedation takes priority and cancels the route without refunding a charge");
            monster.HoldTranquilized(false);HuntingFixture(monster,origin,-2);
            Check(report,!session.CanUseBeacon(-1,beacon.id,out _)&&state.globalBeaconCooldownUntil>ArchiveTopicSession.Clock,
                "Interrupted diversion retains the shared anti-spam cooldown");
            state.globalBeaconCooldownUntil=ArchiveTopicSession.Clock-.1;receipt.cooldownUntil=ArchiveTopicSession.Clock-.1;
            command=BeaconCommand(beacon.id);command.b=previousSerial;
            Check(report,!session.Accept(-1,command),"An old activation serial remains invalid after cooldown expiry");
            Check(report,session.RequestBeacon(beacon.id),"The native request path becomes available again after a real charge cooldown");
            var captured=monster.GetSnapshot();captured.state=(int)KhezuHuntState.Caught;captured.caught=-1;captured.target=0;
            monster.ApplySnapshot(captured,true);
            Check(report,!session.CanUseBeacon(-1,beacon.id,out _),"A captive cannot signal or escape the existing capture flow through a beacon");
            yield return .15f;
            Check(report,state.activeBeacon<0&&!monster.IsBeaconDiverted&&monster.State==KhezuHuntState.Caught,
                "An active capture cancels diversion and retains capture authority");
        }
    }
}
