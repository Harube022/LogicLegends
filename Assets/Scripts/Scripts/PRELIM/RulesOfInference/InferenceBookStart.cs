using UnityEngine;

namespace LogicLegends.Inference
{
    /// <summary>Starts Rules of Inference only through the existing hand/E interaction input.</summary>
    public sealed class InferenceBookStart : MonoBehaviour
    {
        [SerializeField] private InferenceChallenge challenge;
        [SerializeField, Min(1f)] private float interactionRange = 4f;

        private GameInput subscribedInput;

        private void OnEnable() { BookStatuePresentation.BindHand(transform, CanInteract, () => { TryStartFromInteraction(); }); }

        private void Update()
        {
            if (challenge == null) challenge = FindFirstObjectByType<InferenceChallenge>();
            if (subscribedInput != GameInput.Instance)
            {
                if (subscribedInput != null) subscribedInput.OnInteractAction -= OnInteract;
                subscribedInput = GameInput.Instance;
                if (subscribedInput != null) subscribedInput.OnInteractAction += OnInteract;
            }
            bool waiting = challenge != null && challenge.isActiveAndEnabled && !challenge.HasStarted &&
                !challenge.PuzzleCompleted && (StageJourneyUI.Instance == null || !StageJourneyUI.Instance.InferenceEnded);
            BookStatuePresentation.SetRequired(transform, waiting);

        }

        private void OnInteract(object sender, System.EventArgs args) => TryStartFromInteraction();

        public bool TryStartFromInteraction()
        {
            var tutorial = InferenceControlsOnboarding.Active;
            if (tutorial != null && tutorial.IsBookStep)
            {
                tutorial.TryTutorialBookInteraction();
                return true;
            }
            if (!CanInteract()) return false;
            return challenge.StartFromBook();
        }

        private bool CanInteract()
        {
            if (!isActiveAndEnabled) return false;
            var tutorial = InferenceControlsOnboarding.Active;
            if (tutorial != null && tutorial.IsActive) return tutorial.IsBookStep;
            if (challenge == null || !challenge.isActiveAndEnabled || subscribedInput == null ||
                subscribedInput.GameplayInputBlocked || challenge.HasStarted ||
                (StageJourneyUI.Instance != null && StageJourneyUI.Instance.InferenceEnded) ||
                challenge.PuzzleCompleted || challenge.IsBoardOpen ||
                (InferenceControlsOnboarding.Active != null && InferenceControlsOnboarding.Active.IsActive)) return false;
            Player player = Player.LocalInstance;
            if (player == null || !player.enabled) return false;
            Vector3 toward = transform.position - player.transform.position;
            toward.y = 0f;
            if (toward.sqrMagnitude > interactionRange * interactionRange) return false;
            Vector3 facing = player.transform.forward;
            facing.y = 0f;
            if (toward.sqrMagnitude > 0.01f && facing.sqrMagnitude > 0.01f &&
                Vector3.Dot(facing.normalized, toward.normalized) < 0.5f) return false;
            return true;
        }

        private void OnDisable()
        {
            if (subscribedInput != null) subscribedInput.OnInteractAction -= OnInteract;
            subscribedInput = null;
            BookStatuePresentation.SetRequired(transform, false);
        }
    }
}
