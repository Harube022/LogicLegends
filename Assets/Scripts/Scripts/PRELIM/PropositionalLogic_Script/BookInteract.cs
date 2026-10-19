using UnityEngine;
using UnityEngine.UI;

public class BookInteract : MonoBehaviour
{
    [SerializeField] private GameObject interactButton;
    [SerializeField] private QuizManager quizManager;
    [SerializeField] private LevelTimerManager timerManager;
    private bool hasBeenInteracted;
    private Collider[] interactionZones;
    public bool IsReadyForInteraction => !hasBeenInteracted && isActiveAndEnabled;

    private void Start()
    {
        // Retain the serialized reference for existing prefab instances, but remove
        // the obsolete button's input path. The hand is the only book control.
        if (interactButton != null)
        {
            var button = interactButton.GetComponent<Button>();
            if (button != null) { button.onClick.RemoveAllListeners(); button.interactable = false; }
            interactButton.SetActive(false);
        }
        if (timerManager == null) timerManager = Object.FindFirstObjectByType<LevelTimerManager>();
        interactionZones = GetComponentsInChildren<Collider>(true);
        BookStatuePresentation.BindHand(transform, CanUseHand, ActivateFromHand);
    }
    private bool CanUseHand()
    {
        var player = Player.LocalInstance;
        if (!IsReadyForInteraction || player == null || !player.enabled || quizManager == null ||
            quizManager.IsQuizActive || quizManager.IsSequenceComplete ||
            timerManager != null && timerManager.RemainingTime <= 0) return false;
        var room = quizManager.CurrentChallengeForTutorial;
        if (room == null || room.roomChoiceCanvas == null ||
            room.roomChoiceCanvas.transform.parent.GetComponentInChildren<BookInteract>(true) != this) return false;
        var controller = player.GetComponent<CharacterController>();
        if (controller == null || interactionZones == null) return false;
        foreach (var zone in interactionZones)
            if (zone != null && zone.enabled && zone.isTrigger && zone.gameObject.activeInHierarchy && zone.bounds.Intersects(controller.bounds)) return true;
        return false;
    }
    private void ActivateFromHand()
    {
        if (hasBeenInteracted || quizManager == null || quizManager.IsQuizActive || quizManager.IsSequenceComplete ||
            timerManager != null && timerManager.RemainingTime <= 0) return;
        quizManager.OpenQuiz(this);
        timerManager?.SetTimerVisibility(true);
    }
    public void ForceHideButton()
    {
        hasBeenInteracted = true;
        if (interactButton != null) interactButton.SetActive(false);
    }
    public void ResetInteraction() { hasBeenInteracted = false; }
}
