using UnityEngine;

public class PuzzleManager : MonoBehaviour
{
    [SerializeField] private LogicPuzzle[] puzzleOrder;

    private int currentPuzzleIndex = 0;

    private void Start()
    {
        ActivateOnlyCurrentPuzzle();
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
}