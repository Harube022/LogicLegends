using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

// Scene-specific view control for the Setssss board. Both views use the same
// Main Camera and CinemachineBrain.
[DisallowMultipleComponent]
public sealed class SetssssBoardView : MonoBehaviour
{
    [Header("Spawner range")]
    [SerializeField] private Collider spawnerCollider;
    [SerializeField, Min(0.5f)] private float interactionRange = 5f;

    [Header("Existing camera system")]
    [SerializeField] private CinemachineCamera playerCamera;
    [SerializeField] private CinemachineCamera boardCamera;
    [SerializeField] private CinemachinePinchZoom playerZoom;

    [Header("Canvas 1 controls")]
    [SerializeField] private GameObject gameplayControls;
    [SerializeField] private Button viewBoardButton;
    [SerializeField] private Button backButton;

    private Player player;
    private GameInput gameInput;
    private bool viewingBoard;
    private bool previousInputBlocked;
    private bool previousControlsVisible;
    private bool previousZoomEnabled;
    private int previousBoardPriority;
    private float previousPlayerDistance;

    public bool IsViewingBoard => viewingBoard;

    private void Awake()
    {
        if (viewBoardButton != null)
        {
            viewBoardButton.onClick.AddListener(ViewBoard);
            viewBoardButton.gameObject.SetActive(false);
        }

        if (backButton != null)
        {
            backButton.onClick.AddListener(Back);
            backButton.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (viewingBoard) return;

        if (player == null) player = Player.LocalInstance;
        if (gameInput == null) gameInput = GameInput.Instance;

        bool available = player != null && gameInput != null &&
                         !gameInput.GameplayInputBlocked && Time.timeScale > 0f &&
                         IsPlayerNearSpawner();
        if (viewBoardButton != null && viewBoardButton.gameObject.activeSelf != available)
            viewBoardButton.gameObject.SetActive(available);
    }

    private bool IsPlayerNearSpawner()
    {
        if (spawnerCollider == null || player == null) return false;
        // Imported non-convex mesh colliders can return the input point from
        // ClosestPoint even outside the mesh. Bounds give a stable proximity area.
        Vector3 closestPoint = spawnerCollider.bounds.ClosestPoint(player.transform.position);
        return Vector3.Distance(player.transform.position, closestPoint) <= interactionRange;
    }

    public void ViewBoard()
    {
        if (viewingBoard || playerCamera == null || boardCamera == null ||
            gameInput == null || gameInput.GameplayInputBlocked ||
            Time.timeScale <= 0f || !IsPlayerNearSpawner()) return;

        viewingBoard = true;
        previousInputBlocked = gameInput.GameplayInputBlocked;
        previousControlsVisible = gameplayControls != null && gameplayControls.activeSelf;
        previousZoomEnabled = playerZoom != null && playerZoom.enabled;
        previousBoardPriority = boardCamera.Priority.Value;

        CinemachineThirdPersonFollow follow = playerCamera.GetComponent<CinemachineThirdPersonFollow>();
        previousPlayerDistance = follow != null ? follow.CameraDistance : 0f;

        gameInput.SetGameplayInputBlocked(true);
        if (playerZoom != null) playerZoom.enabled = false;
        if (gameplayControls != null) gameplayControls.SetActive(false);
        if (viewBoardButton != null) viewBoardButton.gameObject.SetActive(false);
        if (backButton != null) backButton.gameObject.SetActive(true);

        var priority = boardCamera.Priority;
        priority.Value = playerCamera.Priority.Value + 100;
        boardCamera.Priority = priority;
    }

    public void Back()
    {
        if (!viewingBoard) return;
        viewingBoard = false;

        if (boardCamera != null)
        {
            var priority = boardCamera.Priority;
            priority.Value = previousBoardPriority;
            boardCamera.Priority = priority;
        }

        if (playerCamera != null)
        {
            CinemachineThirdPersonFollow follow = playerCamera.GetComponent<CinemachineThirdPersonFollow>();
            if (follow != null) follow.CameraDistance = previousPlayerDistance;
        }

        if (playerZoom != null) playerZoom.enabled = previousZoomEnabled;
        if (gameplayControls != null) gameplayControls.SetActive(previousControlsVisible);
        if (backButton != null) backButton.gameObject.SetActive(false);
        if (gameInput != null) gameInput.SetGameplayInputBlocked(previousInputBlocked);
    }

    private void OnDestroy()
    {
        Back();
        if (viewBoardButton != null) viewBoardButton.onClick.RemoveListener(ViewBoard);
        if (backButton != null) backButton.onClick.RemoveListener(Back);
    }
}
