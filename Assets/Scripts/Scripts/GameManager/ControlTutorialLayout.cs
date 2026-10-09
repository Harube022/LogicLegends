using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>Places control explanations beside their subject and clear of gameplay UI.</summary>
public static class ControlTutorialLayout
{
    private static readonly string[] blockerNames = { "SettingsButton", "PropositionalHelpButton",
        "TruthTableHelpButton", "InferenceHelpButton", "CurrentStageHud", "TruthTableMinimapPanel",
        "Joystick", "JumpButton", "InteractButton", "InventoryBar", "AdminButton", "ReadBook_BTN",
        "TruthTableSubmitColumnButton", "QuestionText", "TruthTableHud" };

    public static RectTransform[] FindBlockers()
    {
        var result = new List<RectTransform>();
        foreach (var rect in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (System.Array.IndexOf(blockerNames, rect.name) >= 0) result.Add(rect);
        return result.ToArray();
    }

    public static RectTransform JourneyElement(string name)
    {
        return StageJourneyUI.Instance != null
            ? StageJourneyUI.Instance.transform.Find("SafeArea/CurrentStageHud/" + name) as RectTransform : null;
    }

    private static Rect BoundsIn(RectTransform rect, RectTransform host)
    {
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = -min;
        foreach (var corner in corners)
        {
            Vector2 point = host.InverseTransformPoint(corner);
            min = Vector2.Min(min, point); max = Vector2.Max(max, point);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    public static void Place(RectTransform panel, TMP_Text text, RectTransform next,
        RectTransform host, RectTransform target, bool preferBottom, RectTransform[] blockers,
        RectTransform arrow)
    {
        if (panel == null || !panel.gameObject.activeSelf || host.rect.width <= 0f) return;
        bool showNext = next != null && next.gameObject.activeSelf;
        float width = Mathf.Min(620f, host.rect.width - 40f);
        text.enableAutoSizing = false; text.fontSize = 36f;
        float textHeight = text.GetPreferredValues(text.text, width - 48f, 0f).y;
        float height = Mathf.Max(116f, textHeight + 48f) + (showNext ? 92f : 0f);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(width, height);
        text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(24f, showNext ? 106f : 24f);
        text.rectTransform.offsetMax = new Vector2(-24f, -24f);
        if (next != null)
        {
            next.anchorMin = next.anchorMax = next.pivot = new Vector2(0.5f, 0f);
            next.sizeDelta = new Vector2(210f, 76f); next.anchoredPosition = new Vector2(0f, 14f);
        }
        var occupied = new List<Rect>();
        if (blockers != null)
            foreach (var blocker in blockers)
                if (blocker != null && blocker.gameObject.activeInHierarchy)
                    occupied.Add(BoundsIn(blocker, host));
        Rect area = host.rect;
        Rect subject = target != null && target.gameObject.activeInHierarchy ? BoundsIn(target, host)
            : new Rect(area.center, Vector2.zero);
        bool hasTarget = target != null && target.gameObject.activeInHierarchy;
        var candidates = new List<Vector2>();
        float gap = 64f;
        if (hasTarget)
        {
            candidates.Add(new Vector2(subject.center.x, subject.yMax + gap + height * 0.5f));
            candidates.Add(new Vector2(subject.xMin - gap - width * 0.5f, subject.center.y));
            candidates.Add(new Vector2(subject.xMax + gap + width * 0.5f, subject.center.y));
            candidates.Add(new Vector2(subject.center.x, subject.yMin - gap - height * 0.5f));
        }
        else candidates.Add(new Vector2(area.center.x, area.yMin + area.height * (preferBottom ? 0.3f : 0.5f)));
        for (int y = 1; y <= 9; y++)
            for (int x = 1; x <= 9; x++)
                candidates.Add(new Vector2(area.xMin + area.width * x / 10f, area.yMin + area.height * y / 10f));
        Vector2 best = area.center; float bestScore = float.MaxValue;
        foreach (var candidate in candidates)
        {
            Vector2 center = new Vector2(
                Mathf.Clamp(candidate.x, area.xMin + width * 0.5f + 20f, area.xMax - width * 0.5f - 20f),
                Mathf.Clamp(candidate.y, area.yMin + height * 0.5f + 20f, area.yMax - height * 0.5f - 20f));
            Rect box = new Rect(center - new Vector2(width, height) * 0.5f, new Vector2(width, height));
            float overlap = 0f;
            foreach (var obstacle in occupied) overlap += Intersection(box, obstacle);
            if (hasTarget && subject.width * subject.height < area.width * area.height * 0.5f)
                overlap += Intersection(box, subject);
            float score = overlap * 10000f + Vector2.Distance(center, hasTarget ? subject.center : candidates[0]);
            if (score < bestScore) { bestScore = score; best = center; }
        }
        panel.anchoredPosition = best - area.center;
        if (arrow != null && hasTarget && arrow.parent != host)
        {
            Vector2 direction = best - subject.center;
            bool horizontal = Mathf.Abs(direction.x) > Mathf.Abs(direction.y);
            Vector2 edge = horizontal ? new Vector2(direction.x > 0f ? 1f : 0f, 0.5f)
                : new Vector2(0.5f, direction.y > 0f ? 1f : 0f);
            arrow.anchorMin = arrow.anchorMax = edge; arrow.pivot = new Vector2(0.5f, 0.5f);
            arrow.sizeDelta = new Vector2(64f, 48f);
            arrow.anchoredPosition = horizontal ? new Vector2(direction.x > 0f ? 32f : -32f, 0f)
                : new Vector2(0f, direction.y > 0f ? 32f : -32f);
            arrow.localRotation = Quaternion.Euler(0f, 0f, horizontal ? (direction.x > 0f ? -90f : 90f) : direction.y > 0f ? 0f : 180f);
            var label = arrow.GetComponent<TMP_Text>(); label.text = "\u25bc"; label.fontSize = 48f;
        }
    }

    private static float Intersection(Rect a, Rect b)
    {
        return Mathf.Max(0f, Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin)) *
            Mathf.Max(0f, Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin));
    }
}
