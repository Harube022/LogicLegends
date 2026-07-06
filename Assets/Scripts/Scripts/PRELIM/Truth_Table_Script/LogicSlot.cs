using UnityEngine;

public class LogicSlot : MonoBehaviour
{
    [SerializeField] private LogicPuzzle puzzle;
    [SerializeField] private PuzzleColumn columnType;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent(out TruthBlock block))
            return;

        puzzle.TryPlace(block, columnType);
    }
}