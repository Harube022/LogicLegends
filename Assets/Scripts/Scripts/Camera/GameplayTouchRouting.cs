using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;
using Unity.Cinemachine;

/// <summary>Camera gestures belong to the look surface, never to buttons or modal UI.</summary>
public static class GameplayTouchRouting
{
    static readonly Dictionary<int, bool> starts = new Dictionary<int, bool>();
    static readonly List<int> ended = new List<int>();
    static readonly List<RaycastResult> hits = new List<RaycastResult>();
    static readonly List<TouchControl> touches = new List<TouchControl>();
    static int frame = -1;
    public static bool CameraInputAllowed => Time.timeScale > 0 &&
        (GameInput.Instance == null || !GameInput.Instance.GameplayInputBlocked) &&
        (Player.LocalInstance == null || Player.LocalInstance.enabled && !Player.LocalInstance.IsMovementLocked) &&
        !PrelimTutorialCameraRig.HasActiveView && IsPlayerView();
    static bool IsPlayerView()
    {
        var player = Player.LocalInstance;
        if (player == null || Camera.main == null) return true;
        var brain = Camera.main.GetComponent<CinemachineBrain>();
        var camera = brain != null && brain.enabled ? brain.ActiveVirtualCamera as CinemachineVirtualCameraBase : null;
        if (camera == null) return true;
        var target = camera.Follow;
        return target != null && (target == player.transform || target.IsChildOf(player.transform));
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset() { starts.Clear(); touches.Clear(); frame = -1; }
    public static bool IsLookSurface(Vector2 position)
    {
        if (EventSystem.current == null) return true;
        hits.Clear();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
        if (hits.Count == 0) return true;
        var target = hits[0].gameObject;
        if (target.GetComponentInParent<Selectable>() != null || target.GetComponentInParent<ScrollRect>() != null ||
            target.GetComponentInParent<MobileInputUI>() != null) return false;
        return target.GetComponentInParent<MobileLookInput>() != null;
    }
    static void ReadTouches()
    {
        if (frame == Time.frameCount) return;
        frame = Time.frameCount; touches.Clear();
        if (!CameraInputAllowed || Touchscreen.current == null) { starts.Clear(); return; }
        ended.Clear(); foreach (int id in starts.Keys) ended.Add(id);
        foreach (var touch in Touchscreen.current.touches)
        {
            int id = touch.touchId.ReadValue();
            if (!touch.isInProgress) continue;
            ended.Remove(id);
            if (touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began)
                starts[id] = touch.position.ReadValue().x > Screen.width * .5f && IsLookSurface(touch.position.ReadValue());
            if (starts.TryGetValue(id, out bool cameraTouch) && cameraTouch) touches.Add(touch);
        }
        foreach (int id in ended) starts.Remove(id);
    }
    public static int CameraTouchCount { get { ReadTouches(); return touches.Count; } }
    public static bool TryPinch(out TouchControl first, out TouchControl second)
    {
        ReadTouches(); first = touches.Count > 0 ? touches[0] : null; second = touches.Count > 1 ? touches[1] : null;
        // Initial touch positions are not movement deltas: avoid a first-frame zoom jump.
        return first != null && second != null && first.phase.ReadValue() != UnityEngine.InputSystem.TouchPhase.Began &&
            second.phase.ReadValue() != UnityEngine.InputSystem.TouchPhase.Began;
    }
}
