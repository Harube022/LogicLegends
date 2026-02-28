using UnityEngine;
using UnityEngine.SceneManagement;

public class Portal : MonoBehaviour
{
    [SerializeField] private string sceneToLoad;
    [SerializeField] private GameObject interactPrompt; // optional UI

    private bool playerInside;

    private void Start()
    {
        if (interactPrompt != null)
            interactPrompt.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInside = true;

            if (interactPrompt != null)
                interactPrompt.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInside = false;

            if (interactPrompt != null)
                interactPrompt.SetActive(false);
        }
    }

    public void TryEnterPortal()
    {
        if (!playerInside) return;

        Debug.Log("Before load: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);

        UnityEngine.SceneManagement.SceneManager.LoadScene("TUTORIAL", UnityEngine.SceneManagement.LoadSceneMode.Single);

        Debug.Log("After load call");
    }
}