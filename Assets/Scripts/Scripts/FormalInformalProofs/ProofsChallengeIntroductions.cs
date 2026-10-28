using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One paused, answer-free introduction per challenge, before the book starts question 1.</summary>
[DefaultExecutionOrder(1200), DisallowMultipleComponent]
public sealed class ProofsChallengeIntroductions : MonoBehaviour
{
    private enum Focus { Overview, Scrolls, Altars, Choices, Slots, Book }
    private struct Page
    {
        public Focus focus;
        public string title, text;
        public Page(Focus target, string heading, string explanation) { focus = target; title = heading; text = explanation; }
    }
    private static readonly Page[][] Lessons = {
        new[] {
            new Page(Focus.Overview, "Identify proof styles", "Classify two proof scrolls by presentation style.\nFind the scroll stands, proof altars and book.\nComplete two questions to advance."),
            new Page(Focus.Scrolls, "Read before classifying", "Read a scroll. Get This Scroll collects it; Back leaves it.\nPractice: halving a ribbon gives equal lengths.\nJudge the full reasoning, not just words or symbols."),
            new Page(Focus.Altars, "Formal and informal are both valid styles", "Formal: explicit statements with justified steps.\nInformal: valid reasoning in prose. Informal does not mean wrong.\nCarry one scroll to its altar. Use Take Back with empty hands to revise."),
            new Page(Focus.Book, "Confirm the whole arrangement", "Place both scrolls. Confirm at the book.\nWrong or incomplete: −10s; work stays. Correct: time pauses.\nStart question 2 at the book. Finish both to advance.") },
        new[] {
            new Page(Focus.Overview, "Classify a larger collection", "Classify all four scrolls at the Formal and Informal altars.\nComplete two new questions."),
            new Page(Focus.Altars, "Judge the presentation of each proof", "Practice: explain ribbon-halving with justified equations or clear prose. Both may be valid.\nClassify each scroll’s style. Several scrolls may use one altar."),
            new Page(Focus.Book, "Review, correct, and confirm", "Read → Get This Scroll → place at an altar.\nTake Back lets you move a placed scroll.\nConfirm all four at the book. Wrong: −10s; placements stay. Correct: time pauses.") },
        new[] {
            new Page(Focus.Overview, "Arrange a logical proof", "Order statements on numbered STEP pedestals.\nPut facts before steps that use them.\nComplete two questions."),
            new Page(Focus.Choices, "Find dependencies", "Practice only: S means “cut equally”; T means “equal lengths.”\nFrom S → T and S, conclude T.\nBoth premises must come before the conclusion."),
            new Page(Focus.Slots, "Select, place, and revise", "Read and collect a statement. Place it at a STEP pedestal.\nArrows mark empty slots, not answers. Return Scroll restores a held choice. Remove or replace filled slots to revise.\nConfirm the whole order at the book. Wrong: −10s; work stays.") },
        new[] {
            new Page(Focus.Overview, "Supply the missing justification", "Read the proof on the board.\nChoose the RULE that justifies its missing step.\nA justification names a premise or valid rule. Complete two questions."),
            new Page(Focus.Choices, "Match the rule to the actual pattern", "Practice only: S → T and S imply T by Modus Ponens.\nMatch the rule to the premises and claimed step."),
            new Page(Focus.Slots, "Place and check one rule", "Read and collect a RULE scroll. Place it at RULE SLOT 1.\nReturn Scroll, remove or replace to revise.\nConfirm at the book. Wrong: −10s; work stays. Correct: time pauses.") },
        new[] {
            new Page(Focus.Overview, "Formalize an informal argument", "Read the prose on the board. Identify premises and conclusion.\nBuild a formal proof with ordered statements and justifications.\nComplete two questions."),
            new Page(Focus.Choices, "Connect prose to explicit steps", "Practice only: equally cut ribbon pieces have equal lengths.\nWrite S and S → T, then derive T.\nTranslate the reasoning into justified steps."),
            new Page(Focus.Slots, "Pair statements with justifications", "Read and collect one statement or rule.\nPlace it on a STEP or matching RULE pedestal. Arrows mark empty slots.\nPut dependencies first. Return, remove or replace to revise.\nConfirm at the book. Wrong: −10s; work stays. Finish both questions.") }
    };

    private ProofsLibraryStageController stage;
    private ProofsStageUI ui;
    private readonly bool[] seen = new bool[5];
    private readonly Dictionary<GameObject, bool> hidden = new Dictionary<GameObject, bool>();
    private readonly Dictionary<Renderer, bool> renderers = new Dictionary<Renderer, bool>();
    private readonly Dictionary<Behaviour, bool> cameraInputs = new Dictionary<Behaviour, bool>();
    private readonly Dictionary<TMP_Text, Focus> locationLabels = new Dictionary<TMP_Text, Focus>();
    private RectTransform root, card, highlight;
    private TMP_Text heading, body, scrollHint;
    private ScrollRect scroll;
    private PrelimTutorialSkipControl skip;
    private Player player;
    private GameInput input;
    private CinemachineCamera tourCamera;
    private CinemachineBrain brain;
    private CinemachineBlendDefinition oldBlend;
    private bool oldIgnoreTime, oldControl, oldBlocked, oldCursor;
    private CursorLockMode oldLock;
    private float oldTime, pageAt;
    private int introducing, pageIndex;
    private Bounds target;
    private Vector3 goal;
    private GameObject examples;
    private Transform[] exampleChoices, exampleSlots;
    public bool IsOpen { get; private set; }
    public Button NextButton { get; private set; }
    public int PageIndex => pageIndex;
    public int PageCount => Lessons[introducing - 1].Length;
    public bool HasSeen(int challenge) => challenge >= 1 && challenge <= seen.Length && seen[challenge - 1];
    public bool MustIntroduceCurrentChallenge => stage != null && stage.isActiveAndEnabled && stage.WaitingForBook &&
        stage.QuestionNumber == 1 && !HasSeen(stage.ChallengeNumber);

    public void Initialize(ProofsLibraryStageController owner, ProofsStageUI presentation) { stage = owner; ui = presentation; }
    private void Update()
    {
        if (stage == null || ui == null) return;
        if (IsOpen)
        {
            if (!stage.isActiveAndEnabled || stage.Finished) Cancel();
            else NextButton.interactable = Time.unscaledTime - pageAt >= 1f && highlight.gameObject.activeSelf;
            return;
        }
        if (!MustIntroduceCurrentChallenge || stage.OnboardingPending || stage.Tutorial != null || ui.Help.IsOpen ||
            ui.BoardView.Viewing || stage.HasReaderOpen || Player.LocalInstance == null || GameInput.Instance == null ||
            GameInput.Instance.GameplayInputBlocked || Time.timeScale <= 0f) return;
        Begin();
    }

    private void Begin()
    {
        player = Player.LocalInstance; input = GameInput.Instance;
        introducing = stage.ChallengeNumber; pageIndex = 0;
        oldControl = player.enabled; oldBlocked = input.GameplayInputBlocked; oldTime = Time.timeScale;
        oldLock = Cursor.lockState; oldCursor = Cursor.visible;
        if (root == null) BuildUI();
        IsOpen = true;
        foreach (Transform child in ui.SafeArea)
            if (child != root && child.name != "PRELIM Shared Skip Tutorial") Hide(child.gameObject);
        foreach (var button in FindObjectsByType<Button>(FindObjectsSortMode.None))
            if (!button.transform.IsChildOf(root)) Hide(button.gameObject);
        var controls = GameObject.Find("Canvas 1")?.transform.Find("SafeArea/Gameplay_Interface");
        if (controls != null) Hide(controls.gameObject);
        foreach (var behaviour in FindObjectsByType<CinemachineInputAxisController>(FindObjectsSortMode.None)) Pause(behaviour);
        foreach (var behaviour in FindObjectsByType<CinemachinePinchZoom>(FindObjectsSortMode.None)) Pause(behaviour);
        foreach (var behaviour in FindObjectsByType<ThirdPersonCameraController>(FindObjectsSortMode.None)) Pause(behaviour);
        foreach (var renderer in player.GetComponentsInChildren<Renderer>(true)) HideRenderer(renderer);
        // Hide visual roof pieces only. Never disable a combined room mesh, floor collider, or gameplay object.
        foreach (var renderer in stage.BookTarget.root.GetComponentsInChildren<Renderer>(true))
        {
            for (Transform ancestor = renderer.transform; ancestor != null; ancestor = ancestor.parent)
            {
                string name = ancestor.name.ToLowerInvariant();
                bool roof = name.StartsWith("roof") || name.EndsWith(" roof") || name.Contains(" roof ") || name.Contains("_roof");
                if (name.Contains("ceiling") || roof || name.Contains("overhead stone rib") ||
                    (ancestor.parent != null && ancestor.parent.name == "ChamberDecorations" && renderer.bounds.min.y > 10))
                { HideRenderer(renderer); break; }
            }
        }
        foreach (var altar in stage.AltarTargets) { Hide(altar.gameObject); altar.gameObject.SetActive(introducing <= 2); }
        BuildExamples();
        input.SetGameplayInputBlocked(true); MobileInputUI.FindJoystick()?.ResetJoystick(); player.enabled = false;
        Time.timeScale = 0; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
        if (brain != null)
        {
            oldBlend = brain.DefaultBlend; oldIgnoreTime = brain.IgnoreTimeScale;
            brain.IgnoreTimeScale = true;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, .8f);
        }
        tourCamera = new GameObject("Proofs Challenge Introduction Camera").AddComponent<CinemachineCamera>();
        if (Camera.main != null) tourCamera.transform.SetPositionAndRotation(Camera.main.transform.position, Camera.main.transform.rotation);
        var lens = tourCamera.Lens; lens.FieldOfView = 50; tourCamera.Lens = lens;
        tourCamera.Priority = new PrioritySettings { Enabled = true, Value = 200 };
        root.gameObject.SetActive(true); root.SetAsLastSibling();
        skip = PrelimTutorialSkipControl.Create(ui.SafeArea, Skip);
        ShowPage();
    }
    private void Hide(GameObject obj) { if (!hidden.ContainsKey(obj)) hidden[obj] = obj.activeSelf; obj.SetActive(false); }
    private void HideRenderer(Renderer obj) { if (!renderers.ContainsKey(obj)) renderers[obj] = obj.enabled; obj.enabled = false; }
    private void Pause(Behaviour behaviour) { if (behaviour.enabled) { cameraInputs[behaviour] = true; behaviour.enabled = false; } }

    private void BuildExamples()
    {
        examples = new GameObject("Proofs Challenge PRACTICE Examples");
        if (introducing <= 2)
        {
            int count = introducing == 1 ? 2 : 4;
            exampleChoices = new Transform[count]; exampleSlots = stage.AltarTargets;
            for (int i = 0; i < count; i++)
            {
                var stand = Instantiate(stage.ScrollTargets[i], examples.transform);
                stand.transform.position = stage.transform.TransformPoint(new Vector3(i < count / 2 ? -9 : 9, 0, count == 2 ? -1.2f : i % 2 == 0 ? -3.3f : .2f));
                stand.SetContent("PRACTICE SCROLL", "Ribbon-halving example\nNot a gameplay answer");
                stand.SetTabletVisible(true); stand.SetState(ProofWorldPedestal.VisualState.Normal);
                stand.gameObject.SetActive(true); exampleChoices[i] = stand.transform;
                AddLocationLabel("PRACTICE SCROLL " + (char)('A' + i), stand.transform.position + Vector3.up * 4, Focus.Scrolls);
            }
            AddLocationLabel("FORMAL PROOF", stage.AltarTargets[0].position + Vector3.up * 5, Focus.Altars);
            AddLocationLabel("INFORMAL PROOF", stage.AltarTargets[1].position + Vector3.up * 5, Focus.Altars);
        }
        else
        {
            var board = Instantiate(stage.WorldBoard, examples.transform);
            board.transform.SetPositionAndRotation(stage.WorldBoard.transform.position, stage.WorldBoard.transform.rotation);
            board.gameObject.SetActive(true);
            var kind = introducing == 3 ? ProofChallengeKind.OrderSteps : introducing == 4 ? ProofChallengeKind.MissingRule : ProofChallengeKind.Formalize;
            board.Configure(new ProofChallengeData {
                kind = kind, title = "PRACTICE ONLY — Ribbon example",
                steps = new[] {
                    new ProofStepData { id = "S", statement = "S → T", justification = "Premise" },
                    new ProofStepData { id = "S2", statement = "S", justification = "Premise" },
                    new ProofStepData { id = "T", statement = "T", justification = "Modus Ponens", prerequisiteIds = new[] { 0, 1 } }
                },
                acceptableOrders = new[] { new ProofOrderData { stepIds = new[] { 0, 1, 2 } } },
                ruleOptions = new[] { "Modus Ponens", "Simplification", "Addition", "Double Negation" }, expectedRule = "Modus Ponens"
            });
            var choices = new List<Transform>(); var slots = new List<Transform>();
            foreach (var stand in board.GetComponentsInChildren<ProofWorldPedestal>())
            {
                if (stand.name.Contains("Slot")) slots.Add(stand.transform); else choices.Add(stand.transform);
            }
            exampleChoices = choices.ToArray(); exampleSlots = slots.ToArray();
            Bounds choicesBounds = FocusBounds(Focus.Choices), slotsBounds = FocusBounds(Focus.Slots);
            AddLocationLabel("CHOOSE TABLETS", new Vector3(choicesBounds.center.x, 5, choicesBounds.max.z + 2), Focus.Choices);
            AddLocationLabel(introducing == 4 ? "RULE SLOT 1" : "NUMBERED SLOTS", new Vector3(slotsBounds.center.x, 5, slotsBounds.max.z + 2), Focus.Slots);
        }
        AddLocationLabel("BOOK STATUE", stage.BookTarget.position + Vector3.up * 4, Focus.Book);
        // Cloned visuals have no route into the real inventory/validation; remove all physical interaction.
        foreach (var collider in examples.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (var label in examples.GetComponentsInChildren<TMP_Text>(true)) label.text = label.text.Replace("STATEMENT", "EXAMPLE");
    }

    private void AddLocationLabel(string text, Vector3 position, Focus focus)
    {
        var label = new GameObject("Practice location " + text).AddComponent<TextMeshPro>();
        label.transform.SetParent(examples.transform, false); label.transform.position = position;
        label.font = StageUITheme.HeadingFont; label.fontSize = 10;
        label.color = StageUITheme.Gold; label.outlineWidth = .2f; label.outlineColor = Color.black;
        label.text = text; label.alignment = TextAlignmentOptions.Center;
        label.rectTransform.sizeDelta = new Vector2(9, 2);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.enableAutoSizing = true; label.fontSizeMin = 7; label.fontSizeMax = 10;
        locationLabels[label] = focus;
    }

    private void BuildUI()
    {
        root = StageUITheme.Rect("Proofs Challenge Introduction", ui.SafeArea);
        StageUITheme.Stretch(root, Vector2.zero, Vector2.one);
        root.gameObject.AddComponent<Image>().color = Color.clear;
        highlight = StageUITheme.Rect("Challenge Object Highlight", root);
        highlight.anchorMin = highlight.anchorMax = highlight.pivot = Vector2.one * .5f;
        for (int i = 0; i < 4; i++)
        {
            var edge = StageUITheme.Rect("Highlight edge " + i, highlight);
            var image = edge.gameObject.AddComponent<Image>(); image.color = StageUITheme.Gold; image.raycastTarget = false;
            if (i < 2) { edge.anchorMin = new Vector2(0, i); edge.anchorMax = new Vector2(1, i); edge.sizeDelta = new Vector2(0, 3); }
            else { edge.anchorMin = new Vector2(i - 2, 0); edge.anchorMax = new Vector2(i - 2, 1); edge.sizeDelta = new Vector2(3, 0); }
            edge.anchoredPosition = Vector2.zero;
        }
        card = StageUITheme.Rect("Challenge Lesson Card", root);
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, 0);
        card.gameObject.AddComponent<Image>().color = new Color(.06f, .13f, .19f, .97f);
        heading = StageUITheme.Text("Challenge Lesson Title", card, "", 34);
        heading.font = StageUITheme.HeadingFont; heading.color = StageUITheme.Gold; heading.enableAutoSizing = true;
        heading.fontSizeMin = 24; heading.fontSizeMax = 34; heading.alignment = TextAlignmentOptions.Center;
        heading.rectTransform.anchorMin = new Vector2(0, 1); heading.rectTransform.anchorMax = Vector2.one;
        heading.rectTransform.pivot = new Vector2(.5f, 1); heading.rectTransform.sizeDelta = new Vector2(-40, 64);
        heading.rectTransform.anchoredPosition = new Vector2(0, -12);
        var viewport = StageUITheme.Rect("Lesson Reading Area", card);
        StageUITheme.Stretch(viewport, Vector2.zero, Vector2.one);
        viewport.offsetMin = new Vector2(24, 134); viewport.offsetMax = new Vector2(-24, -80);
        viewport.gameObject.AddComponent<Image>().color = Color.clear; viewport.gameObject.AddComponent<RectMask2D>();
        scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport;
        scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
        body = StageUITheme.Text("Challenge Lesson Explanation", viewport, "", 30, true);
        body.font = StageUITheme.ReadingFont; body.alignment = TextAlignmentOptions.TopLeft;
        body.textWrappingMode = TextWrappingModes.Normal; body.raycastTarget = false;
        body.rectTransform.anchorMin = new Vector2(0, 1); body.rectTransform.anchorMax = Vector2.one;
        body.rectTransform.pivot = new Vector2(.5f, 1); body.rectTransform.sizeDelta = Vector2.zero;
        body.rectTransform.anchoredPosition = Vector2.zero;
        body.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = body.rectTransform;
        scrollHint = StageUITheme.Text("Lesson Scroll Hint", card, "Swipe or scroll to read more", 22);
        scrollHint.color = StageUITheme.Gold; scrollHint.alignment = TextAlignmentOptions.Center; scrollHint.raycastTarget = false;
        scrollHint.rectTransform.anchorMin = Vector2.zero; scrollHint.rectTransform.anchorMax = new Vector2(1, 0);
        scrollHint.rectTransform.pivot = new Vector2(.5f, 0); scrollHint.rectTransform.sizeDelta = new Vector2(-40, 26);
        scrollHint.rectTransform.anchoredPosition = new Vector2(0, 99);
        NextButton = StageUITheme.Button("Proofs Challenge Next", card, "Next", Next);
        var next = (RectTransform)NextButton.transform; next.anchorMin = next.anchorMax = next.pivot = new Vector2(.5f, 0);
        next.sizeDelta = new Vector2(220, 76); next.anchoredPosition = new Vector2(0, 14);
        root.gameObject.SetActive(false);
    }
    private void ShowPage()
    {
        var page = Lessons[introducing - 1][pageIndex];
        foreach (var label in locationLabels) label.Key.gameObject.SetActive(page.focus == Focus.Overview || page.focus == label.Value);
        heading.text = "CHALLENGE " + introducing + " — " + page.title + "  (" + (pageIndex + 1) + "/" + PageCount + ")";
        body.text = page.text;
        target = FocusBounds(page.focus);
        foreach (var label in locationLabels)
            if (label.Key.gameObject.activeSelf) target.Encapsulate(new Bounds(label.Key.transform.position, new Vector3(9, 2, 2)));
        pageAt = Time.unscaledTime; NextButton.interactable = false;
        LayoutCard(); Canvas.ForceUpdateCanvases(); scroll.verticalNormalizedPosition = 1;
    }
    private Bounds FocusBounds(Focus focus)
    {
        if (focus == Focus.Book) return new Bounds(stage.BookTarget.position + Vector3.up * 1.5f, new Vector3(3, 3, 3));
        Transform[] subjects = focus == Focus.Altars ? stage.AltarTargets : focus == Focus.Scrolls || focus == Focus.Choices ? exampleChoices :
            focus == Focus.Slots ? exampleSlots : null;
        if (subjects == null || subjects.Length == 0) return new Bounds(stage.transform.TransformPoint(new Vector3(0, 1.5f, 1)), new Vector3(25, 3, 25));
        var bounds = new Bounds(subjects[0].position + Vector3.up * 1.5f, new Vector3(3.5f, 3, 3.5f));
        foreach (var subject in subjects) bounds.Encapsulate(new Bounds(subject.position + Vector3.up * 1.5f, new Vector3(3.5f, 3, 3.5f)));
        return bounds;
    }
    private void LayoutCard()
    {
        float width = Mathf.Min(1100, ui.SafeArea.rect.width - 48);
        float height = Mathf.Min(Mathf.Max(280, body.GetPreferredValues(body.text, width - 48, 0).y + 222), ui.SafeArea.rect.height * .42f);
        card.sizeDelta = new Vector2(width, height); card.anchoredPosition = new Vector2(0, 20);
        scrollHint.gameObject.SetActive(body.GetPreferredValues(body.text, width - 48, 0).y > height - 214);
    }
    private void LateUpdate()
    {
        if (!IsOpen) return;
        LayoutCard();
        float tangent = Mathf.Tan(25 * Mathf.Deg2Rad);
        float distance = Mathf.Max(20, target.extents.x / (tangent * Mathf.Max(.4f, Camera.main.aspect) * .8f), target.extents.z / (tangent * .38f)) * 1.15f;
        goal = target.center + Vector3.back * distance * tangent * .34f + Vector3.up * distance;
        float blend = 1 - Mathf.Exp(-Time.unscaledDeltaTime * 5.5f);
        tourCamera.transform.position = Vector3.Lerp(tourCamera.transform.position, goal, blend);
        tourCamera.transform.rotation = Quaternion.Slerp(tourCamera.transform.rotation, Quaternion.LookRotation(Vector3.down, Vector3.forward), blend);
        foreach (var label in examples.GetComponentsInChildren<TextMeshPro>()) label.transform.rotation = Camera.main.transform.rotation;
        UpdateHighlight();
    }
    private void UpdateHighlight()
    {
        Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity; bool visible = true;
        for (int x = -1; x <= 1; x += 2)
        for (int z = -1; z <= 1; z += 2)
        {
            var screen = Camera.main.WorldToScreenPoint(target.center + Vector3.Scale(target.extents, new Vector3(x, 0, z)));
            if (screen.z <= 0) { visible = false; break; }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(ui.SafeArea, screen, null, out Vector2 local);
            min = Vector2.Min(min, local); max = Vector2.Max(max, local);
        }
        var region = Rect.MinMaxRect(min.x - 7, min.y - 7, max.x + 7, max.y + 7);
        visible &= ui.SafeArea.rect.Contains(region.min) && ui.SafeArea.rect.Contains(region.max) &&
            !region.Overlaps(RelativeBounds(card)) && (skip == null || !region.Overlaps(RelativeBounds(skip.GetComponent<RectTransform>())));
        highlight.gameObject.SetActive(visible);
        if (visible) { highlight.anchoredPosition = (min + max) * .5f; highlight.sizeDelta = max - min + Vector2.one * 14; }
    }
    private Rect RelativeBounds(RectTransform rect)
    {
        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(ui.SafeArea, rect);
        return new Rect(bounds.min, bounds.size);
    }
    public void Next()
    {
        if (!IsOpen || !NextButton.interactable || Time.unscaledTime - pageAt < 1) return;
        if (++pageIndex >= PageCount) { seen[introducing - 1] = true; Cancel(); } else ShowPage();
    }
    public void Skip() { if (IsOpen) { seen[introducing - 1] = true; Cancel(); } }
    public void Cancel()
    {
        if (!IsOpen) return;
        IsOpen = false;
        if (examples != null) { examples.SetActive(false); Destroy(examples); examples = null; }
        if (skip != null) skip.Release(); skip = null;
        if (root != null) root.gameObject.SetActive(false);
        if (tourCamera != null) PrelimTutorialSkipControl.CancelCamera(tourCamera); tourCamera = null;
        if (brain != null) { brain.DefaultBlend = oldBlend; brain.IgnoreTimeScale = oldIgnoreTime; } brain = null;
        foreach (var entry in renderers) if (entry.Key != null) entry.Key.enabled = entry.Value;
        foreach (var entry in cameraInputs) if (entry.Key != null) entry.Key.enabled = entry.Value;
        foreach (var entry in hidden) if (entry.Key != null) entry.Key.SetActive(entry.Value);
        renderers.Clear(); cameraInputs.Clear(); hidden.Clear();
        locationLabels.Clear();
        Time.timeScale = oldTime;
        if (input != null) input.SetGameplayInputBlocked(oldBlocked);
        if (player != null) player.enabled = oldControl;
        Cursor.lockState = oldLock; Cursor.visible = oldCursor;
    }
    private void OnDisable() { Cancel(); }
    private void OnDestroy() { Cancel(); if (root != null) Destroy(root.gameObject); }
}
