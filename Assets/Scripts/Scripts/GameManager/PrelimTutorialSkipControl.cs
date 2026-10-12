using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>One persistent skip control shared by the two PRELIM onboarding flows.</summary>
public sealed class PrelimTutorialSkipControl : MonoBehaviour
{
    private RectTransform host;
    private RectTransform rect;
    private Vector2 lastHostSize;
    private Button button;
    private RectTransform[] reservedTargets = new RectTransform[0];

    // A stage can reserve HUD space even when that HUD is hidden in early steps.
    public void Reserve(params RectTransform[] targets)
    {
        reservedTargets = targets ?? new RectTransform[0];
        Position();
    }

    public static PrelimTutorialSkipControl Create(RectTransform safeArea, UnityAction skip)
    {
        Transform existing = safeArea.Find("PRELIM Shared Skip Tutorial");
        var control = existing != null ? existing.GetComponent<PrelimTutorialSkipControl>() : null;
        if (control == null)
        {
            var created = StageUITheme.Button("PRELIM Shared Skip Tutorial", safeArea, "Skip Tutorial", null);
            control = created.gameObject.AddComponent<PrelimTutorialSkipControl>();
            control.button = created;
            control.rect = (RectTransform)created.transform;
            control.host = safeArea;
        }
        control.button.onClick.RemoveAllListeners();
        control.button.onClick.AddListener(skip);
        control.Show(true);
        control.Position();
        return control;
    }

    public void Show(bool visible)
    {
        gameObject.SetActive(visible);
        if (visible) transform.SetAsLastSibling();
    }

    // Reserve the same space for every step; instruction panels avoid it through
    // ControlTutorialLayout. Recompute only when the safe area changes.
    private void Position()
    {
        if (host == null || host.rect.width <= 0f) return;
        lastHostSize = host.rect.size;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(Mathf.Min(250f, host.rect.width - 40f), 80f);
        var blockers = new System.Collections.Generic.List<RectTransform>(ControlTutorialLayout.FindBlockers());
        blockers.AddRange(reservedTargets);
        Rect area = host.rect;
        Vector2 best = new Vector2(20f, -20f);
        float bestScore = float.MaxValue;
        // Prefer the top row beside Settings/Help, away from movement controls.
        for (int row = 0; row < 3; row++)
        for (float x = 20f; x <= area.width - rect.sizeDelta.x - 20f; x += 20f)
        {
            float y = 20f + row * 100f;
            Rect candidate = new Rect(area.xMin + x, area.yMax - y - 80f, rect.sizeDelta.x, 80f);
            float overlap = 0f;
            foreach (var obstacle in blockers)
            {
                if (obstacle == null || obstacle == rect || (!obstacle.gameObject.activeInHierarchy && System.Array.IndexOf(reservedTargets, obstacle) < 0)) continue;
                var corners = new Vector3[4];
                obstacle.GetWorldCorners(corners);
                Vector2 min = host.InverseTransformPoint(corners[0]);
                Vector2 max = min;
                foreach (var corner in corners)
                {
                    Vector2 point = host.InverseTransformPoint(corner);
                    min = Vector2.Min(min, point); max = Vector2.Max(max, point);
                }
                Rect bounds = Rect.MinMaxRect(min.x - 16f, min.y - 16f, max.x + 16f, max.y + 16f);
                overlap += Mathf.Max(0f, Mathf.Min(candidate.xMax, bounds.xMax) - Mathf.Max(candidate.xMin, bounds.xMin))
                    * Mathf.Max(0f, Mathf.Min(candidate.yMax, bounds.yMax) - Mathf.Max(candidate.yMin, bounds.yMin));
            }
            float score = overlap * 10000f + row * 1000f + Mathf.Abs(x - 380f);
            if (score < bestScore) { bestScore = score; best = new Vector2(x, -y); }
        }
        rect.anchoredPosition = best;
    }

    private void LateUpdate()
    {
        if (host != null && host.rect.size != lastHostSize) Position();
        transform.SetAsLastSibling(); // Remain clickable above the practice quiz shade.
    }

    public void Release()
    {
        button.onClick.RemoveAllListeners();
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    public static void CancelCamera(CinemachineCamera camera)
    {
        if (camera == null) return;
        camera.gameObject.SetActive(false);
        // Drop both the tutorial's moving camera and any Cinemachine blend that
        // still references it, then immediately evaluate the gameplay camera.
        foreach (var brain in Object.FindObjectsByType<CinemachineBrain>(FindObjectsSortMode.None))
        {
            var previousUpdate = brain.UpdateMethod;
            try
            {
                brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
                brain.ResetState();
                brain.ManualUpdate();
                brain.ActiveBlend = null;
            }
            finally { brain.UpdateMethod = previousUpdate; }
        }
        Destroy(camera.gameObject);
    }
}
