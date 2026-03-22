using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
public class TorchPedestal : MonoBehaviourPun
{
    private TorchItem currentTorch; 
    public TorchItem CurrentTorch => currentTorch;

    [SerializeField] private Transform snapPoint;
    
    [Header("Truth Table Logic")]
    [SerializeField] private bool expectedToBeLit; 

    [Header("UI Pop-Up")]
    [SerializeField] private GameObject torchUIPanel;
    [SerializeField] private Button litButton;
    [SerializeField] private Button unlitButton;

    public void PlaceTorchNetworked(GameObject torchObj)
    {
        PhotonView torchView = torchObj.GetComponent<PhotonView>();
        if (torchView != null)
        {
            // Tell all clients to snap THIS specific torch to THIS pedestal
            photonView.RPC("RPC_PlaceTorch", RpcTarget.All, torchView.ViewID);

            OpenTorchUI();
        }
    }

    [PunRPC]
    public void RPC_PlaceTorch(int torchViewID)
    {
        PhotonView torchView = PhotonView.Find(torchViewID);
        if (torchView != null)
        {
            GameObject torchObj = torchView.gameObject;
            TorchItem torch = torchObj.GetComponent<TorchItem>();
            
            if (torch != null)
            {
                currentTorch = torch;
                torchObj.transform.position = snapPoint.position;
                torchObj.transform.rotation = snapPoint.rotation;

                Rigidbody rb = torchObj.GetComponent<Rigidbody>();
                if (rb != null) 
                {
                    rb.isKinematic = true;
                    rb.useGravity = false; 
                }

                if (torchObj.TryGetComponent(out GrabbableObject grab)) grab.enabled = false;
            }
        }
    }

    public void PlaceTorch(GameObject torchObj)
    {
        TorchItem torch = torchObj.GetComponent<TorchItem>();
        if (torch != null)
        {
            currentTorch = torch;
            torchObj.transform.position = snapPoint.position;
            torchObj.transform.rotation = snapPoint.rotation;

            Rigidbody rb = torchObj.GetComponent<Rigidbody>();
            if (rb != null) 
            {
                rb.isKinematic = true;
                rb.useGravity = false; 
            }

            // ---> FIXED: Disable the grab script instead of destroying it! <---
            if (torchObj.TryGetComponent(out GrabbableObject grab)) grab.enabled = false;

            OpenTorchUI();
        }
    }
        public void RemoveTorch()
    {
        currentTorch = null;
        if (torchUIPanel != null) torchUIPanel.SetActive(false); 
    }

    // ---> NEW: Clears the pedestal's memory and tells the torch to reset! <---
    public void ClearPedestal()
    {
        if (currentTorch != null)
        {
            ResettableObject resettable = currentTorch.GetComponent<ResettableObject>();
            if (resettable != null) resettable.ResetPosition();

            currentTorch = null;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && currentTorch != null) 
        {
            PhotonView playerView = other.GetComponent<PhotonView>();
            // Only show UI if the object has a PhotonView AND it belongs to this specific computer
            if (playerView != null && playerView.IsMine) 
            {
                OpenTorchUI();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && torchUIPanel != null) 
        {
            PhotonView playerView = other.GetComponent<PhotonView>();
            if (playerView != null && playerView.IsMine)
            {
                torchUIPanel.SetActive(false);
            }
        }
    }

    public void OpenTorchUI()
    {
        if (torchUIPanel != null)
        {
            torchUIPanel.SetActive(true);
            litButton.onClick.RemoveAllListeners();
            unlitButton.onClick.RemoveAllListeners();
            litButton.onClick.AddListener(() => ChooseState(true));
            unlitButton.onClick.AddListener(() => ChooseState(false));
        }
    }

    private void ChooseState(bool isLit)
    {
        if (currentTorch != null) currentTorch.SetState(isLit);
        if (torchUIPanel != null) torchUIPanel.SetActive(false);
    }

    public bool IsCorrect()
    {
        if (currentTorch == null) return false;
        return currentTorch.IsLit == expectedToBeLit; 
    }
}