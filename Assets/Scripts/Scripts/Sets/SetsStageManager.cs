using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public enum SetsStageState
{
    WAITING_FOR_ACTIVATION,
    ACTIVE,
    CHECKING,
    CHALLENGE_COMPLETE,
    STAGE_COMPLETE,
    GAME_OVER
}

[DisallowMultipleComponent]
public sealed class SetsStageManager : MonoBehaviour
{
    [Header("Challenge Data")]
    [SerializeField] private SetsChallengeCatalog challengeCatalog;
    [SerializeField, Range(0, 4)] private int startingChallengeIndex;

    [Header("Stage References")]
    [SerializeField] private SetElement elementPrefab;
    [SerializeField] private GameObject[] numberedBlockModels = new GameObject[9];
    [SerializeField] private Transform elementSpawnPoint;
    [SerializeField] private Transform challengeBoard;
    [SerializeField] private SetPlacementZone[] placementZones;
    [SerializeField] private SetsUIController uiController;
    [SerializeField] private LevelManager levelManager;
    [SerializeField] private GameOverManager gameOverManager;
    [SerializeField] private StageCompleteManager stageCompleteManager;

    [Header("Gameplay")]
    [SerializeField, Min(30f)] private float stageTimerSeconds = 180f;
    [SerializeField, Min(0.1f)] private float pickupRange = 2.2f;
    [SerializeField, Min(0f)] private float stunDuration = 1.1f;
    [SerializeField, Min(0f)] private float challengeTransitionDelay = 1.8f;

    private readonly HashSet<SetPlacementZone> playerZones = new HashSet<SetPlacementZone>();
    private readonly List<SetElement> activeElements = new List<SetElement>();
    private readonly HashSet<SetElement> completedElements = new HashSet<SetElement>();

    private GameInput gameInput;
    private Player player;
    private SetElement carriedElement;
    private SetElement pickupTarget;
    private SetPlacementZone currentZone;
    private SetsStageState state = SetsStageState.WAITING_FOR_ACTIVATION;
    private int challengeIndex;
    private bool challengeActivated;
    private bool transitionRoutineRunning;
    private Vector3 playerStartPosition;
    private Quaternion playerStartRotation;
    private bool hasPlayerStartPose;
    private int conditionIndex;
    private TextMeshPro boardInstructions;

    public SetsStageState State => state;
    public SetsChallengeData CurrentChallenge => GetChallenge(challengeIndex);
    public int CurrentChallengeIndex => challengeIndex;
    public int CompletedElementCount => completedElements.Count;
    public SetElement CarriedElement => carriedElement;
    public SetPlacementZone CurrentZone => currentZone;
    public int CurrentConditionIndex => conditionIndex;
    private bool IsVennChallenge => challengeIndex == 0 && CurrentChallenge != null &&
        CurrentChallenge.conditions != null && CurrentChallenge.conditions.Count > 0;

    private void Awake()
    {
        if (challengeCatalog == null)
            challengeCatalog = Resources.Load<SetsChallengeCatalog>("SetsChallengeCatalog");

        if (levelManager == null) levelManager = LevelManager.Instance;
        if (uiController == null) uiController = FindFirstObjectByType<SetsUIController>();
        if (gameOverManager == null) gameOverManager = FindFirstObjectByType<GameOverManager>();
        if (stageCompleteManager == null) stageCompleteManager = FindFirstObjectByType<StageCompleteManager>();
        if (placementZones == null || placementZones.Length == 0)
            placementZones = FindObjectsByType<SetPlacementZone>(FindObjectsSortMode.None);

        challengeIndex = Mathf.Clamp(startingChallengeIndex, 0, Mathf.Max(0, ChallengeCount - 1));
        state = SetsStageState.WAITING_FOR_ACTIVATION;
    }

    private void OnEnable()
    {
        BindGameInput();
    }

    private void OnDisable()
    {
        if (gameInput != null) gameInput.OnInteractAction -= HandleInteract;
    }

    private void Start()
    {
        // Rebind after all scene Awake calls; a scene GameInput duplicate may have
        // destroyed itself in favor of the persistent instance during Awake.
        BindGameInput();
        player = Player.LocalInstance != null ? Player.LocalInstance : FindFirstObjectByType<Player>();
        if (player != null)
        {
            playerStartPosition = player.transform.position;
            playerStartRotation = player.transform.rotation;
            hasPlayerStartPose = true;
        }
        if (levelManager == null) levelManager = LevelManager.Instance;
        if (uiController == null) uiController = FindFirstObjectByType<SetsUIController>();
        if (gameOverManager == null) gameOverManager = FindFirstObjectByType<GameOverManager>();
        if (stageCompleteManager == null) stageCompleteManager = FindFirstObjectByType<StageCompleteManager>();

        if (ChallengeCount == 0)
        {
            Debug.LogError("SetsStageManager needs a SetsChallengeCatalog containing at least one challenge.", this);
            enabled = false;
            return;
        }

        if (levelManager == null)
        {
            Debug.LogError("SetsStageManager could not find the existing LevelManager timer. Add its prefab to the Sets scene.", this);
            enabled = false;
            return;
        }

        if (player == null)
        {
            Debug.LogError("SetsStageManager could not find the local Player. Add the existing Player prefab to the Sets scene.", this);
            enabled = false;
            return;
        }

        if (uiController != null)
            uiController.Configure(this, gameOverManager, stageCompleteManager);

        if (challengeBoard == null)
        {
            GameObject board = GameObject.Find("Board 1");
            if (board != null) challengeBoard = board.transform;
        }

        levelManager.BeginSetsStageTimer(stageTimerSeconds);
        if (uiController != null) uiController.SetTimer(stageTimerSeconds);
        RefreshChallengeDisplay(waitingForActivation: true);
    }

    private void BindGameInput()
    {
        GameInput current = GameInput.Instance != null ? GameInput.Instance : FindFirstObjectByType<GameInput>();
        if (current == gameInput) return;
        if (gameInput != null) gameInput.OnInteractAction -= HandleInteract;
        gameInput = current;
        if (isActiveAndEnabled && gameInput != null) gameInput.OnInteractAction += HandleInteract;
    }

    private void Update()
    {
        if (state != SetsStageState.ACTIVE || player == null) return;

        if (carriedElement == null)
        {
            pickupTarget = FindNearestAvailableElement();
            if (uiController != null)
                uiController.ShowPickupPrompt(pickupTarget != null);

            if (DialogueManager.Instance != null)
                DialogueManager.Instance.ToggleInteractButton(pickupTarget != null);
        }
        else
        {
            pickupTarget = null;
            if (uiController != null)
            {
                SetZone? zone = currentZone != null ? currentZone.Zone : (SetZone?)null;
                if (IsVennChallenge) uiController.ShowVennPlacementPrompt(zone);
                else uiController.ShowPlacementPrompt(currentZone != null, zone);
            }

            if (DialogueManager.Instance != null)
                DialogueManager.Instance.ToggleInteractButton(false);
        }

        if (uiController != null && levelManager != null)
            uiController.SetTimer(levelManager.TimeRemaining);
    }

    private int ChallengeCount => challengeCatalog != null && challengeCatalog.Challenges != null
        ? challengeCatalog.Challenges.Count
        : 0;

    private SetsChallengeData GetChallenge(int index)
    {
        if (challengeCatalog == null || challengeCatalog.Challenges == null || index < 0 || index >= challengeCatalog.Challenges.Count)
            return null;
        return challengeCatalog.Challenges[index];
    }

    /// <summary>Hook this to WizardInteraction.OnWizardInteract on the Sets Book Statue.</summary>
    public void ActivateCurrentChallenge()
    {
        if (state != SetsStageState.WAITING_FOR_ACTIVATION || challengeActivated) return;

        SetsChallengeData challenge = CurrentChallenge;
        if (challenge == null || (!IsVennChallenge && (challenge.elements == null || challenge.elements.Count == 0)))
        {
            Debug.LogError($"Sets challenge {challengeIndex + 1} has no element definitions.", this);
            return;
        }

        challengeActivated = true;
        state = SetsStageState.ACTIVE;
        if (IsVennChallenge) SpawnNumberedBlocks();
        else SpawnCurrentChallengeElements(challenge);
        levelManager.ResumeSetsStageTimer();
        RefreshChallengeDisplay(waitingForActivation: false);
        if (uiController != null) uiController.ShowFeedback(IsVennChallenge ? "Start" : "Challenge started", SetsFeedbackKind.Neutral);
    }

    private void SpawnCurrentChallengeElements(SetsChallengeData challenge)
    {
        ClearCurrentElements();
        if (elementSpawnPoint == null)
        {
            GameObject spawner = GameObject.Find("Spawner");
            if (spawner != null) elementSpawnPoint = spawner.transform;
        }

        Vector3 origin = elementSpawnPoint != null ? elementSpawnPoint.position : transform.position;
        Quaternion rotation = elementSpawnPoint != null ? elementSpawnPoint.rotation : Quaternion.identity;
        Transform carryRoot = transform;

        for (int i = 0; i < challenge.elements.Count; i++)
        {
            SetsElementDefinition definition = challenge.elements[i];
            if (definition == null) continue;

            Vector3 spawnPosition = origin + Vector3.up * 0.85f + Vector3.right * ((i % 3) * 0.85f);
            SetElement element;
            if (elementPrefab != null)
            {
                element = Instantiate(elementPrefab, spawnPosition, rotation, carryRoot);
            }
            else
            {
                GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                fallback.name = "Set Element " + definition.value;
                fallback.transform.SetParent(carryRoot, true);
                fallback.transform.SetPositionAndRotation(spawnPosition, rotation);
                fallback.transform.localScale = Vector3.one * 0.72f;
                fallback.AddComponent<Rigidbody>().isKinematic = true;
                element = fallback.AddComponent<SetElement>();
            }

            element.name = "Set Element " + definition.value;
            element.Initialize(definition.value, definition.correctZone, spawnPosition, rotation);
            activeElements.Add(element);
        }
    }

    private void SpawnNumberedBlocks()
    {
        ClearCurrentElements();
        if (elementSpawnPoint == null)
        {
            GameObject spawner = GameObject.Find("Spawner");
            if (spawner != null) elementSpawnPoint = spawner.transform;
        }
        if (elementSpawnPoint == null)
        {
            Debug.LogError("Sets Challenge 1 needs the existing Spawner.", this);
            return;
        }

        Renderer spawnerRenderer = elementSpawnPoint.GetComponentInChildren<Renderer>();
        float surfaceY = spawnerRenderer != null ? spawnerRenderer.bounds.max.y : elementSpawnPoint.position.y;
        for (int i = 0; i < 9; i++)
        {
            if (numberedBlockModels == null || i >= numberedBlockModels.Length || numberedBlockModels[i] == null)
            {
                Debug.LogError($"Sets Challenge 1 is missing numbered block model {i + 1}.", this);
                continue;
            }

            GameObject block = Instantiate(numberedBlockModels[i], transform);
            block.name = $"Set Element {i + 1}";
            Renderer modelRenderer = block.GetComponentInChildren<Renderer>();
            if (modelRenderer == null)
            {
                Destroy(block);
                continue;
            }

            // Fit the imported model inside a readable 3 x 3 arrangement without
            // replacing its mesh or material. Each copy keeps its source FBX link.
            Bounds bounds = modelRenderer.bounds;
            float longest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (longest > 0.01f)
                block.transform.localScale *= Mathf.Min(1f, 0.95f / longest);
            bounds = modelRenderer.bounds;

            int column = i % 3;
            int row = i / 3;
            Vector3 position = new Vector3(
                elementSpawnPoint.position.x + (column - 1) * 2.05f,
                surfaceY + 0.12f + bounds.extents.y,
                elementSpawnPoint.position.z + (row - 1) * 2.05f);
            block.transform.position = position;
            bounds = modelRenderer.bounds;

            BoxCollider collider = block.GetComponent<BoxCollider>();
            if (collider == null) collider = block.AddComponent<BoxCollider>();
            collider.center = block.transform.InverseTransformPoint(bounds.center);
            collider.size = Vector3.Scale(bounds.size, new Vector3(
                1f / Mathf.Max(0.001f, block.transform.lossyScale.x),
                1f / Mathf.Max(0.001f, block.transform.lossyScale.y),
                1f / Mathf.Max(0.001f, block.transform.lossyScale.z)));
            collider.size = Vector3.Max(collider.size, Vector3.one * 0.4f);

            Rigidbody body = block.GetComponent<Rigidbody>();
            if (body == null) body = block.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            GameObject labelObject = new GameObject("Number Label", typeof(TextMeshPro));
            labelObject.transform.SetParent(block.transform, true);
            labelObject.transform.position = new Vector3(bounds.center.x, bounds.max.y + 0.35f, bounds.center.z);
            TextMeshPro label = labelObject.GetComponent<TextMeshPro>();
            label.text = (i + 1).ToString();
            label.fontSize = 6f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.outlineWidth = 0.2f;
            label.outlineColor = new Color32(12, 34, 38, 255);
            label.rectTransform.sizeDelta = new Vector2(2f, 1f);
            labelObject.transform.localScale = Vector3.one * 1.6f;

            SetElement element = block.AddComponent<SetElement>();
            element.Initialize((i + 1).ToString(), SetZone.OUTSIDE, position, block.transform.rotation);
            activeElements.Add(element);
        }
    }

    private SetElement FindNearestAvailableElement()
    {
        if (player == null) return null;

        Vector3 center = player.transform.position + Vector3.up * 0.75f;
        Collider[] nearby = Physics.OverlapSphere(center, pickupRange);
        SetElement nearest = null;
        float nearestDistance = float.PositiveInfinity;

        foreach (Collider collider in nearby)
        {
            SetElement candidate = collider.GetComponentInParent<SetElement>();
            if (candidate == null || candidate.IsCarried || (candidate.IsPlaced && !IsVennChallenge) ||
                !activeElements.Contains(candidate)) continue;

            float distance = (candidate.transform.position - center).sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearest = candidate;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    private void HandleInteract(object sender, EventArgs args)
    {
        if (state != SetsStageState.ACTIVE || carriedElement != null || pickupTarget == null) return;
        if (!pickupTarget.BeginCarry(player != null ? player.GetHoldPoint() : null)) return;

        carriedElement = pickupTarget;
        pickupTarget = null;
        if (uiController != null)
        {
            uiController.ShowPickupPrompt(false);
            uiController.ShowPlacementPrompt(false, null);
        }
    }

    public void PlaceCurrentElement()
    {
        if (state != SetsStageState.ACTIVE || carriedElement == null) return;
        if (IsVennChallenge && (currentZone == null || currentZone.Zone == SetZone.OUTSIDE))
        {
            carriedElement.ReturnToSpawn();
            carriedElement = null;
            if (uiController != null) uiController.ShowFeedback("Returned to the spawner.", SetsFeedbackKind.Neutral);
            return;
        }
        if (currentZone == null)
        {
            if (uiController != null) uiController.ShowFeedback("Move into one of the four regions first.", SetsFeedbackKind.Neutral);
            return;
        }

        SetElement element = carriedElement;
        if (IsVennChallenge)
        {
            element.PlaceInZone(currentZone, tintCorrect: false);
            carriedElement = null;
            if (uiController != null) uiController.ShowFeedback("Placed. You can move it before submitting.", SetsFeedbackKind.Neutral);
            return;
        }

        if (element.CorrectZone == currentZone.Zone)
        {
            state = SetsStageState.CHECKING;
            element.PlaceInZone(currentZone);
            completedElements.Add(element);
            carriedElement = null;

            if (uiController != null)
                uiController.ShowFeedback($"Correct! {element.ElementValue} belongs in {FormatZone(currentZone.Zone)}.", SetsFeedbackKind.Correct);

            if (completedElements.Count >= CurrentChallenge.RequiredCorrectPlacements)
                CompleteCurrentChallenge();
            else
                state = SetsStageState.ACTIVE;
        }
        else
        {
            StartCoroutine(HandleWrongPlacement(element, player));
        }
    }

    public void SubmitCondition()
    {
        if (state != SetsStageState.ACTIVE || !IsVennChallenge) return;
        SetsConditionData condition = CurrentChallenge.conditions[conditionIndex];
        bool valid = carriedElement == null && activeElements.Count == 9;
        var placements = new List<KeyValuePair<string, SetZone>>();
        foreach (SetElement element in activeElements)
        {
            if (element == null) { valid = false; continue; }
            if (element.IsPlaced && element.PlacedZone != null)
                placements.Add(new KeyValuePair<string, SetZone>(element.ElementValue, element.PlacedZone.Zone));
        }
        valid &= ValidateConditionPlacements(condition, placements);

        if (!valid)
        {
            if (uiController != null)
                uiController.ShowFeedback("Incorrect. Check the sets and try again.", SetsFeedbackKind.Wrong);
            return;
        }

        state = SetsStageState.CHECKING;
        if (uiController != null) uiController.SetSubmitInteractable(false);
        if (conditionIndex + 1 == CurrentChallenge.conditions.Count)
        {
            if (uiController != null)
                uiController.ShowFeedback("Challenge 1 Complete!", SetsFeedbackKind.Correct);
            CompleteCurrentChallenge();
        }
        else
        {
            if (uiController != null)
                uiController.ShowFeedback("Correct!", SetsFeedbackKind.Correct);
            StartCoroutine(AdvanceCondition());
        }
    }

    // Shared by the original Sets stage and the scene-specific Setssss board.
    public static bool ValidateConditionPlacements(SetsConditionData condition,
        IEnumerable<KeyValuePair<string, SetZone>> placements)
    {
        if (condition == null || placements == null) return false;
        var a = new HashSet<string>(condition.setA ?? Array.Empty<string>());
        var b = new HashSet<string>(condition.setB ?? Array.Empty<string>());
        var expected = new Dictionary<string, SetZone>();
        foreach (string value in a) expected[value] = b.Contains(value) ? SetZone.INTERSECTION : SetZone.A_ONLY;
        foreach (string value in b) if (!expected.ContainsKey(value)) expected[value] = SetZone.B_ONLY;

        var seen = new HashSet<string>();
        foreach (var placement in placements)
        {
            if (!seen.Add(placement.Key) || !expected.TryGetValue(placement.Key, out SetZone zone) || zone != placement.Value)
                return false;
        }
        return seen.Count == expected.Count;
    }

    private IEnumerator AdvanceCondition()
    {
        yield return new WaitForSeconds(1.2f);
        if (state != SetsStageState.CHECKING) yield break;
        conditionIndex++;
        carriedElement = null;
        pickupTarget = null;
        foreach (SetElement element in activeElements)
            if (element != null) element.ReturnToSpawn();
        state = SetsStageState.ACTIVE;
        RefreshChallengeDisplay(waitingForActivation: false);
    }

    private IEnumerator HandleWrongPlacement(SetElement element, Player playerToStun)
    {
        state = SetsStageState.CHECKING;
        carriedElement = null;
        if (element != null) element.SetFeedbackColor(new Color(0.95f, 0.18f, 0.18f, 1f));
        if (playerToStun != null) playerToStun.ToggleControl(false);
        if (uiController != null) uiController.ShowFeedback("Incorrect region. −10 seconds", SetsFeedbackKind.Wrong);

        levelManager.DeductSetsStageTime(10f);
        if (state == SetsStageState.GAME_OVER) yield break;

        yield return new WaitForSeconds(0.45f);
        if (element != null) element.ReturnToSpawn();
        yield return new WaitForSeconds(stunDuration);

        if (state == SetsStageState.GAME_OVER || state == SetsStageState.STAGE_COMPLETE) yield break;
        if (playerToStun != null) playerToStun.ToggleControl(true);
        state = SetsStageState.ACTIVE;
        if (uiController != null) uiController.ClearFeedback();
    }

    private void CompleteCurrentChallenge()
    {
        if (state == SetsStageState.CHALLENGE_COMPLETE || state == SetsStageState.STAGE_COMPLETE) return;

        state = SetsStageState.CHALLENGE_COMPLETE;
        levelManager.PauseSetsStageTimer();
        if (uiController != null)
        {
            if (IsVennChallenge) uiController.ShowVennChallengeComplete();
            else uiController.ShowChallengeComplete(CurrentChallenge.challengeNumber);
        }

        if (!transitionRoutineRunning)
        {
            transitionRoutineRunning = true;
            StartCoroutine(AdvanceAfterChallengeComplete());
        }
    }

    private IEnumerator AdvanceAfterChallengeComplete()
    {
        yield return new WaitForSeconds(challengeTransitionDelay);
        transitionRoutineRunning = false;
        ClearCurrentElements();
        completedElements.Clear();
        carriedElement = null;
        pickupTarget = null;
        challengeActivated = false;
        conditionIndex = 0;

        if (challengeIndex + 1 < ChallengeCount)
        {
            challengeIndex++;
            state = SetsStageState.WAITING_FOR_ACTIVATION;
            RefreshChallengeDisplay(waitingForActivation: true);
            yield break;
        }

        state = SetsStageState.STAGE_COMPLETE;
        levelManager.StopTimer();
        levelManager.StopStageTimer();
        if (player != null) player.ToggleControl(false);
        if (uiController != null) uiController.ShowStageComplete();

        if (stageCompleteManager == null) stageCompleteManager = FindFirstObjectByType<StageCompleteManager>();
        if (stageCompleteManager != null)
            stageCompleteManager.TriggerStageComplete();
        else
            Debug.LogWarning("Sets Stage finished, but there is no StageCompleteManager in this scene to save stage progression.", this);
    }

    public void HandleSetsGameOver()
    {
        if (state == SetsStageState.GAME_OVER || state == SetsStageState.STAGE_COMPLETE) return;

        state = SetsStageState.GAME_OVER;
        StopAllCoroutines();
        transitionRoutineRunning = false;
        if (player != null) player.ToggleControl(false);
        if (uiController != null) uiController.ShowGameOver();
    }

    public void ResetStageAfterGameOver()
    {
        StopAllCoroutines();
        transitionRoutineRunning = false;
        ClearCurrentElements();
        completedElements.Clear();
        playerZones.Clear();
        currentZone = null;
        carriedElement = null;
        pickupTarget = null;
        challengeIndex = Mathf.Clamp(startingChallengeIndex, 0, ChallengeCount - 1);
        conditionIndex = 0;
        challengeActivated = false;
        state = SetsStageState.WAITING_FOR_ACTIVATION;

        if (player == null) player = Player.LocalInstance != null ? Player.LocalInstance : FindFirstObjectByType<Player>();
        if (player != null)
        {
            if (hasPlayerStartPose)
                player.transform.SetPositionAndRotation(playerStartPosition, playerStartRotation);
            player.ToggleControl(true);
        }
        if (levelManager != null) levelManager.ResetSetsStageTimer(stageTimerSeconds);
        if (uiController != null)
        {
            uiController.SetTimer(stageTimerSeconds);
            uiController.HideOverlays();
            RefreshChallengeDisplay(waitingForActivation: true);
        }
    }

    public void PlayerEnteredZone(SetPlacementZone zone, Player enteringPlayer)
    {
        if (zone == null || enteringPlayer == null || (player != null && enteringPlayer != player)) return;
        player = enteringPlayer;
        playerZones.Add(zone);
        RecomputeCurrentZone();
    }

    public void PlayerExitedZone(SetPlacementZone zone, Player exitingPlayer)
    {
        if (zone == null || exitingPlayer == null || (player != null && exitingPlayer != player)) return;
        playerZones.Remove(zone);
        RecomputeCurrentZone();
    }

    private void RecomputeCurrentZone()
    {
        SetPlacementZone best = null;
        foreach (SetPlacementZone zone in playerZones)
        {
            if (zone == null) continue;
            if (best == null || zone.Priority > best.Priority) best = zone;
        }
        currentZone = best;
    }

    private void RefreshChallengeDisplay(bool waitingForActivation)
    {
        SetsChallengeData challenge = CurrentChallenge;
        if (challenge == null) return;
        if (uiController != null)
        {
            if (IsVennChallenge)
                uiController.DisplayVennCondition(challenge.conditions[conditionIndex], conditionIndex + 1,
                    challenge.conditions.Count, waitingForActivation);
            else
                uiController.DisplayChallenge(challenge, challengeIndex + 1, ChallengeCount, waitingForActivation);
        }
        RefreshBoardDisplay(waitingForActivation);
    }

    private void RefreshBoardDisplay(bool waitingForActivation)
    {
        if (challengeBoard == null) return;
        if (boardInstructions == null)
        {
            GameObject existing = new GameObject("Sets Challenge Board Text", typeof(TextMeshPro));
            existing.transform.SetParent(challengeBoard, true);
            Renderer boardRenderer = challengeBoard.GetComponentInChildren<Renderer>();
            Bounds bounds = boardRenderer != null ? boardRenderer.bounds : new Bounds(challengeBoard.position, new Vector3(18f, 11f, 1f));
            Vector3 playerSide = elementSpawnPoint != null ? elementSpawnPoint.position - bounds.center : Vector3.forward;
            float sign = playerSide.z >= 0f ? 1f : -1f;
            existing.transform.position = new Vector3(bounds.center.x, bounds.center.y, bounds.center.z + sign * (bounds.extents.z + 0.15f));
            existing.transform.rotation = Quaternion.Euler(0f, sign > 0f ? 180f : 0f, 0f);
            boardInstructions = existing.GetComponent<TextMeshPro>();
            boardInstructions.rectTransform.sizeDelta = new Vector2(16.2f, 9.7f);
            TMP_FontAsset boardFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/IMPACT SDF");
            if (boardFont != null) boardInstructions.font = boardFont;
            boardInstructions.fontSize = 7.5f;
            boardInstructions.color = Color.white;
            boardInstructions.alignment = TextAlignmentOptions.Center;
            boardInstructions.textWrappingMode = TextWrappingModes.Normal;
            boardInstructions.overflowMode = TextOverflowModes.Truncate;
            boardInstructions.outlineWidth = 0.12f;
            boardInstructions.outlineColor = new Color32(8, 30, 30, 255);
        }

        if (!IsVennChallenge)
        {
            boardInstructions.text = string.Empty;
            return;
        }
        SetsConditionData condition = CurrentChallenge.conditions[conditionIndex];
        string a = condition.setA == null || condition.setA.Length == 0 ? "{}" : "{" + string.Join(", ", condition.setA) + "}";
        string b = condition.setB == null || condition.setB.Length == 0 ? "{} (empty set)" : "{" + string.Join(", ", condition.setB) + "}";
        boardInstructions.text = $"<color=#F4D794>Challenge 1</color>\nCondition {conditionIndex + 1} of {CurrentChallenge.conditions.Count}\n\nA = {a}    B = {b}\nU = {{1, 2, 3, 4, 5, 6, 7, 8, 9}}\n\nLeft: A - B   Middle: A ∩ B   Right: B - A\nLeave unused numbers at the spawner.\n" +
            (waitingForActivation ? "Interact with the Book Statue to start." : "Press Submit when the diagram is complete.");
    }

    private void ClearCurrentElements()
    {
        foreach (SetElement element in activeElements)
        {
            if (element == null) continue;
            element.ReturnToSpawn();
            Destroy(element.gameObject);
        }
        activeElements.Clear();
    }

    public static string FormatZone(SetZone zone)
    {
        switch (zone)
        {
            case SetZone.A_ONLY: return "A ONLY";
            case SetZone.INTERSECTION: return "A ∩ B";
            case SetZone.B_ONLY: return "B ONLY";
            default: return "OUTSIDE A (inside U)";
        }
    }

#if UNITY_EDITOR
    public void ConfigureForScene(
        SetsChallengeCatalog catalog,
        SetElement prefab,
        Transform spawnPoint,
        SetPlacementZone[] zones,
        LevelManager timer,
        GameOverManager gameOver,
        StageCompleteManager stageComplete,
        SetsUIController ui)
    {
        challengeCatalog = catalog;
        elementPrefab = prefab;
        elementSpawnPoint = spawnPoint;
        placementZones = zones;
        levelManager = timer;
        gameOverManager = gameOver;
        stageCompleteManager = stageComplete;
        uiController = ui;
    }
#endif
}
