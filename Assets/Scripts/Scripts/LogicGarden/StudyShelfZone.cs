using TMPro;
using Unity.Cinemachine;
using UnityEngine;
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

    private GameObject previewRoot;
    private Collider activePlayerCollider;

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
        SetFocused(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        Player player = other.GetComponentInParent<Player>();
        if (player == null) return;
        if (Player.LocalInstance != null && player != Player.LocalInstance) return;

        activePlayerCollider = other;
        if (controller != null) controller.EnterShelf(this);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other != activePlayerCollider) return;
        activePlayerCollider = null;
        if (controller != null) controller.ExitShelf(this);
    }

    public void SetFocused(bool focused)
    {
        if (focusCamera != null) focusCamera.Priority.Value = focused ? 25 : -10;
        if (previewRoot != null) previewRoot.SetActive(focused);
        if (focused && previewRoot != null)
            foreach (Canvas canvas in previewRoot.GetComponentsInChildren<Canvas>(true))
                canvas.worldCamera = Camera.main;
    }

    private void BuildWorldUI()
    {
        GameObject label = CreateWorldCanvas(shelfTitle + " Label", labelWorldPosition, new Vector2(1100f, 270f), 0.0022f);
        Image labelBackground = label.AddComponent<Image>();
        labelBackground.raycastTarget = false;
        labelBackground.color = new Color(0.035f, 0.11f, 0.13f, 0.96f);
        Outline labelOutline = label.AddComponent<Outline>();
        labelOutline.effectColor = new Color(0.25f, 0.95f, 0.7f, 0.95f);
        labelOutline.effectDistance = new Vector2(5f, -5f);
        TMP_Text labelText = StudyLibraryController.CreateText("Period", label.transform, 118f, FontStyles.Bold, TextAlignmentOptions.Center);
        labelText.text = shelfTitle;
        labelText.color = new Color(0.82f, 1f, 0.91f);
        StudyLibraryController.Stretch(labelText.rectTransform, Vector2.zero, Vector2.one, new Vector2(20f, 10f), new Vector2(-20f, -10f));

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
            title.text = book.title;
            title.color = new Color(0.82f, 1f, 0.91f);
            title.enableAutoSizing = true;
            title.fontSizeMin = 20f;
            title.fontSizeMax = 44f;
            title.rectTransform.anchorMin = new Vector2(0.05f, 0.43f);
            title.rectTransform.anchorMax = new Vector2(0.95f, 0.94f);
            title.rectTransform.offsetMin = title.rectTransform.offsetMax = Vector2.zero;

            TMP_Text preview = StudyLibraryController.CreateText("Preview", card.transform, 29f, FontStyles.Normal, TextAlignmentOptions.Center);
            preview.text = book.preview;
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
