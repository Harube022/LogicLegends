using UnityEngine;
using Firebase;
using Firebase.Extensions;
using UnityEngine.Events;

public class FirebaseManager : MonoBehaviour
{
    [Header("Events")]
    public UnityEvent OnFirebaseReady;

    public static bool IsReady { get; private set; }
    public static string InitializationError { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        IsReady = false;
        InitializationError = null;
    }

    private void Start()
    {
        if (IsReady)
        {
            OnFirebaseReady?.Invoke();
            return;
        }
        Debug.Log("Waking up Firebase...");
        
        // This checks if the phone has the required Google Play Services
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task => 
        {
            if (this == null) return;
            if (task.IsCanceled || task.IsFaulted)
            {
                InitializationError = "Could not connect. Please restart the app and try again.";
                var menuAuth = FindFirstObjectByType<AuthManager>();
                if (menuAuth != null) menuAuth.ShowInitializationError(InitializationError);
                return;
            }
            var dependencyStatus = task.Result;
            if (dependencyStatus == DependencyStatus.Available)
            {
                // Firebase is ready to use!
                FirebaseApp app = FirebaseApp.DefaultInstance;
                IsReady = true;
                InitializationError = null;
                Debug.Log("<color=green>Firebase successfully initialized!</color>");
                
                // Trigger the event so the rest of your game knows it's safe to log in
                OnFirebaseReady?.Invoke(); 
            }
            else
            {
                InitializationError = "Login services are unavailable. Please restart the app.";
                var menuAuth = FindFirstObjectByType<AuthManager>();
                if (menuAuth != null) menuAuth.ShowInitializationError(InitializationError);
                Debug.LogError($"Could not resolve all Firebase dependencies: {dependencyStatus}");
            }
        });
    }
}
