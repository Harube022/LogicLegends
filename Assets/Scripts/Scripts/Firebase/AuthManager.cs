using UnityEngine;
using UnityEngine.UI;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;
using Google; 
using System.Threading.Tasks;

public class AuthManager : MonoBehaviour
{
    [Header("UI Panels")]
    [SerializeField] private GameObject loginMenu;     
    // [SerializeField] private GameObject signUpMenu;    
    [SerializeField] private GameObject modeSelection; 
    [SerializeField] private GameObject settingsMenu;
    [SerializeField] private GameObject characterSelectMenu;

    [Header("Google Configuration")]
    [Tooltip("Paste your Web Client ID from the Firebase Console here")]
    [SerializeField] private string webClientId = "";

    [Header("Login Inputs")]
    [SerializeField] private InputField emailLoginInput;
    [SerializeField] private InputField passwordLoginInput;

    [Header("Sign Up Inputs")]
    [SerializeField] private InputField emailSignUpInput;
    [SerializeField] private InputField usernameSignUpInput;
    [SerializeField] private InputField passwordSignUpInput;

    // ---> NEW: Web Registration URL <---
    [Header("Web Links")]
    [SerializeField] private string webRegistrationUrl = "http://127.0.0.1:5500/Logic-Legends-Website/Logic_Legends_Website/LoginPage/login_page.html";

    private FirebaseAuth auth;
    private GoogleSignInConfiguration configuration;

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
    }

    public void CheckLoginState()
    {
        auth = FirebaseAuth.DefaultInstance;

        configuration = new GoogleSignInConfiguration
        {
            WebClientId = webClientId,
            RequestIdToken = true,
            RequestEmail = true
        };

        if (auth.CurrentUser != null)
        {
            Debug.Log("Found saved session. Verifying with server...");
            auth.CurrentUser.ReloadAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsCanceled || task.IsFaulted)
                {
                    Debug.LogWarning("Account is invalid or was deleted. Forcing logout.");
                    auth.SignOut(); 
                    ShowLoginScreen();
                }
                else
                {
                    Debug.Log($"Welcome back, {auth.CurrentUser.DisplayName ?? auth.CurrentUser.Email}!");
                    CheckFirstTimeSetup();
                }
            });
        }
        else
        {
            Debug.Log("No user found. Please log in.");
            ShowLoginScreen(); 
        }
    }

    // --- FIREBASE DATABASE INTERCEPTOR ---

    private void CheckFirstTimeSetup()
    {
        if (auth.CurrentUser == null) return;
        string userId = auth.CurrentUser.UserId;
        DatabaseReference dbRef = FirebaseDatabase.DefaultInstance.RootReference;

        Debug.Log("Checking if player has chosen a base character...");

        dbRef.Child("users").Child(userId).Child("base_character").GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                ShowModeSelection(); // Fallback just in case
                return;
            }

            DataSnapshot snapshot = task.Result;
            if (snapshot.Exists && snapshot.Value != null && snapshot.Value.ToString() != "")
            {
                // They already have a character saved! Send them to the game.
                ShowModeSelection();
            }
            else
            {
                // First time playing! Show the selection screen.
                ShowCharacterSelectScreen();
            }
        });
    }

    // Call this from your Male / Female UI Buttons
    public void SelectBaseCharacter(string characterID)
    {
        if (auth.CurrentUser == null) return;
        string userId = auth.CurrentUser.UserId;
        DatabaseReference dbRef = FirebaseDatabase.DefaultInstance.RootReference;

        // Save their choice to Firebase
        dbRef.Child("users").Child(userId).Child("base_character").SetValueAsync(characterID).ContinueWithOnMainThread(task =>
        {
            if (task.IsCompleted)
            {
                Debug.Log($"Successfully saved {characterID} as base character!");
                ShowModeSelection(); // Move them to the game now!
            }
        });
    }

    // --- GOOGLE SIGN-IN ---

    public void OnClickGoogleSignIn()
    {
        GoogleSignIn.Configuration = configuration;
        GoogleSignIn.Configuration.UseGameSignIn = false;
        GoogleSignIn.Configuration.RequestIdToken = true;

        Debug.Log("Opening Google Sign-In Pop-up...");
        
        GoogleSignIn.DefaultInstance.SignIn().ContinueWithOnMainThread(OnGoogleSignInFinished);
    }

    private void OnGoogleSignInFinished(Task<GoogleSignInUser> task)
    {
        if (task.IsFaulted || task.IsCanceled)
        {
            Debug.LogError("Google Sign-In failed or was canceled.");
            ShowLoginScreen();
            return;
        }

        Debug.Log("Google Token received! Handing over to Firebase...");
        
        Credential credential = GoogleAuthProvider.GetCredential(task.Result.IdToken, null);

        auth.SignInWithCredentialAsync(credential).ContinueWithOnMainThread(authTask =>
        {
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
                return;
            }

            Debug.Log("Google Login Success! Waiting for Database Security Sync...");
            // ---> THE FIX: Wait 0.5 seconds for the database to recognize the new Google token!
            Invoke(nameof(CheckFirstTimeSetup), 0.5f);
        });
    }

    // --- EXISTING EMAIL/PASSWORD METHODS ---

    public void OnClickLogin()
    {
        auth.SignInWithEmailAndPasswordAsync(emailLoginInput.text, passwordLoginInput.text).ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled || task.IsFaulted)
            {
                Debug.LogError("Login Failed!");
                return;
            }

            // ---> NEW: Force Email Verification! <---
            if (!auth.CurrentUser.IsEmailVerified)
            {
                Debug.LogWarning("Access Denied: Please verify your email address first!");
                auth.SignOut(); // Kick them out until they click the link!
                return;
            }
            // ---> THE FIX: Add the same delay here for testing new accounts!
            Invoke(nameof(CheckFirstTimeSetup), 0.5f);
            // ShowModeSelection();
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
        }
    }

    // --- UI ROUTING ---
    public void ShowLoginScreen() 
    { 
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
        if (emailLoginInput != null) emailLoginInput.text = "";
        if (passwordLoginInput != null) passwordLoginInput.text = "";
        if (emailSignUpInput != null) emailSignUpInput.text = "";
        if (usernameSignUpInput != null) usernameSignUpInput.text = "";
        if (passwordSignUpInput != null) passwordSignUpInput.text = "";
    }
}