using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Scene-authored account prompt shown after character selection or on an incomplete profile.</summary>
public sealed class UsernameSetupUI : MonoBehaviour
{
    [SerializeField] private GameObject overlay;
    [SerializeField] private TMP_InputField input;
    [SerializeField] private TMP_Text message;
    [SerializeField] private Button continueButton;
    [SerializeField] private AuthManager auth;

    private bool retrying;
    private bool acceptingName;
    private CanvasGroup overlayGroup;

    private void Awake()
    {
        overlayGroup = overlay.GetComponent<CanvasGroup>();
        if (overlayGroup == null) overlayGroup = overlay.AddComponent<CanvasGroup>();
        continueButton.onClick.AddListener(Continue);
        input.onSubmit.AddListener(_ => { if (continueButton.interactable) Continue(); });
        input.onValueChanged.AddListener(_ => RefreshValidation());
        overlay.SetActive(false);
    }

    private void Continue()
    {
        if (retrying) auth.RetryUsernameSetup();
        else if (acceptingName && PlayerUsername.TryNormalize(input.text, out _)) auth.SubmitUsername(input.text);
    }

    private void RefreshValidation()
    {
        if (!acceptingName) return;
        var result = PlayerUsername.Validate(input.text, out _);
        continueButton.interactable = result == PlayerUsername.ValidationResult.Valid;
        message.text = PlayerUsername.Feedback(result);
    }

    public void ShowLoading()
    {
        retrying = false;
        acceptingName = false;
        input.DeactivateInputField();
        // AuthManager shows profile progress in the authentication feedback.
        // Keep this modal invisible while blocking menu actions until the check finishes.
        overlayGroup.alpha = 0f;
        overlay.SetActive(true);
        overlay.transform.SetAsLastSibling();
        input.gameObject.SetActive(false);
        continueButton.gameObject.SetActive(false);
        message.text = "Checking your player profile...";
    }

    public void ShowPrompt(string error = null)
    {
        retrying = false;
        acceptingName = true;
        overlay.SetActive(true);
        overlay.transform.SetAsLastSibling();
        overlayGroup.alpha = 1f;
        input.gameObject.SetActive(true);
        input.interactable = true;
        continueButton.gameObject.SetActive(true);
        continueButton.GetComponentInChildren<TMP_Text>().text = "CONTINUE";
        RefreshValidation();
        if (!string.IsNullOrEmpty(error)) message.text = error;
        input.Select();
    }

    public void ShowSaving()
    {
        acceptingName = false;
        overlayGroup.alpha = 1f;
        input.interactable = false;
        continueButton.interactable = false;
        message.text = "Saving your username...";
    }

    public void ShowLoadError()
    {
        retrying = true;
        acceptingName = false;
        input.DeactivateInputField();
        overlay.SetActive(true);
        overlay.transform.SetAsLastSibling();
        overlayGroup.alpha = 1f;
        input.gameObject.SetActive(false);
        continueButton.gameObject.SetActive(true);
        continueButton.interactable = true;
        continueButton.GetComponentInChildren<TMP_Text>().text = "RETRY";
        message.text = "Couldn't load your profile. Check your connection and retry.";
    }

    public void Hide()
    {
        retrying = false;
        acceptingName = false;
        input.DeactivateInputField();
        overlay.SetActive(false);
        overlayGroup.alpha = 1f;
        input.text = string.Empty;
    }

#if UNITY_EDITOR
    public void BindForEditor(GameObject panel, TMP_InputField username, TMP_Text feedback,
        Button submit, AuthManager owner)
    {
        overlay = panel;
        input = username;
        message = feedback;
        continueButton = submit;
        auth = owner;
    }
#endif
}
