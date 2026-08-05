using UnityEngine;

public class DynamicLogicSlot : MonoBehaviour
{
    [SerializeField] private DynamicLogicPuzzle puzzle;
    // [SerializeField] private DynamicPuzzleColumn columnType; // Fixed: Now uses DynamicPuzzleColumn
    [SerializeField] private int columnIndex;
    private void OnTriggerEnter(Collider other)
    {
        // Still checks for TruthBlock base class so it accepts old or new blocks seamlessly
        if (!other.TryGetComponent(out TruthBlock block))
            return;

        puzzle.TryPlace(block, columnIndex);
    }
}