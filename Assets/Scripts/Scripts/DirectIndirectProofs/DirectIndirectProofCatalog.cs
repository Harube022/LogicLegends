using System;
using UnityEngine;

public enum TrialProofKind { IdentifyMethod, ArrangeSteps, ChooseAndArrange }
public enum TrialProofMethod { Direct, Contrapositive, Contradiction }

[Serializable]
public sealed class TrialProofTabletData
{
    public string id;
    [TextArea(2, 5)] public string statement;
    [TextArea(1, 3)] public string justification;
}

[Serializable]
public sealed class TrialProofOrder
{
    public int[] tabletIds = new int[0];
}

[Serializable]
public sealed class TrialProofChallenge
{
    public TrialProofKind kind;
    public string title;
    [TextArea(2, 6)] public string problem;
    [TextArea(2, 6)] public string explanation;
    public TrialProofMethod station;
    public TrialProofMethod correctMethod;
    public TrialProofTabletData[] tablets = new TrialProofTabletData[0];
    public int[] displayOrder = new int[0];
    public TrialProofOrder[] acceptedOrders = new TrialProofOrder[0];

    public bool IsCorrect(int selectedMethod, int[] slots)
    {
        if (kind == TrialProofKind.IdentifyMethod)
            return selectedMethod == (int)correctMethod;
        if (kind == TrialProofKind.ChooseAndArrange && selectedMethod != (int)correctMethod)
            return false;
        if (tablets == null || slots == null || slots.Length != tablets.Length ||
            acceptedOrders == null || acceptedOrders.Length == 0)
            return false;

        // A complete answer must use every physical tablet exactly once.
        bool[] used = new bool[tablets.Length];
        foreach (int id in slots)
        {
            if (id < 0 || id >= used.Length || used[id]) return false;
            used[id] = true;
        }
        foreach (TrialProofOrder order in acceptedOrders)
        {
            if (order == null || order.tabletIds == null || order.tabletIds.Length != slots.Length)
                continue;
            bool matches = true;
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] != order.tabletIds[i]) { matches = false; break; }
            if (matches) return true;
        }
        return false;
    }
}

[Serializable]
public sealed class TrialProofVariantSet
{
    public TrialProofChallenge[] variants = new TrialProofChallenge[0];
}

[CreateAssetMenu(fileName = "DirectIndirectProofChallenges", menuName = "Logic Legends/Direct and Indirect Proof Challenges")]
public sealed class DirectIndirectProofCatalog : ScriptableObject
{
    public TrialProofChallenge[] challenges = new TrialProofChallenge[0];
    public TrialProofVariantSet[] variantSets = new TrialProofVariantSet[0];
}
