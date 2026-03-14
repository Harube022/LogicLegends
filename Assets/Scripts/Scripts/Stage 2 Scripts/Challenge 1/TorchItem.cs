using UnityEngine;

public class TorchItem : MonoBehaviour
{
    [SerializeField] private bool isLit = false;
    private bool startingState; // Remembers how it spawned!

    [Header("Torch Models")]
    [SerializeField] private GameObject litModel;
    [SerializeField] private GameObject unlitModel;

    public bool IsLit => isLit;

    private void Awake()
    {
        startingState = isLit; // Save the original state
    }

    private void Start()
    {
        SetState(isLit);
    }

    public void SetState(bool state)
    {
        isLit = state;
        if (litModel != null) litModel.SetActive(isLit);
        if (unlitModel != null) unlitModel.SetActive(!isLit);
    }

    // ---> NEW: Reverts the torch to its original puzzle state <---
    public void ResetFlame()
    {
        SetState(startingState);
    }
}