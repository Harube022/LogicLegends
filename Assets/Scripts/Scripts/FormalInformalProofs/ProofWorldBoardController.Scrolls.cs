using System;
using System.Collections.Generic;

public sealed partial class ProofWorldBoardController
{
    private Action<ProofWorldPedestal> readRequested;
    private Action carryChanged;
    public bool HasCarriedScroll => challenge != null && selected >= 0;
    public bool CarryingRule => HasCarriedScroll && selectedRule;
    public IEnumerable<ProofWorldPedestal> PlacementSlots
    {
        get { foreach (var slot in statementSlots) yield return slot; foreach (var slot in ruleSlots) yield return slot; }
    }
    public void BindScrollInteraction(Action<ProofWorldPedestal> read, Action changed)
    { readRequested = read; carryChanged = changed; }

    private string SourceContent(bool rule, int id)
    {
        if (rule) return challenge.kind == ProofChallengeKind.MissingRule ? challenge.ruleOptions[id] :
            challenge.steps[id].justification + "\nfor " + challenge.steps[id].statement;
        var step = challenge.steps[id];
        return challenge.kind == ProofChallengeKind.OrderSteps ? step.statement + "\n" + step.justification : step.statement;
    }
    private bool SourceIdentity(ProofWorldPedestal source, out bool rule, out int id)
    {
        rule = false; id = -1;
        if (challenge == null || source == null || !source.gameObject.activeInHierarchy) return false;
        int display = IndexOf(statementTablets, source);
        if (display < 0) { rule = true; display = IndexOf(ruleTablets, source); }
        if (display < 0) return false;
        id = rule ? optionToRule[display] : optionToStep[display];
        return true;
    }
    // Both the world preview and the full reader use this same question/option mapping.
    public bool TryReadScroll(ProofWorldPedestal source, out string title, out string content)
    {
        title = content = "";
        if (feedback != 0 || HasCarriedScroll || !SourceIdentity(source, out bool rule, out int id) ||
            !source.TabletVisible || Contains(rule ? assignedRules : assignedSteps, id)) return false;
        title = source.Heading; content = SourceContent(rule, id); return true;
    }
    public bool CollectReadScroll(ProofWorldPedestal source)
    {
        if (!TryReadScroll(source, out _, out _) || !SourceIdentity(source, out bool rule, out int id)) return false;
        selected = id; selectedRule = rule; Refresh(); return true;
    }
    public void ReturnCarriedScroll()
    {
        if (!HasCarriedScroll) return;
        selected = -1; Refresh(); // Restores the very same source; no object is spawned.
    }
    public bool IsAvailablePlacement(ProofWorldPedestal slot)
    {
        if (!AcceptsCarriedScroll(slot)) return false;
        int index = IndexOf(selectedRule ? ruleSlots : statementSlots, slot);
        return index >= 0 && (selectedRule ? assignedRules : assignedSteps)[index] < 0;
    }
    public bool AcceptsCarriedScroll(ProofWorldPedestal slot) => HasCarriedScroll && slot != null &&
        slot.gameObject.activeInHierarchy && feedback == 0 && IndexOf(selectedRule ? ruleSlots : statementSlots, slot) >= 0;
}
