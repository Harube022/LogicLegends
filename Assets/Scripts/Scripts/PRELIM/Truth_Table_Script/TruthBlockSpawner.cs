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

    // NOTE: SpawnBlocksForCurrentStep() was removed. 
    // The phases (Easy/Hard) now explicitly pass the parameters they need directly!

    public void SpawnBlocksForStep(int easyModeStep)
    {
        ClearActiveBlocks();

        if (spawnPoints == null || spawnPoints.Length == 0) return;
        if (trueBlockPrefab == null || falseBlockPrefab == null) return;

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

        SpawnBlockGroup(trueCount, falseCount);
    }

    public void SpawnBlocksForHardModeColumnSimple(DynamicLogicType logicType)
    {
        ClearActiveBlocks();

        if (spawnPoints == null || spawnPoints.Length == 0) return;
        if (trueBlockPrefab == null || falseBlockPrefab == null) return;

        int trueCount = 0;
        int falseCount = 0;

        for (int row = 0; row < 4; row++)
        {
            bool p = (row == 0 || row == 1);
            bool q = (row == 0 || row == 2);

            bool expected = LogicUtility.EvaluateLogic(logicType, p, q);
            if (expected) trueCount++;
            else falseCount++;
        }

        SpawnBlockGroup(trueCount, falseCount);
    }

    public void SpawnBlocksForHardModeColumnComplex(ComplexLogicExpression expr)
    {
        ClearActiveBlocks();

        if (spawnPoints == null || spawnPoints.Length == 0) return;
        if (trueBlockPrefab == null || falseBlockPrefab == null) return;

        int trueCount = 0;
        int falseCount = 0;

        for (int row = 0; row < 4; row++)
        {
            bool p = (row == 0 || row == 1);
            bool q = (row == 0 || row == 2);

            bool expected = LogicUtility.EvaluateComplexLogic(expr, p, q);
            if (expected) trueCount++;
            else falseCount++;
        }

        SpawnBlockGroup(trueCount, falseCount);
    }

    private void SpawnBlockGroup(int trueCount, int falseCount)
    {
        List<GameObject> prefabsToSpawn = new List<GameObject>();
        for (int i = 0; i < trueCount; i++) prefabsToSpawn.Add(trueBlockPrefab);
        for (int i = 0; i < falseCount; i++) prefabsToSpawn.Add(falseBlockPrefab);

        List<Transform> availableSpawns = new List<Transform>(spawnPoints);

        foreach (GameObject prefab in prefabsToSpawn)
        {
            if (availableSpawns.Count == 0) break;

            int randomIndex = Random.Range(0, availableSpawns.Count);
            Transform chosenSpawn = availableSpawns[randomIndex];
            availableSpawns.RemoveAt(randomIndex);

            GameObject spawnedBlock = Instantiate(prefab, chosenSpawn.position, chosenSpawn.rotation);
            activeSpawnedBlocks.Add(spawnedBlock);
        }
    }

    public void ClearActiveBlocks()
    {
        foreach (var block in activeSpawnedBlocks)
        {
            if (block != null && block.GetComponent<Collider>().enabled)
            {
                Destroy(block);
            }
        }
        activeSpawnedBlocks.Clear();
    }
}