using System;
using System.Collections.Generic;
using UnityEngine;

public enum MappingQuestionKind
{
    BuildExactRelation,
    ClassifyFunction,
    SelectProperties
}

[Flags]
public enum MappingProperty
{
    None = 0,
    Function = 1,
    Injective = 2,
    Surjective = 4,
    Bijective = 8,
    ManyToOne = 16
}

[Serializable]
public struct MappingPair : IEquatable<MappingPair>
{
    public string domain;
    public string codomain;

    public MappingPair(string domain, string codomain)
    {
        this.domain = domain;
        this.codomain = codomain;
    }

    public bool Equals(MappingPair other) => domain == other.domain && codomain == other.codomain;
    public override bool Equals(object obj) => obj is MappingPair && Equals((MappingPair)obj);
    public override int GetHashCode() => ((domain ?? "").GetHashCode() * 397) ^ (codomain ?? "").GetHashCode();
    public override string ToString() => domain + " → " + codomain;
}

[Serializable]
public sealed class MappingChallengeData
{
    public string title;
    [TextArea(2, 5)] public string instruction;
    public MappingQuestionKind questionKind;
    public string[] domain = new string[0];
    public string[] codomain = new string[0];
    public List<MappingPair> initialPairs = new List<MappingPair>();
    public List<MappingPair> requiredPairs = new List<MappingPair>();
    public bool initialPairsLocked;
    public bool requireFunction;
    public bool requireInjective;
    public bool requireSurjective;
    public bool correctFunctionAnswer;
    public int correctClassificationIndex = 1;
    public MappingProperty correctProperties;
}

[Serializable]
public sealed class MappingChallengeVariantGroup
{
    public string stageName;
    public List<MappingChallengeData> variants = new List<MappingChallengeData>();
}

[CreateAssetMenu(fileName = "FunctionsRelationsChallenges", menuName = "Logic Legends/Functions and Relations Challenges")]
public sealed class MappingChallengeCatalog : ScriptableObject
{
    [SerializeField] private List<MappingChallengeVariantGroup> challengeGroups = new List<MappingChallengeVariantGroup>();
    public IReadOnlyList<MappingChallengeVariantGroup> ChallengeGroups => challengeGroups;
    public int ChallengeCount => challengeGroups.Count;

    public MappingChallengeData GetVariant(int challengeIndex, int variantIndex)
    {
        if (challengeIndex < 0 || challengeIndex >= challengeGroups.Count) return null;
        var group = challengeGroups[challengeIndex];
        if (group == null || group.variants == null || variantIndex < 0 || variantIndex >= group.variants.Count)
            return null;
        return group.variants[variantIndex];
    }

    public void SetChallengeGroups(List<MappingChallengeVariantGroup> value)
    {
        challengeGroups = value ?? new List<MappingChallengeVariantGroup>();
    }
}
