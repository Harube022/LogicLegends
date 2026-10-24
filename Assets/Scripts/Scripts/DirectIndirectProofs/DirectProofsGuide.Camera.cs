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
    Vector3 closeDirection = Vector3.forward;
    readonly Dictionary<Renderer, bool> hiddenRenderers = new Dictionary<Renderer, bool>();
    Renderer cutawayRoom;
    Material[] roomMaterials, cutawayMaterials;
    public bool IsIntroduction => mode == Mode.Introduction;
    static readonly Regex RoofName = new Regex(@"(^|[ _\-.])(roof|ceiling)([ _\-.]|$)", RegexOptions.IgnoreCase);

    void StartCamera()
    {
        if (guiding || Camera.main == null) return;
        if (returningRig != null) { returningRig.FinishReturn(); returningRig = null; }
        guidedCamera = Camera.main;
        cameraRig = PrelimTutorialCameraRig.Create("Direct Proofs Tutorial Camera");
        goalPosition = cameraRig.transform.position; goalRotation = cameraRig.transform.rotation;
        guiding = true;
        foreach (var renderer in player.GetComponentsInChildren<Renderer>(true)) HideRenderer(renderer);
        // Only visual ceiling pieces are hidden. Never disable room/floor collision.
        foreach (var renderer in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            for (var ancestor = renderer.transform; ancestor != null; ancestor = ancestor.parent)
                if (RoofName.IsMatch(ancestor.name) || ancestor.name.ToLowerInvariant().Contains("ceiling") || ancestor.name.ToLowerInvariant().Contains("overhead rib"))
                { HideRenderer(renderer); break; }
        ApplyChamberCutaway();
    }
    void ApplyChamberCutaway()
    {
        // This chamber imports as one mesh. A temporary height clip removes its
        // visual ceiling without changing its mesh, colliders, or original materials.
        cutawayRoom = GameObject.Find("Stage")?.GetComponent<Renderer>();
        var shader = Resources.Load<Shader>("DirectProofs/ChamberCutaway");
        if (cutawayRoom == null || shader == null) return;
        roomMaterials = cutawayRoom.sharedMaterials; cutawayMaterials = new Material[roomMaterials.Length];
        for (int i = 0; i < roomMaterials.Length; i++)
        {
            var original = roomMaterials[i]; var material = new Material(shader) { name = "Tutorial-only chamber cutaway" };
            if (original != null)
            {
                string map = original.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
                if (original.HasProperty(map))
                { material.SetTexture("_BaseMap", original.GetTexture(map)); material.SetTextureScale("_BaseMap", original.GetTextureScale(map)); material.SetTextureOffset("_BaseMap", original.GetTextureOffset(map)); }
                if (original.HasProperty("_BaseColor")) material.SetColor("_BaseColor", original.GetColor("_BaseColor"));
                else if (original.HasProperty("_Color")) material.SetColor("_BaseColor", original.GetColor("_Color"));
            }
            material.SetFloat("_CutHeight", cutawayRoom.bounds.min.y + cutawayRoom.bounds.size.y * .42f);
            cutawayMaterials[i] = material;
        }
        cutawayRoom.sharedMaterials = cutawayMaterials;
    }
    void HideRenderer(Renderer renderer)
    { if (!hiddenRenderers.ContainsKey(renderer)) hiddenRenderers[renderer] = renderer.enabled; renderer.enabled = false; }
    Bounds ObjectBounds(Transform subject)
    {
        var bounds = new Bounds(subject.position + Vector3.up, Vector3.one * 2); bool first = true;
        foreach (var renderer in subject.GetComponentsInChildren<Renderer>(true))
            if (renderer.enabled && renderer.gameObject.activeInHierarchy && !(renderer is TMPro.TMP_SubMesh))
            { if (first) { bounds = renderer.bounds; first = false; } else bounds.Encapsulate(renderer.bounds); }
        return bounds;
    }
    void Focus(Transform subject, bool overhead)
    {
        StartCamera(); if (!guiding) return;
        focusBounds = ObjectBounds(subject); worldSubject = subject;
        closeDirection = Vector3.forward;
        SetCameraGoal(overhead);
    }
    void FocusOverview()
    {
        StartCamera(); if (!guiding) return;
        focusBounds = ObjectBounds(stage.Book); focusBounds.Encapsulate(ObjectBounds(stage.InformationBoard));
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
            goalPosition = focusBounds.center + Vector3.up * distance + Vector3.back * distance * tangent * .4f;
            goalRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
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
        if (cutawayRoom != null && roomMaterials != null) cutawayRoom.sharedMaterials = roomMaterials;
        if (cutawayMaterials != null) foreach (var material in cutawayMaterials) if (material != null) Destroy(material);
        cutawayRoom = null; roomMaterials = cutawayMaterials = null; hiddenRenderers.Clear();
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
            string task = stage.ChallengeNumber == 1 ? "Read the complete argument on the Information Board. At the Direct or Indirect altar, choose the family of reasoning used. Contrapositive and contradiction are both indirect. No step ordering is required in this challenge." :
                stage.ChallengeNumber == 2 ? "Use the Direct Proof altar. Drag each statement onto another row to swap their positions: start with the givens and definitions, derive consequences in order, and finish with the conclusion. Every statement must appear exactly once." :
                stage.ChallengeNumber == 3 ? "Use the Indirect Proof altar to order a contrapositive. For P → Q, begin with ¬Q and establish ¬P. This proves the original implication. Follow dependencies between the supplied statements; simply negating P → Q is not a proof." :
                stage.ChallengeNumber == 4 ? "Use the neutral trial altar. A contradiction proof assumes the opposite of the claim, reasons from that assumption, identifies an impossibility, and rejects the assumption. Arrange all five rows into that justified chain." :
                "Use the neutral trial altar. Select Direct, Contrapositive, or Contradiction on the board and arrange the supplied statements into a complete proof. The chosen method and the full step order must both fit the argument.";
            string example = stage.ChallengeNumber == 1 ? "Separate practice example: an envelope is wax-sealed; every wax-sealed envelope is closed; therefore it is closed. This derives the result directly from its premises." :
                stage.ChallengeNumber == 2 ? "Separate practice example: four equal cords total 28 cm. Define their common length x, write 4x = 28, divide by 4, and conclude x = 7 cm. A calculation must follow the equation that supports it." :
                stage.ChallengeNumber == 3 ? "Separate practice example: to justify ‘if a shape is a square, it has four sides,’ one may establish ‘if it does not have four sides, it is not a square.’ The direction and both negations matter." :
                stage.ChallengeNumber == 4 ? "Separate practice example: suppose a room is empty yet has a person inside it. Being empty means containing no people, which conflicts with the stated person. Identify the actual conflict before rejecting the assumption." :
                "Separate practice example: proving a ribbon length from given equal lengths and a total suggests a direct equation-based chain. Other arguments may instead negate a conclusion or assume the opposite. Choose the method supported by the supplied reasoning, not by an altar's appearance.";
            return new[] { new Lesson(title, task + "\n\nTwo distinct questions are required. This introduction appears once for this challenge, before its first book activation."), new Lesson("How to reason — practice only", example + "\n\nThese examples are separate from the live question; no answer is placed for you."), new Lesson("Confirm, correct, continue", "Use Back to Room to keep your altar arrangement, then interact with the Book Statue to confirm. Incorrect validated answers subtract " + stage.WrongPenalty.ToString("0.##") + " seconds and preserve the method and order for correction. Correct answers pause time. Start the next question at the book; after both, continue to the next challenge. Finishing or skipping this introduction leaves the timer paused until that book interaction.") };
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
