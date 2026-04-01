using UnityEngine;
using Firebase;
using Firebase.Extensions;
using UnityEngine.Events;

public class FirebaseManager : MonoBehaviour
{
    [Header("Events")]
    public UnityEvent OnFirebaseReady;

    private void Start()
    {
        Debug.Log("Waking up Firebase...");
        
        // This checks if the phone has the required Google Play Services
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task => 
        {
            var dependencyStatus = task.Result;
            if (dependencyStatus == DependencyStatus.Available)
            {
                // Firebase is ready to use!
                FirebaseApp app = FirebaseApp.DefaultInstance;
                Debug.Log("<color=green>Firebase successfully initialized!</color>");
                
                // Trigger the event so the rest of your game knows it's safe to log in
                OnFirebaseReady?.Invoke(); 
            }
            else
            {
                Debug.LogError($"Could not resolve all Firebase dependencies: {dependencyStatus}");
            }
        });
    }
}