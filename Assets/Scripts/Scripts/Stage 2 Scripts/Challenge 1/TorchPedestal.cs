using UnityEngine;
using UnityEngine.UI;

public class TorchPedestal : MonoBehaviour
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
        if (other.CompareTag("Player") && currentTorch != null) OpenTorchUI();
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && torchUIPanel != null) torchUIPanel.SetActive(false);
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