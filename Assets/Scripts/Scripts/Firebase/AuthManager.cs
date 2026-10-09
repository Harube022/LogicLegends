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

    [Header("Cinematic Intro Settings")]
    [Tooltip("The UI Panel that holds your Video Player")]
    [SerializeField] private GameObject cinematicPanel;
    [Tooltip("Drag the Video Player component here")]
    [SerializeField] private VideoPlayer introVideoPlayer;
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
    private int sessionVersion;
    private string observedUserId;
    private volatile bool authStateChanged;
    private Coroutine feedbackTimer;

    private void OnAuthStateChanged(object sender, System.EventArgs args)
    {
        // Process the SDK notification on Unity's main thread in Update.
        authStateChanged = true;
    }

    private void Update()
    {
        if (!authStateChanged || signingIn || auth == null) return;
        authStateChanged = false;
        string currentId = auth.CurrentUser?.UserId;
        if (currentId == observedUserId) return;
        observedUserId = currentId;
        sessionVersion++;
        if (currentId != null) CheckLoginState();
        else
        {
            ShowLoginScreen();
            Feedback("Your session ended. Please log in again.");
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
        if (characterSelectMenu != null && characterSelectMenu.activeSelf)
            hideAfterSeconds = 1f;
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
        ShowLoginScreen();
        Feedback(message);
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
        Feedback("Connecting...");
    }

    public void CheckLoginState()
    {
        if (!FirebaseManager.IsReady) return;
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
            int version = sessionVersion;
            string savedId = savedUser.UserId;
            // Firebase persists credentials. A temporary network failure must not erase them.
            ShowModeSelection();
            Feedback("Welcome back! Loading your profile...");
            CheckFirstTimeSetup();
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
                        ShowLoginScreen();
                        Feedback("Your session has expired. Please log in again.");
                    }
                    else Feedback("Still signed in. Check your connection if your profile does not load.");
                }
                else
                {
                    if (!auth.CurrentUser.IsEmailVerified && HasPasswordProvider(auth.CurrentUser))
                    {
                        sessionVersion++;
                        observedUserId = null;
                        auth.SignOut();
                        ShowLoginScreen();
                        Feedback("Please verify your email before logging in.");
                    }
                }
            });
        }
        else
        {
            Debug.Log("No user found. Please log in.");
            ShowLoginScreen(); 
            Feedback("");
        }
    }

    // --- FIREBASE DATABASE INTERCEPTOR ---

    private void CheckFirstTimeSetup()
    {
        if (this == null || auth == null || auth.CurrentUser == null) return;
        string userId = auth.CurrentUser.UserId;
        observedUserId = userId;
        int version = sessionVersion;
        ShowModeSelection();
        DatabaseReference dbRef = FirebaseDatabase.DefaultInstance.RootReference;

        Debug.Log("Checking if player has chosen a base character...");

        dbRef.Child("users").Child(userId).Child("base_character").GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (!IsCurrentSession(version, userId)) return;
            if (task.IsFaulted || task.IsCanceled)
            {
                Feedback("Signed in. Couldn't load your profile; check your connection.");
                return;
            }

            DataSnapshot snapshot = task.Result;
            if (snapshot.Exists && snapshot.Value != null && snapshot.Value.ToString() != "")
            {
                // They already have a character saved! Send them to the game.
                Feedback("Logged in successfully.", 1f);
            }
            else
            {
                // First time playing! Show the selection screen.
                ShowCharacterSelectScreen();
                Feedback("Logged in successfully.", 1f);
            }
        });
    }

    // Call this from your Male / Female UI Buttons
    public void SelectBaseCharacter(string characterID)
    {
        if (auth == null || auth.CurrentUser == null) return;
        if (characterID != "Male_Character" && characterID != "Female_Character") return;
        string userId = auth.CurrentUser.UserId;
        int version = sessionVersion;
        DatabaseReference dbRef = FirebaseDatabase.DefaultInstance.RootReference;

        // Save their choice to Firebase
        dbRef.Child("users").Child(userId).Child("base_character").SetValueAsync(characterID).ContinueWithOnMainThread(task =>
        {
            if (!IsCurrentSession(version, userId)) return;
            if (task.IsFaulted || task.IsCanceled)
                Feedback("Couldn't save your character. Check your connection and try again.");
            else
            {
                Debug.Log($"Successfully saved {characterID} as base character!");
                // ShowModeSelection(); // Move them to the game now!
                PlayIntroCutscene();
            }
        });
    }

    // ---> NEW: Methods to handle the video and scene loading <---
    private void PlayIntroCutscene()
    {
        ClearFeedback();
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
            introVideoPlayer.loopPointReached -= OnCutsceneFinished;
            introVideoPlayer.errorReceived -= OnCutsceneError;
            // Subscribe to the event that fires when the video finishes
            introVideoPlayer.loopPointReached += OnCutsceneFinished;
            introVideoPlayer.errorReceived += OnCutsceneError;
            introVideoPlayer.Play();
        }
        else
        {
            Debug.LogWarning("No Video Player assigned! Skipping straight to tutorial.");
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

        // Load the Main Menu UI
        ShowModeSelection();
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
            Feedback("Please wait while login services connect.");
            return;
        }
        signingIn = true;
        int version = ++sessionVersion;
        Feedback("Opening Google sign-in...");
        GoogleSignIn.Configuration = configuration;
        GoogleSignIn.Configuration.UseGameSignIn = false;
        GoogleSignIn.Configuration.RequestIdToken = true;

        Debug.Log("Opening Google Sign-In Pop-up...");
        
        GoogleSignIn.DefaultInstance.SignIn().ContinueWithOnMainThread(task =>
        {
            if (this == null || version != sessionVersion) return;
            OnGoogleSignInFinished(task);
        });
    }

    private void OnGoogleSignInFinished(Task<GoogleSignInUser> task)
    {
        if (task.IsFaulted || task.IsCanceled)
        {
            signingIn = false;
            Debug.LogError("Google Sign-In failed or was canceled.");
            ShowLoginScreen();
            Feedback("Google sign-in was canceled or failed. Please try again.");
            return;
        }

        Debug.Log("Google Token received! Handing over to Firebase...");
        
        Credential credential = GoogleAuthProvider.GetCredential(task.Result.IdToken, null);
        int version = sessionVersion;

        auth.SignInWithCredentialAsync(credential).ContinueWithOnMainThread(authTask =>
        {
            if (this == null || version != sessionVersion) return;
            signingIn = false;
            // if (authTask.IsCanceled || authTask.IsFaulted)
            // {
            //     Debug.LogError("Firebase Auth Failed: " + authTask.Exception);
            //     return;
            // }

            // FirebaseUser newUser = auth.CurrentUser;
            // Debug.Log($"Google Login Successful! Welcome {newUser.DisplayName}!");
            
            // ShowModeSelection();
            if (authTask.IsCanceled || authTask.IsFaulted) 
            {
                Debug.LogError("Firebase failed to authenticate Google credential.");
                ShowLoginScreen();
                Feedback("Couldn't log in with Google. Check your connection and try again.");
                return;
            }

            Debug.Log("Google Login Success! Waiting for Database Security Sync...");
            // ---> THE FIX: Wait 0.5 seconds for the database to recognize the new Google token!
            Feedback("Logged in successfully.", 1f);
            CheckFirstTimeSetup();
        });
    }

    // --- EXISTING EMAIL/PASSWORD METHODS ---

    public void OnClickLogin()
    {
        if (signingIn) return;
        if (auth == null || !FirebaseManager.IsReady)
        {
            Feedback("Please wait while login services connect.");
            return;
        }
        string email = emailLoginInput.text.Trim();
        string password = passwordLoginInput.text;
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        {
            Feedback("Enter your email and password.");
            return;
        }
        signingIn = true;
        int version = ++sessionVersion;
        Feedback("Logging in...");
        auth.SignInWithEmailAndPasswordAsync(email, password).ContinueWithOnMainThread(task =>
        {
            if (this == null || version != sessionVersion) return;
            signingIn = false;
            if (task.IsCanceled || task.IsFaulted)
            {
                var error = ErrorCode(task.Exception);
                if (error == AuthError.InvalidEmail || error == AuthError.WrongPassword ||
                    error == AuthError.UserNotFound || error == AuthError.InvalidCredential)
                    Feedback("Incorrect email or password. Please try again.");
                else if (error == AuthError.Failure)
                    Feedback("Couldn't log in. Check your email and password and try again.");
                else if (error == AuthError.TooManyRequests)
                    Feedback("Too many attempts. Please wait and try again.");
                else if (error == AuthError.UserDisabled)
                    Feedback("This account is disabled.");
                else Feedback("Couldn't log in. Check your connection and try again.");
                return;
            }

            // ---> NEW: Force Email Verification! <---
            if (!auth.CurrentUser.IsEmailVerified)
            {
                Debug.LogWarning("Access Denied: Please verify your email address first!");
                auth.SignOut(); // Kick them out until they click the link!
                Feedback("Please verify your email before logging in.");
                return;
            }
            // ---> THE FIX: Add the same delay here for testing new accounts!
            Feedback("Logged in successfully.", 1f);
            CheckFirstTimeSetup();
            // ShowModeSelection();
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
            passwordResetMessage.text = "Enter your email address first.";
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
                        "Couldn't request a reset. Check your email address " +
                        "and connection, then try again.";
                    return;
                }

                passwordResetMessage.text =
                    // "If an eligible account exists for this email, " +
                    // "you'll receive a reset link. Check your spam folder too.";
                    "Reset password link has been sent to your email";
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
        sessionVersion++;
        observedUserId = null;
        signingIn = false;
        CancelInvoke();
        if (auth != null && auth.CurrentUser != null)
        {
            // 1. Log out of Firebase (This works perfectly in the Editor)
            auth.SignOut();

            // 2. Log out of the Google Plugin (ONLY run this on an actual Android phone)
#if UNITY_ANDROID && !UNITY_EDITOR
            if (GoogleSignIn.DefaultInstance != null) 
            {
                GoogleSignIn.DefaultInstance.SignOut(); 
            }
#endif

            // 3. Return to the Login Screen
            ShowLoginScreen();
            Feedback("Logged out successfully.", 6f);
        }
    }

    // --- UI ROUTING ---
    public void ShowLoginScreen() 
    { 
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
        loginMenu.SetActive(false); 
        // signUpMenu.SetActive(false); 
        if (characterSelectMenu != null) characterSelectMenu.SetActive(false);
        modeSelection.SetActive(true); 

        ClearAllInputs();
    }
    private void ShowCharacterSelectScreen()
    {
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
