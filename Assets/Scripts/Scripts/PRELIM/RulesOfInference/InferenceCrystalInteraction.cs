using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.Inference
{
    /// <summary>One shared hand/context pickup path for the standalone stage.</summary>
    public sealed class InferenceCrystalInteraction : MonoBehaviour
    {
        public static InferenceCrystalInteraction Active { get; private set; }
        public InferenceChallenge Challenge { get; private set; }
        public Button CollectButton { get; private set; }
        public InferenceCrystal AvailableCrystal { get; private set; }
        [Min(.5f)] public float interactionRange = 2.4f;
        public void Initialize(InferenceChallenge owner) { Challenge = owner; Active = this; }
        void Update()
        {
            if (CollectButton == null)
            {
                var canvas = GameObject.Find("Canvas 1"); var safe = canvas != null ? canvas.transform.Find("SafeArea") : null;
                if (safe == null) return;
                CollectButton = StageUITheme.Button("CollectRuleCrystalButton", safe, "Collect Rule Crystal", Collect);
                var rect = (RectTransform)CollectButton.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 0f);
                rect.anchoredPosition = new Vector2(0f, 330f); rect.sizeDelta = new Vector2(390f, 88f);
            }
            AvailableCrystal = FindAvailable();
            var tutorial = InferenceControlsOnboarding.Active;
            bool practice = tutorial != null && tutorial.IsCrystalPractice && !GameInput.Instance.GameplayInputBlocked;
            CollectButton.gameObject.SetActive(practice || AvailableCrystal != null);
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
                if (crystal == null || !crystal.gameObject.activeInHierarchy || crystal.IsPlaced || crystal.QuestionId != Challenge.Question?.Id) continue;
                var grab = crystal.GetComponent<GrabbableObject>(); var collider = crystal.GetComponent<SphereCollider>();
                if (grab.IsHeld || grab.isStoredInInventory || collider == null || !collider.enabled) continue;
                float current = Vector3.Distance(point, collider.ClosestPoint(point));
                if (current <= distance) { distance = current; best = crystal; }
            }
            return best;
        }
        void Collect()
        {
            var tutorial = InferenceControlsOnboarding.Active;
            if (tutorial != null && tutorial.IsCrystalPractice) { tutorial.TryPracticeInteraction(); return; }
            if (Player.LocalInstance == null || Player.LocalInstance.GetHeldObject() != null) return;
            TryHandleInteraction();
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
            var grab = crystal.GetComponent<GrabbableObject>(); grab.Grab(player.GetHoldPoint()); player.SetHeldObjectSilently(grab);
            AvailableCrystal = null; if (CollectButton != null) CollectButton.gameObject.SetActive(false);
            return true;
        }
        void OnDisable() { if (Active == this) Active = null; if (CollectButton != null) CollectButton.gameObject.SetActive(false); }
        void OnEnable() { if (Challenge != null) Active = this; }
        void OnDestroy() { if (CollectButton != null) Destroy(CollectButton.gameObject); }
    }
}
