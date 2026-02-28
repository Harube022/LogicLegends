using UnityEngine;
using UnityEngine.EventSystems;

public class MobileLookInput : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
{
    public float sensitivity = 1f;

    private Vector2 lastPosition;
    private bool isDragging;

    public static Vector2 LookDelta { get; private set; }

    public void OnPointerDown(PointerEventData eventData)
    {
        lastPosition = eventData.position;
        isDragging = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        Vector2 delta = eventData.position - lastPosition;
        lastPosition = eventData.position;

        LookDelta = delta * sensitivity;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isDragging = false;
        LookDelta = Vector2.zero;
    }

    private void LateUpdate()
    {
      
        LookDelta = Vector2.zero;
    }
}