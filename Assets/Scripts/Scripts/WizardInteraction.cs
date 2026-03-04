using UnityEngine;

public class WizardInteraction : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject dialoguePanel;

    [Header("Settings")]
    [SerializeField] private float displayDuration = 4f;

    private bool playerInRange = false;
    private bool isDisplaying = false;

    private void Update()
    {
        if (playerInRange && !isDisplaying && Input.GetKeyDown(KeyCode.E))
        {
            StartCoroutine(ShowDialogue());
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
        }
    }

    private System.Collections.IEnumerator ShowDialogue()
    {
        isDisplaying = true;

        dialoguePanel.SetActive(true);

        yield return new WaitForSeconds(displayDuration);

        dialoguePanel.SetActive(false);

        isDisplaying = false;
    }
}