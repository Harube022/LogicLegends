using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>PRELIM presentation for the existing mapping stage; owns no gameplay clock or answers.</summary>
[DefaultExecutionOrder(1100)]
public sealed class FunctionsRelationsPresentation : MonoBehaviour
{
    public FunctionsRelationsStageManager stage;
    public RectTransform safeArea, hud, status, interaction, viewBoard, submit, boardBack, domainPanel, rangePanel;
    public TMP_Text progress, domainHint, rangeHint;
    public RectTransform[] mobileControls;
    private readonly List<Rect> occupied = new List<Rect>();
    private readonly List<Vector2> candidates = new List<Vector2>();
    private readonly List<float> candidateX = new List<float>(), candidateY = new List<float>();
    private readonly Vector3[] corners = new Vector3[4];
    private Vector3 textScale = Vector3.zero;
    private TMP_Text[] labels;
    public FunctionsRelationsConnectionsView Connections { get; private set; }

    private void Awake()
    {
        labels = GetComponentsInChildren<TMP_Text>(true);
        var title=hud!=null?hud.Find("CurrentStageLabel")?.GetComponent<TMP_Text>():null;
        if(title!=null)title.text=TrialCatalog.ForScene("Functions and Relations").Title;
        if (hud != null) StagePresentation.Hud(hud, title, hud.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t => t.name == "StageCountdown"), progress);
        Connections = gameObject.AddComponent<FunctionsRelationsConnectionsView>();
        Connections.Initialize(stage, rangePanel);
        gameObject.AddComponent<FunctionsRelationsGuidance>().Initialize(stage);
        var outputHeading = rangePanel != null ? rangePanel.Find("Heading")?.GetComponent<TMP_Text>() : null;
        if (outputHeading != null) outputHeading.text = "RANGE";
        var boardTitle = stage.WorldBoardLabels[0];
        if (boardTitle != null)
        {
            boardTitle.rectTransform.sizeDelta = new Vector2(1.43f, .48f);
            boardTitle.textWrappingMode = TextWrappingModes.NoWrap;
        }
        var boardSets = stage.WorldBoardLabels[1];
        if (boardSets != null)
        {
            boardSets.font = StageUITheme.ReadingFont; boardSets.richText = true;
            boardSets.enableAutoSizing = true; boardSets.fontSizeMin = 1.5f; boardSets.fontSizeMax = 2f;
            // Explicitly separate the two sets; auto-size each complete line instead of orphaning a brace.
            boardSets.textWrappingMode = TextWrappingModes.NoWrap; boardSets.lineSpacing = 2f;
        }
        // Keep the existing text references while retiring the dedicated station button.
        if (interaction != null)
        {
            var button = interaction.GetComponent<UnityEngine.UI.Button>();
            if (button != null) button.enabled = false;
            foreach (var image in interaction.GetComponentsInChildren<UnityEngine.UI.Image>(true)) image.enabled = false;
            foreach (var text in interaction.GetComponentsInChildren<TMP_Text>(true))
            { text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.Normal; }
        }
    }

    private void LateUpdate()
    {
        if (stage == null || safeArea == null || safeArea.rect.height <= 0f) return;
        if (progress != null)
            progress.text = "Challenge " + Mathf.Min(stage.ChallengeIndex + 1, stage.ChallengeCount) + " of " + stage.ChallengeCount +
                "\nQuestion " + (stage.QuestionIndex + 1) + " of " + stage.QuestionCount;
        hud.gameObject.SetActive(!stage.IsBoardOpen);
        float width = safeArea.rect.width;
        Place(hud, Vector2.one, new Vector2(-24f, -24f), new Vector2(500f, 136f), Vector2.one);
        LayoutMessage(status, -174f);
        float afterStatus = 174f + (status != null && status.gameObject.activeInHierarchy ? status.rect.height + 12f : 0f);
        LayoutMessage(interaction, -afterStatus);
        Place(viewBoard, new Vector2(.5f, 0f), new Vector2(0f, 148f), new Vector2(240f, 88f), new Vector2(.5f, 0f));
        Place(submit, new Vector2(.5f, 0f), new Vector2(0f, 248f), new Vector2(240f, 88f), new Vector2(.5f, 0f));
        Place(boardBack, new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(240f, 88f), new Vector2(0f, 1f));
        FitBetweenControls(viewBoard);
        FitBetweenControls(submit);
        occupied.Clear();
        foreach (var rect in new[] { hud, status, interaction, viewBoard, submit }) AddObstacle(rect);
        if (mobileControls != null) foreach (var rect in mobileControls) AddObstacle(rect);
        LayoutChoices(domainPanel);
        LayoutChoices(rangePanel);
        LayoutRangeContents();
        if (domainHint != null) domainHint.text = "Choose the input for your next connection.";
        if (rangeHint != null)
        {
            rangeHint.text = stage.SelectedDomain != null ?
                "Input " + stage.SelectedDomain + ": choose an output." : "Choose a new input at Domain to add a connection.";
        }
        // Screen resizing can leave saved TMP SDF geometry using the previous canvas scale.
        // Rebuild only when that scale changes, including the initial mobile layout.
        if (textScale != transform.lossyScale)
        {
            textScale = transform.lossyScale;
            foreach (var label in labels) if (label != null) label.ForceMeshUpdate(true, true);
        }
    }

    private void LayoutRangeContents()
    {
        if (rangePanel == null) return;
        var heading = rangePanel.Find("Heading") as RectTransform;
        if (heading != null) StageUITheme.Stretch(heading, new Vector2(.05f,.86f), new Vector2(.95f,.97f));
        if (rangeHint != null) StageUITheme.Stretch(rangeHint.rectTransform, new Vector2(.05f,.73f), new Vector2(.95f,.85f));
        for (int i=0;i<stage.RangeButtons.Length;i++)
        {
            int col=i%2,row=i/2;
            StageUITheme.Stretch((RectTransform)stage.RangeButtons[i].transform,
                new Vector2(.06f+col*.21f,.40f-row*.23f), new Vector2(.24f+col*.21f,.61f-row*.23f));
        }
        var close=rangePanel.Find("Close Choices") as RectTransform;
        if(close!=null)StageUITheme.Stretch(close,new Vector2(.68f,.03f),new Vector2(.94f,.17f));
    }

    private void LayoutMessage(RectTransform rect, float y)
    {
        if (rect == null) return;
        var text = rect.GetComponent<TMP_Text>() ?? rect.GetComponentInChildren<TMP_Text>(true);
        if (text == null) return;
        text.enableAutoSizing = false; text.fontSize = 27f;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.alignment = TextAlignmentOptions.Top;
        text.raycastTarget = false; text.color = StageUITheme.Gold;
        text.outlineColor = new Color(.02f,.035f,.04f); text.outlineWidth = .14f;
        float height = Mathf.Max(38f, text.GetPreferredValues(text.text, 488f, 1000f).y + 8f);
        Place(rect, Vector2.one, new Vector2(-24f, y), new Vector2(hud.rect.width, height), Vector2.one);
        if (text.rectTransform != rect) StageUITheme.Stretch(text.rectTransform, Vector2.zero, Vector2.one);
    }

    private void AddObstacle(RectTransform rect)
    {
        if (rect == null || !rect.gameObject.activeInHierarchy) return;
        occupied.Add(BoundsIn(rect));
    }

    private Rect BoundsIn(RectTransform rect)
    {
        rect.GetWorldCorners(corners);
        Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
        foreach (var corner in corners)
        {
            Vector2 point = safeArea.InverseTransformPoint(corner);
            min = Vector2.Min(min, point); max = Vector2.Max(max, point);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private void FitBetweenControls(RectTransform button)
    {
        if (button == null || mobileControls == null) return;
        Rect area = safeArea.rect, box = BoundsIn(button);
        float left = area.xMin + 24f, right = area.xMax - 24f;
        foreach (var control in mobileControls)
        {
            if (control == null || !control.gameObject.activeInHierarchy) continue;
            Rect obstacle = BoundsIn(control);
            if (obstacle.yMax < box.yMin || obstacle.yMin > box.yMax) continue;
            if (obstacle.center.x < area.center.x) left = Mathf.Max(left, obstacle.xMax + 24f);
            else right = Mathf.Min(right, obstacle.xMin - 24f);
        }
        if (right <= left) return;
        button.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Min(button.rect.width, right - left));
        button.anchoredPosition = new Vector2((left + right) * .5f - area.center.x, button.anchoredPosition.y);
    }

    private void LayoutChoices(RectTransform panel)
    {
        if (panel == null || !panel.gameObject.activeInHierarchy) return;
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
        panel.sizeDelta = panel == rangePanel ?
            new Vector2(Mathf.Min(820f,safeArea.rect.width-48f),440f) :
            new Vector2(Mathf.Min(640f, safeArea.rect.width - 48f), Mathf.Clamp(safeArea.rect.height * .33f, 300f, 360f));
        Rect area = safeArea.rect;
        Vector2 preferred = area.center + new Vector2(0f, 40f), best = preferred;
        float bestScore = float.MaxValue;
        candidates.Clear(); candidateX.Clear(); candidateY.Clear();
        candidateX.Add(preferred.x); candidateY.Add(preferred.y);
        // Combine edges from different controls to find narrow gaps between the Submit row and joystick.
        foreach (var obstacle in occupied)
        {
            float right = obstacle.xMax + panel.rect.width * .5f + 16f;
            float left = obstacle.xMin - panel.rect.width * .5f - 16f;
            float above = obstacle.yMax + panel.rect.height * .5f + 16f;
            float below = obstacle.yMin - panel.rect.height * .5f - 16f;
            candidateX.Add(right); candidateX.Add(left); candidateY.Add(above); candidateY.Add(below);
        }
        for (int i = 1; i <= 9; i++)
        { candidateX.Add(area.xMin + area.width * i / 10f); candidateY.Add(area.yMin + area.height * i / 10f); }
        foreach (var x in candidateX) foreach (var y in candidateY) candidates.Add(new Vector2(x, y));
        foreach (var candidate in candidates)
        {
            Vector2 point = new Vector2(
                Mathf.Clamp(candidate.x, area.xMin + panel.rect.width * .5f + 20f, area.xMax - panel.rect.width * .5f - 20f),
                Mathf.Clamp(candidate.y, area.yMin + panel.rect.height * .5f + 20f, area.yMax - panel.rect.height * .5f - 20f));
            Rect box = new Rect(point - panel.sizeDelta * .5f, panel.sizeDelta);
            float overlap = 0f;
            foreach (var obstacle in occupied)
                overlap += Mathf.Max(0f, Mathf.Min(box.xMax, obstacle.xMax) - Mathf.Max(box.xMin, obstacle.xMin)) *
                    Mathf.Max(0f, Mathf.Min(box.yMax, obstacle.yMax) - Mathf.Max(box.yMin, obstacle.yMin));
            float score = overlap * 10000f + Vector2.Distance(preferred, point);
            if (score < bestScore) { best = point; bestScore = score; }
        }
        panel.anchoredPosition = best - area.center;
    }

    private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
    {
        if (rect == null) return;
        rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot;
        rect.sizeDelta = size; rect.anchoredPosition = position;
    }
}
