using UnityEngine;
using UnityEngine.UI;

/// <summary>Keeps the safe-area profile action scoped to the Main Menu home panel.</summary>
public sealed class MainMenuProfileLauncher : MonoBehaviour
{
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject profileButtonObject;
    [SerializeField] private MainMenuProfileSection profile;
    private Button profileButton;

    private void Awake()
    {
        profileButton = profileButtonObject.GetComponent<Button>();
        profileButton.onClick.AddListener(Open);
    }
    private void Update()
    {
        bool homeVisible = mainMenuPanel.activeInHierarchy;
        if (!homeVisible && profile.gameObject.activeSelf) profile.Hide();
        bool showButton = homeVisible && !profile.gameObject.activeSelf;
        if (profileButton.gameObject.activeSelf != showButton) profileButton.gameObject.SetActive(showButton);
    }
    private void Open()
    {
        if (mainMenuPanel.activeInHierarchy) profile.Show();
    }
    private void OnDisable()
    {
        if (profileButton != null) profileButton.gameObject.SetActive(false);
        if (profile != null) profile.Hide();
    }
    private void OnDestroy()
    {
        if (profileButton != null) profileButton.onClick.RemoveListener(Open);
    }
}
