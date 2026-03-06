using UnityEngine;

public class GatePuzzle : MonoBehaviour
{
    [SerializeField] private PuzzleSlot[] slots;

    [SerializeField] private GameObject gateClosed;
    [SerializeField] private GameObject gateOpen;
    [SerializeField] private HealthManager healthManager;

    private bool isOpen;
    private bool hasPenalized; // Tracks if we already took a heart for this specific wrong setup

    private void Start()
    {
        // Automatically give each slot a reference to this GatePuzzle manager
        foreach (var slot in slots)
        {
            slot.gatePuzzle = this;
        }
    }

    // private void Update()
    // {
    //     bool allFilled = true;

    //     foreach (PuzzleSlot slot in slots)
    //     {
    //         if (!slot.HasObject())
    //         {
    //             allFilled = false;
    //             break;
    //         }
    //     }

    //     // If something missing → must be closed
    //     if (!allFilled)
    //     {
    //         CloseGate();
    //         return;
    //     }

    //     // check correctness
    //     foreach (PuzzleSlot slot in slots)
    //     {
    //         if (!slot.IsCorrect())
    //         {
    //             Debug.Log("There's an error.");
    //             CloseGate();
    //             return;
    //         }
    //     }

    //     // all correct
    //     OpenGate();
    // }

    
    // Called by PuzzleSlot.cs ONLY when a piece is added or removed
    public void CheckPuzzleState()
    {
        bool isComplete = true;
        bool hasError = false;

        // Check every slot on the board
        foreach (PuzzleSlot slot in slots)
        {
            if (!slot.HasObject())
            {
                isComplete = false; // The puzzle isn't fully filled out yet
            }
            else if (!slot.IsCorrect())
            {
                hasError = true; // There is a block placed, but it's the WRONG one!
            }
        }

        // 1. If there is ANY wrong block currently placed on the board
        if (hasError)
        {
            Debug.Log("There's an error.");
            CloseGate();

            // Deduct a heart if we haven't already for this specific mistake
            if (!hasPenalized)
            {
                if (healthManager != null) healthManager.TakeDamage();
                hasPenalized = true;
            }
            
            return; // Stop running the rest of the code
        }

        // 2. If there are NO errors on the board right now (it's either empty or partially correct)
        // We reset the penalty flag. This means if they make a mistake again, they lose another heart.
        hasPenalized = false; 

        // 3. Are all slots filled perfectly?
        if (isComplete)
        {
            OpenGate();
        }
        else
        {
            CloseGate(); // Partially filled but correct, keep gate closed until finished
        }
    }

    private void OpenGate()
    {
        if (isOpen) return;

        isOpen = true;

        gateClosed.SetActive(false);
        gateOpen.SetActive(true);

        Debug.Log("Well done, you solved the puzzle!");
    }

    private void CloseGate()
    {
        if (!isOpen) return;

        isOpen = false;

        gateClosed.SetActive(true);
        gateOpen.SetActive(false);

        Debug.Log("Gate closed.");
    }
}