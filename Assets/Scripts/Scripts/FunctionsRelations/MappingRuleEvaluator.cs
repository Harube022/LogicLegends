using System.Collections.Generic;

// Pure rule checks shared by every question type. In particular, an arbitrary relation
// can have several arrows from one domain node, while a function cannot.
public static class MappingRuleEvaluator
{
    public static bool HasValidEndpoints(IEnumerable<MappingPair> pairs, IEnumerable<string> domain, IEnumerable<string> codomain)
    {
        if (pairs == null || domain == null || codomain == null) return false;
        var sources = new HashSet<string>(domain);
        var targets = new HashSet<string>(codomain);
        var seen = new HashSet<MappingPair>();
        foreach (var pair in pairs)
            if (!sources.Contains(pair.domain) || !targets.Contains(pair.codomain) || !seen.Add(pair)) return false;
        return true;
    }

    public static bool IsFunction(IEnumerable<MappingPair> pairs, IEnumerable<string> domain)
    {
        if (pairs == null || domain == null) return false;
        var outputs = new Dictionary<string, HashSet<string>>();
        foreach (var input in domain) outputs[input] = new HashSet<string>();
        foreach (var pair in pairs)
        {
            if (!outputs.ContainsKey(pair.domain)) return false;
            outputs[pair.domain].Add(pair.codomain);
        }
        foreach (var output in outputs.Values) if (output.Count != 1) return false;
        return outputs.Count > 0;
    }

    public static bool IsInjective(IEnumerable<MappingPair> pairs, IEnumerable<string> domain)
    {
        if (!IsFunction(pairs, domain)) return false;
        var recipients = new HashSet<string>();
        foreach (var pair in pairs) if (!recipients.Add(pair.codomain)) return false;
        return true;
    }

    public static bool IsSurjective(IEnumerable<MappingPair> pairs, IEnumerable<string> codomain)
    {
        if (pairs == null || codomain == null) return false;
        var required = new HashSet<string>(codomain);
        foreach (var pair in pairs) required.Remove(pair.codomain);
        return required.Count == 0;
    }

    public static bool IsBijective(IEnumerable<MappingPair> pairs, IEnumerable<string> domain, IEnumerable<string> codomain)
        => IsInjective(pairs, domain) && IsSurjective(pairs, codomain);

    public static bool IsManyToOne(IEnumerable<MappingPair> pairs, IEnumerable<string> domain)
    {
        if (!IsFunction(pairs, domain)) return false;
        var recipients = new HashSet<string>();
        foreach (var pair in pairs) if (!recipients.Add(pair.codomain)) return true;
        return false;
    }

    public static MappingProperty Properties(IEnumerable<MappingPair> pairs, IEnumerable<string> domain, IEnumerable<string> codomain)
    {
        MappingProperty result = MappingProperty.None;
        if (IsFunction(pairs, domain)) result |= MappingProperty.Function;
        if (IsInjective(pairs, domain)) result |= MappingProperty.Injective;
        if (IsSurjective(pairs, codomain)) result |= MappingProperty.Surjective;
        if (IsBijective(pairs, domain, codomain)) result |= MappingProperty.Bijective;
        if (IsManyToOne(pairs, domain)) result |= MappingProperty.ManyToOne;
        return result;
    }

    public static bool ExactPairs(IEnumerable<MappingPair> actual, IEnumerable<MappingPair> expected)
    {
        if (actual == null || expected == null) return false;
        var a = new HashSet<MappingPair>(actual);
        var e = new HashSet<MappingPair>(expected);
        return a.SetEquals(e);
    }

    public static bool MeetsRules(IEnumerable<MappingPair> pairs, MappingChallengeData challenge)
    {
        if (challenge == null || !HasValidEndpoints(pairs, challenge.domain, challenge.codomain)) return false;
        if (challenge.requireFunction && !IsFunction(pairs, challenge.domain)) return false;
        if (challenge.requireInjective && !IsInjective(pairs, challenge.domain)) return false;
        if (challenge.requireSurjective && !IsSurjective(pairs, challenge.codomain)) return false;
        return true;
    }
}
