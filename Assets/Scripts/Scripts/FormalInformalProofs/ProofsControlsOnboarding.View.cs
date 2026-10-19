using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ProofsControlsOnboarding
{
    Camera guidedCamera;
    bool guiding;
    Vector3 savedCameraPosition, goalPosition;
    Quaternion savedCameraRotation, goalRotation;
    float savedFov;
    readonly List<Behaviour> guidedBehaviours = new List<Behaviour>();
    readonly Dictionary<Renderer, bool> avatarRenderers = new Dictionary<Renderer, bool>();
    readonly Dictionary<RectTransform, RectLayout> layouts = new Dictionary<RectTransform, RectLayout>();
    struct RectLayout
    {
        public Vector2 min, max, pivot, position, size;
        public RectLayout(RectTransform r) { min = r.anchorMin; max = r.anchorMax; pivot = r.pivot; position = r.anchoredPosition; size = r.sizeDelta; }
        public void Restore(RectTransform r) { r.anchorMin = min; r.anchorMax = max; r.pivot = pivot; r.sizeDelta = size; r.anchoredPosition = position; }
    }
    void BuildUI()
    {
        layouts.Clear();
        foreach (var rect in new[] { (RectTransform)stage.ReaderPanel.transform, (RectTransform)stage.TakeBackPanel.transform, (RectTransform)stage.TakeBackButtons[0].transform, (RectTransform)ui.ViewButton.transform }) layouts[rect] = new RectLayout(rect);
        instructions = StageUITheme.Rect("Proofs Tutorial Instructions", safe);
        var image = instructions.gameObject.AddComponent<Image>(); image.color = new Color(.02f, .09f, .14f, .92f); image.raycastTarget = false;
        instruction = StageUITheme.Text("Instruction Text", instructions, "", 36); instruction.color = Color.white;
        next = StageUITheme.Button("Tutorial Next", instructions, "Next", NextStep);
        next.image.sprite = null; next.image.color = new Color(.07f, .43f, .42f);
        highlight = StageUITheme.Rect("Proofs Control Highlight", safe); Border(highlight);
        arrow = StageUITheme.Text("Tutorial Arrow", highlight, "▼", 48).rectTransform; arrow.GetComponent<TMP_Text>().color = new Color(1, .9f, .25f);
        worldRect = StageUITheme.Rect("Proofs World Highlight", safe); Border(worldRect);
        worldRect.anchorMin = worldRect.anchorMax = worldRect.pivot = new Vector2(.5f, .5f);
        var card = StageUITheme.Rect("Proofs Let's Start", safe);
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, .5f); card.sizeDelta = new Vector2(Mathf.Min(680, safe.rect.width - 40), 300);
        StageUITheme.CompactSurface(card.gameObject.AddComponent<Image>());
        var title = StageUITheme.Text("Confirmation", card, "Let's start?", 44);
        StageUITheme.Stretch(title.rectTransform, new Vector2(.08f, .57f), new Vector2(.92f, .91f));
        YesButton = StageUITheme.Button("Tutorial Yes", card, "Yes", ConfirmYes); NoButton = StageUITheme.Button("Tutorial No", card, "No", ConfirmNo);
        StageUITheme.Stretch((RectTransform)YesButton.transform, new Vector2(.10f, .14f), new Vector2(.46f, .46f));
        StageUITheme.Stretch((RectTransform)NoButton.transform, new Vector2(.54f, .14f), new Vector2(.90f, .46f));
        confirmation = card.gameObject; confirmation.SetActive(false);
    }
    static void Border(RectTransform rect)
    {
        for (int i = 0; i < 4; i++)
        {
            var edge = StageUITheme.Rect("Edge " + i, rect); var image = edge.gameObject.AddComponent<Image>(); image.color = new Color(1, .86f, .4f); image.raycastTarget = false;
            if (i == 0) { edge.anchorMin = new Vector2(0, 1); edge.anchorMax = Vector2.one; edge.sizeDelta = new Vector2(0, 5); }
            if (i == 1) { edge.anchorMin = Vector2.zero; edge.anchorMax = new Vector2(1, 0); edge.sizeDelta = new Vector2(0, 5); }
            if (i == 2) { edge.anchorMin = Vector2.zero; edge.anchorMax = new Vector2(0, 1); edge.sizeDelta = new Vector2(5, 0); }
            if (i == 3) { edge.anchorMin = new Vector2(1, 0); edge.anchorMax = Vector2.one; edge.sizeDelta = new Vector2(5, 0); }
            edge.anchoredPosition = Vector2.zero;
        }
    }
    void ShowStep(ProofsTutorialStep step, string message, RectTransform target = null, bool explanation = false, Transform subject = null)
    {
        CurrentStep = step; enteredAt = Time.unscaledTime; instruction.text = message;
        next.gameObject.SetActive(explanation); next.interactable = false; worldTarget = subject;
        highlight.gameObject.SetActive(target != null); arrow.gameObject.SetActive(target != null);
        if (target != null)
        {
            highlight.SetParent(target, false); StageUITheme.Stretch(highlight, Vector2.zero, Vector2.one);
            highlight.offsetMin = -Vector2.one * 6; highlight.offsetMax = Vector2.one * 6; highlight.SetAsLastSibling();
        }
        worldRect.gameObject.SetActive(subject != null);
        instructions.gameObject.SetActive(true); instructions.SetAsLastSibling(); ApplyPracticeUI(); LayoutInstructions();
    }
    void ApplyPracticeUI()
    {
        joystick.gameObject.SetActive(CurrentStep == ProofsTutorialStep.Movement);
        jump.gameObject.SetActive(CurrentStep == ProofsTutorialStep.Jump);
        hand.gameObject.SetActive(CurrentStep == ProofsTutorialStep.Interaction || CurrentStep == ProofsTutorialStep.Book || CurrentStep == ProofsTutorialStep.ConfirmBook || WantsInteraction);
        bool board = CurrentStep == ProofsTutorialStep.Board || CurrentStep == ProofsTutorialStep.BoardBack;
        ui.BackButton.gameObject.SetActive(board); ui.BackButton.interactable = CurrentStep == ProofsTutorialStep.BoardBack;
        ui.ViewButton.gameObject.SetActive(WantsViewBoard); ui.InteractionButton.gameObject.SetActive(WantsInteraction);
        if (WantsViewBoard) ProofsStageUI.Bottom((RectTransform)ui.ViewButton.transform, new Vector2(0, 55), new Vector2(280, 88));
        ui.Hud.gameObject.SetActive(CurrentStep == ProofsTutorialStep.Timer || CurrentStep == ProofsTutorialStep.Progress);
        stage.PromptText.gameObject.SetActive(false); help.HelpButton.gameObject.SetActive(false);
        if (settings != null) settings.gameObject.SetActive(false);
        string action = CurrentStep == ProofsTutorialStep.Book ? BookStatuePresentation.HandPromptText : CurrentStep == ProofsTutorialStep.ReadScroll ? "Read Practice Scroll" :
            CurrentStep == ProofsTutorialStep.TakeBack ? "Take Back Scroll" : CurrentStep == ProofsTutorialStep.ConfirmBook ? "Confirm at Book" : "Place Scroll";
        ui.InteractionButton.GetComponentInChildren<TMP_Text>().text = action;
        if (CurrentStep == ProofsTutorialStep.Reader)
        {
            stage.CollectButton.interactable = Readable;
            ui.LayoutReader(true);
        }
        if (CurrentStep == ProofsTutorialStep.TakeBackPicker)
        {
            StageUITheme.Stretch((RectTransform)stage.TakeBackPanel.transform, new Vector2(.43f, .13f), new Vector2(.97f, .84f));
            StageUITheme.Stretch((RectTransform)stage.TakeBackButtons[0].transform, new Vector2(.08f, .34f), new Vector2(.92f, .65f));
        }
        foreach (var button in stage.TakeBackButtons) if (button != null) button.interactable = Readable;
    }
    void LateUpdate()
    {
        if (!IsActive || CurrentStep == ProofsTutorialStep.Confirm) return;
        ApplyPracticeUI(); next.interactable = Readable && !ui.BoardView.Transitioning;
        if (worldTarget != null) ProjectBounds(RenderBounds(worldTarget), worldRect);
        if (practiceObject != null && practiceLabel != null && Camera.main != null)
        {
            Vector3 screen = Camera.main.WorldToScreenPoint(practiceObject.GetComponent<Renderer>().bounds.center + Vector3.up * .98f);
            practiceLabel.gameObject.SetActive(screen.z > 0);
            if (screen.z > 0 && RectTransformUtility.ScreenPointToLocalPointInRectangle(safe, screen, null, out Vector2 point)) practiceLabel.anchoredPosition = point;
        }
        LayoutInstructions(); if (skip != null) skip.transform.SetAsLastSibling();
    }
    void LayoutInstructions()
    {
        if (instructions == null || !instructions.gameObject.activeSelf) return;
        var blockers = new List<RectTransform>(ControlTutorialLayout.FindBlockers());
        blockers.AddRange(new[] { ui.Hud, (RectTransform)ui.ViewButton.transform, (RectTransform)ui.InteractionButton.transform, (RectTransform)ui.BackButton.transform,
            (RectTransform)stage.ReaderPanel.transform, (RectTransform)stage.TakeBackPanel.transform });
        if (practiceLabel != null) blockers.Add(practiceLabel);
        if (worldRect.gameObject.activeInHierarchy) blockers.Add(worldRect);
        var target = highlight.gameObject.activeSelf ? highlight.parent as RectTransform : worldRect.gameObject.activeSelf ? worldRect : null;
        ControlTutorialLayout.Place(instructions, instruction, (RectTransform)next.transform, safe, target, false, blockers.ToArray(), arrow.gameObject.activeSelf ? arrow : null);
        ((RectTransform)next.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 88);
    }
    static Bounds RenderBounds(Transform target, bool includeText = false)
    {
        var bounds = new Bounds(target.position, Vector3.one); bool found = false;
        foreach (var r in target.GetComponentsInChildren<Renderer>(true))
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy || r is LineRenderer || r is SkinnedMeshRenderer || r.GetComponent<TMP_SubMesh>() != null) continue;
            var text = r.GetComponent<TMP_Text>();
            if (text != null && !includeText) continue;
            Bounds local;
            if (text != null) { text.ForceMeshUpdate(); local = text.textBounds; }
            else
            {
                var mesh = r.GetComponent<MeshFilter>();
                if (mesh == null || mesh.sharedMesh == null) continue;
                local = mesh.sharedMesh.bounds;
            }
            // A skinned turning page can have huge transient renderer bounds.
            // Frame the stable statue/tablet meshes and current label glyphs instead.
            for (int i = 0; i < 8; i++)
            {
                var point = r.transform.TransformPoint(local.center + Vector3.Scale(local.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; } else bounds.Encapsulate(point);
            }
        }
        return bounds;
    }
    void ProjectBounds(Bounds bounds, RectTransform rect)
    {
        if (Camera.main == null) return;
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = -min; bool found = false;
        for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
        {
            var screen = Camera.main.WorldToScreenPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z)));
            if (screen.z <= 0) continue;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(safe, screen, null, out Vector2 point);
            min = Vector2.Min(min, point); max = Vector2.Max(max, point); found = true;
        }
        rect.gameObject.SetActive(found); if (found) { rect.anchoredPosition = (min + max) * .5f; rect.sizeDelta = max - min + Vector2.one * 16; }
    }
    void StartCamera()
    {
        if (guiding || Camera.main == null) return;
        guiding = true; guidedCamera = Camera.main; savedCameraPosition = guidedCamera.transform.position;
        savedCameraRotation = guidedCamera.transform.rotation; savedFov = guidedCamera.fieldOfView;
        guidedBehaviours.Clear(); avatarRenderers.Clear();
        foreach (var b in FindObjectsByType<Behaviour>(FindObjectsSortMode.None))
            if (b.enabled && (b == guidedCamera.GetComponent<CinemachineBrain>() || b is CameraTargetController && b.GetComponentInParent<Player>() == player))
            { guidedBehaviours.Add(b); b.enabled = false; }
        foreach (var r in player.GetComponentsInChildren<Renderer>(true)) { avatarRenderers[r] = r.enabled; r.enabled = false; }
        goalPosition = guidedCamera.transform.position; goalRotation = guidedCamera.transform.rotation;
    }
    void Focus(Transform target, Vector3 forward)
    {
        if (!guiding || target == null) return;
        forward.y = 0; forward.Normalize(); if (forward.sqrMagnitude < .1f) forward = Vector3.forward;
        var bounds = RenderBounds(target, true); var right = Vector3.Cross(Vector3.up, forward);
        float tan = Mathf.Tan((guidedCamera.aspect < 1f ? 60f : 42f) * Mathf.Deg2Rad * .5f);
        float horizontal = Vector3.Dot(bounds.extents, new Vector3(Mathf.Abs(right.x), 0, Mathf.Abs(right.z)));
        float depth = Vector3.Dot(bounds.extents, new Vector3(Mathf.Abs(forward.x), 0, Mathf.Abs(forward.z)));
        float subjectWidth = guidedCamera.aspect < 1f ? .55f : .43f;
        float distance = Mathf.Max(bounds.extents.y / .60f, horizontal / guidedCamera.aspect / subjectWidth) / tan + depth;
        distance = Mathf.Max(distance, 3);
        goalPosition = bounds.center - forward * distance - right * (distance * tan * guidedCamera.aspect * .37f);
        goalRotation = Quaternion.LookRotation(forward, Vector3.up);
    }
    void AnimateCamera()
    {
        if (!guiding || guidedCamera == null || ui.BoardView.Viewing) return;
        float amount = 1 - Mathf.Exp(-Time.unscaledDeltaTime * 7);
        guidedCamera.transform.position = Vector3.Lerp(guidedCamera.transform.position, goalPosition, amount);
        guidedCamera.transform.rotation = Quaternion.Slerp(guidedCamera.transform.rotation, goalRotation, amount);
        guidedCamera.fieldOfView = Mathf.Lerp(guidedCamera.fieldOfView, guidedCamera.aspect < 1f ? 60f : 42f, amount);
    }
    void RestoreCamera()
    {
        if (!guiding) return;
        if (guidedCamera != null) { guidedCamera.transform.SetPositionAndRotation(savedCameraPosition, savedCameraRotation); guidedCamera.fieldOfView = savedFov; }
        foreach (var pair in avatarRenderers) if (pair.Key != null) pair.Key.enabled = pair.Value;
        foreach (var b in guidedBehaviours) if (b != null) { b.enabled = true; if (b is CinemachineBrain brain) brain.ResetState(); }
        avatarRenderers.Clear(); guidedBehaviours.Clear(); guidedCamera = null; guiding = false;
    }
    void CreatePracticeObject()
    {
        DestroyPracticeObject();
        Vector3 forward = Camera.main != null ? Camera.main.transform.forward : player.transform.forward; forward.y = 0; forward.Normalize();
        if (forward.sqrMagnitude < .1f) forward = player.transform.forward;
        player.transform.rotation = Quaternion.LookRotation(forward);
        practiceObject = GameObject.CreatePrimitive(PrimitiveType.Cube); practiceObject.name = "Proofs Practice Object"; practiceObject.layer = 2;
        practiceObject.transform.position = player.transform.position + forward * 1.8f - Vector3.Cross(Vector3.up, forward) * 2.5f + Vector3.up * 1.9f;
        practiceObject.transform.localScale = Vector3.one * 1.35f; practiceObject.GetComponent<Collider>().isTrigger = true;
        practiceMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")); practiceMaterial.SetColor("_BaseColor", new Color(1, .75f, .18f));
        practiceObject.GetComponent<Renderer>().sharedMaterial = practiceMaterial;
        practiceLabel = StageUITheme.Rect("Proofs Practice Label", safe);
        practiceLabel.anchorMin = practiceLabel.anchorMax = practiceLabel.pivot = new Vector2(.5f, .5f); practiceLabel.sizeDelta = new Vector2(290, 80);
        var image = practiceLabel.gameObject.AddComponent<Image>(); image.color = new Color(.03f, .09f, .14f, .95f); image.raycastTarget = false;
        var text = StageUITheme.Text("Practice Label", practiceLabel, "Interact with me", 34, true); StageUITheme.Stretch(text.rectTransform, Vector2.zero, Vector2.one);
    }
    void DestroyPracticeObject()
    {
        if (practiceObject != null) { practiceObject.SetActive(false); Destroy(practiceObject); } practiceObject = null;
        if (practiceLabel != null) { practiceLabel.gameObject.SetActive(false); Destroy(practiceLabel.gameObject); } practiceLabel = null;
        if (practiceMaterial != null) Destroy(practiceMaterial); practiceMaterial = null;
    }
    void CreatePracticeScroll()
    {
        practiceRoot = new GameObject("Proofs Tutorial Practice Content");
        practiceStand = Instantiate(stage.ScrollTargets[0], practiceRoot.transform); practiceStand.name = "PRACTICE Scroll Stand";
        practiceStand.transform.SetPositionAndRotation(stage.ScrollTargets[0].transform.position, stage.ScrollTargets[0].transform.rotation);
        practiceStand.gameObject.SetActive(true); practiceStand.SetContent("PRACTICE SCROLL", "Three equal ribbons measure 12 cm..."); practiceStand.SetTabletVisible(true);
        practiceScroll = Instantiate(stage.PracticeScrollModel, practiceRoot.transform); practiceScroll.name = "PRACTICE Scroll";
        practiceScroll.transform.position = practiceStand.transform.position + Vector3.up * 1.3f; practiceScroll.transform.localScale = Vector3.one * 2.2f; practiceScroll.SetActive(true);
        foreach (var collider in practiceRoot.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
    }
    Vector3 ScrollForward()
    {
        var text = practiceStand.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t => t.name == "Tablet Heading");
        return text != null ? text.transform.forward : Vector3.left;
    }
}
