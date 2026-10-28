using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Compact PRELIM clock/progress HUD; questions stay on the world board.</summary>
[DisallowMultipleComponent]
public sealed class SetssssHudView : MonoBehaviour
{
    private const float CompactWidth = 500f, MinimumHeight = 136f;
    private const float EdgeMargin = 24f, Padding = 16f, HeaderHeight = 136f;
    public RectTransform Hud { get; private set; }
    public RectTransform NoticeDock { get; private set; }
    public RectTransform DeductionArea { get; private set; }
    public TMP_Text Timer { get; private set; }
    public TMP_Text Challenge { get; private set; }
    public TMP_Text Title { get; private set; }
    public PrelimChallengeCompletionNotice Notice { get; private set; }
    private SetssssChallengeOne stage;
    private SetssssChallengePresentation presentation;
    private RectTransform safeArea;
    private RectTransform jump, hand, settings, help;
    private RectTransform[] minimaps;
    private int lastChallenge = -1, lastQuestion = -1;
    private Vector3 previousScale;

    public void Initialize(SetssssChallengeOne owner, TMP_Text clock, TMP_Text problem, TMP_Text feedback)
    {
        if (Hud != null || clock == null) return;
        stage = owner;
        Canvas canvas = clock.GetComponentInParent<Canvas>();
        safeArea = canvas.transform.Find("SafeArea") as RectTransform;
        if (safeArea == null) safeArea = (RectTransform)canvas.transform;
        Hud = StageUITheme.Rect("CurrentStageHud", safeArea);
        Hud.anchorMin = Hud.anchorMax = Hud.pivot = Vector2.one;
        StageUITheme.CompactSurface(Hud.gameObject.AddComponent<Image>());
        Hud.GetComponent<Image>().raycastTarget = false;
        Timer = clock;
        Timer.transform.SetParent(Hud, false);
        Style(Timer, false, 40f, 40f);
        Timer.color = StageUITheme.Gold;
        Timer.gameObject.SetActive(true);
        Challenge = StageUITheme.Text("StageProgress", Hud, "", 26f, true);
        Style(Challenge, true, 22f, 24f);
        Title = StageUITheme.Text("CurrentStageLabel", Hud, TrialCatalog.ForScene("Setssss").Title, 26f);
        Style(Title, false, 22f, 26f);
        StagePresentation.Hud(Hud, Title, Timer, Challenge);
        // The legacy reminder duplicates the Information Board. Retain its
        // serialized reference, but never show it as another problem display.
        if (problem != null) problem.gameObject.SetActive(false);
        DeductionArea = StageUITheme.Rect("Time Adjustment Area", Hud);
        NoticeDock = StageUITheme.Rect("Setssss Notification Dock", safeArea);
        NoticeDock.anchorMin = NoticeDock.anchorMax = NoticeDock.pivot = Vector2.one;
        Notice = PrelimChallengeCompletionNotice.GetOrCreate(NoticeDock, Timer, feedback as TextMeshProUGUI);
        Notice.SuppressWaitingMessages = true;
        Notice.UseHostBounds();
        jump = safeArea.Find("Gameplay_Interface/JumpButton") as RectTransform;
        hand = safeArea.Find("Gameplay_Interface/InteractButton") as RectTransform;
        settings = safeArea.Find("Gameplay_Interface/SettingsButton") as RectTransform;
        var all = safeArea.GetComponentsInChildren<RectTransform>(true);
        var maps = new System.Collections.Generic.List<RectTransform>();
        foreach (var rect in all)
            if (rect.name == "MinimapPanel" || rect.name == "MiniMap" || rect.name == "TruthTableMinimapPanel") maps.Add(rect);
        minimaps = maps.ToArray();
        Refresh(); Layout();
    }

    private static void Style(TMP_Text text, bool reading, float min, float max)
    {
        text.font = reading ? StageUITheme.ReadingFont : StageUITheme.HeadingFont;
        text.color = reading ? StageUITheme.Ink : StageUITheme.Gold;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true; text.fontSizeMin = min; text.fontSizeMax = max;
        text.textWrappingMode = reading ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.margin = Vector4.zero;
    }

    private void Refresh()
    {
        if (stage == null || Hud == null) return;
        if (lastChallenge == stage.ChallengeNumber && lastQuestion == stage.QuestionIndex) return;
        lastChallenge = stage.ChallengeNumber; lastQuestion = stage.QuestionIndex;
        Challenge.text = "Challenge " + stage.ChallengeNumber + " of " + stage.ChallengeCount +
            "\nQuestion " + Mathf.Clamp(stage.QuestionIndex + 1, 1, stage.QuestionCount) + " of " + stage.QuestionCount;
    }

    private void LateUpdate()
    {
        Refresh(); Layout();
        if (safeArea == null || previousScale == safeArea.lossyScale) return;
        previousScale = safeArea.lossyScale;
        foreach (TMP_Text text in Hud.GetComponentsInChildren<TMP_Text>(true)) text.ForceMeshUpdate(true);
    }

    private void Layout()
    {
        if (safeArea == null || Hud == null || safeArea.rect.width <= Padding * 2f) return;
        float width = Mathf.Min(CompactWidth, safeArea.rect.width - EdgeMargin * 2f);
        float right = EdgeMargin, top = EdgeMargin;
        float height = LayoutContents(width);
        if (minimaps != null)
            foreach (var map in minimaps)
                if (map != null && map.gameObject.activeInHierarchy)
                {
                    Rect box = RelativeBounds(map, safeArea);
                    Rect candidate = new Rect(safeArea.rect.xMax - right - width, safeArea.rect.yMax - top - height, width, height);
                    if (!candidate.Overlaps(box)) continue;
                    float beside = safeArea.rect.xMax - box.xMin + 12f;
                    if (safeArea.rect.width - beside - EdgeMargin >= 360f)
                    {
                        right = Mathf.Max(right, beside);
                        width = Mathf.Min(width, safeArea.rect.width - right - EdgeMargin);
                        height = LayoutContents(width);
                    }
                    else top = Mathf.Max(top, safeArea.rect.yMax - box.yMin + 12f);
                }
        // Narrow screens can reserve the toolbar row instead of shrinking text.
        if (help == null) help = safeArea.Find("Gameplay_Interface/SetssssHelpButton") as RectTransform;
        foreach (RectTransform toolbar in new[] { settings, help })
            if (toolbar != null && toolbar.gameObject.activeInHierarchy)
            {
                Rect box = RelativeBounds(toolbar, safeArea);
                Rect candidate = new Rect(safeArea.rect.xMax - right - width, safeArea.rect.yMax - top - height, width, height);
                if (candidate.Overlaps(box))
                    top = Mathf.Max(top, safeArea.rect.yMax - box.yMin + 12f);
            }
        Hud.anchoredPosition = new Vector2(-right, -top);
        Hud.sizeDelta = new Vector2(width, height);
        float dockTop = top + height + 12f;
        float noticeRight = right, noticeWidth = width;
        float availableHeight = safeArea.rect.height - dockTop - 24f;
        foreach (RectTransform control in new[] { jump, hand })
            if (control != null && control.gameObject.activeInHierarchy)
            {
                Rect box = RelativeBounds(control, safeArea);
                if (box.xMax > safeArea.rect.xMax - right - width && box.xMin < safeArea.rect.xMax - right)
                    availableHeight = Mathf.Min(availableHeight, safeArea.rect.yMax - dockTop - box.yMax - 16f);
            }
        if (availableHeight < 140f)
        {
            // Short landscape safe areas have a higher jump button. Keep the
            // notification beside it, with enough height for the full message.
            float left = safeArea.rect.xMax - right - width;
            float upper = safeArea.rect.yMax - dockTop;
            foreach (RectTransform control in new[] { jump, hand })
                if (control != null && control.gameObject.activeInHierarchy)
                {
                    Rect box = RelativeBounds(control, safeArea);
                    Rect candidate = new Rect(left, upper - 180f, noticeWidth, 180f);
                    if (candidate.Overlaps(box) && box.xMin - 16f - left >= 360f)
                    {
                        noticeWidth = box.xMin - 16f - left;
                        noticeRight = safeArea.rect.xMax - box.xMin + 16f;
                    }
                }
            availableHeight = safeArea.rect.height - dockTop - 24f;
            foreach (RectTransform control in new[] { jump, hand })
                if (control != null && control.gameObject.activeInHierarchy)
                {
                    Rect box = RelativeBounds(control, safeArea);
                    if (box.xMax > left && box.xMin < left + noticeWidth)
                        availableHeight = Mathf.Min(availableHeight, upper - box.yMax - 16f);
                }
        }
        NoticeDock.anchoredPosition = new Vector2(-noticeRight, -dockTop);
        NoticeDock.sizeDelta = new Vector2(noticeWidth, Mathf.Clamp(availableHeight, 80f, 180f));
    }

    private float LayoutContents(float width)
    {
        if (presentation == null) presentation = stage.GetComponent<SetssssChallengePresentation>();
        float deductionHeight = presentation != null && presentation.HasVisibleDeductions ? 28f : 0f;
        TopRect(Title.rectTransform, width * .06f, MinimumHeight * .14f, width * .88f, MinimumHeight * .24f);
        TopRect(Timer.rectTransform, width * .06f, MinimumHeight * .43f, width * .25f, MinimumHeight * .39f);
        TopRect(Challenge.rectTransform, width * .32f, MinimumHeight * .40f, width * .62f, MinimumHeight * .52f);
        TopRect(DeductionArea, Padding, HeaderHeight, width - Padding * 2f, 24f);
        return deductionHeight > 0f ? HeaderHeight + deductionHeight + Padding : MinimumHeight;
    }

    private static void TopRect(RectTransform rect, float left, float top, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(left, -top);
        rect.sizeDelta = new Vector2(width, height);
    }

    public static Rect RelativeBounds(RectTransform rect, RectTransform host)
    {
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = -min;
        foreach (var corner in corners) { Vector2 point = host.InverseTransformPoint(corner); min = Vector2.Min(min, point); max = Vector2.Max(max, point); }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private void OnDisable() { if (Notice != null) Notice.Hide(); }
    private void OnDestroy()
    {
        if (Hud != null) Destroy(Hud.gameObject);
        if (NoticeDock != null) Destroy(NoticeDock.gameObject);
    }
}
