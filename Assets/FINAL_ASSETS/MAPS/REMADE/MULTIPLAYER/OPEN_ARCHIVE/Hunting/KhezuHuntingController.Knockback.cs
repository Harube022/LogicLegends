using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using UnityEngine;

namespace LogicLegends.OpenArchive.Hunting
{
    public sealed partial class KhezuHuntingController
    {
        // The Master traces one short collision-safe trajectory. All peers play this same
        // checkpointed path; owner movement packets cannot override the encounter lock.
        Vector3[] BuildFailureKnockback(int actor)
        {
            var player=FindObjectsByType<Player>(FindObjectsSortMode.None).FirstOrDefault(p=>{
                if(!PhotonNetwork.InRoom)return actor==-1 && p==Player.LocalInstance;
                var view=p.GetComponent<PhotonView>();return view!=null && view.ViewID!=0 && view.OwnerActorNr==actor;
            });
            var path=new List<Vector3>{CapturePosition};
            if(player==null || failureKnockbackDistance<=0)return path.ToArray();
            var cc=player.GetComponent<CharacterController>();
            if(cc==null)return path.ToArray();
            Vector3 scale=player.transform.lossyScale;
            float radius=cc.radius*Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.z));
            float half=Mathf.Max(0,cc.height*Mathf.Abs(scale.y)*.5f-radius);
            var center=player.transform.TransformVector(cc.center);
            float footOffset=center.y-half-radius;
            Vector3 direction=CapturePosition-transform.position;direction.y=0;
            if(direction.sqrMagnitude<.01f)direction=transform.forward;
            direction.Normalize();float step=.25f;
            for(float travelled=step;travelled<=Mathf.Min(8,failureKnockbackDistance)+.001f;travelled+=step){
                Vector3 previous=path[path.Count-1],next=CapturePosition+direction*travelled;
                var ground=Physics.RaycastAll(next+Vector3.up*(footOffset+.65f),Vector3.down,1.35f,~(1<<7),QueryTriggerInteraction.Ignore)
                    .Where(h=>!h.transform.IsChildOf(player.transform) && !h.transform.IsChildOf(transform) && h.normal.y>.65f)
                    .OrderBy(h=>h.distance).ToArray();
                if(ground.Length==0)break; // Never push the player off a ledge or into deep water.
                float floor=ground[0].point.y;
                // Support the capsule footprint, not just its center. A center ray alone
                // can leave the player hanging over the corner of a narrow rock/ledge.
                bool supported=true;
                foreach(var offset in new[]{Vector3.right,Vector3.left,Vector3.forward,Vector3.back}){
                    var probe=next+offset*radius*.9f;probe.y=floor+.35f;
                    if(!Physics.RaycastAll(probe,Vector3.down,.65f,~(1<<7),QueryTriggerInteraction.Ignore)
                        .Any(h=>!h.transform.IsChildOf(player.transform) && !h.transform.IsChildOf(transform) && h.normal.y>.65f)){
                        supported=false;break;
                    }
                }
                if(!supported)break;
                next.y=floor-footOffset+cc.skinWidth;
                if(Mathf.Abs(next.y-previous.y)>.55f)break;
                Vector3 delta=next-previous;float distance=delta.magnitude;
                if(distance<.001f)continue;
                Vector3 c=previous+center;
                var hits=Physics.CapsuleCastAll(c+Vector3.up*half,c-Vector3.up*half,Mathf.Max(.05f,radius-.025f),delta/distance,distance+.08f,~(1<<7),QueryTriggerInteraction.Ignore);
                if(hits.Any(h=>!h.transform.IsChildOf(player.transform) && !h.transform.IsChildOf(transform) && h.normal.y<.65f))break;
                path.Add(next);
            }
            return path.ToArray();
        }
        public Vector3 FailurePlayerPosition
        {
            get{
                if(KnockbackPath==null || KnockbackPath.Length==0)return CapturePosition;
                float progress=Mathf.Clamp01((float)(EncounterClock-AttackHitAt)/KnockbackSeconds);
                float index=Mathf.SmoothStep(0,1,progress)*(KnockbackPath.Length-1);
                int a=Mathf.FloorToInt(index),b=Mathf.Min(a+1,KnockbackPath.Length-1);
                return Vector3.Lerp(KnockbackPath[a],KnockbackPath[b],index-a);
            }
        }
    }
}
