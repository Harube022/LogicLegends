using TMPro;
using UnityEngine;

namespace LogicLegends.Induction
{
    [DefaultExecutionOrder(1600), RequireComponent(typeof(GrabbableObject), typeof(BoxCollider))]
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
            // Wrap the whole expression; never replace a proof term with an ellipsis.
            label.overflowMode = TextOverflowModes.Overflow;
            label.fontStyle |= FontStyles.Bold;
            label.enableAutoSizing = true; label.fontSizeMax = 4.8f; label.fontSizeMin = 2.5f;
            label.outlineColor = new Color(.02f,.025f,.035f,1); label.outlineWidth = .16f;
            SetError(false);
        }
        public void SetError(bool error)
        { HasError = error; if (label != null) label.color = error ? new Color(1,.35f,.3f) : new Color(1,.94f,.73f); }
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
                label.rectTransform.sizeDelta = new Vector2(Slot != null ? 2.8f : choiceLabelWidth, Slot != null ? 1.5f : 1.7f);
                var mesh = GetComponent<Renderer>();
                var bounds = mesh != null ? mesh.bounds : new Bounds(transform.position,Vector3.one*.5f);
                InductionWorldLabel.PlaceAbove(label,bounds,Slot != null ? Mathf.Max(.25f,Slot.formulaLabelHeight-.5f) : .4f,Vector3.zero);
            }
        }
    }
}
