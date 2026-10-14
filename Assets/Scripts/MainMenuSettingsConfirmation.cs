using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Confirmation flow used only by the Main Menu settings buttons.</summary>
public sealed class MainMenuSettingsConfirmation : MonoBehaviour
{
    [Header("Dialog")]
    [SerializeField] private GameObject overlay;
    [SerializeField] private RectTransform card;
    [SerializeField] private TMP_Text heading;
    [SerializeField] private TMP_Text message;
    [SerializeField] private TMP_Text confirmLabel;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button logoutButton;
    [SerializeField] private Button quitButton;

    [Header("Confirmed Actions")]
    [SerializeField] private UnityEvent onLogoutConfirmed = new UnityEvent();
    [SerializeField] private UnityEvent onQuitConfirmed = new UnityEvent();

    private enum PendingAction { None, Logout, Quit }
    private PendingAction pending;
    private Button returnTo;

    private void Awake() => Close();
    private void OnDisable() => Close();

    public void RequestLogout() => Show(PendingAction.Logout, logoutButton);
    public void RequestQuit() => Show(PendingAction.Quit, quitButton);

    private void Show(PendingAction action, Button source)
    {
        if (!isActiveAndEnabled || overlay == null || pending != PendingAction.None) return;
        pending = action;
        returnTo = source;
        bool loggingOut = action == PendingAction.Logout;
        heading.text = loggingOut ? "LOG OUT?" : "QUIT GAME?";
        message.text = loggingOut
            ? "Are you sure you want to log out?\nYou will need to sign in again."
            : "Are you sure you want to quit the game?";
        confirmLabel.text = loggingOut ? "LOG OUT" : "QUIT";
        var canvasRect = overlay.transform.parent as RectTransform;
        if (card != null && canvasRect != null)
        {
            float scale = Mathf.Min(1f, (canvasRect.rect.width - 80) / card.sizeDelta.x,
                (canvasRect.rect.height - 80) / card.sizeDelta.y);
            card.localScale = Vector3.one * Mathf.Max(.1f, scale);
        }
        overlay.transform.SetAsLastSibling();
        overlay.SetActive(true);
        if (cancelButton != null) cancelButton.Select();
    }

    public void Cancel()
    {
        var source = returnTo;
        Close();
        if (isActiveAndEnabled && source != null && source.IsActive()) source.Select();
    }

    public void Confirm()
    {
        if (!isActiveAndEnabled || overlay == null || !overlay.activeInHierarchy) return;
        var action = pending;
        // Clear first so repeated clicks cannot invoke the action twice.
        Close();
        if (action == PendingAction.Logout) onLogoutConfirmed.Invoke();
        else if (action == PendingAction.Quit) onQuitConfirmed.Invoke();
    }

    private void Close()
    {
        pending = PendingAction.None;
        returnTo = null;
        if (overlay == null) return;
        var events = EventSystem.current;
        if (events != null && events.currentSelectedGameObject != null &&
            events.currentSelectedGameObject.transform.IsChildOf(overlay.transform))
            events.SetSelectedGameObject(null);
        overlay.SetActive(false);
    }
}
