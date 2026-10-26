using TMPro;
using UnityEngine;

namespace LogicLegends.Induction
{
    /// <summary>Billboard text uses a stable mesh anchor, never its own rotating text bounds.</summary>
    [DefaultExecutionOrder(1600), DisallowMultipleComponent]
    public sealed class InductionWorldLabel : MonoBehaviour
    {
        public Renderer anchor;
        public float clearance = .5f;
        public Vector3 horizontalOffset;
        TMP_Text text;
        void Awake() { text = GetComponent<TMP_Text>(); }
        void LateUpdate()
        {
            if (text == null) text = GetComponent<TMP_Text>();
            if (anchor == null || text == null) return;
            PlaceAbove(text, anchor.bounds, clearance, horizontalOffset);
        }
        public static void PlaceAbove(TMP_Text label, Bounds bounds, float gap, Vector3 offset)
        {
            var camera = Camera.main;
            if (camera != null) label.transform.rotation = camera.transform.rotation;
            Vector2 size = label.rectTransform.rect.size;
            Vector3 scale = label.transform.lossyScale;
            // Account for camera roll as well as pitch. The complete text rectangle
            // stays above the object even as the camera or the carried scroll rotates.
            float halfHeight = size.y * Mathf.Abs(scale.y) * .5f;
            float projected = halfHeight * Mathf.Abs(label.transform.up.y) +
                size.x * Mathf.Abs(scale.x) * .5f * Mathf.Abs(label.transform.right.y);
            label.transform.position = new Vector3(bounds.center.x + offset.x,
                bounds.max.y + gap + Mathf.Max(halfHeight, projected) + offset.y, bounds.center.z + offset.z);
        }
    }
}
