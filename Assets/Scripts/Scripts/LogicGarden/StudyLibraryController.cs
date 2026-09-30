using TMPro;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

public class StudyLibraryController : MonoBehaviour
{
    private StudyShelfZone activeShelf;
    private GameObject readerRoot;
    private TMP_Text titleText;
    private TMP_Text leftPageText;
    private TMP_Text rightPageText;
    private TMP_Text leftPageNumberText;
    private TMP_Text rightPageNumberText;
    private TMP_Text spreadIndicatorText;
    private TMP_Text sourceText;
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
    private RawImage enlargedSlide;
    private GameObject enlargedPage;
    private Texture2D leftTexture;
    private Texture2D rightTexture;
    private RectTransform bookRect;

    public bool IsViewing(StudyShelfZone shelf) => activeShelf == shelf;
    public int CurrentSpreadStartPage => spreadStartPage;

    private void Awake()
    {
        BuildReaderUI();
    }

    private void Update()
    {
        if (readerRoot == null || !readerRoot.activeSelf) return;

        FitBook();

        if (Input.GetKeyDown(KeyCode.Escape)) CloseBook();
        else if (Input.GetKeyDown(KeyCode.LeftArrow)) PreviousSpread();
        else if (Input.GetKeyDown(KeyCode.RightArrow)) NextSpread();
    }

    public void EnterShelf(StudyShelfZone shelf)
    {
        if (activeShelf == shelf) return;

        if (activeShelf == null)
        {
            previousLockMode = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
        }
        else
        {
            activeShelf.SetFocused(false);
        }

        CloseBook();
        activeShelf = shelf;
        activeShelf.SetFocused(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ExitShelf(StudyShelfZone shelf)
    {
        if (activeShelf != shelf) return;

        CloseBook();
        activeShelf.SetFocused(false);
        activeShelf = null;
        Cursor.lockState = previousLockMode;
        Cursor.visible = previousCursorVisible;
    }

    public void OpenBook(StudyShelfZone shelf, StudyBookDefinition book)
    {
        if (shelf == null || shelf != activeShelf || book == null) return;

        currentBook = book;
        spreadStartPage = 0;
        titleText.text = StandardizeTerminology(book.title);
        sourceText.text = book.source;
        readerRoot.SetActive(true);
        readerRoot.transform.SetAsLastSibling();
        Canvas.ForceUpdateCanvases();
        FitBook();
        UpdateSpread();

        pausedPlayer = Player.LocalInstance != null
            ? Player.LocalInstance
            : FindFirstObjectByType<Player>();
        playerWasEnabled = pausedPlayer != null && pausedPlayer.enabled;
        if (pausedPlayer != null) pausedPlayer.ToggleControl(false);
    }

    public void PreviousSpread()
    {
        if (currentBook == null || spreadStartPage <= 0) return;
        spreadStartPage = Mathf.Max(0, spreadStartPage - 2);
        UpdateSpread();
    }

    private void FitBook()
    {
        // Keep the existing book layout inside the host Canvas on any aspect ratio.
        Rect rect = ((RectTransform)readerRoot.transform).rect;
        bookRect.localScale = Vector3.one * Mathf.Min(1f, rect.width / 1640f, rect.height / 940f);
    }

    public void NextSpread()
    {
        if (currentBook == null) return;
        if (spreadStartPage + 2 >= currentBook.PageCount) return;
        spreadStartPage += 2;
        UpdateSpread();
    }

    public void CloseBook()
    {
        if (enlargedPage != null) enlargedPage.SetActive(false);
        ReleasePageTextures();
        if (readerRoot != null) readerRoot.SetActive(false);
        currentBook = null;
        spreadStartPage = 0;

        if (pausedPlayer != null)
        {
            pausedPlayer.ToggleControl(playerWasEnabled);
            pausedPlayer = null;
        }
    }

    private void OnDisable()
    {
        if (activeShelf != null) ExitShelf(activeShelf);
        else CloseBook();
    }

    private void UpdateSpread()
    {
        if (currentBook == null) return;

        enlargedPage.SetActive(false);
        ReleasePageTextures();
        if (currentBook.lessonPages != null && currentBook.lessonPages.Length > 0)
        {
            int count = currentBook.lessonPages.Length;
            spreadStartPage = Mathf.Clamp(spreadStartPage, 0, count - 1);
            ShowSourcePage(leftSlide, leftDiagram, leftPageText, leftPageNumberText, spreadStartPage, ref leftTexture);
            ShowSourcePage(rightSlide, rightDiagram, rightPageText, rightPageNumberText, spreadStartPage + 1, ref rightTexture);
            spreadIndicatorText.text = "Pages " + (spreadStartPage + 1) + "–" + Mathf.Min(spreadStartPage + 2, count) + " of " + count;
            previousButton.interactable = spreadStartPage > 0;
            nextButton.interactable = spreadStartPage + 2 < count;
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

        int visibleEnd = Mathf.Min(rightIndex + 1, pages.Length);
        spreadIndicatorText.text = "Pages " + (leftIndex + 1) + "–" + visibleEnd + " of " + pages.Length;
        previousButton.interactable = spreadStartPage > 0;
        nextButton.interactable = spreadStartPage + 2 < pages.Length;
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
        Image dimmer = readerRoot.AddComponent<Image>();
        dimmer.color = new Color(0.025f, 0.02f, 0.018f, 0.88f);

        GameObject book = CreateUIObject("Open Book", readerRoot.transform);
        bookRect = book.GetComponent<RectTransform>();
        bookRect.anchorMin = bookRect.anchorMax = new Vector2(0.5f, 0.5f);
        bookRect.sizeDelta = new Vector2(1580f, 880f);
        bookRect.anchoredPosition = Vector2.zero;
        Image cover = book.AddComponent<Image>();
        cover.color = new Color(0.23f, 0.105f, 0.045f, 1f);
        Outline coverOutline = book.AddComponent<Outline>();
        coverOutline.effectColor = new Color(0.08f, 0.035f, 0.015f, 1f);
        coverOutline.effectDistance = new Vector2(8f, -8f);

        titleText = CreateText("Book Title", book.transform, 44f, FontStyles.Bold, TextAlignmentOptions.Center);
        titleText.color = new Color(1f, 0.9f, 0.62f);
        titleText.rectTransform.anchorMin = new Vector2(0.12f, 0.89f);
        titleText.rectTransform.anchorMax = new Vector2(0.88f, 0.98f);
        titleText.rectTransform.offsetMin = titleText.rectTransform.offsetMax = Vector2.zero;

        GameObject leftPage = CreatePage("Left Page", book.transform, new Vector2(0.035f, 0.12f), new Vector2(0.495f, 0.88f));
        GameObject rightPage = CreatePage("Right Page", book.transform, new Vector2(0.505f, 0.12f), new Vector2(0.965f, 0.88f));

        leftPageText = CreatePageText("Left Page Content", leftPage.transform);
        rightPageText = CreatePageText("Right Page Content", rightPage.transform);
        leftPageNumberText = CreatePageNumber(leftPage.transform);
        rightPageNumberText = CreatePageNumber(rightPage.transform);
        leftSlide = CreateSlide(leftPage.transform, "Source Slide Left");
        rightSlide = CreateSlide(rightPage.transform, "Source Slide Right");
        leftDiagram = CreateDiagramArea(leftPage.transform, "Native Diagram Left");
        rightDiagram = CreateDiagramArea(rightPage.transform, "Native Diagram Right");
        leftSlide.GetComponent<Button>().onClick.AddListener(() => Enlarge(leftSlide));
        rightSlide.GetComponent<Button>().onClick.AddListener(() => Enlarge(rightSlide));

        GameObject spine = CreateUIObject("Book Spine", book.transform);
        RectTransform spineRect = spine.GetComponent<RectTransform>();
        spineRect.anchorMin = new Vector2(0.493f, 0.115f);
        spineRect.anchorMax = new Vector2(0.507f, 0.885f);
        spineRect.offsetMin = spineRect.offsetMax = Vector2.zero;
        Image spineImage = spine.AddComponent<Image>();
        spineImage.color = new Color(0.17f, 0.075f, 0.032f, 0.95f);

        enlargedPage = CreatePage("Enlarged Source Page", book.transform, new Vector2(0.035f, 0.12f), new Vector2(0.965f, 0.88f));
        enlargedSlide = CreateSlide(enlargedPage.transform, "Source Slide Detail");
        enlargedSlide.GetComponent<Button>().onClick.AddListener(() => enlargedPage.SetActive(false));
        enlargedPage.SetActive(false);

        TMP_Text hint = CreateText("Reading Hint", book.transform, 20f, FontStyles.Normal, TextAlignmentOptions.Center);
        hint.text = "Click a page to enlarge • click again to return";
        hint.color = new Color(1f, 0.9f, 0.62f);
        SetAnchors(hint.rectTransform, new Vector2(0.12f, 0.875f), new Vector2(0.88f, 0.905f));

        previousButton = CreateButton("Previous Pages", book.transform, "<", new Color(0.43f, 0.21f, 0.08f, 1f));
        SetAnchors(previousButton.GetComponent<RectTransform>(), new Vector2(0.30f, 0.025f), new Vector2(0.38f, 0.105f));
        previousButton.onClick.AddListener(PreviousSpread);

        nextButton = CreateButton("Next Pages", book.transform, ">", new Color(0.43f, 0.21f, 0.08f, 1f));
        SetAnchors(nextButton.GetComponent<RectTransform>(), new Vector2(0.62f, 0.025f), new Vector2(0.70f, 0.105f));
        nextButton.onClick.AddListener(NextSpread);

        spreadIndicatorText = CreateText("Spread Indicator", book.transform, 24f, FontStyles.Bold, TextAlignmentOptions.Center);
        spreadIndicatorText.color = new Color(0.96f, 0.84f, 0.62f);
        SetAnchors(spreadIndicatorText.rectTransform, new Vector2(0.39f, 0.035f), new Vector2(0.61f, 0.095f));

        sourceText = CreateText("Source", book.transform, 19f, FontStyles.Italic, TextAlignmentOptions.Center);
        sourceText.color = new Color(0.87f, 0.72f, 0.5f);
        SetAnchors(sourceText.rectTransform, new Vector2(0.05f, 0.025f), new Vector2(0.27f, 0.105f));

        Button closeButton = CreateButton("Close Book", book.transform, "BACK", new Color(0.42f, 0.12f, 0.08f, 1f));
        SetAnchors(closeButton.GetComponent<RectTransform>(), new Vector2(0.75f, 0.025f), new Vector2(0.94f, 0.105f));
        closeButton.onClick.AddListener(CloseBook);

        TMP_FontAsset readingFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
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
        fallback.text = available ? string.Empty : "— END OF BOOK —";
        number.text = available ? (index + 1).ToString() : string.Empty;
        if (!available) return;
        StudyLessonPage page = currentBook.lessonPages[index];
        number.text = (index + 1) + "  •  " + page.source.Replace("IT 105_Discrete Structures 1_2nd Sem_", "");
        number.fontSize = 16f;
        SetAnchors(number.rectTransform, new Vector2(0.03f, 0.025f), new Vector2(0.97f, 0.10f));

        bool isDiagramPage = !string.IsNullOrWhiteSpace(page.diagram);
        bool isTextPage = !string.IsNullOrWhiteSpace(page.text) && !isDiagramPage;
        image.gameObject.SetActive(!isTextPage && !isDiagramPage);
        fallback.gameObject.SetActive(isTextPage);
        diagram.SetActive(isDiagramPage);
        ClearChildren(diagram.transform);
        if (isDiagramPage)
        {
            BuildNativeDiagram(diagram.transform, page.diagram);
            return;
        }
        if (isTextPage)
        {
            fallback.text = FormatStudyText(page.text);
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
        SetAnchors(area.GetComponent<RectTransform>(), new Vector2(0.045f, 0.12f), new Vector2(0.955f, 0.92f));
        area.SetActive(false);
        return area;
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    private static void BuildNativeDiagram(Transform root, string key)
    {
        switch (key)
        {
            case "relation-representations": BuildRelationRepresentations(root); break;
            case "function-mappings": BuildFunctionMappings(root); break;
            case "vertical-line-test": BuildVerticalLineTest(root); break;
            case "rooted-unrooted-trees": BuildTreeComparison(root); break;
            default: AddLabel(root, "Native diagram unavailable: " + key, 0.05f, 0.4f, 0.95f, 0.6f, 24f, FontStyles.Normal); break;
        }
    }

    private static readonly Color Ink = new Color(0.16f, 0.10f, 0.06f, 1f);
    private static readonly Color Accent = new Color(0.18f, 0.36f, 0.34f, 1f);
    private static readonly Color SoftPanel = new Color(0.86f, 0.77f, 0.58f, 0.45f);

    private static void BuildRelationRepresentations(Transform root)
    {
        AddLabel(root, "RELATION REPRESENTATION", 0f, 0.91f, 1f, 1f, 28f, FontStyles.Bold);
        AddPanel(root, 0.04f, 0.67f, 0.96f, 0.89f);
        AddLabel(root, "RELATION IN TABLE", 0.08f, 0.83f, 0.92f, 0.89f, 18f, FontStyles.Bold);
        AddLabel(root, "x      y\n−2     1\n−2     3\n 0    −3\n 1     4\n 3     1", 0.25f, 0.675f, 0.75f, 0.83f, 18f, FontStyles.Normal);
        AddPanel(root, 0.04f, 0.36f, 0.96f, 0.64f);
        AddLabel(root, "RELATION IN GRAPH", 0.08f, 0.58f, 0.92f, 0.64f, 18f, FontStyles.Bold);
        DrawAxes(root, 0.18f, 0.38f, 0.82f, 0.57f);
        float[,] points = { { -2, 1 }, { -2, 3 }, { 0, -3 }, { 1, 4 }, { 3, 1 } };
        for (int i = 0; i < points.GetLength(0); i++)
            AddDot(root, 0.50f + points[i, 0] * 0.052f, 0.475f + points[i, 1] * 0.019f, 8f, new Color(0.75f, 0.13f, 0.35f));
        AddPanel(root, 0.04f, 0.04f, 0.96f, 0.33f);
        AddLabel(root, "RELATION IN MAPPING DIAGRAM", 0.08f, 0.27f, 0.92f, 0.33f, 18f, FontStyles.Bold);
        string[] lx = { "−2", "0", "1", "3" }, ry = { "−3", "1", "3", "4" };
        float[] ly = { .245f, .195f, .145f, .095f }, ryy = { .245f, .195f, .145f, .095f };
        for (int i = 0; i < lx.Length; i++) AddLabel(root, lx[i], .18f, ly[i] - .018f, .28f, ly[i] + .018f, 17f, FontStyles.Bold);
        for (int i = 0; i < ry.Length; i++) AddLabel(root, ry[i], .72f, ryy[i] - .018f, .82f, ryy[i] + .018f, 17f, FontStyles.Bold);
        AddArrow(root, .28f, ly[0], .72f, ryy[1]); AddArrow(root, .28f, ly[0], .72f, ryy[2]);
        AddArrow(root, .28f, ly[1], .72f, ryy[0]); AddArrow(root, .28f, ly[2], .72f, ryy[3]); AddArrow(root, .28f, ly[3], .72f, ryy[1]);
    }

    private static void BuildFunctionMappings(Transform root)
    {
        AddLabel(root, "Functions can also be represented through mapping. In this case, the relation or function is represented by the set of all the connections by the arrows.", .03f, .82f, .97f, .99f, 20f, FontStyles.Normal);
        DrawMapping(root, .03f, .56f, .97f, .80f, new[] { "1", "2", "3", "4", "5" }, new[] { "3", "5", "9", "17", "33" }, new[,] { { 0, 0 }, { 1, 1 }, { 2, 2 }, { 3, 3 }, { 4, 4 } }, "X", "Y");
        DrawMapping(root, .03f, .30f, .97f, .53f, new[] { "5", "6", "7", "8", "9" }, new[] { "0", "1" }, new[,] { { 0, 1 }, { 1, 0 }, { 2, 1 }, { 3, 0 }, { 4, 0 } }, "X", "Y");
        DrawMapping(root, .03f, .04f, .97f, .27f, new[] { "7", "2", "1" }, new[] { "11", "13", "17", "19", "23" }, new[,] { { 0, 0 }, { 0, 1 }, { 1, 2 }, { 1, 3 }, { 2, 4 } }, "X", "Y");
    }

    private static void DrawMapping(Transform root, float x0, float y0, float x1, float y1, string[] left, string[] right, int[,] edges, string leftTitle, string rightTitle)
    {
        AddPanel(root, x0, y0, x1, y1);
        AddLabel(root, leftTitle, x0 + .03f, y1 - .045f, x0 + .18f, y1, 16f, FontStyles.Bold);
        AddLabel(root, rightTitle, x1 - .18f, y1 - .045f, x1 - .03f, y1, 16f, FontStyles.Bold);
        float top = y1 - .065f, bottom = y0 + .025f;
        for (int i = 0; i < left.Length; i++)
        {
            float y = EvenY(i, left.Length, top, bottom);
            AddLabel(root, left[i], x0 + .055f, y - .018f, x0 + .16f, y + .018f, 16f, FontStyles.Bold);
        }
        for (int i = 0; i < right.Length; i++)
        {
            float y = EvenY(i, right.Length, top, bottom);
            AddLabel(root, right[i], x1 - .16f, y - .018f, x1 - .055f, y + .018f, 16f, FontStyles.Bold);
        }
        for (int i = 0; i < edges.GetLength(0); i++)
            AddArrow(root, x0 + .17f, EvenY(edges[i, 0], left.Length, top, bottom), x1 - .17f, EvenY(edges[i, 1], right.Length, top, bottom));
    }

    private static float EvenY(int index, int count, float top, float bottom) => count <= 1 ? (top + bottom) * .5f : Mathf.Lerp(top, bottom, index / (float)(count - 1));

    private static void BuildVerticalLineTest(Transform root)
    {
        AddLabel(root, "Which of the following graphs represent a function?", 0f, .89f, 1f, 1f, 25f, FontStyles.Bold);
        DrawGraphCard(root, .03f, .59f, .97f, .86f, "A", 0);
        DrawGraphCard(root, .03f, .30f, .97f, .56f, "B", 1);
        DrawGraphCard(root, .03f, .01f, .97f, .27f, "C", 2);
    }

    private static void DrawGraphCard(Transform root, float x0, float y0, float x1, float y1, string label, int type)
    {
        AddPanel(root, x0, y0, x1, y1);
        AddLabel(root, label, x0 + .015f, y1 - .07f, x0 + .10f, y1, 24f, FontStyles.Bold);
        float gx0 = x0 + .13f, gx1 = x1 - .05f, gy0 = y0 + .035f, gy1 = y1 - .035f;
        AddLine(root, gx0, (gy0 + gy1) / 2f, gx1, (gy0 + gy1) / 2f, 2f, Ink);
        AddLine(root, (gx0 + gx1) / 2f, gy0, (gx0 + gx1) / 2f, gy1, 2f, Ink);
        if (type == 0)
        {
            Vector2[] p = { new(.05f,.50f), new(.18f,.72f), new(.31f,.55f), new(.43f,.28f), new(.57f,.22f), new(.70f,.48f), new(.84f,.72f), new(.95f,.64f) };
            DrawPolyline(root, p, gx0, gy0, gx1, gy1, Accent);
        }
        else if (type == 1)
        {
            Vector2[] top = { new(.18f,.50f), new(.12f,.62f), new(.20f,.75f), new(.42f,.86f), new(.72f,.92f), new(.94f,.92f) };
            Vector2[] bottom = { new(.18f,.50f), new(.12f,.38f), new(.20f,.25f), new(.42f,.14f), new(.72f,.08f), new(.94f,.08f) };
            DrawPolyline(root, top, gx0, gy0, gx1, gy1, Accent); DrawPolyline(root, bottom, gx0, gy0, gx1, gy1, Accent);
        }
        else
        {
            Vector2[] oval = new Vector2[25];
            for (int i = 0; i < oval.Length; i++) { float a = i * Mathf.PI * 2f / (oval.Length - 1); oval[i] = new Vector2(.52f + .38f * Mathf.Cos(a), .5f + .40f * Mathf.Sin(a)); }
            DrawPolyline(root, oval, gx0, gy0, gx1, gy1, Accent);
        }
        float vx = Mathf.Lerp(gx0, gx1, .72f);
        AddLine(root, vx, gy0, vx, gy1, 2f, new Color(.78f,.32f,.12f));
    }

    private static void BuildTreeComparison(Transform root)
    {
        AddLabel(root, "A TREE?", 0f, .90f, 1f, 1f, 30f, FontStyles.Bold);
        Vector2[] un = { new(.12f,.78f), new(.29f,.707f), new(.43f,.737f), new(.17f,.627f), new(.06f,.615f), new(.12f,.535f), new(.38f,.645f), new(.35f,.56f), new(.29f,.48f), new(.45f,.48f) };
        int[,] ue = { {0,1},{1,2},{1,3},{1,6},{3,4},{3,5},{6,7},{7,8},{7,9} };
        DrawNodeGraph(root, un, ue);
        AddLabel(root, "Unrooted Tree", .05f, .34f, .47f, .43f, 21f, FontStyles.Bold);
        Vector2[] rt = { new(.76f,.78f), new(.65f,.722f), new(.54f,.664f), new(.65f,.659f), new(.77f,.659f), new(.88f,.678f), new(.65f,.596f), new(.65f,.533f), new(.56f,.48f), new(.74f,.48f), new(.88f,.61f) };
        int[,] re = { {0,1},{1,2},{1,3},{1,5},{2,4},{3,6},{6,7},{7,8},{7,9},{5,10} };
        DrawNodeGraph(root, rt, re);
        AddLabel(root, "Rooted Tree", .53f, .34f, .95f, .43f, 21f, FontStyles.Bold);
    }

    private static void DrawNodeGraph(Transform root, Vector2[] nodes, int[,] edges)
    {
        for (int i = 0; i < edges.GetLength(0); i++) AddLine(root, nodes[edges[i,0]].x, nodes[edges[i,0]].y, nodes[edges[i,1]].x, nodes[edges[i,1]].y, 3f, Ink);
        foreach (Vector2 p in nodes) AddDot(root, p.x, p.y, 11f, new Color(.94f,.87f,.70f));
    }

    private static void DrawAxes(Transform root, float x0, float y0, float x1, float y1)
    {
        AddLine(root, x0, (y0+y1)/2f, x1, (y0+y1)/2f, 2f, Ink); AddLine(root, (x0+x1)/2f, y0, (x0+x1)/2f, y1, 2f, Ink);
        for (int i=1;i<8;i++) { float x=Mathf.Lerp(x0,x1,i/8f); AddLine(root,x,y0,x,y1,1f,new Color(Ink.r,Ink.g,Ink.b,.22f)); float y=Mathf.Lerp(y0,y1,i/8f); AddLine(root,x0,y,x1,y,1f,new Color(Ink.r,Ink.g,Ink.b,.22f)); }
    }

    private static void DrawPolyline(Transform root, Vector2[] points, float x0, float y0, float x1, float y1, Color color)
    { for (int i=1;i<points.Length;i++) AddLine(root, Mathf.Lerp(x0,x1,points[i-1].x), Mathf.Lerp(y0,y1,points[i-1].y), Mathf.Lerp(x0,x1,points[i].x), Mathf.Lerp(y0,y1,points[i].y), 3f, color); }

    private static void AddPanel(Transform root, float x0, float y0, float x1, float y1)
    { GameObject go=CreateUIObject("Diagram Panel",root); SetAnchors(go.GetComponent<RectTransform>(),new Vector2(x0,y0),new Vector2(x1,y1)); Image image=go.AddComponent<Image>(); image.color=SoftPanel; image.raycastTarget=false; }

    private static TMP_Text AddLabel(Transform root, string value, float x0, float y0, float x1, float y1, float size, FontStyles style)
    { TMP_Text t=CreateText("Diagram Label",root,size,style,TextAlignmentOptions.Center); t.text=value; t.color=Ink; t.textWrappingMode=TextWrappingModes.Normal; t.enableAutoSizing=true; t.fontSizeMin=Mathf.Max(11f,size-6f); t.fontSizeMax=size; SetAnchors(t.rectTransform,new Vector2(x0,y0),new Vector2(x1,y1)); return t; }

    private static void AddDot(Transform root, float x, float y, float size, Color fill)
    { TMP_Text dot=AddLabel(root,"●",x-.025f,y-.025f,x+.025f,y+.025f,size*2f,FontStyles.Normal); dot.color=fill; Outline outline=dot.gameObject.AddComponent<Outline>(); outline.effectColor=Ink; outline.effectDistance=new Vector2(1f,-1f); }

    private static void AddArrow(Transform root, float x0, float y0, float x1, float y1)
    { AddLine(root,x0,y0,x1,y1,2f,Ink); float a=Mathf.Atan2(y1-y0,x1-x0); float d=.015f; AddLine(root,x1,y1,x1-d*Mathf.Cos(a-.55f),y1-d*Mathf.Sin(a-.55f),2f,Ink); AddLine(root,x1,y1,x1-d*Mathf.Cos(a+.55f),y1-d*Mathf.Sin(a+.55f),2f,Ink); }

    private static void AddLine(Transform root, float x0, float y0, float x1, float y1, float thickness, Color color)
    { GameObject go=CreateUIObject("Diagram Line",root); RectTransform r=go.GetComponent<RectTransform>(); r.anchorMin=r.anchorMax=Vector2.zero; RectTransform rr=(RectTransform)root; Vector2 a=new Vector2(x0*rr.rect.width,y0*rr.rect.height), b=new Vector2(x1*rr.rect.width,y1*rr.rect.height); Vector2 delta=b-a; r.sizeDelta=new Vector2(delta.magnitude,thickness); r.anchoredPosition=(a+b)*.5f; r.localEulerAngles=new Vector3(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg); Image image=go.AddComponent<Image>(); image.color=color; image.raycastTarget=false; }

    private void Enlarge(RawImage page)
    {
        if (page.texture == null) return;
        enlargedSlide.texture = page.texture;
        enlargedSlide.GetComponent<AspectRatioFitter>().aspectRatio = (float)page.texture.width / page.texture.height;
        enlargedPage.SetActive(true);
    }

    private void ReleasePageTextures()
    {
        if (leftSlide != null) leftSlide.texture = null;
        if (rightSlide != null) rightSlide.texture = null;
        if (enlargedSlide != null) enlargedSlide.texture = null;
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
        Button button = content.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
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
        TMP_Text text = CreateText(name, parent, 31f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        text.color = new Color(0.16f, 0.10f, 0.06f);
        text.textWrappingMode = TextWrappingModes.Normal;
        text.enableAutoSizing = true;
        text.fontSizeMin = 22f;
        text.fontSizeMax = 34f;
        text.lineSpacing = 9f;
        text.paragraphSpacing = 11f;
        text.rectTransform.anchorMin = new Vector2(0.065f, 0.105f);
        text.rectTransform.anchorMax = new Vector2(0.935f, 0.915f);
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
        TMP_Text text = CreateText("Page Number", parent, 22f, FontStyles.Italic, TextAlignmentOptions.Center);
        text.color = new Color(0.32f, 0.21f, 0.12f);
        SetAnchors(text.rectTransform, new Vector2(0.4f, 0.025f), new Vector2(0.6f, 0.08f));
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
