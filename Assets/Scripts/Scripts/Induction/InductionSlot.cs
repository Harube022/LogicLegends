using TMPro;
using UnityEngine;

namespace LogicLegends.Induction
{
    public sealed class InductionSlot : MonoBehaviour
    {
        public string key, displayName;
        public TMP_Text label;
        public MeshRenderer rim;
        public Transform tabletAnchor;
        [Min(.5f)] public float formulaLabelHeight = 1.1f;
        public InductionTablet Tablet { get; private set; }
        public bool HasError { get; private set; }
        MaterialPropertyBlock color;
        public void SetTablet(InductionTablet tablet)
        {
            Tablet = tablet;
            if (tablet != null)
            {
                tablet.Slot = this;
                tablet.transform.SetPositionAndRotation(tabletAnchor.position, tabletAnchor.rotation);
                tablet.RefreshLabel();
            }
            MarkError(false);
        }
        public InductionTablet Remove()
        {
            var previous = Tablet; Tablet = null;
            if (previous != null) { previous.Slot = null; previous.SetError(false); previous.RefreshLabel(); }
            MarkError(false); return previous;
        }
        public void MarkError(bool error)
        {
            HasError = error;
            if (Tablet != null) Tablet.SetError(error);
            if (label != null)
            {
                label.text = displayName + (error ? Tablet == null ? "\nMissing tablet" : "\nCheck this tablet" : "");
                label.color = error ? new Color(1, .35f, .3f) : StageUITheme.Ink;
            }
            if (rim == null) return;
            if (color == null) color = new MaterialPropertyBlock();
            color.SetColor("_BaseColor", error ? new Color(.8f,.06f,.04f) : new Color(.05f,.35f,.42f));
            rim.SetPropertyBlock(color);
        }
        void LateUpdate()
        {
            if (Camera.main != null && label != null) label.transform.rotation = Quaternion.LookRotation(Camera.main.transform.forward, Camera.main.transform.up);
        }
    }
}
