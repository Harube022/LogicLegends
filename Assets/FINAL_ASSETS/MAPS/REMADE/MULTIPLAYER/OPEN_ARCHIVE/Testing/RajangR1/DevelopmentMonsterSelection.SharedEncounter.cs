using System;
using LogicLegends.OpenArchive.Cinematics;
using LogicLegends.OpenArchive.Cinematics.VFX;
using LogicLegends.OpenArchive.Hunting;
using LogicLegends.Tests.Khezu;
using Photon.Pun;
using UnityEngine;

namespace LogicLegends.Tests.Monsters
{
    [Serializable]
    public sealed class DevelopmentMonsterProfile
    {
        public GameObject[] visualRoots;
        public Avatar avatar;
        public RuntimeAnimatorController controller;
        public GameObject presentationPrefab;
        public CrimsonMathBarrierStyle barrierStyle;
        public AnimationClip capture, jumpscare, barrierCast;
        public Vector3 localScale = Vector3.one, nestEuler, nestOffset;
        public Vector3 bodyCenter, bodySize;
        public string mouthUpper = "Mouth_Upper", mouthLower = "Mouth_Lower";
        public Vector3 faceOffset;
        public float framingDistance = 1;
        public int agentTypeId;
        public float agentRadius = 9.7f, agentHeight = 10.2f;
    }

    public sealed partial class DevelopmentMonsterSelection
    {
        [Header("Optional shared encounter test - R1 leaves these empty")]
        [SerializeField] KhezuAwakeningTestController sharedDriver;
        [SerializeField] Transform sharedSpawn;
        [SerializeField] DevelopmentMonsterProfile khezuProfile, rajangProfile;
        public bool HasSharedEncounter => sharedDriver != null;
        public DevelopmentMonsterProfile RajangEncounterProfile => rajangProfile;
        public bool CanSwap => SwapBlockedReason.Length == 0;
        public string SwapBlockedReason
        {
            get
            {
                if (!HasSharedEncounter || !Application.isPlaying) return "";
                if (PhotonNetwork.InRoom) return "Swap is an offline development control.";
                var cinematic = sharedDriver.GetComponent<KhezuCinematicJumpscareController>();
                if (cinematic != null && cinematic.IsPlaying) return "Wait for the POV preview to finish.";
                var hunt = sharedDriver.GetComponent<KhezuHuntingController>();
                if (hunt != null && (hunt.State == KhezuHuntState.Caught || hunt.State == KhezuHuntState.ReturningToNest ||
                    hunt.State == KhezuHuntState.NestSleeping || hunt.State == KhezuHuntState.AttackRecovery))
                    return "Finish the captured player's challenge and recovery first.";
                return "";
            }
        }
        public void ConfigureShared(KhezuAwakeningTestController driver, Transform spawn,
            DevelopmentMonsterProfile khezuVisual, DevelopmentMonsterProfile rajangVisual)
        {
            sharedDriver = khezu = rajang = driver;
            sharedSpawn = spawn; khezuProfile = khezuVisual; rajangProfile = rajangVisual;
            SelectSharedEncounter(DevelopmentMonster.Rajang);
        }
        public void SwapMonster() { if (CanSwap) Select(selected == DevelopmentMonster.Rajang ? DevelopmentMonster.Khezu : DevelopmentMonster.Rajang); }
        void SelectSharedEncounter(DevelopmentMonster monster)
        {
            if (!CanSwap) return;
            var profile = monster == DevelopmentMonster.Rajang ? rajangProfile : khezuProfile;
            if (profile == null || profile.avatar == null || profile.controller == null || sharedSpawn == null)
            { Debug.LogError("Assign both existing monster rig profiles to the development selector.", this); return; }
            var hunt = sharedDriver.GetComponent<KhezuHuntingController>();
            if (Application.isPlaying && hunt != null && hunt.Initialized) hunt.Request(KhezuCommand.Sleep);
            var agent = sharedDriver.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null)
            {
                agent.enabled = false;
                agent.agentTypeID = profile.agentTypeId; agent.radius = profile.agentRadius; agent.height = profile.agentHeight;
            }
            // Only visual assemblies change. One existing controller retains rotation, health and capture serials.
            foreach (var group in new[] { khezuProfile, rajangProfile })
                foreach (var root in group.visualRoots) root.SetActive(false);
            foreach (var root in profile.visualRoots) root.SetActive(true);
            var actor = sharedDriver.transform;
            actor.localScale = profile.localScale;
            actor.SetPositionAndRotation(sharedSpawn.position + profile.nestOffset, sharedSpawn.rotation * Quaternion.Euler(profile.nestEuler));
            var animator = sharedDriver.Animator;
            animator.avatar = profile.avatar; animator.runtimeAnimatorController = profile.controller;
            animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var body = sharedDriver.GetComponent<BoxCollider>();
            body.center = profile.bodyCenter; body.size = profile.bodySize;
            var cinematic = sharedDriver.GetComponent<KhezuCinematicJumpscareController>();
            cinematic.presentationPrefab = profile.presentationPrefab;
            cinematic.capture = profile.capture; cinematic.jumpscare = profile.jumpscare; cinematic.barrierCast = profile.barrierCast;
            cinematic.mouthUpperBone = profile.mouthUpper; cinematic.mouthLowerBone = profile.mouthLower;
            cinematic.faceTargetOffset = profile.faceOffset; cinematic.framingDistance = profile.framingDistance;
            var barrier = sharedDriver.GetComponent<KhezuBarrierVfxPresentation>();
            if (barrier != null) barrier.styleOverride = profile.barrierStyle;
            selected = monster;
            sharedDriver.RefreshAssemblyAnchor();
            if (Application.isPlaying)
            {
                animator.Rebind(); animator.Play("Base Layer.Sleep", 0, 0); animator.Update(0);
                sharedDriver.ApplySharedMistakes(0);
                if (hunt != null && hunt.Initialized) hunt.RefreshAnimationRig();
            }
            else
                foreach (var clip in profile.controller.animationClips) if (clip.name == "Sleep") { clip.SampleAnimation(actor.gameObject, 0); break; }
        }
    }
}
