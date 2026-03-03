using UnityEngine;

public class SmoothFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Offset")]
    public Vector3 offset = new Vector3(0f, 3f, -6f);

    [Header("Smooth Settings")]
    public float followSpeed = 8f;
    public float rotationSpeed = 8f;

    [Header("Collision Settings")]
    public float collisionRadius = 0.3f;
    public float minDistance = 1.5f;
    public LayerMask collisionLayers;

    private float currentDistance;

    void Start()
    {
        currentDistance = offset.magnitude;
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 direction = offset.normalized;

        float desiredDistance = offset.magnitude;

        RaycastHit hit;

        // Check if something blocks the camera
        if (Physics.SphereCast(
            target.position,
            collisionRadius,
            direction,
            out hit,
            desiredDistance,
            collisionLayers))
        {
            desiredDistance = Mathf.Clamp(hit.distance, minDistance, offset.magnitude);
        }

        currentDistance = Mathf.Lerp(currentDistance, desiredDistance, Time.deltaTime * 10f);

        Vector3 desiredPosition = target.position + direction * currentDistance;

        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            followSpeed * Time.deltaTime
        );

        Quaternion lookRotation = Quaternion.LookRotation(target.position - transform.position);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            lookRotation,
            rotationSpeed * Time.deltaTime
        );
    }
}