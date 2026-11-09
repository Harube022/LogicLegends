using UnityEngine;
using UnityEngine.EventSystems;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Owns one UI pointer. A tap, interruption or second finger cannot send a team signal.</summary>
    public sealed class ArchiveHoldSignalInput : MonoBehaviour,IPointerDownHandler,IPointerUpHandler,IDragHandler,IInitializePotentialDragHandler,IEndDragHandler,ICancelHandler
    {
        public ArchiveTeamCoordinationUI owner;
        [Range(.12f,.5f)]public float holdSeconds=.22f;
        int pointer;
        bool held,opened;
        float started;
        Vector2 origin,position,screen;
        public bool Holding=>held;
        public void OnPointerDown(PointerEventData e)
        {
            if(held||e.button!=PointerEventData.InputButton.Left||owner==null||!owner.CanOpenSignalWheel)return;
            pointer=e.pointerId;origin=position=e.position;screen=new Vector2(Screen.width,Screen.height);started=Time.unscaledTime;held=true;opened=false;
        }
        public void OnInitializePotentialDrag(PointerEventData e){if(held&&e.pointerId==pointer)e.useDragThreshold=false;}
        public void OnDrag(PointerEventData e){if(held&&e.pointerId==pointer){position=e.position;if(opened)owner.PointSignal(position);}}
        void Update()
        {
            if(!held)return;
            if(owner==null||!owner.CanOpenSignalWheel||screen!=new Vector2(Screen.width,Screen.height)){Abort();return;}
            float progress=Mathf.Clamp01((Time.unscaledTime-started)/holdSeconds);owner.SignalHoldProgress(progress);
            if(!opened&&progress>=1){opened=owner.OpenSignalWheel(origin);if(opened)owner.PointSignal(position);}
        }
        public void OnPointerUp(PointerEventData e)
        {
            if(!held||e.pointerId!=pointer||e.button!=PointerEventData.InputButton.Left)return;
            bool release=opened;held=opened=false;owner.SignalHoldProgress(0);
            if(release)owner.ReleaseSignal(e.position);else owner.HoldSignalHint();
        }
        public void OnEndDrag(PointerEventData e){if(held&&e.pointerId==pointer)Abort();}
        public void OnCancel(BaseEventData e)=>Abort();
        public void Abort(){held=opened=false;if(owner!=null){owner.SignalHoldProgress(0);owner.CloseSignalWheel();}}
        void OnDisable()=>Abort();
        void OnApplicationFocus(bool focused){if(!focused)Abort();}
        void OnApplicationPause(bool paused){if(paused)Abort();}
    }
}
