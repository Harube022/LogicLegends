using UnityEngine;
using UnityEngine.UI;
using Firebase.Auth;
using Firebase.Extensions;
using Google; 
using System.Threading.Tasks;

public class AuthManager : MonoBehaviour
{
    [Header("UI Panels")]
    [SerializeField] private GameObject loginMenu;     
    [SerializeField] private GameObject signUpMenu;    
    [SerializeField] private GameObject modeSelection; 
    [SerializeField] private GameObject settingsMenu;

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

    private FirebaseAuth auth;
    private GoogleSignInConfiguration configuration;

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
                    ShowModeSelection(); 
                }
            });
        }
        else
        {
            Debug.Log("No user found. Please log in.");
            ShowLoginScreen(); 
        }
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
            return;
        }

        Debug.Log("Google Token received! Handing over to Firebase...");
        
        Credential credential = GoogleAuthProvider.GetCredential(task.Result.IdToken, null);

        auth.SignInWithCredentialAsync(credential).ContinueWithOnMainThread(authTask =>
        {
            if (authTask.IsCanceled || authTask.IsFaulted)
            {
                Debug.LogError("Firebase Auth Failed: " + authTask.Exception);
                return;
            }

            FirebaseUser newUser = auth.CurrentUser;
            Debug.Log($"Google Login Successful! Welcome {newUser.DisplayName}!");
            
            ShowModeSelection();
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
            ShowModeSelection();
        });
    }

    public void OnClickSignUp()
    {
        auth.CreateUserWithEmailAndPasswordAsync(emailSignUpInput.text, passwordSignUpInput.text).ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled || task.IsFaulted)
            {
                Debug.LogError("Sign Up Failed!");
                return;
            }
            ShowModeSelection();
        });
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
        signUpMenu.SetActive(false); 
        modeSelection.SetActive(false); 
        if (settingsMenu != null) settingsMenu.SetActive(false); 
    }
    public void ShowSignUpScreen() { loginMenu.SetActive(false); signUpMenu.SetActive(true); modeSelection.SetActive(false); }
    private void ShowModeSelection() { loginMenu.SetActive(false); signUpMenu.SetActive(false); modeSelection.SetActive(true); }
}