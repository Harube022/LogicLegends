using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace LogicLegends.Inference
{
    /// <summary>Fifty structural questions, one random question per difficulty bank.</summary>
    public static class InferenceStageQuestions
    {
        public const int ChallengeCount = 5;
        // Disjoint correct-rule families guarantee a compatible five-question
        // playthrough. Other curriculum rules remain available as distractors.
        public static readonly InferenceQuestionSpec[][] Banks = {
            Bank("SIMP", 1,
                "p&q;p", "p&q;q", "(p&q)&r;p&q", "p&(q&r);q&r", "!p&q;!p",
                "p&!q;!q", "!p&!q;!p", "(p|q)&r;p|q", "p&(q|r);q|r", "(p->q)&r;p->q"),
            Bank("MP", 2,
                "p->q;p;q", "p->!q;p;!q", "!p->q;!p;q", "(p&q)->r;p&q;r", "(p|q)->r;p|q;r",
                "p->(q&r);p;q&r", "p->(q|r);p;q|r", "!p->!q;!p;!q", "(p->q)->r;p->q;r", "p->(q->r);p;q->r"),
            Bank("MT", 3,
                "p->q;!q;!p", "p->!q;!!q;!p", "!p->q;!q;!!p", "(p&q)->r;!r;!(p&q)", "(p|q)->r;!r;!(p|q)",
                "p->(q&r);!(q&r);!p", "p->(q|r);!(q|r);!p", "(!p&q)->r;!r;!(!p&q)", "(p->q)->r;!r;!(p->q)", "p->(q->r);!(q->r);!p"),
            Bank("HS", 4,
                "p->q;q->r;p->r", "p->q;q->!r;p->!r", "!p->q;q->r;!p->r", "(p&q)->r;r->s;(p&q)->s", "(p|q)->r;r->s;(p|q)->s",
                "p->(q&r);(q&r)->s;p->s", "p->(q|r);(q|r)->s;p->s", "p->q;q->r;r->s;p->s", "p->!q;!q->r;p->r", "(p->q)->r;r->s;(p->q)->s"),
            Bank("CD", 5,
                "p->q;r->s;p|r;q|s", "p->!q;r->s;p|r;!q|s", "!p->q;r->s;!p|r;q|s", "p->q;!r->s;p|!r;q|s", "p->!q;r->!s;p|r;!q|!s",
                "(p&q)->r;!p->s;(p&q)|!p;r|s", "(p|q)->r;!p->s;(p|q)|!p;r|s", "p->(q&r);!p->s;p|!p;(q&r)|s",
                "(p->q)->r;!p->s;(p->q)|!p;r|s", "(p->q)&(r->s);p|r;q|s")
        };

        static InferenceQuestionSpec[] Bank(string rule, int difficulty, params string[] questions)
        {
            return questions.Select((q, i) => new InferenceQuestionSpec(rule, difficulty, i, q.Split(';'))).ToArray();
        }

        public static InferenceQuestion[] Select(InferenceRule[] rules, int seed)
        {
            var random = new Random(seed);
            var selected = new List<InferenceQuestion>(); var used = new HashSet<string>();
            foreach (var bank in Banks)
            {
                var compatible = bank.Where(q => !used.Contains(q.RuleCode)).ToArray();
                if (compatible.Length == 0) throw new InvalidOperationException("No compatible inference question remains.");
                var spec = compatible[random.Next(compatible.Length)];
                var rule = rules.FirstOrDefault(r => r != null && r.abbreviation == spec.RuleCode);
                if (rule == null || rule.examples == null || rule.examples.Length == 0)
                    throw new InvalidOperationException("Missing curriculum rule/examples: " + spec.RuleCode);
                used.Add(spec.RuleCode);
                selected.Add(new InferenceQuestion(rule, spec, rule.examples[random.Next(rule.examples.Length)]));
            }
            return selected.ToArray();
        }
    }

    public sealed class InferenceQuestionSpec
    {
        public readonly string RuleCode, Key;
        public readonly int Difficulty;
        public readonly string[] Premises;
        public readonly string Conclusion;
        public InferenceQuestionSpec(string rule, int difficulty, int index, string[] expressions)
        {
            RuleCode = rule; Difficulty = difficulty; Key = difficulty + ":" + index;
            Premises = expressions.Take(expressions.Length - 1).ToArray(); Conclusion = expressions.Last();
        }
        public string[] Sentences(InferenceExample example) => Premises.Select(p => InferenceFormula.Parse(p).Sentence(example)).ToArray();
        public string ConclusionSentence(InferenceExample example) => InferenceFormula.Parse(Conclusion).Sentence(example);
        public bool IsValid()
        {
            var premises = Premises.Select(InferenceFormula.Parse).ToArray(); var result = InferenceFormula.Parse(Conclusion);
            bool satisfiable = false;
            for (int bits = 0; bits < 16; bits++)
                if (premises.All(p => p.Evaluate(bits))) { satisfiable = true; if (!result.Evaluate(bits)) return false; }
            return satisfiable;
        }
        public void BuildPremises(List<List<PuzzlePart>> rows, List<string> answers, InferenceExample example)
        {
            int blanks = 0, limit = Difficulty == 5 ? 8 : Difficulty;
            foreach (var sentence in Sentences(example))
            {
                var row = new List<PuzzlePart>(); int start = 0;
                // Preserve the existing draggable-word flow. Increasing levels
                // involve more connectors, negations, and compound statements.
                foreach (Match word in Regex.Matches(sentence, @"\b(if|then|and|or|not)\b", RegexOptions.IgnoreCase))
                {
                    if (blanks >= limit) break;
                    if (word.Index > start) row.Add(new PuzzlePart(sentence.Substring(start, word.Index - start)));
                    row.Add(new PuzzlePart(word.Value, true)); answers.Add(word.Value); blanks++;
                    start = word.Index + word.Length;
                }
                row.Add(new PuzzlePart(sentence.Substring(start) + ".")); rows.Add(row);
            }
            if (answers.Count == 0) throw new InvalidOperationException("Question has no premise practice.");
        }
    }

    /// <summary>Small propositional AST: parentheses, negation, and/or/implication.</summary>
    public sealed class InferenceFormula
    {
        readonly char op; readonly InferenceFormula left, right;
        InferenceFormula(char operation, InferenceFormula a = null, InferenceFormula b = null) { op = operation; left = a; right = b; }
        public bool Evaluate(int bits)
        {
            if (op >= 'p' && op <= 's') return (bits & (1 << (op - 'p'))) != 0;
            if (op == '!') return !left.Evaluate(bits);
            if (op == '&') return left.Evaluate(bits) && right.Evaluate(bits);
            if (op == '|') return left.Evaluate(bits) || right.Evaluate(bits);
            return !left.Evaluate(bits) || right.Evaluate(bits);
        }
        public string Sentence(InferenceExample example)
        {
            if (op >= 'p' && op <= 's')
            {
                string text = example.statements[op - 'p'].Trim().TrimEnd('.');
                return char.ToLowerInvariant(text[0]) + text.Substring(1);
            }
            if (op == '!') return "it is not true that (" + left.Sentence(example) + ")";
            if (op == '>') return "if (" + left.Sentence(example) + "), then (" + right.Sentence(example) + ")";
            return "(" + left.Sentence(example) + (op == '&' ? " and " : " or ") + right.Sentence(example) + ")";
        }
        public static InferenceFormula Parse(string expression) { var p = new Parser(expression); var node = p.Implication(); p.End(); return node; }
        sealed class Parser
        {
            readonly string text; int at;
            public Parser(string source) { text = source.Replace(" ", ""); }
            bool Take(string token) { if (text.Substring(at).StartsWith(token, StringComparison.Ordinal)) { at += token.Length; return true; } return false; }
            public InferenceFormula Implication() { var a = Or(); return Take("->") ? new InferenceFormula('>', a, Implication()) : a; }
            InferenceFormula Or() { var a = And(); while (Take("|")) a = new InferenceFormula('|', a, And()); return a; }
            InferenceFormula And() { var a = Unary(); while (Take("&")) a = new InferenceFormula('&', a, Unary()); return a; }
            InferenceFormula Unary()
            {
                if (Take("!")) return new InferenceFormula('!', Unary());
                if (Take("(")) { var a = Implication(); if (!Take(")")) throw new FormatException(text); return a; }
                if (at >= text.Length || text[at] < 'p' || text[at] > 's') throw new FormatException(text);
                return new InferenceFormula(text[at++]);
            }
            public void End() { if (at != text.Length) throw new FormatException(text); }
        }
    }
}
