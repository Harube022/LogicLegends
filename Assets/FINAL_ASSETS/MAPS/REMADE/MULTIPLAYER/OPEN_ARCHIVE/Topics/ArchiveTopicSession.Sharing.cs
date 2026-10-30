using System.Linq;
using Photon.Pun;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    public sealed partial class ArchiveTopicSession
    {
        public const float SharingDistance=RescueUseDistance;

        public bool RequestGiveEquippedItem(int target)
        {
            int selected=EquippedItem(LocalActor);
            if(!CanGiveItem(LocalActor,target,(ArchiveSurvivalItem)selected,out var reason))
            {ShowLocalNotice(reason);return false;}
            return SendSurvival(0,ArchiveAction.GiveSurvivalItem,selected,target);
        }

        public bool CanGiveItem(int actor,int target,ArchiveSurvivalItem item,out string reason)
        {
            if(!CanShareSupply(actor,target,item,false,out reason))return false;
            var donor=Inventory(actor);
            if(donor.equippedItem!=(int)item){reason="Equip the item you want to give first.";return false;}
            bool unique=item==ArchiveSurvivalItem.Flashlight||item==ArchiveSurvivalItem.Tranquilizer;
            if(unique&&CountItem(target,item)>0){reason="This teammate already owns that tool.";return false;}
            if(CountItem(target,item)==int.MaxValue){reason="This teammate cannot carry another of that item.";return false;}
            return true;
        }

        public bool CanUseItemOnTeammate(int actor,int target,ArchiveSurvivalItem item,out string reason)
        {
            if(item!=ArchiveSurvivalItem.Battery&&item!=ArchiveSurvivalItem.Medkit&&item!=ArchiveSurvivalItem.RevivalKit)
            {reason="This item can be given to a teammate, but cannot be used on them.";return false;}
            if(!CanShareSupply(actor,target,item,item==ArchiveSurvivalItem.RevivalKit,out reason))return false;
            if(item==ArchiveSurvivalItem.RevivalKit)
            {
                // Match the existing TryReviveActor nest/arrival rule; no remote revival path.
                var fallen=Downed(target);
                if(!IsRevivalTargetAvailable(target)||
                    Vector3.Distance(ActorPlayer(actor).transform.position,fallen.anchor)>RescueUseDistance)
                {reason="Bring the revival kit beside your downed teammate at the Monster Nest.";return false;}
                return true;
            }
            if(item==ArchiveSurvivalItem.Battery)
            {
                if(CountItem(target,ArchiveSurvivalItem.Flashlight)<=0){reason="This teammate needs a flashlight before you can load a battery.";return false;}
                if(FlashlightRemaining(target)>=FlashlightCapacity-1){reason="This teammate's flashlight battery is already full.";return false;}
            }
            else if(hunting?.health==null||!hunting.health.HasAuthoritativeActorHealth||
                hunting.health.ActorHearts(target)<=0||hunting.health.ActorHearts(target)>=hunting.health.MaximumHealth)
            {reason="A medkit restores one missing heart; a downed teammate needs a revival kit.";return false;}
            return true;
        }

        bool CanShareSupply(int actor,int target,ArchiveSurvivalItem item,bool revive,out string reason)
        {
            reason="";
            if(!Ready||State.survival==null||MultiplayerLobbyService.IsGameplayRecovering||Time.timeScale<=0)
            {reason="Wait until gameplay resumes before sharing supplies.";return false;}
            if(actor==0||target==0||actor==target||!SharingActorConnected(actor)||!SharingActorConnected(target))
            {reason="Choose a connected teammate.";return false;}
            if(!CanAct(actor)||Downed(actor)!=null||NightEntranceActive)
            {reason="You cannot share supplies while captured, downed or movement-locked.";return false;}
            int index=(int)item;var donor=Inventory(actor);
            if(index<0||index>=6||donor?.items==null||donor.items.Length<=index||donor.items[index]<=0)
            {reason="You do not own that item.";return false;}
            if(Clock<donor.nextUseAt){reason="Wait a moment before using or giving another item.";return false;}
            if(HasEscapedActor(target)||TeamWon||TeamDefeated||(!revive&&(!CanAct(target)||Downed(target)!=null)))
            {reason="That teammate cannot receive supplies now. Downed players need a revival kit at the nest.";return false;}
            if(revive)return true;
            var source=ActorPlayer(actor);var recipient=ActorPlayer(target);
            if(Vector3.Distance(source.transform.position,recipient.transform.position)>SharingDistance)
            {reason="Move beside your teammate to share supplies.";return false;}
            var origin=SharingBodyCenter(source);var delta=SharingBodyCenter(recipient)-origin;
            if(delta.sqrMagnitude>.0001f&&Physics.RaycastAll(origin,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore)
                .Any(h=>!h.collider.transform.IsChildOf(source.transform)&&!h.collider.transform.IsChildOf(recipient.transform)))
            {reason="A solid obstacle blocks your teammate. Move into clear reach.";return false;}
            return true;
        }

        bool SharingActorConnected(int actor)
        {
            if(PhotonNetwork.InRoom)
            {
                var member=PhotonNetwork.CurrentRoom?.GetPlayer(actor);
                if(member==null||member.IsInactive)return false;
            }
            else if(actor!=LocalActor&&DebugActorPlayer(actor)==null)return false;
            var player=ActorPlayer(actor);
            return player!=null&&player.gameObject.activeInHierarchy;
        }

        static Vector3 SharingBodyCenter(Player player)
        {
            var capsule=player.GetComponent<CharacterController>();
            return capsule!=null?capsule.bounds.center:player.transform.position+player.transform.up*1.5f;
        }

        bool GiveSurvivalItem(int actor,int target,ArchiveSurvivalItem item,out string reason)
        {
            if(!CanGiveItem(actor,target,item,out reason))return false;
            var donor=Inventory(actor);var recipient=EnsureInventory(target);int index=(int)item;
            SettleBattery(donor);SettleBattery(recipient);
            // Transfers bypass chest grants: no free charge or bonus dart is created.
            donor.items[index]--;recipient.items[index]++;
            if(item==ArchiveSurvivalItem.Flashlight)
            {
                recipient.batterySeconds=donor.batterySeconds;recipient.flashlightOn=false;
                donor.batterySeconds=0;donor.flashlightOn=false;
            }
            if(donor.items[index]<=0)donor.equippedItem=-1;
            donor.nextUseAt=Clock+.45;
            return true;
        }
    }
}
