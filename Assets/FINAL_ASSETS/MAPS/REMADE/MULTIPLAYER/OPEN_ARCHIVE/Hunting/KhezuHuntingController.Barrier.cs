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
        public const double BarrierChallengeSeconds=120,FailureStunSeconds=5;
        [Tooltip("Maximum answer time. Open Archive uses two minutes for both guardians. A started encounter keeps its checkpointed deadline.")]
        [Range(30,180)] public float barrierTimeLimitSeconds=120;
        public const float KnockbackSeconds=.35f;
        [Header("Failed barrier attack")]
        [Tooltip("Contact point in the existing Attack clip. Khezu's claw crosses the player at about 0.62 seconds.")]
        [Range(.1f,.8f)] public float failureAttackContact=.345f;
        [Min(0)] public float failureKnockbackDistance=4;
        public int BarrierTimerSerial {get;private set;}
        public double BarrierStartedAt {get;private set;}
        public double BarrierDeadline {get;private set;}
        public double AttackHitAt {get;private set;}
        public double BarrierDissolveAt {get;private set;}
        public double StunEndsAt {get;private set;}
        public bool BarrierTimedOut {get;private set;}
        public bool FailureHitApplied {get;private set;}
        public Vector3[] KnockbackPath {get;private set;}=new Vector3[0];
        public float BarrierSecondsRemaining=>State==KhezuHuntState.Caught && EncounterPhase==4 && !BarrierResolving && BarrierTimerSerial==CaptureSerial?
            Mathf.Max(0,(float)(BarrierDeadline-EncounterClock)):0;
        public bool FailureStunActive=>State==KhezuHuntState.AttackRecovery && ResolvedOutcome==2 && FailureHitApplied && EncounterClock<StunEndsAt;
        public float StunSecondsRemaining=>FailureStunActive?Mathf.Max(0,(float)(StunEndsAt-EncounterClock)):0;
        // Failure keeps the formed prison until contact; success starts dissolving immediately.
        public bool BarrierDissolving=>ResolvedSerial==CaptureSerial && ResolvedOutcome!=0 &&
            (ResolvedOutcome==1 || FailureHitApplied);
        double attackEndsAt;
        int avoidNextTarget;
        public bool BarrierResolving=>State==KhezuHuntState.Caught && ResolvedSerial==CaptureSerial && ResolvedOutcome!=0;
        void EnsureBarrierTimer(){
            if(BarrierTimerSerial==CaptureSerial)return;
            BarrierTimerSerial=CaptureSerial;
            BarrierStartedAt=CaptureStartedAt+LogicLegends.OpenArchive.Cinematics.KhezuCinematicJumpscareController.Duration;
            BarrierDeadline=BarrierStartedAt+Mathf.Clamp(barrierTimeLimitSeconds,30,180);BarrierTimedOut=false;
        }
        void CancelBarrierSequence(){
            BarrierTimerSerial=0;BarrierStartedAt=BarrierDeadline=AttackHitAt=BarrierDissolveAt=StunEndsAt=0;
            FailureHitApplied=BarrierTimedOut=false;ResolvedOutcome=0;KnockbackPath=new Vector3[0];
        }
        public void ConfirmCaptureAnchor(Vector3 position){if(!IsAuthority || State!=KhezuHuntState.Caught || CaptureAnchorValid)return;CapturePosition=position;CaptureAnchorValid=true;SnapshotChanged?.Invoke();}
        public bool ResolveBarrierOutcome(int actor,int serial,bool success,float dissolveSeconds){
            if(!IsAuthority || State!=KhezuHuntState.Caught || EncounterPhase!=4 || BarrierResolving || actor!=CaughtPlayerId || serial!=CaptureSerial)return false;
            if(health==null || !health.HasAuthoritativeActorHealth || !health.IsActorAlive(actor) || monsterSpawnPoint==null ||
                EncounterClock-CaptureStartedAt<LogicLegends.OpenArchive.Cinematics.KhezuCinematicJumpscareController.Duration)return false;
            EnsureBarrierTimer();
            if(success && EncounterClock>=BarrierDeadline)return false;
            ResolvedActor=actor;ResolvedSerial=serial;ResolvedOutcome=success?1:2;ResolvedAt=EncounterClock;
            FailureHitApplied=false;KnockbackPath=new Vector3[0];
            if(success){BarrierDissolveAt=ResolvedAt;ReleaseAt=ResolvedAt+Mathf.Max(.1f,dissolveSeconds);Status="Barrier success: dissolving";}
            else{
                BarrierTimedOut=ResolvedAt>=BarrierDeadline;
                AttackHitAt=ResolvedAt+ClipSeconds("Attack")*Mathf.Clamp(failureAttackContact,.1f,.8f);
                attackEndsAt=ResolvedAt+ClipSeconds("Attack");StunEndsAt=AttackHitAt+FailureStunSeconds;
                BarrierDissolveAt=ReleaseAt=AttackHitAt;
                StopNavigation();animator.speed=1;animator.CrossFadeInFixedTime("Base Layer.Attack",.08f,0,0);
                Status=BarrierTimedOut?"Time expired: monster attacking":"Failed challenge: monster attacking";
            }
            SnapshotChanged?.Invoke();return true;
        }
        bool UpdateBarrierOutcome(){
            if(State==KhezuHuntState.Caught && EncounterPhase==4 && !BarrierResolving){
                bool newTimer=BarrierTimerSerial!=CaptureSerial;EnsureBarrierTimer();
                if(newTimer)SnapshotChanged?.Invoke();
                if(EncounterClock>=BarrierDeadline)ResolveBarrierOutcome(CaughtPlayerId,CaptureSerial,false,.9f);
            }
            if(BarrierResolving){
                if(ResolvedOutcome==2 && EncounterClock<AttackHitAt){
                    var toward=CapturePosition-transform.position;toward.y=0;
                    if(toward.sqrMagnitude>.01f)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(toward),turnSpeed*Time.unscaledDeltaTime);
                    return true;
                }
                if(EncounterClock<ReleaseAt)return true;
                int actor=ResolvedActor;
                if(ResolvedOutcome==2){
                    KnockbackPath=BuildFailureKnockback(actor);FailureHitApplied=true;
                    if(health!=null){health.DeductAuthoritativeBarrierHeart(actor,ResolvedSerial);CheckpointSharedHealth();}
                }
                StopNavigation();TargetPlayerId=CaughtPlayerId=0;EncounterPhase=0;pending=false;nextDestination=0;
                if(ResolvedOutcome==1){State=KhezuHuntState.ReturningToNest;Status="Returning to the Universal Monster Nest";}
                else{State=KhezuHuntState.AttackRecovery;Status="Player hit: knockback and five-second stun";avoidNextTarget=actor;}
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
            if(State==KhezuHuntState.AttackRecovery){
                animator.speed=1;if(EncounterClock>=attackEndsAt)Blend("Idle");
                if(EncounterClock>=System.Math.Max(attackEndsAt,StunEndsAt))BeginHunt();return true;
            }
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
            s.barrierTimerSerial=BarrierTimerSerial;s.barrierStartedAt=BarrierStartedAt;s.barrierDeadline=BarrierDeadline;s.barrierTimedOut=BarrierTimedOut;
            s.attackHitAt=AttackHitAt;s.barrierDissolveAt=BarrierDissolveAt;s.stunEndsAt=StunEndsAt;s.failureHitApplied=FailureHitApplied;s.knockbackPath=KnockbackPath;
            s.healthStates=health!=null&&string.IsNullOrEmpty(encounterId)?health.ExportActorHealth():new HealthManager.ActorHealth[0];
        }
        void RestoreBarrierSnapshot(KhezuSnapshot s){
            CapturePosition=s.capturePosition;CaptureAnchorValid=s.captureAnchorValid;ResolvedSerial=s.resolvedSerial;ResolvedActor=s.resolvedActor;ResolvedOutcome=s.resolvedOutcome;
            ResolvedAt=s.resolvedAt;ReleaseAt=s.releaseAt;NestSleepEndsAt=s.sleepEndsAt;NestArrivedAt=s.nestArrivedAt;attackEndsAt=s.attackEndsAt;avoidNextTarget=s.avoidNextTarget;
            BarrierTimerSerial=s.barrierTimerSerial;BarrierStartedAt=s.barrierStartedAt;BarrierDeadline=s.barrierDeadline;BarrierTimedOut=s.barrierTimedOut;
            AttackHitAt=s.attackHitAt;BarrierDissolveAt=s.barrierDissolveAt;StunEndsAt=s.stunEndsAt;FailureHitApplied=s.failureHitApplied;KnockbackPath=s.knockbackPath??new Vector3[0];
            if(health!=null&&s.healthStates!=null&&s.healthStates.Length>0)health.ApplyActorHealthSnapshot(s.healthStates);
        }
    }
}
