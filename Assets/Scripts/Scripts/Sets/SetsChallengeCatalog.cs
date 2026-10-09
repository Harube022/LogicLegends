using System;
using System.Collections.Generic;
using UnityEngine;

public enum SetsChallengeType
{
    MEMBERSHIP,
    UNION,
    INTERSECTION,
    DIFFERENCE,
    COMPLEMENT
}

public enum SetZone
{
    A_ONLY,
    INTERSECTION,
    B_ONLY,
    OUTSIDE
}

[Serializable]
public class SetsElementDefinition
{
    public string value;
    public SetZone correctZone;

    public SetsElementDefinition(string value, SetZone correctZone)
    {
        this.value = value;
        this.correctZone = correctZone;
    }
}

[Serializable]
public class SetsConditionData
{
    public string[] setA = Array.Empty<string>();
    public string[] setB = Array.Empty<string>();
}

[Serializable]
public class SetsChallengeData
{
    [Min(1)] public int challengeNumber = 1;
    public SetsChallengeType challengeType;
    public string[] setA = Array.Empty<string>();
    public string[] setB = Array.Empty<string>();
    public string[] universalSet = Array.Empty<string>();
    [TextArea(2, 4)] public string instruction;
    public List<SetsElementDefinition> elements = new List<SetsElementDefinition>();
    public List<SetsConditionData> conditions = new List<SetsConditionData>();

    public int RequiredCorrectPlacements => elements == null ? 0 : elements.Count;

    public string FormatSet(string[] values)
    {
        return values == null || values.Length == 0 ? "∅" : "{ " + string.Join(", ", values) + " }";
    }
}

[CreateAssetMenu(fileName = "SetsChallengeCatalog", menuName = "Logic Legends/Sets Challenge Catalog")]
public sealed class SetsChallengeCatalog : ScriptableObject
{
    [SerializeField] private List<SetsChallengeData> challenges = new List<SetsChallengeData>();

    public IReadOnlyList<SetsChallengeData> Challenges => challenges;

    public void SetChallenges(List<SetsChallengeData> value)
    {
        challenges = value ?? new List<SetsChallengeData>();
    }

    public static List<SetsChallengeData> CreateDefaultChallenges()
    {
        return new List<SetsChallengeData>
        {
            new SetsChallengeData
            {
                challengeNumber = 1,
                challengeType = SetsChallengeType.MEMBERSHIP,
                setA = new[] { "1" },
                setB = Array.Empty<string>(),
                universalSet = new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9" },
                instruction = "Place each member of A or B in the matching Venn region.",
                conditions = new List<SetsConditionData>
                {
                    new SetsConditionData { setA = new[] { "1" } },
                    new SetsConditionData { setA = new[] { "1", "2", "3" } },
                    new SetsConditionData { setA = new[] { "1", "3", "5" }, setB = new[] { "2", "6" } },
                    new SetsConditionData { setA = new[] { "1", "3", "5" }, setB = new[] { "1", "2", "6" } },
                    new SetsConditionData { setA = new[] { "1", "2", "3", "4", "5" }, setB = new[] { "3", "4", "6", "9" } }
                }
            },
            new SetsChallengeData
            {
                challengeNumber = 2,
                challengeType = SetsChallengeType.UNION,
                setA = new[] { "1", "3", "5" },
                setB = new[] { "1", "2", "6" },
                universalSet = new[] { "1", "2", "3", "4", "5", "6" },
                instruction = "Construct A ∪ B by placing every distinct element from either set.",
                elements = new List<SetsElementDefinition>
                {
                    new SetsElementDefinition("1", SetZone.INTERSECTION),
                    new SetsElementDefinition("3", SetZone.A_ONLY),
                    new SetsElementDefinition("5", SetZone.A_ONLY),
                    new SetsElementDefinition("2", SetZone.B_ONLY),
                    new SetsElementDefinition("6", SetZone.B_ONLY)
                }
            },
            new SetsChallengeData
            {
                challengeNumber = 3,
                challengeType = SetsChallengeType.INTERSECTION,
                setA = new[] { "1", "2", "3", "4" },
                setB = new[] { "3", "4", "5", "6" },
                universalSet = new[] { "1", "2", "3", "4", "5", "6" },
                instruction = "Construct A ∩ B by placing the elements common to both sets.",
                elements = new List<SetsElementDefinition>
                {
                    new SetsElementDefinition("3", SetZone.INTERSECTION),
                    new SetsElementDefinition("4", SetZone.INTERSECTION)
                }
            },
            new SetsChallengeData
            {
                challengeNumber = 4,
                challengeType = SetsChallengeType.DIFFERENCE,
                setA = new[] { "1", "2", "3", "4" },
                setB = new[] { "3", "4", "5" },
                universalSet = new[] { "1", "2", "3", "4", "5" },
                instruction = "Construct A − B by placing elements in A that are not in B.",
                elements = new List<SetsElementDefinition>
                {
                    new SetsElementDefinition("1", SetZone.A_ONLY),
                    new SetsElementDefinition("2", SetZone.A_ONLY)
                }
            },
            new SetsChallengeData
            {
                challengeNumber = 5,
                challengeType = SetsChallengeType.COMPLEMENT,
                setA = new[] { "1", "2", "3" },
                universalSet = new[] { "1", "2", "3", "4", "5", "6" },
                instruction = "Construct Aᶜ by placing elements in U that are not in A outside Circle A.",
                elements = new List<SetsElementDefinition>
                {
                    new SetsElementDefinition("4", SetZone.OUTSIDE),
                    new SetsElementDefinition("5", SetZone.OUTSIDE),
                    new SetsElementDefinition("6", SetZone.OUTSIDE)
                }
            }
        };
    }
}
