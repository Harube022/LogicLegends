using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

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

        // 1. Only count active touches that are on the RIGHT HALF of the screen
        int rightSideTouches = 0;
        if (Touchscreen.current != null)
        {
            foreach (var touch in Touchscreen.current.touches)
            {
                if (touch.isInProgress && touch.position.ReadValue().x > Screen.width / 2f)
                {
                    rightSideTouches++;
                }
            }
        }

        // 2. If the player is using 2 or more fingers on the Look Area, pause camera rotation
        if (rightSideTouches >= 2)
        {
            LookDelta = Vector2.zero;
            lastPosition = eventData.position; 
            return;
        }

        Vector2 delta = eventData.position - lastPosition;
        lastPosition = eventData.position;

        LookDelta = delta * sensitivity;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isDragging = false;
        LookDelta = Vector2.zero;
    }

    // Call this after the camera updates to clear input for the next frame
    public static void ResetDelta()
    {
        LookDelta = Vector2.zero;
    }

    // private void LateUpdate()
    // {
    //     LookDelta = Vector2.zero;
    // }
}
// using UnityEngine;
// using UnityEngine.EventSystems;

// public class MobileLookInput : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
// {
//     public float sensitivity = 1f;

//     private Vector2 lastPosition;
//     private bool isDragging;

//     public static Vector2 LookDelta { get; private set; }

//     public void OnPointerDown(PointerEventData eventData)
//     {
//         lastPosition = eventData.position;
//         isDragging = true;
//     }

//     public void OnDrag(PointerEventData eventData)
//     {
//         if (!isDragging) return;

//         Vector2 delta = eventData.position - lastPosition;
//         lastPosition = eventData.position;

//         LookDelta = delta * sensitivity;
//     }

//     public void OnPointerUp(PointerEventData eventData)
//     {
//         isDragging = false;
//         LookDelta = Vector2.zero;
//     }

//     private void LateUpdate()
//     {
      
//         LookDelta = Vector2.zero;
//     }
// }