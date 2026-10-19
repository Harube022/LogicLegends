using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.Inference
{
    /// <summary>One shared hand/context pickup path for the standalone stage.</summary>
    public sealed class InferenceCrystalInteraction : MonoBehaviour
    {
        public static InferenceCrystalInteraction Active { get; private set; }
        public InferenceChallenge Challenge { get; private set; }
        public InferenceCrystal AvailableCrystal { get; private set; }
        [Min(.5f)] public float interactionRange = 2.4f;
        [Min(.5f)] public float detectionRange = 8f;
        public void Initialize(InferenceChallenge owner) { Challenge = owner; Active = this; }
        void Update()
        {
            AvailableCrystal = FindAvailable();
        }
        public bool CanShowBubble(InferenceCrystal crystal)
        {
            if(!CanUse()||Player.LocalInstance.GetHeldObject()!=null||crystal==null||crystal.IsPlaced||crystal.IsRecovering||
                crystal.QuestionId!=Challenge.Question?.Id)return false;
            var grab=crystal.GetComponent<GrabbableObject>();var collider=crystal.GetComponent<SphereCollider>();
            return crystal.gameObject.activeInHierarchy&&!grab.IsHeld&&!grab.isStoredInInventory&&collider!=null&&collider.enabled;
        }
        public bool IsWithinRange(InferenceCrystal crystal)
        {return DistanceTo(crystal)<=interactionRange;}
        public bool IsWithinDetectionRange(InferenceCrystal crystal)
        {return DistanceTo(crystal)<=Mathf.Max(detectionRange,interactionRange);}
        float DistanceTo(InferenceCrystal crystal)
        {
            if(crystal==null||Player.LocalInstance==null)return float.PositiveInfinity;
            var player=Player.LocalInstance;var controller=player.GetComponent<CharacterController>();
            var point=controller!=null&&controller.enabled?controller.bounds.center:player.transform.position+Vector3.up;
            var collider=crystal.GetComponent<SphereCollider>();
            return collider!=null&&collider.enabled?Vector3.Distance(point,collider.ClosestPoint(point)):float.PositiveInfinity;
        }
        bool CanUse()
        {
            return Challenge != null && Challenge.HasStarted && !Challenge.PuzzleCompleted && !Challenge.IsBoardOpen &&
                Challenge.Stage == InferencePuzzleStage.FindingDiamond && Player.LocalInstance != null && Player.LocalInstance.enabled &&
                GameInput.Instance != null && !GameInput.Instance.GameplayInputBlocked && Time.timeScale > 0f &&
                (InferenceControlsOnboarding.Active == null || !InferenceControlsOnboarding.Active.IsActive);
        }
        InferenceCrystal FindAvailable()
        {
            if (!CanUse() || Player.LocalInstance.GetHeldObject() != null) return null;
            var player = Player.LocalInstance; var controller = player.GetComponent<CharacterController>();
            Vector3 point = controller != null && controller.enabled ? controller.bounds.center : player.transform.position + Vector3.up;
            InferenceCrystal best = null; float distance = interactionRange;
            foreach (var crystal in Challenge.Crystals)
            {
                if (crystal == null || !crystal.gameObject.activeInHierarchy || crystal.IsPlaced || crystal.IsRecovering || crystal.QuestionId != Challenge.Question?.Id) continue;
                var grab = crystal.GetComponent<GrabbableObject>(); var collider = crystal.GetComponent<SphereCollider>();
                if (grab.IsHeld || grab.isStoredInInventory || collider == null || !collider.enabled) continue;
                float current = Vector3.Distance(point, collider.ClosestPoint(point));
                if (current <= distance) { distance = current; best = crystal; }
            }
            return best;
        }
        public bool TryHandleInteraction()
        {
            if (!CanUse()) return false;
            var player = Player.LocalInstance; var held = player.GetHeldObject();
            if (held != null && held.GetComponent<InferenceCrystal>() != null)
            {
                held.Drop(); player.SetHeldObjectSilently(null); return true;
            }
            var crystal = FindAvailable();
            if (crystal == null) return false;
            if(crystal.PickupQuizCompleted)
            {
                CollectAfterQuiz(crystal,false);
                return true;
            }
            var quiz = Challenge.GetComponent<InferenceCrystalQuiz>();
            if (quiz == null) return false;
            // Only a crystal's first successful collection needs a quiz this challenge.
            if (!quiz.Open(() => CollectAfterQuiz(crystal,true))) return true;
            AvailableCrystal = null;
            return true;
        }
        void CollectAfterQuiz(InferenceCrystal crystal,bool completedQuiz)
        {
            if (!CanUse() || crystal == null || crystal.IsPlaced || crystal.QuestionId != Challenge.Question?.Id) return;
            var player = Player.LocalInstance;
            var grab = crystal.GetComponent<GrabbableObject>();
            if (player.GetHeldObject() != null || grab.IsHeld || grab.isStoredInInventory) return;
            grab.Grab(player.GetHoldPoint()); player.SetHeldObjectSilently(grab);
            if(completedQuiz&&grab.IsHeld)crystal.MarkPickupQuizCompleted();
            AvailableCrystal = null;
        }
        void OnDisable() { if (Active == this) Active = null; }
        void OnEnable() { if (Challenge != null) Active = this; }
    }
}
