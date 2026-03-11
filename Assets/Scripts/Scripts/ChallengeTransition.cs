using UnityEngine;

public class ChallengeTransition : MonoBehaviour
{
    [Header("Teleport Settings")]
    [Tooltip("Drag the empty GameObject where the player should start Challenge 2 here")]
    [SerializeField] private Transform challenge2StartPoint;

    [Header("Visibility Settings")]
    [Tooltip("Drag the Challenge folder you want to HIDE (e.g., Challenge 1)")]
    [SerializeField] private GameObject challengeToHide;
    [Tooltip("Drag the Challenge folder you want to SHOW (e.g., Challenge 2)")]
    [SerializeField] private GameObject challengeToShow;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // 1. Teleport the player
            other.transform.position = challenge2StartPoint.position;

            Rigidbody playerRb = other.GetComponent<Rigidbody>();
            if (playerRb != null) playerRb.linearVelocity = Vector3.zero;

            // 2. Swap which challenge is visible!
            if (challengeToHide != null) challengeToHide.SetActive(false);
            if (challengeToShow != null) challengeToShow.SetActive(true);

            // 3. Update LevelManager
            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.UpdateRespawnPoint(challenge2StartPoint);
                LevelManager.Instance.HideTimer();
            }
        }
    }
}