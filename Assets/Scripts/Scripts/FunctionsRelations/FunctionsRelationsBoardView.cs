using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

/// <summary>An explicit, reversible InformationBoard camera view using the existing gameplay camera.</summary>
[DisallowMultipleComponent]
public sealed class FunctionsRelationsBoardView : MonoBehaviour
{
    public Transform informationBoard;
    public TMP_Text boardInstructions;
    public RectTransform viewSafeArea;
    public Transform connectionBeams;
    public Button backButton;
    [Min(.1f)] public float transitionSeconds = .8f;
    [Range(25f, 65f)] public float boardFieldOfView = 42f;
    [Min(1f)] public float framingPadding = 1.04f;
    public bool Viewing { get; private set; }
    public bool Transitioning { get; private set; }
    public Bounds BoardBounds { get; private set; }
    private Camera view;
    private Vector3 savedPosition;
    private Quaternion savedRotation;
    private float savedFov, framedAspect;
    private Rect framedSafeArea;
    private Coroutine blend;
    private bool returning;
    private Action closed;
    private CursorLockMode savedCursor;
    private bool savedCursorVisible;
    private readonly List<Behaviour> suspended = new List<Behaviour>();
    private readonly Dictionary<Renderer, bool> avatarVisibility = new Dictionary<Renderer, bool>();
    private readonly Vector3[] safeCorners = new Vector3[4];

    private void Start()
    {
        if (backButton != null) { backButton.onClick.AddListener(Back); backButton.gameObject.SetActive(false); }
    }

    public bool OpenView(Action onClosed)
    {
        if (Viewing || informationBoard == null || boardInstructions == null || Camera.main == null) return false;
        view = Camera.main; Viewing = true; returning = false; closed = onClosed;
        savedPosition = view.transform.position; savedRotation = view.transform.rotation; savedFov = view.fieldOfView;
        savedCursor = Cursor.lockState; savedCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        var player = Player.LocalInstance;
        foreach (var behaviour in FindObjectsByType<Behaviour>(FindObjectsSortMode.None))
        {
            if (behaviour is CameraTargetController && behaviour.GetComponentInParent<Player>() != player) continue;
            if (behaviour.enabled && (behaviour == view.GetComponent<CinemachineBrain>() || behaviour is CameraTargetController ||
                behaviour is CinemachineInputAxisController || behaviour is CinemachinePinchZoom || behaviour is ThirdPersonCameraController))
            { suspended.Add(behaviour); behaviour.enabled = false; }
        }
        if (player != null)
            foreach (var renderer in player.GetComponentsInChildren<Renderer>(true))
            { avatarVisibility[renderer] = renderer.enabled; renderer.enabled = false; }
        // Foreground lasers can cross the close-up. Hide their renderers without editing the relation.
        if (connectionBeams != null)
            foreach (var renderer in connectionBeams.GetComponentsInChildren<Renderer>(true))
            { avatarVisibility[renderer] = renderer.enabled; renderer.enabled = false; }
        if (backButton != null) backButton.gameObject.SetActive(true);
        FrameBoard();
        return true;
    }

    private void LateUpdate()
    {
        if (Viewing && !returning && view != null && !Transitioning &&
            (Mathf.Abs(framedAspect - view.aspect) > .001f || framedSafeArea != CurrentSafeArea())) FrameBoard();
    }

    private void FrameBoard()
    {
        var renderers = informationBoard.GetComponentsInChildren<Renderer>();
        bool found = false;
        Bounds bounds = new Bounds(informationBoard.position, Vector3.zero);
        foreach (var renderer in renderers)
        {
            if (!renderer.enabled || renderer.GetComponent<TMP_Text>() != null || renderer.GetComponent<TMP_SubMesh>() != null) continue;
            if (!found) { bounds = renderer.bounds; found = true; } else bounds.Encapsulate(renderer.bounds);
        }
        // Match the reference close-up: fit the complete reading frame rather than its tall support base.
        foreach (var renderer in renderers)
            if (renderer.name == "Info_GoldFrame") { bounds = renderer.bounds; found = true; break; }
        if (!found) bounds = boardInstructions.bounds;
        BoardBounds = bounds;
        Vector3 forward = boardInstructions.transform.forward.normalized;
        Vector3 up = informationBoard.up.normalized, right = Vector3.Cross(up, forward).normalized;
        float height = ProjectExtent(bounds.extents, up), width = ProjectExtent(bounds.extents, right);
        float depth = ProjectExtent(bounds.extents, forward);
        float tangent = Mathf.Tan(boardFieldOfView * Mathf.Deg2Rad * .5f);
        // Back sits beside the board; fit its full reading frame inside the mobile safe area.
        framedAspect = view.aspect; framedSafeArea = CurrentSafeArea();
        // Game view resizing can report the previous safe rectangle for several frames.
        var safe = Rect.MinMaxRect(Mathf.Clamp(framedSafeArea.xMin, 0f, Screen.width), Mathf.Clamp(framedSafeArea.yMin, 0f, Screen.height),
            Mathf.Clamp(framedSafeArea.xMax, 0f, Screen.width), Mathf.Clamp(framedSafeArea.yMax, 0f, Screen.height));
        if (safe.width < 1f || safe.height < 1f) safe = new Rect(0f, 0f, Screen.width, Screen.height);
        float safeHeight = Mathf.Clamp(safe.height / Mathf.Max(1f, Screen.height), .5f, 1f);
        float safeWidth = Mathf.Clamp(safe.width / Mathf.Max(1f, Screen.width), .5f, 1f);
        float distance = Mathf.Max(height / (safeHeight * .88f), width / (framedAspect * safeWidth * .9f)) / tangent * framingPadding + depth;
        float centerY = safe.center.y / Mathf.Max(1f, Screen.height) - .5f;
        float centerX = safe.center.x / Mathf.Max(1f, Screen.width) - .5f;
        Vector3 position = bounds.center - forward * distance - up * (distance * tangent * 2f * centerY) -
            right * (distance * tangent * framedAspect * 2f * centerX);
        StartBlend(position, Quaternion.LookRotation(forward, up), boardFieldOfView, false);
    }

    private static float ProjectExtent(Vector3 extent, Vector3 axis) =>
        Vector3.Dot(extent, new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z)));

    private Rect CurrentSafeArea()
    {
        if (viewSafeArea == null) return Screen.safeArea;
        viewSafeArea.GetWorldCorners(safeCorners);
        var min = RectTransformUtility.WorldToScreenPoint(null, safeCorners[0]);
        var max = RectTransformUtility.WorldToScreenPoint(null, safeCorners[2]);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    public void Back()
    {
        if (!Viewing || returning) return;
        returning = true;
        if (backButton != null) backButton.interactable = false;
        StartBlend(savedPosition, savedRotation, savedFov, true);
    }

    private void StartBlend(Vector3 position, Quaternion rotation, float fov, bool finish)
    {
        if (blend != null) StopCoroutine(blend);
        blend = StartCoroutine(Blend(position, rotation, fov, finish));
    }

    private IEnumerator Blend(Vector3 position, Quaternion rotation, float fov, bool finish)
    {
        Transitioning = true;
        var start = view.transform.position; var startRotation = view.transform.rotation; float startFov = view.fieldOfView;
        for (float elapsed = 0f; elapsed < transitionSeconds; elapsed += Time.unscaledDeltaTime)
        {
            if (view == null) { RestoreImmediately(); yield break; }
            float t = Mathf.SmoothStep(0f, 1f, elapsed / transitionSeconds);
            view.transform.SetPositionAndRotation(Vector3.Lerp(start, position, t), Quaternion.Slerp(startRotation, rotation, t));
            view.fieldOfView = Mathf.Lerp(startFov, fov, t); yield return null;
        }
        view.transform.SetPositionAndRotation(position, rotation); view.fieldOfView = fov;
        blend = null; Transitioning = false;
        if (finish) RestoreImmediately();
    }

    public void RestoreImmediately(bool notify = true)
    {
        if (!Viewing) return;
        if (blend != null) StopCoroutine(blend);
        blend = null; Viewing = false; Transitioning = false; returning = false;
        if (view != null) { view.transform.SetPositionAndRotation(savedPosition, savedRotation); view.fieldOfView = savedFov; }
        foreach (var pair in avatarVisibility) if (pair.Key != null) pair.Key.enabled = pair.Value;
        avatarVisibility.Clear();
        foreach (var behaviour in suspended)
            if (behaviour != null) { behaviour.enabled = true; if (behaviour is CinemachineBrain brain) brain.ResetState(); }
        suspended.Clear();
        Cursor.lockState = savedCursor; Cursor.visible = savedCursorVisible;
        if (backButton != null) { backButton.gameObject.SetActive(false); backButton.interactable = true; }
        var callback = closed; closed = null; if (notify) callback?.Invoke();
    }

    private void OnDisable() => RestoreImmediately();
}
