using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace LogicLegends.Inference
{
    [RequireComponent(typeof(InferenceCrystal))]
    public sealed class RuleDiamond : MonoBehaviour
    {
        public RuleOfInferenceType RuleType { get; private set; }
        public string DisplayName { get; private set; }
        public string Abbreviation { get; private set; }
        public InferenceCrystal Crystal { get; private set; }
        public void Configure(InferenceRule rule)
        {
            RuleType = RuleOfInferencePuzzle.TypeOf(rule);
            DisplayName = rule.ruleName; Abbreviation = rule.abbreviation;
            Crystal = GetComponent<InferenceCrystal>();
            ShowLabel();
        }
        public void ShowLabel()
        {
            var label = Crystal.label;
            label.text = DisplayName + " (" + Abbreviation + ")";
            label.fontSize = 12; label.enableAutoSizing = false;
            label.rectTransform.sizeDelta = new Vector2(16, 3);
            label.transform.localPosition = new Vector3(0, 2.1f, 0);
            label.alignment = TextAlignmentOptions.Center;
            label.gameObject.SetActive(true);
        }
        public static InferenceRule[] Choose(InferenceRule correct, InferenceRule[] pool, int count, System.Random random)
        {
            var other = pool.Where(r => r != correct).Distinct().ToList();
            Shuffle(other, random);
            var choices = other.Take(Mathf.Clamp(count, 4, 8) - 1).ToList();
            choices.Add(correct); Shuffle(choices, random); return choices.ToArray();
        }
        static void Shuffle<T>(List<T> values, System.Random random)
        {
            for (int i = values.Count - 1; i > 0; i--)
            { int j = random.Next(i + 1); T temp = values[i]; values[i] = values[j]; values[j] = temp; }
        }
    }
}
