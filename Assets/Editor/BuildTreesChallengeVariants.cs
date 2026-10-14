using System;
using System.Collections.Generic;
using LogicLegends.Trees;
using UnityEditor;
using UnityEngine;

public static class BuildTreesChallengeVariants
{
    const string AssetPath = "Assets/Prefabs/Trees/TreeChallenges.asset";

    [MenuItem("Tools/Logic Legends/Build Trees Ten Variants Per Challenge")]
    public static void Build()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<TreeChallengeCatalog>(AssetPath);
        if (catalog == null || catalog.challenges == null || catalog.challenges.Count != 5)
            throw new InvalidOperationException("Expected the existing five-challenge Trees catalog at " + AssetPath);

        var sets = new List<TreeChallengeVariantSet>();
        for (int challenge = 0; challenge < 5; challenge++)
        {
            var set = new TreeChallengeVariantSet();
            set.variants.Add(catalog.challenges[challenge]);
            for (int variant = 1; variant < 10; variant++)
            {
                TreeChallengeData data;
                switch (challenge)
                {
                    case 0: data = Identification(variant, catalog.challenges[challenge]); break;
                    case 1: data = BuildTree(variant, catalog.challenges[challenge]); break;
                    case 2: data = RepairTree(variant, catalog.challenges[challenge]); break;
                    case 3: data = DepthTree(variant, catalog.challenges[challenge]); break;
                    default: data = SpanningTree(variant, catalog.challenges[challenge]); break;
                }
                set.variants.Add(data);
            }
            sets.Add(set);
        }
        Undo.RecordObject(catalog, "Build Trees challenge variants");
        catalog.variantSets = sets;
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log("Trees catalog saved: five challenges with ten authored variants each.");
    }

    static TreeChallengeData Base(TreeChallengeData source, TreeChallengeType type)
    {
        return new TreeChallengeData { title = source.title, lesson = source.lesson, type = type };
    }

    static string[] Labels(int count, int seed)
    {
        var labels = new string[count];
        for (int i = 0; i < count; i++)
        {
            int index = (i + seed) % count;
            if (seed % 2 == 0) index = count - 1 - index;
            labels[i] = ((char)('A' + index)).ToString();
        }
        return labels;
    }

    // Each variant has the same node/edge workload as its original challenge.
    // Place leaves evenly, and center every parent over its children.
    static void Diagram(TreeChallengeData data, string[] ids, int[] parent)
    {
        int count = ids.Length;
        var children = new List<int>[count];
        for (int i = 0; i < count; i++) children[i] = new List<int>();
        for (int i = 1; i < count; i++) children[parent[i]].Add(i);
        int leaves = 0, maxDepth = 0;
        for (int i = 0; i < count; i++)
        {
            if (children[i].Count == 0) leaves++;
            int depth = 0, node = i;
            while (node != 0) { node = parent[node]; depth++; }
            maxDepth = Math.Max(maxDepth, depth);
        }
        var x = new float[count];
        var y = new float[count];
        int nextLeaf = 0;
        Place(0, 0, children, leaves, maxDepth, ref nextLeaf, x, y);
        data.root = ids[0];
        for (int i = 0; i < count; i++) data.nodes.Add(new TreeNodeData(ids[i], x[i], y[i]));
        for (int i = 1; i < count; i++) data.required.Add(new TreeEdgeData(ids[parent[i]], ids[i]));
    }

    static float Place(int node, int depth, List<int>[] children, int leaves, int maxDepth,
        ref int nextLeaf, float[] x, float[] y)
    {
        y[node] = 1f - depth / (float)Math.Max(1, maxDepth);
        if (children[node].Count == 0)
            x[node] = leaves == 1 ? 0f : -0.86f + 1.72f * nextLeaf++ / (leaves - 1);
        else
        {
            float sum = 0f;
            foreach (int child in children[node])
                sum += Place(child, depth + 1, children, leaves, maxDepth, ref nextLeaf, x, y);
            x[node] = sum / children[node].Count;
        }
        return x[node];
    }

    static TreeChallengeData Identification(int variant, TreeChallengeData source)
    {
        var data = Base(source, TreeChallengeType.Identify);
        var ids = Labels(7, variant);
        Diagram(data, ids, new[] { -1, 0, 0, 1, 1, 2, 2 });
        data.initial = new List<TreeEdgeData>(data.required);
        switch (variant % 4)
        {
            case 0: data.identification = TreeIdentification.Root; data.objective = "Identify the root node."; break;
            case 1: data.identification = TreeIdentification.Parent; data.target = ids[3]; data.objective = "Which node is the parent of " + data.target + "?"; break;
            case 2: data.identification = TreeIdentification.Child; data.target = ids[2]; data.objective = "Select a child of node " + data.target + "."; break;
            default: data.identification = TreeIdentification.Leaf; data.objective = "Select a leaf node."; break;
        }
        return data;
    }

    static readonly int[][] FiveNodeParents = {
        new[] { -1, 0, 0, 1, 2 },
        new[] { -1, 0, 0, 0, 1 },
        new[] { -1, 0, 1, 1, 2 },
        new[] { -1, 0, 0, 2, 2 },
        new[] { -1, 0, 1, 2, 3 },
        new[] { -1, 0, 0, 0, 0 },
        new[] { -1, 0, 1, 1, 1 },
        new[] { -1, 0, 0, 1, 3 },
        new[] { -1, 0, 0, 2, 1 }
    };

    static TreeChallengeData BuildTree(int variant, TreeChallengeData source)
    {
        var data = Base(source, TreeChallengeType.Build);
        var ids = Labels(5, variant);
        Diagram(data, ids, FiveNodeParents[variant - 1]);
        data.objective = "Root " + ids[0] + ". Add " + EdgeInstructions(data.required) + ".";
        return data;
    }

    static TreeChallengeData RepairTree(int variant, TreeChallengeData source)
    {
        var data = Base(source, TreeChallengeType.Repair);
        var ids = Labels(5, variant);
        int[] parents = FiveNodeParents[variant - 1];
        Diagram(data, ids, parents);
        int correctParent = parents[4];
        int wrongParent = correctParent == 0 ? 1 : 0;
        foreach (var edge in data.required)
            data.initial.Add(new TreeEdgeData(edge.source == ids[correctParent] && edge.destination == ids[4]
                ? ids[wrongParent] : edge.source, edge.destination));
        data.objective = "Repair the tree so " + ids[4] + " is a child of " + ids[correctParent] + ". Keep the other links.";
        return data;
    }

    static readonly int[][] SixNodeParents = {
        new[] { -1, 0, 0, 1, 2, 3 },
        new[] { -1, 0, 0, 1, 1, 4 },
        new[] { -1, 0, 0, 1, 2, 2 },
        new[] { -1, 0, 0, 0, 1, 4 },
        new[] { -1, 0, 1, 1, 2, 3 },
        new[] { -1, 0, 0, 2, 2, 3 },
        new[] { -1, 0, 0, 1, 3, 2 },
        new[] { -1, 0, 0, 1, 2, 4 },
        new[] { -1, 0, 1, 1, 1, 2 }
    };

    static TreeChallengeData DepthTree(int variant, TreeChallengeData source)
    {
        var data = Base(source, TreeChallengeType.Depth);
        var ids = Labels(6, variant);
        Diagram(data, ids, SixNodeParents[variant - 1]);
        data.initial = new List<TreeEdgeData>(data.required);
        data.target = ids[5];
        data.objective = "What is the depth of node " + data.target + "?";
        return data;
    }

    static TreeChallengeData SpanningTree(int variant, TreeChallengeData source)
    {
        var data = Base(source, TreeChallengeType.Spanning);
        var ids = Labels(4, variant);
        data.nodes.Add(new TreeNodeData(ids[0], 0f, 1f));
        data.nodes.Add(new TreeNodeData(ids[1], variant % 2 == 0 ? .84f : -.84f, .5f));
        data.nodes.Add(new TreeNodeData(ids[2], variant % 2 == 0 ? -.84f : .84f, .5f));
        data.nodes.Add(new TreeNodeData(ids[3], 0f, 0f));
        int omitted = (variant - 1) % 6, edgeIndex = 0;
        for (int a = 0; a < 4; a++)
            for (int b = a + 1; b < 4; b++, edgeIndex++)
                if (edgeIndex != omitted) data.initial.Add(new TreeEdgeData(ids[a], ids[b]));
        data.root = ids[0];
        data.objective = source.objective;
        return data;
    }

    static string EdgeInstructions(List<TreeEdgeData> edges)
    {
        var parts = new List<string>();
        foreach (var edge in edges) parts.Add(edge.source + " to " + edge.destination);
        return string.Join(", ", parts.ToArray());
    }
}
