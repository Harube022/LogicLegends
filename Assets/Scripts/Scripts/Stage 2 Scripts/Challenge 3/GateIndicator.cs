using UnityEngine;

public class GateIndicator : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField] private MeshRenderer indicatorRenderer;
    [SerializeField] private Material unpoweredMaterial; 
    [SerializeField] private Material poweredMaterial;   

    [Header("Puzzle Link")]
    [Tooltip("Drag the object holding the TruthTableGate script here")]
    [SerializeField] private TruthTableGate myGateManager;

    [Header("State")]
    public bool isPoweredCorrectly = false;

    public void SetPowerState(bool isHitByBeam, bool isGreenBeam)
    {
        if (isHitByBeam && isGreenBeam)
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
            indicatorRenderer.material = unpoweredMaterial;
            isPoweredCorrectly = false;
        }
    }
}