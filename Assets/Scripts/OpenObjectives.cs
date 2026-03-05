using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class OpenObjectives : MonoBehaviour
{
    [Header("Menu Panels")]
    [SerializeField] private GameObject objectivesPanel;

    [Header("Player Control")]
    [Tooltip("Drag your Player's movement script or PlayerInput here to disable it while talking.")]
    [SerializeField] private Behaviour playerControlScript;
    public void ShowObjectivesMenu()
    {
        objectivesPanel.SetActive(true);
        if (playerControlScript != null) 
        {
            playerControlScript.enabled = false;
        }
    }
    public void CloseObjectivesMenu()
    {
        objectivesPanel.SetActive(false);
        if (playerControlScript != null) 
        {
            playerControlScript.enabled = true;
        }
    }
}
