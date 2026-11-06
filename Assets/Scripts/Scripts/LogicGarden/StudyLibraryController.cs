using TMPro;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class StudyLibraryController : MonoBehaviour
{
    private StudyShelfZone activeShelf;
    private GameObject readerRoot;
    private TMP_Text leftPageText;
    private TMP_Text rightPageText;
    private TMP_Text leftPageNumberText;
    private TMP_Text rightPageNumberText;
    private Button previousButton;
    private Button nextButton;
    private StudyBookDefinition currentBook;
    private int spreadStartPage;
    private Player pausedPlayer;
    private CursorLockMode previousLockMode;
    private bool previousCursorVisible;
    private bool playerWasEnabled;
    private RawImage leftSlide;
    private RawImage rightSlide;
    private GameObject leftDiagram;
    private GameObject rightDiagram;
    private Texture2D leftTexture;
    private Texture2D rightTexture;
    private RectTransform bookRect;
    private RectTransform readerSafeArea, leftPageRect, rightPageRect;
    private UnityEngine.UI.Image readerDimmer;
    private StudyBookPageTurn pageTurn;
    private GameInput pausedInput;
    private bool readingSession, inputWasBlocked;
    private readonly List<Behaviour> pausedCameraInputs = new List<Behaviour>();
    private readonly List<Texture2D> turningTextures = new List<Texture2D>();
    private TMP_FontAsset readingFont;
    private readonly Dictionary<Renderer, bool> browsingRenderers = new Dictionary<Renderer, bool>();
    private GameObject browsingGameplayHUD;
    private bool browsingHUDWasVisible;

    public bool IsViewing(StudyShelfZone shelf) => activeShelf == shelf;
    public void ExitActiveShelf()
    {
        if (activeShelf != null) ExitShelf(activeShelf);
        else
        {
            CloseBookContent();
            if (readerRoot != null) readerRoot.SetActive(false);
            RestoreReadingControls();
        }
    }
    public int CurrentSpreadStartPage => spreadStartPage;
    public bool IsReading => currentBook!=null;
    public bool IsInReadingView => activeShelf != null;
    public bool IsTurningPage => pageTurn != null && pageTurn.IsTurning;

    private void Awake()
    {
        BuildReaderUI();
        pageTurn = gameObject.AddComponent<StudyBookPageTurn>();
    }

    private void Update()
    {
        if (readerRoot == null || !readerRoot.activeSelf) return;

        FitBook();

        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.escapeKey.wasPressedThisFrame) CloseBook();
        else if (keyboard.leftArrowKey.wasPressedThisFrame) PreviousSpread();
        else if (keyboard.rightArrowKey.wasPressedThisFrame) NextSpread();
    }

    private void LateUpdate()
    {
        // Costumes may finish loading after the close-up begins. Hide only the
        // local avatar, without changing its Animator or renderer enabled flags.
        if (!readingSession || pausedPlayer == null) return;
        foreach (var renderer in pausedPlayer.GetComponentsInChildren<Renderer>(true))
        {
            if (!browsingRenderers.ContainsKey(renderer)) browsingRenderers.Add(renderer, renderer.forceRenderingOff);
            renderer.forceRenderingOff = true;
        }
    }

    public void EnterShelf(StudyShelfZone shelf)
    {
        if(LogicLibraryTutorialController.InteractionsBlocked)return;
        if (activeShelf == shelf) return;

        if (activeShelf == null) CaptureReadingControls();
        else
        {
            activeShelf.SetFocused(false);
        }

        CloseBookContent();
        activeShelf = shelf;
        activeShelf.SetFocused(true);
        readerRoot.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ExitShelf(StudyShelfZone shelf)
    {
        if (activeShelf != shelf) return;

        CloseBookContent();
        activeShelf.SetFocused(false);
        activeShelf = null;
        readerRoot.SetActive(false);
        RestoreReadingControls();
    }

    public void OpenBook(StudyShelfZone shelf, StudyBookDefinition book)
    {
        if(LogicLibraryTutorialController.InteractionsBlocked)return;
        if (shelf == null || shelf != activeShelf || book == null) return;
        // Duplicate taps must not replace the original enabled state with the
        // reading panel's disabled state. Switching books first releases that lease.
        if (currentBook == book && readerRoot.activeSelf) return;
        if (currentBook != null) CloseBookContent();

        currentBook = book;
        spreadStartPage = 0;
        readerRoot.SetActive(true);
        bookRect.gameObject.SetActive(true); readerDimmer.enabled = true;
        readerRoot.transform.SetAsLastSibling();
        Canvas.ForceUpdateCanvases();
        FitBook();
        if (book.lessonBlocks != null) book.lessonPages = StudyLibraryPagination.Build(book, leftPageText);
        UpdateSpread();

    }

    public void PreviousSpread()
    {
        if (currentBook == null || IsTurningPage || spreadStartPage <= 0) return;
        TurnSpread(Mathf.Max(0, spreadStartPage - 2), false);
    }

    private void FitBook()
    {
        // Keep the existing book layout inside the host Canvas on any aspect ratio.
        Rect rect = readerSafeArea.rect;
        bookRect.localScale = Vector3.one * Mathf.Min(1f, rect.width / 1640f, Mathf.Max(1, rect.height - 160) / 940f);
        bookRect.anchoredPosition = new Vector2(0, 68);
    }

    public void NextSpread()
    {
        if (currentBook == null || IsTurningPage) return;
        if (spreadStartPage + 2 >= currentBook.PageCount) return;
        TurnSpread(spreadStartPage + 2, true);
    }

    public void CloseBook()
    {
        ExitActiveShelf();
    }

    private void CloseBookContent()
    {
        pageTurn?.Cancel();
        ReleasePageTextures();
        if (bookRect != null) bookRect.gameObject.SetActive(false);
        if (readerDimmer != null) readerDimmer.enabled = false;
        currentBook = null;
        spreadStartPage = 0;
    }

    private void CaptureReadingControls()
    {
        if (readingSession) return;
        readingSession = true;
        previousLockMode = Cursor.lockState; previousCursorVisible = Cursor.visible;
        pausedPlayer = Player.LocalInstance != null ? Player.LocalInstance : FindFirstObjectByType<Player>();
        playerWasEnabled = pausedPlayer != null && pausedPlayer.enabled;
        pausedInput = GameInput.Instance; inputWasBlocked = pausedInput != null && pausedInput.GameplayInputBlocked;
        pausedInput?.SetGameplayInputBlocked(true);
        pausedPlayer?.ToggleControl(false);
        LateUpdate();
        browsingGameplayHUD = DialogueManager.Instance != null ? DialogueManager.Instance.gameplayInterfacePanel : null;
        if (browsingGameplayHUD != null && !readerRoot.transform.IsChildOf(browsingGameplayHUD.transform))
        {
            browsingHUDWasVisible = browsingGameplayHUD.activeSelf;
            browsingGameplayHUD.SetActive(false);
        }
        else browsingGameplayHUD = null;
        foreach (var input in FindObjectsByType<Behaviour>(FindObjectsSortMode.None))
            if (input.enabled && (input is CameraTargetController || input is CinemachinePinchZoom ||
                input is CinemachineInputAxisController || input is ThirdPersonCameraController))
            { pausedCameraInputs.Add(input); input.enabled = false; }
        MobileLookInput.CancelAll();
    }

    private void RestoreReadingControls()
    {
        if (!readingSession) return;
        readingSession = false;
        foreach (var entry in browsingRenderers) if (entry.Key != null) entry.Key.forceRenderingOff = entry.Value;
        browsingRenderers.Clear();
        if (browsingGameplayHUD != null) browsingGameplayHUD.SetActive(browsingHUDWasVisible);
        browsingGameplayHUD = null;
        if (pausedPlayer != null)
        {
            pausedPlayer.ToggleControl(playerWasEnabled);
            pausedPlayer = null;
        }
        pausedInput?.SetGameplayInputBlocked(inputWasBlocked); pausedInput = null;
        foreach (var input in pausedCameraInputs) if (input != null) input.enabled = true;
        pausedCameraInputs.Clear(); MobileLookInput.CancelAll();
        Cursor.lockState = previousLockMode; Cursor.visible = previousCursorVisible;
    }

    private void TurnSpread(int target, bool forward)
    {
        if (leftTexture != null) turningTextures.Add(leftTexture);
        if (rightTexture != null && rightTexture != leftTexture) turningTextures.Add(rightTexture);
        leftTexture = rightTexture = null;
        pageTurn.Play(leftPageRect, rightPageRect, forward,
            () => { spreadStartPage = target; UpdateSpread(); },
            () =>
            {
                foreach (var texture in turningTextures)
                    if (texture != null && texture != leftTexture && texture != rightTexture) Resources.UnloadAsset(texture);
                turningTextures.Clear(); RefreshNavigation();
            });
    }

    private void RefreshNavigation()
    {
        previousButton.interactable = currentBook != null && !IsTurningPage && spreadStartPage > 0;
        nextButton.interactable = currentBook != null && !IsTurningPage && spreadStartPage + 2 < currentBook.PageCount;
    }

    private void OnDisable()
    {
        if (activeShelf != null) ExitShelf(activeShelf);
        else CloseBook();
    }

    private void UpdateSpread()
    {
        if (currentBook == null) return;

        ReleasePageTextures();
        if (currentBook.lessonPages != null && currentBook.lessonPages.Length > 0)
        {
            int count = currentBook.lessonPages.Length;
            spreadStartPage = Mathf.Clamp(spreadStartPage, 0, count - 1);
            ShowSourcePage(leftSlide, leftDiagram, leftPageText, leftPageNumberText, spreadStartPage, ref leftTexture);
            ShowSourcePage(rightSlide, rightDiagram, rightPageText, rightPageNumberText, spreadStartPage + 1, ref rightTexture);
            ApplyReadingFont(); RefreshNavigation();
            return;
        }

        leftSlide.transform.parent.gameObject.SetActive(false);
        rightSlide.transform.parent.gameObject.SetActive(false);

        string[] pages = currentBook.pages;
        if (pages == null || pages.Length == 0)
            pages = new[] { currentBook.content };

        int leftIndex = Mathf.Clamp(spreadStartPage, 0, pages.Length - 1);
        int rightIndex = leftIndex + 1;

        leftPageText.text = pages[leftIndex];
        leftPageNumberText.text = (leftIndex + 1).ToString();

        bool hasRightPage = rightIndex < pages.Length;
        rightPageText.text = hasRightPage ? pages[rightIndex] : "— END OF BOOK —";
        rightPageNumberText.text = hasRightPage ? (rightIndex + 1).ToString() : string.Empty;

        ApplyReadingFont(); RefreshNavigation();
    }

    private void ApplyReadingFont()
    {
        if (readingFont == null) return;
        foreach (var text in bookRect.GetComponentsInChildren<TMP_Text>(true))
            if (text.font != readingFont) text.font = readingFont;
    }

    private void BuildReaderUI()
    {
        Canvas canvas = FindCanvas();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("Study Library Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        readerRoot = CreateUIObject("Open Book Reader", canvas.transform);
        // Nested modal Canvas reuses the existing UI and EventSystem, above the HUD.
        Canvas modal = readerRoot.AddComponent<Canvas>();
        modal.overrideSorting = true;
        modal.sortingOrder = 200;
        readerRoot.AddComponent<GraphicRaycaster>();
        RectTransform rootRect = readerRoot.GetComponent<RectTransform>();
        Stretch(rootRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        readerDimmer = readerRoot.AddComponent<UnityEngine.UI.Image>();
        readerDimmer.color = new Color(0.025f, 0.02f, 0.018f, 0.88f);
        readerSafeArea = CreateUIObject("Library Reading Safe Area", readerRoot.transform).GetComponent<RectTransform>();
        Stretch(readerSafeArea, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        readerSafeArea.gameObject.AddComponent<Crystal.SafeArea>();

        GameObject book = CreateUIObject("Open Book", readerSafeArea);
        bookRect = book.GetComponent<RectTransform>();
        bookRect.anchorMin = bookRect.anchorMax = new Vector2(0.5f, 0.5f);
        bookRect.sizeDelta = new Vector2(1580f, 880f);
        bookRect.anchoredPosition = Vector2.zero;
        Image cover = book.AddComponent<Image>();
        cover.color = new Color(0.23f, 0.105f, 0.045f, 1f);
        Outline coverOutline = book.AddComponent<Outline>();
        coverOutline.effectColor = new Color(0.08f, 0.035f, 0.015f, 1f);
        coverOutline.effectDistance = new Vector2(8f, -8f);

        GameObject leftPage = CreatePage("Left Page", book.transform, new Vector2(0.035f, 0.135f), new Vector2(0.495f, 0.97f));
        GameObject rightPage = CreatePage("Right Page", book.transform, new Vector2(0.505f, 0.135f), new Vector2(0.965f, 0.97f));
        leftPageRect = (RectTransform)leftPage.transform; rightPageRect = (RectTransform)rightPage.transform;
        leftPageText = CreatePageText("Left Page Content", leftPage.transform);
        rightPageText = CreatePageText("Right Page Content", rightPage.transform);
        leftPageNumberText = CreatePageNumber(leftPage.transform);
        rightPageNumberText = CreatePageNumber(rightPage.transform);
        leftSlide = CreateSlide(leftPage.transform, "Source Slide Left");
        rightSlide = CreateSlide(rightPage.transform, "Source Slide Right");
        leftDiagram = CreateDiagramArea(leftPage.transform, "Native Diagram Left");
        rightDiagram = CreateDiagramArea(rightPage.transform, "Native Diagram Right");

        GameObject spine = CreateUIObject("Book Spine", book.transform);
        RectTransform spineRect = spine.GetComponent<RectTransform>();
        spineRect.anchorMin = new Vector2(0.493f, 0.13f);
        spineRect.anchorMax = new Vector2(0.507f, 0.975f);
        spineRect.offsetMin = spineRect.offsetMax = Vector2.zero;
        Image spineImage = spine.AddComponent<Image>();
        spineImage.color = new Color(0.17f, 0.075f, 0.032f, 0.95f);

        previousButton = CreateButton("Previous Pages", book.transform, "<", new Color(0.43f, 0.21f, 0.08f, 1f));
        SetAnchors(previousButton.GetComponent<RectTransform>(), new Vector2(0.30f, 0.005f), new Vector2(0.38f, 0.12f));
        previousButton.onClick.AddListener(PreviousSpread);

        nextButton = CreateButton("Next Pages", book.transform, ">", new Color(0.43f, 0.21f, 0.08f, 1f));
        SetAnchors(nextButton.GetComponent<RectTransform>(), new Vector2(0.62f, 0.005f), new Vector2(0.70f, 0.12f));
        nextButton.onClick.AddListener(NextSpread);

        Button closeButton = CreateButton("Close Book", readerSafeArea, "BACK", new Color(0.42f, 0.12f, 0.08f, 1f));
        var backRect = closeButton.GetComponent<RectTransform>();
        backRect.anchorMin = backRect.anchorMax = backRect.pivot = new Vector2(.5f, 0);
        backRect.anchoredPosition = new Vector2(0, 24); backRect.sizeDelta = new Vector2(280, 96);
        closeButton.onClick.AddListener(CloseBook);

        readingFont = Resources.Load<TMP_FontAsset>("StudyLibrary/LibraryReadingFont") ?? Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (readingFont != null)
            foreach (TMP_Text text in readerRoot.GetComponentsInChildren<TMP_Text>(true))
                text.font = readingFont;

        readerRoot.SetActive(false);
    }

    private void ShowSourcePage(RawImage image, GameObject diagram, TMP_Text fallback, TMP_Text number, int index, ref Texture2D texture)
    {
        bool available = index < currentBook.lessonPages.Length;
        image.transform.parent.gameObject.SetActive(available);
        diagram.SetActive(available);
        fallback.text = string.Empty;
        fallback.gameObject.SetActive(available);
        number.text = available ? (index + 1).ToString() : string.Empty;
        if (!available) return;
        StudyLessonPage page = currentBook.lessonPages[index];
        bool isDiagramPage = !string.IsNullOrWhiteSpace(page.diagram);
        bool isTextPage = !string.IsNullOrWhiteSpace(page.text) && !isDiagramPage;
        image.gameObject.SetActive(!isTextPage && !isDiagramPage);
        fallback.gameObject.SetActive(isTextPage);
        diagram.SetActive(isDiagramPage);
        ClearChildren(diagram.transform);
        if (isDiagramPage)
        {
            StudyLibraryDiagram.Build(diagram.transform, page);
            return;
        }
        if (isTextPage)
        {
            fallback.alignment = page.cover ? TextAlignmentOptions.Center : TextAlignmentOptions.TopLeft;
            fallback.text = page.text;
            return;
        }

        texture = Resources.Load<Texture2D>(page.image);
        image.texture = texture;
        if (texture != null)
            image.GetComponent<AspectRatioFitter>().aspectRatio = (float)texture.width / texture.height;
        else
        {
            fallback.text = "The source page could not be loaded.";
            Debug.LogError("Missing study page: " + page.image);
        }
    }

    private static GameObject CreateDiagramArea(Transform parent, string name)
    {
        GameObject area = CreateUIObject(name, parent);
        SetAnchors(area.GetComponent<RectTransform>(), new Vector2(0.065f, 0.12f), new Vector2(0.935f, 0.94f));
        area.SetActive(false);
        return area;
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        { parent.GetChild(i).gameObject.SetActive(false); Destroy(parent.GetChild(i).gameObject); }
    }

    private void ReleasePageTextures()
    {
        if (leftSlide != null) leftSlide.texture = null;
        if (rightSlide != null) rightSlide.texture = null;
        if (leftTexture != null) Resources.UnloadAsset(leftTexture);
        if (rightTexture != null && rightTexture != leftTexture) Resources.UnloadAsset(rightTexture);
        leftTexture = rightTexture = null;
    }

    private static RawImage CreateSlide(Transform parent, string name)
    {
        GameObject area = CreateUIObject(name + " Area", parent);
        SetAnchors(area.GetComponent<RectTransform>(), new Vector2(0.035f, 0.12f), new Vector2(0.965f, 0.92f));
        GameObject content = CreateUIObject(name, area.transform);
        RawImage image = content.AddComponent<RawImage>();
        AspectRatioFitter fit = content.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        image.raycastTarget = false;
        return image;
    }

    private static GameObject CreatePage(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject page = CreateUIObject(name, parent);
        RectTransform rect = page.GetComponent<RectTransform>();
        SetAnchors(rect, anchorMin, anchorMax);
        Image paper = page.AddComponent<Image>();
        paper.color = new Color(0.94f, 0.87f, 0.70f, 1f);
        Outline pageOutline = page.AddComponent<Outline>();
        pageOutline.effectColor = new Color(0.42f, 0.27f, 0.12f, 0.8f);
        pageOutline.effectDistance = new Vector2(3f, -3f);
        return page;
    }

    private static TMP_Text CreatePageText(string name, Transform parent)
    {
        TMP_Text text = CreateText(name, parent, StudyLibraryPagination.BodySize, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        text.color = new Color(0.16f, 0.10f, 0.06f);
        text.textWrappingMode = TextWrappingModes.Normal;
        text.enableAutoSizing = false;
        text.lineSpacing = StudyLibraryPagination.LineSpacing;
        text.paragraphSpacing = StudyLibraryPagination.ParagraphSpacing;
        text.rectTransform.anchorMin = new Vector2(0.065f, 0.12f);
        text.rectTransform.anchorMax = new Vector2(0.935f, 0.94f);
        text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        return text;
    }

    public static string StandardizeTerminology(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return Regex.Replace(value, @"\bbi[\s-]?conditional\b", "Bi-Conditional", RegexOptions.IgnoreCase);
    }

    public static string FormatStudyText(string value)
    {
        string normalized = StandardizeTerminology(value).Replace("\r\n", "\n");
        string[] lines = normalized.Split('\n');
        var result = new StringBuilder(normalized.Length + 128);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            string trimmed = line.Trim();
            if (IsMainHeading(trimmed))
                result.Append("<size=112%><b>").Append(trimmed).Append("</b></size>");
            else if (IsSubheading(trimmed))
                result.Append("<b>").Append(trimmed).Append("</b>");
            else
                result.Append(line);
            if (i + 1 < lines.Length) result.Append('\n');
        }
        return result.ToString();
    }

    private static bool IsMainHeading(string line)
    {
        if (string.IsNullOrEmpty(line) || line.Length > 64) return false;
        bool hasLetter = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (!char.IsLetter(c)) continue;
            hasLetter = true;
            if (char.IsLower(c)) return false;
        }
        return hasLetter;
    }

    private static bool IsSubheading(string line)
    {
        switch (line)
        {
            case "Definition": case "Example": case "Examples": case "Explanation":
            case "Core Rule": case "Main Rule": case "Logical Form": case "Formal Form":
            case "Formula": case "Meaning": case "Symbol": case "Properties":
            case "Characteristics": case "Conclusion": case "Practice Problem":
                return true;
            default: return false;
        }
    }

    private static TMP_Text CreatePageNumber(Transform parent)
    {
        TMP_Text text = CreateText("Page Number", parent, 24f, FontStyles.Normal, TextAlignmentOptions.BottomLeft);
        text.color = new Color(0.32f, 0.21f, 0.12f);
        SetAnchors(text.rectTransform, new Vector2(0.065f, 0.03f), new Vector2(0.935f, 0.09f));
        return text;
    }

    private static Canvas FindCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Canvas canvas in canvases)
            if (canvas.name == "Canvas 1" && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return canvas;
        foreach (Canvas canvas in canvases)
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return canvas;
        return null;
    }

    public static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    public static TMP_Text CreateText(string name, Transform parent, float fontSize, FontStyles style, TextAlignmentOptions alignment)
    {
        GameObject go = CreateUIObject(name, parent);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    public static Button CreateButton(string name, Transform parent, string label, Color color)
    {
        GameObject go = CreateUIObject(name, parent);
        Image image = go.AddComponent<Image>();
        image.color = color;
        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.2f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.25f);
        colors.disabledColor = new Color(color.r, color.g, color.b, 0.35f);
        button.colors = colors;

        TMP_Text text = CreateText("Label", go.transform, 31f, FontStyles.Bold, TextAlignmentOptions.Center);
        text.text = label;
        text.color = Color.white;
        Stretch(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        return button;
    }

    private static void SetAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    public static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
