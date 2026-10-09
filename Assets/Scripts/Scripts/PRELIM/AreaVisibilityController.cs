using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AreaVisibilityManager : MonoBehaviour
{
    public static AreaVisibilityManager Instance { get; private set; }

    [Header("Module GameObjects")]
    [SerializeField] private GameObject propositionalLogicGroup;
    [SerializeField] private GameObject truthTableGroup;
    [SerializeField] private GameObject rulesOfInferenceGroup;

    [Header("Spawn Locations")]
    [SerializeField] private Transform propositionalLogicSpawnPoint;
    [SerializeField] private Transform truthTableSpawnPoint;
    [SerializeField] private Transform rulesOfInferenceSpawnPoint;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            ActivateOnly(StageSelectionState.SelectedStage);
        }
        else Destroy(gameObject);
    }

    private void Start()
    {
        ConfigureSelectedStageUI();
        StartCoroutine(ConfigureRunningWhenPlayerIsReady());

        // QuizManager owns the randomized Propositional Logic room spawn. Starting a
        // second teleport here would race it and can leave the player in the fixed
        // Conjunction room while another randomized challenge is active.
        if (StageSelectionState.SelectedStage != 1 && !StageSelectionState.UsesEditorSpawnPoint)
        {
            StartCoroutine(TeleportWhenPlayerIsReady(GetSpawnPoint(StageSelectionState.SelectedStage)));
        }
    }

    public void TransitionToTruthTable()
    {
        if (StageSelectionState.SelectedStage != 1) return;

        if (Player.LocalInstance == null || truthTableSpawnPoint == null) return;
        TeleportManager.EnsureExists().StartTeleport(Player.LocalInstance.gameObject, truthTableSpawnPoint, () =>
        {
            // Swap the area and its UI only after the black frame has rendered.
            EnvironmentAudioManager.Instance?.DeactivateAllLayers();
            StageSelectionState.Select(2);
            StageCompleteManager.UnlockStage(2);
            ActivateOnly(2);
            ConfigureSelectedStageUI();
        });
    }

    public void TransitionToRulesOfInference()
    {
        StageSelectionState.Select(3);
        StageCompleteManager.UnlockStage(3);

        TeleportManager.EnsureExists().LoadSceneWithFade("RulesOfInference");
    }

    private void ActivateOnly(int stageNumber)
    {
        if (propositionalLogicGroup != null) propositionalLogicGroup.SetActive(stageNumber == 1);
        if (truthTableGroup != null) truthTableGroup.SetActive(stageNumber == 2);
        if (rulesOfInferenceGroup != null) rulesOfInferenceGroup.SetActive(stageNumber == 3);
        if (Player.LocalInstance != null)
            Player.LocalInstance.SetRunningEnabled(stageNumber == 2 || stageNumber == 3);
    }

    private IEnumerator ConfigureRunningWhenPlayerIsReady()
    {
        while (Player.LocalInstance == null) yield return null;
        Player.LocalInstance.SetRunningEnabled(StageSelectionState.SelectedStage == 2 || StageSelectionState.SelectedStage == 3);
    }

    private Transform GetSpawnPoint(int stageNumber)
    {
        switch (stageNumber)
        {
            case 2: return truthTableSpawnPoint;
            case 3: return rulesOfInferenceSpawnPoint;
            default: return propositionalLogicSpawnPoint;
        }
    }

    public bool TryGetStageForSpawnPoint(Transform spawnPoint, out int stageNumber)
    {
        stageNumber = StageSelectionState.FirstStage;
        if (spawnPoint == null) return false;

        if (spawnPoint == truthTableSpawnPoint ||
            (truthTableGroup != null && spawnPoint.IsChildOf(truthTableGroup.transform)))
        {
            stageNumber = 2;
            return true;
        }

        if (spawnPoint == rulesOfInferenceSpawnPoint ||
            (rulesOfInferenceGroup != null && spawnPoint.IsChildOf(rulesOfInferenceGroup.transform)))
        {
            stageNumber = 3;
            return true;
        }

        if (spawnPoint == propositionalLogicSpawnPoint ||
            (propositionalLogicGroup != null && spawnPoint.IsChildOf(propositionalLogicGroup.transform)))
            return true;

        return false;
    }

    private void ConfigureSelectedStageUI()
    {
        bool isTruthTable = StageSelectionState.SelectedStage == 2;
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.SetInventoryVisibility(isTruthTable);
        }

        if (StageSelectionState.SelectedStage == 1)
        {
            return;
        }

        LevelTimerManager propositionalTimer = Object.FindFirstObjectByType<LevelTimerManager>();
        if (propositionalTimer != null)
        {
            propositionalTimer.StopTimer();
            propositionalTimer.SetTimerVisibility(false);
        }
    }

    private IEnumerator TeleportWhenPlayerIsReady(Transform target)
    {
        if (target == null)
        {
            Debug.LogError($"AreaVisibilityManager: Stage {StageSelectionState.SelectedStage} spawn point is not assigned.");
            yield break;
        }

        GameObject player = null;
        while (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player");
            yield return null;
        }

        yield return TeleportRoutine(player, target);
    }

    private void TeleportPlayer(Transform target)
    {
        if (target == null) return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            StartCoroutine(TeleportRoutine(player, target));
        }
    }

    private IEnumerator TeleportRoutine(GameObject player, Transform target)
    {
        yield return TeleportManager.EnsureExists().TeleportAndWait(player, target);
    }
}
