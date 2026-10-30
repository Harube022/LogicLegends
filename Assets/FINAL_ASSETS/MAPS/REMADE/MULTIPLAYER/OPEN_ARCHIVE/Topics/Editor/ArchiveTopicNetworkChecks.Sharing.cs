using System;
using System.Collections;
using System.Linq;
using Photon.Pun;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static partial class ArchiveTopicNetworkChecks
    {
        static int sharingRecipient;
        static int[] sharingRecipientCounts;
        static float sharingRecipientCharge;

        static void SharingMoveHost(Player player,Vector3 position,Quaternion rotation)
        {
            if(player==null)return;
            var capsule=player.GetComponent<CharacterController>();bool wasEnabled=capsule!=null&&capsule.enabled;
            if(capsule!=null)capsule.enabled=false;
            player.transform.SetPositionAndRotation(position,rotation);player.ResetMotionAfterTeleport();
            if(capsule!=null)capsule.enabled=wasEnabled;
            Physics.SyncTransforms();
        }
        static Vector3 SharingCenter(Player player)
        {
            var capsule=player.GetComponent<CharacterController>();
            return capsule!=null?capsule.bounds.center:player.transform.position+Vector3.up*1.5f;
        }
        static IEnumerator SharingCooldown(Peer peer)
        {yield return Wait(()=>ArchiveTopicSession.Clock>=session.Inventory(peer.Actor).nextUseAt,"Sharing respects the donor's existing item-use cooldown",3);}
        static bool SharingSnapshotMatches(Peer peer)
        {
            var bag=peer.state?.survival?.players?.FirstOrDefault(p=>p.actor==sharingRecipient);
            return bag!=null&&sharingRecipientCounts!=null&&bag.items.SequenceEqual(sharingRecipientCounts)&&
                !bag.flashlightOn&&Mathf.Abs(bag.batterySeconds-sharingRecipientCharge)<.03f;
        }
        static bool SharingAuthorityRetained()
        {
            var bag=session.Inventory(sharingRecipient);
            return bag!=null&&sharingRecipientCounts!=null&&bag.items.SequenceEqual(sharingRecipientCounts)&&
                !bag.flashlightOn&&Mathf.Abs(bag.batterySeconds-sharingRecipientCharge)<.03f;
        }

        static IEnumerator SharingFlow(Peer a,int host)
        {
            report.scope+=" Sharing additionally uses actual sender-bound guest commands against the rendered host: partial-charge flashlight transfer, unique-tool and replay rejection, targeted medkit/battery use, self/distance/solid-wall checks, gun/ammo conservation, recipient late join and host-transfer checkpoints. Stock, charge and one-heart setup are explicit host-authorized test fixtures.";
            Status="Live guest-to-host supply transfers and targeted support use";
            var guest=session.ActorPlayer(a.Actor);var local=session.ActorPlayer(host);var health=session.hunting.health;
            var oldGuestPosition=guest.transform.position;var oldGuestRotation=guest.transform.rotation;
            var oldHostPosition=local.transform.position;var oldHostRotation=local.transform.rotation;
            GameObject wall=null;
            Action cleanup=()=>
            {
                if(wall!=null){UnityEngine.Object.DestroyImmediate(wall);wall=null;}
                if(Application.isPlaying){SharingMoveHost(local,oldHostPosition,oldHostRotation);SharingMoveHost(guest,oldGuestPosition,oldGuestRotation);}
            };
            // Finish() already invokes this fixture hook on timeout or assertion failure.
            hotbarDartCleanup=cleanup;
            try
            {
                Check(session.RequestAdminTest(ArchiveAdminTestAction.ClearInventory,host),"Host authorizes an empty recipient inventory fixture");
                for(int i=0;i<3;i++)Check(session.RequestAdminTest(ArchiveAdminTestAction.GrantItem,a.Actor,(int)ArchiveSurvivalItem.Medkit),"Host authorizes one of three guest medkits for targeted support tests");
                var donor=session.Inventory(a.Actor);donor.batterySeconds=137;donor.batteryUpdatedAt=ArchiveTopicSession.Clock;donor.flashlightOn=false;SurvivalCommit();
                float guestFoot=guest.GetComponent<CharacterController>().bounds.min.y;
                float hostOffset=local.transform.position.y-local.GetComponent<CharacterController>().bounds.min.y;
                bool clear=false;Vector3 beside=Vector3.zero;
                foreach(var offset in new[]{guest.transform.right*3.2f,-guest.transform.right*3.2f,guest.transform.forward*3.2f,-guest.transform.forward*3.2f})
                {
                    beside=guest.transform.position+offset;beside.y=guestFoot+hostOffset;
                    SharingMoveHost(local,beside,oldHostRotation);
                    // Equip below is the actual guest command; this probe only chooses an unobstructed fixture.
                    var from=SharingCenter(guest);var delta=SharingCenter(local)-from;
                    clear=!Physics.RaycastAll(from,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore)
                        .Any(h=>!h.collider.transform.IsChildOf(guest.transform)&&!h.collider.transform.IsChildOf(local.transform));
                    if(clear)break;
                }
                Check(clear&&Vector3.Distance(local.transform.position,guest.transform.position)<=ArchiveTopicSession.SharingDistance,"Rendered host stands within real clear physical reach of the replicated guest");
                yield return HotbarMove(a,oldGuestPosition,oldGuestRotation);
                yield return SurvivalSend(a,HotbarCommand(a,ArchiveAction.EquipSurvivalItem,(int)ArchiveSurvivalItem.Flashlight),true,"Guest equips the partially charged flashlight before giving it");
                yield return SharingCooldown(a);
                var give=HotbarCommand(a,ArchiveAction.GiveSurvivalItem,(int)ArchiveSurvivalItem.Flashlight,host);
                Packet(a,give,true);yield return Wait(()=>a.receipts.ContainsKey(give.sequence),"Forged sender-field transfer receives its authenticated receipt",8);
                Check(a.receipts[give.sequence].accepted,"Server binds Give to the real Photon guest despite an invented host actor field");report.acceptedRemoteCommands++;
                var recipient=session.Inventory(host);
                Check(session.CountItem(a.Actor,ArchiveSurvivalItem.Flashlight)==0&&session.CountItem(host,ArchiveSurvivalItem.Flashlight)==1&&
                    session.EquippedItem(a.Actor)==-1&&!donor.flashlightOn&&!recipient.flashlightOn&&donor.batterySeconds==0&&recipient.batterySeconds<=137&&recipient.batterySeconds>130,
                    "Exactly one partially charged flashlight transfers off with its real remaining charge, never a fresh four-minute battery");
                yield return Wait(()=>a.state?.survival?.players?.Any(p=>p.actor==host&&p.items[0]==1&&p.batterySeconds>130&&p.batterySeconds<=137&&!p.flashlightOn)==true,
                    "Remote owner receives the recipient's transferred partial-charge checkpoint",8);
                Packet(a,give);yield return .2f;
                Check(session.CountItem(a.Actor,ArchiveSurvivalItem.Flashlight)==0&&session.CountItem(host,ArchiveSurvivalItem.Flashlight)==1&&recipient.batterySeconds<=137,
                    "Replaying Give cannot duplicate a unique tool or recreate its charge");
                Check(session.RequestAdminTest(ArchiveAdminTestAction.GrantItem,a.Actor,(int)ArchiveSurvivalItem.Flashlight),"Explicit host fixture restores a separate donor flashlight for duplicate-tool rejection");
                yield return SurvivalSend(a,HotbarCommand(a,ArchiveAction.EquipSurvivalItem,(int)ArchiveSurvivalItem.Flashlight),true,"Guest selects its separately granted flashlight");
                yield return SharingCooldown(a);
                yield return SurvivalSend(a,HotbarCommand(a,ArchiveAction.GiveSurvivalItem,(int)ArchiveSurvivalItem.Flashlight,host),false,"Recipient cannot receive a second owned flashlight");
                Check(session.CountItem(a.Actor,ArchiveSurvivalItem.Flashlight)==1&&session.CountItem(host,ArchiveSurvivalItem.Flashlight)==1,"Unique-tool rejection preserves both inventories");

                yield return SurvivalSend(a,HotbarCommand(a,ArchiveAction.EquipSurvivalItem,(int)ArchiveSurvivalItem.Medkit),true,"Guest selects its own medkit for teammate treatment");
                Check(health.DamageAuthoritativeActor(host),"Original health owner removes one host heart for targeted medkit validation");session.hunting.PublishRescueHealth();SurvivalCommit();
                int meds=donor.items[(int)ArchiveSurvivalItem.Medkit],guestHearts=health.ActorHearts(a.Actor);
                yield return SurvivalSend(a,HotbarCommand(a,ArchiveAction.UseSurvivalItem,(int)ArchiveSurvivalItem.Medkit,host),true,"Authenticated guest uses its medkit on the nearby host");
                Check(health.ActorHearts(host)==health.MaximumHealth&&health.ActorHearts(a.Actor)==guestHearts&&donor.items[2]==meds-1&&recipient.items[2]==0,
                    "Targeted medkit restores exactly one recipient heart and consumes only the donor's one kit");
                yield return SharingCooldown(a);
                yield return SurvivalSend(a,HotbarCommand(a,ArchiveAction.UseSurvivalItem,(int)ArchiveSurvivalItem.Medkit,host),false,"Full-heart teammate treatment is rejected without consuming a kit");
                yield return SurvivalSend(a,HotbarCommand(a,ArchiveAction.GiveSurvivalItem,(int)ArchiveSurvivalItem.Medkit,a.Actor),false,"A forged self-recipient cannot create an inventory transfer");
                SharingMoveHost(local,beside+guest.transform.forward*20,oldHostRotation);
                yield return SurvivalSend(a,HotbarCommand(a,ArchiveAction.GiveSurvivalItem,(int)ArchiveSurvivalItem.Medkit,host),false,"Guest cannot give supplies to a distant teammate");
                SharingMoveHost(local,beside,oldHostRotation);
                var gap=SharingCenter(local)-SharingCenter(guest);wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Sharing_Solid_LOS_QA";
                wall.transform.SetPositionAndRotation((SharingCenter(local)+SharingCenter(guest))*.5f,Quaternion.LookRotation(gap));wall.transform.localScale=new Vector3(6,8,.4f);wall.GetComponent<Renderer>().enabled=false;Physics.SyncTransforms();
                yield return SurvivalSend(a,HotbarCommand(a,ArchiveAction.GiveSurvivalItem,(int)ArchiveSurvivalItem.Medkit,host),false,"A real solid wall blocks giving between nearby players");
                UnityEngine.Object.DestroyImmediate(wall);wall=null;Physics.SyncTransforms();
                Check(donor.items[2]==meds-1&&recipient.items[2]==0,"Full, self, distance and solid-wall rejections consume or create no medkits");

                yield return SurvivalSend(a,HotbarCommand(a,ArchiveAction.EquipSurvivalItem,(int)ArchiveSurvivalItem.Battery),true,"Guest equips its battery for teammate flashlight support");
                int batteries=donor.items[1];
                yield return SurvivalSend(a,HotbarCommand(a,ArchiveAction.UseSurvivalItem,(int)ArchiveSurvivalItem.Battery,host),true,"Guest loads its battery into the recipient's owned flashlight");
                Check(recipient.batterySeconds==ArchiveTopicSession.FlashlightCapacity&&donor.items[1]==batteries-1&&!recipient.flashlightOn,"Targeted battery refills only the recipient and consumes one donor battery");
                yield return SharingCooldown(a);
                yield return SurvivalSend(a,HotbarCommand(a,ArchiveAction.UseSurvivalItem,(int)ArchiveSurvivalItem.Battery,host),false,"A full recipient flashlight rejects another battery without consumption");
                Check(donor.items[1]==batteries-1,"Rejected full recharge retains the donor's remaining batteries");

                yield return SurvivalSend(a,HotbarCommand(a,ArchiveAction.EquipSurvivalItem,(int)ArchiveSurvivalItem.Tranquilizer),true,"Guest selects its real gun for giving");
                int ammo=donor.items[5];
                yield return SurvivalSend(a,HotbarCommand(a,ArchiveAction.GiveSurvivalItem,(int)ArchiveSurvivalItem.Tranquilizer,host),true,"Guest gives the selected gun through its authenticated supply command");
                Check(donor.items[4]==0&&recipient.items[4]==1&&recipient.items[5]==0&&donor.items[5]==ammo&&session.EquippedItem(a.Actor)==-1,
                    "Giving a gun moves exactly the owned tool, clears its donor hand and grants no free ammunition");
                yield return SurvivalSend(a,HotbarCommand(a,ArchiveAction.EquipSurvivalItem,(int)ArchiveSurvivalItem.TranquilizerAmmo),true,"Guest selects its darts as a separate transferable item");
                yield return SharingCooldown(a);
                yield return SurvivalSend(a,HotbarCommand(a,ArchiveAction.GiveSurvivalItem,(int)ArchiveSurvivalItem.TranquilizerAmmo,host),true,"Guest separately gives exactly one tranquilizer dart");
                Check(donor.items[5]==ammo-1&&recipient.items[5]==1,"One ammo transfer conserves the combined dart count");
                Check(session.RequestAdminTest(ArchiveAdminTestAction.GrantItem,a.Actor,(int)ArchiveSurvivalItem.Tranquilizer),"Explicit host fixture restores a donor gun for the existing late-join hand checks");
                yield return SurvivalSend(a,HotbarCommand(a,ArchiveAction.EquipSurvivalItem,(int)ArchiveSurvivalItem.Tranquilizer),true,"Guest finishes sharing with its gun selected for the existing late-join and migration checks");
                sharingRecipient=host;sharingRecipientCounts=recipient.items.ToArray();sharingRecipientCharge=recipient.batterySeconds;
                yield return Wait(()=>SharingSnapshotMatches(a),"Real peer sees the complete recipient inventory and charge checkpoint",8);
            }
            finally {cleanup();hotbarDartCleanup=null;}
            yield return HotbarMove(a,oldGuestPosition,oldGuestRotation);
        }
    }
}
