using UnityEngine;
using TMPro;
using Photon.Pun; // 1. Added Photon Namespace

public class FruitValidator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WizardInteraction wizard;
    [SerializeField] private FruitBasket challenge2Basket;
    [SerializeField] private TextMeshProUGUI objectiveText;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip crossOutTaskClip;
    [SerializeField] private AudioSource wizardVoiceSource;
    [SerializeField] private AudioClip wizardCongratulationClip;
    private PhotonView view; // 2. Add PhotonView Reference

    // ---> NEW: A string to memorize the clean text <---
    private string originalObjectiveString;

    private void Start()
    {
        // ---> NEW: Save the text exactly as it is when the game starts <---
        if (objectiveText != null)
        {
            originalObjectiveString = objectiveText.text;
        }
    }

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
            //Play the fail dialogue
            if (wizard != null) wizard.PlayFailDialogue();
        }
    }

    // ---> NEW METHOD: The Wizard will call this when the dialogue finishes <---
    public void ExecuteFailConsequences()
    {
        // To prevent losing 2 hearts at once, we only let the Master Client issue the penalty
        if (PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient) return;

        // Clear the wrong fruit so they can try again
        if (challenge2Basket != null) challenge2Basket.ClearBasket();
        
        // Tell the LevelManager it was a shared team mistake
        if (LevelManager.Instance != null) 
        {
            LevelManager.Instance.LoseHeart(); 
        }
    }

    [PunRPC]
    private void RPC_CrossOutObjective()
    {
        if (objectiveText != null && !objectiveText.text.Contains("<s>"))
        {
            objectiveText.text = "<color=#008000><s>" + objectiveText.text + "</s></color>";
            // ---> NEW: Play the Cross Out Sound <---
            if (audioSource != null && crossOutTaskClip != null)
            {
                audioSource.PlayOneShot(crossOutTaskClip);
            }
            if (wizardVoiceSource != null && wizardCongratulationClip != null)
            {
                wizardVoiceSource.Stop(); // prevent overlap
                wizardVoiceSource.PlayOneShot(wizardCongratulationClip);
            }
        }
        
    }

    // ---> NEW: The method to undo the cross-out! <---
    public void ResetValidatorText()
    {
        if (objectiveText != null && !string.IsNullOrEmpty(originalObjectiveString))
        {
            objectiveText.text = originalObjectiveString;
        }
    }
}