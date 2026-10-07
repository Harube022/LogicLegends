using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using LogicLegends.Inference;
namespace LogicLegends.RulesOfInferenceUI
{
    public sealed class InferenceButtonPresentation : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public Button button;
        public Image face;
        public TMP_Text label;
        bool hovered,pressed;
        public void OnPointerEnter(PointerEventData e) { hovered=true; }
        public void OnPointerExit(PointerEventData e) { hovered=false; pressed=false; }
        public void OnPointerDown(PointerEventData e) { pressed=true; }
        public void OnPointerUp(PointerEventData e) { pressed=false; }
        void OnDisable() { hovered=pressed=false; }
        void LateUpdate()
        {
            face.color=!button.interactable ? new Color(.035f,.085f,.11f,.6f) : pressed ? new Color(0,0,0,.25f)
                : hovered ? new Color(.89f,.76f,.46f,.12f) : Color.clear;
            label.color=button.interactable ? new Color(.91f,.95f,.93f) : new Color(.60f,.72f,.74f);
        }
    }
}

