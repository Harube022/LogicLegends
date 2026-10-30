using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LogicLegends.OpenArchive.Hunting
{
    public sealed partial class KhezuHuntingController
    {
        [Header("Encounter identity")]
        [Tooltip("Empty is the original encounter. The night guardian uses a separate snapshot namespace.")]
        public string encounterId="";
        public bool rajangCounterProfile;
        static readonly List<KhezuHuntingController> sceneMonsters=new List<KhezuHuntingController>();
        public bool EntranceHeld {get;private set;}
        public static KhezuHuntingController[] ActiveMonsters => sceneMonsters.Where(m=>m!=null&&m.isActiveAndEnabled).ToArray();
        bool RegisterMonster()
        {
            sceneMonsters.RemoveAll(m=>m==null);
            if(sceneMonsters.Any(m=>m!=this&&m.gameObject.scene==gameObject.scene&&m.encounterId==encounterId))return false;
            sceneMonsters.Add(this);return true;
        }
        bool ReservedByAnotherMonster(int actor) => ActiveMonsters.Any(m=>m!=this&&
            (m.CaughtPlayerId==actor&&m.State==KhezuHuntState.Caught||m.ResolvedActor==actor&&m.FailureStunActive||
             m.TargetPlayerId==actor&&(m.State==KhezuHuntState.Hunting||m.State==KhezuHuntState.NavigationBlocked)));
        bool CaptureSlotAvailable() => !ActiveMonsters.Any(m=>m!=this&&(m.State==KhezuHuntState.Caught||m.FailureStunActive));
        int NextSharedCaptureSerial() => Mathf.Max(CaptureSerial,Mathf.Max(ActiveMonsters.Select(m=>m.CaptureSerial).DefaultIfEmpty(0).Max(),
            health==null?0:health.ExportActorHealth().Select(h=>h.lastPenaltySerial).DefaultIfEmpty(0).Max()))+1;
        void CheckpointSharedHealth()
        {foreach(var monster in ActiveMonsters)if(monster!=this&&string.IsNullOrEmpty(monster.encounterId))monster.SnapshotChanged?.Invoke();}
        public void HoldNightEntrance(float normalizedTime)
        {
            if(!Initialized)return;EntranceHeld=true;StopNavigation();
            driver.ApplyReplicaCounters(4,4);State=KhezuHuntState.Waking;pending=false;fullyAwake=false;
            animator.speed=0;animator.Play("Base Layer.Full_Awaken",0,Mathf.Clamp01(normalizedTime));
        }
        public void ReleaseNightEntrance()
        {
            if(!Initialized)return;
            if(!EntranceHeld&&State!=KhezuHuntState.Sleeping&&State!=KhezuHuntState.Waking)return;
            EntranceHeld=false;driver.ApplyReplicaCounters(4,4);animator.speed=1;fullyAwake=true;pending=false;
            State=KhezuHuntState.Ready;Blend("Idle");if(IsAuthority)BeginHunt();
        }
        public void AwakenForNight()
        {
            // A night transition can arrive between the first three mistake stages.
            // Those partial wakes must join the night hunt too; earned nest sleep and
            // an active encounter still retain their existing state and deadlines.
            if(!Initialized||!IsAuthority||(State!=KhezuHuntState.Sleeping&&State!=KhezuHuntState.Waking))return;
            if(State==KhezuHuntState.Waking&&driver.PlayingStage>=4)return;
            driver.ApplyReplicaCounters(4,4);
            // Sedation owns the visible Sleep pose until its original expiry. Queue
            // the full wake without overriding that pose or moving the hit anchor.
            if(!IsTranquilized){animator.speed=1;animator.CrossFadeInFixedTime("Base Layer.Full_Awaken",.25f,0,0);}
            fullyAwake=false;pending=true;State=KhezuHuntState.Waking;idleReadyAt=-1;SnapshotChanged?.Invoke();
        }
    }
}
