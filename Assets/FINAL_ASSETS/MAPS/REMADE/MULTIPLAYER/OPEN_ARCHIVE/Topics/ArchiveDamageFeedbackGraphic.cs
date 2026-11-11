using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Local impact feedback driven by a confirmed decrease in the existing health HUD.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ArchiveDamageFeedbackGraphic : UnityEngine.UI.MaskableGraphic
    {
        const float Duration = .58f;
        const float RimDuration = .38f;
        readonly List<ArchiveHeartGraphic> hearts = new List<ArchiveHeartGraphic>();
        readonly Vector3[] slotCorners = new Vector3[4];
        RectTransform healthHud;
        Camera healthCamera;
        int firstLost, lostCount;
        float startedAt;

        public static ArchiveDamageFeedbackGraphic Create(Transform owner)
        {
            var root = new GameObject("Archive_Damage_Feedback", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(root, owner.gameObject.scene);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 155;

            var layer = new GameObject("Damage_Edge_Pulse", typeof(RectTransform), typeof(ArchiveDamageFeedbackGraphic));
            var effect = layer.GetComponent<ArchiveDamageFeedbackGraphic>();
            effect.rectTransform.SetParent(root.transform, false);
            effect.rectTransform.anchorMin = Vector2.zero;
            effect.rectTransform.anchorMax = Vector2.one;
            effect.rectTransform.offsetMin = effect.rectTransform.offsetMax = Vector2.zero;
            effect.raycastTarget = false;
            return effect;
        }

        public void Present(HealthManager health, int current, int previous)
        {
            healthHud = health.transform as RectTransform;
            var healthCanvas = health.GetComponentInParent<Canvas>();
            healthCamera = healthCanvas != null && healthCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? healthCanvas.worldCamera : null;
            firstLost = current;
            lostCount = previous - current;
            while (hearts.Count < lostCount)
            {
                var layer = new GameObject("Lost_Heart_Pulse", typeof(RectTransform), typeof(ArchiveHeartGraphic));
                var heart = layer.GetComponent<ArchiveHeartGraphic>();
                heart.rectTransform.SetParent(rectTransform, false);
                heart.raycastTarget = false;
                hearts.Add(heart);
            }

            startedAt = Time.unscaledTime;
            gameObject.SetActive(true);
            TickPulse(0);
        }

        void Update()
        {
            float elapsed = Time.unscaledTime - startedAt;
            if (elapsed >= Duration)
            {
                gameObject.SetActive(false);
                return;
            }
            TickPulse(elapsed);
        }

        void TickPulse(float elapsed)
        {
            float rim = 1 - Mathf.Clamp01(elapsed / RimDuration);
            canvasRenderer.SetAlpha(rim * rim);
            float t = Mathf.Clamp01(elapsed / Duration);
            float grow = Mathf.Lerp(1.15f, 1.6f, Mathf.Sin(t * Mathf.PI * .5f));
            for (int i = 0; i < hearts.Count; i++)
            {
                var heart = hearts[i];
                var slot = i < lostCount && healthHud != null ? healthHud.Find("Heart_" + (firstLost + i + 1)) as RectTransform : null;
                heart.gameObject.SetActive(slot != null);
                if (slot == null) continue;

                slot.GetWorldCorners(slotCorners);
                var lower = RectTransformUtility.WorldToScreenPoint(healthCamera, slotCorners[0]);
                var upper = RectTransformUtility.WorldToScreenPoint(healthCamera, slotCorners[2]);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, (lower + upper) * .5f, null, out var center);
                heart.rectTransform.anchoredPosition = center + Vector2.down * (upper.y - lower.y) * (.40f * t);
                heart.rectTransform.sizeDelta = (upper - lower) * grow / Mathf.Max(.01f, canvas.scaleFactor);
                heart.canvasRenderer.SetAlpha(1 - t);
            }
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            var rect = GetPixelAdjustedRect();
            float edge = Mathf.Min(rect.width, rect.height) * .17f;
            var tint = new Color(.83f, .065f, .10f, .70f);
            var clear = new Color(tint.r, tint.g, tint.b, 0);
            for (int i = 0; i < 4; i++)
            {
                var from = Corner(rect, i, 0);
                var to = Corner(rect, (i + 1) % 4, 0);
                var innerTo = Corner(rect, (i + 1) % 4, edge);
                var innerFrom = Corner(rect, i, edge);
                int vertex = mesh.currentVertCount;
                mesh.AddVert(from, tint, Vector2.zero);
                mesh.AddVert(to, tint, Vector2.zero);
                mesh.AddVert(innerTo, clear, Vector2.zero);
                mesh.AddVert(innerFrom, clear, Vector2.zero);
                mesh.AddTriangle(vertex, vertex + 1, vertex + 2);
                mesh.AddTriangle(vertex, vertex + 2, vertex + 3);
            }
        }

        static Vector2 Corner(Rect rect, int corner, float inset)
        {
            float x = corner < 2 ? rect.xMin + inset : rect.xMax - inset;
            float y = corner == 0 || corner == 3 ? rect.yMin + inset : rect.yMax - inset;
            return new Vector2(x, y);
        }
    }
}
