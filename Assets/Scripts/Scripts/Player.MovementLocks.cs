using System.Collections.Generic;
using UnityEngine;

public partial class Player
{
    sealed class MovementLock {public string reason;public Vector3 position;public int priority;}
    struct BodyState {public RigidbodyConstraints constraints;public bool gravity;}
    readonly Dictionary<Object,MovementLock> movementLocks=new Dictionary<Object,MovementLock>();
    readonly Dictionary<Rigidbody,BodyState> lockedBodies=new Dictionary<Rigidbody,BodyState>();
    readonly Dictionary<Animator,bool> lockedRootMotion=new Dictionary<Animator,bool>();
    readonly List<Object> expiredMovementOwners=new List<Object>();
    float nextLockLog;
    public bool IsMovementLocked {get{PruneMovementLocks();return movementLocks.Count>0;}}
    public Vector3 MovementLockPosition {
        get{var position=transform.position;int priority=int.MinValue;foreach(var entry in movementLocks)if(entry.Key!=null && entry.Value.priority>priority){position=entry.Value.position;priority=entry.Value.priority;}return position;}
    }
    public void AcquireMovementLock(Object owner,string reason,Vector3 position,int priority=0){
        if(owner==null)return;
        PruneMovementLocks();bool first=movementLocks.Count==0,added=!movementLocks.ContainsKey(owner);
        MovementLock record;if(!movementLocks.TryGetValue(owner,out record))movementLocks[owner]=record=new MovementLock();
        record.reason=reason;record.position=position;record.priority=priority;
        if(first){
            foreach(var body in GetComponentsInChildren<Rigidbody>(true))lockedBodies[body]=new BodyState{constraints=body.constraints,gravity=body.useGravity};
            foreach(var a in GetComponentsInChildren<Animator>(true))lockedRootMotion[a]=a.applyRootMotion;
        }
        EnforceMovementLock();
        if(added)MovementLockLog("Movement lock applied: "+reason+" on "+name);
    }
    public void ReleaseMovementLock(Object owner){
        if(ReferenceEquals(owner,null))return;
        MovementLock entry;if(!movementLocks.TryGetValue(owner,out entry))return;
        movementLocks.Remove(owner);MovementLockLog("Movement lock released: "+entry.reason+" on "+name);
        if(movementLocks.Count==0)RestoreMovementLockPhysics();
    }
    void PruneMovementLocks(){
        if(movementLocks.Count==0)return;
        expiredMovementOwners.Clear();foreach(var entry in movementLocks)if(entry.Key==null)expiredMovementOwners.Add(entry.Key);
        foreach(var owner in expiredMovementOwners)movementLocks.Remove(owner);
        if(movementLocks.Count==0)RestoreMovementLockPhysics();
    }
    public void EnforceMovementLock(){
        if(!IsMovementLocked)return;
        ResetMotionAfterTeleport();isWalking=false;isJumping=false;OffRun();
        var anchor=MovementLockPosition;
        if(controller!=null && controller.enabled)controller.Move(Vector3.zero);
        foreach(var entry in lockedRootMotion)if(entry.Key!=null)entry.Key.applyRootMotion=false;
        foreach(var entry in lockedBodies){
            var body=entry.Key;if(body==null)continue;
            if(!body.isKinematic){body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;}
            body.useGravity=false;body.constraints=RigidbodyConstraints.FreezeAll;
        }
        transform.position=anchor;
    }
    void RestoreMovementLockPhysics(){
        foreach(var entry in lockedBodies){var body=entry.Key;if(body==null)continue;
            if(body.constraints==RigidbodyConstraints.FreezeAll)body.constraints=entry.Value.constraints;
            if(!body.useGravity)body.useGravity=entry.Value.gravity;
        }
        foreach(var entry in lockedRootMotion)if(entry.Key!=null && !entry.Key.applyRootMotion)entry.Key.applyRootMotion=entry.Value;
        lockedBodies.Clear();lockedRootMotion.Clear();ResetMotionAfterTeleport();
        // Existing enabled/control/input restrictions remain owned by their original systems.
    }
    void LateUpdate(){if(IsMovementLocked)EnforceMovementLock();}
    void FixedUpdate(){if(IsMovementLocked)EnforceMovementLock();}
    void NoteRejectedMovement(){
        if(Time.unscaledTime<nextLockLog)return;nextLockLog=Time.unscaledTime+2;
        MovementLockLog("Movement commands rejected while locked: "+name);
    }
    [System.Diagnostics.Conditional("UNITY_EDITOR"),System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    static void MovementLockLog(string message){Debug.Log("[Khezu Barrier] "+message);}
}
