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
    private BoxCollider detection;
    private HandThoughtBubble bubble;
    private Material shelfOutlineMaterial;
    private Material shelfGlowMaterial;
    private readonly List<GameObject> shelfHighlightObjects = new List<GameObject>();
    public string Period => shelfTitle;
    public CinemachineCamera FocusCamera => focusCamera;
    public Vector3 LabelPosition => labelWorldPosition;

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
        detection = trigger;
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
        bubble = HandThoughtBubble.Create(transform, shelfTitle + " Shelf Thought Bubble", yellowMarker: true);
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
        // CharacterController teleports can omit trigger exits. Never retain a
        // shelf interaction merely because its old enter event is still cached.
        if (activePlayerCollider != null && !PlayerWithinRange) activePlayerCollider = null;
        bool available = !LogicLibraryTutorialController.InteractionsBlocked &&
            (controller == null || !controller.IsInReadingView) && (gameInput == null || !gameInput.GameplayInputBlocked);
        if (bubble != null)
        {
            // Keep the prompt below the period sign and in front of the shelf.
            Vector3 anchor = new Vector3(transform.position.x, labelWorldPosition.y - 4, transform.position.z);
            bubble.Show(available, PlayerWithinRange, anchor);
            if (available && PlayerWithinRange) bubble.KeepInView();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Player player = other.GetComponentInParent<Player>();
        if (player == null) return;
        if (Player.LocalInstance != null && player != Player.LocalInstance) return;

        activePlayerCollider = other;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other != activePlayerCollider) return;
        activePlayerCollider = null;
        // Reading is an explicit Back-controlled view, not a movement gesture.
        // Forced trigger exits must not close a book or change its current spread.
    }

    private void HandleInteractAction(object sender, System.EventArgs eventArgs)
    {
        if(LogicLibraryTutorialController.InteractionsBlocked||(gameInput!=null&&gameInput.GameplayInputBlocked))return;
        if (!PlayerWithinRange || controller == null || controller.IsViewing(this)) return;
        controller.EnterShelf(this);
    }

    private bool PlayerWithinRange => activePlayerCollider != null && activePlayerCollider.enabled &&
        activePlayerCollider.gameObject.activeInHierarchy && detection != null && detection.bounds.Intersects(activePlayerCollider.bounds);

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

        // Stable screen-space cards remain readable during the shelf camera blend.
        previewRoot = new GameObject(shelfTitle + " Book Previews", typeof(RectTransform), typeof(Canvas),
            typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        previewRoot.transform.SetParent(transform, false);
        var canvas = previewRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 150;
        var scaler = previewRoot.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
        var safe = StageUITheme.Rect("Shelf Topic Safe Area", previewRoot.transform);
        StageUITheme.Stretch(safe, Vector2.zero, Vector2.one); safe.gameObject.AddComponent<Crystal.SafeArea>();
        var heading = StageUITheme.Text("Shelf Heading", safe, shelfTitle, 42, true);
        heading.fontStyle = FontStyles.Bold; heading.color = new Color(1, .89f, .62f);
        StageUITheme.Stretch(heading.rectTransform, new Vector2(.08f,.78f), new Vector2(.92f,.89f));
        var row = StageUITheme.Rect("Topic Cards", safe);
        StageUITheme.Stretch(row, new Vector2(.055f,.26f), new Vector2(.945f,.76f));
        var group = row.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        group.spacing = 24; group.childControlWidth = group.childControlHeight = true;
        group.childForceExpandWidth = group.childForceExpandHeight = true;
        for (int i = 0; i < (books == null ? 0 : books.Length); i++)
        {
            int bookIndex = i;
            StudyBookDefinition book = books[i];
            var card = StageUITheme.Rect("Book " + (i + 1) + " Preview", row);
            var cardImage = card.gameObject.AddComponent<UnityEngine.UI.Image>();
            StageUITheme.CompactSurface(cardImage);
            card.Find("CalmContentInset").gameObject.AddComponent<UnityEngine.UI.LayoutElement>().ignoreLayout = true;
            var button = card.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = cardImage;
            var colors = button.colors;
            colors.highlightedColor = new Color(.82f,1,.92f); colors.pressedColor = new Color(.6f,.85f,.76f);
            button.colors = colors;
            button.onClick.AddListener(() => controller.OpenBook(this, books[bookIndex]));
            var column = card.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            column.padding = new RectOffset(26,26,24,24); column.spacing = 18;
            column.childControlWidth = column.childControlHeight = true;
            column.childForceExpandWidth = true; column.childForceExpandHeight = false;
            var title = StageUITheme.Text("Title", card, StudyLibraryController.StandardizeTerminology(book.title), 36, true);
            title.fontStyle = FontStyles.Bold; title.color = new Color(.88f,1,.95f);
            title.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 112;
            var preview = StageUITheme.Text("Preview", card, StudyLibraryController.StandardizeTerminology(book.preview), 28, true);
            preview.color = new Color(.94f,.95f,.92f);
            var descriptionSize = preview.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            descriptionSize.preferredHeight = 116; descriptionSize.flexibleHeight = 1;
            var action = StageUITheme.Text("Read", card, "Open book", 32, true);
            action.fontStyle = FontStyles.Bold; action.color = new Color(1,.88f,.55f);
            action.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 60;
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
