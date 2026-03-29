using UnityEngine;

public class WorldIndicator : MonoBehaviour
{
    [Header("Target Setup")]
    public Transform currentTarget; 
    [SerializeField] private float heightOffset = 3.5f;

    [Header("Animation Settings")]
    [SerializeField] private float bobSpeed = 6f;
    [SerializeField] private float bobHeight = 0.5f;

    private void Update()
    {
        if (currentTarget == null) return;

        // Bobbing math
        Vector3 basePosition = currentTarget.position + (Vector3.up * heightOffset);
        float bobbingOffset = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        
        transform.position = basePosition + new Vector3(0, bobbingOffset, 0);
        transform.LookAt(Camera.main.transform);
    }

    public void PointAt(Transform newTarget)
    {
        currentTarget = newTarget;
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}