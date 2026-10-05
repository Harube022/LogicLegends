// using UnityEngine;
// using UnityEngine.EventSystems;

// namespace LogicLegends.Inference
// {
//     [RequireComponent(typeof(UnityEngine.UI.Image))]
//     public sealed class DropZone : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
//     {
//         public DraggableWord PlacedWord { get; private set; }
//         public string Value => PlacedWord == null ? null : PlacedWord.Word;
//         public bool Locked { get; private set; }
//         UnityEngine.UI.Image background;
//         readonly Color normal = new Color(0.38f, 0.4f, 0.42f);
//         readonly Color hover = new Color(0.28f, 0.45f, 0.42f);
//         readonly Color solved = new Color(0.13f, 0.4f, 0.3f);

//         void Awake() { background = GetComponent<UnityEngine.UI.Image>(); background.color = normal; }
//         public void OnPointerEnter(PointerEventData data)
//         {
//             if (!Locked && DraggableWord.Active != null) background.color = hover;
//         }
//         public void OnPointerExit(PointerEventData data) { background.color = Locked ? solved : normal; }
//         public void OnDrop(PointerEventData data)
//         {
//             var word = data.pointerDrag == null ? null : data.pointerDrag.GetComponent<DraggableWord>();
//             Place(word);
//         }
//         public bool Place(DraggableWord word)
//         {
//             if (Locked || word == null || word.Locked) return false;
//             if (PlacedWord != null && PlacedWord != word) PlacedWord.ReturnToBank();
//             if (word.Zone != null) word.Zone.Release(word);
//             PlacedWord = word; word.Zone = this;
//             word.transform.SetParent(transform, false);
//             var rect = (RectTransform)word.transform;
//             rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
//             rect.anchoredPosition = Vector2.zero; rect.localScale = Vector3.one;
//             background.color = normal;
//             return true;
//         }
//         internal void Release(DraggableWord word)
//         {
//             if (PlacedWord == word) PlacedWord = null;
//             word.Zone = null;
//         }
//         public void Lock()
//         {
//             Locked = true; background.color = solved;
//             if (PlacedWord != null)
//             {
//                 PlacedWord.Locked = true;
//                 PlacedWord.GetComponent<UnityEngine.UI.Image>().color = solved;
//             }
//         }
//     }
// }


using UnityEngine;
using UnityEngine.EventSystems;

namespace LogicLegends.Inference
{
    [RequireComponent(typeof(UnityEngine.UI.Image))]
    public sealed class DropZone : MonoBehaviour, IDropHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public DraggableWord PlacedWord { get; private set; }
        public string Value => PlacedWord == null ? null : PlacedWord.Word;
        public bool Locked { get; private set; }
        UnityEngine.UI.Image background;
        readonly Color normal = new Color(0.38f, 0.4f, 0.42f);
        readonly Color hover = new Color(0.28f, 0.45f, 0.42f);
        readonly Color solved = new Color(0.13f, 0.4f, 0.3f);
        void Awake() { background = GetComponent<UnityEngine.UI.Image>(); background.color = normal; }
        public void OnPointerEnter(PointerEventData data)
        {
            if (!Locked && (DraggableWord.Active != null || DraggableWord.Selected != null)) background.color = hover;
        }
        public void OnPointerExit(PointerEventData data) { background.color = Locked ? solved : normal; }
        public void OnDrop(PointerEventData data)
        {
            var word = data.pointerDrag == null ? null : data.pointerDrag.GetComponent<DraggableWord>();
            if (word != null && word.CanDrop(data)) Place(word);
        }
        public void OnPointerClick(PointerEventData data)
        {
            var word = DraggableWord.Selected;
            if (word != null && Place(word)) DraggableWord.ClearSelection();
        }
        public bool Place(DraggableWord word)
        {
            if (Locked || word == null || word.Locked) return false;
            if (PlacedWord == word && word.Zone == this) return true;
            if (PlacedWord != null && PlacedWord != word) PlacedWord.ReturnToBank();
            if (word.Zone != null) word.Zone.Release(word);
            PlacedWord = word; word.Zone = this;
            word.transform.SetParent(transform, false);
            var rect = (RectTransform)word.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero; rect.localScale = Vector3.one;
            background.color = normal; return true;
        }
        internal void Release(DraggableWord word)
        {
            if (PlacedWord == word) PlacedWord = null;
            word.Zone = null;
        }
        public void Lock()
        {
            Locked = true; background.color = solved;
            if (PlacedWord != null)
            {
                PlacedWord.Locked = true;
                PlacedWord.GetComponent<UnityEngine.UI.Image>().color = solved;
            }
        }
    }
}