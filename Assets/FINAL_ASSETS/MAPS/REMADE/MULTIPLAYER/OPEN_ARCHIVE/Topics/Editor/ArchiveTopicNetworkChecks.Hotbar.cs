using System;
using System.Collections;
using System.IO;
using System.Linq;
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
        static bool hotbarOnly;
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test Hotbar Equip (Live Photon)")]
        public static void BeginHotbar(){StartChecks(false,hotbar:true);}

        static ArchiveCommand HotbarCommand(Peer peer,ArchiveAction action,int item,int target=0)
        {
            var command=SurvivalCommand(peer,0,action,item,target);
            return command;
        }
        static bool HotbarSnapshotMatches(Peer peer,int actor,int equipped,int[] counts)
        {
            var inventory=peer.state?.survival?.players?.FirstOrDefault(p=>p.actor==actor);
            return inventory!=null&&inventory.equippedItem==equipped&&inventory.items.SequenceEqual(counts);
        }
        static Action hotbarDartCleanup;
        static Vector3 HotbarShotOrigin(Player player)
        {
            var capsule=player.GetComponent<CharacterController>();
            return capsule.bounds.center+Vector3.up*capsule.bounds.extents.y*.45f;
        }
        static KhezuHuntingController HotbarRayTarget(Player player,Vector3 origin,Vector3 direction)
        {
            foreach(var hit in Physics.RaycastAll(origin,direction,ArchiveTopicSession.TranquilizerRange,~0,QueryTriggerInteraction.Collide).OrderBy(h=>h.distance))
            {
                if(hit.collider.transform.IsChildOf(player.transform))continue;
                var monster=hit.collider.GetComponentInParent<KhezuHuntingController>();
                if(hit.collider.isTrigger&&monster==null)continue;
                return monster;
            }
            return null;
        }
        static IEnumerator HotbarMove(Peer peer,Vector3 position,Quaternion rotation)
        {
            peer.client.OpRaiseEvent(201,new object[]{PhotonNetwork.ServerTimestamp,null,new object[]{peer.viewId,false,null,position,rotation,false,false,false,0f}},
                new RaiseEventOptions{Receivers=ReceiverGroup.Others},SendOptions.SendReliable);
            yield return Wait(()=>session.ActorPlayer(peer.Actor)!=null&&Vector3.Distance(session.ActorPlayer(peer.Actor).transform.position,position)<.25f&&
                Quaternion.Angle(session.ActorPlayer(peer.Actor).transform.rotation,rotation)<2,"Remote shooter reaches the clear-ray fixture and faces the monster",8);
            yield return .1f;
        }
        static bool HotbarSleepCheckpoint(Peer peer,KhezuHuntingController monster,Vector3 anchor,Quaternion rotation)
        {
            var sync=monster.GetComponent<KhezuPhotonSync>();
            if(sync==null||!peer.client.CurrentRoom.CustomProperties.TryGetValue(sync.StateKey,out var value)||!(value is object[] packet)||
                packet.Length!=4||!Equals(packet[0],sync.SyncChannel)||!Equals(packet[1],PhotonNetwork.LocalPlayer.ActorNumber)||!(packet[3] is string json))return false;
            var snapshot=JsonUtility.FromJson<KhezuSnapshot>(json);
            return snapshot!=null&&snapshot.animation=="Sleep"&&Vector3.Distance(snapshot.position,anchor)<.03f&&Quaternion.Angle(snapshot.rotation,rotation)<.5f;
        }
        static IEnumerator HotbarDartHit(Peer peer)
        {
            Status="Remote accepted dart hit, rendered host FX and sleeping checkpoint";
            var monster=session.hunting;var player=session.ActorPlayer(peer.Actor);var capsule=player.GetComponent<CharacterController>();
            var saved=monster.GetSnapshot();var oldPosition=player.transform.position;var oldRotation=player.transform.rotation;
            var oldEffect=session.State.survival.sedations.FirstOrDefault(s=>s.encounterId==monster.encounterId);
            if(oldEffect!=null)oldEffect=JsonUtility.FromJson<ArchiveMonsterSedation>(JsonUtility.ToJson(oldEffect));
            hotbarDartCleanup=()=>
            {
                if(session==null||monster==null||!Application.isPlaying)return;
                session.State.survival.sedations.RemoveAll(s=>s.encounterId==monster.encounterId);
                if(oldEffect!=null)session.State.survival.sedations.Add(oldEffect);
                monster.HoldTranquilized(false);monster.ApplySnapshot(saved,true);monster.Request(KhezuCommand.Sleep);SurvivalCommit();
            };
            session.State.survival.sedations.RemoveAll(s=>s.encounterId==monster.encounterId);
            monster.HoldTranquilized(false);
            var awake=monster.GetSnapshot();awake.state=(int)KhezuHuntState.Ready;awake.target=awake.caught=0;awake.pending=false;awake.awake=true;
            awake.mistakes=awake.stage=4;awake.animation="Idle";awake.animationSpeed=1;monster.ApplySnapshot(awake,true);
            Physics.SyncTransforms();
            var body=monster.GetComponent<BoxCollider>();var filter=new NavMeshQueryFilter{agentTypeID=monster.Agent.agentTypeID,areaMask=monster.Agent.areaMask};
            float footOffset=player.transform.position.y-LogicLegends.OpenArchive.Water.OpenArchiveWater.Feet(capsule).y;
            bool found=false;var position=Vector3.zero;var rotation=Quaternion.identity;
            foreach(float radius in new[]{18f,24f,14f})
            {
                for(int i=0;i<32&&!found;i++)
                {
                    float angle=i*Mathf.PI/16;var desired=monster.transform.position+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius;
                    if(!NavMesh.SamplePosition(desired,out var nav,3,filter))continue;
                    var candidate=nav.position+Vector3.up*(footOffset+.05f);
                    var facing=Quaternion.LookRotation(Vector3.ProjectOnPlane(monster.transform.position-candidate,Vector3.up),Vector3.up);
                    var origin=candidate+facing*Vector3.Scale(capsule.center,player.transform.lossyScale)+Vector3.up*capsule.bounds.extents.y*.45f;
                    var direction=body.bounds.center-origin;
                    if(direction.magnitude>30||direction.sqrMagnitude<1||HotbarRayTarget(player,origin,direction.normalized)!=monster)continue;
                    position=candidate;rotation=facing;found=true;
                }
                if(found)break;
            }
            Check(monster.CanTranquilize&&found,"Remote dart fixture uses a clear physical actor-to-monster ray on the existing NavMesh");
            yield return HotbarMove(peer,position,rotation);Physics.SyncTransforms();
            var actualOrigin=HotbarShotOrigin(player);var aim=(body.bounds.center-actualOrigin).normalized;
            Check(HotbarRayTarget(player,actualOrigin,aim)==monster,"The received guest capsule has an unobstructed production-origin ray to the real monster collider");
            yield return Wait(()=>ArchiveTopicSession.Clock>=session.Inventory(peer.Actor).nextUseAt,"Guest item cooldown settles before its actual dart command",3);
            int ammo=session.CountItem(peer.Actor,ArchiveSurvivalItem.TranquilizerAmmo),serial=session.State.survival.shotSerial;
            var anchor=monster.transform.position;var anchorRotation=monster.transform.rotation;
            var shot=HotbarCommand(peer,ArchiveAction.UseSurvivalItem,(int)ArchiveSurvivalItem.Tranquilizer);shot.aimDirection=aim;
            yield return SurvivalSend(peer,shot,true,"Authenticated guest fires an actual accepted tranquilizer hit");
            Check(session.CountItem(peer.Actor,ArchiveSurvivalItem.TranquilizerAmmo)==ammo-1&&session.State.survival.shotSerial==serial+1&&
                session.State.survival.shotActor==peer.Actor&&session.State.survival.shotHit&&Vector3.Distance(session.State.survival.shotOrigin,actualOrigin)<.03f,
                "One accepted remote shot spends exactly one dart and records its real sender, server origin and sedating hit");
            yield return Wait(()=>peer.state?.survival!=null&&peer.state.survival.shotSerial==serial+1&&peer.state.survival.shotActor==peer.Actor&&
                peer.state.survival.shotHit&&peer.state.survival.players.First(p=>p.actor==peer.Actor).items[(int)ArchiveSurvivalItem.TranquilizerAmmo]==ammo-1,
                "Accepted dart and exact ammunition checkpoint reach its remote owner",8);
            yield return Wait(()=>session.GetComponent<ArchiveTranquilizerVfx>()!=null&&session.GetComponent<ArchiveTranquilizerVfx>().LastVisualSerial==serial+1,
                "Rendered host observes the accepted remote shot once in its pooled dart FX",3);
            var vfx=session.GetComponent<ArchiveTranquilizerVfx>();
            Check(vfx.LastVisualHit&&Vector3.Distance(vfx.LastVisualEnd,session.State.survival.shotEnd)<.01f&&vfx.PoolCapacity==4,
                "Remote dart FX uses the validated hit endpoint and bounded four-dart pool");
            yield return .65f;
            Check(monster.IsTranquilized&&monster.IsTranquilizedSleepPose&&monster.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Sleep")&&
                monster.GetSnapshot().animation=="Sleep"&&Vector3.Distance(monster.transform.position,anchor)<.03f&&Quaternion.Angle(monster.transform.rotation,anchorRotation)<.5f,
                "The remote hit settles the real monster into Sleep at its hit location without a nest teleport or rotation snap");
            yield return Wait(()=>HotbarSleepCheckpoint(peer,monster,anchor,anchorRotation),"Authenticated sleeping monster checkpoint reaches the remote guest",5);
            Packet(peer,shot);yield return .2f;
            Check(session.CountItem(peer.Actor,ArchiveSurvivalItem.TranquilizerAmmo)==ammo-1&&session.State.survival.shotSerial==serial+1&&vfx.LastVisualSerial==serial+1,
                "Replayed remote Fire cannot consume another dart or create another shot or FX serial");
            hotbarDartCleanup();hotbarDartCleanup=null;
            yield return HotbarMove(peer,oldPosition,oldRotation);
            Check(!monster.IsTranquilized&&monster.State==KhezuHuntState.Sleeping,
                "Dart fixture clears sedation and restores the original sleeping encounter before late-join and host-transfer checks");
        }
        static IEnumerator HotbarFlow(Peer a,string room)
        {
            report.scope="Live invisible private Photon room: rendered Editor host and independent Realtime peers. Explicit host-authorized supply grants and a confirmed chest fixture exercise equip/unequip authority, sender binding, inventory preservation, actual final-copy medkit use, remote accepted physical dart hit, pooled host FX, stationary real Sleep and its remote monster checkpoint, replay protection, late join and master transfer. The rendered host also checks the guest avatar hand model; native mobile-style UI is tested separately. No physical Android or second rendered client is verified.";
            session.suppressDefeatForVerification=true;
            if(session.worldProgression!=null)session.worldProgression.suppressThreatsForVerification=true;
            session.hunting.Request(KhezuCommand.Sleep);
            int host=PhotonNetwork.LocalPlayer.ActorNumber;

            Status="Unowned equip rejection and chest storage";
            yield return SurvivalSend(a,HotbarCommand(a,ArchiveAction.EquipSurvivalItem,(int)ArchiveSurvivalItem.Flashlight),false,
                "A guest cannot equip an unowned tool");
            Check(session.EquippedItem(a.Actor)==-1&&session.CountItem(a.Actor,ArchiveSurvivalItem.Flashlight)==0,
                "Rejected equip creates no item or hand selection");

            // The chest is an explicit confirmed-completion fixture, not invented academic progress.
            session.State.stations[0].phase=2;session.State.stations[0].round=ArchiveTopicCatalog.RoundCount(0);
            session.State.survival.chests.Add(new ArchiveTopicChest{topic=0,owner=a.Actor,item=ArchiveSurvivalItem.Medkit,completedAt=ArchiveTopicSession.Clock-3});
            SurvivalCommit();
            yield return Move(a,session.stations[0],ArchiveRewardView.ChestLocalPosition+Vector3.right);
            yield return SurvivalSend(a,HotbarCommand(a,ArchiveAction.ClaimChest,-1),true,"Chest claim stores its actual item in the sender inventory");
            Check(session.CountItem(a.Actor,ArchiveSurvivalItem.Medkit)==1&&session.EquippedItem(a.Actor)==-1,
                "Collecting a chest item does not consume or automatically equip it");

            Status="Actual equip commands preserve inventories";
            Check(session.RequestAdminTest(ArchiveAdminTestAction.GrantAllItems,a.Actor),
                "Authorized host creates the documented all-supplies fixture for the real guest");
            var initial=(int[])session.Inventory(a.Actor).items.Clone();
            int[] hostBefore=session.Inventory(host)?.items?.ToArray()??new int[6];
            yield return SurvivalSend(a,HotbarCommand(a,ArchiveAction.EquipSurvivalItem,(int)ArchiveSurvivalItem.Flashlight),true,
                "Authenticated guest equips the owned flashlight");
            Check(session.EquippedItem(a.Actor)==(int)ArchiveSurvivalItem.Flashlight&&session.Inventory(a.Actor).items.SequenceEqual(initial),
                "Selecting a slot equips one item without consuming any supplies");
            yield return Wait(()=>HotbarSnapshotMatches(a,a.Actor,(int)ArchiveSurvivalItem.Flashlight,initial),"Equipped selection reaches its remote owner checkpoint");
            var hands=session.GetComponent<ArchiveEquippedItems>();
            yield return Wait(()=>hands!=null&&hands.VisibleItem(a.Actor)==(int)ArchiveSurvivalItem.Flashlight,"Rendered remote avatar displays checkpointed flashlight",8);
            Check(hands.EquippedModel(a.Actor).GetComponentsInChildren<Renderer>().Any(r=>r.enabled),
                "The rendered host sees the guest-owned 3D tool on the remote avatar");
            yield return SurvivalSend(a,HotbarCommand(a,ArchiveAction.UseSurvivalItem,(int)ArchiveSurvivalItem.Flashlight),true,
                "Guest flashlight toggle uses the existing authenticated item command");
            yield return Wait(()=>UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Any(l=>
                l.name=="Actor_"+a.Actor+"_Flashlight"&&l.enabled&&l.GetComponent<ArchiveFlashlightVfx>()!=null&&
                l.GetComponent<ArchiveFlashlightVfx>().BeamRenderer!=null&&l.GetComponent<ArchiveFlashlightVfx>().BeamRenderer.enabled),
                "Rendered host receives the actual guest spotlight and beam",8);
            Check(session.Inventory(a.Actor).flashlightOn&&session.Inventory(a.Actor).items.SequenceEqual(initial),
                "Remote flashlight illumination and beam appear without consuming inventory stacks");
            yield return .5f;
            yield return SurvivalSend(a,HotbarCommand(a,ArchiveAction.UseSurvivalItem,(int)ArchiveSurvivalItem.Flashlight),true,
                "Guest turns off its existing flashlight through the same owner-validated command");


            var same=HotbarCommand(a,ArchiveAction.EquipSurvivalItem,(int)ArchiveSurvivalItem.Flashlight);
            yield return SurvivalSend(a,same,true,"A repeated explicit selection remains idempotently equipped");
            Packet(a,same);yield return .2f;
            Check(session.EquippedItem(a.Actor)==(int)ArchiveSurvivalItem.Flashlight&&session.Inventory(a.Actor).items.SequenceEqual(initial),
                "Duplicate reliable equip packets never toggle the hand or consume an item");
            yield return SurvivalSend(a,HotbarCommand(a,ArchiveAction.EquipSurvivalItem,(int)ArchiveSurvivalItem.Tranquilizer),true,
                "Selecting another owned slot switches the single equipped item");
            Check(session.EquippedItem(a.Actor)==(int)ArchiveSurvivalItem.Tranquilizer&&session.Inventory(a.Actor).items.SequenceEqual(initial),
                "Switching to the gun does not fire or spend ammunition");
            yield return SurvivalSend(a,HotbarCommand(a,ArchiveAction.EquipSurvivalItem,-1),true,
                "Explicit empty-hand selection unequips without dropping inventory");
            Check(session.EquippedItem(a.Actor)==-1&&session.Inventory(a.Actor).items.SequenceEqual(initial),
                "Unequip leaves every stack intact");
            yield return SurvivalSend(a,HotbarCommand(a,ArchiveAction.EquipSurvivalItem,6),false,"Out-of-range positive equip index is rejected");
            yield return SurvivalSend(a,HotbarCommand(a,ArchiveAction.EquipSurvivalItem,-2),false,"An invalid negative equip index is rejected");

            var spoof=HotbarCommand(a,ArchiveAction.EquipSurvivalItem,(int)ArchiveSurvivalItem.Battery,host);
            Packet(a,spoof,true);yield return Wait(()=>a.receipts.ContainsKey(spoof.sequence),"Spoofed actor equip receives an authenticated response",8);
            Check(a.receipts[spoof.sequence].accepted&&session.EquippedItem(a.Actor)==(int)ArchiveSurvivalItem.Battery&&session.EquippedItem(host)==-1,
                "Forged actor and target fields cannot equip another player's hand");
            report.acceptedRemoteCommands++;
            Check((session.Inventory(host)?.items??new int[6]).SequenceEqual(hostBefore),"Guest selection cannot change host inventory quantities");

            var protection=new GameObject("Hotbar_Restriction_QA");
            session.ActorPlayer(a.Actor).AcquireMovementLock(protection,"Equip rejection fixture",session.ActorPlayer(a.Actor).transform.position,150);
            yield return SurvivalSend(a,HotbarCommand(a,ArchiveAction.EquipSurvivalItem,(int)ArchiveSurvivalItem.Medkit),false,
                "Movement-locked guest cannot change equipped items during a capture-style restriction");
            session.ActorPlayer(a.Actor).ReleaseMovementLock(protection);UnityEngine.Object.Destroy(protection);
            Check(session.EquippedItem(a.Actor)==(int)ArchiveSurvivalItem.Battery,"Rejected restricted selection preserves the previous valid hand item");

            Status="Consume only through actual Use command";
            var supplies=session.Inventory(a.Actor);supplies.items[(int)ArchiveSurvivalItem.Medkit]=1;
            session.hunting.health.RegisterActors(new[]{host,a.Actor});
            Check(session.hunting.health.DamageAuthoritativeActor(a.Actor),"Fixture removes one real guest heart through the original health owner");
            session.hunting.PublishRescueHealth();SurvivalCommit();
            yield return SurvivalSend(a,HotbarCommand(a,ArchiveAction.EquipSurvivalItem,(int)ArchiveSurvivalItem.Medkit),true,
                "The final medkit copy can be equipped without healing on selection");
            Check(session.hunting.health.ActorHearts(a.Actor)==session.hunting.health.MaximumHealth-1&&session.CountItem(a.Actor,ArchiveSurvivalItem.Medkit)==1,
                "Tap-to-equip does not heal or consume the medkit");
            var use=HotbarCommand(a,ArchiveAction.UseSurvivalItem,(int)ArchiveSurvivalItem.Medkit);
            yield return SurvivalSend(a,use,true,"Separate Use command consumes the equipped medkit through authoritative validation");
            Check(session.hunting.health.ActorHearts(a.Actor)==session.hunting.health.MaximumHealth&&session.CountItem(a.Actor,ArchiveSurvivalItem.Medkit)==0&&session.EquippedItem(a.Actor)==-1,
                "Final-copy use restores exactly one heart and automatically clears the empty hand selection");
            Packet(a,use);yield return .2f;
            Check(session.CountItem(a.Actor,ArchiveSurvivalItem.Medkit)==0&&session.hunting.health.ActorHearts(a.Actor)==session.hunting.health.MaximumHealth,
                "A replayed Use command cannot consume or heal a second time");

            Status="Checkpoint authority, late join and host transfer";
            yield return SurvivalSend(a,HotbarCommand(a,ArchiveAction.EquipSurvivalItem,(int)ArchiveSurvivalItem.Tranquilizer),true,
                "Guest re-equips its existing gun before checkpoint tests");
            yield return HotbarDartHit(a);
            yield return SharingFlow(a,host);
            Status="Checkpoint authority, late join and host transfer";
            var finalCounts=session.Inventory(a.Actor).items.ToArray();
            var fake=JsonUtility.FromJson<ArchiveTeamState>(JsonUtility.ToJson(session.State));fake.revision+=10000;
            fake.survival.players.First(p=>p.actor==a.Actor).equippedItem=(int)ArchiveSurvivalItem.RevivalKit;
            fake.survival.players.First(p=>p.actor==a.Actor).items[(int)ArchiveSurvivalItem.Medkit]=99;
            a.client.OpRaiseEvent(ArchiveTopicSession.SnapshotEvent,new object[]{ArchiveTopicSession.Channel,JsonUtility.ToJson(fake)},new RaiseEventOptions{Receivers=ReceiverGroup.All},SendOptions.SendReliable);
            yield return .25f;
            Check(session.EquippedItem(a.Actor)==(int)ArchiveSurvivalItem.Tranquilizer&&session.Inventory(a.Actor).items.SequenceEqual(finalCounts),
                "Guest-forged equip and quantity snapshots cannot override the host checkpoint");
            var b=Connect("Hotbar late join QA");yield return Wait(()=>b.client.State==ClientState.ConnectedToMasterServer,"Late peer connects");
            b.client.OpJoinRoom(new EnterRoomParams{RoomName=room});yield return Wait(()=>b.client.InRoom&&b.state?.survival!=null,"Late peer receives item checkpoint");
            yield return Wait(()=>HotbarSnapshotMatches(b,a.Actor,(int)ArchiveSurvivalItem.Tranquilizer,finalCounts),"Late peer restores exact selected tool and stack counts");
            Check(HotbarSnapshotMatches(b,a.Actor,(int)ArchiveSurvivalItem.Tranquilizer,finalCounts),"Late join restores equipped item and remaining stacks without a fresh pickup");
            yield return Wait(()=>SharingSnapshotMatches(b),"Late join restores the recipient's transferred tools, dart and flashlight charge");
            PhotonNetwork.SetMasterClient(PhotonNetwork.CurrentRoom.GetPlayer(a.Actor));
            yield return Wait(()=>!PhotonNetwork.IsMasterClient&&a.client.CurrentRoom.MasterClientId==a.Actor,"Guest temporarily becomes room authority");
            a.client.CurrentRoom.SetMasterClient(a.client.CurrentRoom.GetPlayer(host));yield return Wait(()=>PhotonNetwork.IsMasterClient,"Rendered host resumes authority");yield return .35f;
            Check(session.EquippedItem(a.Actor)==(int)ArchiveSurvivalItem.Tranquilizer&&session.Inventory(a.Actor).items.SequenceEqual(finalCounts),
                "Host migration preserves selected tool and every remaining stack exactly");
            Check(SharingAuthorityRetained(),"Host migration preserves recipient transfers and exact flashlight charge");
            Check(session.State.mistakes.Count==0&&session.State.solved.Count==0,
                "Equipping and item test fixtures do not create academic answers or monster mistakes");
            session.suppressDefeatForVerification=false;
            if(session.worldProgression!=null)session.worldProgression.suppressThreatsForVerification=false;
            session.hunting.Request(KhezuCommand.Sleep);Status="Completed live hotbar checks";
        }
    }
}
