using UnityEngine;
using UnityEngine.SceneManagement;

public class StageEndPortal : MonoBehaviour
{
    [Header("Stage Transition")]
    public string nextSceneName;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Loading next stage: " + nextSceneName);
            SceneManager.LoadScene(nextSceneName);
        }
    }
}