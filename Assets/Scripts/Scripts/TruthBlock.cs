using UnityEngine;
using System.Collections;

public class TruthBlock : MonoBehaviour
{
    public bool value; // T = true, F = false

    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        originalPosition = transform.position;
        originalRotation = transform.rotation;
    }

    public void ReturnToOrigin(bool smooth = true)
    {
        Collider col = GetComponent<Collider>();
        col.enabled = false;

        rb.isKinematic = true;
        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (smooth)
        {
            StartCoroutine(SmoothReturn(col));
        }
        else
        {
            transform.position = originalPosition;
            transform.rotation = originalRotation;

            rb.isKinematic = false;
            rb.useGravity = true;
            col.enabled = true;
        }
    }

    private IEnumerator SmoothReturn(Collider col)
    {
        float duration = 0.4f;
        float time = 0f;

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;

            transform.position = Vector3.Lerp(startPos, originalPosition, t);
            transform.rotation = Quaternion.Slerp(startRot, originalRotation, t);

            yield return null;
        }

        transform.position = originalPosition;
        transform.rotation = originalRotation;

        rb.isKinematic = false;
        rb.useGravity = true;
        col.enabled = true;
    }
}