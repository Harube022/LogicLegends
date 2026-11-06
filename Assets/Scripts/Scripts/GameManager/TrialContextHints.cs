using System;
using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>One non-interactive, safe-area hint display shared by Trials 1–10.</summary>
[DefaultExecutionOrder(5000)]
public sealed class TrialContextHints : MonoBehaviour
{
    public static TrialContextHints Instance { get; private set; }
    public readonly TrialHintState State = new TrialHintState();
    public RectTransform HintPanel => panel;
    public float Opacity => group != null ? group.alpha : 0;
    public bool Suspended { get; private set; }
    ITrialHintSource source;
    MonoBehaviour sourceComponent;
    Canvas canvas;
    CanvasGroup group;
    RectTransform safe, panel;
    TMP_Text label;
    StageHelpPage[] helpPages = Array.Empty<StageHelpPage>();
    CinemachineBrain[] brains = Array.Empty<CinemachineBrain>();
    PrelimTutorialCameraRig[] rigs = Array.Empty<PrelimTutorialCameraRig>();
    readonly List<RectTransform> blockers = new List<RectTransform>();
    float nextDiscovery, nextLayout;
    string displayed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Instance = null; SceneManager.sceneLoaded -= SceneLoaded; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install() { SceneManager.sceneLoaded -= SceneLoaded; SceneManager.sceneLoaded += SceneLoaded; }
    static void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Instance != null) { Instance.State.Clear(); Instance.source = null; Instance.sourceComponent = null; }
        if (!IsTrial(SceneManager.GetActiveScene().name)) return;
        if (Instance == null) new GameObject("Context-aware Trial Hints").AddComponent<TrialContextHints>();
    }
    static bool IsTrial(string name)
    { for (int i = 1; i <= TrialCatalog.Count; i++) if (TrialCatalog.Get(i).Scene == name) return true; return false; }
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this; DontDestroyOnLoad(gameObject); Build();
    }
    void Build()
    {
        canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 250;
        var scaler = gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        // No GraphicRaycaster: hints can never consume a joystick, hand, or UI tap.
        group = gameObject.AddComponent<CanvasGroup>(); group.alpha = 0; group.blocksRaycasts = group.interactable = false;
        safe = StageUITheme.Rect("Hint Safe Area", transform);
        StageUITheme.Stretch(safe, Vector2.zero, Vector2.one);
        safe.gameObject.AddComponent<Crystal.SafeArea>();
        panel = StageUITheme.Rect("Required Action Hint", safe);
        var surface = panel.gameObject.AddComponent<Image>(); StageUITheme.CompactSurface(surface); surface.raycastTarget = false;
        var icon = StageUITheme.Text("Notice Icon", panel, "!", 34, true);
        icon.fontStyle = FontStyles.Bold; icon.color = StageUITheme.Gold;
        icon.rectTransform.anchorMin = new Vector2(0, 0); icon.rectTransform.anchorMax = new Vector2(0, 1);
        icon.rectTransform.offsetMin = new Vector2(16, 10); icon.rectTransform.offsetMax = new Vector2(62, -10);
        var disc = icon.gameObject.AddComponent<Outline>(); disc.effectColor = new Color(0, 0, 0, .5f);
        // A notice badge, made from the game's existing circular UI sprite.
        var badge = StageUITheme.Rect("Notice Badge", panel);
        var badgeImage = badge.gameObject.AddComponent<Image>();
        badgeImage.sprite = HandThoughtBubble.Circle();
        badgeImage.color = new Color(.09f, .2f, .22f); badgeImage.raycastTarget = false;
        badge.anchorMin = badge.anchorMax = new Vector2(0, .5f); badge.pivot = new Vector2(.5f, .5f);
        badge.sizeDelta = new Vector2(43, 43); badge.anchoredPosition = new Vector2(39, 0); badge.SetSiblingIndex(1);
        icon.transform.SetAsLastSibling();
        label = StageUITheme.Text("Action", panel, "", 30, true); label.fontStyle = FontStyles.Bold;
        label.color = StageUITheme.Ink; label.alignment = TextAlignmentOptions.MidlineLeft;
        label.outlineColor = new Color(0, 0, 0, .6f); label.outlineWidth = .1f;
        StageUITheme.Stretch(label.rectTransform, Vector2.zero, Vector2.one);
        label.rectTransform.offsetMin = new Vector2(76, 16); label.rectTransform.offsetMax = new Vector2(-24, -16);
        foreach (var graphic in GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
    }
    void Discover()
    {
        nextDiscovery = Time.unscaledTime + 1;
        if (sourceComponent == null)
        {
            // Scene controllers implement the interface; no reflection or save data is used.
            foreach (var component in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (component.gameObject.scene != SceneManager.GetActiveScene() || !(component is ITrialHintSource candidate)) continue;
                if (component is QuizManager && StageSelectionState.SelectedStage == 2) continue;
                if (component is DynamicLogicPuzzle puzzle && !puzzle.IsTruthTableHintSource) continue;
                source = candidate; sourceComponent = component; break;
            }
        }
        helpPages = FindObjectsByType<StageHelpPage>(FindObjectsSortMode.None);
        brains = FindObjectsByType<CinemachineBrain>(FindObjectsSortMode.None);
        rigs = FindObjectsByType<PrelimTutorialCameraRig>(FindObjectsSortMode.None);
        blockers.Clear(); blockers.AddRange(ControlTutorialLayout.FindBlockers());
        foreach (var hud in FindObjectsByType<TrialHudStyle>(FindObjectsSortMode.None))
            if (hud.transform is RectTransform rect) blockers.Add(rect);
        foreach (var rect in FindObjectsByType<RectTransform>(FindObjectsSortMode.None))
            if (rect.name == "Objective") blockers.Add(rect);
    }
    void LateUpdate() => Evaluate(Time.unscaledDeltaTime);
    public bool ShowCurrentForAdmin()
    {
        if (!AdminAuthorization.IsAuthorized) return false;
        Discover(); Evaluate(0);
        if (Suspended || sourceComponent == null) return false;
        var action = source.RequiredHintAction();
        if (!action.Available || action.Paused) return false;
        State.ShowNow(action); Evaluate(0);
        return true;
    }
#if UNITY_EDITOR
    // The offline verification harness can test 30-second waits without changing
    // Time.timeScale, the trial countdown, or the release build's delay.
    public void AdvanceForValidation(float seconds) { Discover(); Evaluate(seconds); }
#endif
    void Evaluate(float elapsed)
    {
        if (!IsTrial(SceneManager.GetActiveScene().name)) { State.Clear(); group.alpha = 0; Suspended = true; return; }
        if (Time.unscaledTime >= nextDiscovery) Discover();
        if (sourceComponent == null || !sourceComponent.isActiveAndEnabled)
        { State.Clear(); group.alpha = 0; Suspended = true; return; }
        var action = source.RequiredHintAction();
        bool pause = Time.timeScale <= 0 || Player.LocalInstance == null || ExpandableMinimap.OpenMap != null ||
            TeleportManager.Instance != null && TeleportManager.Instance.IsTransitioning ||
            PrelimTeleportTransition.Instance != null && PrelimTeleportTransition.Instance.IsTransitioning;
        foreach (var page in helpPages) if (page != null && page.isActiveAndEnabled) pause = true;
        foreach (var brain in brains) if (brain != null && brain.isActiveAndEnabled && brain.IsBlending) pause = true;
        foreach (var rig in rigs) if (rig != null && rig.isActiveAndEnabled && rig.IsMoving) pause = true;
        // New Help panels can be created between discovery samples. An input block
        // also pauses hints except in a provider's own interactive answer mode.
        pause |= GameInput.Instance != null && GameInput.Instance.GameplayActionsBlocked && !AllowsAnswerInput(sourceComponent);
        Suspended = pause || action.Paused;
        if (Suspended && action.Available && State.Key != null)
            State.Advance(new TrialHintAction(State.Key, State.Message, true), 0, true);
        else State.Advance(action, elapsed, pause);
        if (!action.Available || pause || action.Paused) { group.alpha = 0; return; }
        if (State.Visible && displayed != State.Message)
        { displayed = State.Message; label.text = displayed; Layout(); }
        if (State.Visible && Time.unscaledTime >= nextLayout) Layout();
        group.alpha = Mathf.MoveTowards(group.alpha, State.Visible ? 1 : 0, Time.unscaledDeltaTime / .3f);
    }
    static bool AllowsAnswerInput(MonoBehaviour owner)
    {
        if (owner is LogicLegends.Trees.TreeStageManager tree) return tree.BoardOpen && !tree.LessonOpen && (tree.Introductions == null || !tree.Introductions.IsOpen);
        if (owner is LogicLegends.Graph.GraphStageManager graph) return graph.QuestionView != null && graph.QuestionView.IsOpen && !graph.HelpOpen && !graph.TutorialActive;
        if (owner is LogicLegends.Inference.InferenceChallenge inference) return inference.IsBoardOpen;
        if (owner is DirectIndirectProofStageController direct) return direct.OrderingBoard != null && direct.OrderingBoard.IsOpen && !direct.GuidanceBlocksGameplay;
        return false;
    }
    void Layout()
    {
        nextLayout = Time.unscaledTime + .5f;
        Canvas.ForceUpdateCanvases();
        float width = Mathf.Min(600, safe.rect.width - 36);
        float height = Mathf.Max(88, label.GetPreferredValues(label.text, width - 100, 0).y + 34);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, 1);
        panel.sizeDelta = new Vector2(width, height);
        // Stay centered; move down only enough to clear HUDs or a degree question.
        float top = 18;
        for (int i = 0; i < 20; i++)
        {
            Rect candidate = new Rect(safe.rect.center.x - width / 2, safe.rect.yMax - top - height, width, height);
            bool hit = false;
            foreach (var blocker in blockers)
            {
                if (blocker == null || !blocker.gameObject.activeInHierarchy || blocker.IsChildOf(transform)) continue;
                var parentCanvas = blocker.GetComponentInParent<Canvas>();
                if (parentCanvas == null || parentCanvas.renderMode == RenderMode.WorldSpace) continue;
                var corners = new Vector3[4]; blocker.GetWorldCorners(corners);
                var min = new Vector2(float.MaxValue, float.MaxValue); var max = -min;
                foreach (var corner in corners)
                {
                    var screen = RectTransformUtility.WorldToScreenPoint(parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera, corner);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(safe, screen, null, out var point);
                    min = Vector2.Min(min, point); max = Vector2.Max(max, point);
                }
                Rect bounds = Rect.MinMaxRect(min.x - 10, min.y - 10, max.x + 10, max.y + 10);
                // Full-screen UI roots are containers, not visible obstacles.
                if (bounds.width > safe.rect.width * .95f && bounds.height > safe.rect.height * .7f) continue;
                if (candidate.Overlaps(bounds)) { top = Mathf.Max(top + 10, safe.rect.yMax - bounds.yMin + 12); hit = true; break; }
            }
            if (!hit) break;
        }
        panel.anchoredPosition = new Vector2(0, -Mathf.Min(top, Mathf.Max(18, safe.rect.height * .48f - height)));
    }
    void OnDestroy() { if (Instance == this) Instance = null; }
}
