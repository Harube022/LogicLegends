using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Read-only assertions usable by the saved-scene mobile Play Mode checks.</summary>
public static class TutorialSpotlightChecks
{
    public static void Verify(RectTransform explanation, RectTransform control)
    {
        var owner = explanation.GetComponent<TutorialSpotlight>();
        Require(owner != null && owner.IsVisible, "Tutorial spotlight is visible for " + control.name);
        Require(Vector2.Distance(owner.TargetScreenRect.center, ScreenCenter(control)) < 1f,
            "Spotlight follows the actual control after safe-area/layout changes");
        var graphics = UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Graphic>(FindObjectsSortMode.None)
            .Where(g => g.name == "Tutorial Spotlight (Visual Only)" && g.gameObject.activeInHierarchy).ToArray();
        Require(graphics.Length == 1, "Only one tutorial veil is visible");
        var graphic = graphics[0];
        Require(!graphic.raycastTarget && graphic.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null,
            "Spotlight cannot receive or intercept touch events");
        var mesh = graphic.canvasRenderer.GetMesh();
        {
            Require(mesh != null, "Spotlight mesh is available after rendering");
            Require(mesh.vertexCount > 0, "Spotlight draws a dim exterior");
            Require(!Covers(graphic, mesh, ScreenCenter(control)), "Target control stays bright");
            Require(!Covers(graphic, mesh, ScreenCenter(explanation)), "Explanation stays bright");
            foreach (var skip in UnityEngine.Object.FindObjectsByType<PrelimTutorialSkipControl>(FindObjectsSortMode.None))
                if (skip.gameObject.activeInHierarchy)
                    Require(!Covers(graphic, mesh, ScreenCenter((RectTransform)skip.transform)), "Shared Skip stays bright");
            var customSkip = UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None)
                .FirstOrDefault(button => button.name == "Inference Shared Skip");
            if (customSkip != null)
                Require(!Covers(graphic, mesh, ScreenCenter((RectTransform)customSkip.transform)), "Inference Skip stays bright");
            if (EventSystem.current != null)
            {
                var eventData = new PointerEventData(EventSystem.current) { position = ScreenCenter(control) };
                var hits = new System.Collections.Generic.List<RaycastResult>();
                EventSystem.current.RaycastAll(eventData, hits);
                Require(hits.All(hit => hit.gameObject != graphic.gameObject), "Spotlight never enters mobile raycast results");
            }
        }
    }

    public static void VerifyCleanedUp()
    {
        Require(!UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Graphic>(FindObjectsSortMode.None)
            .Any(g => g.name == "Tutorial Spotlight (Visual Only)" && g.gameObject.activeInHierarchy),
            "Tutorial veil is removed after skip/completion");
    }

    static Vector2 ScreenCenter(RectTransform rect)
    {
        var canvas = rect.GetComponentInParent<Canvas>();
        return RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            rect.TransformPoint(rect.rect.center));
    }
    static bool Covers(UnityEngine.UI.Graphic graphic, Mesh mesh, Vector2 screen)
    {
        Vector2 point;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(graphic.rectTransform, screen,
            graphic.canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : graphic.canvas.worldCamera, out point);
        var vertices = mesh.vertices; var indices = mesh.triangles;
        for (int i = 0; i < indices.Length; i += 3)
        {
            Vector2 a = vertices[indices[i]], b = vertices[indices[i + 1]], c = vertices[indices[i + 2]];
            float one = Cross(point - a, b - a), two = Cross(point - b, c - b), three = Cross(point - c, a - c);
            if ((one >= 0 && two >= 0 && three >= 0) || (one <= 0 && two <= 0 && three <= 0)) return true;
        }
        return false;
    }
    static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
