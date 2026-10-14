using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Authors ten persistent, comparable problems for each Proofs Library challenge.</summary>
public static class BuildFormalInformalProofVariants
{
    private const string CatalogPath = "Assets/Resources/ProofChallenges.asset";

    private sealed class Topic
    {
        public readonly string name, formal, informal;
        public Topic(string name, string formal, string informal)
        { this.name = name; this.formal = formal; this.informal = informal; }
    }

    private static readonly Topic[] Topics =
    {
        new Topic("Even Sum", "Claim: Two even integers have an even sum.\n1. Let a = 2m and b = 2n. (Definition of even.)\n2. a + b = 2(m + n). (Algebra.)\n3. m + n is an integer, so a + b is even. (Definition of even.)",
            "Two even numbers are made of whole pairs. Putting the pairs together leaves no unpaired item, so their sum is even."),
        new Topic("Odd Sum", "Claim: Two odd integers have an even sum.\n1. Let a = 2m + 1 and b = 2n + 1. (Definition of odd.)\n2. a + b = 2(m + n + 1). (Algebra.)\n3. m + n + 1 is an integer, so a + b is even. (Definition of even.)",
            "Each odd number has one leftover after forming pairs. The two leftovers form another pair, so the sum is even."),
        new Topic("Even Square", "Claim: The square of an even integer is even.\n1. Let n = 2k. (Definition of even.)\n2. n² = 4k² = 2(2k²). (Algebra.)\n3. 2k² is an integer, so n² is even. (Definition of even.)",
            "An even number is built from pairs. Squaring it still produces a number of complete pairs, so its square is even."),
        new Topic("Odd Square", "Claim: The square of an odd integer is odd.\n1. Let n = 2k + 1. (Definition of odd.)\n2. n² = 2(2k² + 2k) + 1. (Algebra.)\n3. 2k² + 2k is an integer, so n² is odd. (Definition of odd.)",
            "An odd number has a leftover after pairing. When it is squared, one leftover remains, so the square is odd."),
        new Topic("Even Plus Odd", "Claim: An even integer plus an odd integer is odd.\n1. Let a = 2m and b = 2n + 1. (Parity definitions.)\n2. a + b = 2(m + n) + 1. (Algebra.)\n3. m + n is an integer, so a + b is odd. (Definition of odd.)",
            "The even group has no leftover and the odd group has one. Together they still have one leftover, so the sum is odd."),
        new Topic("Even Product", "Claim: An even integer times an integer is even.\n1. Let a = 2m and let b be an integer. (Definition of even.)\n2. ab = 2(mb). (Algebra.)\n3. mb is an integer, so ab is even. (Definition of even.)",
            "An even amount consists of pairs. Taking any whole number of copies preserves those pairs, so the product is even."),
        new Topic("Multiples of Three", "Claim: The sum of two multiples of 3 is a multiple of 3.\n1. Let a = 3m and b = 3n. (Definition of multiple.)\n2. a + b = 3(m + n). (Algebra.)\n3. m + n is an integer, so 3 divides a + b. (Definition of divides.)",
            "Each number is made of complete groups of three. Combining the groups still gives complete groups of three."),
        new Topic("Even Difference", "Claim: The difference of two even integers is even.\n1. Let a = 2m and b = 2n. (Definition of even.)\n2. a - b = 2(m - n). (Algebra.)\n3. m - n is an integer, so a - b is even. (Definition of even.)",
            "Subtracting complete pairs from complete pairs leaves a whole number of pairs, so the difference is even."),
        new Topic("Odd Successor", "Claim: The successor of an odd integer is even.\n1. Let n = 2k + 1. (Definition of odd.)\n2. n + 1 = 2(k + 1). (Algebra.)\n3. k + 1 is an integer, so n + 1 is even. (Definition of even.)",
            "An odd number has one item left after pairing. Add one more item and it completes a pair, making the successor even."),
        new Topic("Multiple of Four", "Claim: Every multiple of 4 is even.\n1. Let n = 4k. (Definition of multiple.)\n2. n = 2(2k). (Algebra.)\n3. 2k is an integer, so n is even. (Definition of even.)",
            "Four consists of two pairs. Any whole number of groups of four can be split into pairs, so every multiple is even.")
    };

    private sealed class Chain
    {
        public readonly string p, q, r;
        public Chain(string p, string q, string r) { this.p = p; this.q = q; this.r = r; }
    }

    private static readonly Chain[] OrderChains =
    {
        new Chain("The code compiles", "The program runs", "The test executes"),
        new Chain("The key is valid", "The request is authenticated", "The data is accessible"),
        new Chain("The graph is connected", "A path exists", "The target is reachable"),
        new Chain("The input is sorted", "Binary search applies", "The lookup terminates"),
        new Chain("The file exists", "The loader opens it", "The records are read"),
        new Chain("The password matches", "The login succeeds", "The dashboard opens"),
        new Chain("The sensor is active", "A signal is sent", "The alert appears"),
        new Chain("The package installs", "The library loads", "The app starts"),
        new Chain("The value is positive", "The square root is real", "The calculation proceeds")
    };

    private static readonly Chain[] FormalizeChains =
    {
        new Chain("The number is even", "Its square is even", "The parity check passes"),
        new Chain("The user has access", "The file can open", "The report can load"),
        new Chain("The device is online", "The message can send", "The server receives it"),
        new Chain("The data is valid", "The parser accepts it", "The record is stored"),
        new Chain("The queue is nonempty", "An item can be removed", "The worker can process it"),
        new Chain("The vertex is visited", "Its edges are checked", "Its neighbors are discovered"),
        new Chain("The payment succeeds", "The order is confirmed", "The receipt is issued"),
        new Chain("The model is trained", "A prediction is produced", "The result is displayed"),
        new Chain("The token is current", "The session remains valid", "The page is shown")
    };

    [MenuItem("Logic Legends/Build Proofs Library Ten Variants Per Challenge")]
    public static void Build()
    {
        ProofChallengeCatalog catalog = AssetDatabase.LoadAssetAtPath<ProofChallengeCatalog>(CatalogPath);
        if (catalog == null || catalog.challenges == null || catalog.challenges.Length != 5)
            throw new InvalidOperationException("Create the five base Proofs Library challenges first.");

        var sets = new ProofChallengeVariantSet[5];
        for (int challenge = 0; challenge < 5; challenge++)
        {
            sets[challenge] = new ProofChallengeVariantSet { variants = new ProofChallengeData[10] };
            sets[challenge].variants[0] = catalog.challenges[challenge];
        }
        for (int variant = 1; variant < 10; variant++)
        {
            Topic first = Topics[variant - 1];
            Topic second = Topics[(variant + 4) % Topics.Length];
            sets[0].variants[variant] = MakeClassification(first, null, variant);
            sets[1].variants[variant] = MakeClassification(first, second, variant);
            sets[2].variants[variant] = MakeChain(OrderChains[variant - 1], false);
            sets[3].variants[variant] = MakeMissingRule(variant);
            sets[4].variants[variant] = MakeChain(FormalizeChains[variant - 1], true);
        }
        Validate(sets);
        catalog.variantSets = sets;
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log("Proofs Library: saved ten validated variants for each of five challenges.");
    }

    private static ProofChallengeData MakeClassification(Topic first, Topic second, int variant)
    {
        var scrolls = new List<ProofScrollData>();
        if (second == null)
        {
            scrolls.Add(Scroll("A", first.name, first.formal, true));
            scrolls.Add(Scroll("B", first.name, first.informal, false));
        }
        else
        {
            scrolls.Add(Scroll("A", first.name, first.formal, true));
            scrolls.Add(Scroll("B", second.name, second.informal, false));
            scrolls.Add(Scroll("C", second.name, second.formal, true));
            scrolls.Add(Scroll("D", first.name, first.informal, false));
        }
        // Alternate display order so a player's position is never a reliable answer key.
        int rotation = variant % scrolls.Count;
        ProofScrollData[] rotated = scrolls.Skip(rotation).Concat(scrolls.Take(rotation)).ToArray();
        for (int i = 0; i < rotated.Length; i++) rotated[i].id = ((char)('A' + i)).ToString();
        return new ProofChallengeData
        {
            kind = ProofChallengeKind.SortScrolls,
            title = second == null ? "Identify Proof Styles" : "Classify Multiple Proofs",
            instructions = second == null ? "Read both proofs and place each on the correct altar." :
                "Classify all four valid proofs by how their reasoning is presented.",
            scrolls = rotated
        };
    }

    private static ProofChallengeData MakeChain(Chain c, bool formalize)
    {
        ProofStepData[] steps =
        {
            Step("premise-p", c.p, "Premise"),
            Step("premise-pq", "If " + c.p.ToLowerInvariant() + ", then " + c.q.ToLowerInvariant(), "Premise"),
            Step("premise-qr", "If " + c.q.ToLowerInvariant() + ", then " + c.r.ToLowerInvariant(), "Premise"),
            Step("derive-q", c.q, "Modus Ponens from the first two premises", 0, 1),
            Step("derive-r", c.r, "Modus Ponens from the third premise and " + c.q.ToLowerInvariant(), 2, 3)
        };
        ProofOrderData[] orders =
        {
            Order(0, 1, 2, 3, 4), Order(0, 2, 1, 3, 4), Order(1, 0, 2, 3, 4),
            Order(1, 2, 0, 3, 4), Order(2, 0, 1, 3, 4), Order(2, 1, 0, 3, 4)
        };
        string premise = c.p + "; if " + c.p.ToLowerInvariant() + ", then " + c.q.ToLowerInvariant() +
            "; if " + c.q.ToLowerInvariant() + ", then " + c.r.ToLowerInvariant() + ".";
        return new ProofChallengeData
        {
            kind = formalize ? ProofChallengeKind.Formalize : ProofChallengeKind.OrderSteps,
            title = formalize ? "Formalize an Informal Explanation" : "Arrange Formal Proof Steps",
            context = formalize ? "Given " + premise + " Explain why " + c.r.ToLowerInvariant() + "." :
                "Premises: " + premise + " Conclusion: " + c.r + ".",
            instructions = formalize ? "Place all five statements in a valid order and pair each with its justification." :
                "Arrange the five shuffled statements into a valid proof, then confirm at the Book Statue.",
            steps = steps, acceptableOrders = orders
        };
    }

    private static ProofChallengeData MakeMissingRule(int variant)
    {
        string[] contexts =
        {
            "1. If the input is valid, the parser accepts it.  2. The input is valid.  3. The parser accepts it. — [MISSING]",
            "1. If the file exists, it can open.  2. It cannot open.  3. The file does not exist. — [MISSING]",
            "1. The flag is set and the timer is running.  2. The flag is set. — [MISSING]",
            "1. The user is logged in.  2. The user is logged in or the page is public. — [MISSING]",
            "1. The key is valid.  2. The server is online.  3. The key is valid and the server is online. — [MISSING]",
            "1. If the graph is connected, a path exists.  2. The graph is connected.  3. A path exists. — [MISSING]",
            "1. If the token is valid, access is granted.  2. Access is not granted.  3. The token is invalid. — [MISSING]",
            "1. The value is positive and even.  2. The value is even. — [MISSING]",
            "1. The test passes.  2. The test passes or the build is skipped. — [MISSING]"
        };
        string[] answers =
        {
            "Modus Ponens", "Modus Tollens", "Simplification", "Addition", "Conjunction",
            "Modus Ponens", "Modus Tollens", "Simplification", "Addition"
        };
        string answer = answers[variant - 1];
        string[] all = { "Modus Ponens", "Modus Tollens", "Simplification", "Addition", "Conjunction" };
        string[] distractors = all.Where(option => option != answer).Take(3).ToArray();
        string[] options = { answer, distractors[0], distractors[1], distractors[2] };
        int rotation = variant % options.Length;
        options = options.Skip(rotation).Concat(options.Take(rotation)).ToArray();
        return new ProofChallengeData
        {
            kind = ProofChallengeKind.MissingRule,
            title = "Missing Justification",
            context = contexts[variant - 1],
            instructions = "Read the given proof, then place its missing justification in the slot.",
            ruleOptions = options, expectedRule = answer
        };
    }

    private static ProofScrollData Scroll(string id, string title, string content, bool formal)
    { return new ProofScrollData { id = id, title = title, content = content, isFormal = formal }; }
    private static ProofStepData Step(string id, string statement, string justification, params int[] prerequisites)
    { return new ProofStepData { id = id, statement = statement, justification = justification, prerequisiteIds = prerequisites }; }
    private static ProofOrderData Order(params int[] ids) { return new ProofOrderData { stepIds = ids }; }

    private static void Validate(ProofChallengeVariantSet[] sets)
    {
        ProofChallengeKind[] kinds = { ProofChallengeKind.SortScrolls, ProofChallengeKind.SortScrolls,
            ProofChallengeKind.OrderSteps, ProofChallengeKind.MissingRule, ProofChallengeKind.Formalize };
        for (int challenge = 0; challenge < sets.Length; challenge++)
        {
            if (sets[challenge].variants.Length != 10) throw new InvalidOperationException("Expected ten variants.");
            foreach (ProofChallengeData variant in sets[challenge].variants)
            {
                if (variant == null || variant.kind != kinds[challenge])
                    throw new InvalidOperationException("Challenge kind is missing or changed.");
                if (challenge < 2 && (variant.scrolls.Length != (challenge == 0 ? 2 : 4) ||
                    variant.scrolls.Count(s => s.isFormal) * 2 != variant.scrolls.Length))
                    throw new InvalidOperationException("Classification variant needs a balanced set of scrolls.");
                if ((challenge == 2 || challenge == 4) && (variant.steps.Length != 5 ||
                    variant.acceptableOrders.Length != 6 || variant.steps[4].prerequisiteIds.Length != 2))
                    throw new InvalidOperationException("Proof variant must retain the five-step derivation.");
                if (challenge == 3 && (variant.ruleOptions.Length != 4 ||
                    variant.ruleOptions.Distinct().Count() != 4 ||
                    variant.ruleOptions.Count(o => o == variant.expectedRule) != 1))
                    throw new InvalidOperationException("Missing-rule variant must have one answer among four options.");
            }
        }
    }
}
