using UnityEngine;

public class LogicSlot : MonoBehaviour
{
    [SerializeField] private LogicPuzzle parentPuzzle;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent(out TruthBlock block))
            return;

        bool accepted = parentPuzzle.TryPlace(block);

        if (!accepted)
        {
            // Reject incorrect placement
            // Option 1: Do nothing (block falls back)
            // Option 2: Push block slightly back
        }
    }
}