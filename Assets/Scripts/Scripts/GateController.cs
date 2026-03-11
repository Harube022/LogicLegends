using UnityEngine;

public class GateController : MonoBehaviour
{
    [Header("Pressure Plates")]
    [Tooltip("Drag the Left Plate object (the one with the script) here")]
    [SerializeField] private PressurePlate leftPlate;
    [Tooltip("Drag the Right Plate object (the one with the script) here")]
    [SerializeField] private PressurePlate rightPlate;

    [Header("Gate Objects")]
    [SerializeField] private GameObject closeGateObj;
    [SerializeField] private GameObject openGateObj;
    private bool isSolved = false;

    private void Update()
    {
        if (isSolved) return;

        if (leftPlate.isPressed && rightPlate.isPressed)
        {

            isSolved = true;
            leftPlate.LockPlateOn();
            rightPlate.LockPlateOn();
            // Open the gate!
            closeGateObj.SetActive(false);
            openGateObj.SetActive(true);

            // STOP TIMER
            if (LevelManager.Instance != null) LevelManager.Instance.StopTimer();
        }
        else
        {
            // Keep it closed (or close it if someone steps off)
            closeGateObj.SetActive(true);
            openGateObj.SetActive(false);
            
        }
    }

    public void ResetGate()
    {
        isSolved = false;
        leftPlate.ResetPlate();
        rightPlate.ResetPlate();
        closeGateObj.SetActive(true);
        openGateObj.SetActive(false);
    }
}