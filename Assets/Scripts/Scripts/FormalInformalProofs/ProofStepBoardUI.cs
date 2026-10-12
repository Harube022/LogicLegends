using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Tap a tablet, then a numbered slot. A slot can always be replaced or cleared.</summary>
public sealed class ProofStepBoardUI : MonoBehaviour
{
    private ProofChallengeData challenge;
    private int[] assignedSteps = new int[0];
    private int[] assignedRules = new int[0];
    private int selected = -1;
    private bool selectingRule;
    private bool clearMode;
    private TMP_Text heading;
    private TMP_Text context;
    private readonly List<Button> stepOptions = new List<Button>();
    private readonly List<Button> ruleOptions = new List<Button>();
    private readonly List<int> stepOptionIds = new List<int>();
    private readonly List<int> ruleOptionIds = new List<int>();
    private readonly List<Button> stepSlots = new List<Button>();
    private readonly List<Button> ruleSlots = new List<Button>();
    private Transform content;
    public Action OnClosed;
    public bool IsOpen => gameObject.activeSelf;

    public int AssignedStep(int slot) { return assignedSteps[slot]; }
    public int AssignedRule(int slot) { return assignedRules[slot]; }

    public void Build(Transform hud)
    {
        transform.SetParent(hud, false);
        var rect = (RectTransform)transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.sizeDelta = new Vector2(1700, 920);
        var image = gameObject.AddComponent<Image>();
        image.color = new Color(.045f, .075f, .13f, .98f);
        heading = Text("Board Heading", transform, new Vector2(0, 390), new Vector2(1560, 70), 43, Color.white);
        context = Text("Proof Context", transform, new Vector2(0, 315), new Vector2(1550, 80), 27, new Color(.95f, .84f, .64f));
        Button close = Button("Close Board", transform, "CLOSE BOARD", new Vector2(680, -395), new Vector2(250, 62), 29);
        close.onClick.AddListener(Close);
        Button clear = Button("Clear Slot Tool", transform, "CLEAR SLOT", new Vector2(390, -395), new Vector2(250, 62), 29);
        clear.onClick.AddListener(() => { selected = -1; clearMode = true; Refresh(); });
        content = new GameObject("Tablet and Slot Content", typeof(RectTransform)).transform;
        content.SetParent(transform, false);
        gameObject.SetActive(false);
    }

    public void Configure(ProofChallengeData data)
    {
        challenge = data;
        selected = -1;
        selectingRule = false;
        clearMode = false;
        foreach (Transform child in content) Destroy(child.gameObject);
        stepOptions.Clear(); ruleOptions.Clear(); stepOptionIds.Clear(); ruleOptionIds.Clear();
        stepSlots.Clear(); ruleSlots.Clear();
        heading.text = data.title.ToUpperInvariant();
        context.text = data.context + "\n" + data.instructions;
        int count = data.kind == ProofChallengeKind.MissingRule ? 1 : data.steps.Length;
        assignedSteps = new int[count];
        assignedRules = new int[count];
        for (int i = 0; i < count; i++) { assignedSteps[i] = -1; assignedRules[i] = -1; }
        bool hasStatements = data.kind != ProofChallengeKind.MissingRule;
        bool hasRules = data.kind != ProofChallengeKind.OrderSteps;
        if (hasStatements)
        {
            Text("Statement Header", content, new Vector2(-520, 235), new Vector2(570, 50), 30, Color.white).text =
                data.kind == ProofChallengeKind.OrderSteps ? "SHUFFLED STEP TABLETS" : "STATEMENT TABLETS";
            for (int display = 0; display < data.steps.Length; display++)
            {
                int i = data.steps.Length == 5 ? new[] { 2, 4, 0, 3, 1 }[display] : display;
                int id = i;
                string label = data.kind == ProofChallengeKind.OrderSteps ?
                    data.steps[i].statement + " — " + data.steps[i].justification : data.steps[i].statement;
                Button b = Button("Step Tablet " + i, content, label,
                    new Vector2(-520, 175 - display * 83), new Vector2(550, 72), 25);
                b.onClick.AddListener(() => { selected = id; selectingRule = false; clearMode = false; Refresh(); });
                stepOptions.Add(b);
                stepOptionIds.Add(id);
            }
        }
        if (hasRules)
        {
            string[] options = data.kind == ProofChallengeKind.MissingRule ? data.ruleOptions : RuleLabels(data.steps);
            float x = data.kind == ProofChallengeKind.MissingRule ? -480 : -35;
            Text("Rule Header", content, new Vector2(x, 235), new Vector2(510, 50), 30, Color.white).text = "JUSTIFICATION TABLETS";
            for (int display = 0; display < options.Length; display++)
            {
                int i = data.kind == ProofChallengeKind.Formalize && options.Length == 5 ?
                    new[] { 3, 1, 4, 0, 2 }[display] : display;
                int id = i;
                Button b = Button("Rule Tablet " + i, content, options[i],
                    new Vector2(x, 175 - display * 83), new Vector2(480, 72), 25);
                b.onClick.AddListener(() => { selected = id; selectingRule = true; clearMode = false; Refresh(); });
                ruleOptions.Add(b);
                ruleOptionIds.Add(id);
            }
        }
        Text("Slot Header", content, new Vector2(550, 235), new Vector2(550, 50), 30, Color.white).text = "NUMBERED PROOF SLOTS";
        for (int i = 0; i < count; i++)
        {
            int slot = i;
            if (hasStatements)
            {
                Button b = Button("Step Slot " + i, content, "", new Vector2(420, 175 - i * 83),
                    new Vector2(hasRules ? 305 : 570, 72), 23);
                b.onClick.AddListener(() => Assign(slot, false));
                stepSlots.Add(b);
            }
            if (hasRules)
            {
                Button b = Button("Rule Slot " + i, content, "", new Vector2(hasStatements ? 705 : 550, 175 - i * 83),
                    new Vector2(hasStatements ? 245 : 570, 72), 23);
                b.onClick.AddListener(() => Assign(slot, true));
                ruleSlots.Add(b);
            }
        }
        Refresh();
    }

    private static string[] RuleLabels(ProofStepData[] steps)
    {
        string[] labels = new string[steps.Length];
        for (int i = 0; i < labels.Length; i++) labels[i] = steps[i].justification + " (" + steps[i].statement + ")";
        return labels;
    }

    public void Open() { gameObject.SetActive(true); }
    public void Close() { gameObject.SetActive(false); OnClosed?.Invoke(); }

    private void Assign(int slot, bool rule)
    {
        if (challenge == null || (rule && challenge.kind == ProofChallengeKind.OrderSteps) ||
            (!rule && challenge.kind == ProofChallengeKind.MissingRule)) return;
        int[] target = rule ? assignedRules : assignedSteps;
        if (clearMode || selected < 0) target[slot] = -1;
        else if (selectingRule == rule)
        {
            for (int i = 0; i < target.Length; i++) if (target[i] == selected) target[i] = -1;
            target[slot] = selected;
            selected = -1;
        }
        Refresh();
    }

    public bool IsComplete()
    {
        if (challenge == null) return false;
        for (int i = 0; i < assignedSteps.Length; i++)
        {
            if (challenge.kind != ProofChallengeKind.MissingRule && assignedSteps[i] < 0) return false;
            if (challenge.kind != ProofChallengeKind.OrderSteps && assignedRules[i] < 0) return false;
        }
        return true;
    }

    public bool IsCorrect()
    {
        if (!IsComplete()) return false;
        if (challenge.kind == ProofChallengeKind.MissingRule)
            return challenge.ruleOptions[assignedRules[0]] == challenge.expectedRule;
        bool acceptable = false;
        foreach (ProofOrderData order in challenge.acceptableOrders)
        {
            if (order.stepIds.Length != assignedSteps.Length) continue;
            bool same = true;
            for (int i = 0; i < assignedSteps.Length; i++) if (order.stepIds[i] != assignedSteps[i]) same = false;
            if (same) { acceptable = true; break; }
        }
        if (!acceptable) return false;
        for (int i = 0; i < assignedSteps.Length; i++)
        {
            int id = assignedSteps[i];
            if (id < 0 || id >= challenge.steps.Length) return false;
            foreach (int prior in challenge.steps[id].prerequisiteIds)
            {
                bool found = false;
                for (int j = 0; j < i; j++) if (assignedSteps[j] == prior) found = true;
                if (!found) return false;
            }
            if (challenge.kind == ProofChallengeKind.Formalize && assignedRules[i] != id) return false;
        }
        return assignedSteps[assignedSteps.Length - 1] == challenge.steps.Length - 1;
    }

    private void Refresh()
    {
        if (challenge == null) return;
        for (int i = 0; i < stepOptions.Count; i++) stepOptions[i].GetComponent<Image>().color =
            selected == stepOptionIds[i] && !selectingRule && !clearMode ? new Color(.12f, .55f, .84f) : new Color(.19f, .31f, .45f);
        for (int i = 0; i < ruleOptions.Count; i++) ruleOptions[i].GetComponent<Image>().color =
            selected == ruleOptionIds[i] && selectingRule && !clearMode ? new Color(.12f, .55f, .84f) : new Color(.35f, .24f, .24f);
        for (int i = 0; i < stepSlots.Count; i++)
        {
            int id = assignedSteps[i];
            stepSlots[i].GetComponentInChildren<TMP_Text>().text = (i + 1) + ". " + (id < 0 ? "[CHOOSE STEP]" : challenge.steps[id].statement);
        }
        for (int i = 0; i < ruleSlots.Count; i++)
        {
            int id = assignedRules[i];
            string value = id < 0 ? "[CHOOSE RULE]" : challenge.kind == ProofChallengeKind.MissingRule ?
                challenge.ruleOptions[id] : challenge.steps[id].justification;
            ruleSlots[i].GetComponentInChildren<TMP_Text>().text = (challenge.kind == ProofChallengeKind.MissingRule ? "3. " : "") + value;
        }
    }

    private static TMP_Text Text(string name, Transform parent, Vector2 pos, Vector2 size, int font, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        var text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = font;
        text.enableAutoSizing = true;
        text.fontSizeMin = font * .7f;
        text.fontSizeMax = font;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private static Button Button(string name, Transform parent, string label, Vector2 pos, Vector2 size, int font)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        go.GetComponent<Image>().color = new Color(.19f, .31f, .45f);
        var t = Text("Label", go.transform, Vector2.zero, size - new Vector2(12, 8), font, Color.white);
        t.text = label;
        return go.GetComponent<Button>();
    }
}
