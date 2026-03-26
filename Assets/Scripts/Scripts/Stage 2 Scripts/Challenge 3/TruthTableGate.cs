using UnityEngine;

public class TruthTableGate : MonoBehaviour
{
    [Header("Puzzle Requirements")]
    [Tooltip("Drag the 3 RED CRYSTALS from the gate in here")]
    [SerializeField] private GateIndicator[] requiredIndicators;
    
    [Header("Gate Objects")]
    [Tooltip("Drag the vines you want to disappear here")]
    [SerializeField] private GameObject vinesToHide;

    // Called by the indicators whenever they light up
    public void EvaluateGate()
    {
        bool allPowered = true;

        // Loop through all 3 indicators to see if they are green
        foreach (GateIndicator indicator in requiredIndicators)
        {
            if (!indicator.isPoweredCorrectly)
            {
                allPowered = false;
                break; // One is wrong, stop checking
            }
        }

        // If all 3 are correct, open the gate!
        if (allPowered)
        {
            OpenGate();
        }
    }

    private void OpenGate()
    {
        if (vinesToHide != null)
        {
            vinesToHide.SetActive(false);
            Debug.Log("The OR Gate is solved! Vines removed.");
        }
    }
}