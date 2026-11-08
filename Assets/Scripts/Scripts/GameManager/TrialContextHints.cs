using System;
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
    RectTransform safe, panel, badge;
    TMP_Text label, icon;
    Image surface, borderPulse;
    AudioSource notification;
    AudioClip notificationClip;
    RectTransform settingsButton, helpButton;
    readonly Vector3[] corners = new Vector3[4];
    Vector2 settled;
    float offscreenX, slide, pulse = -1, measuredWidth = -1, measuredFont = -1;
    string actionKey, renderedKey, measuredText;
    int actionEpisode, renderedEpisode = -1, soundedEpisode = -1;
#if UNITY_EDITOR
    public int NotificationPlayCount { get; private set; }
    public float SlideProgress => slide;
#endif
    StageHelpPage[] helpPages = Array.Empty<StageHelpPage>();
    CinemachineBrain[] brains = Array.Empty<CinemachineBrain>();
    PrelimTutorialCameraRig[] rigs = Array.Empty<PrelimTutorialCameraRig>();
    float nextDiscovery;
    string displayed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Instance = null; SceneManager.sceneLoaded -= SceneLoaded; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install() { SceneManager.sceneLoaded -= SceneLoaded; SceneManager.sceneLoaded += SceneLoaded; }
    static void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Instance != null) { Instance.State.Clear(); Instance.ResetVisual(); Instance.source = null; Instance.sourceComponent = null; Instance.settingsButton = Instance.helpButton = null; Instance.nextDiscovery = 0; }
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
        surface = panel.gameObject.AddComponent<Image>(); StageUITheme.CompactSurface(surface); surface.raycastTarget = false;
        var pulseRect = StageUITheme.Rect("Notice Border Pulse", panel);
        StageUITheme.Stretch(pulseRect, Vector2.zero, Vector2.one);
        borderPulse = pulseRect.gameObject.AddComponent<Image>();
        borderPulse.sprite = surface.sprite; borderPulse.type = Image.Type.Sliced;
        borderPulse.pixelsPerUnitMultiplier = surface.pixelsPerUnitMultiplier;
        borderPulse.fillCenter = false; borderPulse.color = Color.clear; borderPulse.raycastTarget = false;
        badge = StageUITheme.Rect("Notice Badge", panel);
        var badgeImage = badge.gameObject.AddComponent<Image>();
        badgeImage.sprite = HandThoughtBubble.Circle();
        badgeImage.color = new Color(.09f, .2f, .22f); badgeImage.raycastTarget = false;
        badge.anchorMin = badge.anchorMax = badge.pivot = new Vector2(.5f, .5f);
        badge.sizeDelta = new Vector2(43, 43);
        icon = StageUITheme.Text("Notice Icon", badge, "!", 34, true);
        icon.fontStyle = FontStyles.Bold; icon.color = StageUITheme.Gold;
        StageUITheme.Stretch(icon.rectTransform, Vector2.zero, Vector2.one);
        label = StageUITheme.Text("Action", panel, "", 32, true); label.fontStyle = FontStyles.Bold;
        label.color = StageUITheme.Ink; label.alignment = TextAlignmentOptions.MidlineLeft;
        label.enableAutoSizing = false; label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Overflow;
        label.outlineColor = new Color(0, 0, 0, .6f); label.outlineWidth = .1f;
        StageUITheme.Stretch(label.rectTransform, Vector2.zero, Vector2.one);
        notificationClip = Resources.Load<AudioClip>("Audio/Notification");
        notification = gameObject.AddComponent<AudioSource>();
        notification.playOnAwake = false; notification.loop = false;
        notification.spatialBlend = 0; notification.volume = .8f;
        AudioVolumeSettings.Route(notification, GameAudioChannel.SoundFX);
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
        DiscoverDock();
    }
    void DiscoverDock()
    {
        // Reuse the same Settings reference that positions Help. Keep it while
        // controls are temporarily hidden by a board view or answer mode.
        StageHelpDock fallback = null;
        foreach (var dock in FindObjectsByType<StageHelpDock>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (dock.gameObject.scene != SceneManager.GetActiveScene() || dock.Settings == null) continue;
            if (fallback == null) fallback = dock;
            if (dock.gameObject.activeInHierarchy) { fallback = dock; break; }
        }
        if (fallback != null) { settingsButton = fallback.Settings; helpButton = (RectTransform)fallback.transform; }
    }
    void LateUpdate() => Evaluate(Time.unscaledDeltaTime);
    public bool ShowCurrentForAdmin()
    {
        if (!AdminAuthorization.IsAuthorized) return false;
        Discover(); Evaluate(0, false);
        if (Suspended || sourceComponent == null) return false;
        var action = source.RequiredHintAction();
        if (!action.Available || action.Paused) return false;
        State.ShowNow(action); Evaluate(0, false);
        return true;
    }
#if UNITY_EDITOR
    // The offline verification harness can test 30-second waits without changing
    // Time.timeScale, the trial countdown, or the release build's delay.
    public void AdvanceForValidation(float seconds) { Discover(); Evaluate(seconds, false); }
#endif
    void Evaluate(float elapsed, bool render = true)
    {
        if (!IsTrial(SceneManager.GetActiveScene().name)) { State.Clear(); ResetVisual(); Suspended = true; return; }
        if (Time.unscaledTime >= nextDiscovery) Discover();
        if (sourceComponent == null || !sourceComponent.isActiveAndEnabled)
        { State.Clear(); ResetVisual(); Suspended = true; return; }
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
        if (!action.Available) { ResetVisual(); return; }
        // Modals hide immediately and freeze the presentation, not its identity.
        // Returning to the same unfinished action must not play another sound.
        if (Suspended) { group.alpha = 0; StopPulse(); notification.Stop(); return; }
        // Settings/Help dock in LateUpdate. Event handlers update logical state
        // only; measuring during Update can sample a temporary portrait layout.
        if (render) RenderNotification(State.Key, State.Message, State.Visible, Time.unscaledDeltaTime);
    }
    static bool AllowsAnswerInput(MonoBehaviour owner)
    {
        if (owner is LogicLegends.Trees.TreeStageManager tree) return tree.BoardOpen && !tree.LessonOpen && (tree.Introductions == null || !tree.Introductions.IsOpen);
        if (owner is LogicLegends.Graph.GraphStageManager graph) return graph.QuestionView != null && graph.QuestionView.IsOpen && !graph.HelpOpen && !graph.TutorialActive;
        if (owner is LogicLegends.Inference.InferenceChallenge inference) return inference.IsBoardOpen;
        if (owner is DirectIndirectProofStageController direct) return direct.OrderingBoard != null && direct.OrderingBoard.IsOpen && !direct.GuidanceBlocksGameplay;
        return false;
    }
    void RenderNotification(string key, string message, bool visible, float delta)
    {
        if (key != actionKey) { actionKey = key; actionEpisode++; }
        bool same = visible && renderedKey == key && renderedEpisode == actionEpisode;
        if (renderedKey == null && visible)
        {
            renderedKey = key; renderedEpisode = actionEpisode;
            displayed = message; label.text = message; Layout();
            StopPulse();
            if (soundedEpisode != renderedEpisode)
            {
                soundedEpisode = renderedEpisode;
                if (notificationClip != null)
                {
                    notification.Stop();
                    notification.PlayOneShot(notificationClip);
#if UNITY_EDITOR
                    NotificationPlayCount++;
#endif
                }
            }
            same = true;
        }
        if (same && displayed != message) { displayed = message; label.text = message; Layout(); }
        if (renderedKey == null) { group.alpha = 0; return; }
        Layout();
        if (!same) StopPulse();
        slide = Mathf.MoveTowards(slide, same ? 1 : 0, Mathf.Max(0, delta) / (same ? .36f : .28f));
        // One reversible clock: a new request can interrupt an entrance or exit
        // without a second coroutine, stale callback, jump, or duplicate panel.
        float eased = 1 - Mathf.Pow(1 - slide, 3);
        panel.anchoredPosition = new Vector2(Mathf.Lerp(offscreenX, settled.x, eased), settled.y);
        group.alpha = Mathf.Min(1, slide * 4);
        if (slide >= 1 && same)
        {
            if (pulse < 0) pulse = 0;
            pulse = Mathf.Repeat(pulse + Mathf.Max(0, delta), 2.8f);
            // Two broad, soft beats followed by a rest. Change only tint/glow;
            // text, panel geometry and icon size remain completely steady.
            float beat = HeartbeatLobe(pulse, 0, .72f) + .65f * HeartbeatLobe(pulse, .84f, .56f);
            borderPulse.color = new Color(1, .86f, .52f, .04f + .36f * beat);
            icon.color = Color.Lerp(StageUITheme.Gold, new Color(1, .94f, .72f), beat * .75f);
        }
        else StopPulse();
        if (slide <= 0 && !same) { renderedKey = null; group.alpha = 0; pulse = -1; }
    }
    static float HeartbeatLobe(float time, float start, float duration)
    {
        float wave = Mathf.Sin(Mathf.Clamp01((time - start) / duration) * Mathf.PI);
        return wave * wave;
    }
    void StopPulse()
    {
        pulse = -1;
        borderPulse.color = Color.clear;
        icon.color = StageUITheme.Gold;
    }
    void ResetVisual()
    {
        group.alpha = 0; slide = 0; pulse = -1;
        renderedKey = actionKey = null; renderedEpisode = soundedEpisode = -1;
        StopPulse();
        if (notification != null) notification.Stop();
    }
    bool BoundsInSafe(RectTransform rect, out Rect bounds)
    {
        bounds = default;
        if (rect == null) return false;
        var host = rect.GetComponentInParent<Canvas>();
        if (host == null || host.renderMode == RenderMode.WorldSpace) return false;
        rect.GetWorldCorners(corners);
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = -min;
        foreach (var corner in corners)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(host.renderMode == RenderMode.ScreenSpaceOverlay ? null : host.worldCamera, corner);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(safe, screen, null, out var point);
            min = Vector2.Min(min, point); max = Vector2.Max(max, point);
        }
        bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y); return true;
    }
    void Layout()
    {
        if (safe.rect.width <= 1) Canvas.ForceUpdateCanvases();
        if (settingsButton == null || helpButton == null) DiscoverDock();
        float left = safe.rect.xMin + 20, right = left + Mathf.Min(400, safe.rect.width - 40), top = safe.rect.yMax - 130;
        if (BoundsInSafe(settingsButton, out var settingsBounds) && BoundsInSafe(helpButton, out var helpBounds))
        {
            left = settingsBounds.xMin; right = helpBounds.xMax;
            top = Mathf.Min(settingsBounds.yMin, helpBounds.yMin) - 12;
        }
        left = Mathf.Clamp(left, safe.rect.xMin, safe.rect.xMax - 120);
        right = Mathf.Clamp(right, left + 120, safe.rect.xMax);
        float width = right - left;
        // Include the actual sprite's nine-slice border before adding breathing
        // room. Fixed offsets alone allowed glyphs to touch the wood frame.
        Vector4 edge = surface.sprite != null ? surface.sprite.border / surface.pixelsPerUnitMultiplier : Vector4.zero;
        float l = Mathf.Max(22, edge.x + 14), r = Mathf.Max(22, edge.z + 14);
        float bottom = Mathf.Max(20, edge.y + 12), upper = Mathf.Max(20, edge.w + 12);
        float textLeft = l + badge.sizeDelta.x + 14;
        float font = Mathf.Max(32, 15 / Mathf.Max(.01f, canvas.scaleFactor));
        if (!Mathf.Approximately(measuredWidth, width) || !Mathf.Approximately(measuredFont, font) || measuredText != label.text)
        {
            label.fontSize = font;
            float height = Mathf.Max(badge.sizeDelta.y, label.GetPreferredValues(label.text, Mathf.Max(40, width - textLeft - r), 0).y) + upper + bottom;
            panel.sizeDelta = new Vector2(width, height);
            measuredWidth = width; measuredFont = font; measuredText = label.text;
        }
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0, 1);
        panel.localScale = Vector3.one;
        label.rectTransform.offsetMin = new Vector2(textLeft, bottom);
        label.rectTransform.offsetMax = new Vector2(-r, -upper);
        badge.anchorMin = badge.anchorMax = new Vector2(0, .5f);
        badge.anchoredPosition = new Vector2(l + badge.sizeDelta.x * .5f, 0);
        settled = new Vector2(left - safe.rect.xMin, top - safe.rect.yMax);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(safe, Vector2.zero, null, out var screenLeft);
        offscreenX = screenLeft.x - safe.rect.xMin - width - 24;
    }
    void OnDestroy() { if (Instance == this) Instance = null; }
}
