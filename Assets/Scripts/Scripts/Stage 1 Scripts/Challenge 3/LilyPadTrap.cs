using UnityEngine;
using System.Collections;

public class LilyPadTrap : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField] private Animator padAnimator;
    [SerializeField] private float dangerSpinSpeed = 4f;

    [Header("Physics")]
    [SerializeField] private float knockbackForce = 15f;
    [SerializeField] private float upwardForce = 10f;

    private bool hasTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (!hasTriggered && other.CompareTag("Player"))
        {
            StartCoroutine(TriggerTrapSequence(other.gameObject));
        }
    }

    private IEnumerator TriggerTrapSequence(GameObject player)
    {
        hasTriggered = true;

        if (padAnimator != null) padAnimator.speed = dangerSpinSpeed;

        // 1. Turn off custom movement so they can fly
        Player playerScript = player.GetComponent<Player>();
        Rigidbody playerRb = player.GetComponent<Rigidbody>();

        // ---> NEW: Get the Character Controller
        CharacterController cc = player.GetComponent<CharacterController>();

        CapsuleCollider backupCollider = player.GetComponent<CapsuleCollider>();

        if (backupCollider != null) backupCollider.enabled = true;
        if (playerScript != null) playerScript.enabled = false;

        // ---> NEW: Turn off the controller so the Rigidbody can fly!
        if (cc != null) cc.enabled = false;

        if (playerRb != null)
        {
            playerRb.isKinematic = false;
            playerRb.linearVelocity = Vector3.zero;

            Vector3 pushDirection = (player.transform.position - transform.position).normalized;
            pushDirection.y = 0;
            Vector3 finalForce = (pushDirection * knockbackForce) + (Vector3.up * upwardForce);
            playerRb.AddForce(finalForce, ForceMode.Impulse);
        }

        // 2. Wait 2 seconds for the animation to finish spinning and for the player to fall
        yield return new WaitForSeconds(3f);

        // 3. Failsafe: Just in case they got thrown and landed on a safe platform instead of water!
        if (playerRb != null) playerRb.isKinematic = true;
        // ---> NEW: Turn the controller back on when they land
        if (cc != null) cc.enabled = true;
        if (backupCollider != null) backupCollider.enabled = false;
        if (playerScript != null) playerScript.enabled = true;

        if (padAnimator != null) padAnimator.speed = 1f;
        hasTriggered = false;
    }
}