using UnityEngine;

public class GateIndicator : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField] private MeshRenderer indicatorRenderer;
    [SerializeField] private Material unpoweredMaterial; 
    [SerializeField] private Material poweredMaterial;

    // ---> NEW: What it looks like when hit by a False beam! <---
    [SerializeField] private Material failedMaterial;   

    [Header("Puzzle Link")]
    [Tooltip("Drag the object holding the TruthTableGate script here")]
    [SerializeField] private TruthTableGate myGateManager;

    [Header("State")]
    public bool isPoweredCorrectly = false;

    public void SetPowerState(bool isHitByBeam, bool isGreenBeam)
    {
        // ---> NEW: If the puzzle is already locked/failing, ignore new beams! <---
        if (myGateManager != null && myGateManager.isLocked) return;

        if (isHitByBeam)
        {
            if (isGreenBeam)
            {
                if (!isPoweredCorrectly) // Only change if it wasn't already green
                {
                    indicatorRenderer.material = poweredMaterial;
                    isPoweredCorrectly = true;
                    
                    // Tell the gate to check if we won!
                    if (myGateManager != null) myGateManager.EvaluateGate();
                }
            }
            else
            {
                // ---> NEW: HIT BY A RED BEAM! Trigger the penalty! <---
                indicatorRenderer.material = failedMaterial;
                isPoweredCorrectly = false;
                
                if (myGateManager != null) myGateManager.TriggerFailSequence();
            }
        }
        else
        {
            indicatorRenderer.material = unpoweredMaterial;
            isPoweredCorrectly = false;
        }
    }
    
    // ---> NEW: Forces the crystal to wipe its color back to default! <---
    public void ResetIndicator()
    {
        isPoweredCorrectly = false;
        if (indicatorRenderer != null) indicatorRenderer.material = unpoweredMaterial;
    }
}