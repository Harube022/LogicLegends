using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class MobileLookInput : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
{
    public float sensitivity = 1f;

    private Vector2 lastPosition;
    private bool isDragging;
    private int pointer;
    private static readonly HashSet<MobileLookInput> active = new HashSet<MobileLookInput>();

    public static Vector2 LookDelta { get; private set; }

    // Add these inside MobileLookInput.cs
    private void Awake()
    {
        LookDelta = Vector2.zero;
    }

    private void OnDisable()
    {
        active.Remove(this);
        // Clears any stuck drag states when the script is destroyed or disabled during a scene change
        isDragging = false;
        LookDelta = Vector2.zero;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!GameplayTouchRouting.CameraInputAllowed || isDragging && pointer != eventData.pointerId) return;
        pointer = eventData.pointerId;
        lastPosition = eventData.position;
        isDragging = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || pointer != eventData.pointerId) return;
        if (!GameplayTouchRouting.CameraInputAllowed) { CancelDrag(); return; }

        // 1. Only count active touches that are on the RIGHT HALF of the screen
        int rightSideTouches = GameplayTouchRouting.CameraTouchCount;

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
        if (!isDragging || pointer != eventData.pointerId) return;
        isDragging = false;
        LookDelta = Vector2.zero;
    }

    // Call this after the camera updates to clear input for the next frame
    public static void ResetDelta()
    {
        LookDelta = Vector2.zero;
    }
    private void OnEnable() { active.Add(this); CancelDrag(); SoloTouchSettings.RegisterControl(transform as RectTransform, true); }
    private void Update() { if (!GameplayTouchRouting.CameraInputAllowed) { CancelDrag(); GameplayTouchRouting.Reset(); } }
    private void OnApplicationFocus(bool focus) { if (!focus) { CancelDrag(); GameplayTouchRouting.Reset(); } }
    private void OnApplicationPause(bool paused) { if (paused) { CancelDrag(); GameplayTouchRouting.Reset(); } }
    private void CancelDrag() { isDragging = false; LookDelta = Vector2.zero; }
    public static void CancelAll()
    { foreach (var input in active) if (input != null) input.CancelDrag(); GameplayTouchRouting.Reset(); }

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
