using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(LineRenderer))]
public class CrystalTurret : MonoBehaviourPun
{
    [Header("Truth Table Logic")]
    [SerializeField] private bool isTrueCrystal; 

    [Header("Beam Settings")]
    [SerializeField] private Transform beamOrigin; 
    [SerializeField] private float maxBeamDistance = 20f;
    [SerializeField] private LayerMask obstacleLayer; 

    [Header("Visuals")]
    [SerializeField] private Material greenBeamMat;
    [SerializeField] private Material redBeamMat;

    private LineRenderer lineRenderer;
    private GateIndicator currentTarget; // NEW: Remembers the specific indicator we are hitting

    private bool playerInRange = false;
    private GameInput gameInput;

    private Quaternion startRotation;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.startWidth = 0.1f;
        lineRenderer.endWidth = 0.1f;
        lineRenderer.material = redBeamMat;
    }

    private void Start()
    {
        // ---> NEW: Memorize exactly where it is facing when the game starts! <---
        startRotation = transform.rotation;

        gameInput = FindFirstObjectByType<GameInput>();
        if (gameInput != null) gameInput.OnInteractAction += GameInput_OnInteractAction;
    }

    private void OnDestroy()
    {
        if (gameInput != null) gameInput.OnInteractAction -= GameInput_OnInteractAction;
    }

    private void Update()
    {
        CastBeam();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PhotonView pv = other.GetComponent<PhotonView>();
            if (!PhotonNetwork.InRoom || (pv != null && pv.IsMine))
            {
                playerInRange = true;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PhotonView pv = other.GetComponent<PhotonView>();
            if (pv != null && !pv.IsMine) return;

            playerInRange = false;
            // if (DialogueManager.Instance != null) DialogueManager.Instance.ToggleInteractButton(false);
        }
    }

    private void GameInput_OnInteractAction(object sender, System.EventArgs e)
    {
        if (playerInRange)
        {
            InteractRotate();
        }
    }

    private void CastBeam()
    {
        lineRenderer.SetPosition(0, beamOrigin.position);

        // Shoot the raycast
        if (Physics.Raycast(beamOrigin.position, beamOrigin.forward, out RaycastHit hit, maxBeamDistance, obstacleLayer))
        {
            lineRenderer.SetPosition(1, hit.point);

            GateIndicator hitIndicator = hit.collider.GetComponent<GateIndicator>();

            // Did we hit an indicator?
            if (hitIndicator != null)
            {
                // Is it a NEW indicator we weren't hitting a frame ago?
                if (currentTarget != hitIndicator)
                {
                    // Turn off the old one if we had one
                    if (currentTarget != null) currentTarget.SetPowerState(false, false); 
                    
                    // Turn on the new one
                    currentTarget = hitIndicator;
                    currentTarget.SetPowerState(true, isTrueCrystal);

                    if (isTrueCrystal)
                    {
                        lineRenderer.material = greenBeamMat;
                    }
                }
            }
            else 
            {
                // We hit a wall, a tree, or the gate itself (not an indicator)
                TurnOffCurrentTarget();
            }
        }
        else
        {
            // We hit absolutely nothing (shooting into the sky)
            lineRenderer.SetPosition(1, beamOrigin.position + beamOrigin.forward * maxBeamDistance);
            TurnOffCurrentTarget();
        }
    }

    // Helper method to keep code clean
    private void TurnOffCurrentTarget()
    {
        if (currentTarget != null)
        {
            currentTarget.SetPowerState(false, false); // Tell it to turn Red
            currentTarget = null; // Forget it

            lineRenderer.material = redBeamMat;
            
        }
    }

    public void InteractRotate()
    {
        if (PhotonNetwork.InRoom) photonView.RPC("RPC_RotateCrystal", RpcTarget.All);
        else RPC_RotateCrystal(); 
    }

    [PunRPC]
    public void RPC_RotateCrystal()
    {
        transform.Rotate(0, 90f, 0);
    }

    // ---> NEW: Add this to the very bottom of the script <---
    public void ResetTurret()
    {
        // Snap back to original rotation
        transform.rotation = startRotation;
        
        if (currentTarget != null)
        {
            currentTarget.ResetIndicator();
            currentTarget = null;

            lineRenderer.material = redBeamMat;
        }
    }
}