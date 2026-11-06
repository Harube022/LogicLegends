using LogicLegends.OpenArchive.Topics;
using UnityEngine;
using UnityEngine.AI;

namespace LogicLegends.OpenArchive.Hunting
{
    public sealed partial class KhezuHuntingController
    {
        bool beaconWasActive;
        int beaconPathToken=-1;
        public bool IsBeaconDiverted=>beaconWasActive;
        public bool CanDivertToBeacon(Vector3 point,bool activation=true,float range=55)
        {
            if(!Initialized||!isActiveAndEnabled||EntranceHeld||IsTranquilized||CarryingRescueActor||
                (State!=KhezuHuntState.Hunting&&State!=KhezuHuntState.NavigationBlocked)||TargetPlayerId==0||
                CloseCounterPhase!=MonsterCloseCounterPhase.None||Agent==null)return false;
            float distance=ArchiveFieldEvents.Horizontal(transform.position,point);
            if(distance>range||activation&&distance<8)return false;
            // Once accepted, the live route is revalidated at the destination
            // cadence below, not with a new full path query on every frame.
            if(!activation)return true;
            var filter=new NavMeshQueryFilter{agentTypeID=Agent.agentTypeID,areaMask=Agent.areaMask};
            if(!NavMesh.SamplePosition(point,out var end,Agent.radius+2,filter)||Mathf.Abs(end.position.y-point.y)>2.5f||
                !NavMesh.SamplePosition(transform.position,out var start,Agent.radius+2,filter))return false;
            var path=new NavMeshPath();return NavMesh.CalculatePath(start.position,end.position,filter,path)&&path.status==NavMeshPathStatus.PathComplete;
        }
        public void CancelBeaconDiversion()
        {
            if(!beaconWasActive&&!HuntingPersonalitySuppressed)return;
            beaconWasActive=false;beaconPathToken=-1;HuntingPersonalitySuppressed=false;
            if(agent!=null)agent.autoBraking=false;
            nextDestination=nextCatch=0;
        }
        bool UpdateBeaconDiversion()
        {
            var session=ArchiveTopicSession.Current;var state=session?.State?.fieldEvents;
            var owner=session?.fieldEvents;
            var selected=state!=null&&state.beaconMonster==0?session?.hunting:session?.worldProgression?.secondary;
            var view=state!=null&&owner!=null?owner.Beacon(state.activeBeacon):null;
            if(state==null||owner==null||!owner.isActiveAndEnabled||view==null||selected!=this||state.activeBeacon<0||
                state.beaconHuntSerial!=HuntSerial||state.beaconTarget!=TargetPlayerId||EncounterClock>=state.beaconEndsAt||
                !CanDivertToBeacon(view.transform.position,false,owner.guardianHearingRange+10))
            {CancelBeaconDiversion();return false;}
            HuntingPersonalitySuppressed=true;CancelHuntingPersonality();
            // The audible rising chime is a real wind-up, not instant immunity.
            if(EncounterClock<state.beaconStartedAt+ArchiveFieldRules.BeaconWarningSeconds)return false;
            if(!agent.enabled)agent.enabled=true;
            if(!agent.isOnNavMesh){CancelBeaconDiversion();return false;}
            var receipt=state.beacons.Find(b=>b.id==state.activeBeacon);
            int token=state.activeBeacon*100000+(receipt?.serial??0);
            if(!beaconWasActive||beaconPathToken!=token||Time.time>=nextDestination)
            {
                var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
                var path=new NavMeshPath();
                if(!NavMesh.SamplePosition(view.transform.position,out var hit,agent.radius+2,filter)||
                    !NavMesh.CalculatePath(transform.position,hit.position,filter,path)||path.status!=NavMeshPathStatus.PathComplete||!agent.SetPath(path))
                {CancelBeaconDiversion();return false;}
                nextDestination=Time.time+.4f;beaconPathToken=token;
            }
            beaconWasActive=true;agent.speed=Mathf.Min(normalPlayerRunSpeed*1.15f,normalPlayerRunSpeed*chaseSpeedMultiplier);
            agent.angularSpeed=turnSpeed;agent.acceleration=acceleration;agent.autoBraking=true;
            agent.updateRotation=true;agent.isStopped=false;
            bool arrived=!agent.pathPending&&agent.remainingDistance<1.25f;
            if(arrived)agent.isStopped=true;
            Blend(arrived?"Idle":"Chase");animator.speed=arrived?1:Mathf.Clamp(agent.velocity.magnitude/chaseAnimationReferenceSpeed,.65f,1.6f);
            Status="Drawn to resonance beacon · original hunt resumes shortly";
            return true;
        }
    }
}
