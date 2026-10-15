using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ProofsLibraryStageController
{
#if UNITY_EDITOR
    public static bool SuppressProgressionForValidation { get; set; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetValidationFlag() { SuppressProgressionForValidation = false; }
#endif
    public ProofsStageUI Presentation { get; private set; }
    public bool OnboardingPending { get; private set; } = true;
    public ProofsControlsOnboarding Tutorial => ProofsControlsOnboarding.Active != null && ProofsControlsOnboarding.Active.IsActive ? ProofsControlsOnboarding.Active : null;
    public bool GameplayPaused => OnboardingPending || Tutorial != null || Presentation != null && Presentation.Help != null && Presentation.Help.IsOpen;
    public bool InputSuppressed => GameplayPaused || Presentation != null && Presentation.BoardView != null && Presentation.BoardView.Viewing;
    public bool HasReaderOpen => readerPanel.activeSelf || takeBackPanel != null && takeBackPanel.activeSelf;
    public int ChallengeCount => catalog.challenges.Length;
    public float StartingSeconds => startingSeconds;
    public float WrongPenalty => wrongPenalty;
    public Transform BookTarget => bookStatue;
    public Transform BoardTarget => stepBoardWorld;
    public TMP_Text BoardInstructions => boardText;
    public TMP_Text ClockText => timerText;
    public TMP_Text PromptText => promptText;
    public Transform[] AltarTargets => pedestals;
    public ProofWorldPedestal[] ScrollTargets => scrollSources;
    public GameObject PracticeScrollModel => carriedVisuals[0];
    public GameObject ReaderPanel => readerPanel;
    public TMP_Text ReaderTitle => readerTitle;
    public TMP_Text ReaderBody => readerBody;
    public Button CollectButton => carryButton;
    public Button ReaderCloseButton => closeButton;
    public GameObject TakeBackPanel => takeBackPanel;
    public TMP_Text TakeBackTitle => takeBackTitle;
    public Button[] TakeBackButtons => takeBackButtons;
    public ProofWorldBoardController WorldBoard => worldBoard;
    public void FinishOnboarding() { OnboardingPending = false; }
    public void InteractNearby() { OnInteract(this, System.EventArgs.Empty); }
    public bool HasNearbyInteraction => !finished && !transitioning && !HasReaderOpen && !InputSuppressed &&
        input != null && !input.GameplayInputBlocked && player != null && player.enabled && NearestTarget() >= 0;
}
