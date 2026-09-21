using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace LogicLegends.Inference
{
    [Serializable]
    public class InferencePremise
    {
        [Tooltip("{p} creates a proposition blank; [IF] creates a word blank; other text is fixed. Use a newline token to wrap a long premise.")]
        public string[] tokens;
    }

    [Serializable]
    public class InferenceForm
    {
        public InferencePremise[] premises;
        public string conclusion;
        public string[] distractors;
    }

    [Serializable]
    public class InferenceExample
    {
        public string title;
        [Tooltip("Statements for p, q, r and s, in that order. Keep each statement short for the board.")]
        public string[] statements;
        [Tooltip("Optional accepted alternatives, one pipe-separated entry per statement.")]
        public string[] aliases;
    }

    [CreateAssetMenu(menuName = "Logic Legends/Inference Rule")]
    public class InferenceRule : ScriptableObject
    {
        public string ruleName;
        public string abbreviation;
        [TextArea] public string instruction;
        public InferenceForm[] forms;
        public InferenceExample[] examples;
    }

    // Pure question/validation code, independent of scene objects and Unity's global random state.
    public sealed class InferenceQuestion
    {
        public readonly InferenceRule Rule;
        public readonly InferenceForm Form;
        public readonly InferenceExample Example;
        public readonly string Id = Guid.NewGuid().ToString("N");
        public string Conclusion => Resolve(Form.conclusion);

        public InferenceQuestion(InferenceRule rule, int form, int example)
        {
            Rule = rule; Form = rule.forms[form]; Example = rule.examples[example];
        }

        public static bool IsBlank(string token) => token.StartsWith("{") || token.StartsWith("[");
        public string Resolve(string template)
        {
            return Regex.Replace(template, @"\{([pqrs])\}", m => Example.statements[m.Groups[1].Value[0] - 'p']);
        }

        public bool Accepts(string token, string input)
        {
            string expected = token.StartsWith("[") ? token.Trim('[', ']') : Resolve(token);
            if (Normalize(input) == Normalize(expected)) return true;
            if (!token.StartsWith("{") || token.Length != 3) return false;
            int index = token[1] - 'p';
            if (Example.aliases == null || index >= Example.aliases.Length || Example.aliases[index] == null) return false;
            foreach (string alias in Example.aliases[index].Split('|'))
                if (!string.IsNullOrWhiteSpace(alias) && Normalize(input) == Normalize(alias)) return true;
            return false;
        }

        public static string Normalize(string text)
        {
            return Regex.Replace((text ?? "").ToLowerInvariant().Replace('’', '\''), @"[^\p{L}\p{N}]+", " ").Trim();
        }
    }

    public sealed class InferenceDeck
    {
        readonly System.Random random;
        readonly List<int> bag = new List<int>();
        readonly Dictionary<InferenceRule, int> lastExamples = new Dictionary<InferenceRule, int>();
        InferenceRule lastRule;
        public InferenceDeck(int seed) { random = new System.Random(seed); }

        public InferenceQuestion Draw(InferenceRule[] rules)
        {
            if (rules == null || rules.Length == 0) throw new InvalidOperationException("No inference rules configured.");
            if (bag.Count == 0)
            {
                for (int i = 0; i < rules.Length; i++) bag.Add(i);
                for (int i = bag.Count - 1; i > 0; i--)
                { int j = random.Next(i + 1); int swap = bag[i]; bag[i] = bag[j]; bag[j] = swap; }
                if (bag.Count > 1 && rules[bag[bag.Count - 1]] == lastRule)
                { int swap = bag[0]; bag[0] = bag[bag.Count - 1]; bag[bag.Count - 1] = swap; }
            }
            int selected = bag[bag.Count - 1]; bag.RemoveAt(bag.Count - 1);
            var rule = rules[selected];
            int example = random.Next(rule.examples.Length);
            if (rule.examples.Length > 1 && lastExamples.TryGetValue(rule, out int last) && example == last)
                example = (example + 1 + random.Next(rule.examples.Length - 1)) % rule.examples.Length;
            lastExamples[rule] = example; lastRule = rule;
            return new InferenceQuestion(rule, random.Next(rule.forms.Length), example);
        }
    }
}
