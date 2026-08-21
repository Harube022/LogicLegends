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
    [SerializeField] private Transform truthTableSpawnPoint;
    [SerializeField] private Transform rulesOfInferenceSpawnPoint;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // Initial setup: Show Propositional Logic, hide the rest
        if (propositionalLogicGroup != null) propositionalLogicGroup.SetActive(true);
        if (truthTableGroup != null) truthTableGroup.SetActive(false);
        if (rulesOfInferenceGroup != null) rulesOfInferenceGroup.SetActive(false);
    }

    public void TransitionToTruthTable()
    {
        if (truthTableGroup != null) truthTableGroup.SetActive(true);
        if (propositionalLogicGroup != null) propositionalLogicGroup.SetActive(false);

        TeleportPlayer(truthTableSpawnPoint);
    }

    public void TransitionToRulesOfInference()
    {
        if (rulesOfInferenceGroup != null) rulesOfInferenceGroup.SetActive(true);
        if (truthTableGroup != null) truthTableGroup.SetActive(false);

        TeleportPlayer(rulesOfInferenceSpawnPoint);
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

        player.transform.position = target.position;
        player.transform.rotation = target.rotation;

        yield return null;

        if (charController != null) charController.enabled = true;
    }
}