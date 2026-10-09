// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Text.RegularExpressions;

// namespace LogicLegends.Inference
// {
//     public enum RuleOfInferenceType
//     {
//         ModusPonens, ModusTollens, HypotheticalSyllogism, DisjunctiveSyllogism,
//         ConstructiveDilemma, Simplification, Conjunction, Addition, DoubleNegation, Tautology
//     }
//     public sealed class PuzzlePart
//     {
//         public readonly string Text;
//         public readonly bool IsBlank;
//         public PuzzlePart(string text, bool blank = false) { Text = text; IsBlank = blank; }
//     }
//     /// <summary>Sentence blanks and hidden rule/conclusion derived from the existing curriculum data.</summary>
//     public sealed class RuleOfInferencePuzzle
//     {
//         public readonly InferenceQuestion Source;
//         public readonly RuleOfInferenceType RuleType;
//         public readonly List<List<PuzzlePart>> Premises = new List<List<PuzzlePart>>();
//         public readonly List<string> CorrectPlacements = new List<string>();
//         public readonly string[] LogicalPremises;
//         public string Id => Source.Id;
//         public string Conclusion { get; private set; }
//         public string RuleLabel => Source.Rule.ruleName + " (" + Source.Rule.abbreviation + ")";
//         public static RuleOfInferenceType TypeOf(InferenceRule rule)
//         {
//             string[] codes = { "MP", "MT", "HS", "DS", "CD", "SIMP", "CONJ", "ADD", "DN", "TAU" };
//             int index = Array.IndexOf(codes, rule.abbreviation);
//             if (index < 0) throw new ArgumentException("Unknown rule: " + rule.abbreviation);
//             return (RuleOfInferenceType)index;
//         }
//         public RuleOfInferencePuzzle(InferenceQuestion source)
//         {
//             Source = source; RuleType = TypeOf(source.Rule);
//             LogicalPremises = source.Form.premises.Select(p => string.Join(" ", p.tokens.Where(t => t != "\n"))
//                 .Replace("{", "").Replace("}", "").Replace("[", "").Replace("]", "")).ToArray();
//             var s = source.Example.statements;
//             string p = s[0], q = s[1], r = s[2], t = s[3];
//             switch (RuleType)
//             {
//                 case RuleOfInferenceType.ModusPonens:
//                     Conditional(p, q); Assertion(p, false); Conclusion = q; break;
//                 case RuleOfInferenceType.ModusTollens:
//                     Conditional(p, q); NegatedAssertion(q, true); Conclusion = Negate(p); break;
//                 case RuleOfInferenceType.HypotheticalSyllogism:
//                     Conditional(p, q); Conditional(q, r); Conclusion = "If " + Lower(p) + ", then " + Lower(r); break;
//                 case RuleOfInferenceType.DisjunctiveSyllogism:
//                     Joined(p, "or", q, true); NegatedAssertion(p, true); Conclusion = q; break;
//                 case RuleOfInferenceType.ConstructiveDilemma:
//                     var cd = NewRow(); Fixed(cd, "("); ConditionalParts(cd, p, q);
//                     Fixed(cd, ") and ("); ConditionalParts(cd, r, t); Fixed(cd, ").");
//                     Joined(p, "or", r, true); Conclusion = q + " or " + Lower(t); break;
//                 case RuleOfInferenceType.Simplification:
//                     Joined(p, "and", q, true); Conclusion = p; break;
//                 case RuleOfInferenceType.Conjunction:
//                     Assertion(p, true); Assertion(q, true); Conclusion = p + " and " + Lower(q); break;
//                 case RuleOfInferenceType.Addition:
//                     Assertion(p, true); Conclusion = p + " or " + Lower(q); break;
//                 case RuleOfInferenceType.DoubleNegation:
//                     if (source.Form.premises[0].tokens.Contains("[NOT]"))
//                     {
//                         var dn = NewRow(); Blank(dn, "It"); Blank(dn, "is");
//                         Fixed(dn, "not the case that it is not the case that " + Lower(p) + "."); Conclusion = p;
//                     }
//                     else { Assertion(p, true); Conclusion = "It is not the case that " + Lower(Negate(p)); }
//                     break;
//                 default:
//                     Joined(p, source.Form.premises[0].tokens.Contains("[AND]") ? "and" : "or", p, true);
//                     Conclusion = p; break;
//             }
//             Conclusion = Conclusion.TrimEnd('.', ' ') + ".";
//         }

//         List<PuzzlePart> NewRow() { var row = new List<PuzzlePart>(); Premises.Add(row); return row; }
//         void Blank(List<PuzzlePart> row, string word) { row.Add(new PuzzlePart(word, true)); CorrectPlacements.Add(word); }
//         static void Fixed(List<PuzzlePart> row, string text) { row.Add(new PuzzlePart(text)); }
        
//         void Conditional(string a, string b) { var row = NewRow(); ConditionalParts(row, a, b); EndSentence(row); }
//         void ConditionalParts(List<PuzzlePart> row, string a, string b)
//         {
//             Blank(row, "If"); Fixed(row, Lower(a) + ","); Blank(row, "then"); Fixed(row, Lower(b));
//         }

//         void Assertion(string text, bool blankVerb)
//         {
//             var row = NewRow();
//             if (blankVerb)
//             {
//                 var auxiliary = Regex.Match(text, @"\b(is|are|can|will|do|does)\b");
//                 if (auxiliary.Success)
//                 {
//                     if (auxiliary.Index > 0) Fixed(row, text.Substring(0, auxiliary.Index).Trim());
//                     Blank(row, auxiliary.Value); // Blanks the verb (is, are, will)
//                     Fixed(row, text.Substring(auxiliary.Index + auxiliary.Length).Trim());
//                 }
//                 else Fixed(row, text);
//             }
//             else Fixed(row, text);
//             EndSentence(row);
//         }

//         void NegatedAssertion(string text, bool blankNot)
//         {
//             var row = NewRow();
//             var copula = Regex.Match(text, @"\b(is|are|was|were|can|will)\b");
//             if (copula.Success)
//             {
//                 Fixed(row, text.Substring(0, copula.Index + copula.Length).Trim());
//                 if (blankNot) Blank(row, "not"); else Fixed(row, "not");
//                 Fixed(row, text.Substring(copula.Index + copula.Length).Trim());
//             }
//             else if (text.StartsWith("I "))
//             {
//                 Fixed(row, "I do");
//                 if (blankNot) Blank(row, "not"); else Fixed(row, "not");
//                 Fixed(row, text.Substring(2).Trim());
//             }
//             else
//             {
//                 var words = text.Split(' ');
//                 var verbs = new Dictionary<string, string> { { "studies", "study" }, { "passes", "pass" },
//                     { "practices", "practice" }, { "solves", "solve" }, { "wakes", "wake" },
//                     { "exercises", "exercise" }, { "stays", "stay" }, { "drinks", "drink" }, { "grows", "grow" } };
//                 bool found = false;
//                 for (int i = 1; i < words.Length; i++)
//                 {
//                     if (verbs.TryGetValue(words[i], out string verb))
//                     {
//                         Fixed(row, string.Join(" ", words.Take(i)) + " does");
//                         if (blankNot) Blank(row, "not"); else Fixed(row, "not");
//                         Fixed(row, verb + (i + 1 < words.Length ? " " + string.Join(" ", words.Skip(i + 1)) : ""));
//                         found = true; break;
//                     }
//                 }
//                 if (!found)
//                 {
//                     if (blankNot) { Blank(row, "It"); Blank(row, "is"); Blank(row, "not"); Fixed(row, "the case that " + Lower(text)); }
//                     else Fixed(row, "It is not the case that " + Lower(text));
//                 }
//             }
//             EndSentence(row);
//         }

//         void Joined(string a, string connector, string b, bool blankConnector)
//         {
//             var row = NewRow();
//             Fixed(row, a);
//             if (blankConnector) Blank(row, connector); else Fixed(row, connector);
//             Fixed(row, Lower(b));
//             EndSentence(row);
//         }

//         static void EndSentence(List<PuzzlePart> row)
//         {
//             var last = row[row.Count - 1];
//             if (last.IsBlank) Fixed(row, ".");
//             else row[row.Count - 1] = new PuzzlePart(last.Text + ".");
//         }

//         static string Lower(string text)
//         {
//             if (text.StartsWith("It is ")) return "it's " + text.Substring(6);
//             if (text.StartsWith("The ")) return "the " + text.Substring(4);
//             return text;
//         }

//         static string Negate(string text)
//         {
//             var copula = Regex.Match(text, @"\b(is|are|was|were|can|will)\b");
//             if (copula.Success) return text.Insert(copula.Index + copula.Length, " not");
//             if (text.StartsWith("I ")) return "I do not " + text.Substring(2);
//             var words = text.Split(' ');
//             var verbs = new Dictionary<string, string> { { "studies", "study" }, { "passes", "pass" },
//                 { "practices", "practice" }, { "solves", "solve" }, { "wakes", "wake" },
//                 { "exercises", "exercise" }, { "stays", "stay" }, { "drinks", "drink" }, { "grows", "grow" } };
//             for (int i = 1; i < words.Length; i++)
//                 if (verbs.TryGetValue(words[i], out string verb))
//                     return string.Join(" ", words.Take(i)) + " does not " + verb + (i + 1 < words.Length ? " " + string.Join(" ", words.Skip(i + 1)) : "");
//             return "It is not the case that " + Lower(text);
//         }
//     }
//     public enum ArgumentCheck { Correct, IncompleteOrIncorrect, NoRuleSelected, WrongRule }
//     public static class ArgumentValidator
//     {
//         public static bool PremisesMatch(RuleOfInferencePuzzle puzzle, IReadOnlyList<string> placements)
//         {
//             if (puzzle == null || placements == null || placements.Count != puzzle.CorrectPlacements.Count) return false;
//             for (int i = 0; i < placements.Count; i++)
//                 if (!string.Equals(placements[i], puzzle.CorrectPlacements[i], StringComparison.Ordinal)) return false;
//             return true;
//         }
//         public static ArgumentCheck Check(RuleOfInferencePuzzle puzzle, IReadOnlyList<string> placements, RuleOfInferenceType? selected)
//         {
//             if (!PremisesMatch(puzzle, placements)) return ArgumentCheck.IncompleteOrIncorrect;
//             if (!selected.HasValue) return ArgumentCheck.NoRuleSelected;
//             return selected.Value == puzzle.RuleType ? ArgumentCheck.Correct : ArgumentCheck.WrongRule;
//         }
//     }
// }


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace LogicLegends.Inference
{
    public enum RuleOfInferenceType
    {
        ModusPonens, ModusTollens, HypotheticalSyllogism, DisjunctiveSyllogism,
        ConstructiveDilemma, Simplification, Conjunction, Addition, DoubleNegation, Tautology
    }
    public sealed class PuzzlePart
    {
        public readonly string Text;
        public readonly bool IsBlank;
        public PuzzlePart(string text, bool blank = false) { Text = text; IsBlank = blank; }
    }
    
    public sealed class RuleOfInferencePuzzle
    {
        public readonly InferenceQuestion Source;
        public readonly RuleOfInferenceType RuleType;
        public readonly List<List<PuzzlePart>> Premises = new List<List<PuzzlePart>>();
        public readonly List<string> CorrectPlacements = new List<string>();
        public readonly string[] LogicalPremises;
        public string Id => Source.Id;
        public string Conclusion { get; private set; }
        public string RuleLabel => Source.Rule.ruleName + " (" + Source.Rule.abbreviation + ")";

        public static RuleOfInferenceType TypeOf(InferenceRule rule)
        {
            string[] codes = { "MP", "MT", "HS", "DS", "CD", "SIMP", "CONJ", "ADD", "DN", "TAU" };
            int index = Array.IndexOf(codes, rule.abbreviation);
            if (index < 0) throw new ArgumentException("Unknown rule: " + rule.abbreviation);
            return (RuleOfInferenceType)index;
        }

        public RuleOfInferencePuzzle(InferenceQuestion source)
        {
            Source = source; RuleType = TypeOf(source.Rule);
            LogicalPremises = source.Form.premises.Select(p => string.Join(" ", p.tokens.Where(t => t != "\n"))
                .Replace("{", "").Replace("}", "").Replace("[", "").Replace("]", "")).ToArray();
            
            var s = source.Example.statements;
            string p = s[0], q = s[1], r = s[2], t = s[3];
            
            switch (RuleType)
            {
                case RuleOfInferenceType.ModusPonens:
                    Conditional(p, q); Assertion(p, false); Conclusion = q; break;
                case RuleOfInferenceType.ModusTollens:
                    Conditional(p, q); NegatedAssertion(q, true); Conclusion = Negate(p); break;
                case RuleOfInferenceType.HypotheticalSyllogism:
                    Conditional(p, q); Conditional(q, r); Conclusion = "If " + Lower(p) + ", then " + Lower(r); break;
                case RuleOfInferenceType.DisjunctiveSyllogism:
                    Joined(p, "or", q, true); NegatedAssertion(p, true); Conclusion = q; break;
                case RuleOfInferenceType.ConstructiveDilemma:
                    var cd = NewRow(); Fixed(cd, "("); ConditionalParts(cd, p, q);
                    Fixed(cd, ") and ("); ConditionalParts(cd, r, t); Fixed(cd, ").");
                    Joined(p, "or", r, true); Conclusion = q + " or " + Lower(t); break;
                case RuleOfInferenceType.Simplification:
                    Joined(p, "and", q, true); Conclusion = p; break;
                case RuleOfInferenceType.Conjunction:
                    Assertion(p, true); Assertion(q, true); Conclusion = p + " and " + Lower(q); break;
                case RuleOfInferenceType.Addition:
                    Assertion(p, true); Conclusion = p + " or " + Lower(q); break;
                case RuleOfInferenceType.DoubleNegation:
                    if (source.Form.premises[0].tokens.Contains("[NOT]"))
                    {
                        var dn = NewRow(); Blank(dn, "It"); Blank(dn, "is");
                        Fixed(dn, "not true that " + Lower(Negate(p)) + "."); Conclusion = p;
                    }
                    else { Assertion(p, true); Conclusion = "It is not true that " + Lower(Negate(p)); }
                    break;
                default:
                    Joined(p, source.Form.premises[0].tokens.Contains("[AND]") ? "and" : "or", p, true);
                    Conclusion = p; break;
            }
            Conclusion = Conclusion.TrimEnd('.', ' ') + ".";
        }

        List<PuzzlePart> NewRow() { var row = new List<PuzzlePart>(); Premises.Add(row); return row; }
        void Blank(List<PuzzlePart> row, string word) { row.Add(new PuzzlePart(word, true)); CorrectPlacements.Add(word); }
        static void Fixed(List<PuzzlePart> row, string text) { row.Add(new PuzzlePart(text)); }
        
        void Conditional(string a, string b) { var row = NewRow(); ConditionalParts(row, a, b); EndSentence(row); }
        
        void ConditionalParts(List<PuzzlePart> row, string a, string b)
        {
            string cleanA = a.Replace(".", "").Replace(",", "").Trim();
            string cleanB = b.Replace(".", "").Replace(",", "").Trim();
            Blank(row, "If"); Fixed(row, Lower(cleanA) + ","); Blank(row, "then"); Fixed(row, Lower(cleanB));
        }

        void Assertion(string text, bool blankVerb)
        {
            var row = NewRow();
            string cleanText = text.Replace(".", "").Replace(",", "").Trim();
            if (blankVerb)
            {
                var auxiliary = Regex.Match(cleanText, @"\b(is|are|can|will|do|does)\b");
                if (auxiliary.Success)
                {
                    if (auxiliary.Index > 0) Fixed(row, cleanText.Substring(0, auxiliary.Index).Trim());
                    Blank(row, auxiliary.Value); 
                    Fixed(row, cleanText.Substring(auxiliary.Index + auxiliary.Length).Trim());
                }
                else Fixed(row, cleanText);
            }
            else Fixed(row, cleanText);
            EndSentence(row);
        }

        void NegatedAssertion(string text, bool blankNot)
        {
            var row = NewRow();
            string cleanText = text.Replace(".", "").Replace(",", "").Trim();
            var copula = Regex.Match(cleanText, @"\b(is|are|was|were|can|will)\b");
            if (copula.Success)
            {
                Fixed(row, cleanText.Substring(0, copula.Index + copula.Length).Trim());
                if (blankNot) Blank(row, "not"); else Fixed(row, "not");
                Fixed(row, cleanText.Substring(copula.Index + copula.Length).Trim());
            }
            else if (cleanText.StartsWith("I "))
            {
                Fixed(row, "I do");
                if (blankNot) Blank(row, "not"); else Fixed(row, "not");
                Fixed(row, cleanText.Substring(2).Trim());
            }
            else
            {
                var words = cleanText.Split(' ');
                var verbs = new Dictionary<string, string> { { "studies", "study" }, { "passes", "pass" },
                    { "practices", "practice" }, { "solves", "solve" }, { "wakes", "wake" },
                    { "exercises", "exercise" }, { "stays", "stay" }, { "drinks", "drink" }, { "grows", "grow" } };
                bool found = false;
                for (int i = 1; i < words.Length; i++)
                {
                    string word = words[i].ToLowerInvariant();
                    if (verbs.TryGetValue(word, out string verb))
                    {
                        Fixed(row, string.Join(" ", words.Take(i)) + " does");
                        if (blankNot) Blank(row, "not"); else Fixed(row, "not");
                        Fixed(row, verb + (i + 1 < words.Length ? " " + string.Join(" ", words.Skip(i + 1)) : ""));
                        found = true; break;
                    }
                }
                if (!found)
                {
                    if (blankNot) { Blank(row, "It"); Blank(row, "is"); Blank(row, "not"); Fixed(row, "true that " + Lower(cleanText)); }
                    else Fixed(row, "It is not true that " + Lower(cleanText));
                }
            }
            EndSentence(row);
        }

        void Joined(string a, string connector, string b, bool blankConnector)
        {
            var row = NewRow();
            string cleanA = a.Replace(".", "").Replace(",", "").Trim();
            string cleanB = b.Replace(".", "").Replace(",", "").Trim();
            Fixed(row, cleanA);
            if (blankConnector) Blank(row, connector); else Fixed(row, connector);
            Fixed(row, Lower(cleanB));
            EndSentence(row);
        }

        static void EndSentence(List<PuzzlePart> row)
        {
            var last = row[row.Count - 1];
            if (last.IsBlank) Fixed(row, ".");
            else if (!last.Text.EndsWith(".")) row[row.Count - 1] = new PuzzlePart(last.Text + ".");
        }

        static string Lower(string text)
        {
            if (text.StartsWith("It is ")) return "it's " + text.Substring(6);
            if (text.StartsWith("The ")) return "the " + text.Substring(4);
            return text;
        }

        static string Negate(string text)
        {
            string cleanText = text.Replace(".", "").Replace(",", "").Trim();
            var copula = Regex.Match(cleanText, @"\b(is|are|was|were|can|will)\b");
            if (copula.Success) return cleanText.Insert(copula.Index + copula.Length, " not");
            if (cleanText.StartsWith("I ")) return "I do not " + cleanText.Substring(2);
            var words = cleanText.Split(' ');
            var verbs = new Dictionary<string, string> { { "studies", "study" }, { "passes", "pass" },
                { "practices", "practice" }, { "solves", "solve" }, { "wakes", "wake" },
                { "exercises", "exercise" }, { "stays", "stay" }, { "drinks", "drink" }, { "grows", "grow" } };
            for (int i = 1; i < words.Length; i++)
            {
                string word = words[i].ToLowerInvariant();
                if (verbs.TryGetValue(word, out string verb))
                    return string.Join(" ", words.Take(i)) + " does not " + verb + (i + 1 < words.Length ? " " + string.Join(" ", words.Skip(i + 1)) : "");
            }
            return "It is not true that " + Lower(cleanText);
        }
    }

    public enum ArgumentCheck { Correct, IncompleteOrIncorrect, NoRuleSelected, WrongRule }
    public static class ArgumentValidator
    {
        public static bool PremisesMatch(RuleOfInferencePuzzle puzzle, IReadOnlyList<string> placements)
        {
            if (puzzle == null || placements == null || placements.Count != puzzle.CorrectPlacements.Count) return false;
            for (int i = 0; i < placements.Count; i++)
                if (!string.Equals(placements[i], puzzle.CorrectPlacements[i], StringComparison.Ordinal)) return false;
            return true;
        }
        public static ArgumentCheck Check(RuleOfInferencePuzzle puzzle, IReadOnlyList<string> placements, RuleOfInferenceType? selected)
        {
            if (!PremisesMatch(puzzle, placements)) return ArgumentCheck.IncompleteOrIncorrect;
            if (!selected.HasValue) return ArgumentCheck.NoRuleSelected;
            return selected.Value == puzzle.RuleType ? ArgumentCheck.Correct : ArgumentCheck.WrongRule;
        }
    }
}