using UnityEngine;
using TMPro;

public class FruitValidator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WizardInteraction wizard;
    [SerializeField] private FruitBasket challenge2Basket;
    [SerializeField] private TextMeshProUGUI objectiveText;

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
            CrossOutObjective();
            if (wizard != null) wizard.PlaySuccessDialogue();
        }
        else
        {
            if (wizard != null) wizard.PlayFailDialogue();
        }
    }

    private void CrossOutObjective()
    {
        if (objectiveText != null && !objectiveText.text.Contains("<s>"))
        {
            objectiveText.text = "<color=#008000><s>" + objectiveText.text + "</s></color>";
        }
    }
}