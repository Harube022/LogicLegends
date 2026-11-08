using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Applies preferences after stage-specific HUD layout, preserving its dynamic positioning.</summary>
[DefaultExecutionOrder(32010)]
public sealed class SoloTouchSettings : MonoBehaviour
{
    sealed class Node
    {
        public RectTransform rect;
        public CanvasGroup group;
        public Vector2 min, max, position, pivot, appliedMin, appliedMax, appliedPosition, appliedPivot;
        public Vector3 scale, appliedScale;
        public float alpha, appliedAlpha;
        public bool applied;
        public bool lookSurface;
    }
    readonly List<Node> nodes = new List<Node>();
    static SoloTouchSettings active;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { active = null; }
    void Awake() { active = this; }
    void Start() { Discover(); }
    public static void RegisterControl(RectTransform rect, bool lookSurface = false)
    { if (active != null) active.Add(rect, lookSurface); }
    void LateUpdate()
    {
        var data = SoloGameSettings.Current;
        foreach (var n in nodes)
        {
            if (n.rect == null) continue;
            var rect = n.rect;
            // A stage can update just one layout property. Keep the other
            // authored values rather than capturing our already-mirrored ones.
            if (!n.applied || rect.anchorMin != n.appliedMin) n.min = rect.anchorMin;
            if (!n.applied || rect.anchorMax != n.appliedMax) n.max = rect.anchorMax;
            if (!n.applied || rect.anchoredPosition != n.appliedPosition) n.position = rect.anchoredPosition;
            if (!n.applied || rect.pivot != n.appliedPivot) n.pivot = rect.pivot;
            if (!n.applied || rect.localScale != n.appliedScale) n.scale = rect.localScale;
            if (!n.applied || !Mathf.Approximately(n.group.alpha, n.appliedAlpha)) n.alpha = n.group.alpha;
            rect.anchorMin = data.leftHanded ? new Vector2(1 - n.max.x, n.min.y) : n.min;
            rect.anchorMax = data.leftHanded ? new Vector2(1 - n.min.x, n.max.y) : n.max;
            rect.pivot = data.leftHanded ? new Vector2(1 - n.pivot.x, n.pivot.y) : n.pivot;
            rect.anchoredPosition = data.leftHanded ? new Vector2(-n.position.x, n.position.y) : n.position;
            rect.localScale = n.scale * (n.lookSurface ? 1 : data.buttonSize);
            n.group.alpha = n.alpha * (n.lookSurface ? 1 : data.buttonOpacity);
            n.appliedMin = rect.anchorMin; n.appliedMax = rect.anchorMax; n.appliedPosition = rect.anchoredPosition;
            n.appliedScale = rect.localScale; n.appliedAlpha = n.group.alpha; n.appliedPivot = rect.pivot; n.applied = true;
        }
    }
    void Discover()
    {
        var roots = new List<GameObject>();
        var mobile = new List<MobileInputUI>(); var looks = new List<MobileLookInput>(); var buttons = new List<Button>();
        gameObject.scene.GetRootGameObjects(roots);
        foreach (var root in roots)
        {
            mobile.Clear(); looks.Clear(); buttons.Clear();
            root.GetComponentsInChildren(true, mobile); root.GetComponentsInChildren(true, looks); root.GetComponentsInChildren(true, buttons);
            foreach (var control in mobile) if (control.HasJoystick) Add(control.transform as RectTransform);
            foreach (var look in looks) Add(look.transform as RectTransform, true);
            foreach (var button in buttons)
            {
                string name = button.name.ToLowerInvariant().Replace("_", "");
                if (name == "jumpbutton" || name == "interactbutton" || name == "runbutton" || name == "jumpbtn" || name == "handbutton")
                    Add(button.transform as RectTransform);
            }
        }
    }
    void Add(RectTransform rect, bool lookSurface = false)
    {
        // MobileInputUI lives on Background, centered inside the actual
        // Joystick. Mirror and resize that outer control once, not its ring.
        if (!lookSurface && rect != null)
            for (Transform parent = rect; parent != null && !(parent.GetComponent<Canvas>() != null); parent = parent.parent)
                if (parent.name == "Joystick") { rect = parent as RectTransform; break; }
        if (rect == null || rect.gameObject.scene != gameObject.scene || nodes.Exists(n => n.rect == rect)) return;
        var group = rect.GetComponent<CanvasGroup>(); if (group == null) group = rect.gameObject.AddComponent<CanvasGroup>();
        nodes.Add(new Node { rect = rect, group = group, lookSurface = lookSurface });
    }
    void OnDestroy()
    {
        if (active == this) active = null;
        foreach (var n in nodes)
        {
            if (n.rect == null || !n.applied) continue;
            n.rect.anchorMin = n.min; n.rect.anchorMax = n.max; n.rect.pivot = n.pivot;
            n.rect.anchoredPosition = n.position; n.rect.localScale = n.scale;
            if (n.group != null) n.group.alpha = n.alpha;
        }
    }
}
