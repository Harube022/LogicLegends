using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.Inference
{
    [RequireComponent(typeof(BoxCollider))]
    public class InferenceAltarTrigger : MonoBehaviour
    {
        public InferenceChallenge challenge;

        private Player nearbyPlayer;
        private float lastSeenAt;
        private Button viewBoardButton;
        [SerializeField] private Transform altarVisual;
        [SerializeField, Min(0f)] private float labelHeight = 6f;
        [SerializeField, Min(0f)] private float indicatorHeight = 11.5f;
        private TextMeshPro altarLabel;
        private TextMeshPro tutorialIndicator;

        private void OnTriggerEnter(Collider other) => TrackLocalPlayer(other);
        private void OnTriggerStay(Collider other) => TrackLocalPlayer(other);

        private void TrackLocalPlayer(Collider other)
        {
            Player player = other.GetComponentInParent<Player>();
            if (player == null) return;
            PhotonView view = player.GetComponent<PhotonView>();
            if (PhotonNetwork.InRoom && view != null && !view.IsMine) return;
            nearbyPlayer = player;
            lastSeenAt = Time.unscaledTime;
        }

        private void Update()
        {
            EnsureButton();
            UpdateAltarLabels();
            if (viewBoardButton == null) return;
            var tutorial = InferenceControlsOnboarding.Active;
            bool available = challenge != null && challenge.isActiveAndEnabled &&
                !challenge.IsBoardOpen && nearbyPlayer != null && nearbyPlayer.enabled &&
                Time.unscaledTime - lastSeenAt < 0.3f &&
                (tutorial == null || !tutorial.IsActive || tutorial.IsBoardApproach) &&
                (GameInput.Instance == null || !GameInput.Instance.GameplayInputBlocked);
            viewBoardButton.gameObject.SetActive(available);
        }

        private void UpdateAltarLabels()
        {
            if (altarVisual == null)
                altarVisual = GameObject.Find("ALTAR")?.transform.Find("Cube.101");
            if (altarVisual == null) return;
            if (altarLabel == null) altarLabel = CreateWorldLabel("Altar Label", "Altar", 60f);
            if (tutorialIndicator == null) tutorialIndicator = CreateWorldLabel("Altar Tutorial Indicator", "!", 80f);

            Renderer[] renderers = altarVisual.GetComponentsInChildren<Renderer>(true);
            Vector3 center = altarVisual.position;
            float top = center.y;
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                center = bounds.center;
                top = bounds.max.y;
            }
            altarLabel.transform.position = new Vector3(center.x, top + labelHeight, center.z);
            tutorialIndicator.transform.position = new Vector3(center.x, top + indicatorHeight, center.z);
            if (Camera.main != null)
            {
                altarLabel.transform.rotation = Camera.main.transform.rotation;
                tutorialIndicator.transform.rotation = Camera.main.transform.rotation;
            }
            var tutorial = InferenceControlsOnboarding.Active;
            tutorialIndicator.gameObject.SetActive(tutorial != null && tutorial.IsBoardApproach);
        }

        private static TextMeshPro CreateWorldLabel(string name, string text, float fontSize)
        {
            var label = new GameObject(name, typeof(TextMeshPro)).GetComponent<TextMeshPro>();
            label.text = text;
            label.font = StageUITheme.HeadingFont;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = StageUITheme.Gold;
            label.outlineColor = Color.black;
            label.outlineWidth = 0.3f;
            label.overflowMode = TextOverflowModes.Overflow;
            label.rectTransform.sizeDelta = new Vector2(30f, 12f);
            label.transform.localScale = Vector3.one * 0.6f;
            return label;
        }

        private void EnsureButton()
        {
            if (viewBoardButton != null) return;
            GameObject canvas = GameObject.Find("Canvas 1");
            Transform safeArea = canvas != null ? canvas.transform.Find("SafeArea") : null;
            if (safeArea == null) return;
            viewBoardButton = StageUITheme.Button("InferenceViewBoardButton", safeArea,
                "View Board", ViewBoard);
            RectTransform rect = (RectTransform)viewBoardButton.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 175f);
            rect.sizeDelta = new Vector2(280f, 86f);
            viewBoardButton.gameObject.SetActive(false);
        }

        public void ViewBoard()
        {
            var tutorial = InferenceControlsOnboarding.Active;
            if (challenge == null || challenge.IsBoardOpen || nearbyPlayer == null || !nearbyPlayer.enabled ||
                Time.unscaledTime - lastSeenAt >= 0.3f ||
                (tutorial != null && tutorial.IsActive && !tutorial.IsBoardApproach) ||
                (GameInput.Instance != null && GameInput.Instance.GameplayInputBlocked)) return;
            if (tutorial != null && tutorial.IsBoardApproach)
            {
                tutorial.TryTutorialBoardView();
                return;
            }
            challenge.OpenBoard(nearbyPlayer);
        }

        private void OnDisable()
        {
            if (viewBoardButton != null) Destroy(viewBoardButton.gameObject);
            viewBoardButton = null;
            if (altarLabel != null) Destroy(altarLabel.gameObject);
            if (tutorialIndicator != null) Destroy(tutorialIndicator.gameObject);
            altarLabel = null;
            tutorialIndicator = null;
            nearbyPlayer = null;
        }
    }
}
