using UnityEngine;
using UnityEngine.AI;

namespace LogicLegends.OpenArchive.Hunting
{
    /// <summary>Finds the reachable side of a dry obstacle when the target is outside the monster's clearance.</summary>
    public sealed class KhezuLandApproach
    {
        Transform target;
        Vector3 lastTarget;
        float nextSearch;
        bool hasRoute;
        public void Reset(){target=null;hasRoute=false;nextSearch=0;}

        public static bool ClearLine(Vector3 from,Transform target,float feet,int obstructionMask){
            var to=target.position;from.y=to.y=Mathf.Max(from.y,feet)+1.5f;
            RaycastHit hit;
            return !Physics.Linecast(from,to,out hit,obstructionMask,QueryTriggerInteraction.Ignore) ||
                hit.transform==target || hit.transform.IsChildOf(target);
        }

        static bool ClearApproach(Vector3 from,Transform target,float feet,NavMeshAgent agent,int mask){
            if(feet-from.y<=.85f)return ClearLine(from,target,feet,mask);
            // Score a real upper-body view from the reachable lower level. The old
            // level ray floated above Khezu and could prefer the wrong side of a ledge.
            from.y+=agent.height*.7f;
            var to=target.position;to.y=Mathf.Max(to.y,feet+1.5f);
            RaycastHit hit;
            return !Physics.Linecast(from,to,out hit,mask,QueryTriggerInteraction.Ignore) ||
                hit.transform==target || hit.transform.IsChildOf(target);
        }

        public bool TryRoute(Transform selected,float feet,NavMeshAgent agent,int obstructionMask,System.Func<Vector3,float> elevatedReachPenalty,out NavMeshPath route){
            route=null;
            bool moved=target!=selected || FlatSquared(selected.position-lastTarget)>4;
            bool valid=agent.hasPath && agent.pathStatus==NavMeshPathStatus.PathComplete;
            if(hasRoute && !moved && Time.time<nextSearch && valid)return true;
            target=selected;lastTarget=selected.position;nextSearch=Time.time+.6f;
            var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
            float best=float.MaxValue;
            // Several small samples find alternatives around a pillar; one large nearest-point
            // query can repeatedly choose the wrong side of the same wall or an isolated region.
            bool elevated=feet-agent.transform.position.y>.85f;
            for(int level=0;level<(elevated?2:1);level++)for(int ring=0;ring<4;ring++){
                int count=ring==0?1:12;
                float radius=ring*agent.radius*.85f;
                for(int i=0;i<count;i++){
                    float angle=i*Mathf.PI*2/count;
                    Vector3 point=selected.position+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius;
                    point.y=level==0?feet:agent.transform.position.y;
                    NavMeshHit hit;
                    if(!NavMesh.SamplePosition(point,out hit,ring==0?agent.radius*2:agent.radius*.65f,filter))continue;
                    if(Mathf.Abs(hit.position.y-feet)>agent.height)continue;
                    var path=new NavMeshPath();
                    if(!NavMesh.CalculatePath(agent.transform.position,hit.position,filter,path) || path.status!=NavMeshPathStatus.PathComplete)continue;
                    float exposure=elevated && elevatedReachPenalty!=null?elevatedReachPenalty(hit.position):
                        (ClearApproach(hit.position,selected,feet,agent,obstructionMask)?0:40);
                    float length=0;var corners=path.corners;
                    for(int c=1;c<corners.Length;c++)length+=Vector3.Distance(corners[c-1],corners[c]);
                    float score=Mathf.Sqrt(FlatSquared(hit.position-selected.position))+Mathf.Abs(hit.position.y-feet)*2+length*.04f+exposure;
                    // When an obstruction still covers the player, explore another reachable
                    // side rather than continuously returning an already reached blocked point.
                    if(exposure>0 && FlatSquared(hit.position-agent.transform.position)<4)score+=30;
                    if(score>=best)continue;
                    best=score;route=path;
                }
            }
            hasRoute=route!=null;
            return hasRoute || valid;
        }
        static float FlatSquared(Vector3 p)=>p.x*p.x+p.z*p.z;
    }
}
