using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using LogicLegends.Inference;
namespace LogicLegends.RulesOfInferenceUI
{
    public sealed class InferenceTilePresentation : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public DropZone zone;
        public DraggableWord word;
        bool hover, invalid; string invalidValue;
        public void MarkInvalid(bool value) { invalid=value; invalidValue=zone==null?null:zone.Value; }
        public void OnPointerEnter(PointerEventData e) { hover=true; }
        public void OnPointerExit(PointerEventData e) { hover=false; }
        void LateUpdate()
        {
            if(invalid && zone!=null && zone.Value!=invalidValue) invalid=false;
            var face=GetComponent<Image>(); var rim=GetComponent<Outline>();
            bool locked=zone!=null ? zone.Locked : word!=null && word.Locked;
            bool selected=word!=null && (DraggableWord.Active==word || DraggableWord.Selected==word);
            bool target=zone!=null && hover && (DraggableWord.Active!=null || DraggableWord.Selected!=null);
            face.color=invalid ? new Color(.32f,.16f,.16f) : locked ? new Color(.13f,.4f,.3f) : target || selected ? new Color(.22f,.40f,.41f)
                : zone!=null ? new Color(.12f,.20f,.23f) : new Color(.16f,.31f,.33f);
            rim.effectColor=invalid ? new Color(1,.72f,.57f) : locked ? new Color(.55f,1,.78f) : target || selected || word!=null ? new Color(.89f,.76f,.46f) : new Color(.60f,.72f,.74f);
            if(zone!=null)
            {
                var placeholder=zone.GetComponentsInChildren<TMP_Text>().FirstOrDefault(t=>t.transform.parent==zone.transform);
                if(placeholder!=null) placeholder.enabled=zone.PlacedWord==null;
            }
        }
    }

}




