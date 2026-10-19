using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LogicLegends.Tests.Khezu
{
    /// <summary>Manual Phase 1 harness only. No encounter, movement or networking dependencies.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class KhezuAwakeningTestController : MonoBehaviour, LogicLegends.OpenArchive.Hunting.IKhezuAwakeningDriver
    {
        [SerializeField] Animator animator;
        [SerializeField, Range(.1f, .6f)] float blendSeconds = .25f;
        static readonly string[] Stages = { "Sleep", "Wake_01", "Wake_02", "Wake_03", "Full_Awaken" };
        Vector3 anchorPosition, anchorScale;
        Quaternion anchorRotation;
        int mistakes, playingStage;
        bool preview;
        bool huntingControl, replica;
        public event System.Action MistakeRequested;
        public event System.Action SleepRequested;

        public void SetHuntingControl(bool value, bool isReplica) { huntingControl = value; replica = isReplica; }
        public void ApplySharedMistakes(int count)
        {
            count = Mathf.Clamp(count, 0, 4);
            if (count == 0) ResetLocal();
            else { preview = false; mistakes = count; }
        }
        public void ApplyReplicaCounters(int count, int stage) { mistakes = count; playingStage = stage; preview = false; }

        public Animator Animator { get { return animator; } }
        public int Mistakes { get { return mistakes; } }
        public int PlayingStage { get { return playingStage; } }
        public bool IsPreviewing { get { return preview; } }
        public float BlendSeconds { get { return blendSeconds; } }
        public string[] AvailableAnimations
        {
            get { return animator != null && animator.runtimeAnimatorController != null
                ? animator.runtimeAnimatorController.animationClips.Select(c => c.name).Distinct().ToArray()
                : new string[0]; }
        }
        public string CurrentState
        {
            get
            {
                if (!Application.isPlaying || animator == null || !animator.isInitialized) return "Not playing";
                var state = animator.GetCurrentAnimatorStateInfo(0);
                foreach (var name in AvailableAnimations) if (state.IsName(name)) return name;
                return "Transitioning";
            }
        }

        void Reset() { animator = GetComponent<Animator>(); }
        public void RefreshAssemblyAnchor()
        {
            anchorPosition = transform.localPosition;
            anchorRotation = transform.localRotation;
            anchorScale = transform.localScale;
        }
        void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            RefreshAssemblyAnchor();
            if (animator.runtimeAnimatorController == null || Stages.Any(n => !HasState(n)) || !HasState("Idle"))
            {
                Debug.LogError("Khezu test requires the existing ProofDevourer controller and all six awakening clips.", this);
                enabled = false;
                return;
            }
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            mistakes = playingStage = 0;
            preview = false;
            ClearTriggers();
            // Initialization only: first rendered frame is Sleep. All subsequent changes blend.
            animator.Play("Base Layer.Sleep", 0, 0);
        }

        void Update()
        {
            if (replica || preview || mistakes <= playingStage || animator.IsInTransition(0)) return;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            // Sleep may be interrupted at any breathing phase; each one-shot must finish.
            if (playingStage != 0 && (!state.IsName(Stages[playingStage]) || state.normalizedTime < 1)) return;
            playingStage++;
            BlendTo(Stages[playingStage]);
        }

        void LateUpdate()
        {
            // Lock only the prefab assembly, never the authored animated bones.
            if (!huntingControl)
            {
                transform.localPosition = anchorPosition;
                transform.localRotation = anchorRotation;
            }
            transform.localScale = anchorScale;
        }

        [ContextMenu("Test/Increase Mistake Count")]
        public void IncreaseMistakeCount()
        {
            if (!Application.isPlaying || !enabled) return;
            if (MistakeRequested != null) { MistakeRequested(); return; }
            if (preview) ResetTest();
            mistakes = Mathf.Min(4, mistakes + 1);
        }

        [ContextMenu("Test/Reset To Sleep")]
        public void ResetTest()
        {
            if (!Application.isPlaying || !enabled) return;
            if (SleepRequested != null) { SleepRequested(); return; }
            ResetLocal();
        }
        void ResetLocal()
        {
            mistakes = playingStage = 0;
            preview = false;
            BlendTo("Sleep");
        }

        public bool PreviewAnimation(string clipName)
        {
            if (huntingControl || !Application.isPlaying || !enabled || !AvailableAnimations.Contains(clipName) || !HasState(clipName)) return false;
            preview = true;
            BlendTo(clipName);
            return true;
        }

        bool HasState(string name) { return animator.HasState(0, Animator.StringToHash("Base Layer." + name)); }
        void ClearTriggers()
        {
            foreach (var parameter in animator.parameters)
                if (parameter.type == AnimatorControllerParameterType.Trigger) animator.ResetTrigger(parameter.nameHash);
        }
        void BlendTo(string name)
        {
            ClearTriggers();
            animator.CrossFadeInFixedTime("Base Layer." + name, blendSeconds, 0, 0);
        }
    }
}
