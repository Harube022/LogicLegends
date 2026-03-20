using UnityEngine;
using UnityEngine.Events;

public class ChallengeModule : MonoBehaviour
{
    [Header("Challenge Settings")]
    [SerializeField] private Transform respawnPoint;
    [SerializeField] private float challengeTimerDuration = 180f;

    [Header("Puzzle Elements to Reset")]
    // You can drag your specific Challenge 1 or Challenge 2 items here in the Inspector
    [SerializeField] private ResettableObject[] resettableObjects;
    [SerializeField] private LeverController lever; // Leave empty if this challenge doesn't have one

    [Header("Custom Events")]
    // We use SerializeField on UnityEvents so you can wire up unique things in the Inspector!
    [SerializeField] private UnityEvent onResetChallenge;

    public Transform GetRespawnPoint() { return respawnPoint; }
    public float GetTimerDuration() { return challengeTimerDuration; }

    public void ResetThisChallenge()
    {
        // 1. Reset standard objects
        foreach (var obj in resettableObjects)
        {
            if (obj != null) obj.ResetPosition();
        }

        if (lever != null) lever.ResetLever();

        // 2. Trigger any custom resets you drag into the Inspector
        onResetChallenge?.Invoke();
    }
}