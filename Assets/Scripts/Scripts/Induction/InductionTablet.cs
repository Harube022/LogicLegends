using TMPro;
using UnityEngine;

namespace LogicLegends.Induction
{
    [RequireComponent(typeof(GrabbableObject), typeof(BoxCollider))]
    public sealed class InductionTablet : MonoBehaviour
    {
        public FormulaTabletData Data { get; private set; }
        public InductionSlot Slot { get; internal set; }
        public Vector3 Home { get; private set; }
        public Quaternion HomeRotation { get; private set; }
        public GrabbableObject Grabber => GetComponent<GrabbableObject>();
        public TMP_Text label;
        [Min(1)] public float choiceLabelWidth = 4.4f;
        public bool HasError { get; private set; }
        public bool AtStand { get; private set; }
        Transform stockParent;
        public void Initialize(FormulaTabletData data, Vector3 home)
        {
            Data = data; Home = home; HomeRotation = transform.rotation; stockParent = transform.parent; label.text = data.expression;
            label.textWrappingMode = TextWrappingModes.Normal;
            // The full formula remains available when selecting it in the stand catalogue.
            // Avoid spilling into neighbouring labels if an unusually long formula
            // cannot fit at the configured minimum font size.
            label.overflowMode = TextOverflowModes.Ellipsis;
            SetError(false);
        }
        public void SetError(bool error)
        { HasError = error; if (label != null) label.color = error ? new Color(1,.35f,.3f) : StageUITheme.Ink; }
        public void RefreshLabel()
        {
            if (Data == null || label == null) return;
            string line = Slot != null && Slot.key.StartsWith("Step:") && Slot.displayName != "Inductive Step (goal)"
                ? Slot.key.Substring(5) + ". " : "";
            label.text = line + Data.expression;
        }
        public void ReturnHome()
        { ReturnToStand(); }
        public void ReturnToStand()
        {
            if (Slot != null) Slot.Remove();
            Grabber.Drop();
            transform.SetParent(stockParent,true);
            transform.SetPositionAndRotation(Home, HomeRotation);
            SetError(false); AtStand = true; gameObject.SetActive(false);
        }
        public void TakeFromStand() { AtStand = false; SetError(false); gameObject.SetActive(true); }
        void LateUpdate()
        {
            var camera = Camera.main;
            if (camera != null && label != null)
            {
                RefreshLabel();
                label.transform.position = transform.position + Vector3.up * (Slot != null ? Slot.formulaLabelHeight : 1.1f);
                label.rectTransform.sizeDelta = new Vector2(Slot != null ? 2.8f : choiceLabelWidth, Slot != null ? 1.1f : 1.4f);
                label.transform.rotation = Quaternion.LookRotation(camera.transform.forward, camera.transform.up);
            }
        }
    }
}
