using System.Collections.Generic;
using UnityEngine;

public class TruthBlockSpawner : MonoBehaviour
{
    [Header("Puzzle Reference")]
    [SerializeField] private DynamicLogicPuzzle puzzle;

    [Header("Block Prefabs")]
    [SerializeField] private GameObject trueBlockPrefab;     // TrueBlock_Variant Prefab
    [SerializeField] private GameObject falseBlockPrefab;    // FalseBlock_Variant Prefab

    [Header("Random Spawn Positions")]
    [SerializeField] private Transform[] spawnPoints;        // Array of spawn points in the scene

    private List<GameObject> activeSpawnedBlocks = new List<GameObject>();

    private void Awake()
    {
        if (puzzle == null)
        {
            puzzle = GetComponent<DynamicLogicPuzzle>();
        }
    }

    /// <summary>
    /// Spawns blocks automatically based on the puzzle's current step.
    /// </summary>
    public void SpawnBlocksForCurrentStep()
    {
        if (puzzle == null) return;
        SpawnBlocksForStep(puzzle.EasyModeStep);
    }

    /// <summary>
    /// Spawns required True/False blocks at random spawn positions for a given step.
    /// </summary>
    public void SpawnBlocksForStep(int easyModeStep)
    {
        // 1. Clear unplaced blocks remaining from the previous topic step
        ClearActiveBlocks();

        if (spawnPoints == null || spawnPoints.Length == 0) return;
        if (trueBlockPrefab == null || falseBlockPrefab == null) return;

        // 2. Determine required True and False counts based on topic output truth table
        int trueCount = 0;
        int falseCount = 0;

        switch (easyModeStep)
        {
            case 0: // Conjunction (T, F, F, F)
                trueCount = 1; falseCount = 3;
                break;

            case 1: // Disjunction (T, T, T, F)
                trueCount = 3; falseCount = 1;
                break;

            case 2: // Exclusive OR (F, T, T, F)
                trueCount = 2; falseCount = 2;
                break;

            case 3: // Implication (T, F, T, T)
                trueCount = 3; falseCount = 1;
                break;

            case 4: // Biconditional (T, F, F, T)
                trueCount = 2; falseCount = 2;
                break;
        }

        // 3. Assemble block list to spawn
        List<GameObject> prefabsToSpawn = new List<GameObject>();
        for (int i = 0; i < trueCount; i++) prefabsToSpawn.Add(trueBlockPrefab);
        for (int i = 0; i < falseCount; i++) prefabsToSpawn.Add(falseBlockPrefab);

        // 4. Shuffle available spawn points randomly without overlap
        List<Transform> availableSpawns = new List<Transform>(spawnPoints);

        foreach (GameObject prefab in prefabsToSpawn)
        {
            if (availableSpawns.Count == 0) break;

            int randomIndex = Random.Range(0, availableSpawns.Count);
            Transform chosenSpawn = availableSpawns[randomIndex];
            availableSpawns.RemoveAt(randomIndex); // Prevent overlapping spawns

            GameObject spawnedBlock = Instantiate(prefab, chosenSpawn.position, chosenSpawn.rotation);
            activeSpawnedBlocks.Add(spawnedBlock);
        }
    }

    /// <summary>
    /// Cleans up unplaced blocks left in the world.
    /// </summary>
    public void ClearActiveBlocks()
    {
        foreach (var block in activeSpawnedBlocks)
        {
            // Only destroy blocks that haven't been locked into a puzzle slot yet
            if (block != null && block.GetComponent<Collider>().enabled)
            {
                Destroy(block);
            }
        }
        activeSpawnedBlocks.Clear();
    }
}