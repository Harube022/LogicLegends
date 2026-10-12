using System.Collections.Generic;

// Ten fixed, reviewable problems per stage. Random selection happens once when the scene starts.
public static class MappingChallengeVariantFactory
{
    private static readonly string[] Domain = { "1", "2", "3", "4" };
    private static readonly string[] Codomain = { "A", "B", "C", "D" };
    private static readonly int[][] Permutations =
    {
        new[] { 0, 1, 2, 3 }, new[] { 0, 1, 3, 2 },
        new[] { 0, 2, 1, 3 }, new[] { 0, 2, 3, 1 },
        new[] { 0, 3, 1, 2 }, new[] { 0, 3, 2, 1 },
        new[] { 1, 0, 2, 3 }, new[] { 1, 2, 3, 0 },
        new[] { 2, 3, 0, 1 }, new[] { 3, 2, 1, 0 }
    };
    // Challenge 3 avoids straight-through diagrams so the order clue must be interpreted.
    private static readonly int[][] InferencePermutations =
    {
        new[] { 2, 0, 3, 1 }, new[] { 1, 3, 0, 2 },
        new[] { 3, 1, 0, 2 }, new[] { 2, 3, 1, 0 },
        new[] { 1, 2, 3, 0 }, new[] { 3, 0, 2, 1 },
        new[] { 2, 1, 3, 0 }, new[] { 1, 3, 2, 0 },
        new[] { 3, 2, 0, 1 }, new[] { 0, 3, 1, 2 }
    };

    public static List<MappingChallengeVariantGroup> BuildGroups()
    {
        var groups = new List<MappingChallengeVariantGroup>();
        string[] names = { "Build the relation", "Connect the ordered pairs", "Complete the function",
            "Function or not?", "Name every property" };
        for (int stage = 0; stage < names.Length; stage++)
        {
            var group = new MappingChallengeVariantGroup { stageName = names[stage] };
            for (int variant = 0; variant < Permutations.Length; variant++)
                group.variants.Add(Build(stage, variant, names[stage]));
            groups.Add(group);
        }
        return groups;
    }

    private static MappingChallengeData Build(int stage, int variant, string title)
    {
        var permutation = stage == 2 ? InferencePermutations[variant] : Permutations[variant];
        var fourPairs = new List<MappingPair>();
        for (int source = 0; source < Domain.Length; source++)
            fourPairs.Add(new MappingPair(Domain[source], Codomain[permutation[source]]));

        var challenge = new MappingChallengeData
        {
            title = title,
            domain = (string[])Domain.Clone(),
            codomain = (string[])Codomain.Clone()
        };
        switch (stage)
        {
            case 0:
                challenge.questionKind = MappingQuestionKind.BuildExactRelation;
                challenge.requiredPairs = fourPairs;
                challenge.instruction = "Connect " + PairList(fourPairs) + ". The range contains the outputs you use.";
                break;
            case 1:
                challenge.questionKind = MappingQuestionKind.BuildExactRelation;
                challenge.requiredPairs = WithExtraPair(fourPairs, permutation, variant);
                challenge.instruction = "Build R = {" + SetList(challenge.requiredPairs) + "}. An input may have two lasers.";
                break;
            case 2:
                challenge.questionKind = MappingQuestionKind.BuildExactRelation;
                int fixedSource = variant % Domain.Length;
                challenge.initialPairs.Add(fourPairs[fixedSource]);
                challenge.requiredPairs = fourPairs;
                challenge.initialPairsLocked = true;
                challenge.requireFunction = true;
                challenge.requireInjective = true;
                challenge.requireSurjective = true;
                var remainingInputs = new List<int>();
                for (int source = 0; source < Domain.Length; source++)
                    if (source != fixedSource) remainingInputs.Add(source);
                remainingInputs.Sort((a, b) => permutation[a].CompareTo(permutation[b]));
                challenge.instruction = fourPairs[fixedSource] + " is fixed. Make a bijection (one-to-one and onto). " +
                    "The other output letters increase for inputs " + Domain[remainingInputs[0]] + ", " +
                    Domain[remainingInputs[1]] + ", " + Domain[remainingInputs[2]] + ", in that order.";
                break;
            case 3:
                challenge.questionKind = MappingQuestionKind.ClassifyFunction;
                challenge.initialPairs = variant % 2 == 0 ? fourPairs : WithMissingAndDuplicate(fourPairs, variant);
                challenge.initialPairsLocked = true;
                challenge.correctClassificationIndex = MappingRuleEvaluator.IsFunction(challenge.initialPairs, challenge.domain) ? 0 : 2;
                challenge.instruction = "Four lasers are preset. Every valid diagram is a relation. Trace each input; choose Function Only if each has one output, otherwise Relation Only.";
                break;
            default:
                challenge.questionKind = MappingQuestionKind.SelectProperties;
                challenge.initialPairs = variant % 2 == 0 ? fourPairs : WithExtraPair(fourPairs, permutation, variant);
                challenge.initialPairsLocked = true;
                challenge.correctProperties = MappingRuleEvaluator.Properties(challenge.initialPairs, challenge.domain, challenge.codomain) &
                    (MappingProperty.Injective | MappingProperty.Surjective | MappingProperty.Bijective);
                challenge.instruction = "Inspect the preset lasers. Toggle every applicable property: Injective, Surjective, Bijective.";
                break;
        }
        return challenge;
    }

    private static List<MappingPair> WithExtraPair(List<MappingPair> fourPairs, int[] permutation, int variant)
    {
        var pairs = new List<MappingPair>(fourPairs);
        int source = variant % Domain.Length;
        int otherSource = (source + 1 + (variant / Domain.Length) % 2) % Domain.Length;
        pairs.Add(new MappingPair(Domain[source], Codomain[permutation[otherSource]]));
        pairs.Sort((a, b) =>
        {
            int byDomain = string.CompareOrdinal(a.domain, b.domain);
            return byDomain != 0 ? byDomain : string.CompareOrdinal(a.codomain, b.codomain);
        });
        return pairs;
    }

    private static List<MappingPair> WithMissingAndDuplicate(List<MappingPair> fourPairs, int variant)
    {
        var pairs = new List<MappingPair>(fourPairs);
        int missingSource = variant % 3; // Keep Domain 4 involved in every variant.
        int duplicateSource = (missingSource + 1 + (variant / 3) % 3) % Domain.Length;
        var movedOutput = fourPairs[missingSource].codomain;
        pairs.RemoveAt(missingSource);
        pairs.Add(new MappingPair(Domain[duplicateSource], movedOutput));
        pairs.Sort((a, b) =>
        {
            int byDomain = string.CompareOrdinal(a.domain, b.domain);
            return byDomain != 0 ? byDomain : string.CompareOrdinal(a.codomain, b.codomain);
        });
        return pairs;
    }

    private static string PairList(List<MappingPair> pairs)
    {
        var text = new string[pairs.Count];
        for (int i = 0; i < pairs.Count; i++) text[i] = pairs[i].ToString();
        return string.Join(", ", text);
    }

    private static string SetList(List<MappingPair> pairs)
    {
        var text = new string[pairs.Count];
        for (int i = 0; i < pairs.Count; i++) text[i] = "(" + pairs[i].domain + "," + pairs[i].codomain + ")";
        return string.Join(", ", text);
    }
}
