using System;
using System.Collections.Generic;
using LogicLegends.Graph;
using UnityEditor;
using UnityEngine;

public static class BuildGraphChallengeVariants
{
    const string AssetPath = "Assets/Prefabs/Graph/GraphChallenges.asset";

    [MenuItem("Tools/Logic Legends/Build Graph Ten Variants Per Challenge")]
    public static void Build()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<GraphChallengeCatalog>(AssetPath);
        if (catalog == null || catalog.challenges == null || catalog.challenges.Count < 5)
            throw new InvalidOperationException("The Graph challenge catalog is missing.");
        var sets = new List<GraphChallengeVariantSet>();
        for (int round = 0; round < GraphChallengeCatalog.ChallengesPerRun; round++)
        {
            var set = new GraphChallengeVariantSet();
            for (int variant = 0; variant < 10; variant++)
                set.variants.Add(Create(round, variant, catalog.challenges[round].lesson));
            sets.Add(set);
        }
        Undo.RecordObject(catalog, "Build Graph challenge variants");
        catalog.variantSets = sets;
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log("Graph catalog saved: ten variants in each of five progressively harder challenges.");
    }

    static GraphChallengeData Create(int round, int variant, string lesson)
    {
        switch (round)
        {
            case 0: return EdgeChallenge(variant, lesson);
            case 1: return DegreeChallenge(variant, lesson);
            case 2: return PathChallenge(variant, lesson);
            case 3: return DirectedChallenge(variant, lesson);
            default: return CycleChallenge(variant, lesson);
        }
    }

    static string[] Labels(int count, int variant)
    {
        var labels = new string[count];
        int offset = variant < 5 ? variant : variant - 5;
        for (int i = 0; i < count; i++)
            labels[i] = variant < 5 ? ((char)('A' + offset + i)).ToString() : (1 + offset + i).ToString();
        return labels;
    }

    static GraphChallengeData New(GraphChallengeType type, string title, string objective, string lesson, string[] ids)
    {
        return new GraphChallengeData {
            type = type, title = title, objective = objective, lesson = lesson,
            start = ids[0], destination = ids[ids.Length - 1], degreeVertex = ids[Math.Min(2, ids.Length - 1)]
        };
    }

    static void Vertex(GraphChallengeData data, string id, float x, float z, int variant)
    {
        float mirror = variant % 2 == 0 ? 1f : -1f;
        data.vertices.Add(new GraphVertexData(id, x * mirror, z));
    }

    static void Edge(GraphChallengeData data, string[] ids, int a, int b, bool directed = false)
    {
        data.edges.Add(new GraphEdgeData(ids[a], ids[b], directed));
    }

    static GraphChallengeData EdgeChallenge(int variant, string lesson)
    {
        var ids = Labels(2, variant);
        var data = New(GraphChallengeType.VerticesAndEdges, "Vertices and edges",
            "Cross the bridge from " + ids[0] + " to " + ids[1] + ".", lesson, ids);
        Vertex(data, ids[0], 0, 0, variant);
        float x = variant % 3 == 1 ? 12f : variant % 3 == 2 ? 8.5f : 0f;
        float z = variant % 3 == 1 ? 0f : variant % 3 == 2 ? 8.5f : 12f;
        Vertex(data, ids[1], x, z, variant);
        Edge(data, ids, 0, 1);
        return data;
    }

    static GraphChallengeData DegreeChallenge(int variant, string lesson)
    {
        var ids = Labels(5, variant);
        var data = New(GraphChallengeType.Degree, "Vertex degree",
            "What is the degree of vertex " + ids[2] + "? Count its incident bridges.", lesson, ids);
        Vertex(data, ids[0], 0, 0, variant);
        Vertex(data, ids[1], -12, 12, variant);
        Vertex(data, ids[2], 0, 12, variant);
        Vertex(data, ids[3], 12, 12, variant);
        Vertex(data, ids[4], 0, 24, variant);
        Edge(data, ids, 0, 2); Edge(data, ids, 1, 2); Edge(data, ids, 2, 3);
        if (variant % 2 == 0)
        { Edge(data, ids, 2, 4); if (variant % 4 == 0) Edge(data, ids, 0, 1); }
        else Edge(data, ids, 1, 4);
        return data;
    }

    static void SixVertexRoutes(GraphChallengeData data, string[] ids, int variant)
    {
        Vertex(data, ids[0], 0, 0, variant);
        Vertex(data, ids[1], -8, 10, variant);
        Vertex(data, ids[2], 8, 10, variant);
        Vertex(data, ids[3], -8, 22, variant);
        Vertex(data, ids[4], 8, 22, variant);
        Vertex(data, ids[5], 0, 32, variant);
    }

    static GraphChallengeData PathChallenge(int variant, string lesson)
    {
        var ids = Labels(6, variant);
        var data = New(GraphChallengeType.Path, "Find a simple path",
            "Reach " + ids[5] + " from " + ids[0] + " without repeating a vertex.", lesson, ids);
        SixVertexRoutes(data, ids, variant);
        Edge(data, ids, 0, 1); Edge(data, ids, 1, 3); Edge(data, ids, 3, 5);
        Edge(data, ids, 0, 2); Edge(data, ids, 2, 4); Edge(data, ids, 4, 5);
        if (variant % 3 == 0) Edge(data, ids, 1, 2);
        else if (variant % 3 == 1) Edge(data, ids, 3, 4);
        return data;
    }

    static GraphChallengeData DirectedChallenge(int variant, string lesson)
    {
        var ids = Labels(6, variant);
        var data = New(GraphChallengeType.DirectedPath, "Follow the arrows",
            "Reach " + ids[5] + " from " + ids[0] + ". Follow each arrow; one branch leads to a dead end.", lesson, ids);
        SixVertexRoutes(data, ids, variant);
        bool leftWorks = variant % 2 == 0;
        Edge(data, ids, 0, 1, true); Edge(data, ids, 1, 3, true);
        Edge(data, ids, leftWorks ? 3 : 5, leftWorks ? 5 : 3, true);
        Edge(data, ids, 0, 2, true); Edge(data, ids, 2, 4, true);
        Edge(data, ids, leftWorks ? 5 : 4, leftWorks ? 4 : 5, true);
        if (variant % 3 == 1) Edge(data, ids, 1, 2, true);
        else if (variant % 3 == 2) Edge(data, ids, leftWorks ? 3 : 4, leftWorks ? 4 : 3, true);
        return data;
    }

    static GraphChallengeData CycleChallenge(int variant, string lesson)
    {
        var ids = Labels(6, variant);
        var data = New(GraphChallengeType.Cycle, "Complete a cycle",
            "Start at " + ids[0] + ", visit the ring, and return to " + ids[0] + " without repeating another vertex. Watch for the dead end.", lesson, ids);
        data.destination = ids[0];
        Vertex(data, ids[0], 0, 0, variant);
        Vertex(data, ids[1], -8, 10, variant);
        Vertex(data, ids[2], -8, 24, variant);
        Vertex(data, ids[3], 8, 24, variant);
        Vertex(data, ids[4], 8, 10, variant);
        int spur = 1 + variant % 4;
        float spurX = spur <= 2 ? -18 : 18;
        float spurZ = spur == 1 || spur == 4 ? 10 : 24;
        Vertex(data, ids[5], spurX, spurZ, variant);
        Edge(data, ids, 0, 1); Edge(data, ids, 1, 2); Edge(data, ids, 2, 3);
        Edge(data, ids, 3, 4); Edge(data, ids, 4, 0); Edge(data, ids, spur, 5);
        return data;
    }
}
