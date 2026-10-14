using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>Shared client-side name and room-chat filter for common abusive language.</summary>
public static class InappropriateTextFilter
{
    // Longer fragments catch joined words; short words stay exact to avoid censoring names
    // such as Scunthorpe, Boboy, Cocktail, Grape, and Fukushima.
    private static readonly string[] BlockedFragments =
    {
        "nigger", "nigga", "faggot", "faggy", "chink", "gook",
        "retard", "fuck", "fukyou", "bitch", "whore", "slut", "porn",
        "shit", "pussy", "asshole", "dumbass", "jackass",
        "rapist", "molest", "pedophile", "paedophile", "nazi", "hitler", "whitepower", "kkk",
        "putangina", "putanginamo", "punyeta", "pakyu", "tarantado", "bwisit",
        "gago", "tanga", "ulol"
    };

    private static readonly HashSet<string> BlockedWholeWords = new HashSet<string>(StringComparer.Ordinal)
    {
        "ass", "bastard", "damn", "sex", "suicide", "kys", "admin", "moderator",
        "official", "kike", "spic", "rape", "fuk", "cunt", "dick", "cock", "bobo"
    };

    private static readonly HashSet<string> BlockedExact = BuildExactSet();
    private static readonly Regex Words = new Regex(@"[\p{L}\p{M}\p{Nd}_@$!]+", RegexOptions.CultureInvariant);

    private static HashSet<string> BuildExactSet()
    {
        var terms = new HashSet<string>(BlockedWholeWords, StringComparer.Ordinal);
        foreach (string fragment in BlockedFragments) terms.Add(fragment);
        return terms;
    }

    public static bool IsBlockedName(string value) => ContainsBlocked(Fold(value));

    public static string Censor(string message)
    {
        if (string.IsNullOrEmpty(message)) return message ?? string.Empty;
        MatchCollection matches = Words.Matches(message);
        var tokens = new List<Match>(matches.Count);
        var folded = new List<string>(matches.Count);
        var mask = new bool[message.Length];

        foreach (Match match in matches)
        {
            tokens.Add(match);
            string word = Fold(match.Value);
            folded.Add(word);
            if (ContainsBlocked(word)) Mark(mask, match.Index, match.Index + match.Length);
        }

        // Catch words split by punctuation or spaces, such as f.u.c.k or putang ina.
        for (int start = 0; start < tokens.Count; start++)
        {
            var joined = new StringBuilder();
            for (int end = start; end < tokens.Count && end < start + 12; end++)
            {
                joined.Append(folded[end]);
                if (end > start && BlockedExact.Contains(joined.ToString()))
                    Mark(mask, tokens[start].Index, tokens[end].Index + tokens[end].Length);
            }
        }

        char[] result = message.ToCharArray();
        for (int i = 0; i < result.Length; i++)
            if (mask[i] && !char.IsWhiteSpace(result[i])) result[i] = '*';
        return new string(result);
    }

    private static void Mark(bool[] mask, int start, int end)
    {
        for (int i = start; i < end; i++) mask[i] = true;
    }

    private static bool ContainsBlocked(string candidate)
    {
        if (BlockedWholeWords.Contains(candidate)) return true;
        foreach (string fragment in BlockedFragments)
            if (candidate.Contains(fragment)) return true;
        return false;
    }

    private static string Fold(string value)
    {
        string decomposed;
        try { decomposed = (value ?? string.Empty).Normalize(NormalizationForm.FormKD).ToLowerInvariant(); }
        catch (ArgumentException) { return string.Empty; }
        var folded = new StringBuilder(decomposed.Length);
        foreach (char c in decomposed)
        {
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (c == '_' || category == UnicodeCategory.NonSpacingMark ||
                category == UnicodeCategory.SpacingCombiningMark) continue;
            switch (c)
            {
                case '0': folded.Append('o'); break;
                case '1': folded.Append('i'); break;
                case '3': folded.Append('e'); break;
                case '4': folded.Append('a'); break;
                case '5': folded.Append('s'); break;
                case '7': folded.Append('t'); break;
                case '8': folded.Append('b'); break;
                case '@': folded.Append('a'); break;
                case '$': folded.Append('s'); break;
                case '!': folded.Append('i'); break;
                default: folded.Append(c); break;
            }
        }
        return folded.ToString();
    }
}
