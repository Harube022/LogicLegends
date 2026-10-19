using ExitGames.Client.Photon;
using LogicLegends.OpenArchive.Cinematics.VFX;
using LogicLegends.OpenArchive.Hunting;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace LogicLegends.OpenArchive.Cinematics
{
    public sealed partial class KhezuCaptureEncounterController
    {
        public const byte DevelopmentOutcomeEvent=173;
        public const string DevelopmentOutcomeChannel="ll.khezu.barrier.dev.v1";
        [Header("Dedicated development challenge")]
        [Tooltip("Both this switch and an Editor/development build are required. Release builds reject test outcomes.")]
        public bool developmentTestOutcomes;
        public bool DevelopmentOutcomesEnabled=>CompiledDevelopmentOutcomes && developmentTestOutcomes;
        public static bool CompiledDevelopmentOutcomes {
            get{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return true;
#else
                return false;
#endif
            }
        }
        public bool LocalChallengeActive=>isActiveAndEnabled && !disconnected && Phase==KhezuEncounterPhase.BarrierChallenge && !hunting.BarrierResolving &&
            KhezuCaptureEncounterController.IsCaughtLocalPlayer(hunting.CaughtPlayerId) && (cinematic==null || !cinematic.IsPlaying);
        Player barrierPlayer;
        int lockedActor,lockedSerial;
        float nextPlayerSearch;
        KhezuHuntState lastLoggedState=(KhezuHuntState)(-1);
        public void SynchronizeBarrierMovement(){
            if(hunting==null)return;
            if(lastLoggedState!=hunting.State){lastLoggedState=hunting.State;BarrierLog("Next monster state: "+hunting.State);}
            if(disconnected || hunting.State!=KhezuHuntState.Caught){ReleaseBarrierMovement();return;}
            if(barrierPlayer!=null && (lockedActor!=hunting.CaughtPlayerId || lockedSerial!=hunting.CaptureSerial))ReleaseBarrierMovement();
            if(barrierPlayer==null){
                if(Time.unscaledTime<nextPlayerSearch)return;nextPlayerSearch=Time.unscaledTime+.1f;
                barrierPlayer=CapturedAvatar(hunting.CaughtPlayerId);
                if(barrierPlayer==null)return;lockedActor=hunting.CaughtPlayerId;lockedSerial=hunting.CaptureSerial;
                if(!hunting.CaptureAnchorValid && hunting.IsAuthority)hunting.ConfirmCaptureAnchor(barrierPlayer.transform.position);
                BarrierLog("Captured player "+lockedActor+"; movement protection begins for capture "+lockedSerial);
            }
            if(hunting.CaptureAnchorValid)barrierPlayer.AcquireMovementLock(this,"Mathematical barrier capture "+lockedSerial,hunting.CapturePosition,100);
            if(hunting.IsAuthority && hunting.health!=null && !hunting.health.IsActorAlive(lockedActor) && !hunting.BarrierResolving){hunting.Request(KhezuCommand.ResetHunt);hunting.Request(KhezuCommand.StartHunting);}
        }
        void LateUpdate(){if(barrierPlayer!=null && barrierPlayer.IsMovementLocked)barrierPlayer.EnforceMovementLock();}
        void ReleaseBarrierMovement(){
            if(barrierPlayer!=null){
                if(hunting!=null && hunting.health!=null && !hunting.health.IsActorAlive(lockedActor))hunting.health.HoldDeathMovement(barrierPlayer,hunting.CapturePosition);
                barrierPlayer.ReleaseMovementLock(this);BarrierLog("Barrier movement lock released for "+lockedActor);
                if(hunting!=null && hunting.ResolvedSerial==lockedSerial)BarrierLog("Barrier dissolved for actor "+lockedActor+"; next state "+hunting.State);
            }
            barrierPlayer=null;lockedActor=lockedSerial=0;nextPlayerSearch=0;
        }
        static Player CapturedAvatar(int actor){
            if(!PhotonNetwork.InRoom)return actor==-1?Player.LocalInstance:null;
            foreach(var p in FindObjectsByType<Player>(FindObjectsSortMode.None)){var view=p.GetComponent<PhotonView>();if(view!=null && view.ViewID!=0 && view.OwnerActorNr==actor)return p;}return null;
        }
        public bool SubmitDevelopmentOutcome(bool success){
            if(!DevelopmentOutcomesEnabled || !LocalChallengeActive)return false;
            int sender=PhotonNetwork.InRoom?PhotonNetwork.LocalPlayer.ActorNumber:-1;
            BarrierLog((success?"SUCCESS":"FAILED")+" pressed by "+sender);
            if(hunting.IsAuthority)return AcceptDevelopmentOutcome(sender,hunting.CaptureSerial,success);
            return PhotonNetwork.RaiseEvent(DevelopmentOutcomeEvent,new object[]{DevelopmentOutcomeChannel,hunting.CaptureSerial,success},new RaiseEventOptions{Receivers=ReceiverGroup.MasterClient},SendOptions.SendReliable);
        }
        public void OnEvent(EventData data){
            if(data.Code!=DevelopmentOutcomeEvent || hunting==null || !hunting.IsAuthority || !PhotonNetwork.InRoom)return;
            var packet=data.CustomData as object[];
            if(packet==null || packet.Length!=3 || !(packet[0] is string) || (string)packet[0]!=DevelopmentOutcomeChannel || !(packet[1] is int) || !(packet[2] is bool))return;
            var sender=PhotonNetwork.CurrentRoom.GetPlayer(data.Sender);if(sender==null || sender.IsInactive)return;
            // Identity comes exclusively from Photon's authenticated event sender, never the payload.
            AcceptDevelopmentOutcome(data.Sender,(int)packet[1],(bool)packet[2]);
        }
        bool AcceptDevelopmentOutcome(int sender,int serial,bool success){
            if(!isActiveAndEnabled || !DevelopmentOutcomesEnabled || !hunting.IsAuthority || disconnected || Phase!=KhezuEncounterPhase.BarrierChallenge ||
                hunting.BarrierResolving || sender!=hunting.CaughtPlayerId || serial!=hunting.CaptureSerial ||
                KhezuHuntingController.EncounterClock-hunting.CaptureStartedAt<KhezuCinematicJumpscareController.Duration)return false;
            var presentation=GetComponent<KhezuBarrierVfxPresentation>();float dissolve=.9f;
            if(presentation!=null && presentation.barrierPrefab!=null && presentation.barrierPrefab.style!=null){var style=presentation.barrierPrefab.style;dissolve=style.dissolveSeconds/style.animationSpeed;}
            bool accepted=hunting.ResolveBarrierOutcome(sender,serial,success,dissolve);
            if(accepted)BarrierLog("Host outcome accepted exactly once: actor "+sender+", capture "+serial+", "+(success?"SUCCESS":"FAILED")+"; barrier dissolving");
            return accepted;
        }
        [System.Diagnostics.Conditional("UNITY_EDITOR"),System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void BarrierLog(string message){Debug.Log("[Khezu Barrier] "+message);}
    }
}
