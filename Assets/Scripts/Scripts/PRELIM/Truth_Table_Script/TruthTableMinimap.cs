using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Displays the map in PRELIM Truth Table and RulesOfInference.
/// The dedicated camera renders into a small texture; the normal gameplay camera is untouched.
/// </summary>
public class TruthTableMinimap : MonoBehaviour
{
    public RectTransform Panel => minimapPanel;
    [Header("Minimap References")]
    [SerializeField] private Camera minimapCamera;
    [SerializeField] private RectTransform minimapPanel;
    [SerializeField] private RectTransform safeArea;
    [SerializeField] private RectTransform playerMarker;

    [Header("Map View")]
    [SerializeField, Min(1f)] private float cameraHeight = 180f;

    [Header("Screen Layout")]
    [SerializeField, Range(0.1f, 0.45f)] private float sizeOfShorterScreenSide = 0.26f;
    [SerializeField, Min(64f)] private float minimumSize = 140f;
    [SerializeField, Min(64f)] private float maximumSize = 280f;
    [SerializeField, Min(0f)] private float edgeMargin = 28f;

    private Transform playerTarget;
    private Vector2 lastSafeAreaSize;

    private void Awake()
    {
        if (minimapCamera == null || minimapPanel == null || safeArea == null || playerMarker == null)
        {
            Debug.LogError("TruthTableMinimap is missing a scene reference.", this);
            if (minimapPanel != null) minimapPanel.gameObject.SetActive(false);
            if (minimapCamera != null) minimapCamera.enabled = false;
            enabled = false;
            return;
        }

        bool inTruthTable = (SceneManager.GetActiveScene().name == "PRELIM" &&
                             StageSelectionState.SelectedStage == 2) ||
                            SceneManager.GetActiveScene().name == "RulesOfInference";
        minimapPanel.gameObject.SetActive(inTruthTable);
        minimapCamera.enabled = inTruthTable;
        if (inTruthTable) UpdatePanelLayout();
    }

    private void LateUpdate()
    {
        // The stage can change in this same scene after Propositional Logic ends.
        bool inTruthTable = (SceneManager.GetActiveScene().name == "PRELIM" &&
                             StageSelectionState.SelectedStage == 2) ||
                            SceneManager.GetActiveScene().name == "RulesOfInference";
        bool wasVisible = minimapPanel.gameObject.activeSelf && minimapCamera.enabled;
        if (minimapPanel.gameObject.activeSelf != inTruthTable)
            minimapPanel.gameObject.SetActive(inTruthTable);
        if (minimapCamera.enabled != inTruthTable)
            minimapCamera.enabled = inTruthTable;
        if (!inTruthTable)
        {
            playerTarget = null;
            return;
        }

        Player player = Player.LocalInstance;
        if (player == null || !player.gameObject.activeInHierarchy)
            player = FindFirstObjectByType<Player>();
        playerTarget = player != null ? player.transform : null;

        if (playerTarget != null)
        {
            minimapCamera.transform.position = playerTarget.position + Vector3.up * cameraHeight;
            minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // North (+Z) is at the top of the map; turn the marker with the player.
            Vector3 facing = playerTarget.forward;
            playerMarker.localRotation = Quaternion.Euler(0f, 0f,
                -Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg);
        }

        if (!wasVisible || lastSafeAreaSize != safeArea.rect.size) UpdatePanelLayout();
    }

    private void UpdatePanelLayout()
    {
        Vector2 available = safeArea.rect.size;
        float shorterSide = Mathf.Min(available.x, available.y);
        float preferredSize = Mathf.Clamp(shorterSide * sizeOfShorterScreenSide,
            minimumSize, Mathf.Max(minimumSize, maximumSize));
        float fittingSize = Mathf.Max(64f, shorterSide - 2f * edgeMargin);
        float size = Mathf.Min(preferredSize, fittingSize);

        minimapPanel.anchorMin = Vector2.one;
        minimapPanel.anchorMax = Vector2.one;
        minimapPanel.pivot = Vector2.one;
        minimapPanel.sizeDelta = new Vector2(size, size);
        minimapPanel.anchoredPosition = new Vector2(-edgeMargin, -edgeMargin);
        lastSafeAreaSize = available;
    }

    private void OnDisable()
    {
        if (minimapCamera != null) minimapCamera.enabled = false;
        if (minimapPanel != null) minimapPanel.gameObject.SetActive(false);
    }
}
