using System;
using UnityEngine;

public enum ProofChallengeKind { SortScrolls, OrderSteps, MissingRule, Formalize }

[Serializable]
public sealed class ProofScrollData
{
    public string id;
    public string title;
    [TextArea(3, 10)] public string content;
    public bool isFormal;
}

[Serializable]
public sealed class ProofStepData
{
    public string id;
    public string statement;
    public string justification;
    public int[] prerequisiteIds = new int[0];
}

[Serializable]
public sealed class ProofOrderData
{
    public int[] stepIds = new int[0];
}

[Serializable]
public sealed class ProofChallengeData
{
    public ProofChallengeKind kind;
    public string title;
    [TextArea(2, 5)] public string instructions;
    [TextArea(2, 5)] public string context;
    public ProofScrollData[] scrolls = new ProofScrollData[0];
    public ProofStepData[] steps = new ProofStepData[0];
    public ProofOrderData[] acceptableOrders = new ProofOrderData[0];
    public string[] ruleOptions = new string[0];
    public string expectedRule;
}

[Serializable]
public sealed class ProofChallengeVariantSet
{
    public ProofChallengeData[] variants = new ProofChallengeData[0];
}

[CreateAssetMenu(fileName = "ProofChallenges", menuName = "Logic Legends/Proof Challenge Catalog")]
public sealed class ProofChallengeCatalog : ScriptableObject
{
    public ProofChallengeData[] challenges = new ProofChallengeData[0];
    public ProofChallengeVariantSet[] variantSets = new ProofChallengeVariantSet[0];
}
