using UnityEngine;

public class GatePuzzle : MonoBehaviour
{
    [SerializeField] private PuzzleSlot[] slots;

    [SerializeField] private GameObject gateClosed;
    [SerializeField] private GameObject gateOpen;

    private bool isOpen;

    private void Update()
    {
        bool allFilled = true;

        foreach (PuzzleSlot slot in slots)
        {
            if (!slot.HasObject())
            {
                allFilled = false;
                break;
            }
        }

        // If something missing → must be closed
        if (!allFilled)
        {
            CloseGate();
            return;
        }

        // check correctness
        foreach (PuzzleSlot slot in slots)
        {
            if (!slot.IsCorrect())
            {
                Debug.Log("There's an error.");
                CloseGate();
                return;
            }
        }

        // all correct
        OpenGate();
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