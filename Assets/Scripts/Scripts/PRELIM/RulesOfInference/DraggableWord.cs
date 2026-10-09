// using UnityEngine;
// using UnityEngine.EventSystems;

// namespace LogicLegends.Inference
// {
//     [RequireComponent(typeof(CanvasGroup))]
//     public sealed class DraggableWord : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
//     {
//         public string Word { get; private set; }
//         public DropZone Zone { get; internal set; }
//         public bool Locked { get; set; }
//         public static DraggableWord Active { get; private set; }
//         Transform home, dragLayer;
//         RectTransform rect;
//         CanvasGroup group;
//         bool dragging;

//         public void Configure(string word, Transform bankHome, Transform layer)
//         {
//             Word = word; home = bankHome; dragLayer = layer;
//             rect = (RectTransform)transform; group = GetComponent<CanvasGroup>();
//         }

//         public void OnBeginDrag(PointerEventData data)
//         {
//             if (Locked || (Active != null && Active != this)) return;
//             dragging = true; Active = this;
//             if (Zone != null) Zone.Release(this);
//             transform.SetParent(dragLayer, true); transform.SetAsLastSibling();
//             group.blocksRaycasts = false; group.alpha = 0.9f;
//             OnDrag(data);
//         }

//         public void OnDrag(PointerEventData data)
//         {
//             if (!dragging) return;
//             Vector3 point;
//             if (RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)dragLayer, data.position, data.pressEventCamera, out point))
//                 rect.position = point;
//         }

//         public void OnEndDrag(PointerEventData data)
//         {
//             if (!dragging) return;
//             dragging = false; group.alpha = 1; group.blocksRaycasts = true;
//             if (Zone == null) ReturnToBank();
//             Active = null;
//         }

//         public void ReturnToBank()
//         {
//             if (Zone != null) Zone.Release(this);
//             transform.SetParent(home, false);
//             rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
//             rect.pivot = new Vector2(0.5f, 0.5f);
//             rect.anchoredPosition = Vector2.zero;
//             rect.localScale = Vector3.one;
//         }

//         void OnDisable()
//         {
//             if (Active != this) return;
//             Active = null; dragging = false;
//             if (group != null) { group.blocksRaycasts = true; group.alpha = 1; }
//             if (home != null) ReturnToBank();
//         }
//     }
// }


using UnityEngine;
using UnityEngine.EventSystems;

namespace LogicLegends.Inference
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class DraggableWord : MonoBehaviour, IPointerClickHandler, IInitializePotentialDragHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public string Word { get; private set; }
        public DropZone Zone { get; internal set; }
        public bool Locked { get; set; }
        public static DraggableWord Selected { get; private set; }
        public static DraggableWord Active { get; private set; }
        Transform home, dragLayer;
        RectTransform rect;
        CanvasGroup group;
        Canvas canvas;
        bool dragging, returnOnEnable;
        int dragPointerId;
        Vector3 pointerOffset;

        public void Configure(string word, Transform bankHome, Transform layer)
        {
            Word = word; home = bankHome; dragLayer = layer;
            rect = (RectTransform)transform; group = GetComponent<CanvasGroup>();
            canvas = GetComponentInParent<Canvas>();
        }
        Camera PointerCamera(PointerEventData data)
        {
            if (data.pressEventCamera != null) return data.pressEventCamera;
            return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        }
        public void OnInitializePotentialDrag(PointerEventData data)
        {
            // InputSystemUIInputModule delivers mouse and individual finger pointers.
            data.useDragThreshold = false;
        }
        public void OnBeginDrag(PointerEventData data)
        {
            if (Locked || dragLayer == null || data.button != PointerEventData.InputButton.Left ||
                (Active != null && Active != this)) return;
            Vector3 point;
            if (!RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)dragLayer,
                data.position, PointerCamera(data), out point)) return;
            ClearSelection(); pointerOffset = rect.position - point;
            dragging = true; dragPointerId = data.pointerId; Active = this;
            if (Zone != null) Zone.Release(this);
            transform.SetParent(dragLayer, true); transform.SetAsLastSibling();
            transform.localScale = Vector3.one;
            group.blocksRaycasts = false; group.alpha = 0.9f;
            OnDrag(data);
        }
        public void OnDrag(PointerEventData data)
        {
            if (!dragging || data.pointerId != dragPointerId) return;
            Vector3 point;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)dragLayer,
                data.position, PointerCamera(data), out point)) rect.position = point + pointerOffset;
        }
        internal bool CanDrop(PointerEventData data) => dragging && data.pointerId == dragPointerId;
        public void OnEndDrag(PointerEventData data)
        {
            if (!dragging || data.pointerId != dragPointerId) return;
            dragging = false; group.alpha = 1; group.blocksRaycasts = true;
            if (Active == this) Active = null;
            if (Zone == null) ReturnToBank();
        }
        public void OnPointerClick(PointerEventData data)
        {
            if (Locked || dragging || Active != null) return;
            if (Selected == this) ClearSelection();
            else { ClearSelection(); Selected = this; transform.localScale = Vector3.one * 1.15f; }
        }
        internal static void ClearSelection()
        {
            if (Selected != null) Selected.transform.localScale = Vector3.one;
            Selected = null;
        }
        public void ReturnToBank()
        {
            if (Zone != null) Zone.Release(this);
            if (home == null) return;
            transform.SetParent(home, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero; rect.localScale = Vector3.one;
            if (Selected == this) ClearSelection();
        }
        void OnEnable()
        {
            if (!returnOnEnable) return;
            returnOnEnable = false; ReturnToBank();
        }
        void OnDisable()
        {
            // Keep placed words in their blanks. Defer an interrupted drag's return until
            // reopening, when Unity has finished disabling the hierarchy.
            if (dragging && Zone == null) returnOnEnable = true;
            dragging = false;
            if (Active == this) Active = null;
            if (Selected == this) ClearSelection();
            if (group != null) { group.blocksRaycasts = true; group.alpha = 1; }
        }
    }
}