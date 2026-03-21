using UnityEngine;
using Photon.Pun; // 1. Added Photon namespace

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
    private PhotonView view; // 2. Add PhotonView reference

    private void Awake()
    {
        view = GetComponent<PhotonView>();
    }

    private void Update()
    {
        if (isSolved) return;

        // 3. ONLY the Master Client checks the puzzle state to prevent multiple people solving it at once
        if (leftPlate.isPressed && rightPlate.isPressed)
        {
            if (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient)
            {
                view.RPC("RPC_OpenGate", RpcTarget.All);
            }
            else if (!PhotonNetwork.InRoom)
            {
                // Fallback for solo testing
                RPC_OpenGate(); 
            }
        }
        else
        {
            // Keep it closed (or close it if someone steps off)
            closeGateObj.SetActive(true);
            openGateObj.SetActive(false);
            
        }
    }

    // 4. Move the solving logic into this networked method
    [PunRPC]
    public void RPC_OpenGate()
    {
        isSolved = true;
        leftPlate.LockPlateOn();
        rightPlate.LockPlateOn();
        
        // Open the gate!
        closeGateObj.SetActive(false);
        openGateObj.SetActive(true);

        // STOP TIMER
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.StopTimer();
            LevelManager.Instance.ResetTimer();
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