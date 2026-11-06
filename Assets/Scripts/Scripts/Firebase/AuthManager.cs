using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;
using Google; 
using System.Threading.Tasks;
using System.Collections;
using UnityEngine.Video;           // ---> NEW: Required for VideoPlayer
using UnityEngine.SceneManagement; // ---> NEW: Required to load LogicGarden

public class AuthManager : MonoBehaviour
{
    [Header("UI Panels")]
    [SerializeField] private GameObject loginMenu;     
    // [SerializeField] private GameObject signUpMenu;    
    [SerializeField] private GameObject modeSelection; 
    [SerializeField] private GameObject settingsMenu;
    [SerializeField] private GameObject characterSelectMenu;
    [SerializeField] private UsernameSetupUI usernameSetup;

    [Header("Cinematic Intro Settings")]
    [Tooltip("The UI Panel that holds your Video Player")]
    [SerializeField] private GameObject cinematicPanel;
    [Tooltip("Drag the Video Player component here")]
    [SerializeField] private VideoPlayer introVideoPlayer;
    [Tooltip("Menu music to pause during the intro, without muting the video's audio.")]
    [SerializeField] private AudioSource mainMenuMusic;
    [Tooltip("Drag your Skip Button GameObject here")]
    [SerializeField] private GameObject skipButton;
    [Tooltip("The exact name of the tutorial scene")]
    [SerializeField] private string tutorialSceneName = "LogicGarden";

    [Header("Google Configuration")]
    [Tooltip("Paste your Web Client ID from the Firebase Console here")]
    [SerializeField] private string webClientId = "";

    [Header("Login Inputs")]
    [SerializeField] private TMP_InputField emailLoginInput;
    [SerializeField] private TMP_InputField passwordLoginInput;

    [Header("Password Reset")]
    [SerializeField] private Button forgotPasswordButton;
    [SerializeField] private TMP_Text passwordResetMessage;

    [Header("Sign Up Inputs")]
    [SerializeField] private TMP_InputField emailSignUpInput;
    [SerializeField] private TMP_InputField usernameSignUpInput;
    [SerializeField] private TMP_InputField passwordSignUpInput;

    // ---> NEW: Web Registration URL <---
    [Header("Web Links")]
    [SerializeField] private string webRegistrationUrl = "https://logiclegends.netlify.app/loginpage/sign_up_page.html";

    private FirebaseAuth auth;
    private GoogleSignInConfiguration configuration;
    private bool sendingPasswordReset;
    [Header("Login Feedback")]
    [SerializeField] private TMP_Text authenticationMessage;
    private bool signingIn;
    private bool playIntroAfterUsername;
    private bool resumeMenuMusicAfterIntro;
    private bool finishingIntro;
    private bool accountLoading;
    private bool connectionErrorVisible;
    private bool accountReveal;
    private System.Action accountRetry;
    private int sessionVersion;
    private string observedUserId;
    private volatile bool authStateChanged;

    [Header("Feedback Timing")]
    [SerializeField] private float loginFeedbackDuration = 1f;
    [SerializeField] private float logoutFeedbackDuration = 1.5f;
    private Coroutine feedbackTimer;

    private void OnAuthStateChanged(object sender, System.EventArgs args)
    {
        // Process the SDK notification on Unity's main thread in Update.
        authStateChanged = true;
    }

    private void Update()
    {
        if (accountLoading && Application.internetReachability == NetworkReachability.NotReachable)
        {
            ShowConnectionError();
            return;
        }
        if (connectionErrorVisible) return;
        if (!authStateChanged || signingIn || auth == null) return;
        authStateChanged = false;
        string currentId = auth.CurrentUser?.UserId;
        if (currentId == observedUserId) return;
        PlayerUsername.Clear();
        observedUserId = currentId;
        sessionVersion++;
        if (currentId != null) CheckLoginState();
        else
        {
            CompleteAccountLoading(() => { ShowLoginScreen(); Feedback("Session ended. Please log in again."); });
        }
    }

    private void Feedback(string message, float hideAfterSeconds = 0f)
    {
        if (feedbackTimer != null)
        {
            StopCoroutine(feedbackTimer);
            feedbackTimer = null;
        }
        // Late Firebase responses must never cover the intro video.
        if (cinematicPanel != null && cinematicPanel.activeSelf) message = "";
        if (authenticationMessage != null)
        {
            authenticationMessage.text = message;
            var group = authenticationMessage.GetComponentInParent<CanvasGroup>();
            if (group != null) group.alpha = string.IsNullOrEmpty(message) ? 0f : 1f;
        }
        // Character selection should remain unobstructed even if a delayed request shows a message.
        if (characterSelectMenu != null && characterSelectMenu.activeSelf && !string.IsNullOrEmpty(message))
            hideAfterSeconds = loginFeedbackDuration;
        if (!string.IsNullOrEmpty(message) && hideAfterSeconds > 0f)
            feedbackTimer = StartCoroutine(HideFeedbackAfter(hideAfterSeconds));
    }

    private IEnumerator HideFeedbackAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        feedbackTimer = null;
        ClearFeedback();
    }

    private void ClearFeedback() { Feedback(""); }

    public void ShowInitializationError(string message)
    {
        accountRetry = RetryInitialization;
        ShowConnectionError();
    }

    private void Start()
    {
        if (!FirebaseManager.IsReady && !connectionErrorVisible) BeginAccountLoading(RetryInitialization);
    }

    private MainMenuLoadingScreen LoadingScreen => FindFirstObjectByType<MainMenuLoadingScreen>();

    private int BeginAccountLoading(System.Action retry)
    {
        accountRetry = retry;
        accountLoading = true;
        connectionErrorVisible = false;
        ClearFeedback();
        LoadingScreen?.ShowAccountLoading();
        return ++sessionVersion;
    }

    private void CompleteAccountLoading(System.Action prepareContent)
    {
        accountLoading = false;
        connectionErrorVisible = false;
        accountRetry = null;
        ClearFeedback();
        StartCoroutine(RevealAccountContent(prepareContent));
    }

    private IEnumerator RevealAccountContent(System.Action prepareContent)
    {
        accountReveal = true;
        var loading = LoadingScreen;
        try
        {
            if (loading != null) yield return loading.RevealLoadedContent(prepareContent);
            else prepareContent();
        }
        finally { accountReveal = false; }
    }

    private void ShowConnectionError()
    {
        accountLoading = false;
        connectionErrorVisible = true;
        signingIn = false;
        sessionVersion++; // Ignore late replies from the failed attempt.
        ClearFeedback();
        var retry = accountRetry ?? (System.Action)CheckLoginState;
        LoadingScreen?.ShowConnectionError(() => { signingIn = false; connectionErrorVisible = false; retry(); });
    }

    private void RetryInitialization()
    {
        if (FirebaseManager.IsReady) { CheckLoginState(); return; }
        BeginAccountLoading(RetryInitialization);
        FindFirstObjectByType<FirebaseManager>()?.RetryInitialization();
    }

    private static AuthError ErrorCode(System.AggregateException exception)
    {
        if (exception != null)
            foreach (var error in exception.Flatten().InnerExceptions)
            {
                // Older Firebase SDKs report the enumeration-protected backend response as Failure.
                string detail = error.ToString().ToUpperInvariant();
                if (detail.Contains("INVALID_LOGIN_CREDENTIALS") || detail.Contains("INVALID_PASSWORD") ||
                    detail.Contains("EMAIL_NOT_FOUND")) return AuthError.InvalidCredential;
                if (error is Firebase.FirebaseException firebaseError)
                    return (AuthError)firebaseError.ErrorCode;
            }
        return AuthError.Failure;
    }

    private bool IsCurrentSession(int version, string userId)
    {
        return this != null && version == sessionVersion && auth != null &&
            auth.CurrentUser != null && auth.CurrentUser.UserId == userId;
    }

    private void OnDestroy()
    {
        ResumeMenuMusicAfterIntro();
        if (auth != null) auth.StateChanged -= OnAuthStateChanged;
        sessionVersion++;
        CancelInvoke();
        if (introVideoPlayer != null)
        {
            introVideoPlayer.loopPointReached -= OnCutsceneFinished;
            introVideoPlayer.errorReceived -= OnCutsceneError;
        }
    }

    // ---> FIX 1: THE FLASHING LOGIN SCREEN <---
    private void Awake()
    {
        if (gameObject.scene.name == "Main Menu")
        {
            MainMenuMobileInput.EnsureForScene(gameObject.scene);
            CommerceClient.Prewarm();
        }
        // Hide everything the exact millisecond the scene loads.
        // This prevents the login screen from flashing while Firebase checks your saved session!
        if (loginMenu != null) loginMenu.SetActive(false);
        // if (signUpMenu != null) signUpMenu.SetActive(false);
        if (modeSelection != null) modeSelection.SetActive(false);
        if (characterSelectMenu != null) characterSelectMenu.SetActive(false);
        if (settingsMenu != null) settingsMenu.SetActive(false);
        if (cinematicPanel != null) cinematicPanel.SetActive(false);
        if (introVideoPlayer != null) introVideoPlayer.Stop();
        if (skipButton != null) skipButton.SetActive(false);
        ClearFeedback();
    }

    public void CheckLoginState()
    {
        if (!FirebaseManager.IsReady || connectionErrorVisible) return;
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            accountRetry = CheckLoginState;
            ShowConnectionError();
            return;
        }
        auth = FirebaseAuth.DefaultInstance;
        auth.StateChanged -= OnAuthStateChanged;
        auth.StateChanged += OnAuthStateChanged;
        observedUserId = auth.CurrentUser?.UserId;

        configuration = new GoogleSignInConfiguration
        {
            WebClientId = webClientId,
            RequestIdToken = true,
            RequestEmail = true
        };

        if (auth.CurrentUser != null)
        {
            var savedUser = auth.CurrentUser;
            int version = BeginAccountLoading(CheckLoginState);
            string savedId = savedUser.UserId;
            // Firebase persists credentials. A temporary network failure must not erase them.
            Debug.Log("Found saved session. Verifying with server...");
            savedUser.ReloadAsync().ContinueWithOnMainThread(task =>
            {
                if (!IsCurrentSession(version, savedId)) return;
                if (task.IsCanceled || task.IsFaulted)
                {
                    var error = ErrorCode(task.Exception);
                    if (error == AuthError.UserDisabled || error == AuthError.UserNotFound ||
                        error == AuthError.InvalidUserToken || error == AuthError.UserTokenExpired)
                    {
                        sessionVersion++;
                        observedUserId = null;
                        auth.SignOut();
                        CompleteAccountLoading(() => { ShowLoginScreen(); Feedback("Session expired. Please log in again."); });
                    }
                    else ShowConnectionError();
                }
                else
                {
                    if (!auth.CurrentUser.IsEmailVerified && HasPasswordProvider(auth.CurrentUser))
                    {
                        sessionVersion++;
                        observedUserId = null;
                        auth.SignOut();
                        CompleteAccountLoading(() => { ShowLoginScreen(); Feedback("Please verify the email address before logging in."); });
                    }
                    else CheckFirstTimeSetup();
                }
            });
        }
        else
        {
            Debug.Log("No user found. Please log in.");
            CompleteAccountLoading(ShowLoginScreen);
        }
    }

    // --- FIREBASE DATABASE INTERCEPTOR ---

    private void CheckFirstTimeSetup()
    {
        if (this == null || auth == null || auth.CurrentUser == null) return;
        string userId = auth.CurrentUser.UserId;
        observedUserId = userId;
        int version = BeginAccountLoading(CheckFirstTimeSetup);
        playIntroAfterUsername = false;
        if (usernameSetup != null) usernameSetup.Hide();
        DatabaseReference dbRef = FirebaseDatabase.DefaultInstance.RootReference;

        Debug.Log("Checking if player has chosen a base character...");
        dbRef.Child("users").Child(userId).Child("base_character").GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (!IsCurrentSession(version, userId)) return;
            if (task.IsFaulted || task.IsCanceled)
            {
                ShowConnectionError();
                return;
            }

            DataSnapshot snapshot = task.Result;
            if (snapshot.Exists && snapshot.Value != null && snapshot.Value.ToString() != "")
            {
                dbRef.Child("users").Child(userId).Child("username").GetValueAsync().ContinueWithOnMainThread(nameTask =>
                {
                    if (!IsCurrentSession(version, userId)) return;
                    if (nameTask.IsFaulted || nameTask.IsCanceled)
                    {
                        ShowConnectionError();
                        return;
                    }
                    if (PlayerUsername.TryNormalize(nameTask.Result.Value?.ToString(), out string storedUsername))
                    {
                        PlayerUsername.SetVerified(userId, storedUsername);
                        if (usernameSetup != null) usernameSetup.Hide();
                        CompleteAccountLoading(ShowModeSelection);
                    }
                    else if (usernameSetup != null)
                    {
                        CompleteAccountLoading(() => ShowUsernamePrompt());
                    }
                    else Debug.LogError("Username prompt is missing from the Main Menu scene.");
                });
            }
            else
            {
                if (usernameSetup != null) usernameSetup.Hide();
                CompleteAccountLoading(ShowCharacterSelectScreen);
            }
        });
    }

    // Call this from your Male / Female UI Buttons
    public void SelectBaseCharacter(string characterID)
    {
        if (auth == null || auth.CurrentUser == null) return;
        if (characterID != "Male_Character" && characterID != "Female_Character") return;
        string userId = auth.CurrentUser.UserId;
        int version = BeginAccountLoading(() => SelectBaseCharacter(characterID));
        DatabaseReference dbRef = FirebaseDatabase.DefaultInstance.RootReference;

        // Save their choice to Firebase
        dbRef.Child("users").Child(userId).Child("base_character").SetValueAsync(characterID).ContinueWithOnMainThread(task =>
        {
            if (!IsCurrentSession(version, userId)) return;
            if (task.IsFaulted || task.IsCanceled)
                ShowConnectionError();
            else
            {
                Debug.Log($"Successfully saved {characterID} as base character!");
                LogicGardenProgress.ResetForNewGame();
                playIntroAfterUsername = true;
                if (usernameSetup != null) CompleteAccountLoading(() => ShowUsernamePrompt());
                else Debug.LogError("Username prompt is missing from the Main Menu scene.");
            }
        });
    }

    public void RetryUsernameSetup() => CheckFirstTimeSetup();

    private void ShowUsernamePrompt(string error = null)
    {
        if (characterSelectMenu != null) characterSelectMenu.SetActive(false);
        ClearFeedback();
        usernameSetup.ShowPrompt(error);
    }

    public void SubmitUsername(string value)
    {
        if (auth == null || auth.CurrentUser == null || usernameSetup == null) return;
        var validation = PlayerUsername.Validate(value, out string username);
        if (validation != PlayerUsername.ValidationResult.Valid)
        {
            ShowUsernamePrompt(PlayerUsername.Feedback(validation));
            return;
        }
        string userId = auth.CurrentUser.UserId;
        int version = BeginAccountLoading(() => SubmitUsername(value));
        usernameSetup.ShowSaving();
        FirebaseDatabase.DefaultInstance.RootReference.Child("users").Child(userId).Child("username")
            .SetValueAsync(username).ContinueWithOnMainThread(task =>
            {
                if (!IsCurrentSession(version, userId)) return;
                if (task.IsFaulted || task.IsCanceled)
                {
                    ShowConnectionError();
                    return;
                }
                CompleteAccountLoading(() =>
                {
                    usernameSetup.Hide();
                    PlayerUsername.SetVerified(userId, username);
                    if (playIntroAfterUsername) PlayIntroCutscene();
                    else ShowModeSelection();
                    playIntroAfterUsername = false;
                });
            });
    }

    // ---> NEW: Methods to handle the video and scene loading <---
    private void PlayIntroCutscene()
    {
        ClearFeedback();
        if (usernameSetup != null) usernameSetup.Hide();
        // 1. Hide all other UI menus
        if (loginMenu != null) loginMenu.SetActive(false);
        if (characterSelectMenu != null) characterSelectMenu.SetActive(false);
        if (modeSelection != null) modeSelection.SetActive(false);
        if (settingsMenu != null) settingsMenu.SetActive(false);

        // 2. Show the Cinematic Panel
        if (cinematicPanel != null) cinematicPanel.SetActive(true);
        if (skipButton != null) skipButton.SetActive(true);

        // 3. Play the video and listen for the end
        if (introVideoPlayer != null)
        {
            PauseMenuMusicForIntro();
            introVideoPlayer.loopPointReached -= OnCutsceneFinished;
            introVideoPlayer.errorReceived -= OnCutsceneError;
            // Subscribe to the event that fires when the video finishes
            introVideoPlayer.loopPointReached += OnCutsceneFinished;
            introVideoPlayer.errorReceived += OnCutsceneError;
            introVideoPlayer.Play();
        }
        else
        {
            Debug.LogWarning("No Video Player assigned! Returning to the main menu.");
            FinishCutsceneAndShowMainMenu();
        }
    }

    private void OnCutsceneFinished(VideoPlayer vp)
    {
        FinishCutsceneAndShowMainMenu();
    }

    private void OnCutsceneError(VideoPlayer vp, string message)
    {
        Debug.LogError($"Intro video playback failed: {message}");
        FinishCutsceneAndShowMainMenu();
    }

    // Call this method from your Skip Button's OnClick event in the Unity Editor
    public void OnClickSkipCutscene()
    {
        FinishCutsceneAndShowMainMenu();
    }

    private void FinishCutsceneAndShowMainMenu()
    {
        if (finishingIntro) return;
        finishingIntro = true;
        StartCoroutine(RevealMainMenuAfterIntro());
    }

    private IEnumerator RevealMainMenuAfterIntro()
    {
        ClearFeedback();
        // Clean up video player events and stop playback
        if (introVideoPlayer != null)
        {
            introVideoPlayer.loopPointReached -= OnCutsceneFinished;
            introVideoPlayer.errorReceived -= OnCutsceneError;
            introVideoPlayer.Stop();
        }

        // Hide the video panel
        if (cinematicPanel != null) cinematicPanel.SetActive(false);
        if (skipButton != null) skipButton.SetActive(false);

        // Revalidate the signed-in session before revealing the menu. A slow request
        // keeps the loading screen visible; a failed request exposes Retry / Cancel.
        if (FirebaseManager.IsReady) CheckLoginState();
        else RetryInitialization();
        while (accountLoading || connectionErrorVisible || accountReveal) yield return null;
        ResumeMenuMusicAfterIntro();
        finishingIntro = false;
    }

    private void PauseMenuMusicForIntro()
    {
        if (mainMenuMusic == null || !mainMenuMusic.isPlaying) return;
        mainMenuMusic.Pause();
        resumeMenuMusicAfterIntro = true;
    }

    private void ResumeMenuMusicAfterIntro()
    {
        if (!resumeMenuMusicAfterIntro) return;
        resumeMenuMusicAfterIntro = false;
        if (mainMenuMusic != null) mainMenuMusic.UnPause();
    }

    // private void OnCutsceneFinished(VideoPlayer vp)
    // {
    //     // Unsubscribe from the event to prevent memory leaks
    //     vp.loopPointReached -= OnCutsceneFinished; 
        
    //     LoadTutorialScene();
    // }

    // private void LoadTutorialScene()
    // {
    //     // Load the LogicGarden scene
    //     SceneManager.LoadScene(tutorialSceneName);
    // }

    // --- GOOGLE SIGN-IN ---

    public void OnClickGoogleSignIn()
    {
        if (signingIn) return;
        if (auth == null || !FirebaseManager.IsReady)
        {
            Feedback("Please wait while login services connect.", loginFeedbackDuration);
            return;
        }
        if (string.IsNullOrWhiteSpace(webClientId))
        {
            Feedback("Google sign-in is not configured. Please contact support.", loginFeedbackDuration);
            Debug.LogError("Google sign-in: Web Client ID is missing from AuthManager.");
            return;
        }
        signingIn = true;
        int version = BeginAccountLoading(OnClickGoogleSignIn);
        StartCoroutine(BeginGoogleSignIn(version));
    }

    private IEnumerator BeginGoogleSignIn(int version)
    {
        // Let the button event finish before the Android plugin starts another Activity.
        yield return null;
        if (this == null || version != sessionVersion || !signingIn) yield break;

        try
        {
            var currentConfiguration = GoogleSignIn.Configuration;
            if (currentConfiguration == null)
                GoogleSignIn.Configuration = configuration;
            else if (currentConfiguration.WebClientId != webClientId || !currentConfiguration.RequestIdToken)
                throw new System.InvalidOperationException("Google sign-in configuration changed after initialization.");

            Debug.Log("Google sign-in: creating native client.");
            var googleSignIn = GoogleSignIn.DefaultInstance;
            Debug.Log("Google sign-in: starting account picker.");
            var signInTask = googleSignIn.SignIn();
            signInTask.ContinueWithOnMainThread(task =>
            {
                if (this == null || version != sessionVersion) return;
                OnGoogleSignInFinished(task);
            });
        }
        catch (System.Exception exception)
        {
            signingIn = false;
            Debug.LogError("Google sign-in could not start.");
            Debug.LogException(exception);
            CompleteAccountLoading(() => { ShowLoginScreen(); Feedback("Couldn't open Google sign-in. Please try again.", loginFeedbackDuration); });
        }
    }

    private void OnGoogleSignInFinished(Task<GoogleSignInUser> task)
    {
        if (task.IsFaulted || task.IsCanceled)
        {
            signingIn = false;
            Debug.LogError("Google Sign-In failed or was canceled.");
            CompleteAccountLoading(() => { ShowLoginScreen(); Feedback("Google sign-in was canceled or failed. Please try again.", loginFeedbackDuration); });
            return;
        }

        if (task.Result == null || string.IsNullOrEmpty(task.Result.IdToken))
        {
            signingIn = false;
            CompleteAccountLoading(() => { ShowLoginScreen(); Feedback("Google did not return a sign-in token. Please try again.", loginFeedbackDuration); });
            return;
        }

        Debug.Log("Google Token received! Handing over to Firebase...");
        Credential credential = GoogleAuthProvider.GetCredential(task.Result.IdToken, null);
        int version = sessionVersion;

        auth.SignInWithCredentialAsync(credential).ContinueWithOnMainThread(authTask =>
        {
            if (this == null || version != sessionVersion) return;
            signingIn = false;
            if (authTask.IsCanceled || authTask.IsFaulted)
            {
                Debug.LogError("Firebase failed to authenticate Google credential.");
                ShowConnectionError();
                return;
            }

            Debug.Log("Google Login Success! Waiting for Database Security Sync...");
            CheckFirstTimeSetup();
        });
    }

    // --- EXISTING EMAIL/PASSWORD METHODS ---

    public void OnClickLogin()
    {
        if (signingIn) return;
        if (auth == null || !FirebaseManager.IsReady)
        {
            Feedback("Please wait while login services connect.", loginFeedbackDuration);
            return;
        }
        string email = emailLoginInput.text.Trim();
        string password = passwordLoginInput.text;
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        {
            Feedback("Enter an email address and password.", loginFeedbackDuration);
            return;
        }
        SignInWithEmail(email, password);
    }

    private void SignInWithEmail(string email, string password)
    {
        signingIn = true;
        int version = BeginAccountLoading(() => SignInWithEmail(email, password));
        auth.SignInWithEmailAndPasswordAsync(email, password).ContinueWithOnMainThread(task =>
        {
            if (this == null || version != sessionVersion) return;
            signingIn = false;
            if (task.IsCanceled || task.IsFaulted)
            {
                var error = ErrorCode(task.Exception);
                string message;
                if (error == AuthError.InvalidEmail || error == AuthError.WrongPassword ||
                    error == AuthError.UserNotFound || error == AuthError.InvalidCredential)
                    message = "Incorrect email or password. Please try again.";
                else if (error == AuthError.TooManyRequests)
                    message = "Too many attempts. Please wait and try again.";
                else if (error == AuthError.UserDisabled)
                    message = "This account is disabled.";
                else { ShowConnectionError(); return; }
                CompleteAccountLoading(() => { ShowLoginScreen(); Feedback(message, loginFeedbackDuration); });
                return;
            }

            if (!auth.CurrentUser.IsEmailVerified)
            {
                Debug.LogWarning("Access Denied: Please verify your email address first!");
                auth.SignOut();
                CompleteAccountLoading(() => { ShowLoginScreen(); Feedback("Please verify the email address before logging in.", loginFeedbackDuration); });
                return;
            }
            CheckFirstTimeSetup();
        });
    }

    public void OnClickForgotPassword()
    {
        if (sendingPasswordReset) return;

        if (auth == null)
        {
            passwordResetMessage.text = "Please wait while we connect.";
            return;
        }

        string email = emailLoginInput.text.Trim();

        if (string.IsNullOrWhiteSpace(email))
        {
            passwordResetMessage.text = "Enter an email address first.";
            return;
        }

        sendingPasswordReset = true;
        forgotPasswordButton.interactable = false;
        passwordResetMessage.text = "Sending reset link...";

        auth.SendPasswordResetEmailAsync(email)
            .ContinueWithOnMainThread(task =>
            {
                if (this == null) return;

                sendingPasswordReset = false;
                forgotPasswordButton.interactable = true;

                if (task.IsCanceled || task.IsFaulted)
                {
                    passwordResetMessage.text =
                        "Couldn't request a reset. Check the email address " +
                        "and connection, then try again.";
                    return;
                }

                passwordResetMessage.text =
                    // "If an eligible account exists for this email, " +
                    // "you'll receive a reset link. Check your spam folder too.";
                    "Password reset link sent. Check the email inbox.";
            });
    }

    // ---> NEW: Opens the Web Browser <---
    public void OnClickOpenWebRegistration()
    {
        Debug.Log("Opening Web Registration: " + webRegistrationUrl);
        Application.OpenURL(webRegistrationUrl);
    }

    public void OnClickLogout()
    {
        accountLoading = false;
        accountRetry = null;
        sessionVersion++;
        observedUserId = null;
        signingIn = false;
        playIntroAfterUsername = false;
        PlayerUsername.Clear();
        if (usernameSetup != null) usernameSetup.Hide();
        CancelInvoke();
        if (auth != null && auth.CurrentUser != null)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            bool wasGoogleUser = false;
            foreach (var provider in auth.CurrentUser.ProviderData)
                if (provider.ProviderId == "google.com") wasGoogleUser = true;
#endif
            auth.SignOut();
#if UNITY_ANDROID && !UNITY_EDITOR
            // Accessing DefaultInstance creates the native client. Do not do that
            // when an email account signs out or Google sign-in was never started.
            if (wasGoogleUser && GoogleSignIn.Configuration != null)
            {
                try { GoogleSignIn.DefaultInstance.SignOut(); }
                catch (System.Exception exception) { Debug.LogException(exception); }
            }
#endif
            ShowLoginScreen();
            Feedback("Logged out successfully.", logoutFeedbackDuration);
        }
    }

    // --- UI ROUTING ---
    public void ShowLoginScreen() 
    { 
        if (usernameSetup != null) usernameSetup.Hide();
        var menu = FindFirstObjectByType<MainMenuManager>();
        if (menu != null) menu.ShowLoginMenu();
        if (characterSelectMenu != null) characterSelectMenu.SetActive(false);
        loginMenu.SetActive(true); 
        // signUpMenu.SetActive(false); 
        modeSelection.SetActive(false); 
        if (settingsMenu != null) settingsMenu.SetActive(false); 

        ClearAllInputs();
    }
    public void ShowSignUpScreen() 
    { 
        loginMenu.SetActive(false); 
        // signUpMenu.SetActive(true); 
        modeSelection.SetActive(false); 
    }
    private void ShowModeSelection() 
    { 
        if (usernameSetup != null) usernameSetup.Hide();
        loginMenu.SetActive(false); 
        // signUpMenu.SetActive(false); 
        if (characterSelectMenu != null) characterSelectMenu.SetActive(false);
        modeSelection.SetActive(true); 

        ClearAllInputs();
    }
    private void ShowCharacterSelectScreen()
    {
        if (usernameSetup != null) usernameSetup.Hide();
        loginMenu.SetActive(false); 
        // signUpMenu.SetActive(false); 
        modeSelection.SetActive(false); 
        characterSelectMenu.SetActive(true);

        ClearAllInputs();
    }

    private void ClearAllInputs()
    {
        if (passwordResetMessage != null) passwordResetMessage.text = "";
        if (emailLoginInput != null) emailLoginInput.text = "";
        if (passwordLoginInput != null) passwordLoginInput.text = "";
        if (emailSignUpInput != null) emailSignUpInput.text = "";
        if (usernameSignUpInput != null) usernameSignUpInput.text = "";
        if (passwordSignUpInput != null) passwordSignUpInput.text = "";
    }

    private static bool HasPasswordProvider(FirebaseUser user)
    {
        foreach (var provider in user.ProviderData)
            if (provider.ProviderId == "password") return true;
        return false;
    }
}
