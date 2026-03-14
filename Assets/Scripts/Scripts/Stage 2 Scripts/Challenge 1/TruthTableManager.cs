using UnityEngine;

public class TruthTableManager : MonoBehaviour
{
    [SerializeField] private TorchPedestal[] answerPedestals;
    
    [Header("Portals")]
    [Tooltip("Drag the CLOSED portal asset here")]
    [SerializeField] private GameObject closedPortal;
    [Tooltip("Drag the OPEN portal asset here")]
    [SerializeField] private GameObject openPortal;
    
    private bool isSolved = false;

    private void Update()
    {
        if (isSolved) return;

        bool allCorrect = true;
        foreach (var ped in answerPedestals)
        {
            if (!ped.IsCorrect())
            {
                allCorrect = false;
                break;
            }
        }

        if (allCorrect)
        {
            isSolved = true;
            Debug.Log("NOT Gate Solved!");
            
            // ---> NEW: Swap the portals! <---
            if (closedPortal != null) closedPortal.SetActive(false);
            if (openPortal != null) openPortal.SetActive(true);
        }
    }
}