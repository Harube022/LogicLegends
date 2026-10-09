using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Label feedback keeps the source wooden rim and stable touch target intact.</summary>
public sealed class StageButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    private Button button;
    private TMP_Text[] labels;
    private bool hovered, pressed, selected;
    private void Awake() { button = GetComponent<Button>(); labels = GetComponentsInChildren<TMP_Text>(true); }
    private void OnEnable() { button = GetComponent<Button>(); labels = GetComponentsInChildren<TMP_Text>(true); }
    private void OnDisable() { hovered = pressed = selected = false; }
    public void OnPointerEnter(PointerEventData e) { hovered = true; }
    public void OnPointerExit(PointerEventData e) { hovered = pressed = false; }
    public void OnPointerDown(PointerEventData e) { pressed = true; }
    public void OnPointerUp(PointerEventData e) { pressed = false; }
    public void OnSelect(BaseEventData e) { selected = true; }
    public void OnDeselect(BaseEventData e) { selected = false; }
    private void LateUpdate()
    {
        if (button == null) return;
        foreach (var label in labels)
        {
            label.color = !button.interactable ? new Color(0.60f, 0.72f, 0.74f)
                : pressed ? new Color(0.80f, 0.88f, 0.85f) : hovered || selected ? StageUITheme.Gold : StageUITheme.Ink;
        }
    }
}
