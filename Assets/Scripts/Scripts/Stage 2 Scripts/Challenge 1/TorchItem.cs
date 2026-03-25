using UnityEngine;
using Photon.Pun;
public class TorchItem : MonoBehaviourPun
{
    [SerializeField] private bool isLit = false;
    private bool startingState;

    [Header("Torch Models")]
    [SerializeField] private GameObject litModel;
    [SerializeField] private GameObject unlitModel;

    public bool IsLit => isLit;

    private void Awake()
    {
        startingState = isLit;
    }

    private void Start()
    {
        // Keep this local for initialization
        UpdateModels(isLit); 
    }

    // Call this to sync the state across the network
    public void SetState(bool state)
    {
        // If we are playing multiplayer, tell everyone to change the flame
        if (PhotonNetwork.InRoom)
        {
            photonView.RPC("RPC_SetFlameState", RpcTarget.All, state);
        }
        else
        {
            // If we are playing solo, just change it locally right now!
            isLit = state;
            UpdateModels(isLit);
        }
    }

    [PunRPC]
    private void RPC_SetFlameState(bool state)
    {
        isLit = state;
        UpdateModels(isLit);
    }

    private void UpdateModels(bool state)
    {
        if (litModel != null) litModel.SetActive(state);
        if (unlitModel != null) unlitModel.SetActive(!state);
    }

    public void ResetFlame()
    {
        SetState(startingState); // This now networks the reset automatically!
    }
}