using TMPro;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

[RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
public class StudyShelfZone : MonoBehaviour
{
    [SerializeField] private StudyLibraryController controller;
    [SerializeField] private CinemachineCamera focusCamera;
    [SerializeField] private string shelfTitle;
    [SerializeField] private StudyBookDefinition[] books;
    [SerializeField] private Vector3 labelWorldPosition;
    [SerializeField] private Vector3[] cardWorldPositions;
    [SerializeField] private Vector3 uiEulerAngles;

    [Header("Interactive Shelf Highlight")]
    [SerializeField] private bool shelfHighlightEnabled = true;
    [SerializeField, Min(0.001f)] private float outlineSize = 0.105f;
    [SerializeField, Range(1f, 5f)] private float glowSize = 2.4f;
    [SerializeField, Range(0f, 2f)] private float glowIntensity = 0.55f;
    [SerializeField] private Color darkOutlineColor = new Color(0.004f, 0.007f, 0.01f, 0.92f);
    [SerializeField] private Color glowColor = new Color(0.015f, 0.20f, 0.16f, 0.42f);

    private GameObject previewRoot;
    private Collider activePlayerCollider;
    private GameInput gameInput;
    private Material shelfOutlineMaterial;
    private Material shelfGlowMaterial;
    private readonly List<GameObject> shelfHighlightObjects = new List<GameObject>();

    public void Configure(StudyLibraryController newController, CinemachineCamera newCamera, string title,
        StudyBookDefinition[] shelfBooks, Vector3 labelPosition, Vector3[] cardPositions, Vector3 canvasEulerAngles)
    {
        controller = newController;
        focusCamera = newCamera;
        shelfTitle = title;
        books = shelfBooks;
        labelWorldPosition = labelPosition;
        cardWorldPositions = cardPositions;
        uiEulerAngles = canvasEulerAngles;
    }

    private void Awake()
    {
        BoxCollider trigger = GetComponent<BoxCollider>();
        trigger.isTrigger = true;
        Rigidbody body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        if (focusCamera != null) focusCamera.Priority.Value = -10;
    }

    private void Start()
    {
        // Refresh from the shared catalog so topic reorganizations do not require
        // duplicating or manually reauthoring the serialized shelf data.
        books = StudyLibraryCatalog.GetShelf(shelfTitle);
        BuildWorldUI();
        BuildShelfHighlight();
        SetFocused(false);
        gameInput = FindFirstObjectByType<GameInput>();
        if (gameInput != null) gameInput.OnInteractAction += HandleInteractAction;
    }

    private void OnDestroy()
    {
        if (gameInput != null) gameInput.OnInteractAction -= HandleInteractAction;
        for (int i = shelfHighlightObjects.Count - 1; i >= 0; i--)
            if (shelfHighlightObjects[i] != null) Destroy(shelfHighlightObjects[i]);
        if (shelfOutlineMaterial != null) Destroy(shelfOutlineMaterial);
        if (shelfGlowMaterial != null) Destroy(shelfGlowMaterial);
    }

    private void Update()
    {
        UpdateShelfHighlight();
    }

    private void OnTriggerEnter(Collider other)
    {
        Player player = other.GetComponentInParent<Player>();
        if (player == null) return;
        if (Player.LocalInstance != null && player != Player.LocalInstance) return;

        activePlayerCollider = other;
        if (DialogueManager.Instance != null) DialogueManager.Instance.ToggleInteractButton(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other != activePlayerCollider) return;
        activePlayerCollider = null;
        if (DialogueManager.Instance != null) DialogueManager.Instance.ToggleInteractButton(false);
        if (controller != null) controller.ExitShelf(this);
    }

    private void HandleInteractAction(object sender, System.EventArgs eventArgs)
    {
        if (activePlayerCollider == null || controller == null || controller.IsViewing(this)) return;
        controller.EnterShelf(this);
        if (DialogueManager.Instance != null) DialogueManager.Instance.ToggleInteractButton(false);
    }

    public void SetFocused(bool focused)
    {
        if (focusCamera != null) focusCamera.Priority.Value = focused ? 25 : -10;
        if (previewRoot != null) previewRoot.SetActive(focused);
        if (focused && previewRoot != null)
            foreach (Canvas canvas in previewRoot.GetComponentsInChildren<Canvas>(true))
                canvas.worldCamera = Camera.main;
    }

    private void BuildShelfHighlight()
    {
        if (!shelfHighlightEnabled) return;

        string targetName;
        switch (shelfTitle)
        {
            case "PRELIM": targetName = "BOOKSHELVES"; break;
            case "MIDTERMS": targetName = "BOOKSHELVES (1)"; break;
            case "PRE-FINALS": targetName = "BOOKSHELVES (2)"; break;
            case "FINALS": targetName = "BOOKSHELVES (3)"; break;
            default: return;
        }

        GameObject shelf = GameObject.Find(targetName);
        if (shelf == null)
        {
            Debug.LogWarning("Interactive shelf highlight could not find " + targetName + ".", this);
            return;
        }

        Shader shader = Resources.Load<Shader>("StudyLibrary/LogicGardenShelfHighlight");
        if (shader == null)
        {
            Debug.LogError("Missing StudyLibrary/LogicGardenShelfHighlight shader.", this);
            return;
        }

        shelfOutlineMaterial = new Material(shader)
        {
            name = shelfTitle + " Dark Shelf Edge (Runtime)",
            hideFlags = HideFlags.DontSave
        };
        shelfGlowMaterial = new Material(shader)
        {
            name = shelfTitle + " Shelf Glow (Runtime)",
            hideFlags = HideFlags.DontSave
        };
        shelfGlowMaterial.renderQueue = 3040;
        shelfOutlineMaterial.renderQueue = 3041;

        Renderer[] renderers = shelf.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer source in renderers)
        {
            if (!(source is MeshRenderer) || IsBookRenderer(source.transform, shelf.transform)) continue;
            MeshFilter sourceFilter = source.GetComponent<MeshFilter>();
            if (sourceFilter == null || sourceFilter.sharedMesh == null) continue;

            CreateHighlightShell(source, sourceFilter.sharedMesh, shelfGlowMaterial, "__Interactive Shelf Soft Glow");
            CreateHighlightShell(source, sourceFilter.sharedMesh, shelfOutlineMaterial, "__Interactive Shelf Dark Edge");
        }

        UpdateShelfHighlight();
    }

    private void CreateHighlightShell(Renderer source, Mesh mesh, Material material, string objectName)
    {
        GameObject outline = new GameObject(objectName, typeof(MeshFilter), typeof(MeshRenderer));
        outline.layer = source.gameObject.layer;
        outline.transform.SetParent(source.transform, false);
        outline.transform.localPosition = Vector3.zero;
        outline.transform.localRotation = Quaternion.identity;
        outline.transform.localScale = Vector3.one;

        outline.GetComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer outlineRenderer = outline.GetComponent<MeshRenderer>();
        int materialCount = Mathf.Max(1, source.sharedMaterials.Length);
        Material[] materials = new Material[materialCount];
        for (int i = 0; i < materials.Length; i++) materials[i] = material;
        outlineRenderer.sharedMaterials = materials;
        outlineRenderer.shadowCastingMode = ShadowCastingMode.Off;
        outlineRenderer.receiveShadows = false;
        outlineRenderer.lightProbeUsage = LightProbeUsage.Off;
        outlineRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        outlineRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        outlineRenderer.allowOcclusionWhenDynamic = false;
        shelfHighlightObjects.Add(outline);
    }

    private static bool IsBookRenderer(Transform rendererTransform, Transform shelfRoot)
    {
        Transform current = rendererTransform;
        while (current != null && current != shelfRoot)
        {
            if (current.name.StartsWith("Book", System.StringComparison.OrdinalIgnoreCase)) return true;
            current = current.parent;
        }
        return false;
    }

    private void UpdateShelfHighlight()
    {
        if (shelfOutlineMaterial == null || shelfGlowMaterial == null) return;

        for (int i = 0; i < shelfHighlightObjects.Count; i++)
            if (shelfHighlightObjects[i] != null && shelfHighlightObjects[i].activeSelf != shelfHighlightEnabled)
                shelfHighlightObjects[i].SetActive(shelfHighlightEnabled);
        if (!shelfHighlightEnabled) return;

        shelfOutlineMaterial.SetFloat("_Width", outlineSize);
        shelfOutlineMaterial.SetColor("_Color", darkOutlineColor);

        Color activeGlow = glowColor;
        activeGlow.r *= glowIntensity;
        activeGlow.g *= glowIntensity;
        activeGlow.b *= glowIntensity;
        shelfGlowMaterial.SetFloat("_Width", outlineSize * glowSize);
        shelfGlowMaterial.SetColor("_Color", activeGlow);
    }

    private void BuildWorldUI()
    {
        GameObject label = CreateWorldCanvas(shelfTitle + " Label", labelWorldPosition, new Vector2(3400f, 840f), 0.0018f);
        Image labelBackground = label.AddComponent<Image>();
        labelBackground.raycastTarget = false;
        labelBackground.color = new Color(0.035f, 0.11f, 0.13f, 0.96f);
        Outline labelOutline = label.AddComponent<Outline>();
        labelOutline.effectColor = new Color(0.25f, 0.95f, 0.7f, 0.95f);
        labelOutline.effectDistance = new Vector2(12f, -12f);
        TMP_Text labelText = StudyLibraryController.CreateText("Period", label.transform, 300f, FontStyles.Bold, TextAlignmentOptions.Center);
        labelText.text = shelfTitle;
        labelText.color = new Color(0.82f, 1f, 0.91f);
        StudyLibraryController.Stretch(labelText.rectTransform, Vector2.zero, Vector2.one, new Vector2(60f, 30f), new Vector2(-60f, -30f));

        previewRoot = new GameObject(shelfTitle + " Book Previews");
        previewRoot.transform.SetParent(transform, false);

        int count = Mathf.Min(books == null ? 0 : books.Length, cardWorldPositions == null ? 0 : cardWorldPositions.Length);
        for (int i = 0; i < count; i++)
        {
            int bookIndex = i;
            StudyBookDefinition book = books[i];
            GameObject card = CreateWorldCanvas("Book " + (i + 1) + " Preview", cardWorldPositions[i], new Vector2(420f, 250f), 0.0013f);
            card.transform.SetParent(previewRoot.transform, true);
            Image cardImage = card.AddComponent<Image>();
            cardImage.color = new Color(0.04f, 0.12f, 0.16f, 0.97f);
            cardImage.raycastTarget = true;
            Button button = card.AddComponent<Button>();
            button.interactable = true;
            button.targetGraphic = cardImage;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.1f, 0.36f, 0.33f, 1f);
            colors.pressedColor = new Color(0.05f, 0.62f, 0.45f, 1f);
            button.colors = colors;
            button.onClick.AddListener(() => controller.OpenBook(this, books[bookIndex]));

            Outline cardOutline = card.AddComponent<Outline>();
            cardOutline.effectColor = new Color(0.22f, 0.86f, 0.66f, 0.9f);
            cardOutline.effectDistance = new Vector2(3f, -3f);

            TMP_Text title = StudyLibraryController.CreateText("Title", card.transform, 44f, FontStyles.Bold, TextAlignmentOptions.Center);
            title.text = StudyLibraryController.StandardizeTerminology(book.title);
            title.color = new Color(0.82f, 1f, 0.91f);
            title.enableAutoSizing = true;
            title.fontSizeMin = 20f;
            title.fontSizeMax = 44f;
            title.rectTransform.anchorMin = new Vector2(0.05f, 0.43f);
            title.rectTransform.anchorMax = new Vector2(0.95f, 0.94f);
            title.rectTransform.offsetMin = title.rectTransform.offsetMax = Vector2.zero;

            TMP_Text preview = StudyLibraryController.CreateText("Preview", card.transform, 29f, FontStyles.Normal, TextAlignmentOptions.Center);
            preview.text = StudyLibraryController.StandardizeTerminology(book.preview);
            preview.color = new Color(0.87f, 0.92f, 0.92f);
            preview.rectTransform.anchorMin = new Vector2(0.06f, 0.08f);
            preview.rectTransform.anchorMax = new Vector2(0.94f, 0.43f);
            preview.rectTransform.offsetMin = preview.rectTransform.offsetMax = Vector2.zero;
        }
    }

    private GameObject CreateWorldCanvas(string objectName, Vector3 position, Vector2 size, float scale)
    {
        GameObject canvasObject = new GameObject(objectName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(StudyShelfGraphicRaycaster));
        canvasObject.transform.SetParent(transform, true);
        canvasObject.transform.position = position;
        canvasObject.transform.rotation = Quaternion.Euler(uiEulerAngles);
        canvasObject.transform.localScale = Vector3.one * scale;
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        // LogicGarden's HUD contains interactive canvases at sorting orders 10–20.
        // Keep shelf topics above those raycasters while leaving the book reader
        // (sorting order 200) on top when a book is open.
        canvas.overrideSorting = true;
        canvas.sortingOrder = 100;
        StudyShelfGraphicRaycaster raycaster = canvasObject.GetComponent<StudyShelfGraphicRaycaster>();
        raycaster.ignoreReversedGraphics = false;
        raycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;
        RectTransform rect = canvasObject.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        return canvasObject;
    }
}
