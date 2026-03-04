using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class OpenObjectives : MonoBehaviour
{
    [Header("Menu Panels")]
    [SerializeField] private GameObject objectivesPanel;
    public void ShowObjectivesMenu()
    {
        objectivesPanel.SetActive(true);
    }
    public void CloseObjectivesMenu()
    {
        objectivesPanel.SetActive(false);
    }
}
