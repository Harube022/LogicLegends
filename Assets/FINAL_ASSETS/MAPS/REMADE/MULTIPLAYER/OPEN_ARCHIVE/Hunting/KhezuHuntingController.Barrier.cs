using UnityEngine;
using UnityEngine.AI;

namespace LogicLegends.OpenArchive.Hunting
{
    public sealed partial class KhezuHuntingController
    {
        [Header("Existing encounter outcomes")]
        public Transform monsterSpawnPoint;
        public HealthManager health;
        public const double SuccessSleepSeconds=30;
        public Vector3 CapturePosition {get;private set;}
        public bool CaptureAnchorValid {get;private set;}
        public int ResolvedSerial {get;private set;}
        public int ResolvedActor {get;private set;}
        public int ResolvedOutcome {get;private set;}
        public double ResolvedAt {get;private set;}
        public double ReleaseAt {get;private set;}
        public double NestSleepEndsAt {get;private set;}
        public double NestArrivedAt {get;private set;}
        double attackEndsAt;
        int avoidNextTarget;
        public bool BarrierResolving=>State==KhezuHuntState.Caught && ResolvedSerial==CaptureSerial && ResolvedOutcome!=0;
        public void ConfirmCaptureAnchor(Vector3 position){if(!IsAuthority || State!=KhezuHuntState.Caught || CaptureAnchorValid)return;CapturePosition=position;CaptureAnchorValid=true;SnapshotChanged?.Invoke();}
        public bool ResolveBarrierOutcome(int actor,int serial,bool success,float dissolveSeconds){
            if(!IsAuthority || State!=KhezuHuntState.Caught || EncounterPhase!=4 || BarrierResolving || actor!=CaughtPlayerId || serial!=CaptureSerial)return false;
            if(health==null || !health.HasAuthoritativeActorHealth || !health.IsActorAlive(actor) || monsterSpawnPoint==null)return false;
            ResolvedActor=actor;ResolvedSerial=serial;ResolvedOutcome=success?1:2;ResolvedAt=EncounterClock;ReleaseAt=ResolvedAt+Mathf.Max(.1f,dissolveSeconds);
            Status=success?"Barrier success: dissolving":"Barrier failed: dissolving";SnapshotChanged?.Invoke();return true;
        }
        bool UpdateBarrierOutcome(){
            if(BarrierResolving){
                if(EncounterClock<ReleaseAt)return true;
                int actor=ResolvedActor;
                if(ResolvedOutcome==2 && health!=null)health.DeductAuthoritativeBarrierHeart(actor,ResolvedSerial);
                StopNavigation();TargetPlayerId=CaughtPlayerId=0;EncounterPhase=0;pending=false;nextDestination=0;
                if(ResolvedOutcome==1){State=KhezuHuntState.ReturningToNest;Status="Returning to the Universal Monster Nest";}
                else{State=KhezuHuntState.AttackRecovery;Status="Failed challenge: Attack, then next eligible target";avoidNextTarget=actor;attackEndsAt=EncounterClock+ClipSeconds("Attack");animator.speed=1;Blend("Attack");}
                SnapshotChanged?.Invoke();return true;
            }
            if(State==KhezuHuntState.ReturningToNest){UpdateReturnToNest();return true;}
            if(State==KhezuHuntState.NestSleeping){
                animator.speed=1;
                if(EncounterClock>=NestSleepEndsAt){
                    // Resume the original final awakening stage, then its existing Idle transition.
                    driver.ApplyReplicaCounters(4,4);animator.CrossFadeInFixedTime("Base Layer.Full_Awaken",.25f,0,0);
                    fullyAwake=false;pending=true;idleReadyAt=-1;State=KhezuHuntState.Waking;Status="Thirty-second nest sleep completed: Full_Awaken";SnapshotChanged?.Invoke();
                }else if(EncounterClock-NestArrivedAt>=ClipSeconds("Return_To_Sleep"))Blend("Sleep");
                return true;
            }
            if(State==KhezuHuntState.AttackRecovery){if(EncounterClock>=attackEndsAt)BeginHunt();return true;}
            return false;
        }
        float ClipSeconds(string name){AnimationClip clip;return clips.TryGetValue(name,out clip)?clip.length:1.8f;}
        void UpdateReturnToNest(){
            if(monsterSpawnPoint==null){Status="Return requires the existing MonsterSpawnPoint";return;}
            if(!agent.enabled)agent.enabled=true;
            if(!agent.isOnNavMesh){Status="Return route is outside the existing navigation";return;}
            agent.autoBraking=true;agent.speed=normalPlayerRunSpeed*.85f;agent.acceleration=acceleration;agent.angularSpeed=turnSpeed;
            var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};NavMeshHit goal;
            if(!NavMesh.SamplePosition(monsterSpawnPoint.position,out goal,agent.radius+1,filter))return;
            var delta=goal.position-transform.position;delta.y=0;
            if(delta.magnitude<.65f && (!agent.pathPending && (!agent.hasPath || agent.remainingDistance<.8f))){
                StopNavigation();driver.ApplySharedMistakes(0);animator.speed=1;Blend("Return_To_Sleep");
                State=KhezuHuntState.NestSleeping;fullyAwake=false;pending=false;NestArrivedAt=EncounterClock;NestSleepEndsAt=NestArrivedAt+SuccessSleepSeconds;Status="At nest: sleeping for thirty seconds";SnapshotChanged?.Invoke();return;
            }
            if(Time.time>=nextDestination){
                nextDestination=Time.time+.5f;var path=new NavMeshPath();
                if(NavMesh.CalculatePath(transform.position,goal.position,filter,path) && path.status==NavMeshPathStatus.PathComplete){agent.isStopped=false;agent.SetPath(path);}
                else Status="Waiting for a complete dry return route";
            }
            bool moving=agent.velocity.sqrMagnitude>.09f;Blend(moving?"Chase":"Idle");animator.speed=moving?Mathf.Clamp(agent.velocity.magnitude/chaseAnimationReferenceSpeed,.65f,1.6f):1;
        }
        void FillBarrierSnapshot(KhezuSnapshot s){
            s.capturePosition=CapturePosition;s.captureAnchorValid=CaptureAnchorValid;s.resolvedSerial=ResolvedSerial;s.resolvedActor=ResolvedActor;s.resolvedOutcome=ResolvedOutcome;
            s.resolvedAt=ResolvedAt;s.releaseAt=ReleaseAt;s.sleepEndsAt=NestSleepEndsAt;s.nestArrivedAt=NestArrivedAt;s.attackEndsAt=attackEndsAt;s.avoidNextTarget=avoidNextTarget;
            s.healthStates=health!=null?health.ExportActorHealth():new HealthManager.ActorHealth[0];
        }
        void RestoreBarrierSnapshot(KhezuSnapshot s){
            CapturePosition=s.capturePosition;CaptureAnchorValid=s.captureAnchorValid;ResolvedSerial=s.resolvedSerial;ResolvedActor=s.resolvedActor;ResolvedOutcome=s.resolvedOutcome;
            ResolvedAt=s.resolvedAt;ReleaseAt=s.releaseAt;NestSleepEndsAt=s.sleepEndsAt;NestArrivedAt=s.nestArrivedAt;attackEndsAt=s.attackEndsAt;avoidNextTarget=s.avoidNextTarget;
            if(health!=null)health.ApplyActorHealthSnapshot(s.healthStates);
        }
    }
}
