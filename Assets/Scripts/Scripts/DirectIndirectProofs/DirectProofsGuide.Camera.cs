using System.Collections.Generic;
using System.Text.RegularExpressions;
using Unity.Cinemachine;
using UnityEngine;

public sealed partial class DirectProofsGuide
{
    Camera guidedCamera;
    Vector3 goalPosition;
    Quaternion goalRotation;
    PrelimTutorialCameraRig cameraRig, returningRig;
    Transform worldSubject;
    Bounds focusBounds;
    bool guiding;
    Vector3 closeDirection = Vector3.forward, tourForward = Vector3.back;
    readonly Dictionary<Renderer, bool> hiddenRenderers = new Dictionary<Renderer, bool>();
    public bool IsIntroduction => mode == Mode.Introduction;
    static readonly Regex RoofName = new Regex(@"(^|[ _\-.])(roof|ceiling)([ _\-.]|$)", RegexOptions.IgnoreCase);

    void StartCamera()
    {
        if (guiding || Camera.main == null) return;
        if (returningRig != null) { returningRig.FinishReturn(); returningRig = null; }
        guidedCamera = Camera.main;
        cameraRig = PrelimTutorialCameraRig.Create("Direct Proofs Tutorial Camera");
        var lens = cameraRig.Camera.Lens;
        lens.NearClipPlane = guidedCamera.nearClipPlane;
        lens.FarClipPlane = guidedCamera.farClipPlane;
        cameraRig.Camera.Lens = lens;
        // Keep the entry heading through overview and close-ups; changing the
        // overhead up vector to the opposite heading rolls the view by 180 degrees.
        tourForward = Vector3.ProjectOnPlane(guidedCamera.transform.forward, Vector3.up).normalized;
        if (tourForward.sqrMagnitude < .1f) tourForward = Vector3.back;
        closeDirection = -tourForward;
        goalPosition = cameraRig.transform.position; goalRotation = cameraRig.transform.rotation;
        guiding = true;
        foreach (var renderer in player.GetComponentsInChildren<Renderer>(true)) HideRenderer(renderer);
        // Only visual ceiling pieces are hidden. Never disable room/floor collision.
        foreach (var renderer in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            for (var ancestor = renderer.transform; ancestor != null; ancestor = ancestor.parent)
                if (RoofName.IsMatch(ancestor.name) || ancestor.name.ToLowerInvariant().Contains("ceiling") || ancestor.name.ToLowerInvariant().Contains("overhead rib") || IsSuspendedChamberArt(ancestor))
                { HideRenderer(renderer); break; }
        // Stage is an open chamber mesh: its upper walls and arches are not a
        // ceiling. A height-clip material used to slice them off at 42% height.
    }
    static bool IsSuspendedChamberArt(Transform part)
    {
        // The shared art includes beams, chains and lamps above the proof altars.
        // Hide their renderers during the tour, while retaining lighting and collision.
        if (part.name != "Overhead Stone Rib" && part.name != "Suspension Chain" && part.name != "Hanging Chandelier") return false;
        for (var parent = part.parent; parent != null; parent = parent.parent)
            if (parent.name == "ChamberDecorations") return true;
        return false;
    }
    void HideRenderer(Renderer renderer)
    { if (!hiddenRenderers.ContainsKey(renderer)) hiddenRenderers[renderer] = renderer.enabled; renderer.enabled = false; }
    Bounds ObjectBounds(Transform subject)
    {
        var bounds = new Bounds(subject.position + Vector3.up, Vector3.one * 2); bool first = true;
        // As with the book indicator anchor, frame the stable statue geometry.
        // Imported skinned-page bounds can briefly expand by kilometres mid-flip.
        // Excluding those bounds does not hide or stop the page animation.
        foreach (var renderer in subject.GetComponentsInChildren<Renderer>(true))
            if (renderer.enabled && renderer.gameObject.activeInHierarchy && !(renderer is TMPro.TMP_SubMesh) && !(renderer is SkinnedMeshRenderer))
            { if (first) { bounds = renderer.bounds; first = false; } else bounds.Encapsulate(renderer.bounds); }
        return bounds;
    }
    void Focus(Transform subject, bool overhead)
    {
        StartCamera(); if (!guiding) return;
        focusBounds = ObjectBounds(subject); worldSubject = subject;
        SetCameraGoal(overhead);
    }
    void FocusOverview()
    {
        StartCamera(); if (!guiding) return;
        focusBounds = ObjectBounds(stage.Book); focusBounds.Encapsulate(ObjectBounds(stage.InformationBoardDisplay));
        foreach (var altar in stage.Altars) if (altar.gameObject.activeSelf) focusBounds.Encapsulate(ObjectBounds(altar));
        worldSubject = null; SetCameraGoal(true);
    }
    bool overheadView;
    void SetCameraGoal(bool overhead)
    {
        overheadView = overhead;
        float tangent = Mathf.Tan(27.5f * Mathf.Deg2Rad), aspect = Mathf.Max(.4f, guidedCamera.aspect);
        if (overhead)
        {
            float distance = Mathf.Max(24, focusBounds.extents.x / (tangent * aspect * .85f), focusBounds.extents.z / (tangent * .42f));
            goalPosition = focusBounds.center + Vector3.up * distance - tourForward * distance * tangent * .4f;
            goalRotation = Quaternion.LookRotation(Vector3.down, tourForward);
        }
        else
        {
            float distance = Mathf.Max(7, focusBounds.extents.x / (tangent * aspect * .75f), focusBounds.extents.y / (tangent * .43f)) + focusBounds.extents.z;
            goalPosition = focusBounds.center + closeDirection * distance + Vector3.up;
            goalRotation = Quaternion.LookRotation(focusBounds.center - Vector3.up * distance * tangent * .34f - goalPosition);
        }
    }
    void AnimateCamera()
    {
        if (guiding && guidedCamera != null)
        {
            SetCameraGoal(overheadView);
            cameraRig?.MoveTowards(goalPosition, goalRotation, 55);
        }
        UpdateWorldHighlight();
    }
    void UpdateWorldHighlight()
    {
        if (worldHighlight == null) return;
        bool show = IsBusy && mode != Mode.Help && guidedCamera != null && guiding && !(IsPractice && Step == DirectProofsPracticeStep.Arrange);
        if (!show) { worldHighlight.gameObject.SetActive(false); return; }
        Bounds bounds = worldSubject != null ? ObjectBounds(worldSubject) : focusBounds;
        Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
        for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
        {
            Vector3 point = guidedCamera.WorldToScreenPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z)));
            if (point.z <= 0) { show = false; break; }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(SafeArea, point, null, out Vector2 local);
            min = Vector2.Min(min, local); max = Vector2.Max(max, local);
        }
        var box = Rect.MinMaxRect(min.x - 8, min.y - 8, max.x + 8, max.y + 8);
        show &= SafeArea.rect.Contains(box.min) && SafeArea.rect.Contains(box.max) && !box.Overlaps(RelativeRect(card));
        worldHighlight.gameObject.SetActive(show);
        if (show)
        {
            worldHighlight.anchorMin = worldHighlight.anchorMax = worldHighlight.pivot = new Vector2(.5f, .5f);
            worldHighlight.anchoredPosition = box.center - SafeArea.rect.center; worldHighlight.sizeDelta = box.size;
        }
    }
    void RestoreCamera()
    {
        if (cameraRig != null)
        {
            returningRig = cameraRig; cameraRig = null;
            returningRig.ReturnToGameplay(RestoreCameraVisuals);
        }
        guiding = false; worldSubject = null;
        if (worldHighlight != null) worldHighlight.gameObject.SetActive(false);
    }
    void RestoreCameraVisuals()
    {
        foreach (var entry in hiddenRenderers) if (entry.Key != null) entry.Key.enabled = entry.Value;
        hiddenRenderers.Clear();
    }
    void FinishCameraImmediately()
    {
        if (cameraRig != null) { cameraRig.ReturnToGameplay(RestoreCameraVisuals); cameraRig.FinishReturn(); cameraRig = null; }
        if (returningRig != null) { returningRig.FinishReturn(); returningRig = null; }
        RestoreCameraVisuals();
    }
    Lesson[] IntroductionPages
    {
        get
        {
            string title = stage.ChallengeNumber == 1 ? "Identify a proof method" : stage.ChallengeNumber == 2 ? "Order a direct proof" : stage.ChallengeNumber == 3 ? "Order a contrapositive" : stage.ChallengeNumber == 4 ? "Order a contradiction proof" : "Choose a strategy and build a proof";
            string task = stage.ChallengeNumber == 1 ? "Read the argument on the board.\nChoose Direct or Indirect at its altar.\nContrapositive and contradiction are indirect. No ordering is required here." :
                stage.ChallengeNumber == 2 ? "Open the Direct Proof altar.\nDrag rows onto one another to swap.\nOrder givens → definitions → consequences → conclusion. Use every statement once." :
                stage.ChallengeNumber == 3 ? "Open the Indirect Proof altar.\nFor P → Q, prove ¬Q → ¬P.\nOrder each fact before the steps that use it. Negating the original claim alone is not proof." :
                stage.ChallengeNumber == 4 ? "Open the neutral altar.\nOrder all five rows: assume the opposite → derive consequences → identify a contradiction → conclude the claim." :
                "Open the neutral altar.\nChoose Direct, Contrapositive or Contradiction.\nOrder the full proof to match that method.";
            string example = stage.ChallengeNumber == 1 ? "Separate practice example: an envelope is wax-sealed; every wax-sealed envelope is closed; therefore it is closed. This derives the result directly from its premises." :
                stage.ChallengeNumber == 2 ? "Practice only: four equal cords total 28 cm.\nLet each length be x. Then 4x = 28, so x = 7 cm.\nPut each calculation after its supporting equation." :
                stage.ChallengeNumber == 3 ? "Practice only: square → four sides.\nIts contrapositive is: not four sides → not square.\nReverse the direction and negate both statements." :
                stage.ChallengeNumber == 4 ? "Practice only: an empty room contains a person.\nEmpty means no people. These claims conflict.\nIdentify the contradiction before rejecting the assumption." :
                "Practice only: equal ribbon lengths and a total suggest a direct equation.\nChoose the method that fits the reasoning, not the altar’s appearance.";
            return new[] { new Lesson(title, task + "\n\nTwo distinct questions are required. This introduction appears once for this challenge, before its first book activation."), new Lesson("How to reason — practice only", example + "\n\nThese examples are separate from the live question; no answer is placed for you."), new Lesson("Confirm, correct, continue", "Use Back to Room to keep your altar arrangement, then interact with the Book Statue to confirm. Incorrect validated answers subtract " + stage.WrongPenalty.ToString("0.##") + " seconds. Your method and order stay for correction.\nCorrect answers pause time. Start each new question at the book. Finish both to advance.\nNext or Skip leaves the book waiting.") };
        }
    }
    void BeginIntroduction()
    {
        BeginSession(Mode.Introduction); Time.timeScale = 0; page = 0;
        skip = PrelimTutorialSkipControl.Create(SafeArea, Skip); skip.Reserve(hud);
        ShowIntroductionPage();
    }
    void ShowIntroductionPage()
    {
        var lesson = IntroductionPages[page]; SetCard("Challenge " + stage.ChallengeNumber + " — " + lesson.title + " (" + (page + 1) + "/3)", lesson.body);
        PreviousButton.gameObject.SetActive(false); BeginButton.gameObject.SetActive(false); ActionButton.gameObject.SetActive(false); AlternateButton.gameObject.SetActive(false);
        NextButton.gameObject.SetActive(true); NextButton.interactable = false; SetHighlight(null);
        if (page == 0) FocusOverview();
        else if (page == 1) Focus(stage.Altars[stage.ChallengeNumber <= 2 ? 0 : stage.ChallengeNumber == 3 ? 1 : 2], false);
        else Focus(stage.Book, false);
    }
    void EndIntroduction() { seen[stage.ChallengeNumber - 1] = true; Cancel(); }
}
