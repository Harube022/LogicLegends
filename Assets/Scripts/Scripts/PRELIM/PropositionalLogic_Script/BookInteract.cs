// using UnityEngine;
// using UnityEngine.UI;

// public class BookInteract: MonoBehaviour
// {
//     [Header("UI References")]
//     [Tooltip("Drag your Hand/Read Button here")]
//     [SerializeField] private GameObject interactButton; 
    
//     [Tooltip("Drag the GameObject that has your QuizManager script here")]
//     [SerializeField] private QuizManager quizManager; 

//     [Header("Timer System Link")]
//     [Tooltip("Optional: Drag your LevelTimerManager here (will find automatically if left empty)")]
//     [SerializeField] private LevelTimerManager timerManager;

//     private Button btnComponent;
//     private bool hasBeenInteracted = false; // Tracks if this book was just accessed

//     private void Start()
//     {
//         // Get the actual Button component from the GameObject
//         if (interactButton != null)
//         {
//             btnComponent = interactButton.GetComponent<Button>();
//             // Ensure the button is hidden when the game starts
//             interactButton.SetActive(false); 
//         }
//         // Auto-locate the timer controller in the stage scene if not manually configured
//         if (timerManager == null)
//         {
//             timerManager = Object.FindFirstObjectByType<LevelTimerManager>();
//         }
//     }

//     private void OnTriggerEnter(Collider other)
//     {
//         // SAFETY CHECK: Do not show the prompt if the game is over/timer is spent!
//         if (timerManager != null && !timerManager.IsTimerRunning && LevelTimerManager.savedTopicIndex > 0)
//         {
//             return;
//         }
//         // When the player walks INTO the trigger zone
//         if (other.CompareTag("Player") && !hasBeenInteracted)
//         {
//             // NEW CONDITION: Do NOT show the button if the quiz panel is already showing,
//             // or if the player already completed a selection pad charge and hid the panel.
//             if (quizManager != null && (quizManager.IsQuizActive || hasBeenInteracted))
//             {
//                 return;
//             }

//             if (interactButton != null)
//             {
//                 interactButton.SetActive(true);
//                 if (btnComponent != null)
//                 {
//                     btnComponent.onClick.RemoveAllListeners(); 
//                     btnComponent.onClick.AddListener(OnInteractClicked); 
//                 }
//             }
//         }
//     }

//     private void OnTriggerStay(Collider other)
//     {
//         // Continuous check while the player remains inside the bookstand zone.
//         // If they walked away, charged a pad, and came back, this keeps the button hidden safely.
//         if (other.CompareTag("Player"))
//         {
//             if (quizManager != null && (quizManager.IsQuizActive || hasBeenInteracted))
//             {
//                 if (interactButton != null && interactButton.activeSelf)
//                 {
//                     interactButton.SetActive(false);
//                 }
//             }
//             else
//             {
//                 // If the quiz isn't showing and they haven't locked in an answer, 
//                 // make sure the button stays visible while standing near the book
//                 if (interactButton != null && !interactButton.activeSelf)
//                 {
//                     interactButton.SetActive(true);
//                 }
//             }
//         }
//     }

//     private void OnTriggerExit(Collider other)
//     {
//         if (other.CompareTag("Player"))
//         {
//             if (interactButton != null)
//             {
//                 interactButton.SetActive(false);
//             }
//         }
//     }

//     private void OnInteractClicked()
//     {
//         // SAFETY CHECK: Double check that the game hasn't expired before allowing menu canvas layout to swap
//         if (timerManager != null && !timerManager.IsTimerRunning && LevelTimerManager.savedTopicIndex > 0)
//         {
//             interactButton.SetActive(false);
//             return;
//         }
        
//         // 1. Hide the interact button so it isn't covering the quiz
//         if (interactButton != null) interactButton.SetActive(false);
        
//         // 2. Tell the QuizManager to show the question
//         if (quizManager != null)
//         {
//             quizManager.OpenQuiz(this);
//         }
//     }

//     // Public method called by the QuizManager to turn off the UI button
//     public void ForceHideButton()
//     {
//         hasBeenInteracted = true;
//         if (interactButton != null)
//         {
//             interactButton.SetActive(false);
//         }
//     }

//     public void ResetInteraction()
//     {
//         hasBeenInteracted = false;
//     }
// }

using UnityEngine;
using UnityEngine.UI;

public class BookInteract : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Drag your Hand/Read Button here")]
    [SerializeField] private GameObject interactButton; 
    
    [Tooltip("Drag the GameObject that has your QuizManager script here")]
    [SerializeField] private QuizManager quizManager; 

    [Header("Timer System Link")]
    [Tooltip("Optional: Drag your LevelTimerManager here (will find automatically if left empty)")]
    [SerializeField] private LevelTimerManager timerManager;

    private Button btnComponent;
    private bool hasBeenInteracted = false; 

    private void Start()
    {
        if (interactButton != null)
        {
            btnComponent = interactButton.GetComponent<Button>();
            interactButton.SetActive(false); 
        }
        if (timerManager == null)
        {
            timerManager = Object.FindFirstObjectByType<LevelTimerManager>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (timerManager != null && !timerManager.IsTimerRunning && LevelTimerManager.savedTopicIndex > 0)
        {
            return;
        }

        if (other.CompareTag("Player") && !hasBeenInteracted)
        {
            if (quizManager != null && (quizManager.IsQuizActive || hasBeenInteracted))
            {
                return;
            }

            if (interactButton != null)
            {
                interactButton.SetActive(true);
                if (btnComponent != null)
                {
                    btnComponent.onClick.RemoveAllListeners(); 
                    btnComponent.onClick.AddListener(OnInteractClicked); 
                }
            }
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (quizManager != null && (quizManager.IsQuizActive || hasBeenInteracted))
            {
                if (interactButton != null && interactButton.activeSelf)
                {
                    interactButton.SetActive(false);
                }
            }
            else
            {
                if (interactButton != null && !interactButton.activeSelf)
                {
                    interactButton.SetActive(true);
                }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (interactButton != null)
            {
                interactButton.SetActive(false);
            }
        }
    }

    private void OnInteractClicked()
    {
        if (timerManager != null && !timerManager.IsTimerRunning && LevelTimerManager.savedTopicIndex > 0)
        {
            interactButton.SetActive(false);
            return;
        }
        
        if (interactButton != null) interactButton.SetActive(false);
        
        if (quizManager != null)
        {
            quizManager.OpenQuiz(this);
        }

            // NEW: Show the timer text
        if (timerManager != null)
        {
            timerManager.SetTimerVisibility(true);
        }
    }

    public void ForceHideButton()
    {
        hasBeenInteracted = true;
        if (interactButton != null)
        {
            interactButton.SetActive(false);
        }
    }

    public void ResetInteraction()
    {
        hasBeenInteracted = false;
    }
}