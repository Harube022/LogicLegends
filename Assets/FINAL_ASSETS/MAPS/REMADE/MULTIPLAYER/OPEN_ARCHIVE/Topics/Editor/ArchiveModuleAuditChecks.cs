using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LogicLegends.Graph;
using LogicLegends.Trees;
using UnityEditor;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>Independent mathematical invariants for the module-bounded multiplayer adapters.</summary>
    public static class ArchiveModuleAuditChecks
    {
        [Serializable] public sealed class Report
        {
            public bool passed;
            public int seededQuestionCases, inferenceModels, graphRouteCases, treeCases, mappingCases, inductionIdentities;
            public int[] topicCases = new int[10];
            public string scope = "256 match seeds across every current T1-T10 stage and T3 subphase. Checks module references, existing answer validation plus independent graph, tree, set, mapping and induction invariants. Complements the source-slide alignment report; difficulty still requires student playtesting.";
            public List<string> errors = new List<string>();
        }

        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Double Check DM1 Module Questions")]
        public static Report Validate()
        {
            var report = new Report();
            try
            {
                var catalog = AssetDatabase.LoadAssetAtPath<ArchiveTopicCatalog>(ArchiveTopicSetup.Folder + "/Archive_Topic_Catalog.asset");
                Need(catalog != null, "Missing actual multiplayer catalog.");
                foreach (var bank in ArchiveCurriculumQuestions.InferenceBanks) foreach (var spec in bank)
                {
                    Need(spec.IsValid(), "Invalid inference: " + spec.Key);
                    report.inferenceModels += 16;
                }
                for (int seedIndex = 0; seedIndex < 256; seedIndex++)
                {
                    int seed = unchecked(seedIndex * 7919 + 37);
                    for (int topic = 0; topic < 10; topic++) for (int round = 0; round < ArchiveTopicCatalog.RoundCount(topic); round++)
                    for (int sub = 0; sub < (topic == 2 ? 2 : 1); sub++)
                    {
                        var q = ArchiveTopicQuestions.Build(catalog, topic, round, seed, sub);
                        Need(q.moduleReference == ArchiveCurriculumQuestions.References[topic], "Module reference missing at T" + (topic + 1));
                        Need(ArchiveTopicQuestions.Validate(q, ArchiveTopicChecks.Solution(q), out _), "No accepted complete answer at T" + (topic + 1) + "/" + round);
                        if (topic == 3 && round == 3)
                        {
                            Need(q.proof.steps.Length == 0, "Unrelated copied proof steps remain in the missing-rule prompt.");
                            Need(q.prompt.Split(new[] { "[MISSING JUSTIFICATION]" }, StringSplitOptions.None).Length == 2, "Missing-rule prompt must contain exactly one justification gap.");
                            Need(q.items.Count(s => s == q.proof.expectedRule) == 1, "Missing rule has no unique named answer.");
                        }
                        if (topic == 6) CheckSets(q);
                        if (topic == 7) { CheckMapping(q); report.mappingCases++; }
                        if (topic == 8) { CheckGraph(q); report.graphRouteCases++; }
                        if (topic == 9) { CheckTree(q, round); report.treeCases++; }
                        report.seededQuestionCases++; report.topicCases[topic]++;
                    }
                }
                // The displayed induction family sums a*i+b. Independently check
                // its closed form and k -> k+1 identity, including the base case.
                foreach (int a in Enumerable.Range(1, 10)) foreach (int b in Enumerable.Range(-1, 6))
                for (int n = 1; n <= 20; n++)
                {
                    int actual = Enumerable.Range(1, n).Sum(i => a * i + b);
                    int closed = n * (a * n + a + 2 * b) / 2;
                    Need(actual == closed, "Arithmetic-series induction formula is incorrect.");
                    int next = (n + 1) * (a * (n + 1) + a + 2 * b) / 2;
                    Need(next == closed + a * (n + 1) + b, "Induction step does not append the correct next term.");
                    report.inductionIdentities++;
                }
            }
            catch (Exception ex) { report.errors.Add(ex.ToString()); }
            report.passed = report.errors.Count == 0;
            Directory.CreateDirectory("Temp/ArchiveModuleAudit");
            File.WriteAllText("Temp/ArchiveModuleAudit/Questions.json", JsonUtility.ToJson(report, true));
            return report;
        }

        static void Need(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }
        static void CheckSets(ArchiveQuestion q)
        {
            var a = new HashSet<string>(q.setA); var b = new HashSet<string>(q.setB);
            for (int region = 0; region < q.slots.Length; region++)
            {
                IEnumerable<string> expected;
                if (q.setGroup == 4) { var named = q.setName == "B" ? b : a; expected = q.setUniverse.Where(v => named.Contains(v) == (region == 0)); }
                else if (q.setGroup == 3) expected = region == 0 ? a : region == 1 ? b : q.setSourceValues.Where(v => region == 2 ? a.Contains(v) && b.Contains(v) : region == 3 ? a.Contains(v) && !b.Contains(v) : b.Contains(v) && !a.Contains(v));
                else expected = q.setSourceValues.Where(v => region == 0 ? a.Contains(v) && !b.Contains(v) : region == 1 ? a.Contains(v) && b.Contains(v) : b.Contains(v) && !a.Contains(v));
                Need(new HashSet<string>(expected).SetEquals(q.regionValues[region]), "Set region contradicts membership.");
            }
        }
        static void CheckMapping(ArchiveQuestion q)
        {
            var m = q.mapping;
            var pairs = m.questionKind == MappingQuestionKind.BuildExactRelation ? m.requiredPairs : m.initialPairs;
            Need(pairs.All(p => m.domain.Contains(p.domain) && m.codomain.Contains(p.codomain)), "Mapping has an endpoint outside its displayed sets.");
            bool function = m.domain.All(d => pairs.Count(p => p.domain == d) == 1);
            bool injective = function && pairs.Select(p => p.codomain).Distinct().Count() == m.domain.Length;
            bool onto = function && m.codomain.All(c => pairs.Any(p => p.codomain == c));
            if (m.questionKind == MappingQuestionKind.ClassifyFunction) Need(m.correctClassificationIndex == (function ? 0 : 2), "Function verdict contradicts the actual arrows.");
            if (m.questionKind == MappingQuestionKind.SelectProperties)
            {
                int flags = (injective ? 2 : 0) | (onto ? 4 : 0) | (injective && onto ? 8 : 0);
                Need((int)m.correctProperties == flags, "Function properties contradict input/output coverage.");
            }
        }
        static void CheckGraph(ArchiveQuestion q)
        {
            var g = q.graph;
            var ids = new HashSet<string>(g.vertices.Select(v => v.id));
            Need(ids.Count == g.vertices.Count && g.edges.All(e => e.source != e.destination && ids.Contains(e.source) && ids.Contains(e.destination)), "Invalid graph endpoints or duplicate vertices.");
            var solution = ArchiveTopicChecks.Solution(q);
            if (g.type == GraphChallengeType.Degree)
            {
                int degree = g.edges.Count(e => e.traversalEnabled && e.source == g.degreeVertex) + g.edges.Count(e => e.traversalEnabled && e.destination == g.degreeVertex);
                Need(solution.selection == degree && degree <= 6, "Degree answer is incorrect or outside mobile answer choices."); return;
            }
            var route = solution.route.Select(i => q.nodeIds[i]).ToArray();
            Need(route.Length >= 2 && route[0] == g.start, "Graph solution starts at another vertex.");
            for (int i = 1; i < route.Length; i++) Need(g.edges.Any(e => e.traversalEnabled && (e.source == route[i - 1] && e.destination == route[i] || !e.directed && e.destination == route[i - 1] && e.source == route[i])), "Graph route crosses an absent or reversed arrow.");
            if (g.type == GraphChallengeType.Cycle) Need(route.Last() == g.start && route.Take(route.Length - 1).Distinct().Count() == route.Length - 1, "Cycle repeats an intermediate vertex or does not close.");
            else Need(route.Last() == g.destination && route.Distinct().Count() == route.Length, "Simple path is not simple or misses the destination.");
        }
        static void CheckTree(ArchiveQuestion q, int round)
        {
            var t = q.tree;
            Need(t.nodes.Select(n => n.id).Distinct().Count() == 7, "Multiplayer tree must keep all seven vertices.");
            if (round == 0) Need(t.initial.Single(e => e.destination == "E").source == "B", "Parent of E is not B.");
            if (round == 1 || round == 2) Need(t.required.Count == 6 && t.required.All(e => e.destination != t.root) && t.nodes.Where(n => n.id != t.root).All(n => t.required.Count(e => e.destination == n.id) == 1), "A required non-root node has no unique parent.");
            if (round == 3)
            {
                var depths = new Dictionary<string, int> { { t.root, 0 } };
                for (int pass = 0; pass < 7; pass++) foreach (var e in t.initial) if (depths.ContainsKey(e.source)) depths[e.destination] = depths[e.source] + 1;
                Need(depths.Count == 7 && depths["E"] == 3 && depths.Values.Max() == 4, "Depth/height example does not match its edge-count convention.");
            }
            if (round == 4)
            {
                Need(t.type == TreeChallengeType.Spanning && t.initial.Count == 9, "Spanning task lost its original cyclic graph.");
                var solution = ArchiveTopicChecks.Solution(q);
                Need(ArchiveTreeRules.Decode(q, solution.links, out var edges) && edges.Count == 6, "Spanning solution must contain n-1 original edges.");
            }
        }
    }
}
