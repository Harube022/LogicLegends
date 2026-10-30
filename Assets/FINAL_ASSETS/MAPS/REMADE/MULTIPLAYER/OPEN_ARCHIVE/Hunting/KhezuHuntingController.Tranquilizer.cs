using UnityEngine;

namespace LogicLegends.OpenArchive.Hunting
{
    public sealed partial class KhezuHuntingController
    {
        [Header("Tranquilizer sleep presentation")]
        [Range(.15f,.6f)] public float tranquilizedSleepBlendSeconds=.4f;
        public bool IsTranquilized { get; private set; }
        public Vector3 TranquilizedAnchorPosition { get; private set; }
        public Quaternion TranquilizedAnchorRotation { get; private set; }
        public bool IsTranquilizedSleepPose => IsTranquilized && animator!=null &&
            ((!animator.IsInTransition(0)&&animator.GetCurrentAnimatorStateInfo(0).IsName("Sleep")) ||
            (animator.IsInTransition(0)&&animator.GetNextAnimatorStateInfo(0).IsName("Sleep")));
        double tranquilizedResumeAt=double.NegativeInfinity;
        string tranquilizedResumeAnimation="Idle";

        /// <summary>The existing topic checkpoint owns duration and immunity; this only presents its held state.</summary>
        public void HoldTranquilized(bool held)
        {
            if (held && !CanTranquilize && !IsTranquilized) return;
            if (IsTranquilized == held) return;
            if(held)
            {
                // Capture world pose before disabling navigation. Sleep changes only the authored
                // bones; it must not invoke the nest-sleep command or reset awakening/rotation.
                TranquilizedAnchorPosition=transform.position;
                TranquilizedAnchorRotation=transform.rotation;
                IsTranquilized=true;
                ResetCloseCounter(true);StopNavigation();
                MaintainTranquilizedSleep();
                Status="Tranquilized — sleeping at the hit location";
            }
            else
            {
                IsTranquilized=false;
                driver.SetHuntingControl(true,!IsAuthority);
                animator.speed=1;animator.applyRootMotion=false;
                nextDestination=nextCatch=0;
                tranquilizedResumeAnimation=ResumeAnimationAfterTranquilizer();
                tranquilizedResumeAt=EncounterClock;
                animator.CrossFadeInFixedTime("Base Layer."+tranquilizedResumeAnimation,tranquilizedSleepBlendSeconds,0,0);
                Status=State.ToString();
            }
            SnapshotChanged?.Invoke();
        }
        string ResumeAnimationAfterTranquilizer()
        {
            // A dart may interrupt an unfinished awakening one-shot. Resume that existing
            // stage without changing the mistake/stage counters or replaying solved stages.
            if(State==KhezuHuntState.Waking && driver.PlayingStage>0 && driver.PlayingStage<=4)
                return new[]{"Sleep","Wake_01","Wake_02","Wake_03","Full_Awaken"}[driver.PlayingStage];
            return State==KhezuHuntState.Sleeping||State==KhezuHuntState.NestSleeping?"Sleep":"Idle";
        }
        void MaintainTranquilizedSleep()
        {
            if(!IsTranquilized||animator==null)return;
            StopNavigation();
            // The existing awakening driver must not advance a queued wake stage over
            // the Sleep pose. Replica mode pauses that driver without altering counters.
            driver.SetHuntingControl(true,true);
            animator.applyRootMotion=false;animator.speed=1;
            bool sleeping=(!animator.IsInTransition(0)&&animator.GetCurrentAnimatorStateInfo(0).IsName("Sleep")) ||
                (animator.IsInTransition(0)&&animator.GetNextAnimatorStateInfo(0).IsName("Sleep"));
            if(!sleeping)animator.CrossFadeInFixedTime("Base Layer.Sleep",tranquilizedSleepBlendSeconds,0,0);
            Status="Tranquilized — sleeping at the hit location";
        }
        void HoldTranquilizedRoot()
        {
            if(!Initialized||MultiplayerLobbyService.IsGameplayRecovering)return;
            MaintainTranquilizedSleep();
            if(IsAuthority)
            {
                // Apply after Animator evaluation too: root-curve bindings may otherwise
                // move a model even with root motion disabled on the imported rig.
                transform.SetPositionAndRotation(TranquilizedAnchorPosition,TranquilizedAnchorRotation);
            }
            else if(replica!=null)
            {
                // A remote client may be interpolating the last in-flight movement packet
                // when sedation arrives. Settle smoothly onto the authority's stationary pose.
                TranquilizedAnchorPosition=replica.position;TranquilizedAnchorRotation=replica.rotation;
                float alpha=1-Mathf.Exp(-18*Time.deltaTime);
                transform.position=Vector3.Lerp(transform.position,TranquilizedAnchorPosition,alpha);
                transform.rotation=Quaternion.Slerp(transform.rotation,TranquilizedAnchorRotation,alpha);
            }
        }
    }
}
