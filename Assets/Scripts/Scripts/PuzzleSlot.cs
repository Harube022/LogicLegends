using UnityEngine;

public class PuzzleSlot : MonoBehaviour
{
    public string correctID;
    [SerializeField] private Transform snapPoint;

    [HideInInspector] public GatePuzzle gatePuzzle; // Assigned automatically by GatePuzzle.Start()
    private TowerPiece currentPiece;

    public bool IsCorrect()
    {
        return currentPiece != null && currentPiece.id == correctID;
    }

    public bool HasObject()
    {
        return currentPiece != null;
    }

    public bool TryPlace(TowerPiece piece)
    {
        if (currentPiece != null) return false;

        currentPiece = piece;

        piece.transform.position = snapPoint.position;
        piece.transform.rotation = snapPoint.rotation;

        if (piece.TryGetComponent(out Rigidbody rb))
        {
            rb.isKinematic = true;
        }

        if (piece.TryGetComponent(out GrabbableObject grab))
        {
            grab.SetSlot(this);
        }

        // Notify GatePuzzle to check the new state
        if (gatePuzzle != null) gatePuzzle.CheckPuzzleState();

        return true;
    }

    public void RemovePiece(TowerPiece piece)
    {
        if (currentPiece == piece)
        {
            currentPiece = null;
            // Notify GatePuzzle that a piece was removed
            if (gatePuzzle != null) gatePuzzle.CheckPuzzleState();
        }
    }
}