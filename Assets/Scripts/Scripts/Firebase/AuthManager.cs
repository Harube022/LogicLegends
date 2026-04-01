using UnityEngine;
using UnityEngine.UI;
using Firebase.Auth;
using Firebase.Extensions;

public class AuthManager : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject loginMenu;     
    public GameObject signUpMenu;    
    public GameObject modeSelection; 

    [Header("Login Inputs")]
    public InputField emailLoginInput;
    public InputField passwordLoginInput;

    [Header("Sign Up Inputs")]
    public InputField emailSignUpInput;
    public InputField usernameSignUpInput;
    public InputField passwordSignUpInput;

    private FirebaseAuth auth;

    // We will call this from your FirebaseManager once Firebase wakes up!
    public void CheckLoginState()
    {
        auth = FirebaseAuth.DefaultInstance;

        // Is there a saved session on the device?
        if (auth.CurrentUser != null)
        {
            Debug.Log("Found saved session. Verifying with server...");

            // Force a check with the Firebase database to ensure the account wasn't deleted
            auth.CurrentUser.ReloadAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsCanceled || task.IsFaulted)
                {
                    // The server rejected the token (Account deleted, disabled, or password changed)
                    Debug.LogWarning("Account is invalid or was deleted. Forcing logout.");
                    auth.SignOut(); // Wipes the local memory!
                    ShowLoginScreen();
                }
                else
                {
                    // The server confirmed the account is still good!
                    Debug.Log($"Welcome back, {auth.CurrentUser.Email}!");
                    ShowModeSelection(); 
                }
            });
        }
        else
        {
            Debug.Log("No user found. Please log in.");
            ShowLoginScreen(); // Force them to log in!
        }
    }

    // --- BUTTON METHODS ---

    public void OnClickLogin()
    {
        string email = emailLoginInput.text;
        string password = passwordLoginInput.text;

        auth.SignInWithEmailAndPasswordAsync(email, password).ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled || task.IsFaulted)
            {
                Debug.LogError("Login Failed: " + task.Exception);
                return;
            }

            Debug.Log("Login Successful!");
            ShowModeSelection();
        });
    }

    public void OnClickSignUp()
    {
        string email = emailSignUpInput.text;
        string password = passwordSignUpInput.text;
        
        // We will use this username to create a profile later!
        string username = usernameSignUpInput.text; 

        auth.CreateUserWithEmailAndPasswordAsync(email, password).ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled || task.IsFaulted)
            {
                Debug.LogError("Sign Up Failed: " + task.Exception);
                return;
            }

            Debug.Log("Account Created Successfully!");
            ShowModeSelection();
        });
    }

    public void OnClickLogout()
    {
        if (auth != null && auth.CurrentUser != null)
        {
            auth.SignOut();
            Debug.Log("Logged out successfully.");
            ShowLoginScreen();
        }
    }

    // --- UI ROUTING ---

    public void ShowLoginScreen()
    {
        loginMenu.SetActive(true);
        signUpMenu.SetActive(false);
        modeSelection.SetActive(false);
    }

    public void ShowSignUpScreen()
    {
        loginMenu.SetActive(false);
        signUpMenu.SetActive(true);
        modeSelection.SetActive(false);
    }

    private void ShowModeSelection()
    {
        loginMenu.SetActive(false);
        signUpMenu.SetActive(false);
        modeSelection.SetActive(true);
    }
}