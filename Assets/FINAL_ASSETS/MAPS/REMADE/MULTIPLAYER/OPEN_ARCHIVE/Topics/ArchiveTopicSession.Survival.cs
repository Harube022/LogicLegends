using System;
using System.Linq;
using LogicLegends.OpenArchive.Hunting;
using Photon.Pun;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    public sealed partial class ArchiveTopicSession
    {
        public const float FlashlightCapacity = 240, BatteryRefill = 240, TranquilizerRange = 32;
        public const float TranquilizerSeconds = 8, TranquilizerImmunity = 20;
        float nextSurvivalTick;
        [NonSerialized] public bool suppressDefeatForVerification;
        bool returningAfterDefeat;
        public bool TeamDefeated=>Ready&&State.survival!=null&&State.survival.defeated;
        public float DefeatRemaining=>TeamDefeated?Mathf.Max(0,6-(float)(Clock-State.survival.defeatedAt)):6;
        void NormalizeSurvival()
        {
            if(State.survival==null)State.survival=new ArchiveSurvivalState();
            var data=State.survival;
            if(data.chests==null)data.chests=new System.Collections.Generic.List<ArchiveTopicChest>();
            if(data.players==null)data.players=new System.Collections.Generic.List<ArchiveSurvivalInventory>();
            if(data.sedations==null)data.sedations=new System.Collections.Generic.List<ArchiveMonsterSedation>();
            foreach(var inventory in data.players)
            {
                if(inventory.items==null||inventory.items.Length!=6)Array.Resize(ref inventory.items,6);
                inventory.batterySeconds=Mathf.Clamp(inventory.batterySeconds,0,FlashlightCapacity);
                if(inventory.equippedItem< -1||inventory.equippedItem>5||inventory.equippedItem>=0&&inventory.items[inventory.equippedItem]<=0)inventory.equippedItem=-1;
            }
            NormalizeRescue();
        }
        public ArchiveTopicChest Chest(int topic)=>Ready?State.survival?.chests?.FirstOrDefault(c=>c.topic==topic):null;
        public ArchiveSurvivalInventory Inventory(int actor)=>Ready?State.survival?.players?.FirstOrDefault(p=>p.actor==actor):null;
        public int CountItem(int actor,ArchiveSurvivalItem item)
        {var inventory=Inventory(actor);return inventory!=null&&inventory.items!=null&&(int)item>=0&&(int)item<inventory.items.Length?inventory.items[(int)item]:0;}
        ArchiveSurvivalInventory EnsureInventory(int actor)
        {
            var inventory=Inventory(actor);
            if(inventory==null){inventory=new ArchiveSurvivalInventory{actor=actor,batteryUpdatedAt=Clock};State.survival.players.Add(inventory);}
            return inventory;
        }
        void CreateTopicReward(ArchiveStationState station,int solver)
        {
            NormalizeSurvival();
            if(solver==0||station.phase!=2||Chest(station.topic)!=null)return;
            // The accepted final answer owns this single, persistent draw. Admin resets cannot reroll it.
            var random=new System.Random(unchecked(State.seed ^ ((station.topic+1)*73856093) ^ 0x51C4E57));
            State.survival.chests.Add(new ArchiveTopicChest{topic=station.topic,resetSerial=station.testResetSerial,
                owner=solver,item=(ArchiveSurvivalItem)random.Next(6),completedAt=Clock});
        }
        public bool RequestChest(int topic)
        {return SendSurvival(topic,ArchiveAction.ClaimChest,-1,0);}
        public bool CanClaimChest(int actor,int topic)
        {
            if(!Ready||topic<0||topic>=10||!CanAct(actor))return false;
            var chest=Chest(topic);
            return chest!=null&&!chest.claimed&&(chest.owner==actor||chest.orphaned)&&State.stations[topic].phase==2&&
                Clock-chest.completedAt>=1.35&&Near(actor,topic,ArchiveRewardView.ChestLocalPosition,3);
        }
        public bool RequestUseItem(ArchiveSurvivalItem item,int target=0)
        {return SendSurvival(0,ArchiveAction.UseSurvivalItem,(int)item,target);}
        public int EquippedItem(int actor)=>Inventory(actor)?.equippedItem??-1;
        public bool RequestEquipItem(int item)=>item>=-1&&item<6&&SendSurvival(0,ArchiveAction.EquipSurvivalItem,item,0);
        public bool RequestUseEquippedItem(int target=0)
        {int selected=EquippedItem(LocalActor);return selected>=0&&RequestUseItem((ArchiveSurvivalItem)selected,target);}
        bool SendSurvival(int topic,ArchiveAction action,int item,int target)
        {
            if(!Ready||!CanAct(LocalActor)||topic<0||topic>=10)return false;
            var record=State.actors.FirstOrDefault(a=>a.actor==LocalActor);
            localSequence=Math.Max(localSequence,record?.lastSequence??0);
            var player=ActorPlayer(LocalActor);
            var command=new ArchiveCommand{topic=topic,action=action,a=item,b=target,sequence=++localSequence,
                matchId=State.matchId,contextVersion=1,resetSerial=State.stations[topic].testResetSerial,
                aimDirection=action==ArchiveAction.UseSurvivalItem&&item==(int)ArchiveSurvivalItem.Tranquilizer?
                    LocalTranquilizerAim(player,Camera.main):Camera.main!=null?Camera.main.transform.forward:player.transform.forward};
            return DispatchCommand(command);
        }
        public static Vector3 LocalTranquilizerAim(Player player,Camera camera)
        {
            if(player==null)return Vector3.forward;
            var capsule=player.GetComponent<CharacterController>();
            var origin=capsule!=null?capsule.bounds.center+Vector3.up*capsule.bounds.extents.y*.45f:player.transform.position+Vector3.up*1.5f;
            if(camera==null)return player.transform.forward;
            var ray=camera.ViewportPointToRay(new Vector3(.5f,.5f,0));
            float reach=TranquilizerRange+Vector3.Distance(camera.transform.position,origin);
            Vector3 target=ray.origin+ray.direction*reach;
            foreach(var hit in Physics.RaycastAll(ray,reach,~0,QueryTriggerInteraction.Collide).OrderBy(h=>h.distance))
            {
                if(hit.collider.transform.IsChildOf(player.transform))continue;
                if(hit.collider.isTrigger&&hit.collider.GetComponentInParent<KhezuHuntingController>()==null)continue;
                target=hit.point;break;
            }
            // Only a direction is sent. The authority still casts from the real actor capsule,
            // so a camera peeking around cover cannot invent a shot origin or bypass a wall.
            var direction=target-origin;
            return direction.sqrMagnitude>.0001f?direction.normalized:camera.transform.forward;
        }
        bool AcceptSurvival(int actor,ArchiveCommand command)
        {
            NormalizeSurvival();
            if(!CanAct(actor)||command.matchId!=State.matchId||command.contextVersion!=1||command.sequence<=0)return false;
            var record=State.actors.FirstOrDefault(a=>a.actor==actor);
            if(command.sequence<=(record?.lastSequence??0))return false;
            var inventory=EnsureInventory(actor);
            if(command.action==ArchiveAction.EquipSurvivalItem)
            {
                if(command.a< -1||command.a>5||command.a>=0&&inventory.items[command.a]<=0)return false;
                SettleBattery(inventory);
                if(command.a!=(int)ArchiveSurvivalItem.Flashlight)inventory.flashlightOn=false;
                inventory.equippedItem=command.a;
            }
            else if(command.action==ArchiveAction.ClaimChest)
            {
                var chest=Chest(command.topic);
                if(!CanClaimChest(actor,command.topic))
                {commandRejection="Only the final solver can claim this chest. Move beside it and use Claim.";return false;}
                chest.claimed=true;chest.claimedAt=Clock;GrantSurvivalItem(inventory,chest.item);
            }
            else if(command.action==ArchiveAction.GiveSurvivalItem)
            {
                if(!GiveSurvivalItem(actor,command.b,(ArchiveSurvivalItem)command.a,out commandRejection))return false;
            }
            else
            {
                if(command.a<0||command.a>=6||Clock<inventory.nextUseAt||inventory.items[command.a]<=0)return false;
                var item=(ArchiveSurvivalItem)command.a;
                bool teammate=command.b!=0&&command.b!=actor;
                if(teammate&&!CanUseItemOnTeammate(actor,command.b,item,out commandRejection))return false;
                SettleBattery(inventory);
                bool used=false;
                switch(item)
                {
                    case ArchiveSurvivalItem.Flashlight:
                        if(inventory.flashlightOn){inventory.flashlightOn=false;used=true;}
                        else if(inventory.batterySeconds>0){inventory.flashlightOn=true;used=true;}
                        else commandRejection="Flashlight is empty. Load a flashlight battery.";
                        break;
                    case ArchiveSurvivalItem.Battery:
                        var lightInventory=teammate?Inventory(command.b):inventory;
                        if(lightInventory==null||lightInventory.items[(int)ArchiveSurvivalItem.Flashlight]<=0){commandRejection="A flashlight is required before using a battery.";break;}
                        SettleBattery(lightInventory);
                        if(lightInventory.batterySeconds>=FlashlightCapacity-1){commandRejection="The flashlight battery is already full.";break;}
                        lightInventory.batterySeconds=Mathf.Min(FlashlightCapacity,lightInventory.batterySeconds+BatteryRefill);inventory.items[command.a]--;used=true;break;
                    case ArchiveSurvivalItem.Medkit:
                        if(hunting!=null&&hunting.health!=null&&hunting.health.RestoreAuthoritativeActorHeart(teammate?command.b:actor))
                        {inventory.items[command.a]--;used=true;PublishSurvivalHealth();}
                        else commandRejection="A medkit restores one missing heart. It cannot revive a downed player.";
                        break;
                    case ArchiveSurvivalItem.RevivalKit:
                        int target=command.b;
                        if(target==0){var player=ActorPlayer(actor);target=State.survival.downed.Where(d=>d.phase==2&&d.actor!=actor&&IsRevivalTargetAvailable(d.actor))
                            .OrderBy(d=>(d.anchor-player.transform.position).sqrMagnitude).FirstOrDefault()?.actor??0;}
                        if(target!=0&&TryReviveActor(actor,target)){inventory.items[command.a]--;used=true;PublishSurvivalHealth();}
                        else commandRejection="Bring the revival kit to your downed teammate at the Monster Nest.";
                        break;
                    case ArchiveSurvivalItem.Tranquilizer:
                        if(inventory.items[(int)ArchiveSurvivalItem.TranquilizerAmmo]<=0){commandRejection="No tranquilizer darts remaining.";break;}
                        if(FireTranquilizer(actor,command.aimDirection)){inventory.items[(int)ArchiveSurvivalItem.TranquilizerAmmo]--;used=true;}
                        break;
                    case ArchiveSurvivalItem.TranquilizerAmmo:
                        commandRejection="Darts are loaded automatically. Use the tranquilizer gun to fire.";break;
                }
                if(!used)return false;
                inventory.nextUseAt=Clock+.45;inventory.batteryUpdatedAt=Clock;
            }
            if(record==null){record=new ArchiveActor{actor=actor,displayName=ActorName(actor)};State.actors.Add(record);}
            record.lastSequence=command.sequence;Commit();return true;
        }
        void GrantSurvivalItem(ArchiveSurvivalInventory inventory,ArchiveSurvivalItem item)
        {
            // Equipment duplicates remain useful: a second torch refills charge; a second gun includes a dart.
            if(item==ArchiveSurvivalItem.Flashlight){inventory.items[(int)item]=1;inventory.batterySeconds=Mathf.Min(FlashlightCapacity,inventory.batterySeconds+FlashlightCapacity);}
            else if(item==ArchiveSurvivalItem.Tranquilizer){inventory.items[(int)item]=1;inventory.items[(int)ArchiveSurvivalItem.TranquilizerAmmo]++;}
            else inventory.items[(int)item]++;
        }
        void PublishSurvivalHealth()
        {foreach(var monster in KhezuHuntingController.ActiveMonsters)if(string.IsNullOrEmpty(monster.encounterId)){monster.PublishRescueHealth();break;}}
        public float FlashlightRemaining(int actor)
        {
            var inventory=Inventory(actor);if(inventory==null)return 0;
            return Mathf.Max(0,inventory.batterySeconds-(inventory.flashlightOn?(float)Math.Max(0,Clock-inventory.batteryUpdatedAt):0));
        }
        void TickSurvival()
        {
            NormalizeSurvival();
            // A new/admin-reset match can reuse this component after a previous defeat.
            if(!TeamDefeated)returningAfterDefeat=false;
            foreach(var monster in KhezuHuntingController.ActiveMonsters)
                monster.HoldTranquilized(State.survival.sedations.Any(s=>s.encounterId==monster.encounterId&&Clock<s.until));
            if(TeamDefeated&&!suppressDefeatForVerification&&DefeatRemaining<=0&&!returningAfterDefeat)
            {
                returningAfterDefeat=true;
                MultiplayerLobbyService.EnsureExists().ReturnToLobbyAfterMatch();
                return;
            }
            if(TeamDefeated)return;
            TickRescue();
            if(!IsAuthority||Time.timeScale<=0||Time.unscaledTime<nextSurvivalTick)return;
            nextSurvivalTick=Time.unscaledTime+1;bool changed=false;
            if(!suppressDefeatForVerification&&!TeamWon&&!TeamDefeated&&hunting?.health!=null&&hunting.health.HasAuthoritativeActorHealth)
            {
                int[] actors=PhotonNetwork.InRoom?PhotonNetwork.CurrentRoom.Players.Values.Select(p=>p.ActorNumber).ToArray():DebugPracticeActors;
                if(actors.Length>0&&actors.All(a=>!hunting.health.IsActorAlive(a)))
                {State.survival.defeated=true;State.survival.defeatedAt=Clock;changed=true;if(PhotonNetwork.InRoom)PhotonNetwork.CurrentRoom.IsOpen=false;}
            }
            if(PhotonNetwork.InRoom)foreach(var chest in State.survival.chests.Where(c=>!c.claimed&&!c.orphaned))
            {
                // Inactive actors keep their reservation. Salvage starts only after actual room removal.
                var member=PhotonNetwork.CurrentRoom.GetPlayer(chest.owner);
                if(member!=null&&!member.IsInactive){if(chest.ownerAbsentSince>=0){chest.ownerAbsentSince=-1;changed=true;}}
                else if(chest.ownerAbsentSince<0){chest.ownerAbsentSince=Clock;changed=true;}
                else if(member==null&&Clock-chest.ownerAbsentSince>=45){chest.orphaned=true;changed=true;}
            }
            foreach(var inventory in State.survival.players)
            {
                if(inventory.flashlightOn)
                {
                    SettleBattery(inventory);changed=true;
                    if(inventory.batterySeconds<=0||!CanAct(inventory.actor))inventory.flashlightOn=false;
                }
                inventory.batteryUpdatedAt=Clock;
            }
            if(changed)Commit();
        }
        static void SettleBattery(ArchiveSurvivalInventory inventory)
        {
            if(inventory.flashlightOn)inventory.batterySeconds=Mathf.Max(0,inventory.batterySeconds-(float)Math.Max(0,Clock-inventory.batteryUpdatedAt));
            inventory.batteryUpdatedAt=Clock;
        }
        bool FireTranquilizer(int actor,Vector3 direction)
        {
            if(float.IsNaN(direction.x)||float.IsNaN(direction.y)||float.IsNaN(direction.z)||
                float.IsInfinity(direction.x)||float.IsInfinity(direction.y)||float.IsInfinity(direction.z)||direction.sqrMagnitude<.1f||direction.sqrMagnitude>4)return false;
            direction.Normalize();var player=ActorPlayer(actor);var capsule=player.GetComponent<CharacterController>();
            Vector3 origin=capsule!=null?capsule.bounds.center+Vector3.up*capsule.bounds.extents.y*.45f:player.transform.position+Vector3.up*1.5f;
            // Existing monster body boxes are triggers; ignore only unrelated trigger volumes such as water.
            var rayHits=Physics.RaycastAll(origin,direction,TranquilizerRange,~0,QueryTriggerInteraction.Collide).OrderBy(h=>h.distance);
            Vector3 end=origin+direction*TranquilizerRange;KhezuHuntingController hitMonster=null;
            foreach(var hit in rayHits)
            {
                if(hit.collider.transform.IsChildOf(player.transform))continue;
                if(hit.collider.isTrigger&&hit.collider.GetComponentInParent<KhezuHuntingController>()==null)continue;
                end=hit.point;hitMonster=hit.collider.GetComponentInParent<KhezuHuntingController>();break;
            }
            bool sedated=false;
            if(hitMonster!=null&&hitMonster.CanTranquilize)
            {
                var effect=State.survival.sedations.FirstOrDefault(s=>s.encounterId==hitMonster.encounterId);
                if(effect==null){effect=new ArchiveMonsterSedation{encounterId=hitMonster.encounterId};State.survival.sedations.Add(effect);}
                if(Clock>=effect.immuneUntil){effect.until=Clock+TranquilizerSeconds;effect.immuneUntil=effect.until+TranquilizerImmunity;hitMonster.HoldTranquilized(true);sedated=true;}
            }
            var state=State.survival;state.shotSerial++;state.shotActor=actor;state.shotAt=Clock;state.shotOrigin=origin;state.shotEnd=end;state.shotHit=sedated;
            return true;
        }
    }
}
