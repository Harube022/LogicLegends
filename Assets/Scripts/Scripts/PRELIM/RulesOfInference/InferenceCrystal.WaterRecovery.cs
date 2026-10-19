using System;
using UnityEngine;

namespace LogicLegends.Inference
{
    public partial class InferenceCrystal
    {
        public bool IsRecovering { get; private set; }
        Renderer water;
        Vector3 recoveryStart, recoveryLanding;
        float recoveryElapsed, recoveryCooldown;
        int waterRecoveryCount;
        const float RecoverySeconds=.85f;

        bool AboveWater(Vector3 point)
        {
            if(owner==null||owner.gameObject.scene.name!="RulesOfInference")return false;
            if(water==null)water=GameObject.Find("Ocean50x50W750H750")?.GetComponent<Renderer>();
            if(water==null)return false;
            var bounds=water.bounds;
            return point.x>=bounds.min.x&&point.x<=bounds.max.x&&point.z>=bounds.min.z&&point.z<=bounds.max.z;
        }
        void UpdateWaterRecovery(float delta)
        {
            if(IsRecovering)
            {
                recoveryElapsed+=delta;
                float t=Mathf.Clamp01(recoveryElapsed/RecoverySeconds);
                // A short hop carries the same object upward and outward to the shore.
                transform.position=Vector3.Lerp(recoveryStart,recoveryLanding,Mathf.SmoothStep(0,1,t))+Vector3.up*(3f*4*t*(1-t));
                if(t<1)return;
                if(!SupportedLanding(recoveryLanding)){ReturnToOrigin();return;}
                IsRecovering=false;recoveryCooldown=Time.time+2f;
                pickupCollider.enabled=true;
                // Recovered pickups rest like the original tower pickups. A later Drop
                // restores gravity normally; shore slopes cannot roll them back into water.
                body.isKinematic=true;body.useGravity=false;
                body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
                Physics.SyncTransforms();
                return;
            }
            if(IsPlaced||grabbable==null||grabbable.IsHeld||grabbable.isStoredInInventory||body==null||body.isKinematic||Time.time<recoveryCooldown)return;
            if(!AboveWater(transform.position)||pickupCollider==null||pickupCollider.bounds.min.y>water.bounds.max.y+.05f)return;
            waterRecoveryCount++;
            RestoreSolidCollider();
            recoveryStart=transform.position;
            if(!FindDryLanding(out recoveryLanding)){ReturnToOrigin();return;}
            body.linearVelocity=body.angularVelocity=Vector3.zero;
            body.isKinematic=true;body.useGravity=false;
            pickupCollider.enabled=false;
            transform.rotation=Quaternion.identity;
            IsRecovering=true;recoveryElapsed=0;trappedSince=-1;
        }
        float SolidRadius=>originalColliderRadius*Mathf.Max(Mathf.Abs(transform.lossyScale.x),Mathf.Max(Mathf.Abs(transform.lossyScale.y),Mathf.Abs(transform.lossyScale.z)));
        Vector3 SolidOffset=>Vector3.Scale(originalColliderCenter,transform.lossyScale);
        bool FindDryLanding(out Vector3 landing)
        {
            Vector3 outward=transform.position-water.bounds.center;outward.y=0;
            if(outward.sqrMagnitude<.01f){outward=origin-transform.position;outward.y=0;}
            outward=outward.sqrMagnitude<.01f?Vector3.forward:outward.normalized;
            for(float radius=4;radius<=24;radius+=4)
                for(int angle=0;angle<24;angle++)
                {
                    Vector3 sample=transform.position+Quaternion.Euler(0,angle*15,0)*outward*radius;
                    RaycastHit hit;
                    if(!DryGround(sample,out hit))continue;
                    landing=hit.point+Vector3.up*(SolidRadius+.18f)-SolidOffset;
                    if(SupportedLanding(landing))return true;
                }
            landing=origin;return false;
        }
        bool DryGround(Vector3 point,out RaycastHit result)
        {
            var from=new Vector3(point.x,Mathf.Max(water.bounds.max.y+40,origin.y+20),point.z);
            var hits=Physics.RaycastAll(from,Vector3.down,120,~0,QueryTriggerInteraction.Ignore);
            Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            foreach(var hit in hits)
            {
                // Prefer actual terrain/floor, rather than tree canopies or tower roofs.
                bool ground=hit.collider is TerrainCollider||hit.collider.name.StartsWith("FLOOR",StringComparison.OrdinalIgnoreCase);
                if(ground&&hit.normal.y>=.8f&&hit.point.y>water.bounds.max.y+.35f){result=hit;return true;}
            }
            result=default(RaycastHit);return false;
        }
        bool SupportedLanding(Vector3 position)
        {
            float radius=SolidRadius;RaycastHit hit;
            Vector3 center=position+SolidOffset;
            if(!DryGround(center,out hit)||Mathf.Abs(center.y-radius-.18f-hit.point.y)>.4f||IsEmbedded(position,.06f))return false;
            foreach(var offset in new[]{Vector3.left,Vector3.right,Vector3.forward,Vector3.back})
            {
                RaycastHit edge;
                if(!DryGround(center+offset*radius*.7f,out edge)||Mathf.Abs(edge.point.y-hit.point.y)>.45f)return false;
            }
            return true;
        }
    }
}
