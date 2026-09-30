using System.Collections;
using UnityEngine;

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

        // QuizManager owns the randomized Propositional Logic room spawn. Starting a
        // second teleport here would race it and can leave the player in the fixed
        // Conjunction room while another randomized challenge is active.
        if (StageSelectionState.SelectedStage != 1)
        {
            StartCoroutine(TeleportWhenPlayerIsReady(GetSpawnPoint(StageSelectionState.SelectedStage)));
        }
    }

    public void TransitionToTruthTable()
    {
        StageSelectionState.Select(2);
        StageCompleteManager.UnlockStage(2);
        ActivateOnly(2);
        TeleportPlayer(truthTableSpawnPoint);
    }

    public void TransitionToRulesOfInference()
    {
        StageSelectionState.Select(3);
        StageCompleteManager.UnlockStage(3);
        ActivateOnly(3);
        TeleportPlayer(rulesOfInferenceSpawnPoint);
    }

    private void ActivateOnly(int stageNumber)
    {
        if (propositionalLogicGroup != null) propositionalLogicGroup.SetActive(stageNumber == 1);
        if (truthTableGroup != null) truthTableGroup.SetActive(stageNumber == 2);
        if (rulesOfInferenceGroup != null) rulesOfInferenceGroup.SetActive(stageNumber == 3);
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
        CharacterController charController = player.GetComponent<CharacterController>();
        if (charController != null) charController.enabled = false;

        yield return new WaitForFixedUpdate();

        // Calculate displacement vector
        Vector3 deltaPosition = target.position - player.transform.position;

        player.transform.position = target.position;
        player.transform.rotation = target.rotation;
        
        // Notify Cinemachine to warp the camera instantly
        Unity.Cinemachine.CinemachineCore.OnTargetObjectWarped(player.transform, deltaPosition);

        yield return null;

        if (charController != null) charController.enabled = true;
    }
}
