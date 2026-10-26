using System;
using System.Collections;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Standalone account panel. Reads the signed-in account; only Save writes a username.</summary>
public sealed class ProfileAccountInformationUI : MonoBehaviour
{
    [SerializeField] private TMP_Text username, email, playerId, signInMethod, emailVerification;
    [SerializeField] private MenuDataFeedback presentation;
    [SerializeField] private Button editUsername, copyId, retry, close;
    [SerializeField] private GameObject usernameEditor;
    [SerializeField] private TMP_InputField usernameInput;
    [SerializeField] private TMP_Text editorFeedback;
    [SerializeField] private Button saveUsername, cancelEdit;

    private FirebaseAuth auth;
    private string userId, currentUsername;
    private int requestVersion;
    private bool ready, saving;
    private volatile bool authChanged;

    private void Awake()
    {
        editUsername.onClick.AddListener(BeginEdit);
        copyId.onClick.AddListener(CopyId);
        retry.onClick.AddListener(Reload);
        close.onClick.AddListener(Hide);
        saveUsername.onClick.AddListener(SaveUsername);
        cancelEdit.onClick.AddListener(CancelEdit);
        usernameInput.onValueChanged.AddListener(_ => ValidateInput());
        usernameInput.onSubmit.AddListener(_ => { if (saveUsername.interactable) SaveUsername(); });
        usernameEditor.SetActive(false);
    }

    private void OnEnable() => Reload();
    private void OnDisable()
    {
        requestVersion++;
        StopAllCoroutines();
        if (auth != null) auth.StateChanged -= OnAuthChanged;
        usernameEditor.SetActive(false);
        usernameInput.SetTextWithoutNotify(string.Empty);
        ready = saving = false;
    }

    private void OnAuthChanged(object sender, EventArgs args) => authChanged = true;
    private void Update()
    {
        if (!authChanged) return;
        authChanged = false;
        if (auth != null && auth.CurrentUser?.UserId != userId)
        {
            saving = false;
            Reload();
        }
    }

    public void Show() => gameObject.SetActive(true);
    public void ShowUsernameEditor()
    {
        Show();
        StartCoroutine(OpenEditorWhenReady(requestVersion));
    }
    private IEnumerator OpenEditorWhenReady(int version)
    {
        float deadline = Time.realtimeSinceStartup + 20f;
        while ((!ready || !presentation.IsRevealed) && version == requestVersion && Time.realtimeSinceStartup < deadline) yield return null;
        if (ready && presentation.IsRevealed && version == requestVersion) BeginEdit();
    }
    public void Hide() => gameObject.SetActive(false);

    public void Reload()
    {
        if (!isActiveAndEnabled || saving) return;
        StopAllCoroutines();
        presentation.BeginLoad("Loading account information...");
        ready = false;
        userId = currentUsername = null;
        usernameEditor.SetActive(false);
        username.text = email.text = playerId.text = signInMethod.text = string.Empty;
        emailVerification.text = string.Empty;
        editUsername.interactable = copyId.interactable = false;
        retry.gameObject.SetActive(false);
        StartCoroutine(LoadAccount(++requestVersion));
    }

    private IEnumerator LoadAccount(int version)
    {
        float deadline = Time.realtimeSinceStartup + 20f;
        while (!FirebaseManager.IsReady && string.IsNullOrEmpty(FirebaseManager.InitializationError) &&
               Time.realtimeSinceStartup < deadline) yield return null;
        if (!FirebaseManager.IsReady)
        {
            LoadError("Account services are unavailable. Please retry.");
            yield break;
        }
        if (auth != null) auth.StateChanged -= OnAuthChanged;
        auth = FirebaseAuth.DefaultInstance;
        auth.StateChanged += OnAuthChanged;
        var user = auth.CurrentUser;
        if (user == null)
        {
            LoadError("Sign in to view account information.");
            yield break;
        }
        userId = user.UserId;
        string id = userId;
        email.text = string.IsNullOrEmpty(user.Email) ? "Not provided" : user.Email;
        playerId.text = id;
        signInMethod.text = ProviderLabel(user);
        emailVerification.text = string.IsNullOrEmpty(user.Email) ? string.Empty :
            (user.IsEmailVerified ? "EMAIL VERIFIED" : "EMAIL NOT VERIFIED");
        var request = FirebaseDatabase.DefaultInstance.RootReference.Child("users").Child(id)
            .Child("username").GetValueAsync();
        request.ContinueWithOnMainThread(task =>
        {
            if (!IsCurrent(version, id)) return;
            if (task.IsCanceled || task.IsFaulted)
            {
                LoadError("Couldn't load the username. Check the connection and retry.");
                return;
            }
            currentUsername = task.Result.Exists ? task.Result.Value?.ToString() ?? string.Empty : string.Empty;
            username.text = string.IsNullOrEmpty(currentUsername) ? "Not set" : currentUsername;
            if (PlayerUsername.TryNormalize(currentUsername, out string verified)) PlayerUsername.SetVerified(id, verified);
            ready = true;
            editUsername.interactable = copyId.interactable = true;
            retry.gameObject.SetActive(false);
            presentation.RevealWhenReady();
        });
        deadline = Time.realtimeSinceStartup + 20f;
        while (!request.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
        if (!request.IsCompleted && IsCurrent(version, id))
            LoadError("The connection is taking longer. You can wait or retry.");
    }

    private bool IsCurrent(int version, string id) => this != null && isActiveAndEnabled &&
        version == requestVersion && auth != null && auth.CurrentUser?.UserId == id;

    private static string ProviderLabel(FirebaseUser user)
    {
        bool google = false, password = false;
        foreach (var provider in user.ProviderData)
        {
            google |= provider.ProviderId == "google.com";
            password |= provider.ProviderId == "password";
        }
        if (google && password) return "Google and Email";
        if (google) return "Google";
        if (password) return "Email and Password";
        return user.IsAnonymous ? "Guest" : "Other provider";
    }

    private void LoadError(string message)
    {
        ready = false;
        editUsername.interactable = false;
        presentation.FailLoad(message);
        retry.gameObject.SetActive(true);
    }

    private void BeginEdit()
    {
        if (!ready || !presentation.IsRevealed || saving || !IsCurrent(requestVersion, userId)) return;
        usernameEditor.SetActive(true);
        usernameInput.interactable = true;
        cancelEdit.interactable = true;
        cancelEdit.GetComponentInChildren<TMP_Text>().text = "CANCEL";
        usernameInput.SetTextWithoutNotify(currentUsername ?? string.Empty);
        ValidateInput();
        usernameInput.Select();
        usernameInput.ActivateInputField();
        StartCoroutine(PlaceUsernameCaret());
    }

    private IEnumerator PlaceUsernameCaret()
    {
        yield return null;
        yield return null;
        if (usernameInput != null && usernameInput.isFocused) usernameInput.MoveTextEnd(false);
    }

    private void ValidateInput()
    {
        if (!usernameEditor.activeSelf || saving) return;
        var result = PlayerUsername.Validate(usernameInput.text, out string normalized);
        bool changed = normalized != currentUsername;
        saveUsername.interactable = result == PlayerUsername.ValidationResult.Valid && changed;
        editorFeedback.text = result != PlayerUsername.ValidationResult.Valid ? PlayerUsername.Feedback(result) :
            (changed ? "The account ID will stay the same." : "Enter a different username.");
    }

    private void CancelEdit()
    {
        // Closing the panel does not cancel an already submitted Firebase write.
        if (saving) { Hide(); return; }
        usernameInput.DeactivateInputField();
        usernameEditor.SetActive(false);
        usernameInput.SetTextWithoutNotify(string.Empty);
    }

    private void SaveUsername()
    {
        if (!ready || saving || !usernameEditor.activeSelf || !IsCurrent(requestVersion, userId) ||
            !PlayerUsername.TryNormalize(usernameInput.text, out string normalized) || normalized == currentUsername) return;
        saving = true;
        usernameInput.DeactivateInputField();
        usernameInput.interactable = saveUsername.interactable = false;
        cancelEdit.GetComponentInChildren<TMP_Text>().text = "CLOSE";
        editorFeedback.text = string.Empty;
        int version = requestVersion;
        string id = userId;
        StartCoroutine(DelayedSavingFeedback(version, id));
        FirebaseDatabase.DefaultInstance.RootReference.Child("users").Child(id).Child("username")
            .SetValueAsync(normalized).ContinueWithOnMainThread(task =>
            {
                if (!task.IsCanceled && !task.IsFaulted && FirebaseManager.IsReady &&
                    FirebaseAuth.DefaultInstance.CurrentUser?.UserId == id) PlayerUsername.SetVerified(id, normalized);
                if (!IsCurrent(version, id)) return;
                saving = false;
                presentation.HideFeedback();
                usernameInput.interactable = cancelEdit.interactable = true;
                cancelEdit.GetComponentInChildren<TMP_Text>().text = "CANCEL";
                if (task.IsCanceled || task.IsFaulted)
                {
                    ValidateInput();
                    presentation.ShowTransient("Couldn't save the username. Check the connection and try again.");
                    return;
                }
                currentUsername = normalized;
                username.text = normalized;
                CancelEdit();
                TransientFeedback("Username updated.");
            });
    }

    private IEnumerator DelayedSavingFeedback(int version, string id)
    {
        yield return new WaitForSecondsRealtime(.2f);
        if (saving && IsCurrent(version, id)) presentation.ShowMessage("Saving username...");
    }

    private void CopyId()
    {
        if (string.IsNullOrEmpty(userId) || !IsCurrent(requestVersion, userId)) return;
        GUIUtility.systemCopyBuffer = userId;
        TransientFeedback("Account ID copied.");
    }

    private void TransientFeedback(string message)
    {
        // Keep a loading/error instruction visible until its operation finishes or is retried.
        if (!ready) return;
        presentation.ShowTransient(message);
    }
}
