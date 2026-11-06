using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using ExitGames.Client.Photon;
using LogicLegends.OpenArchive.Hunting;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static partial class ArchiveTopicNetworkChecks
    {
        static bool fieldEventsOnly;
        static Action fieldEventsCleanup;
        const BindingFlags FieldPrivate=BindingFlags.Instance|BindingFlags.NonPublic;

        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test Field Events (Live Photon)")]
        public static void BeginFieldEvents()
        {
            if(!Application.isPlaying||PhotonNetwork.InRoom||active||ArchiveTopicSession.Current==null||
                ArchiveTopicSession.Current.gameObject.scene.path!="Assets/Scenes/MultiplayerGameplay.unity")
                throw new InvalidOperationException("Use fresh offline Play Mode from saved MultiplayerGameplay, outside a Photon room.");
            StartChecks(false,fieldEvents:true);
        }
        static void SaveFieldEvents()
        {
            if(fieldEventsCleanup!=null&&(report.passed||report.errors.Count>0))
            {
                try{fieldEventsCleanup();}catch(Exception exception){report.errors.Add("Field fixture cleanup: "+exception);report.passed=false;}
                fieldEventsCleanup=null;
            }
            Directory.CreateDirectory("Temp/ArchiveFieldEvents");
            File.WriteAllText("Temp/ArchiveFieldEvents/Photon.json",JsonUtility.ToJson(report,true));
        }
        static ArchiveCommand FieldBeaconCommand(Peer peer,int id)
        {
            var receipt=session.State.fieldEvents.beacons.FirstOrDefault(item=>item.id==id);
            var command=Command(peer,0,ArchiveAction.ActivateBeacon,id,receipt?.serial??0);
            typeof(ArchiveTopicSession).GetMethod("StampCommandContext",FieldPrivate).Invoke(session,new object[]{command});
            return command;
        }
        static IEnumerator FieldReceipt(Peer peer,ArchiveCommand command,bool spoof=false)
        {
            peer.receipts.Remove(command.sequence);Packet(peer,command,spoof);
            yield return Wait(()=>peer.receipts.ContainsKey(command.sequence),"Guest receives field-command receipt",8);
        }
        static Vector3 FieldAvatarPosition(Player player,Vector3 feet)
        {
            var capsule=player.GetComponent<CharacterController>();
            float offset=capsule!=null?player.transform.position.y-Water.OpenArchiveWater.Feet(capsule).y:0;
            return feet+Vector3.up*(offset+.04f);
        }
        static void FieldMoveHost(Vector3 feet)
        {
            var player=Player.LocalInstance;var capsule=player.GetComponent<CharacterController>();
            Vector3 destination=FieldAvatarPosition(player,feet),delta=destination-player.transform.position;
            bool enabled=capsule!=null&&capsule.enabled;if(enabled)capsule.enabled=false;
            player.transform.position=destination;player.ResetMotionAfterTeleport();if(enabled)capsule.enabled=true;
            TeleportCameraSync.ResetAfterMove(player.transform,delta);
        }
        static IEnumerator FieldMoveGuest(Peer peer,Vector3 feet)
        {
            Vector3 position=FieldAvatarPosition(session.ActorPlayer(peer.Actor),feet);
            peer.client.OpRaiseEvent(201,new object[]{PhotonNetwork.ServerTimestamp,null,
                new object[]{peer.viewId,false,null,position,Quaternion.identity,false,false,false,0f}},
                new RaiseEventOptions{Receivers=ReceiverGroup.Others},SendOptions.SendReliable);
            yield return Wait(()=>session.ActorPlayer(peer.Actor)!=null&&
                Vector3.Distance(session.ActorPlayer(peer.Actor).transform.position,position)<.3f,"Genuine guest movement packet reaches the beacon",8);
        }
        static string FieldHealth(KhezuHuntingController monster)=>string.Join(",",monster.health.ExportActorHealth()
            .OrderBy(item=>item.actor).Select(item=>item.actor+":"+item.hearts+":"+item.lastPenaltySerial));
        static string FieldTopics()=>string.Join("|",session.State.stations.Select(station=>JsonUtility.ToJson(station)));
        static void ApplyFieldHunt(KhezuHuntingController monster,KhezuSnapshot fixture)
        {
            monster.ApplySnapshot(JsonUtility.FromJson<KhezuSnapshot>(JsonUtility.ToJson(fixture)),true);
            if(monster.Agent.enabled)monster.Agent.Warp(fixture.position);
            typeof(KhezuHuntingController).GetMethod("RefreshPlayers",FieldPrivate).Invoke(monster,null);
        }
        static bool FieldCheckpointHunt(KhezuPhotonSync sync,int serial,int target)
        {
            if(!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(sync.StateKey,out var value)||!(value is object[] packet)||packet.Length!=4||!(packet[3] is string json))return false;
            var checkpoint=JsonUtility.FromJson<KhezuSnapshot>(json);
            return checkpoint!=null&&checkpoint.huntSerial==serial&&checkpoint.target==target&&checkpoint.awake;
        }

        static IEnumerator FieldEventsFlow(Peer guest,string room)
        {
            report.scope="Live invisible private Photon room: rendered Editor host and independent Realtime guest. Genuine guest movement and authenticated beacon packets exercise replay, stale context/serial, forged snapshots, shared cooldown and exact absolute timestamps. An explicitly trusted awake-monster/NavMesh fixture and warning-site fixture avoid unrelated awakening waits. The existing guardian actually traverses its baked route for the finite diversion, retains its hunt and resumes after expiry. A brief bare-Realtime master handoff checks checkpoint continuity; it does not simulate a second AI or rendered phone. Practice fixtures are excluded from permanent rewards; no production currency request or Android verification is claimed.";
            Status="Prepare trusted field and monster fixtures";
            yield return Wait(()=>session.hunting!=null&&session.hunting.Initialized&&session.hunting.Agent!=null&&session.hunting.health!=null,
                "The reloaded online scene initializes its existing guardian",12);
            var owner=session.fieldEvents;var monster=session.hunting;int host=PhotonNetwork.LocalPlayer.ActorNumber;
            if(owner==null||owner.beacons.Length!=4||owner.hazards.Length!=8)throw new InvalidOperationException("Saved field-event owner and all authored sites are required.");
            bool oldDisturbances=owner.disturbancesEnabled,oldOwnerEnabled=owner.enabled,oldDefeat=session.suppressDefeatForVerification;
            bool oldWorld=session.worldProgression!=null&&session.worldProgression.suppressThreatsForVerification;
            Vector3 oldPosition=Player.LocalInstance.transform.position;Quaternion oldRotation=Player.LocalInstance.transform.rotation;
            string oldFields=JsonUtility.ToJson(session.State.fieldEvents);
            var oldMonster=JsonUtility.FromJson<KhezuSnapshot>(JsonUtility.ToJson(monster.GetSnapshot()));
            fieldEventsCleanup=()=>
            {
                if(monster!=null&&Application.isPlaying)
                {
                    monster.CancelBeaconDiversion();monster.HoldTranquilized(false);monster.Request(KhezuCommand.Sleep);
                    monster.ApplySnapshot(oldMonster,true);monster.Request(KhezuCommand.Sleep);
                }
                if(owner!=null){owner.disturbancesEnabled=oldDisturbances;owner.enabled=false;owner.enabled=oldOwnerEnabled;}
                if(session!=null)
                {
                    session.suppressDefeatForVerification=oldDefeat;
                    if(session.worldProgression!=null)session.worldProgression.suppressThreatsForVerification=oldWorld;
                    if(session.Ready&&session.State!=null)
                    {
                        session.State.fieldEvents=JsonUtility.FromJson<ArchiveFieldEventState>(oldFields);
                        if(session.IsAuthority)CommitLearningFixture();
                    }
                }
                var player=Player.LocalInstance;
                if(player!=null&&Application.isPlaying&&player.gameObject.scene.path=="Assets/Scenes/MultiplayerGameplay.unity")
                {
                    var capsule=player.GetComponent<CharacterController>();bool enabled=capsule!=null&&capsule.enabled;
                    if(enabled)capsule.enabled=false;Vector3 delta=oldPosition-player.transform.position;
                    player.transform.SetPositionAndRotation(oldPosition,oldRotation);player.ResetMotionAfterTeleport();if(enabled)capsule.enabled=true;
                    TeleportCameraSync.ResetAfterMove(player.transform,delta);
                }
            };
            session.suppressDefeatForVerification=true;
            if(session.worldProgression!=null)session.worldProgression.suppressThreatsForVerification=true;
            owner.disturbancesEnabled=false;monster.Request(KhezuCommand.Sleep);
            session.MarkRewardsAdminUsed();
            var filter=new NavMeshQueryFilter{agentTypeID=monster.Agent.agentTypeID,areaMask=monster.Agent.areaMask};
            ArchiveResonanceBeaconView beacon=null;Vector3 origin=Vector3.zero,hostFeet=Vector3.zero;
            foreach(var candidate in owner.beacons.OrderBy(view=>view.id))
            {
                Vector3 inward=-new Vector3(candidate.transform.position.x,0,candidate.transform.position.z).normalized;
                if(!NavMesh.SamplePosition(candidate.transform.position,out var destination,3,filter))continue;
                foreach(float distance in new[]{25f,30f,20f})
                {
                    if(!NavMesh.SamplePosition(candidate.transform.position+inward*distance,out var start,4,filter)||
                        !NavMesh.SamplePosition(start.position+inward*75,out var target,8,filter))continue;
                    var lureRoute=new NavMeshPath();var huntRoute=new NavMeshPath();
                    if(!NavMesh.CalculatePath(start.position,destination.position,filter,lureRoute)||lureRoute.status!=NavMeshPathStatus.PathComplete||
                        !NavMesh.CalculatePath(start.position,target.position,filter,huntRoute)||huntRoute.status!=NavMeshPathStatus.PathComplete||
                        ArchiveFieldEvents.Horizontal(start.position,target.position)<65)continue;
                    beacon=candidate;origin=start.position;hostFeet=target.position;break;
                }
                if(beacon!=null)break;
            }
            Check(beacon!=null,"Trusted fixture uses real baked routes with guardian near the beacon and original target over 65m away");
            FieldMoveHost(hostFeet);yield return FieldMoveGuest(guest,beacon.transform.position+beacon.transform.right*2);
            var state=session.State.fieldEvents;var zone=owner.hazards[0];
            state.serial++;state.site=zone.id;state.kind=1;state.warnedAt=ArchiveTopicSession.Clock;
            state.activeAt=state.warnedAt+ArchiveFieldRules.WarningSeconds;state.endsAt=state.activeAt+ArchiveFieldRules.ActiveSeconds;
            double warnedAt=state.warnedAt,activeAt=state.activeAt,zoneEnds=state.endsAt;
            CommitLearningFixture();
            var stale=FieldBeaconCommand(guest,beacon.id);stale.matchId="field-previous-match";
            yield return FieldReceipt(guest,stale);
            var awake=monster.GetSnapshot();awake.state=(int)KhezuHuntState.Hunting;awake.awake=true;awake.pending=false;
            awake.mistakes=awake.stage=4;awake.huntSerial++;awake.target=host;awake.caught=0;awake.position=origin;
            awake.animation="Chase";awake.animationSpeed=1;awake.counterPhase=0;awake.rescueActor=0;awake.resolvedOutcome=0;
            awake.personalityPhase=0;awake.personalityStartedAt=awake.personalityCooldownUntil=0;
            ApplyFieldHunt(monster,awake);
            Check(!guest.receipts[stale.sequence].accepted&&session.CanUseBeacon(guest.Actor,beacon.id,out _)&&
                ArchiveFieldEvents.Horizontal(ArchiveFieldEvents.Feet(session.ActorPlayer(guest.Actor)),beacon.transform.position)<owner.interactionRange,
                "The genuine remote avatar is in range while a stale-match activation remains rejected");
            var badSerial=FieldBeaconCommand(guest,beacon.id);badSerial.b++;
            yield return FieldReceipt(guest,badSerial);
            Check(!guest.receipts[badSerial.sequence].accepted&&state.activeBeacon<0,"A fresh guest command cannot invent the beacon activation serial");
            ApplyFieldHunt(monster,awake);
            string health=FieldHealth(monster),topics=FieldTopics(),rewards=JsonUtility.ToJson(session.State.rewards);
            int mistakes=session.State.mistakes.Count,solved=session.State.solved.Count;double deadline=session.State.matchTime.deadline;
            int[] rotation=monster.HuntedPlayerIds.OrderBy(actor=>actor).ToArray();
            var activation=FieldBeaconCommand(guest,beacon.id);yield return FieldReceipt(guest,activation,true);
            yield return Wait(()=>guest.state?.fieldEvents?.activeBeacon==beacon.id,"Guest receives accepted absolute beacon receipt",8);
            report.acceptedRemoteCommands++;
            state=session.State.fieldEvents;double started=state.beaconStartedAt,expires=state.beaconEndsAt,cooldown=state.globalBeaconCooldownUntil;
            int serial=state.beacons.Single(receipt=>receipt.id==beacon.id).serial;
            Check(guest.receipts[activation.sequence].accepted&&state.beaconActor==guest.Actor&&state.beaconMonster==0&&
                state.beaconTarget==host&&state.beaconHuntSerial==monster.HuntSerial,
                "Host authenticates the guest sender despite a forged actor field and retains the existing hunt identity");
            Check(Math.Abs(expires-started-ArchiveFieldRules.BeaconSeconds)<.002&&Math.Abs(cooldown-started-ArchiveFieldRules.BeaconCooldownSeconds)<.002&&
                Math.Abs(guest.state.fieldEvents.beaconStartedAt-started)<.002&&Math.Abs(guest.state.fieldEvents.beaconEndsAt-expires)<.002&&
                Math.Abs(guest.state.fieldEvents.globalBeaconCooldownUntil-cooldown)<.002,
                "Guest receives the same absolute activation, finite expiry and global recharge deadline");
            yield return FieldReceipt(guest,activation);
            state=session.State.fieldEvents;
            Check(state.beaconEndsAt==expires&&state.globalBeaconCooldownUntil==cooldown&&state.beacons.Single(receipt=>receipt.id==beacon.id).serial==serial,
                "An exact packet replay returns its cached receipt without extending or charging the beacon twice");
            var forged=JsonUtility.FromJson<ArchiveTeamState>(JsonUtility.ToJson(session.State));forged.revision+=1000;
            forged.fieldEvents.activeBeacon=-1;forged.fieldEvents.beaconEndsAt+=9999;forged.fieldEvents.globalBeaconCooldownUntil=0;forged.fieldEvents.warnedAt+=99;
            guest.client.OpRaiseEvent(ArchiveTopicSession.SnapshotEvent,new object[]{ArchiveTopicSession.Channel,JsonUtility.ToJson(forged)},
                new RaiseEventOptions{Receivers=ReceiverGroup.All},SendOptions.SendReliable);yield return .2f;
            Check(session.State.fieldEvents.beaconEndsAt==expires&&session.State.fieldEvents.globalBeaconCooldownUntil==cooldown&&
                Math.Abs(guest.state.fieldEvents.beaconEndsAt-expires)<.002&&Math.Abs(guest.state.fieldEvents.warnedAt-warnedAt)<.002,
                "Guest-authored snapshots cannot replace diversion, cooldown or warning timestamps on either peer");
            yield return Wait(()=>monster.IsBeaconDiverted&&monster.Agent.enabled&&monster.Agent.hasPath,"Actual guardian follows the beacon after its audible warning",3);
            yield return .35f;
            Check(monster.Agent.pathStatus==NavMeshPathStatus.PathComplete&&ArchiveFieldEvents.Horizontal(monster.Agent.destination,beacon.transform.position)<4&&
                Vector3.Distance(monster.transform.position,origin)>.4f,
                "The existing guardian physically moves along its complete beacon route without teleportation");
            Check(monster.TargetPlayerId==host&&monster.HuntedPlayerIds.OrderBy(actor=>actor).SequenceEqual(rotation),
                "Active diversion keeps the original target and hunt rotation");

            Status="Brief master handoff preserves absolute field clocks";
            var sync=monster.GetComponent<KhezuPhotonSync>();
            typeof(KhezuPhotonSync).GetMethod("Changed",FieldPrivate).Invoke(sync,null);
            yield return Wait(()=>FieldCheckpointHunt(sync,awake.huntSerial,host),"Trusted hunt identity reaches its original room checkpoint",5);
            PhotonNetwork.SetMasterClient(PhotonNetwork.CurrentRoom.GetPlayer(guest.Actor));
            yield return Wait(()=>!PhotonNetwork.IsMasterClient&&guest.client.CurrentRoom.MasterClientId==guest.Actor,"Realtime guest briefly owns the room",5);
            guest.client.CurrentRoom.SetMasterClient(guest.client.CurrentRoom.GetPlayer(host));
            yield return Wait(()=>PhotonNetwork.IsMasterClient,"Rendered host regains field authority",5);
            yield return Wait(()=>Math.Abs((guest.state?.fieldEvents?.beaconEndsAt??-9999)-expires)<.002&&Math.Abs(session.State.fieldEvents.beaconEndsAt-expires)<.002,
                "Restored field checkpoint reaches the guest",5);
            state=session.State.fieldEvents;
            Check(Math.Abs(state.beaconStartedAt-started)<.002&&Math.Abs(state.beaconEndsAt-expires)<.002&&Math.Abs(state.globalBeaconCooldownUntil-cooldown)<.002&&
                Math.Abs(state.warnedAt-warnedAt)<.002&&Math.Abs(state.activeAt-activeAt)<.002&&Math.Abs(state.endsAt-zoneEnds)<.002,
                "Authority handoff preserves both original timelines and never restarts their durations");
            yield return Wait(()=>monster.IsBeaconDiverted&&monster.TargetPlayerId==host&&monster.Agent.enabled&&monster.Agent.isOnNavMesh&&
                monster.Agent.hasPath&&!monster.Agent.pathPending&&ArchiveFieldEvents.Horizontal(monster.Agent.destination,beacon.transform.position)<4,
                "Original guardian resumes the remaining diversion after handoff",3);
            Check(monster.HuntSerial==awake.huntSerial&&monster.HuntedPlayerIds.OrderBy(actor=>actor).SequenceEqual(rotation)&&ArchiveTopicSession.Clock<expires,
                "The same hunt resumes the remaining live route after the bare-Realtime handoff");

            Status="Observe real expiry and original pursuit resumption";
            yield return Wait(()=>ArchiveTopicSession.Clock>=expires+.12&&session.State.fieldEvents.activeBeacon<0&&guest.state?.fieldEvents?.activeBeacon<0,
                "Actual six-and-a-half-second expiry is checkpointed to the guest",9);
            state=session.State.fieldEvents;
            Check(!monster.IsBeaconDiverted&&Math.Abs(state.beaconEndsAt-expires)<.002&&Math.Abs(state.globalBeaconCooldownUntil-cooldown)<.002&&
                Math.Abs(guest.state.fieldEvents.beaconEndsAt-expires)<.002&&Math.Abs(guest.state.fieldEvents.globalBeaconCooldownUntil-cooldown)<.002,
                "Both peers end the finite diversion at its original deadline and retain its recharge");
            Vector3 resumedFrom=monster.transform.position;yield return .65f;
            Check(monster.TargetPlayerId==host&&monster.HuntSerial==awake.huntSerial&&monster.State==KhezuHuntState.Hunting&&
                monster.Agent.enabled&&monster.Agent.hasPath&&!monster.IsBeaconDiverted&&Vector3.Distance(monster.transform.position,resumedFrom)>.4f,
                "After expiry the same guardian physically resumes chasing its original distant target");
            var other=owner.beacons.First(view=>view.id!=beacon.id);yield return FieldMoveGuest(guest,other.transform.position+other.transform.right*2);
            var chained=FieldBeaconCommand(guest,other.id);yield return FieldReceipt(guest,chained);
            Check(!guest.receipts[chained.sequence].accepted&&guest.receipts[chained.sequence].message.Contains("Recharging")&&
                session.State.fieldEvents.beacons.Single(receipt=>receipt.id==other.id).serial==0&&Math.Abs(session.State.fieldEvents.globalBeaconCooldownUntil-cooldown)<.002,
                "Global cooldown rejects a nearby guest at a different unused beacon after the first diversion expires");
            state=session.State.fieldEvents;
            Check(FieldHealth(monster)==health&&FieldTopics()==topics&&session.State.mistakes.Count==mistakes&&session.State.solved.Count==solved&&
                session.State.matchTime.deadline==deadline&&JsonUtility.ToJson(session.State.rewards)==rewards&&
                Math.Abs(state.warnedAt-warnedAt)<.002&&Math.Abs(state.activeAt-activeAt)<.002&&Math.Abs(state.endsAt-zoneEnds)<.002&&
                ArchiveFieldRules.Phase(state,ArchiveTopicSession.Clock)==ArchiveFieldRules.Phase(guest.state.fieldEvents,ArchiveTopicSession.Clock),
                "Field events preserve hearts, mistakes, academic progress, rewards, match time and the same checkpointed disturbance phase");
            fieldEventsCleanup();fieldEventsCleanup=null;
        }
    }
}
