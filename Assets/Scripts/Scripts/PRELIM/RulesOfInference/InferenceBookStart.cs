using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.Inference
{
    /// <summary>Starts Rules of Inference only through the existing hand/E interaction input.</summary>
    public sealed class InferenceBookStart : MonoBehaviour
    {
        [SerializeField] private InferenceChallenge challenge;
        [SerializeField, Min(1f)] private float interactionRange = 4f;
        [SerializeField] private string promptText = "Interact to start the game";

        private GameInput subscribedInput;
        private TextMeshPro prompt;
        private Button interactButton;
        private TextMeshPro waitingIndicator;
        private Bounds bookBounds;
        private bool boundsReady;

        private void Update()
        {
            if (challenge == null) challenge = FindFirstObjectByType<InferenceChallenge>();
            if (subscribedInput != GameInput.Instance)
            {
                if (subscribedInput != null) subscribedInput.OnInteractAction -= OnInteract;
                subscribedInput = GameInput.Instance;
                if (subscribedInput != null) subscribedInput.OnInteractAction += OnInteract;
            }
            EnsurePrompt();
            EnsureButton();
            bool waiting = challenge != null && challenge.isActiveAndEnabled && !challenge.HasStarted &&
                !challenge.PuzzleCompleted && (StageJourneyUI.Instance == null || !StageJourneyUI.Instance.InferenceEnded);
            if (waitingIndicator != null)
            {
                waitingIndicator.gameObject.SetActive(waiting);
                waitingIndicator.transform.position = new Vector3(bookBounds.center.x,
                    bookBounds.max.y + 3.8f + Mathf.Sin(Time.unscaledTime * 2f) * .35f, bookBounds.center.z);
                if (Camera.main != null) waitingIndicator.transform.rotation = Camera.main.transform.rotation;
            }
            if (interactButton != null) interactButton.gameObject.SetActive(CanInteract());
            if (prompt != null)
            {
                prompt.gameObject.SetActive(waiting);
                if (prompt.gameObject.activeSelf)
                {
                    prompt.transform.position = new Vector3(bookBounds.center.x, bookBounds.max.y + .8f, bookBounds.center.z);
                    if (Camera.main != null) prompt.transform.rotation = Camera.main.transform.rotation;
                }
            }
        }

        private void EnsureButton()
        {
            if (interactButton != null) return;
            GameObject canvas = GameObject.Find("Canvas 1");
            Transform safeArea = canvas != null ? canvas.transform.Find("SafeArea") : null;
            if (safeArea == null) return;
            interactButton = StageUITheme.Button("InferenceInteractBookButton", safeArea,
                "Interact with Book", () => TryStartFromInteraction());
            RectTransform rect = (RectTransform)interactButton.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 175f);
            rect.sizeDelta = new Vector2(300f, 86f);
            interactButton.gameObject.SetActive(false);
        }

        private void EnsurePrompt()
        {
            if (prompt != null) return;
            // Cache only the statue's geometry; including our labels makes bounds grow every frame.
            if (!boundsReady)
            {
                bookBounds = new Bounds(transform.position, Vector3.zero);
                foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
                    if (renderer.GetComponent<TMP_Text>() == null && renderer.GetComponent<TMP_SubMesh>() == null)
                        bookBounds.Encapsulate(renderer.bounds);
                boundsReady = true;
            }
            GameObject label = new GameObject("Inference Book Start Prompt", typeof(TextMeshPro));
            prompt = label.GetComponent<TextMeshPro>();
            prompt.text = promptText;
            prompt.font = StageUITheme.HeadingFont;
            prompt.fontSize = 4f;
            prompt.alignment = TextAlignmentOptions.Center;
            prompt.color = StageUITheme.Gold;
            prompt.outlineColor = Color.black;
            prompt.outlineWidth = 0.25f;
            prompt.rectTransform.sizeDelta = new Vector2(26f, 5f);
            prompt.transform.localScale = Vector3.one * 0.75f;
            var indicator = new GameObject("Inference Book Waiting Indicator", typeof(TextMeshPro));
            waitingIndicator = indicator.GetComponent<TextMeshPro>();
            waitingIndicator.text = "!";
            waitingIndicator.font = StageUITheme.HeadingFont;
            waitingIndicator.fontSize = 13f;
            waitingIndicator.alignment = TextAlignmentOptions.Center;
            waitingIndicator.color = StageUITheme.Gold;
            waitingIndicator.outlineColor = Color.black;
            waitingIndicator.outlineWidth = .25f;
            waitingIndicator.rectTransform.sizeDelta = new Vector2(5f, 5f);
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
            if (prompt != null) Destroy(prompt.gameObject);
            prompt = null;
            if (waitingIndicator != null) Destroy(waitingIndicator.gameObject);
            waitingIndicator = null;
            if (interactButton != null) Destroy(interactButton.gameObject);
            interactButton = null;
        }
    }
}
