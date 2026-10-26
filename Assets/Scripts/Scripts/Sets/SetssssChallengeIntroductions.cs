using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Once per challenge, before the real book start. Examples never enter validation.</summary>
[DefaultExecutionOrder(1200), DisallowMultipleComponent]
public sealed class SetssssChallengeIntroductions : MonoBehaviour
{
    public enum Focus { Spawner, A, B, Both, Intersection, AMinusB, BMinusA, Universe }
    private struct Page
    {
        public Focus focus; public string title, text;
        public Page(Focus target, string heading, string explanation) { focus = target; title = heading; text = explanation; }
    }
    private static readonly Page[][] Lessons =
    {
        new[] {
            new Page(Focus.Spawner, "Set membership — Elements", "Numbered blocks represent elements. The number identifies each element.\nCollect one with Pick Up Element or the hand."),
            new Page(Focus.A, "Set membership — Set A", "The left circle represents A.\nPractice: A = {201}. Place 201 inside A.\nRead the board: it may request A or B. Leave unused elements at the spawner."),
            new Page(Focus.A, "Place, submit, and correct", "Drop inside the required region. Let the element settle, then tap Submit.\nGreen: keep. Red: correct. Wrong submissions cost 10s; positions stay.\nNext returns to the waiting book.") },
        new[] {
            new Page(Focus.Both, "Sets A and B — Read the board", "A is the left circle. B is the right circle.\nNear the spawner, tap View Board to read both sets."),
            new Page(Focus.Both, "Sets A and B — Example only", "Practice only: A = {201, 202}; B = {203}.\nPlace 201 and 202 in A-only. Place 203 in B-only.\nNo shared elements: keep A ∩ B empty."),
            new Page(Focus.Both, "Submit the complete arrangement", "Place every listed element. Let them settle, then Submit.\nKeep green placements; fix red ones. Wrong: −10s.\nFinish two questions. Next returns to the waiting book.") },
        new[] {
            new Page(Focus.Intersection, "Intersection — A ∩ B", "A ∩ B is the intersection: elements in both sets.\nA ∪ B is the union of both circles, not just the overlap."),
            new Page(Focus.Both, "Intersection — Example only", "Practice only: A = {201, 202}; B = {202, 203}.\n201 → A-only. 203 → B-only. 202 → A ∩ B.\nCompare your question’s sets on the board."),
            new Page(Focus.Intersection, "Check and correct", "Place each element once. Let it settle, then Submit.\nKeep green placements; correct red ones. Wrong: −10s; positions stay.\nNext returns to the waiting book.") },
        new[] {
            new Page(Focus.AMinusB, "Set differences — A − B", "A − B means in A, not B.\nUse its separate answer region.\nPractice: A = {201, 202}; B = {202, 203}. Then A − B = {201}."),
            new Page(Focus.BMinusA, "Set differences — B − A", "B − A means in B, not A.\nIn this practice example, B − A = {203}.\nShared element 202 goes in A ∩ B, not either difference."),
            new Page(Focus.Both, "Five answer regions", "Fill full A, full B and A ∩ B too. Each block counts once.\nPractice copies: 201 → A and A − B.\n203 → B and B − A.\n202 → A, B and A ∩ B."),
            new Page(Focus.Spawner, "Obtain duplicate element blocks", "Spawner pickups leave an identical replacement. Collect again for more copies.\nPicking up a placed element creates no copy."),
            new Page(Focus.Both, "Submit all five regions", "Fill A, B, A ∩ B, A − B and B − A. Let blocks settle, then Submit.\nKeep green placements. Correct red or missing ones. Wrong: −10s; positions stay.\nNext returns to the waiting book.") },
        new[] {
            new Page(Focus.Universe, "Complement — Universal set U", "U contains every element considered in the question.\nA complement is inside U but outside the named set.\nRead U and that set on the board."),
            new Page(Focus.A, "Complement — Example only", "Practice only: U = {201, 202, 203, 204}; A = {201, 203}.\n201 and 203 → inside A. 202 and 204 → outside A, inside U.\nYour question may name A or B. Only that circle is active."),
            new Page(Focus.Universe, "Rearrange, then submit", "Reposition the supplied elements.\nMembers go inside the named circle. Other U elements go outside it, inside U. Use each once; no copies.\nSubmit. Fix red placements and retry. Wrong: −10s. Next returns to the waiting book.") }
    };

    private SetssssChallengeOne stage;
    private SetssssHelpUI help;
    private readonly bool[] seen = new bool[5];
    private readonly Dictionary<GameObject, bool> hiddenUI = new Dictionary<GameObject, bool>();
    private readonly Dictionary<Behaviour, bool> cameraInputs = new Dictionary<Behaviour, bool>();
    private readonly Dictionary<Renderer, bool> avatarRenderers = new Dictionary<Renderer, bool>();
    private RectTransform safe, root, card, highlight, bodyViewport;
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
    private float oldTimeScale, pageAt;
    private int pageIndex, introducing;
    private Vector3 goal;
    private Quaternion goalRotation;
    private Bounds target;
    private GameObject examples;
    public bool IsOpen { get; private set; }
    public Focus CurrentFocus => Lessons[introducing - 1][pageIndex].focus;
    public Button NextButton { get; private set; }
    public int PageIndex => pageIndex;
    public int PageCount => Lessons[introducing - 1].Length;
    public bool HasSeen(int number) => number >= 1 && number <= 5 && seen[number - 1];
    public bool MustIntroduceCurrentChallenge => stage != null && stage.isActiveAndEnabled && !stage.Started && !stage.Complete && !stage.IsGameOver && !HasSeen(stage.ChallengeNumber);

    public void Initialize(SetssssChallengeOne owner)
    {
        stage = owner;
        help = FindFirstObjectByType<SetssssHelpUI>(FindObjectsInactive.Include);
        safe = GameObject.Find("Canvas 1")?.transform.Find("SafeArea") as RectTransform;
        var labels = GetComponent<SetssssDiagramLabels>();
        if (labels == null) labels = gameObject.AddComponent<SetssssDiagramLabels>();
        labels.Initialize(owner);
    }

    private void Update()
    {
        if (stage == null || safe == null) return;
        if (IsOpen)
        {
            if (!stage.isActiveAndEnabled || stage.IsGameOver || stage.Complete) Cancel();
            else NextButton.interactable = Time.unscaledTime - pageAt >= 1f && highlight.gameObject.activeSelf;
            return;
        }
        if (!MustIntroduceCurrentChallenge || help != null && help.BlocksChallengeIntroduction || Player.LocalInstance == null || GameInput.Instance == null ||
            GameInput.Instance.GameplayInputBlocked || Time.timeScale <= 0f ||
            SetssssControlsOnboarding.Active != null && SetssssControlsOnboarding.Active.IsActive ||
            FindFirstObjectByType<SetssssBoardView>()?.IsViewingBoard == true) return;
        Begin();
    }

    private void Begin()
    {
        player = Player.LocalInstance; input = GameInput.Instance;
        introducing = stage.ChallengeNumber; pageIndex = 0;
        oldControl = player.enabled; oldBlocked = input.GameplayInputBlocked; oldTimeScale = Time.timeScale;
        oldLock = Cursor.lockState; oldCursor = Cursor.visible;
        if (root == null) BuildUI();
        stage.Hud?.Notice.Hide();
        hiddenUI.Clear();
        foreach (Transform child in safe)
            if (child != root && child.name != "PRELIM Shared Skip Tutorial")
            { hiddenUI[child.gameObject] = child.gameObject.activeSelf; child.gameObject.SetActive(false); }
        // The Admin button uses a separate canvas in this scene.
        foreach (var button in FindObjectsByType<Button>(FindObjectsSortMode.None))
            if (!button.transform.IsChildOf(root) && button.GetComponent<PrelimTutorialSkipControl>() == null)
            { hiddenUI[button.gameObject] = button.gameObject.activeSelf; button.gameObject.SetActive(false); }
        foreach (var behaviour in FindObjectsByType<CinemachineInputAxisController>(FindObjectsSortMode.None)) Pause(behaviour);
        foreach (var behaviour in FindObjectsByType<CinemachinePinchZoom>(FindObjectsSortMode.None)) Pause(behaviour);
        foreach (var behaviour in FindObjectsByType<ThirdPersonCameraController>(FindObjectsSortMode.None)) Pause(behaviour);
        foreach (var renderer in player.GetComponentsInChildren<Renderer>(true))
        { avatarRenderers[renderer] = renderer.enabled; renderer.enabled = false; }
        input.SetGameplayInputBlocked(true); MobileInputUI.FindJoystick()?.ResetJoystick(); player.enabled = false;
        Time.timeScale = 0f; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
        if (brain != null)
        {
            oldBlend = brain.DefaultBlend; oldIgnoreTime = brain.IgnoreTimeScale;
            brain.IgnoreTimeScale = true;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, .8f);
        }
        var cameraObject = new GameObject("Sets Challenge Introduction Camera");
        tourCamera = cameraObject.AddComponent<CinemachineCamera>();
        if (Camera.main != null) tourCamera.transform.SetPositionAndRotation(Camera.main.transform.position, Camera.main.transform.rotation);
        var lens = tourCamera.Lens; lens.FieldOfView = 50f; tourCamera.Lens = lens;
        tourCamera.Priority = new PrioritySettings { Enabled = true, Value = 200 };
        IsOpen = true; stage.RefreshIntroductionPreview();
        root.gameObject.SetActive(true); root.SetAsLastSibling();
        skip = PrelimTutorialSkipControl.Create(safe, Skip);
        ShowPage();
    }

    private void Pause(Behaviour behaviour)
    { if (behaviour.enabled) { cameraInputs[behaviour] = true; behaviour.enabled = false; } }

    private void BuildUI()
    {
        root = StageUITheme.Rect("Sets Challenge Introduction", safe);
        StageUITheme.Stretch(root, Vector2.zero, Vector2.one);
        root.gameObject.AddComponent<Image>().color = Color.clear;
        highlight = StageUITheme.Rect("Challenge Region Highlight", root);
        highlight.anchorMin = highlight.anchorMax = highlight.pivot = new Vector2(.5f, .5f);
        for (int i = 0; i < 4; i++)
        {
            var edge = StageUITheme.Rect("Highlight edge " + i, highlight);
            edge.gameObject.AddComponent<Image>().color = StageUITheme.Gold;
            edge.GetComponent<Image>().raycastTarget = false;
            if (i < 2) { edge.anchorMin = new Vector2(0, i); edge.anchorMax = new Vector2(1, i); edge.sizeDelta = new Vector2(0, 3); }
            else { edge.anchorMin = new Vector2(i - 2, 0); edge.anchorMax = new Vector2(i - 2, 1); edge.sizeDelta = new Vector2(3, 0); }
            edge.anchoredPosition = Vector2.zero;
        }
        card = StageUITheme.Rect("Challenge Lesson Card", root);
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, 0);
        card.gameObject.AddComponent<Image>().color = new Color(.06f, .13f, .19f, .97f);
        heading = StageUITheme.Text("Challenge Lesson Title", card, "", 34);
        heading.font = StageUITheme.HeadingFont; heading.color = StageUITheme.Gold;
        heading.enableAutoSizing = true; heading.fontSizeMin = 24; heading.fontSizeMax = 34;
        heading.alignment = TextAlignmentOptions.Center;
        heading.rectTransform.anchorMin = new Vector2(0, 1); heading.rectTransform.anchorMax = Vector2.one;
        heading.rectTransform.pivot = new Vector2(.5f, 1); heading.rectTransform.sizeDelta = new Vector2(-40, 64);
        heading.rectTransform.anchoredPosition = new Vector2(0, -12);
        bodyViewport = StageUITheme.Rect("Lesson Reading Area", card);
        StageUITheme.Stretch(bodyViewport, Vector2.zero, Vector2.one);
        bodyViewport.offsetMin = new Vector2(24, 134); bodyViewport.offsetMax = new Vector2(-24, -80);
        bodyViewport.gameObject.AddComponent<Image>().color = Color.clear;
        bodyViewport.gameObject.AddComponent<RectMask2D>();
        scroll = bodyViewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = bodyViewport;
        scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
        body = StageUITheme.Text("Challenge Lesson Explanation", bodyViewport, "", 30, true);
        body.font = StageUITheme.ReadingFont; body.alignment = TextAlignmentOptions.TopLeft;
        body.textWrappingMode = TextWrappingModes.Normal; body.raycastTarget = false;
        var textRect = body.rectTransform; textRect.anchorMin = new Vector2(0, 1); textRect.anchorMax = Vector2.one;
        textRect.pivot = new Vector2(.5f, 1); textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;
        body.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = textRect;
        scrollHint = StageUITheme.Text("Lesson Scroll Hint", card, "Swipe or scroll to read more", 22);
        scrollHint.color = StageUITheme.Gold; scrollHint.alignment = TextAlignmentOptions.Center;
        scrollHint.raycastTarget = false;
        scrollHint.rectTransform.anchorMin = new Vector2(0, 0); scrollHint.rectTransform.anchorMax = new Vector2(1, 0);
        scrollHint.rectTransform.pivot = new Vector2(.5f, 0); scrollHint.rectTransform.sizeDelta = new Vector2(-40, 26);
        scrollHint.rectTransform.anchoredPosition = new Vector2(0, 99);
        NextButton = StageUITheme.Button("Challenge Introduction Next", card, "Next", Next);
        var nextRect = (RectTransform)NextButton.transform; nextRect.anchorMin = nextRect.anchorMax = nextRect.pivot = new Vector2(.5f, 0);
        nextRect.sizeDelta = new Vector2(220, 76); nextRect.anchoredPosition = new Vector2(0, 14);
        root.gameObject.SetActive(false);
    }

    private void ShowPage()
    {
        var page = Lessons[introducing - 1][pageIndex];
        heading.text = "CHALLENGE " + introducing + " — " + page.title + "  (" + (pageIndex + 1) + "/" + PageCount + ")";
        body.text = page.text;
        if (examples != null) { examples.SetActive(false); Destroy(examples); examples = null; }
        if (page.focus == Focus.Spawner)
        {
            examples = stage.CreateIntroductionExamples();
            body.text += " The displayed 201–203 blocks are labeled practice examples, not gameplay answers.";
        }
        pageAt = Time.unscaledTime; NextButton.interactable = false;
        target = stage.IntroductionBounds(page.focus); FitCamera(); LayoutCard();
        Canvas.ForceUpdateCanvases(); scroll.verticalNormalizedPosition = 1;
    }

    private void FitCamera()
    {
        float aspect = Mathf.Max(.45f, Camera.main != null ? Camera.main.aspect : 1.78f);
        float tangent = Mathf.Tan(25 * Mathf.Deg2Rad);
        Bounds frame = target;
        if (CurrentFocus != Focus.Spawner && CurrentFocus != Focus.AMinusB && CurrentFocus != Focus.BMinusA)
            frame.Encapsulate(new Vector3(target.center.x, target.max.y + 6f, target.max.z + 10f));
        // Reserve a top strip for Skip and a lower strip for the reading panel.
        float distance = Mathf.Max(26, frame.extents.x / (tangent * aspect * .88f), frame.extents.z / (tangent * .38f)) * 1.1f;
        Vector3 focus = frame.center + Vector3.back * distance * tangent * .34f;
        goal = focus + Vector3.up * distance;
        goalRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
    }

    private void LayoutCard()
    {
        float width = Mathf.Min(1100, safe.rect.width - 48);
        float height = Mathf.Min(Mathf.Max(280, body.GetPreferredValues(body.text, width - 48, 0).y + 222), safe.rect.height * .42f);
        card.sizeDelta = new Vector2(width, height); card.anchoredPosition = new Vector2(0, 20);
        scrollHint.gameObject.SetActive(body.GetPreferredValues(body.text, width - 48, 0).y > height - 214);
    }

    private void LateUpdate()
    {
        if (!IsOpen) return;
        LayoutCard(); FitCamera();
        float blend = 1 - Mathf.Exp(-Time.unscaledDeltaTime * 5.5f);
        tourCamera.transform.position = Vector3.Lerp(tourCamera.transform.position, goal, blend);
        tourCamera.transform.rotation = Quaternion.Slerp(tourCamera.transform.rotation, goalRotation, blend);
        if (examples != null && Camera.main != null)
            foreach (var label in examples.GetComponentsInChildren<TextMeshPro>()) label.transform.rotation = Camera.main.transform.rotation;
        UpdateHighlight();
    }

    private void UpdateHighlight()
    {
        if (Camera.main == null) return;
        Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
        bool visible = true;
        for (int x = -1; x <= 1; x += 2)
        for (int z = -1; z <= 1; z += 2)
        {
            Vector3 point = target.center + Vector3.Scale(target.extents, new Vector3(x, 0, z));
            Vector3 screen = Camera.main.WorldToScreenPoint(point);
            if (screen.z <= 0) { visible = false; break; }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(safe, screen, null, out Vector2 local);
            min = Vector2.Min(min, local); max = Vector2.Max(max, local);
        }
        // While the camera travels, do not sweep the reticle across the reading
        // panel or Skip. Reveal it when the actual target is safely framed.
        if (visible)
        {
            var region = Rect.MinMaxRect(min.x - 7, min.y - 7, max.x + 7, max.y + 7);
            visible = !region.Overlaps(SetssssHudView.RelativeBounds(card, safe)) &&
                (skip == null || !region.Overlaps(SetssssHudView.RelativeBounds(skip.GetComponent<RectTransform>(), safe)));
        }
        highlight.gameObject.SetActive(visible);
        if (visible) { highlight.anchoredPosition = (min + max) * .5f; highlight.sizeDelta = max - min + Vector2.one * 14; }
    }

    public void Next()
    {
        if (!IsOpen || !NextButton.interactable || Time.unscaledTime - pageAt < 1f) return;
        if (++pageIndex >= Lessons[introducing - 1].Length) { seen[introducing - 1] = true; Cancel(); }
        else ShowPage();
    }
    public void Skip() { if (IsOpen) { seen[introducing - 1] = true; Cancel(); } }
    public void ResetPlaythrough() { Cancel(); System.Array.Clear(seen, 0, seen.Length); }
    public void Cancel()
    {
        if (!IsOpen) return;
        IsOpen = false;
        if (examples != null) { examples.SetActive(false); Destroy(examples); examples = null; }
        if (skip != null) skip.Release(); skip = null;
        if (root != null) root.gameObject.SetActive(false);
        if (tourCamera != null) PrelimTutorialSkipControl.CancelCamera(tourCamera); tourCamera = null;
        if (brain != null) { brain.DefaultBlend = oldBlend; brain.IgnoreTimeScale = oldIgnoreTime; } brain = null;
        foreach (var entry in cameraInputs) if (entry.Key != null) entry.Key.enabled = entry.Value;
        foreach (var entry in avatarRenderers) if (entry.Key != null) entry.Key.enabled = entry.Value;
        cameraInputs.Clear(); avatarRenderers.Clear();
        foreach (var entry in hiddenUI) if (entry.Key != null) entry.Key.SetActive(entry.Value);
        hiddenUI.Clear();
        Time.timeScale = oldTimeScale;
        if (input != null) input.SetGameplayInputBlocked(oldBlocked);
        if (player != null) player.enabled = oldControl;
        Cursor.lockState = oldLock; Cursor.visible = oldCursor;
        stage.RefreshIntroductionPreview();
        if (stage.isActiveAndEnabled && HasSeen(stage.ChallengeNumber) && stage.ChallengeNumber > 1 && !stage.Started && !stage.Complete && !stage.IsGameOver)
            stage.Hud?.Notice.ShowWaiting(stage.ChallengeNumber - 1, stage.ChallengeNumber);
    }
    private void OnDisable() { Cancel(); }
    private void OnDestroy() { Cancel(); if (root != null) Destroy(root.gameObject); }
}

public sealed partial class SetssssChallengeOne
{
    internal Bounds IntroductionBounds(SetssssChallengeIntroductions.Focus focus)
    {
        switch (focus)
        {
            case SetssssChallengeIntroductions.Focus.Spawner: return GetRendererBounds(spawner.gameObject);
            case SetssssChallengeIntroductions.Focus.A: return aCircleBounds;
            case SetssssChallengeIntroductions.Focus.B: return bCircleBounds;
            case SetssssChallengeIntroductions.Focus.Intersection:
                return new Bounds((aCircleBounds.center + bCircleBounds.center) * .5f,
                    new Vector3(aCircleBounds.max.x - bCircleBounds.min.x, .4f, aCircleBounds.size.z * .68f));
            case SetssssChallengeIntroductions.Focus.AMinusB: return aMinusBBounds;
            case SetssssChallengeIntroductions.Focus.BMinusA: return bMinusABounds;
            case SetssssChallengeIntroductions.Focus.Universe: return universalBounds;
            default: return diagramBounds;
        }
    }
    internal void RefreshIntroductionPreview() { RefreshDifferenceRegions(); RefreshComplementVisuals(); }
    internal GameObject CreateIntroductionExamples()
    {
        var root = new GameObject("Sets PRACTICE EXAMPLES — not gameplay elements");
        var source = numberedBlocks[0].GetComponent<MeshFilter>();
        if (source == null) return root;
        Bounds stand = GetRendererBounds(spawner.gameObject);
        for (int i = 0; i < 3; i++)
        {
            var visual = new GameObject("Example element " + (201 + i), typeof(MeshFilter), typeof(MeshRenderer));
            visual.transform.SetParent(root.transform, false);
            visual.GetComponent<MeshFilter>().sharedMesh = source.sharedMesh;
            var renderer = visual.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = source.GetComponent<Renderer>().sharedMaterials;
            visual.transform.localScale = source.transform.lossyScale;
            Vector3 position = stand.center + new Vector3((i - 1) * 4f, 2f, 0);
            visual.transform.position += position - renderer.bounds.center;
            var label = new GameObject("Practice example label", typeof(TextMeshPro)).GetComponent<TextMeshPro>();
            label.transform.SetParent(root.transform, false);
            label.font = StageUITheme.ReadingFont; label.fontSize = 5;
            label.text = (201 + i) + "\nEXAMPLE"; label.color = StageUITheme.Gold;
            label.alignment = TextAlignmentOptions.Center; label.rectTransform.sizeDelta = new Vector2(4, 2.5f);
            label.transform.position = new Vector3(position.x, renderer.bounds.max.y + 1.5f, position.z);
        }
        return root;
    }
    internal string DisplayedComplementSet => challengeNumber == 5 && started && activeComplement != null ? activeComplement.setName :
        challengeNumber == 5 && introductions != null && introductions.IsOpen ? "A" : "";
}
