using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Authored, equally sized problem banks for the five proof trials.</summary>
public static class BuildDirectIndirectProofVariants
{
    private const string Path = "Assets/Resources/DirectIndirectProofChallenges.asset";
    private static readonly int[][] Shuffles =
    {
        new[] { 2, 4, 0, 3, 1 }, new[] { 3, 0, 4, 1, 2 },
        new[] { 4, 2, 0, 3, 1 }, new[] { 1, 4, 2, 0, 3 },
        new[] { 3, 1, 4, 2, 0 }, new[] { 2, 0, 4, 1, 3 },
        new[] { 4, 1, 3, 0, 2 }, new[] { 1, 3, 0, 4, 2 },
        new[] { 2, 4, 1, 3, 0 }, new[] { 4, 0, 2, 1, 3 }
    };

    [MenuItem("Logic Legends/Proofs/Build Ten Variants Per Challenge")]
    public static void Build()
    {
        DirectIndirectProofCatalog catalog = AssetDatabase.LoadAssetAtPath<DirectIndirectProofCatalog>(Path);
        if (catalog == null || catalog.challenges == null || catalog.challenges.Length != 5)
            throw new InvalidOperationException("The existing five-challenge proof catalog is missing.");

        var banks = new List<TrialProofChallenge>[5];
        for (int i = 0; i < banks.Length; i++)
            banks[i] = new List<TrialProofChallenge> { catalog.challenges[i] };

        // Challenge 1: classify a short proof. Five direct and five indirect examples.
        I(banks[0], "Even-sum argument", "Given even a and b, the argument writes a = 2m and b = 2n, then a + b = 2(m + n). Is this proof direct or indirect?", "It begins with the premise and derives the conclusion directly.", TrialProofMethod.Direct);
        I(banks[0], "No greatest even integer", "An argument assumes a greatest even integer E, then produces the larger even integer E + 2. Is this proof direct or indirect?", "It contradicts the assumption that a greatest even integer exists.", TrialProofMethod.Contradiction);
        I(banks[0], "Odd-product argument", "Given odd a and b, the argument expands (2m + 1)(2n + 1) = 2(2mn + m + n) + 1. Is this proof direct or indirect?", "The argument derives oddness directly from the two odd inputs.", TrialProofMethod.Direct);
        I(banks[0], "Odd-square argument", "To prove n² odd implies n odd, the argument assumes n even and obtains n² = 4k², which is even. Is this proof direct or indirect?", "It proves the contrapositive, so the method is indirect.", TrialProofMethod.Contrapositive);
        I(banks[0], "Multiples of three", "Given a = 3m and b = 3n, the argument shows a + b = 3(m + n). Is this proof direct or indirect?", "It starts with the given divisibility and reaches the result directly.", TrialProofMethod.Direct);
        I(banks[0], "Smallest rational", "An argument assumes a smallest positive rational r, then constructs the smaller positive rational r/2. Is this proof direct or indirect?", "The constructed rational contradicts the assumed smallest one.", TrialProofMethod.Contradiction);
        I(banks[0], "Even square", "To prove n² even implies n even, an argument assumes n odd and shows n² = 2(2k² + 2k) + 1. Is this proof direct or indirect?", "It proves the contrapositive, an indirect method.", TrialProofMethod.Contrapositive);
        I(banks[0], "Divisible difference", "Given a = 5m and b = 5n, the argument computes a - b = 5(m - n). Is this proof direct or indirect?", "The conclusion follows directly from the starting assumptions.", TrialProofMethod.Direct);
        I(banks[0], "No greatest integer", "An argument assumes a greatest integer N and finds N + 1, an integer larger than N. Is this proof direct or indirect?", "It reaches a contradiction from the opposite of the claim.", TrialProofMethod.Contradiction);

        // Challenge 2: each direct argument has exactly five dependent steps.
        D(banks[1], "Even plus odd", "Prove directly that an even integer plus an odd integer is odd.", "Write both integers by parity, add, and factor the even part.",
            "Let a be even and b be odd.", "a = 2m and b = 2n + 1 for integers m,n.", "a + b = 2m + 2n + 1.", "a + b = 2(m + n) + 1.", "Hence a + b is odd.");
        D(banks[1], "Even product", "Prove directly that an even integer times any integer is even.", "Factoring the product as twice an integer proves evenness.",
            "Let a be even and b be any integer.", "a = 2m for some integer m.", "ab = (2m)b.", "ab = 2(mb), and mb is an integer.", "Hence ab is even.");
        D(banks[1], "Odd product", "Prove directly that the product of two odd integers is odd.", "The expanded product has the form twice an integer plus one.",
            "Let a and b be odd integers.", "a = 2m + 1 and b = 2n + 1.", "ab = 4mn + 2m + 2n + 1.", "ab = 2(2mn + m + n) + 1.", "Hence ab is odd.");
        D(banks[1], "Multiples of three", "Prove directly that the sum of two multiples of 3 is a multiple of 3.", "The sum retains a factor of 3.",
            "Let a and b be multiples of 3.", "a = 3m and b = 3n for integers m,n.", "a + b = 3m + 3n.", "a + b = 3(m + n), with m + n an integer.", "Hence 3 divides a + b.");
        D(banks[1], "Even difference", "Prove directly that the difference of two even integers is even.", "The difference is twice an integer.",
            "Let a and b be even integers.", "a = 2m and b = 2n for integers m,n.", "a - b = 2m - 2n.", "a - b = 2(m - n), with m - n an integer.", "Hence a - b is even.");
        D(banks[1], "Even square", "Prove directly that the square of an even integer is even.", "Its square still has a factor of 2.",
            "Let n be an even integer.", "n = 2k for an integer k.", "n² = (2k)² = 4k².", "n² = 2(2k²), with 2k² an integer.", "Hence n² is even.");
        D(banks[1], "Odd square", "Prove directly that the square of an odd integer is odd.", "The square has the form twice an integer plus one.",
            "Let n be an odd integer.", "n = 2k + 1 for an integer k.", "n² = 4k² + 4k + 1.", "n² = 2(2k² + 2k) + 1.", "Hence n² is odd.");
        D(banks[1], "Three even integers", "Prove directly that the sum of three even integers is even.", "Their sum has a common factor of 2.",
            "Let a, b, and c be even integers.", "a = 2i, b = 2j, and c = 2k.", "a + b + c = 2i + 2j + 2k.", "a + b + c = 2(i + j + k).", "Hence a + b + c is even.");
        D(banks[1], "Divisibility of sums", "If d divides both a and b, prove directly that d divides a + b.", "The same divisor factors from the sum.",
            "Assume d divides a and d divides b.", "Then a = dm and b = dn for integers m,n.", "a + b = dm + dn.", "a + b = d(m + n), with m + n an integer.", "Hence d divides a + b.");

        // Challenge 3: equivalent contrapositives, each with five parity steps.
        P(banks[2], "Odd square", "Prove: if n² is odd, then n is odd.", "Even n yields an even square, proving the contrapositive.",
            "Assume n is even.", "n = 2k for an integer k.", "n² = (2k)² = 4k².", "n² = 2(2k²), which is even.", "Therefore n² odd implies n odd.");
        P(banks[2], "Even cube", "Prove: if n³ is even, then n is even.", "Odd n yields an odd cube, proving the contrapositive.",
            "Assume n is odd.", "n = 2k + 1 for an integer k.", "n³ = 8k³ + 12k² + 6k + 1.", "n³ = 2(4k³ + 6k² + 3k) + 1, which is odd.", "Therefore n³ even implies n even.");
        P(banks[2], "Odd cube", "Prove: if n³ is odd, then n is odd.", "Even n yields an even cube, proving the contrapositive.",
            "Assume n is even.", "n = 2k for an integer k.", "n³ = (2k)³ = 8k³.", "n³ = 2(4k³), which is even.", "Therefore n³ odd implies n odd.");
        P(banks[2], "Even triple square", "Prove: if 3n² is even, then n is even.", "For odd n, 3n² is odd; this is the contrapositive.",
            "Assume n is odd.", "n = 2k + 1 for an integer k.", "3n² = 12k² + 12k + 3.", "3n² = 2(6k² + 6k + 1) + 1, which is odd.", "Therefore 3n² even implies n even.");
        P(banks[2], "Odd triple square", "Prove: if 3n² is odd, then n is odd.", "For even n, 3n² is even; this is the contrapositive.",
            "Assume n is even.", "n = 2k for an integer k.", "3n² = 3(2k)² = 12k².", "3n² = 2(6k²), which is even.", "Therefore 3n² odd implies n odd.");
        P(banks[2], "Even shifted square", "Prove: if n² + 2n is even, then n is even.", "For odd n, n² + 2n is odd.",
            "Assume n is odd.", "n = 2k + 1 for an integer k.", "n² + 2n = 4k² + 8k + 3.", "n² + 2n = 2(2k² + 4k + 1) + 1, which is odd.", "Therefore n² + 2n even implies n even.");
        P(banks[2], "Odd shifted square", "Prove: if n² + 4n is odd, then n is odd.", "For even n, n² + 4n is even.",
            "Assume n is even.", "n = 2k for an integer k.", "n² + 4n = 4k² + 8k.", "n² + 4n = 2(2k² + 4k), which is even.", "Therefore n² + 4n odd implies n odd.");
        P(banks[2], "Even spaced product", "Prove: if n(n + 2) is even, then n is even.", "For odd n, the two factors are odd and so is their product.",
            "Assume n is odd.", "n = 2k + 1 for an integer k.", "n(n + 2) = (2k + 1)(2k + 3) = 4k² + 8k + 3.", "n(n + 2) = 2(2k² + 4k + 1) + 1, which is odd.", "Therefore n(n + 2) even implies n even.");
        P(banks[2], "Odd spaced product", "Prove: if n(n + 4) is odd, then n is odd.", "For even n, the product is even.",
            "Assume n is even.", "n = 2k for an integer k.", "n(n + 4) = 2k(2k + 4).", "n(n + 4) = 2(k(2k + 4)), which is even.", "Therefore n(n + 4) odd implies n odd.");

        // Challenge 4: contradiction arguments with one explicit conflicting result.
        C(banks[3], "No least integer", "Prove that there is no least integer by contradiction.", "The supposed least integer has a still smaller integer predecessor.",
            "Assume a least integer N exists.", "N - 1 is also an integer.", "N - 1 < N.", "This contradicts N being the least integer.", "Therefore no least integer exists.");
        C(banks[3], "No greatest even integer", "Prove that there is no greatest even integer by contradiction.", "Adding 2 produces a larger even integer.",
            "Assume a greatest even integer E exists.", "E + 2 is also even.", "E + 2 > E.", "This contradicts E being the greatest even integer.", "Therefore no greatest even integer exists.");
        C(banks[3], "No least even integer", "Prove that there is no least even integer by contradiction.", "Subtracting 2 produces a smaller even integer.",
            "Assume a least even integer E exists.", "E - 2 is also even.", "E - 2 < E.", "This contradicts E being the least even integer.", "Therefore no least even integer exists.");
        C(banks[3], "No greatest odd integer", "Prove that there is no greatest odd integer by contradiction.", "Adding 2 produces a larger odd integer.",
            "Assume a greatest odd integer O exists.", "O + 2 is also odd.", "O + 2 > O.", "This contradicts O being the greatest odd integer.", "Therefore no greatest odd integer exists.");
        C(banks[3], "No least odd integer", "Prove that there is no least odd integer by contradiction.", "Subtracting 2 produces a smaller odd integer.",
            "Assume a least odd integer O exists.", "O - 2 is also odd.", "O - 2 < O.", "This contradicts O being the least odd integer.", "Therefore no least odd integer exists.");
        C(banks[3], "No greatest rational", "Prove that there is no greatest rational number by contradiction.", "Adding 1 produces a larger rational number.",
            "Assume a greatest rational number r exists.", "r + 1 is also rational.", "r + 1 > r.", "This contradicts r being the greatest rational.", "Therefore no greatest rational exists.");
        C(banks[3], "No smallest positive rational", "Prove that there is no smallest positive rational by contradiction.", "Halving a positive rational yields a smaller positive rational.",
            "Assume a smallest positive rational r exists.", "r/2 is rational and remains positive.", "0 < r/2 < r.", "This contradicts r being the smallest positive rational.", "Therefore no smallest positive rational exists.");
        C(banks[3], "Even and odd", "Prove by contradiction that no integer is both even and odd.", "The two parity forms would make an even number equal to 1.",
            "Assume an integer n is both even and odd.", "Then n = 2a = 2b + 1 for integers a,b.", "Thus 2(a - b) = 1.", "An even integer cannot equal 1: contradiction.", "Therefore no integer is both even and odd.");
        C(banks[3], "Consecutive evens", "Prove by contradiction that two consecutive integers cannot both be even.", "The successor of an even integer is odd.",
            "Assume n and n + 1 are both even.", "Since n is even, n = 2k for some integer k.", "Then n + 1 = 2k + 1 is odd.", "This contradicts n + 1 being even.", "Therefore consecutive integers cannot both be even.");

        // Challenge 5: choose Direct, then arrange a five-step derivation.
        F(banks[4], "Consecutive sum", "Prove that the sum of two consecutive integers is odd.", "The sum is twice an integer plus one.",
            "Let n and n + 1 be consecutive integers.", "Their sum is n + (n + 1).", "n + (n + 1) = 2n + 1.", "This has the form 2k + 1 with k = n.", "Therefore the sum is odd.");
        F(banks[4], "Multiple of four", "Prove that every multiple of 4 is even.", "A multiple of 4 is also a multiple of 2.",
            "Let n be a multiple of 4.", "n = 4k for some integer k.", "n = 2(2k).", "Because 2k is an integer, n is twice an integer.", "Therefore n is even.");
        F(banks[4], "Triple an odd number", "Prove that three times an odd integer is odd.", "The product has the form 2k + 1.",
            "Let n be an odd integer.", "n = 2k + 1 for some integer k.", "3n = 3(2k + 1) = 6k + 3.", "3n = 2(3k + 1) + 1.", "Therefore 3n is odd.");
        F(banks[4], "Two evens and one odd", "Prove that two even integers plus one odd integer sum to an odd integer.", "The sum is twice an integer plus one.",
            "Let a,b be even and c be odd.", "a = 2i, b = 2j, and c = 2k + 1.", "a + b + c = 2i + 2j + 2k + 1.", "a + b + c = 2(i + j + k) + 1.", "Therefore a + b + c is odd.");
        F(banks[4], "Divisibility of products", "If d divides a, prove that d divides ab for any integer b.", "The divisor remains a factor after multiplication.",
            "Assume d divides a, and let b be an integer.", "Then a = dk for some integer k.", "ab = (dk)b.", "ab = d(kb), where kb is an integer.", "Therefore d divides ab.");
        F(banks[4], "Odd successor", "Prove that the successor of an odd integer is even.", "Adding one to the odd form gives a factor of 2.",
            "Let n be an odd integer.", "n = 2k + 1 for some integer k.", "n + 1 = 2k + 2.", "n + 1 = 2(k + 1).", "Therefore n + 1 is even.");
        F(banks[4], "Even linear expression", "If n is even, prove that 3n + 2 is even.", "The expression factors into twice an integer.",
            "Let n be an even integer.", "n = 2k for some integer k.", "3n + 2 = 3(2k) + 2 = 6k + 2.", "3n + 2 = 2(3k + 1).", "Therefore 3n + 2 is even.");
        F(banks[4], "Multiples of five", "Prove that the difference of two multiples of 5 is a multiple of 5.", "The difference retains a factor of 5.",
            "Let a and b be multiples of 5.", "a = 5m and b = 5n for integers m,n.", "a - b = 5m - 5n.", "a - b = 5(m - n), with m - n an integer.", "Therefore 5 divides a - b.");
        F(banks[4], "Square of a multiple of three", "Prove that the square of a multiple of 3 is a multiple of 3.", "The square retains a factor of 3.",
            "Let n be a multiple of 3.", "n = 3k for some integer k.", "n² = (3k)² = 9k².", "n² = 3(3k²), with 3k² an integer.", "Therefore 3 divides n².");

        catalog.variantSets = new TrialProofVariantSet[5];
        for (int i = 0; i < banks.Length; i++)
        {
            if (banks[i].Count != 10) throw new InvalidOperationException("Challenge " + (i + 1) + " must have ten variants.");
            catalog.variantSets[i] = new TrialProofVariantSet { variants = banks[i].ToArray() };
        }
        Validate(catalog);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log("Direct and Indirect Proofs: authored and validated 10 variants for each of five challenges.");
    }

    private static void I(List<TrialProofChallenge> bank, string title, string problem, string explanation, TrialProofMethod method)
    {
        bank.Add(new TrialProofChallenge { kind = TrialProofKind.IdentifyMethod, title = title,
            problem = problem + " Activate the matching altar, then confirm at the Book Statue.",
            explanation = explanation, station = method, correctMethod = method });
    }

    private static void D(List<TrialProofChallenge> bank, string title, string problem, string explanation, params string[] steps)
        => AddSteps(bank, TrialProofKind.ArrangeSteps, TrialProofMethod.Direct, title, problem, explanation, steps);
    private static void P(List<TrialProofChallenge> bank, string title, string problem, string explanation, params string[] steps)
        => AddSteps(bank, TrialProofKind.ArrangeSteps, TrialProofMethod.Contrapositive, title, problem, explanation, steps);
    private static void C(List<TrialProofChallenge> bank, string title, string problem, string explanation, params string[] steps)
        => AddSteps(bank, TrialProofKind.ArrangeSteps, TrialProofMethod.Contradiction, title, problem, explanation, steps);
    private static void F(List<TrialProofChallenge> bank, string title, string problem, string explanation, params string[] steps)
        => AddSteps(bank, TrialProofKind.ChooseAndArrange, TrialProofMethod.Direct, title, problem, explanation, steps);

    private static void AddSteps(List<TrialProofChallenge> bank, TrialProofKind kind, TrialProofMethod method,
        string title, string problem, string explanation, string[] steps)
    {
        if (steps.Length != 5) throw new ArgumentException("Proof variants must have five steps.");
        int variant = bank.Count;
        string instruction = kind == TrialProofKind.ChooseAndArrange ?
            " Choose a strategy and arrange five steps on the trial altar." :
            " Arrange five steps on the active altar.";
        var tablets = new TrialProofTabletData[5];
        for (int i = 0; i < 5; i++)
            tablets[i] = new TrialProofTabletData { id = "proof-" + kind + "-" + method + "-v" + variant + "-" + i,
                statement = steps[i], justification = Justification(method, i) };
        bank.Add(new TrialProofChallenge { kind = kind, title = title, problem = problem + instruction,
            explanation = explanation, station = method, correctMethod = method,
            tablets = tablets, displayOrder = (int[])Shuffles[variant].Clone(),
            acceptedOrders = new[] { new TrialProofOrder { tabletIds = new[] { 0, 1, 2, 3, 4 } } } });
    }

    private static string Justification(TrialProofMethod method, int step)
    {
        if (method == TrialProofMethod.Contradiction)
            return new[] { "Assume the opposite of the claim.", "Construct a conflicting case.",
                "Compare it with the assumption.", "Identify the contradiction.", "Reject the assumption." }[step];
        if (method == TrialProofMethod.Contrapositive)
            return new[] { "Negate the original conclusion.", "Use the parity definition.",
                "Substitute and simplify.", "Identify the opposite parity.", "Conclude by contrapositive." }[step];
        return new[] { "Begin with the premise.", "Use a definition or given relation.",
            "Substitute and simplify.", "Rewrite the result in the required form.",
            "Apply the definition to conclude." }[step];
    }

    private static void Validate(DirectIndirectProofCatalog catalog)
    {
        TrialProofKind[] kinds = { TrialProofKind.IdentifyMethod, TrialProofKind.ArrangeSteps,
            TrialProofKind.ArrangeSteps, TrialProofKind.ArrangeSteps, TrialProofKind.ChooseAndArrange };
        TrialProofMethod[] stations = { TrialProofMethod.Direct, TrialProofMethod.Direct,
            TrialProofMethod.Contrapositive, TrialProofMethod.Contradiction, TrialProofMethod.Direct };
        for (int challenge = 0; challenge < 5; challenge++)
        {
            var variants = catalog.variantSets[challenge].variants;
            if (variants.Length != 10) throw new InvalidOperationException("Incorrect variant count.");
            for (int v = 0; v < variants.Length; v++)
            {
                TrialProofChallenge item = variants[v];
                if (item == null || item.kind != kinds[challenge] ||
                    string.IsNullOrWhiteSpace(item.title) || string.IsNullOrWhiteSpace(item.problem))
                    throw new InvalidOperationException("Invalid challenge " + challenge + " variant " + v);
                if (challenge == 0)
                {
                    TrialProofMethod wrongChoice = item.correctMethod == TrialProofMethod.Direct ?
                        TrialProofMethod.Contrapositive : TrialProofMethod.Direct;
                    if (!item.IsCorrect((int)item.correctMethod, Array.Empty<int>()) ||
                        item.IsCorrect((int)wrongChoice, Array.Empty<int>()))
                        throw new InvalidOperationException("Invalid classification answer.");
                    continue;
                }
                if (item.station != stations[challenge] || item.tablets == null || item.tablets.Length != 5 ||
                    item.displayOrder == null || item.displayOrder.Length != 5 ||
                    item.acceptedOrders == null || item.acceptedOrders.Length != 1)
                    throw new InvalidOperationException("Invalid five-step proof data.");
                var seen = new bool[5];
                foreach (int id in item.displayOrder)
                {
                    if (id < 0 || id >= 5 || seen[id]) throw new InvalidOperationException("Invalid shuffle.");
                    seen[id] = true;
                }
                int[] correct = { 0, 1, 2, 3, 4 };
                if (!item.IsCorrect((int)item.correctMethod, correct) ||
                    item.IsCorrect((int)item.correctMethod, item.displayOrder))
                    throw new InvalidOperationException("Invalid answer key.");
                if (item.kind == TrialProofKind.ChooseAndArrange &&
                    item.IsCorrect((int)TrialProofMethod.Contrapositive, correct))
                    throw new InvalidOperationException("Wrong strategy accepted.");
                foreach (TrialProofTabletData tablet in item.tablets)
                    if (tablet == null || string.IsNullOrWhiteSpace(tablet.statement) ||
                        string.IsNullOrWhiteSpace(tablet.justification))
                        throw new InvalidOperationException("Empty tablet.");
            }
        }
    }
}
