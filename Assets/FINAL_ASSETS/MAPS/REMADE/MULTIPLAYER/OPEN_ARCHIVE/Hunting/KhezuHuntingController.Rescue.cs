using UnityEngine;
using UnityEngine.AI;

namespace LogicLegends.OpenArchive.Hunting
{
    public sealed partial class KhezuHuntingController
    {
        public int RescueActor { get; private set; }
        public bool CarryingRescueActor => RescueActor != 0 && State == KhezuHuntState.CarryingToNest;
        public bool RescueAtNest { get; private set; }
        public bool RescuePickedUp { get; private set; }
        public bool CanTranquilize => Initialized && !EntranceHeld && !CarryingRescueActor &&
            State != KhezuHuntState.Caught && State != KhezuHuntState.AttackRecovery &&
            State != KhezuHuntState.ReturningToNest && State != KhezuHuntState.NestSleeping && State != KhezuHuntState.Sleeping;
        public Vector3 RescueCarryAnchor
        {
            get
            {
                // World-space bounds also work for the two differently scaled existing rigs.
                var bounds = body != null ? body.bounds : new Bounds(transform.position + Vector3.up * 3, Vector3.one * 4);
                return transform.position + Vector3.up * Mathf.Clamp(bounds.size.y * .56f, 2, 5.5f) +
                    transform.right * Mathf.Clamp(bounds.extents.x * .55f, 1.4f, 3.8f) + transform.forward * .4f;
            }
        }
        public Vector3 RescueDropPoint(int actor)
        {
            var origin = monsterSpawnPoint != null ? monsterSpawnPoint : transform;
            // Separate rescue pads avoid stacking multiple incapacitated teammates in one capsule.
            int slot = Mathf.Abs(actor) % 4;
            float angle = (-55 + slot * 36) * Mathf.Deg2Rad;
            var desired = origin.position + origin.right * Mathf.Sin(angle) * 7 + origin.forward * Mathf.Cos(angle) * 7;
            var filter = new NavMeshQueryFilter { agentTypeID = Agent.agentTypeID, areaMask = Agent.areaMask };
            return NavMesh.SamplePosition(desired, out var hit, 5, filter) ? hit.position : origin.position + origin.forward * 4;
        }
        public bool BeginRescueCarry(int actor)
        {
            if (!IsAuthority || !Initialized || actor == 0 || monsterSpawnPoint == null || health == null || health.IsActorAlive(actor)) return false;
            if (CarryingRescueActor) return RescueActor == actor;
            if (EntranceHeld || State == KhezuHuntState.Caught || State==KhezuHuntState.Waking || State==KhezuHuntState.ReturningToNest || State==KhezuHuntState.NestSleeping || FailureStunActive || IsTranquilized) return false;
            ResetCloseCounter(true); StopNavigation();
            RescueActor = actor; RescueAtNest = RescuePickedUp = false;
            State = KhezuHuntState.CarryingToNest; TargetPlayerId = CaughtPlayerId = 0; EncounterPhase = 0;
            pending = false; animator.speed = 1; nextDestination = 0;
            Status = "Carrying a fallen teammate to the Universal Monster Nest";
            SnapshotChanged?.Invoke(); return true;
        }
        public void RestoreRescueCarry(int actor, bool arrived, bool pickedUp=false)
        {
            RescueActor = actor; RescueAtNest = arrived; RescuePickedUp = pickedUp;
        }
        public void EndRescueCarry(int actor)
        {
            if (!IsAuthority || RescueActor != actor) return;
            RescueActor = 0; RescueAtNest = RescuePickedUp = false;
            StopNavigation(); animator.speed = 1;
            if (fullyAwake) { State = KhezuHuntState.Ready; Blend("Idle"); BeginHunt(); }
            else { State = KhezuHuntState.Sleeping; driver.ApplySharedMistakes(0); }
            SnapshotChanged?.Invoke();
        }
        public void PublishRescueHealth()
        {
            if (!IsAuthority) return;
            SnapshotChanged?.Invoke(); CheckpointSharedHealth(); nextRoster = 0;
        }
        bool UpdateRescueCarry()
        {
            if (!CarryingRescueActor) return false;
            if (RescueAtNest) { StopNavigation(); Blend("Idle"); return true; }
            if (monsterSpawnPoint == null) { StopNavigation(); Status = "Carry requires the existing nest spawn"; return true; }
            if (!agent.enabled) agent.enabled = true;
            if (!agent.isOnNavMesh) { Status = "Carrying: waiting for valid navigation"; return true; }
            agent.autoBraking = true; agent.speed = normalPlayerRunSpeed * .72f; agent.acceleration = acceleration; agent.angularSpeed = turnSpeed;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            var victim = Topics.ArchiveTopicSession.Current?.ActorPlayer(RescueActor);
            if (!RescuePickedUp && victim == null) { StopNavigation(); Status = "Waiting for the fallen teammate to reconnect"; return true; }
            var target = victim == null ? null : new Target { transform=victim.transform, capsule=victim.GetComponent<CharacterController>(), name="Fallen teammate" };
            if (!RescuePickedUp && victim != null)
            {
                var toward=victim.transform.position-transform.position;toward.y=0;
                if(toward.sqrMagnitude>.01f)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(toward),turnSpeed*Time.deltaTime);
                if (CanPickUpRescue(target))
                { RescuePickedUp = true; nextDestination = 0; SnapshotChanged?.Invoke(); }
            }
            var destination = RescuePickedUp ? monsterSpawnPoint.position : victim.transform.position;
            NavMeshHit goal=default;
            if (RescuePickedUp&&!NavMesh.SamplePosition(destination, out goal, agent.radius+2, filter)) return true;
            var delta = goal.position - transform.position; delta.y = 0;
            if (RescuePickedUp && delta.magnitude < .9f && !agent.pathPending && (!agent.hasPath || agent.remainingDistance < 1.1f))
            {
                RescueAtNest = true; StopNavigation(); Blend("Idle"); animator.speed = 1;
                Status = "Fallen teammate delivered to the nest"; SnapshotChanged?.Invoke(); return true;
            }
            if (Time.time >= nextDestination)
            {
                nextDestination = Time.time + .5f;
                var path = new NavMeshPath();
                bool complete=RescuePickedUp ? NavMesh.CalculatePath(transform.position,goal.position,filter,path)&&path.status==NavMeshPathStatus.PathComplete : TryRescueApproach(target,filter,out path);
                if (complete&&path!=null&&path.corners.Length>1)
                {
                    // Rebuild on the agent's current polygon. Static query paths can belong
                    // to an overlapping bake at bridge seams and fail SetPath validation.
                    agent.isStopped=false;var corners=path.corners;
                    if(!agent.SetDestination(corners[corners.Length-1]))Status="Carrying: waiting for a valid agent destination";
                }
                else { agent.isStopped = true; Status = RescuePickedUp?"Carrying: waiting for a complete nest route":"Approaching the fallen teammate"; }
            }
            bool moving = !agent.isStopped && agent.velocity.sqrMagnitude > .09f;
            Blend(moving ? "Chase" : "Idle"); animator.speed = moving ? Mathf.Clamp(agent.velocity.magnitude / chaseAnimationReferenceSpeed, .65f, 1.4f) : 1;
            return true;
        }
        bool CanPickUpRescue(Target target)
        {
            if(target==null||body==null)return false;
            float feet=TargetFeet(target);
            // Existing reach tests already account for rubble, tree clearance and actual upper-body sight.
            if(feet-transform.position.y>elevatedFeetThreshold&&CanReachElevatedTarget(target))return true;
            if(CanReachGroundTarget(target))return true;
            Vector3 center=target.capsule!=null?target.capsule.bounds.center:target.transform.position+Vector3.up;
            return feet<=body.bounds.max.y && TargetSurfaceGap(target)<=2.5f && ClearUpperReach(body.bounds.center,center,target.transform);
        }
        bool TryRescueApproach(Target target,NavMeshQueryFilter filter,out NavMeshPath path)
        {
            path=null;if(target==null)return false;
            bool wet=waterPursuit!=null&&waterPursuit.ContainsTarget(target.transform,target.capsule);
            if(!wet&&NavMesh.SamplePosition(target.transform.position,out var directGoal,agent.radius+2,filter)){
                var direct=new NavMeshPath();
                if(NavMesh.CalculatePath(transform.position,directGoal.position,filter,direct)&&direct.status==NavMeshPathStatus.PathComplete&&direct.corners.Length>1){path=direct;return true;}
            }
            if(wet&&waterPursuit.TryRescueApproach(target.transform,agent,out path))return true;
            if(!wet&&landApproach.TryRoute(target.transform,TargetFeet(target),agent,catchObstructionMask,
                p=>ElevatedApproachPenalty(target,p),out path))
            {if(path==null&&agent.hasPath)path=agent.path;return path!=null;}
            if(NavMesh.SamplePosition(target.transform.position,out var goal,12,filter)&&
                NavMesh.CalculatePath(transform.position,goal.position,filter,path=new NavMeshPath())&&path.status==NavMeshPathStatus.PathComplete)return true;
            return false;
        }
        public bool TryRescueWaterRetrieval(int actor,Vector3 anchor,out Vector3 next)
        {
            next=anchor;
            if(!IsAuthority||!CarryingRescueActor||RescueActor!=actor||RescuePickedUp||agent==null||!agent.enabled||!agent.isOnNavMesh||
                agent.pathPending||(agent.hasPath&&agent.remainingDistance>1.4f))return false;
            var victim=Topics.ArchiveTopicSession.Current?.ActorPlayer(actor);if(victim==null)return false;
            var capsule=victim.GetComponent<CharacterController>();
            if(capsule==null||waterPursuit==null||!waterPursuit.ContainsTarget(victim.transform,capsule))return false;
            var toward=transform.position-anchor;toward.y=0;
            if(toward.magnitude>12||toward.magnitude<1.5f)return false;
            float step=Mathf.Min(toward.magnitude,1.5f*Mathf.Min(.1f,Time.unscaledDeltaTime));if(step<=0)return false;
            var center=capsule.bounds.center;float radius=Mathf.Max(.12f,capsule.bounds.extents.x*.85f);
            float half=Mathf.Max(0,capsule.bounds.extents.y-radius);var direction=toward.normalized;
            foreach(var hit in Physics.CapsuleCastAll(center+Vector3.up*half,center-Vector3.up*half,radius,direction,step+.04f,catchObstructionMask,QueryTriggerInteraction.Ignore))
            {
                if(hit.collider.transform.IsChildOf(victim.transform)||hit.collider.transform.IsChildOf(transform)||hit.normal.y>.45f)continue;
                step=Mathf.Min(step,Mathf.Max(0,hit.distance-.04f));
            }
            if(step<.0001f)return false;
            next=anchor+direction*step;Status="Retrieving fallen teammate from water";return true;
        }
    }
}
