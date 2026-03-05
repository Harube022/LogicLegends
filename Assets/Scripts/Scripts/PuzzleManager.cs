using UnityEngine;
using TMPro;

public class PuzzleManager : MonoBehaviour
{
    [Header("Wizard Reference")]
    [Tooltip("Drag the Wizard (lefyahj) object here")]
    [SerializeField] private WizardInteraction wizardInteraction;

    [SerializeField] private LogicPuzzle[] puzzleOrder;
    [Header("Task 1: Negation")]
    [Tooltip("Drag the '1. Place True/False...' Text object here")]
    [SerializeField] private TextMeshProUGUI negationTaskText;

    [Header("Task 2: OR")]
    [Tooltip("Drag the '2. Place True/False...' GameObject here so we can hide/show it")]
    [SerializeField] private GameObject orTaskObject;
    [Tooltip("Drag the same Task 2 text component here so we can cross it out later")]
    [SerializeField] private TextMeshProUGUI orTaskText;

    [Header("Task 3: AND")]
    [Tooltip("Drag the AND_Task GameObject here")]
    [SerializeField] private GameObject andTaskObject;
    [Tooltip("Drag the AND_Task TextMeshPro component here")]
    [SerializeField] private TextMeshProUGUI andTaskText;

    [Header("Task 4: IMPLICATION")]
    [Tooltip("Drag the Implication_Task GameObject here")]
    [SerializeField] private GameObject implicationTaskObject;
    [Tooltip("Drag the Implication_Task TextMeshPro component here")]
    [SerializeField] private TextMeshProUGUI implicationTaskText;

    [Header("Task 5: BICONDITIONAL")]
    [Tooltip("Drag the Biconditional_Task GameObject here")]
    [SerializeField] private GameObject biconditionalTaskObject;
    [Tooltip("Drag the Biconditional_Task TextMeshPro component here")]
    [SerializeField] private TextMeshProUGUI biconditionalTaskText;


    private int currentPuzzleIndex = 0;

    private void Start()
    {
        ActivateOnlyCurrentPuzzle();

        // 1. Hide the OR task when the game starts
        if (orTaskObject != null) orTaskObject.SetActive(false);
        // Hide the new tasks when the game starts
        if (andTaskObject != null) andTaskObject.SetActive(false);
        if (implicationTaskObject != null) implicationTaskObject.SetActive(false);
        if (biconditionalTaskObject != null) biconditionalTaskObject.SetActive(false);
    }

    
    private void ActivateOnlyCurrentPuzzle()
    {
        for (int i = 0; i < puzzleOrder.Length; i++)
        {
            puzzleOrder[i].SetActiveState(i == currentPuzzleIndex);
        }
    }

    public void NotifyPuzzleCompleted(LogicPuzzle completedPuzzle)
    {
        // 2. Did they just finish the NEGATION table?
        if (completedPuzzle.PuzzleLogicType == LogicType.NOT)
        {
            Debug.Log("Negation Table Complete! Updating UI...");
            
            // Cross out Task 1 and make it grey
            if (negationTaskText != null && !negationTaskText.text.StartsWith("<s>"))
            {
                negationTaskText.text = "<color=#008000><s>" + negationTaskText.text + "</s></color>";
            }

            // Reveal Task 2
            if (orTaskObject != null)
            {
                orTaskObject.SetActive(true);
            }
        }
        // 3. Did they just finish the OR table?
        else if (completedPuzzle.PuzzleLogicType == LogicType.OR)
        {
            Debug.Log("OR Table Complete! Updating UI...");
            
            // Cross out Task 2
            if (orTaskText != null && !orTaskText.text.StartsWith("<s>"))
            {
                orTaskText.text = "<color=#008000><s>" + orTaskText.text + "</s></color>";
            }
            
            // You can add code here to reveal Task 3, or trigger the level ending!
            // NEW: Reveal Task 3 (AND)
            if (andTaskObject != null) andTaskObject.SetActive(true);
        }
        else if (completedPuzzle.PuzzleLogicType == LogicType.AND)
        {
            Debug.Log("AND Table Complete! Updating UI...");
            
            if (andTaskText != null && !andTaskText.text.Contains("<s>"))
            {
                andTaskText.text = "<color=#008000><s>" + andTaskText.text + "</s></color>";
            }
            
            // Reveal Task 4 (IMPLICATION)
            if (implicationTaskObject != null) implicationTaskObject.SetActive(true);
        }
        // 5. Did they just finish the IMPLICATION table?
        else if (completedPuzzle.PuzzleLogicType == LogicType.IMPLICATION) // Make sure this matches your LogicType enum!
        {
            Debug.Log("IMPLICATION Table Complete! Updating UI...");
            
            if (implicationTaskText != null && !implicationTaskText.text.Contains("<s>"))
            {
                implicationTaskText.text = "<color=#008000><s>" + implicationTaskText.text + "</s></color>";
            }
            
            // Reveal Task 5 (BICONDITIONAL)
            if (biconditionalTaskObject != null) biconditionalTaskObject.SetActive(true);
        }
        // 6. Did they just finish the BICONDITIONAL table?
        else if (completedPuzzle.PuzzleLogicType == LogicType.BICONDITIONAL) // Make sure this matches your LogicType enum!
        {
            Debug.Log("BICONDITIONAL Table Complete! Updating UI...");
            
            if (biconditionalTaskText != null && !biconditionalTaskText.text.Contains("<s>"))
            {
                biconditionalTaskText.text = "<color=#008000><s>" + biconditionalTaskText.text + "</s></color>";
            }
            
            // All logic table tasks complete! You can trigger end-of-level logic here.
            // --- NEW: Tell the Wizard all tables are done! ---
            if (wizardInteraction != null)
            {
                wizardInteraction.UnlockFinalDialogue();
            }
        }


        if (puzzleOrder[currentPuzzleIndex] != completedPuzzle)
            return;

        currentPuzzleIndex++;

        if (currentPuzzleIndex >= puzzleOrder.Length)
        {
            Debug.Log("All puzzles completed!");
            return;
        }

        Debug.Log("Next puzzle unlocked: " + puzzleOrder[currentPuzzleIndex].name);

        ActivateOnlyCurrentPuzzle();
    }

    // NEW: Call this to reset the puzzles for replay
    public void RestartPuzzles()
    {
        Debug.Log("Resetting puzzles for replay...");
        currentPuzzleIndex = 0;
        ActivateOnlyCurrentPuzzle();
        
        // We do NOT touch the UI texts or GameObjects here, 
        // so all your crossed-out tasks will remain visible and crossed out!
    }
}
