using UnityEngine;
using TMPro;
using Photon.Pun; // 1. Added Photon Namespace

public class FruitValidator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WizardInteraction wizard;
    [SerializeField] private FruitBasket challenge2Basket;
    [SerializeField] private TextMeshProUGUI objectiveText;

    [Tooltip("Drag the 'Wizard_Confirm_Task' GameObject here")]
    [SerializeField] private GameObject talkToWizardTaskObj;

    [Tooltip("Drag the 'Proceed_Gate_Task' GameObject here")]
    [SerializeField] private GameObject proceedToGateTaskObj;
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip crossOutTaskClip;
    [SerializeField] private AudioSource wizardVoiceSource;
    [SerializeField] private AudioClip wizardCongratulationClip;
    private PhotonView view; // 2. Add PhotonView Reference

    // ---> NEW: A string to memorize the clean text <---
    private string originalBaseTaskString;
    private bool isWizardTaskShowing = false;
    private bool isSolved = false; // Stops checking once the correct fruit is found

    private void Start()
    {
        // ---> NEW: Save the text exactly as it is when the game starts <---
        if (objectiveText != null)
        {
          originalBaseTaskString = objectiveText.text;
        }

        // 2. Ensure our toggleable tasks start in the correct hidden state
        if (talkToWizardTaskObj != null) talkToWizardTaskObj.SetActive(false);
        if (proceedToGateTaskObj != null) proceedToGateTaskObj.SetActive(false);
    }

    private void Awake()
    {
        view = GetComponent<PhotonView>();
    }
    
    private void Update()
    {
        if (isSolved || challenge2Basket == null) return;

        bool hasFruit = challenge2Basket.HasFruit();

        // 1. If a fruit is placed -> Show the static Wizard Confirm Object!
        if (hasFruit && !isWizardTaskShowing)
        {
            if (talkToWizardTaskObj != null) talkToWizardTaskObj.SetActive(true);
            isWizardTaskShowing = true;
        }
        // 2. If the basket is empty -> Hide the static Wizard Confirm Object!
        else if (!hasFruit && isWizardTaskShowing)
        {
            if (talkToWizardTaskObj != null) talkToWizardTaskObj.SetActive(false);
            isWizardTaskShowing = false;
        }
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
        isSolved = true; // Stop the Update loop

        // 1. Cross out the main fruit text
        if (objectiveText != null && !objectiveText.text.Contains("<s>"))
        {
            objectiveText.text = "<color=#008000><s>" + originalBaseTaskString + "</s></color>";
        }

        // 2. Hide the temporary wizard task object
        if (talkToWizardTaskObj != null) talkToWizardTaskObj.SetActive(false);

        // 3. Show the final gate task object
        if (proceedToGateTaskObj != null) proceedToGateTaskObj.SetActive(true);

        if (audioSource != null && crossOutTaskClip != null)
        {
            audioSource.PlayOneShot(crossOutTaskClip);
        }
        if (wizardVoiceSource != null && wizardCongratulationClip != null)
        {
            wizardVoiceSource.Stop(); 
            wizardVoiceSource.PlayOneShot(wizardCongratulationClip);
        }
        
    }

    // ---> NEW: The method to undo the cross-out! <---
    public void ResetValidatorText()
    {
        isSolved = false;
        isWizardTaskShowing = false;

        // Un-cross the main text
        if (objectiveText != null && !string.IsNullOrEmpty(originalBaseTaskString))
        {
            objectiveText.text = originalBaseTaskString;
        }

        // Hide both extra objects
        if (talkToWizardTaskObj != null) talkToWizardTaskObj.SetActive(false);
        if (proceedToGateTaskObj != null) proceedToGateTaskObj.SetActive(false);
    }
}