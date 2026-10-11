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
            if (interactButton != null) interactButton.gameObject.SetActive(CanInteract());
            if (prompt != null)
            {
                prompt.gameObject.SetActive(challenge != null && !challenge.HasStarted &&
                    !challenge.PuzzleCompleted);
                if (prompt.gameObject.activeSelf)
                {
                    Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
                    if (renderers.Length > 0)
                    {
                        Bounds bounds = renderers[0].bounds;
                        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                        prompt.transform.position = new Vector3(bounds.center.x, bounds.max.y + 0.8f,
                            bounds.center.z);
                    }
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
            if (interactButton != null) Destroy(interactButton.gameObject);
            interactButton = null;
        }
    }
}
