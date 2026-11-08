using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>One library-only agent. Paths and animation choices belong to segments.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public sealed class WizardLibraryRoaming : MonoBehaviour
{
    public Bounds interior=new Bounds(new Vector3(-126,21.1f,670.5f),new Vector3(34,1,32));
    [Min(.1f)] public float movementSpeed=2.4f;
    public Vector2 restSeconds=new Vector2(1.5f,3.5f);
    public NavMeshAgent agent;
    public WizardActor actor;
    public bool Paused { get; private set; }=true;
    public int SegmentCount { get; private set; }
    public int Walking1Segments { get; private set; }
    public int CurrentVariant { get; private set; }
    public int RejectedDestinations { get; private set; }
    public int BlockedSegments { get; private set; }
    public Vector3 Destination { get; private set; }
    readonly System.Random random=new System.Random();
    readonly RaycastHit[] hits=new RaycastHit[24];
    NavMeshPath candidate;
    float waitUntil,stalledTime;
    Vector3 lastPosition;

    void Awake()
    {
        candidate=new NavMeshPath();
        if(actor==null)actor=GetComponent<WizardActor>();if(agent==null)agent=GetComponent<NavMeshAgent>();
        agent.updatePosition=false;agent.updateRotation=false;agent.autoTraverseOffMeshLink=false;
        agent.speed=movementSpeed;lastPosition=transform.position;
    }
    public static int ChooseWalkingVariant(double roll){return roll<.9?0:1;}
    public void SetPaused(bool paused)
    {
        if(Paused==paused)return;Paused=paused;
        if(paused)StopSegment();else waitUntil=Time.time+.3f;
    }
    public bool Inside(Vector3 point)
    {return point.x>=interior.min.x&&point.x<=interior.max.x&&point.z>=interior.min.z&&point.z<=interior.max.z&&Mathf.Abs(point.y-interior.center.y)<.75f;}
    public bool TryDestination(Vector3 point)
    {
        if(Paused||agent==null||!agent.isOnNavMesh||!Inside(point)){RejectedDestinations++;return false;}
        NavMeshHit hit;
        var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
        if(!NavMesh.SamplePosition(point,out hit,1,filter)||!Inside(hit.position)||
            !agent.CalculatePath(hit.position,candidate)||candidate.status!=NavMeshPathStatus.PathComplete)
        {RejectedDestinations++;return false;}
        foreach(var corner in candidate.corners)if(!Inside(corner)){RejectedDestinations++;return false;}
        if(Vector3.Distance(transform.position,hit.position)<4){RejectedDestinations++;return false;}
        if(!agent.SetPath(candidate)){RejectedDestinations++;return false;}
        CurrentVariant=ChooseWalkingVariant(random.NextDouble());SegmentCount++;if(CurrentVariant==1)Walking1Segments++;
        Destination=hit.position;agent.isStopped=false;stalledTime=0;lastPosition=transform.position;return true;
    }
    void Update()
    {
        if(Paused||Time.timeScale<=0||agent==null||!agent.isOnNavMesh)return;
        if(!agent.hasPath)
        {
            actor.ReturnIdle();if(Time.time<waitUntil)return;
            for(int attempt=0;attempt<24;attempt++)
            {
                var point=new Vector3(Mathf.Lerp(interior.min.x,interior.max.x,(float)random.NextDouble()),interior.center.y,
                    Mathf.Lerp(interior.min.z,interior.max.z,(float)random.NextDouble()));
                if(TryDestination(point))return;
            }
            waitUntil=Time.time+1;return;
        }
        if(agent.pathStatus!=NavMeshPathStatus.PathComplete){FinishSegment(true);return;}
        Vector3 next=agent.nextPosition,delta=next-transform.position;float distance=delta.magnitude;
        if(!Inside(next)||Blocked(delta,distance)){FinishSegment(true);return;}
        transform.position=next;
        float speed=distance/Mathf.Max(.0001f,Time.deltaTime);
        if(delta.sqrMagnitude>.0001f)
        {
            delta.y=0;if(delta.sqrMagnitude>.0001f)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(delta),180*Time.deltaTime);
        }
        actor.SetWalking(speed,CurrentVariant);
        if(Vector3.Distance(lastPosition,transform.position)<.01f)stalledTime+=Time.deltaTime;else stalledTime=0;
        lastPosition=transform.position;
        if(stalledTime>1.3f){FinishSegment(true);return;}
        if(!agent.pathPending&&agent.remainingDistance<=agent.stoppingDistance+.12f)FinishSegment(false);
    }
    bool Blocked(Vector3 delta,float distance)
    {
        if(distance<.001f)return false;
        float radius=agent.radius*.9f;
        int count=Physics.CapsuleCastNonAlloc(transform.position+Vector3.up*(radius+.15f),
            transform.position+Vector3.up*(agent.height-radius),radius,delta.normalized,hits,distance+.15f,~0,QueryTriggerInteraction.Ignore);
        for(int i=0;i<count;i++)if(hits[i].collider!=null&&!hits[i].transform.IsChildOf(transform))return true;
        return false;
    }
    void FinishSegment(bool blocked)
    {
        if(blocked)BlockedSegments++;StopSegment();
        waitUntil=Time.time+(blocked?.4f:Mathf.Lerp(restSeconds.x,restSeconds.y,(float)random.NextDouble()));
    }
    void StopSegment()
    {
        if(agent!=null&&agent.isOnNavMesh){agent.isStopped=true;agent.ResetPath();agent.velocity=Vector3.zero;agent.nextPosition=transform.position;}
        if(actor!=null)actor.ReturnIdle();stalledTime=0;
    }
    void OnDisable(){StopSegment();Paused=true;}
}
