using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Extra PRELIM testing blocks. Never participates in house selection or occupancy.
/// Attached automatically in Editor/development players when SpawnNearby markers exist.
/// Disable this component during Play mode to remove the test blocks.
/// </summary>
[DisallowMultipleComponent]
public sealed class NearbyTruthBlockTestSpawner : MonoBehaviour
{
    private readonly List<Transform> points = new List<Transform>();
    private readonly List<TruthBlock> currentGroup = new List<TruthBlock>();
    private readonly List<TruthBlock> ownedBlocks = new List<TruthBlock>();

    public static NearbyTruthBlockTestSpawner AttachForTesting(
        TruthBlockSpawner source, Transform[] housePoints)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (source.gameObject.scene.name != "PRELIM") return null;
        var markers = new List<Transform>();
        // All difficulty spawners share the nearby testing markers, even when
        // their house points come from a different RandomSpawnpoints group.
        Transform puzzleRoot = source.transform;
        while (puzzleRoot != null && puzzleRoot.name != "Truth_Table") puzzleRoot = puzzleRoot.parent;
        if (puzzleRoot != null)
        {
            foreach (Transform candidate in puzzleRoot.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name == "SpawnNearby" || candidate.name.StartsWith("SpawnNearby ("))
                    markers.Add(candidate);
            }
        }
        if (markers.Count == 0) return null;
        markers.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        var helper = source.GetComponent<NearbyTruthBlockTestSpawner>();
        if (helper == null) helper = source.gameObject.AddComponent<NearbyTruthBlockTestSpawner>();
        helper.points.Clear();
        helper.points.AddRange(markers);
        return helper;
#else
        return null;
#endif
    }

    public void SpawnGroup(GameObject truePrefab, GameObject falsePrefab, int trueCount, int falseCount)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!isActiveAndEnabled) return;
        ClearCurrentGroup(false);
        var available = points.FindAll(point => point != null && point.gameObject.activeInHierarchy);
        int count = trueCount + falseCount;
        if (available.Count < count)
        {
            Debug.LogWarning("Nearby testing needs one active SpawnNearby marker per block. House spawns are unchanged.", this);
            return;
        }
        ownedBlocks.RemoveAll(block => block == null);
        for (int i = 0; i < count; i++)
        {
            GameObject prefab = i < trueCount ? truePrefab : falsePrefab;
            if (prefab == null) continue;
            Transform point = available[i];
            GameObject instance = Instantiate(prefab, point.position, point.rotation);
            TruthBlock block = instance.GetComponent<TruthBlock>();
            if (block == null)
            {
                Destroy(instance);
                Debug.LogError("Nearby test prefab is missing TruthBlock.", this);
                continue;
            }
            instance.name = prefab.name + " (Nearby Test)";
            // No house owner: returning this block uses its nearby original position.
            block.InitializeSpawn(null, point);
            currentGroup.Add(block);
            ownedBlocks.Add(block);
        }
#endif
    }

    public void ClearCurrentGroup(bool includePlacedBlocks)
    {
        foreach (TruthBlock block in currentGroup)
            if (block != null && (includePlacedBlocks || !block.IsPlacedInColumn)) Despawn(block);
        currentGroup.Clear();
    }

    public void RestoreCurrentGroupAtNearbyPoints()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!isActiveAndEnabled) return;
        // The book starts every difficulty by rerolling house blocks. Restore
        // the separate test set nearby too, without disturbing completed slots.
        foreach (TruthBlock block in currentGroup)
            if (block != null && !block.IsPlacedInColumn && block.CurrentSpawnPoint != null)
                block.RespawnAt(block.CurrentSpawnPoint);
#endif
    }

    private static void Despawn(TruthBlock block)
    {
        block.PrepareForDespawn();
        block.gameObject.SetActive(false);
        Destroy(block.gameObject);
    }

    private void OnDisable()
    {
        foreach (TruthBlock block in ownedBlocks)
            if (block != null) Despawn(block);
        ownedBlocks.Clear();
        currentGroup.Clear();
    }
}
