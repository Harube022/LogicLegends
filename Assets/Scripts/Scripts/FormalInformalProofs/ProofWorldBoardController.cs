using TMPro;
using UnityEngine;

/// <summary>World-space tablet selection and numbered proof slots for challenges 3–5.</summary>
[DisallowMultipleComponent]
public sealed class ProofWorldBoardController : MonoBehaviour
{
    [SerializeField] private ProofWorldPedestal[] statementTablets = new ProofWorldPedestal[0];
    [SerializeField] private ProofWorldPedestal[] ruleTablets = new ProofWorldPedestal[0];
    [SerializeField] private ProofWorldPedestal[] statementSlots = new ProofWorldPedestal[0];
    [SerializeField] private ProofWorldPedestal[] ruleSlots = new ProofWorldPedestal[0];

    private readonly int[] optionToStep = new int[5];
    private readonly int[] optionToRule = new int[5];
    private int[] assignedSteps = new int[0];
    private int[] assignedRules = new int[0];
    private ProofChallengeData challenge;
    private int selected = -1;
    private bool selectedRule;
    private int feedback; // 0 = normal/blue, 1 = all green, -1 = all red.

    public int AssignedStep(int slot) => assignedSteps[slot];
    public int AssignedRule(int slot) => assignedRules[slot];
    public bool IsConfigured => challenge != null;

    private void Awake() { Hide(); }

    public void Hide()
    {
        challenge = null;
        selected = -1;
        feedback = 0;
        SetActive(statementTablets, 0);
        SetActive(ruleTablets, 0);
        SetActive(statementSlots, 0);
        SetActive(ruleSlots, 0);
    }

    public void Configure(ProofChallengeData data)
    {
        if (data == null || data.kind == ProofChallengeKind.SortScrolls) { Hide(); return; }
        challenge = data;
        selected = -1;
        selectedRule = false;
        feedback = 0;
        int count = data.kind == ProofChallengeKind.MissingRule ? 1 : data.steps.Length;
        assignedSteps = new int[count];
        assignedRules = new int[count];
        for (int i = 0; i < count; i++) { assignedSteps[i] = -1; assignedRules[i] = -1; }

        bool showStatements = data.kind != ProofChallengeKind.MissingRule;
        bool showRules = data.kind != ProofChallengeKind.OrderSteps;
        int stepCount = showStatements ? Mathf.Min(data.steps.Length, statementTablets.Length) : 0;
        int ruleCount = showRules ? Mathf.Min(
            data.kind == ProofChallengeKind.MissingRule ? data.ruleOptions.Length : data.steps.Length,
            ruleTablets.Length) : 0;
        SetActive(statementTablets, stepCount);
        SetActive(ruleTablets, ruleCount);
        SetActive(statementSlots, showStatements ? count : 0);
        SetActive(ruleSlots, showRules ? count : 0);
        ArrangeStations(data.kind);

        for (int display = 0; display < stepCount; display++)
        {
            int id = data.steps.Length == 5 ? new[] { 2, 4, 0, 3, 1 }[display] : display;
            optionToStep[display] = id;
            ProofStepData step = data.steps[id];
            string body = data.kind == ProofChallengeKind.OrderSteps ?
                step.statement + "\n" + step.justification : step.statement;
            statementTablets[display].SetContent("STATEMENT " + (char)('A' + display), body);
        }
        for (int display = 0; display < ruleCount; display++)
        {
            int id = data.kind == ProofChallengeKind.Formalize && ruleCount == 5 ?
                new[] { 3, 1, 4, 0, 2 }[display] : display;
            optionToRule[display] = id;
            string body = data.kind == ProofChallengeKind.MissingRule ?
                data.ruleOptions[id] : data.steps[id].justification + "\nfor " + data.steps[id].statement;
            ruleTablets[display].SetContent("RULE " + (char)('A' + display), body);
        }
        Refresh();
    }

    private static void SetActive(ProofWorldPedestal[] stations, int count)
    {
        for (int i = 0; i < stations.Length; i++)
            if (stations[i] != null) stations[i].gameObject.SetActive(i < count);
    }

    private void ArrangeStations(ProofChallengeKind kind)
    {
        if (kind == ProofChallengeKind.OrderSteps)
        {
            for (int i = 0; i < 5; i++)
            {
                Place(statementTablets[i], -7f, -5.5f + i * 3.15f, 0f);
                Place(statementSlots[i], 7f, -5.5f + i * 3.15f, 0f);
            }
        }
        else if (kind == ProofChallengeKind.MissingRule)
        {
            for (int i = 0; i < 4; i++)
            {
                bool left = i < 2;
                Place(ruleTablets[i], left ? -9f : 9f,
                    i % 2 == 0 ? -3.3f : .2f, left ? -90f : 90f);
            }
            Place(ruleSlots[0], 0f, 4.0f, 0f);
        }
        else
        {
            for (int i = 0; i < 5; i++)
            {
                float z = -6.2f + i * 3.25f;
                Place(statementTablets[i], -10.4f, z, -90f);
                Place(ruleTablets[i], -6.5f, z, -90f);
                Place(statementSlots[i], 6.5f, z, 90f);
                Place(ruleSlots[i], 10.4f, z, 90f);
            }
        }
    }

    private static void Place(ProofWorldPedestal station, float x, float z, float yaw)
    {
        station.transform.localPosition = new Vector3(x, 0f, z);
        station.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
    }

    public ProofWorldPedestal Nearest(Vector3 position, float rangeSquared, out float distanceSquared)
    {
        ProofWorldPedestal nearest = null;
        distanceSquared = rangeSquared;
        FindNearest(statementTablets, position, ref nearest, ref distanceSquared);
        FindNearest(ruleTablets, position, ref nearest, ref distanceSquared);
        FindNearest(statementSlots, position, ref nearest, ref distanceSquared);
        FindNearest(ruleSlots, position, ref nearest, ref distanceSquared);
        return nearest;
    }

    private static void FindNearest(ProofWorldPedestal[] stations, Vector3 position,
        ref ProofWorldPedestal nearest, ref float best)
    {
        foreach (ProofWorldPedestal station in stations)
        {
            if (station == null || !station.gameObject.activeInHierarchy) continue;
            Vector3 d = station.transform.position - position;
            float distance = d.x * d.x + d.z * d.z;
            if (distance < best) { best = distance; nearest = station; }
        }
    }

    public string Prompt(ProofWorldPedestal station)
    {
        if (station == null) return "Approach a proof pedestal.";
        if (IndexOf(statementTablets, station) >= 0 || IndexOf(ruleTablets, station) >= 0)
            return "INTERACT  •  Select " + station.Heading;
        int slot = IndexOf(statementSlots, station);
        bool rule = slot < 0;
        if (rule) slot = IndexOf(ruleSlots, station);
        if (slot < 0) return "Approach a proof pedestal.";
        int assigned = rule ? assignedRules[slot] : assignedSteps[slot];
        if (selected >= 0 && selectedRule == rule)
            return "INTERACT  •  Place selected tablet in slot " + (slot + 1);
        if (selected >= 0)
            return "Select a " + (rule ? "rule" : "statement") + " tablet for slot " + (slot + 1) + ".";
        return assigned >= 0 ? "INTERACT  •  Remove tablet from slot " + (slot + 1) :
            "Select a " + (rule ? "rule" : "statement") + " tablet for slot " + (slot + 1) + ".";
    }

    public void Interact(ProofWorldPedestal station)
    {
        if (challenge == null || station == null || !station.gameObject.activeInHierarchy || feedback != 0) return;
        int index = IndexOf(statementTablets, station);
        if (index >= 0) { selected = optionToStep[index]; selectedRule = false; Refresh(); return; }
        index = IndexOf(ruleTablets, station);
        if (index >= 0) { selected = optionToRule[index]; selectedRule = true; Refresh(); return; }
        index = IndexOf(statementSlots, station);
        if (index >= 0) { Assign(index, false); return; }
        index = IndexOf(ruleSlots, station);
        if (index >= 0) Assign(index, true);
    }

    private void Assign(int slot, bool rule)
    {
        int[] target = rule ? assignedRules : assignedSteps;
        if (selected >= 0 && selectedRule == rule)
        {
            for (int i = 0; i < target.Length; i++)
                if (target[i] == selected) target[i] = -1;
            target[slot] = selected;
            selected = -1;
        }
        else if (selected < 0) target[slot] = -1;
        Refresh();
    }

    private static int IndexOf(ProofWorldPedestal[] stations, ProofWorldPedestal station)
    {
        for (int i = 0; i < stations.Length; i++) if (stations[i] == station) return i;
        return -1;
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

    public void SetFeedback(bool correct) { feedback = correct ? 1 : -1; Refresh(); }
    public void ClearFeedback() { feedback = 0; Refresh(); }

    private void Refresh()
    {
        if (challenge == null) return;
        for (int i = 0; i < statementTablets.Length; i++)
        {
            ProofWorldPedestal station = statementTablets[i];
            if (station == null || !station.gameObject.activeSelf) continue;
            int id = optionToStep[i];
            bool assigned = Contains(assignedSteps, id);
            station.SetState(State(selected == id && !selectedRule || assigned));
        }
        for (int i = 0; i < ruleTablets.Length; i++)
        {
            ProofWorldPedestal station = ruleTablets[i];
            if (station == null || !station.gameObject.activeSelf) continue;
            int id = optionToRule[i];
            bool assigned = Contains(assignedRules, id);
            station.SetState(State(selected == id && selectedRule || assigned));
        }
        for (int i = 0; i < statementSlots.Length; i++)
        {
            ProofWorldPedestal station = statementSlots[i];
            if (station == null || !station.gameObject.activeSelf) continue;
            int id = assignedSteps[i];
            station.SetContent("STEP " + (i + 1), id < 0 ? "[EMPTY]" :
                challenge.steps[id].statement + "\n" + challenge.steps[id].justification);
            station.SetState(State(id >= 0));
        }
        for (int i = 0; i < ruleSlots.Length; i++)
        {
            ProofWorldPedestal station = ruleSlots[i];
            if (station == null || !station.gameObject.activeSelf) continue;
            int id = assignedRules[i];
            station.SetContent("RULE SLOT " + (i + 1), id < 0 ? "[EMPTY]" :
                challenge.kind == ProofChallengeKind.MissingRule ? challenge.ruleOptions[id] :
                challenge.steps[id].justification);
            station.SetState(State(id >= 0));
        }
    }

    private ProofWorldPedestal.VisualState State(bool active)
    {
        if (feedback > 0) return ProofWorldPedestal.VisualState.Correct;
        if (feedback < 0) return ProofWorldPedestal.VisualState.Incorrect;
        return active ? ProofWorldPedestal.VisualState.Selected : ProofWorldPedestal.VisualState.Normal;
    }

    private static bool Contains(int[] values, int id)
    {
        foreach (int value in values) if (value == id) return true;
        return false;
    }
}
