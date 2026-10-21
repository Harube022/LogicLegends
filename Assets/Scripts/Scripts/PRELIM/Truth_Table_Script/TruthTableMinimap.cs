using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Linq;

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
    private TruthTableStageClock stageClock;
    private Vector2 lastSafeAreaSize;
    private ExpandableMinimap expanded;
    private Bounds playableBounds;
    private float normalZoom;
    private RenderTexture overviewTexture, originalTexture;
    private RawImage mapImage;
    private Vector2 markerPosition;
    private Vector3 markerScale;
    private readonly Vector3[] hudCorners = new Vector3[4];

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

        expanded = ExpandableMinimap.Attach(minimapPanel);
        expanded.ViewChanged += ChangeView;
        normalZoom = minimapCamera.orthographicSize;
        mapImage = minimapPanel.GetComponentInChildren<RawImage>(true);
        markerPosition = playerMarker.anchoredPosition;
        markerScale = playerMarker.localScale;
        bool inTruthTable = ((SceneManager.GetActiveScene().name == "PRELIM" || SceneManager.GetActiveScene().name == "TruthTable") &&
                             StageSelectionState.SelectedStage == 2) ||
                            SceneManager.GetActiveScene().name == "RulesOfInference";
        minimapPanel.gameObject.SetActive(inTruthTable);
        minimapCamera.enabled = inTruthTable;
        if (inTruthTable) UpdatePanelLayout();
    }

    private void LateUpdate()
    {
        // The stage can change in this same scene after Propositional Logic ends.
        bool inTruthTable = ((SceneManager.GetActiveScene().name == "PRELIM" || SceneManager.GetActiveScene().name == "TruthTable") &&
                             StageSelectionState.SelectedStage == 2) ||
                            SceneManager.GetActiveScene().name == "RulesOfInference";
        if(SceneManager.GetActiveScene().name=="RulesOfInference")
        {
            var challenge=LogicLegends.Inference.InferenceCrystalInteraction.Active?.Challenge;
            if((challenge!=null&&challenge.IsBoardOpen)||(StageJourneyUI.Instance!=null&&StageJourneyUI.Instance.InferenceQuizOpen)||
                (InferenceControlsOnboarding.Active!=null&&InferenceControlsOnboarding.Active.IsPracticeBoardOpen))inTruthTable=false;
        }
        if ((SceneManager.GetActiveScene().name == "PRELIM" || SceneManager.GetActiveScene().name == "TruthTable") && StageSelectionState.SelectedStage == 2)
        {
            if (stageClock == null) stageClock = FindFirstObjectByType<TruthTableStageClock>(FindObjectsInactive.Include);
            if (stageClock != null && stageClock.IsPlayerAtBoard) inTruthTable = false;
        }
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
            minimapCamera.transform.position = expanded.IsOpen
                ? new Vector3(playableBounds.center.x, playableBounds.max.y + cameraHeight + 100, playableBounds.center.z)
                : playerTarget.position + Vector3.up * cameraHeight;
            minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            if (expanded.IsOpen)
            {
                minimapCamera.orthographicSize = Mathf.Max(playableBounds.extents.z, playableBounds.extents.x) * 1.1f;
                var point = minimapCamera.WorldToViewportPoint(playerTarget.position);
                var markerHost = (RectTransform)playerMarker.parent;
                playerMarker.anchoredPosition = new Vector2((point.x-.5f)*markerHost.rect.width,(point.y-.5f)*markerHost.rect.height);
                // The map grows, but its player arrow should remain a small readable marker.
                float pixels = Mathf.Max(playerMarker.rect.width,playerMarker.rect.height) * minimapPanel.lossyScale.x;
                playerMarker.localScale = Vector3.one * Mathf.Min(1,28f/Mathf.Max(1,pixels));
            }
            else { minimapCamera.orthographicSize = normalZoom; playerMarker.anchoredPosition = markerPosition; }

            // North (+Z) is at the top of the map; turn the marker with the player.
            Vector3 facing = playerTarget.forward;
            playerMarker.localRotation = Quaternion.Euler(0f, 0f,
                -Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg);
        }

        if (!expanded.IsOpen && (!wasVisible || lastSafeAreaSize != safeArea.rect.size)) UpdatePanelLayout();
    }

    private void UpdatePanelLayout()
    {
        Vector2 available = safeArea.rect.size;
        float shorterSide = Mathf.Min(available.x, available.y);
        float preferredSize = Mathf.Clamp(shorterSide * sizeOfShorterScreenSide,
            minimumSize, Mathf.Max(minimumSize, maximumSize));
        float fittingSize = Mathf.Max(64f, shorterSide - 2f * edgeMargin);
        float size = Mathf.Min(preferredSize * .85f, fittingSize);

        minimapPanel.anchorMin = Vector2.one;
        minimapPanel.anchorMax = Vector2.one;
        minimapPanel.pivot = Vector2.one;
        minimapPanel.sizeDelta = new Vector2(size, size);
        minimapPanel.anchoredPosition = new Vector2(-edgeMargin, -edgeMargin);
        lastSafeAreaSize = available;
    }

    void ChangeView(bool open)
    {
        if (open)
        {
            // Both camera-map stages use this named collision floor. Background ocean
            // bounds are deliberately excluded from the playable overview.
            var root = SceneManager.GetActiveScene().name == "PRELIM" || SceneManager.GetActiveScene().name == "TruthTable" ? GameObject.Find("Truth_Table") : null;
            var candidates = root != null ? root.GetComponentsInChildren<Collider>(true) : FindObjectsByType<Collider>(FindObjectsSortMode.None);
            var floor = candidates.Where(c => !c.isTrigger && c.name.StartsWith("FLOOR",System.StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(c => c.bounds.size.x*c.bounds.size.z).FirstOrDefault();
            playableBounds = floor != null ? floor.bounds : new Bounds(playerTarget != null ? playerTarget.position : transform.position,new Vector3(300,20,300));
            originalTexture = minimapCamera.targetTexture;
            overviewTexture = new RenderTexture(1024,1024,16);overviewTexture.name="Expanded Map Overview";overviewTexture.Create();
            minimapCamera.targetTexture=overviewTexture;if(mapImage!=null)mapImage.texture=overviewTexture;
        }
        else
        {
            minimapCamera.orthographicSize=normalZoom;minimapCamera.targetTexture=originalTexture;
            if(mapImage!=null)mapImage.texture=originalTexture;
            playerMarker.anchoredPosition=markerPosition;
            playerMarker.localScale=markerScale;
            if(overviewTexture!=null){overviewTexture.Release();Destroy(overviewTexture);overviewTexture=null;}
            lastSafeAreaSize=Vector2.zero;
        }
    }

    private void OnDisable()
    {
        if (expanded != null && expanded.IsOpen) expanded.Close();
        if (minimapCamera != null) minimapCamera.enabled = false;
        if (minimapPanel != null) minimapPanel.gameObject.SetActive(false);
    }
    void OnDestroy() { if (expanded != null) expanded.ViewChanged -= ChangeView; }
}
