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
    [SerializeField, Min(1f)] private float sameHouseRadius = 12f;
    [SerializeField, Min(0.1f)] private float minimumSpawnSeparation = 1.5f;

    private List<GameObject> activeSpawnedBlocks = new List<GameObject>();
    private readonly Dictionary<TruthBlock, Transform> occupiedSpawns = new Dictionary<TruthBlock, Transform>();
    private readonly Dictionary<int, int> houseUseCounts = new Dictionary<int, int>();
    private NearbyTruthBlockTestSpawner nearbyTestSpawner;

    private void Awake()
    {
        if (puzzle == null)
        {
            puzzle = GetComponent<DynamicLogicPuzzle>();
        }
        nearbyTestSpawner = NearbyTruthBlockTestSpawner.AttachForTesting(this, spawnPoints);
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

    public void SpawnBlocksForTruthValues(bool[] values)
    {
        ClearActiveBlocks();
        if (values == null || values.Length != 4 || spawnPoints == null || spawnPoints.Length == 0 ||
            trueBlockPrefab == null || falseBlockPrefab == null) return;

        int trueCount = 0;
        foreach (bool value in values) if (value) trueCount++;
        SpawnBlockGroup(trueCount, values.Length - trueCount);
    }

    public void RespawnBlocksForRetry(bool[] values)
    {
        // Unlike a new column, the current column's placed blocks are discarded.
        // This prevents an incorrect attempt from leaving extra blocks behind.
        var previousTrueHouses = new List<Transform>();
        var previousFalseHouses = new List<Transform>();
        foreach (GameObject spawned in activeSpawnedBlocks)
        {
            if (spawned == null) continue;
            TruthBlock block = spawned.GetComponent<TruthBlock>();
            if (block == null) continue;
            (block.value ? previousTrueHouses : previousFalseHouses).Add(
                block.LastCollectedSpawnPoint ?? block.CurrentSpawnPoint);
        }
        ClearActiveBlocks(true);
        if (values == null || values.Length != 4 || spawnPoints == null || spawnPoints.Length == 0 ||
            trueBlockPrefab == null || falseBlockPrefab == null) return;

        int trueCount = 0;
        foreach (bool value in values) if (value) trueCount++;
        SpawnBlockGroup(trueCount, values.Length - trueCount,
            previousTrueHouses, previousFalseHouses);
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

    private void SpawnBlockGroup(int trueCount, int falseCount,
        List<Transform> previousTrueHouses = null, List<Transform> previousFalseHouses = null)
    {
        List<GameObject> prefabsToSpawn = new List<GameObject>();
        for (int i = 0; i < trueCount; i++) prefabsToSpawn.Add(trueBlockPrefab);
        for (int i = 0; i < falseCount; i++) prefabsToSpawn.Add(falseBlockPrefab);

        for (int i = 0; i < prefabsToSpawn.Count; i++)
        {
            GameObject prefab = prefabsToSpawn[i];
            int priorIndex = i < trueCount ? i : i - trueCount;
            List<Transform> priorHouses = i < trueCount ? previousTrueHouses : previousFalseHouses;
            Transform priorHouse = priorHouses != null && priorIndex < priorHouses.Count ?
                priorHouses[priorIndex] : null;
            Transform chosenSpawn = SelectSpawnPoint(null, priorHouse, null);
            if (chosenSpawn == null)
            {
                Debug.LogError("Not enough free RandomSpawnpoints for Truth_Table blocks.", this);
                break;
            }

            GameObject spawnedBlock = Instantiate(prefab, chosenSpawn.position, chosenSpawn.rotation);
            TruthBlock truthBlock = spawnedBlock.GetComponent<TruthBlock>();
            if (truthBlock == null)
            {
                Destroy(spawnedBlock);
                Debug.LogError("Truth_Table block prefab is missing TruthBlock.", prefab);
                continue;
            }
            truthBlock.InitializeSpawn(this, chosenSpawn);
            occupiedSpawns[truthBlock] = chosenSpawn;
            RecordHouseUse(chosenSpawn);
            activeSpawnedBlocks.Add(spawnedBlock);
        }
        // A different difficulty can request blocks before its spawner's Awake.
        if (nearbyTestSpawner == null)
            nearbyTestSpawner = NearbyTruthBlockTestSpawner.AttachForTesting(this, spawnPoints);
        if (nearbyTestSpawner != null)
            nearbyTestSpawner.SpawnGroup(trueBlockPrefab, falseBlockPrefab, trueCount, falseCount);
    }

    // The initial puzzle creates its blocks before the statue is used. Deal those
    // existing blocks again when the stage actually starts, without duplicating them.
    public void RerollActiveBlocks()
    {
        if (nearbyTestSpawner != null) nearbyTestSpawner.RestoreCurrentGroupAtNearbyPoints();
        var blocks = new List<TruthBlock>();
        var previousSpawns = new Dictionary<TruthBlock, Transform>();
        foreach (GameObject spawned in activeSpawnedBlocks)
        {
            if (spawned == null) continue;
            TruthBlock block = spawned.GetComponent<TruthBlock>();
            if (block == null || block.IsPlacedInColumn) continue;
            blocks.Add(block);
            previousSpawns[block] = block.CurrentSpawnPoint;
            ReleaseSpawn(block);
        }

        // Free all old assignments before dealing the group. Otherwise the first
        // block can only choose houses left vacant by the previous deal.
        foreach (TruthBlock block in blocks)
        {
            Transform destination = SelectSpawnPoint(previousSpawns[block], block.LastCollectedSpawnPoint, block);
            if (destination == null)
            {
                Debug.LogError("No free RandomSpawnpoint while starting Truth_Table.", this);
                block.RestoreWithoutSpawn();
                continue;
            }
            occupiedSpawns[block] = destination;
            RecordHouseUse(destination);
            block.RespawnAt(destination);
        }
    }

    public void ReleaseSpawn(TruthBlock block)
    {
        occupiedSpawns.Remove(block);
    }

    public void RespawnBlock(TruthBlock block)
    {
        Transform previous = block.CurrentSpawnPoint;
        Transform lastCollected = block.LastCollectedSpawnPoint;
        ReleaseSpawn(block);

        Transform destination = SelectSpawnPoint(previous, lastCollected, block);
        if (destination == null)
        {
            Debug.LogError("No free RandomSpawnpoint for a returning Truth_Table block.", this);
            block.RestoreWithoutSpawn();
            return;
        }

        occupiedSpawns[block] = destination;
        RecordHouseUse(destination);
        block.RespawnAt(destination);
    }

    private Transform SelectSpawnPoint(Transform previous, Transform lastCollected, TruthBlock returningBlock)
    {
        List<Transform> candidates = GetAvailableSpawns(returningBlock);
        if (candidates.Count == 0) return null;

        // The last collection house has priority over merely avoiding the last
        // assigned point or spreading a deal, including during a retry.
        if (lastCollected != null)
        {
            List<Transform> otherHouses = candidates.FindAll(point => !IsSameHouse(point, lastCollected));
            if (otherHouses.Count > 0) candidates = otherHouses;
        }
        if (previous != null)
        {
            List<Transform> otherHouses = candidates.FindAll(point => !IsSameHouse(point, previous));
            if (otherHouses.Count > 0) candidates = otherHouses;
        }

        // Spread a deal over houses; use another point in an occupied house
        // only if there are no free houses among the eligible destinations.
        List<Transform> unoccupiedHouses = candidates.FindAll(point => !IsHouseOccupied(point, returningBlock));
        if (unoccupiedHouses.Count > 0) candidates = unoccupiedHouses;

        // Choose a house uniformly, rather than weighting houses by their number
        // of spawn markers. Prefer houses used less often across this session.
        var houseKeys = new List<int>();
        int lowestUse = int.MaxValue;
        foreach (Transform point in candidates)
        {
            int key = GetHouseKey(point);
            int uses = houseUseCounts.TryGetValue(key, out int count) ? count : 0;
            if (uses < lowestUse)
            {
                lowestUse = uses;
                houseKeys.Clear();
            }
            if (uses == lowestUse && !houseKeys.Contains(key)) houseKeys.Add(key);
        }
        int chosenHouse = houseKeys[Random.Range(0, houseKeys.Count)];
        List<Transform> housePoints = candidates.FindAll(point => GetHouseKey(point) == chosenHouse);
        return housePoints[Random.Range(0, housePoints.Count)];
    }

    private List<Transform> GetAvailableSpawns(TruthBlock returningBlock)
    {
        List<Transform> available = new List<Transform>();
        if (spawnPoints == null) return available;
        foreach (Transform point in spawnPoints)
        {
            if (point == null || !point.gameObject.activeInHierarchy ||
                point.root != transform.root || available.Contains(point)) continue;
            bool occupied = false;
            foreach (KeyValuePair<TruthBlock, Transform> entry in occupiedSpawns)
            {
                if (entry.Key == null || entry.Key == returningBlock || entry.Value == null) continue;
                if (point == entry.Value ||
                    Vector3.Distance(point.position, entry.Value.position) < minimumSpawnSeparation)
                {
                    occupied = true;
                    break;
                }
            }
            if (!occupied) available.Add(point);
        }
        return available;
    }

    private bool IsHouseOccupied(Transform point, TruthBlock returningBlock)
    {
        foreach (KeyValuePair<TruthBlock, Transform> entry in occupiedSpawns)
            if (entry.Key != null && entry.Key != returningBlock && IsSameHouse(point, entry.Value))
                return true;
        return false;
    }

    private int GetHouseKey(Transform point)
    {
        for (int i = 0; i < spawnPoints.Length; i++)
            if (spawnPoints[i] != null && IsSameHouse(spawnPoints[i], point)) return i;
        return -1;
    }

    private void RecordHouseUse(Transform point)
    {
        int key = GetHouseKey(point);
        houseUseCounts[key] = houseUseCounts.TryGetValue(key, out int count) ? count + 1 : 1;
    }

    private bool IsSameHouse(Transform first, Transform second)
    {
        return first != null && second != null &&
               Vector2.Distance(new Vector2(first.position.x, first.position.z),
                                new Vector2(second.position.x, second.position.z)) < sameHouseRadius;
    }

    public void ClearActiveBlocks(bool includePlacedBlocks = false)
    {
        if (nearbyTestSpawner != null) nearbyTestSpawner.ClearCurrentGroup(includePlacedBlocks);
        foreach (var block in activeSpawnedBlocks)
        {
            if (block == null) continue;
            TruthBlock truthBlock = block.GetComponent<TruthBlock>();
            if (truthBlock == null || includePlacedBlocks || !truthBlock.IsPlacedInColumn)
            {
                if (truthBlock != null) truthBlock.PrepareForDespawn();
                block.SetActive(false);
                Destroy(block);
            }
        }
        activeSpawnedBlocks.Clear();
    }
}
