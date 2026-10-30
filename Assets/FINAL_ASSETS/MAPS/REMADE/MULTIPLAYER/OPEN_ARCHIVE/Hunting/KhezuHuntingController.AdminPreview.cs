using UnityEngine;
namespace LogicLegends.OpenArchive.Hunting
{
    public sealed partial class KhezuHuntingController
    {
        public void AdminPreviewCapture(int actor,bool barrier)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var session=Topics.ArchiveTopicSession.Current;
            if(session==null||!session.CanUseTopicAdmin||!Initialized||session.ActorPlayer(actor)==null)return;
            var snapshot=GetSnapshot();snapshot.state=(int)KhezuHuntState.Caught;snapshot.target=snapshot.caught=actor;
            snapshot.captureSerial=NextSharedCaptureSerial();snapshot.huntSerial++;snapshot.captureStartedAt=EncounterClock-(barrier?Cinematics.KhezuCinematicJumpscareController.Duration+.1:0);
            snapshot.capturePosition=session.ActorPlayer(actor).transform.position;snapshot.captureAnchorValid=true;snapshot.encounterPhase=barrier?4:1;
            snapshot.resolvedOutcome=0;snapshot.resolvedSerial=0;snapshot.awake=true;snapshot.pending=false;snapshot.barrierDeadline=0;snapshot.barrierTimerSerial=0;
            snapshot.rescueActor=0;snapshot.animation="Idle";snapshot.animationSpeed=1;ApplySnapshot(snapshot,true);StopNavigation();SnapshotChanged?.Invoke();
#endif
        }
    }
}
