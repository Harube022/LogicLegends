using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Touch and mouse ordering surface shown when a player uses a proof altar.</summary>
public sealed class ProofAltarOrderingBoard : MonoBehaviour
{
    private readonly ProofStepDragCard[] cards = new ProofStepDragCard[5];
    private Canvas canvas;
    private RectTransform panel;
    private CanvasGroup group;
    private TMP_Text heading;
    private TMP_Text instruction;
    private Button closeButton;
    private readonly Button[] strategyButtons = new Button[3];
    private DirectIndirectProofStageController stage;
    private TrialProofTabletData[] steps;
    private int[] order;
    private bool open;
    private float reveal;

    public bool IsOpen => open;

    private void Awake()
    {
        canvas = GetComponent<Canvas>();
        if (canvas == null) { enabled = false; return; }
        Build();
        panel.parent.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!open || reveal >= 1f) return;
        reveal = Mathf.Min(1f, reveal + Time.unscaledDeltaTime * 5f);
        float eased = 1f - (1f - reveal) * (1f - reveal);
        panel.localScale = Vector3.one * Mathf.Lerp(.78f, 1f, eased);
        group.alpha = eased;
    }

    public void Show(DirectIndirectProofStageController owner, TrialProofChallenge challenge, int[] currentOrder, bool neutral)
    {
        stage = owner;
        steps = challenge.tablets;
        order = currentOrder;
        heading.text = neutral ? "PROOF TRIAL · ARRANGE THE STEPS" :
            challenge.station == TrialProofMethod.Direct ? "DIRECT PROOF · ARRANGE THE STEPS" :
            "INDIRECT PROOF · ARRANGE THE STEPS";
        instruction.text = challenge.kind == TrialProofKind.ChooseAndArrange ?
            "Choose a strategy and drag the steps into order. Then confirm at the Book Statue." :
            "Drag a statement onto another row to swap them. Then confirm at the Book Statue.";
        for (int i = 0; i < strategyButtons.Length; i++)
            strategyButtons[i].gameObject.SetActive(challenge.kind == TrialProofKind.ChooseAndArrange);
        UpdateStrategy();
        Render();
        open = true;
        reveal = 0f;
        panel.localScale = Vector3.one * .78f;
        group.alpha = 0f;
        panel.parent.gameObject.SetActive(true);
        panel.parent.SetAsLastSibling();
    }

    public void Close()
    {
        if (!open) return;
        open = false;
        panel.parent.gameObject.SetActive(false);
        if (stage != null) stage.CloseAltarBoard();
    }

    public void ForceClose()
    {
        open = false;
        if (panel != null) panel.parent.gameObject.SetActive(false);
    }

    private void ChooseStrategy(int method)
    {
        if (stage == null) return;
        stage.SelectMethod(method);
        UpdateStrategy();
    }

    private void UpdateStrategy()
    {
        for (int i = 0; i < strategyButtons.Length; i++)
            strategyButtons[i].targetGraphic.color = stage != null && stage.SelectedMethod == i ?
                new Color(.34f, .53f, .27f) : new Color(.17f, .29f, .39f);
    }

    public void Swap(int from, Vector2 screenPoint, Camera eventCamera)
    {
        if (!open || from < 0 || from >= order.Length ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(panel, screenPoint, eventCamera,
                out Vector2 local))
        { Render(); return; }
        if (Mathf.Abs(local.x) > 530f || local.y > 265f || local.y < -290f)
        { Render(); return; }
        int to = Mathf.Clamp(Mathf.RoundToInt((200f - local.y) / 105f), 0, order.Length - 1);
        if (to != from)
        {
            int id = order[from];
            order[from] = order[to];
            order[to] = id;
            if (stage != null) stage.OnAltarStepsReordered();
        }
        Render();
    }

    private void Render()
    {
        for (int i = 0; i < cards.Length; i++)
        {
            bool used = order != null && steps != null && i < order.Length && order[i] < steps.Length;
            cards[i].gameObject.SetActive(used);
            if (!used) continue;
            TrialProofTabletData step = steps[order[i]];
            cards[i].SetText((i + 1) + "   " + step.statement +
                (string.IsNullOrEmpty(step.justification) ? "" : "\n       " + step.justification));
            cards[i].ResetPosition();
        }
    }

    private void Build()
    {
        TMP_FontAsset font = GetComponentInChildren<TMP_Text>(true)?.font;
        GameObject overlay = new GameObject("Altar Ordering Overlay", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image));
        RectTransform shade = (RectTransform)overlay.transform;
        shade.SetParent(transform, false);
        Stretch(shade);
        overlay.GetComponent<Image>().color = new Color(.015f, .026f, .055f, .88f);
        group = overlay.AddComponent<CanvasGroup>();

        panel = Block("Proof Altar Board", shade, new Color(.07f, .12f, .19f),
            new Vector2(1150f, 900f), Vector2.zero);
        var border = panel.gameObject.AddComponent<Outline>();
        border.effectColor = new Color(.72f, .55f, .27f);
        border.effectDistance = new Vector2(4f, -4f);
        heading = Label("Heading", panel, font, 43f, new Vector2(0f, 395f),
            new Vector2(1040f, 70f), new Color(1f, .84f, .48f));
        instruction = Label("Instructions", panel, font, 26f, new Vector2(0f, 339f),
            new Vector2(1040f, 42f), new Color(.88f, .91f, .95f));

        string[] strategyNames = { "DIRECT", "CONTRAPOSITIVE", "CONTRADICTION" };
        for (int i = 0; i < strategyButtons.Length; i++)
        {
            int method = i;
            RectTransform rect = Block("Strategy " + strategyNames[i], panel,
                new Color(.17f, .29f, .39f), new Vector2(320f, 58f),
                new Vector2((i - 1) * 350f, 281f));
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            button.onClick.AddListener(() => ChooseStrategy(method));
            Label("Strategy Label", rect, font, 25f, Vector2.zero,
                new Vector2(305f, 48f), Color.white).text = strategyNames[i];
            strategyButtons[i] = button;
        }

        for (int i = 0; i < cards.Length; i++)
        {
            RectTransform cardRect = Block("Draggable Step " + (i + 1), panel,
                i % 2 == 0 ? new Color(.15f, .27f, .38f) : new Color(.13f, .23f, .33f),
                new Vector2(1030f, 96f), new Vector2(0f, 200f - i * 105f));
            TMP_Text label = Label("Statement", cardRect, font, 30f, Vector2.zero,
                new Vector2(990f, 86f), new Color(1f, .96f, .83f));
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.enableAutoSizing = true;
            label.fontSizeMin = 20f;
            ProofStepDragCard drag = cardRect.gameObject.AddComponent<ProofStepDragCard>();
            drag.Initialize(this, canvas, i, label);
            cards[i] = drag;
        }
        RectTransform closeRect = Block("Close Altar Board", panel, new Color(.15f, .42f, .51f),
            new Vector2(330f, 68f), new Vector2(0f, -365f));
        closeButton = closeRect.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = closeRect.GetComponent<Image>();
        closeButton.onClick.AddListener(Close);
        Label("Close Label", closeRect, font, 31f, Vector2.zero,
            new Vector2(300f, 58f), Color.white).text = "BACK TO ROOM";
    }

    private static RectTransform Block(string name, Transform parent, Color color, Vector2 size, Vector2 position)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        go.GetComponent<Image>().color = color;
        return rect;
    }

    private static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, float size,
        Vector2 position, Vector2 dimensions, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}

/// <summary>One draggable proof step; a release over another row swaps their contents.</summary>
public sealed class ProofStepDragCard : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private ProofAltarOrderingBoard board;
    private Canvas canvas;
    private RectTransform rect;
    private TMP_Text label;
    private CanvasGroup group;
    private int index;
    private Vector2 home;

    public void Initialize(ProofAltarOrderingBoard owner, Canvas rootCanvas, int row, TMP_Text text)
    {
        board = owner;
        canvas = rootCanvas;
        index = row;
        label = text;
        rect = (RectTransform)transform;
        home = rect.anchoredPosition;
        group = gameObject.AddComponent<CanvasGroup>();
    }

    public void SetText(string value) { label.text = value; }
    public void ResetPosition() { rect.anchoredPosition = home; group.alpha = 1f; }
    public void OnBeginDrag(PointerEventData eventData) { group.alpha = .78f; transform.SetAsLastSibling(); }
    public void OnDrag(PointerEventData eventData)
    {
        rect.anchoredPosition += eventData.delta / Mathf.Max(.01f, canvas.scaleFactor);
    }
    public void OnEndDrag(PointerEventData eventData)
    {
        group.alpha = 1f;
        board.Swap(index, eventData.position, eventData.pressEventCamera);
    }
}
