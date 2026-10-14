using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class FunctionsRelationsControlsOnboarding
{
    private Camera guidedCamera;
    private bool guiding;
    private Vector3 savedCameraPosition, goalPosition;
    private Quaternion savedCameraRotation, goalRotation;
    private float savedFov;
    private readonly List<Behaviour> guidedBehaviours = new List<Behaviour>();
    private readonly Dictionary<Renderer, bool> avatarRenderers = new Dictionary<Renderer, bool>();
    private readonly Vector3[] worldCorners = new Vector3[8];

    private void BuildTutorialUI()
    {
        instructions = StageUITheme.Rect("Functions Relations Tutorial Instructions", safe);
        var backing = instructions.gameObject.AddComponent<Image>();
        backing.color = new Color(.02f, .09f, .14f, .88f); backing.raycastTarget = false;
        instruction = StageUITheme.Text("Instruction Text", instructions, "", 36f);
        instruction.color = Color.white;
        next = StageUITheme.Button("Tutorial Next", instructions, "Next", NextStep);
        // PRELIM's instruction panels use a flat teal action rather than the Help card frame.
        next.image.sprite = null; next.image.color = new Color(.07f, .43f, .42f);
        highlight = StageUITheme.Rect("Functions Relations Control Highlight", safe);
        Border(highlight);
        arrow = StageUITheme.Text("Tutorial Arrow", highlight, "▼", 48f).rectTransform;
        arrow.GetComponent<TMP_Text>().color = new Color(1f, .9f, .25f);
        worldRect = StageUITheme.Rect("Functions Relations World Highlight", safe);
        worldRect.anchorMin = worldRect.anchorMax = worldRect.pivot = new Vector2(.5f, .5f);
        Border(worldRect);
        var card = StageUITheme.Rect("Functions Relations Let's Start", safe);
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, .5f);
        card.sizeDelta = new Vector2(Mathf.Min(680f, safe.rect.width - 40f), 300f);
        StageUITheme.CompactSurface(card.gameObject.AddComponent<Image>());
        var title = StageUITheme.Text("Confirmation", card, "Let's start?", 44f);
        StageUITheme.Stretch(title.rectTransform, new Vector2(.08f, .57f), new Vector2(.92f, .91f));
        YesButton = StageUITheme.Button("Tutorial Yes", card, "Yes", ConfirmYes);
        NoButton = StageUITheme.Button("Tutorial No", card, "No", ConfirmNo);
        StageUITheme.Stretch((RectTransform)YesButton.transform, new Vector2(.1f, .14f), new Vector2(.46f, .46f));
        StageUITheme.Stretch((RectTransform)NoButton.transform, new Vector2(.54f, .14f), new Vector2(.9f, .46f));
        confirmation = card.gameObject; confirmation.SetActive(false);
    }
    private static void Border(RectTransform rect)
    {
        for (int i = 0; i < 4; i++)
        {
            var edge = StageUITheme.Rect("Edge " + i, rect);
            edge.gameObject.AddComponent<Image>().color = new Color(1f, .86f, .4f);
            edge.GetComponent<Image>().raycastTarget = false;
            if (i == 0) { edge.anchorMin = new Vector2(0f, 1f); edge.anchorMax = Vector2.one; edge.sizeDelta = new Vector2(0f, 5f); }
            if (i == 1) { edge.anchorMin = Vector2.zero; edge.anchorMax = new Vector2(1f, 0f); edge.sizeDelta = new Vector2(0f, 5f); }
            if (i == 2) { edge.anchorMin = Vector2.zero; edge.anchorMax = new Vector2(0f, 1f); edge.sizeDelta = new Vector2(5f, 0f); }
            if (i == 3) { edge.anchorMin = new Vector2(1f, 0f); edge.anchorMax = Vector2.one; edge.sizeDelta = new Vector2(5f, 0f); }
            edge.anchoredPosition = Vector2.zero;
        }
    }
    private void ShowStep(FunctionsRelationsTutorialStep step, string message, RectTransform target = null, bool explanation = false, Transform subject = null)
    {
        CurrentStep = step; instruction.text = message; next.gameObject.SetActive(explanation);
        worldTarget = subject;
        highlight.gameObject.SetActive(target != null); arrow.gameObject.SetActive(target != null);
        if (target != null)
        {
            highlight.SetParent(target, false); StageUITheme.Stretch(highlight, Vector2.zero, Vector2.one);
            highlight.offsetMin = -Vector2.one * 6f; highlight.offsetMax = Vector2.one * 6f; highlight.SetAsLastSibling();
        }
        worldRect.gameObject.SetActive(subject != null);
        instructions.gameObject.SetActive(true); instructions.SetAsLastSibling();
        ApplyPracticeUI(); LayoutInstructions();
    }
    private void ApplyPracticeUI()
    {
        bool board = CurrentStep == FunctionsRelationsTutorialStep.Board || CurrentStep == FunctionsRelationsTutorialStep.BoardBack;
        joystick.gameObject.SetActive(CurrentStep == FunctionsRelationsTutorialStep.Movement);
        jump.gameObject.SetActive(CurrentStep == FunctionsRelationsTutorialStep.Jump);
        hand.gameObject.SetActive(CurrentStep == FunctionsRelationsTutorialStep.Interaction || CurrentStep == FunctionsRelationsTutorialStep.Book ||
            CurrentStep == FunctionsRelationsTutorialStep.Domain || CurrentStep == FunctionsRelationsTutorialStep.Output);
        if (settings != null) settings.gameObject.SetActive(!board);
        help.HelpButton.gameObject.SetActive(false);
        ui.hud.gameObject.SetActive(CurrentStep == FunctionsRelationsTutorialStep.Timer); ui.status.gameObject.SetActive(false);
        ui.viewBoard.gameObject.SetActive(CurrentStep == FunctionsRelationsTutorialStep.ViewBoard);
        ui.submit.gameObject.SetActive(CurrentStep == FunctionsRelationsTutorialStep.Submit);
        Stage.SubmitButton.interactable = true;
        ui.boardBack.gameObject.SetActive(board); Stage.BoardView.backButton.interactable = CurrentStep == FunctionsRelationsTutorialStep.BoardBack;
        bool interact = CurrentStep == FunctionsRelationsTutorialStep.Book || CurrentStep == FunctionsRelationsTutorialStep.Domain || CurrentStep == FunctionsRelationsTutorialStep.Output;
        ui.interaction.gameObject.SetActive(interact);
        if (interact) Stage.InteractionButton.GetComponentInChildren<TMP_Text>().text = "PRACTICE: Interact with " +
            (CurrentStep == FunctionsRelationsTutorialStep.Book ? "Book" : CurrentStep == FunctionsRelationsTutorialStep.Domain ? "Domain" : "Output");
        ui.domainPanel.gameObject.SetActive(CurrentStep == FunctionsRelationsTutorialStep.Input);
        ui.rangePanel.gameObject.SetActive(CurrentStep == FunctionsRelationsTutorialStep.OutputChoice || CurrentStep == FunctionsRelationsTutorialStep.Connection || CurrentStep == FunctionsRelationsTutorialStep.Undo);
        Stage.UndoButton.interactable = CurrentStep == FunctionsRelationsTutorialStep.Undo;
        Stage.UndoButton.GetComponentInChildren<TMP_Text>().text = practiceBeam != null ? "UNDO PRACTICE CONNECTION" : "NO PRACTICE CONNECTION";
        ui.domainHint.text = "PRACTICE — Choose input 6 or 9.";
        ui.rangeHint.text = PracticeInput == null ? "PRACTICE — Select an input first." :
            "PRACTICE input " + PracticeInput + (PracticeOutput == null ? ". Codomain = {X, Y}." : ". Range = {" + PracticeOutput + "}.");
    }
    private void LateUpdate()
    {
        if (!IsActive || CurrentStep == FunctionsRelationsTutorialStep.Confirm) return;
        ApplyPracticeUI();
        if (worldTarget != null) ProjectBounds(RenderBounds(worldTarget), worldRect);
        if (practice != null && practiceLabel != null)
        {
            Vector3 screen = Camera.main.WorldToScreenPoint(practice.transform.position + Vector3.up * 1.2f);
            practiceLabel.gameObject.SetActive(screen.z > 0f);
            if (screen.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(safe, screen, null, out Vector2 point))
                practiceLabel.anchoredPosition = point;
        }
        UpdateNodeLabels(); LayoutInstructions();
        if (skip != null) skip.transform.SetAsLastSibling();
    }
    private void LayoutInstructions()
    {
        if (instructions == null || !instructions.gameObject.activeSelf) return;
        var blockers = new List<RectTransform>(ControlTutorialLayout.FindBlockers());
        blockers.AddRange(new[] { ui.hud, ui.status, ui.interaction, ui.viewBoard, ui.submit, ui.boardBack, ui.domainPanel, ui.rangePanel });
        if (practiceLabel != null) blockers.Add(practiceLabel);
        blockers.AddRange(nodeLabels);
        RectTransform target = highlight.gameObject.activeSelf ? highlight.parent as RectTransform : worldRect.gameObject.activeSelf ? worldRect : null;
        ControlTutorialLayout.Place(instructions, instruction, (RectTransform)next.transform, safe, target, false, blockers.ToArray(), arrow.gameObject.activeSelf ? arrow : null);
        ((RectTransform)next.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 88f);
    }
    private static Bounds RenderBounds(Transform target, bool includeLabels = false)
    {
        Bounds result = new Bounds(target.position, Vector3.one); bool found = false;
        foreach (var r in target.GetComponentsInChildren<Renderer>(true))
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy || !includeLabels && r.GetComponent<TMP_Text>() != null || r is LineRenderer || r.GetComponent<TMP_SubMesh>() != null) continue;
            if (!found) { result = r.bounds; found = true; } else result.Encapsulate(r.bounds);
        }
        return result;
    }
    private void ProjectBounds(Bounds bounds, RectTransform rect)
    {
        if (Camera.main == null) return;
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = -min; bool found = false;
        for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
        {
            var screen = Camera.main.WorldToScreenPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z)));
            if (screen.z <= 0f) continue;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(safe, screen, null, out Vector2 point);
            min = Vector2.Min(min, point); max = Vector2.Max(max, point); found = true;
        }
        rect.gameObject.SetActive(found);
        if (found) { rect.anchoredPosition = (min + max) * .5f; rect.sizeDelta = max - min + Vector2.one * 16f; }
    }
    private void StartCameraGuide()
    {
        if (guiding || Camera.main == null) return;
        guidedCamera = Camera.main; guiding = true;
        savedCameraPosition = guidedCamera.transform.position; savedCameraRotation = guidedCamera.transform.rotation; savedFov = guidedCamera.fieldOfView;
        guidedBehaviours.Clear(); avatarRenderers.Clear();
        foreach (var b in FindObjectsByType<Behaviour>(FindObjectsSortMode.None))
            if (b.enabled && (b == guidedCamera.GetComponent<CinemachineBrain>() || b is CameraTargetController && b.GetComponentInParent<Player>() == player))
            { guidedBehaviours.Add(b); b.enabled = false; }
        foreach (var r in player.GetComponentsInChildren<Renderer>(true)) { avatarRenderers[r] = r.enabled; r.enabled = false; }
        goalPosition = guidedCamera.transform.position; goalRotation = guidedCamera.transform.rotation;
    }
    private void Focus(Transform target)
    {
        if (!guiding || target == null) return;
        var bounds = RenderBounds(target, true);
        Vector3 forward = Stage.BoardView.boardInstructions.transform.forward; forward.y = 0f; forward.Normalize();
        if (forward.sqrMagnitude < .1f) forward = Vector3.forward;
        float tangent = Mathf.Tan(42f * Mathf.Deg2Rad * .5f);
        float distance = Mathf.Max(bounds.extents.y / .70f, Mathf.Max(bounds.extents.x, bounds.extents.z) / guidedCamera.aspect / .65f) / tangent + Mathf.Max(bounds.extents.x, bounds.extents.z);
        goalPosition = bounds.center - forward * Mathf.Max(distance, 3f) + Vector3.up * bounds.size.y * .08f;
        goalRotation = Quaternion.LookRotation(bounds.center - goalPosition, Vector3.up);
    }
    private void AnimateCamera()
    {
        if (!guiding || guidedCamera == null || Stage.BoardView.Viewing) return;
        float t = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 7f);
        guidedCamera.transform.position = Vector3.Lerp(guidedCamera.transform.position, goalPosition, t);
        guidedCamera.transform.rotation = Quaternion.Slerp(guidedCamera.transform.rotation, goalRotation, t);
        guidedCamera.fieldOfView = Mathf.Lerp(guidedCamera.fieldOfView, 42f, t);
    }
    private void RestoreCamera()
    {
        if (!guiding) return;
        if (guidedCamera != null) { guidedCamera.transform.SetPositionAndRotation(savedCameraPosition, savedCameraRotation); guidedCamera.fieldOfView = savedFov; }
        foreach (var pair in avatarRenderers) if (pair.Key != null) pair.Key.enabled = pair.Value;
        foreach (var b in guidedBehaviours) if (b != null) { b.enabled = true; if (b is CinemachineBrain brain) brain.ResetState(); }
        avatarRenderers.Clear(); guidedBehaviours.Clear(); guidedCamera = null; guiding = false;
    }
    private void CreatePracticeObject()
    {
        DestroyPracticeObject();
        var forward = Camera.main != null ? Camera.main.transform.forward : player.transform.forward; forward.y = 0f; forward.Normalize();
        if (forward.sqrMagnitude < .1f) forward = player.transform.forward;
        player.transform.rotation = Quaternion.LookRotation(forward);
        practice = GameObject.CreatePrimitive(PrimitiveType.Cube); practice.name = "Functions Relations Practice Object"; practice.layer = 2;
        practice.transform.position = player.transform.position + forward * 2.4f + Vector3.Cross(Vector3.up, forward) * -1.5f + Vector3.up;
        practice.transform.localScale = Vector3.one * 1.35f; practice.GetComponent<Collider>().isTrigger = true;
        practiceMaterial = new Material(Stage.Mapping.PracticeLaserMaterial);
        if (practiceMaterial.HasProperty("_BaseColor")) practiceMaterial.SetColor("_BaseColor", new Color(1f, .75f, .18f));
        if (practiceMaterial.HasProperty("_Color")) practiceMaterial.SetColor("_Color", new Color(1f, .75f, .18f));
        practice.GetComponent<Renderer>().sharedMaterial = practiceMaterial;
        practiceLabel = StageUITheme.Rect("Functions Relations Practice Label", safe);
        practiceLabel.anchorMin = practiceLabel.anchorMax = practiceLabel.pivot = new Vector2(.5f, .5f); practiceLabel.sizeDelta = new Vector2(290f, 80f);
        practiceLabel.gameObject.AddComponent<Image>().color = new Color(.03f, .09f, .14f, .95f);
        practiceLabel.GetComponent<Image>().raycastTarget = false;
        var label = StageUITheme.Text("Practice Label", practiceLabel, "Interact with me", 34f, true);
        StageUITheme.Stretch(label.rectTransform, Vector2.zero, Vector2.one);
    }
    private void DestroyPracticeObject()
    {
        if (practice != null) { practice.SetActive(false); Destroy(practice); } practice = null;
        if (practiceLabel != null) { practiceLabel.gameObject.SetActive(false); Destroy(practiceLabel.gameObject); } practiceLabel = null;
        if (practiceMaterial != null) Destroy(practiceMaterial); practiceMaterial = null;
    }
    private void SetPracticeChoices()
    {
        for (int i = 0; i < Stage.DomainButtons.Length; i++)
        {
            Stage.DomainButtons[i].gameObject.SetActive(i < practiceInputs.Length);
            if (i < practiceInputs.Length) Stage.DomainButtons[i].GetComponentInChildren<TMP_Text>().text = practiceInputs[i];
        }
        for (int i = 0; i < Stage.RangeButtons.Length; i++)
        {
            Stage.RangeButtons[i].gameObject.SetActive(i < practiceOutputs.Length);
            if (i < practiceOutputs.Length) Stage.RangeButtons[i].GetComponentInChildren<TMP_Text>().text = practiceOutputs[i];
        }
    }
    private void CreateNodeLabels()
    {
        if (nodeLabels.Count > 0) return;
        for (int i = 0; i < 4; i++)
        {
            var label = StageUITheme.Text("Practice Value " + i, safe, "PRACTICE " + (i < 2 ? practiceInputs[i] : practiceOutputs[i - 2]), 28f);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = label.rectTransform.pivot = new Vector2(.5f, .5f);
            label.rectTransform.sizeDelta = new Vector2(170f, 48f); label.color = StageUITheme.Gold;
            nodeLabels.Add(label.rectTransform);
        }
    }
    private void UpdateNodeLabels()
    {
        if (Camera.main == null) return;
        for (int i = 0; i < nodeLabels.Count; i++)
        {
            Transform node = i < 2 ? Stage.Mapping.DomainDiamonds[i] : Stage.Mapping.RangeDiamonds[i - 2];
            var screen = Camera.main.WorldToScreenPoint(DiamondPoint(node) + Vector3.up * .7f);
            // Selection panels already show these values. Avoid drawing world
            // captions over their headings and number buttons.
            nodeLabels[i].gameObject.SetActive(screen.z > 0f && !ui.domainPanel.gameObject.activeSelf && !ui.rangePanel.gameObject.activeSelf);
            if (screen.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(safe, screen, null, out Vector2 point)) nodeLabels[i].anchoredPosition = point;
        }
    }
    private void CreatePracticeBeam()
    {
        if (practiceRoot == null) { practiceRoot = new GameObject("Functions Relations Practice Connections"); }
        if (practiceBeam == null)
        {
            var beam = new GameObject("Practice Laser"); beam.transform.SetParent(practiceRoot.transform);
            practiceBeam = beam.AddComponent<LineRenderer>(); practiceBeam.sharedMaterial = Stage.Mapping.PracticeLaserMaterial;
            practiceBeam.useWorldSpace = true;
            practiceBeam.positionCount = 2; practiceBeam.widthMultiplier = .28f; practiceBeam.numCapVertices = 6;
        }
        int from = System.Array.IndexOf(practiceInputs, PracticeInput), to = System.Array.IndexOf(practiceOutputs, PracticeOutput);
        practiceBeam.SetPosition(0, DiamondPoint(Stage.Mapping.DomainDiamonds[from]));
        practiceBeam.SetPosition(1, DiamondPoint(Stage.Mapping.RangeDiamonds[to]));
    }
    private static Vector3 DiamondPoint(Transform node)
    {
        // Imported diamonds share offset pivots; use the visible mesh center,
        // matching the real mapping controller's geometry-based beam anchors.
        var renderer = node.GetComponent<Renderer>();
        return renderer != null ? renderer.bounds.center : node.position;
    }
    private void ResetPractice()
    {
        DestroyPracticeObject();
        if (practiceRoot != null) { practiceRoot.SetActive(false); Destroy(practiceRoot); } practiceRoot = null; practiceBeam = null;
        foreach (var rect in nodeLabels) if (rect != null) { rect.gameObject.SetActive(false); Destroy(rect.gameObject); } nodeLabels.Clear();
        PracticeInput = PracticeOutput = null;
        if (Stage != null) Stage.BoardView.framingPadding = originalBoardPadding;
        foreach (var pair in texts) if (pair.Key != null) pair.Key.text = pair.Value;
        foreach (var b in Stage.DomainButtons) if (b != null && visible.ContainsKey(b.gameObject)) b.gameObject.SetActive(visible[b.gameObject]);
        foreach (var b in Stage.RangeButtons) if (b != null && visible.ContainsKey(b.gameObject)) b.gameObject.SetActive(visible[b.gameObject]);
    }
}
