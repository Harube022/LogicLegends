using UnityEngine.SceneManagement;
using UnityEngine;

public class SceneManagement : MonoBehaviour
{
    public void ExitGame()
    {
        Debug.Log("Exit");

        Application.Quit();

#if UNITY_EDITOR

UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}