using LogicLegends.OpenArchive.Hunting;
using LogicLegends.OpenArchive.Water;
using Photon.Pun;
using UnityEngine;

namespace LogicLegends.OpenArchive.Cinematics.VFX
{
    /// <summary>Reads the existing confirmed encounter. Never starts/resolves a challenge or changes player state.</summary>
    [DefaultExecutionOrder(120),DisallowMultipleComponent]
    [RequireComponent(typeof(KhezuCaptureEncounterController))]
    public sealed class KhezuBarrierVfxPresentation : MonoBehaviourPunCallbacks
    {
        public CrimsonMathBarrierVfx barrierPrefab;
        [Tooltip("Optional monster palette. Empty uses the barrier prefab's original crimson style.")]
        public CrimsonMathBarrierStyle styleOverride;
        public KhezuCaptureEncounterController encounter;
        public KhezuHuntingController hunting;
        public CrimsonMathBarrierVfx ActiveBarrier {get;private set;}
        public int PresentedSerial {get;private set;}
        Player captured;
        int actor;
        float nextPlayerSearch;
        bool disconnected;
        void Awake(){if(encounter==null)encounter=GetComponent<KhezuCaptureEncounterController>();if(hunting==null)hunting=GetComponent<KhezuHuntingController>();}
        void LateUpdate(){
            bool confirmed=!disconnected && encounter!=null && hunting!=null && !hunting.BarrierResolving && encounter.isActiveAndEnabled && encounter.Phase==KhezuEncounterPhase.BarrierChallenge &&
                KhezuHuntingController.EncounterClock-hunting.CaptureStartedAt>=KhezuCinematicJumpscareController.Duration;
            if(confirmed && KhezuCaptureEncounterController.IsCaughtLocalPlayer(hunting.CaughtPlayerId) && encounter.cinematic!=null && encounter.cinematic.IsPlaying)confirmed=false;
            if(!confirmed){EndVisual();return;}
            if(ActiveBarrier!=null && (PresentedSerial!=hunting.CaptureSerial || actor!=hunting.CaughtPlayerId))EndVisual();
            if(captured==null || actor!=hunting.CaughtPlayerId){
                if(Time.unscaledTime<nextPlayerSearch)return;nextPlayerSearch=Time.unscaledTime+.2f;captured=FindPlayer(hunting.CaughtPlayerId);
            }
            if(captured==null || barrierPrefab==null)return;
            if(ActiveBarrier==null){
                actor=hunting.CaughtPlayerId;PresentedSerial=hunting.CaptureSerial;
                ActiveBarrier=Instantiate(barrierPrefab,Feet(captured),Quaternion.identity);ActiveBarrier.name="Crimson_Math_Barrier_Actor_"+actor;
                if(styleOverride!=null)ActiveBarrier.style=styleOverride;
                // Late snapshots resume the formed visual rather than replaying formation.
                float elapsed=(float)(KhezuHuntingController.EncounterClock-hunting.CaptureStartedAt)-KhezuCinematicJumpscareController.Duration;
                ActiveBarrier.BeginFormation(elapsed);
            }
            ActiveBarrier.transform.position=Feet(captured);
        }
        static Vector3 Feet(Player p){var cc=p.GetComponent<CharacterController>();var position=p.transform.position;if(cc!=null)position.y=OpenArchiveWater.Feet(cc).y;return position+Vector3.up*.045f;}
        static Player FindPlayer(int id){
            if(!PhotonNetwork.InRoom)return id==-1?Player.LocalInstance:null;
            foreach(var p in FindObjectsByType<Player>(FindObjectsSortMode.None)){var view=p.GetComponent<PhotonView>();if(view!=null && view.ViewID!=0 && view.OwnerActorNr==id)return p;}
            return null;
        }
        void EndVisual(){
            if(ActiveBarrier!=null){float elapsed=hunting!=null && hunting.ResolvedSerial==PresentedSerial && hunting.ResolvedOutcome!=0?(float)(KhezuHuntingController.EncounterClock-hunting.ResolvedAt)*ActiveBarrier.style.animationSpeed:0;ActiveBarrier.Dissolve(elapsed);}
            ActiveBarrier=null;captured=null;actor=0;nextPlayerSearch=0;
        }
        public override void OnDisconnected(Photon.Realtime.DisconnectCause cause){disconnected=true;EndVisual();}
        public override void OnLeftRoom(){disconnected=true;EndVisual();}
        public override void OnJoinedRoom(){disconnected=false;}
        public override void OnDisable(){if(ActiveBarrier!=null)Destroy(ActiveBarrier.gameObject);ActiveBarrier=null;captured=null;actor=0;base.OnDisable();}
    }
}
