using UnityEngine;
using TMPro;
using Photon.Pun; // 1. Added Photon Namespace

public class FruitValidator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WizardInteraction wizard;
    [SerializeField] private FruitBasket challenge2Basket;
    [SerializeField] private TextMeshProUGUI objectiveText;
    private PhotonView view; // 2. Add PhotonView Reference

    private void Awake()
    {
        view = GetComponent<PhotonView>();
    }

    // We changed the name to reflect what it does now!
    public void EvaluateBasketOrTalk()
    {
        // 1. If basket is empty, tell the wizard to just say his normal tutorial lines
        if (challenge2Basket == null || !challenge2Basket.HasFruit()) 
        {
            if (wizard != null) wizard.StartStandardDialogue();
            return;
        }

        // 2. If there IS fruit, skip the tutorial and judge it instantly!
        if (challenge2Basket.CheckORGate())
        {
            // 3. Tell everyone on the network to cross out the text!
            if (view != null && PhotonNetwork.InRoom) view.RPC("RPC_CrossOutObjective", RpcTarget.All);
            else RPC_CrossOutObjective();
            
            if (wizard != null) wizard.PlaySuccessDialogue();
        }
        else
        {
            if (wizard != null) wizard.PlayFailDialogue();
        }
    }

    [PunRPC]
    private void RPC_CrossOutObjective()
    {
        if (objectiveText != null && !objectiveText.text.Contains("<s>"))
        {
            objectiveText.text = "<color=#008000><s>" + objectiveText.text + "</s></color>";
        }
    }
}