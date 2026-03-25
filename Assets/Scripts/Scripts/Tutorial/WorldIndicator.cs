using UnityEngine;

public class WorldIndicator : MonoBehaviour
{
    [Header("Target Setup")]
    // Serialized so you can watch it change in the Inspector while playing
    [SerializeField] private Transform currentTarget; 
    [SerializeField] private float heightOffset = 3.5f; // How high above the object it floats

    [Header("Animation Settings")]
    [SerializeField] private float bobSpeed = 6f;
    [SerializeField] private float bobHeight = 0.5f;

    private void Update()
    {
        if (currentTarget == null) return;

        // 1. Calculate the base position (Object's position + height)
        Vector3 basePosition = currentTarget.position + (Vector3.up * heightOffset);

        // 2. Add the smooth bobbing math (Mathf.Sin)
        float bobbingOffset = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        
        // 3. Apply the final position
        transform.position = basePosition + new Vector3(0, bobbingOffset, 0);

        // 4. Force the arrow to always face the camera (Billboarding)
        transform.LookAt(Camera.main.transform);
        
        // If your sprite looks backwards/upside down after LookAt, uncomment this line:
        // transform.Rotate(0, 180, 0);
    }

    // Call this method from other scripts to move the arrow!
    public void PointAt(Transform newTarget)
    {
        currentTarget = newTarget;
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        currentTarget = null;
    }
}