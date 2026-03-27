using UnityEngine;
using UnityEngine.Events;

public class ChallengeModule : MonoBehaviour
{
    [Header("Challenge Settings")]
    [SerializeField] private Transform respawnPoint;
    [SerializeField] private float challengeTimerDuration = 180f;

    [Header("Puzzle Elements to Reset")]
    [SerializeField] private LeverController lever; // Leave empty if this challenge doesn't have one

    [Header("Custom Events")]
    // We use SerializeField on UnityEvents so you can wire up unique things in the Inspector!
    [SerializeField] private UnityEvent onResetChallenge;

    public Transform GetRespawnPoint() { return respawnPoint; }
    public float GetTimerDuration() { return challengeTimerDuration; }

    public void ResetThisChallenge()
    {
        // ---> AUTOMATION: This searches through all child objects attached to this Challenge
        // and automatically finds any script called "ResettableObject". <---
        ResettableObject[] myResettableObjects = GetComponentsInChildren<ResettableObject>(true);
        
        foreach (var obj in myResettableObjects)
        {
            if (obj != null) obj.ResetPosition();
        }

        if (lever != null) lever.ResetLever();

        // 2. Trigger any custom resets you drag into the Inspector
        onResetChallenge?.Invoke();
    }
}