using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>Updates reading styles when text or preferences change, without recurring scene searches.</summary>
[DefaultExecutionOrder(32020)]
public sealed class SoloAccessibility : MonoBehaviour
{
    sealed class Label
    {
        public TMP_Text text;
        public float min, max, size, appliedMin, appliedMax;
        public Color color, appliedColor;
        public string source, appliedText;
        public bool initialized, feedback, reading;
    }
    readonly Dictionary<TMP_Text, Label> labels = new Dictionary<TMP_Text, Label>();
    readonly HashSet<TMP_Text> known = new HashSet<TMP_Text>();
    readonly HashSet<TMP_Text> pending = new HashSet<TMP_Text>();
    readonly List<TMP_Text> work = new List<TMP_Text>();
    void OnEnable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(TextChanged);
        SoloGameSettings.Changed += PreferencesChanged;
    }
    void Start()
    {
        var roots = new List<GameObject>(); var texts = new List<TMP_Text>();
        gameObject.scene.GetRootGameObjects(roots);
        foreach (var root in roots)
        {
            texts.Clear(); root.GetComponentsInChildren(true, texts);
            foreach (var text in texts) Register(text);
        }
    }
    void OnDisable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(TextChanged);
        SoloGameSettings.Changed -= PreferencesChanged;
    }
    void TextChanged(Object changed)
    {
        if (!(changed is TMP_Text text) || text.gameObject.scene != gameObject.scene) return;
        Register(text); if (labels.ContainsKey(text)) pending.Add(text);
    }
    void Register(TMP_Text text)
    {
        if (text == null || text.GetComponentInParent<GameSettingsPanel>(true) != null || !known.Add(text)) return;
        string name = text.name.ToLowerInvariant();
        bool reading = name.Contains("body") || name.Contains("summary") || name.Contains("question") || name.Contains("prompt") || name.Contains("hint") || name.Contains("description");
        bool feedback = name.Contains("feedback");
        if (!reading && !feedback) return;
        labels.Add(text, new Label { text = text, reading = reading, feedback = feedback }); pending.Add(text);
    }
    void PreferencesChanged() { foreach (var text in labels.Keys) if (text != null) pending.Add(text); }
    void LateUpdate()
    {
        if (pending.Count == 0) return;
        work.Clear(); work.AddRange(pending); pending.Clear();
        var data = SoloGameSettings.Current;
        foreach (var text in work)
        {
            if (text == null || !text.gameObject.activeInHierarchy || !labels.TryGetValue(text, out var l)) continue;
            var t = l.text;
            if (!l.initialized || !Mathf.Approximately(t.fontSizeMin, l.appliedMin) || !Mathf.Approximately(t.fontSizeMax, l.appliedMax))
            { l.min = t.fontSizeMin; l.max = t.enableAutoSizing ? t.fontSizeMax : t.fontSize; l.size = t.fontSize; }
            if (!l.initialized || t.color != l.appliedColor) l.color = t.color;
            if (l.reading)
            {
                t.fontSizeMin = Mathf.Min(l.min, l.max * data.textSize); t.fontSizeMax = l.max * data.textSize;
                if (!t.enableAutoSizing) t.fontSize = l.size * data.textSize;
                if (data.highContrast)
                {
                    float lightness = l.color.r * .2126f + l.color.g * .7152f + l.color.b * .0722f;
                    t.color = lightness > .55f ? new Color(1, .98f, .93f, l.color.a) : new Color(.10f, .065f, .035f, l.color.a);
                }
                else t.color = l.color;
            }
            if (l.feedback)
            {
                if (!l.initialized || t.text != l.appliedText) l.source = t.text;
                Color.RGBToHSV(l.color, out float h, out float s, out float v);
                string cue = s > .35f && v > .3f ? h > .20f && h < .48f ? "Correct: " : h < .10f || h > .93f ? "Check: " : "" : "";
                string value = data.colorIndependentCues && !string.IsNullOrEmpty(l.source) && !l.source.StartsWith(cue) ? cue + l.source : l.source;
                if (t.text != value) t.text = value;
            }
            l.appliedMin = t.fontSizeMin; l.appliedMax = t.fontSizeMax; l.appliedColor = t.color; l.appliedText = t.text; l.initialized = true;
        }
    }
}
