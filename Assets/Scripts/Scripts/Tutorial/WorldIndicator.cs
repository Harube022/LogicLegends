using UnityEngine;
using UnityEngine.UI;

public class WorldIndicator : MonoBehaviour
{
    [Header("Target Setup")]
    public Transform currentTarget; 
    [SerializeField] private float heightOffset = 3.5f;

    [Header("3D Animation Settings")]
    [SerializeField] private float bobSpeed = 6f;
    [SerializeField] private float bobHeight = 0.5f;

    [Header("Off-Screen UI Settings")]
    [Tooltip("Drag the 2D UI Arrow PREFAB you just made here")]
    [SerializeField] private GameObject uiPointerPrefab;
    [Tooltip("How far from the edge of the screen should it float?")]
    [SerializeField] private float edgePadding = 40f;

    private GameObject uiPointerInstance;
    private RectTransform uiPointerRect;

    private void Start()
    {
        // 1. Find your Canvas and spawn the UI arrow inside it
        if (uiPointerPrefab != null)
        {
            GameObject mainCanvas = GameObject.Find("Canvas 1");
            if (mainCanvas != null)
            {
                uiPointerInstance = Instantiate(uiPointerPrefab, mainCanvas.transform);
                uiPointerRect = uiPointerInstance.GetComponent<RectTransform>();
                uiPointerInstance.SetActive(false); // Hide it until we need it
            }
            else
            {
                Debug.LogError("Could not find a GameObject named 'Canvas 1'!");
            }
        }
        else
        {
            Debug.LogWarning("You forgot to assign the UI Pointer Prefab in the Inspector!");
        }
    }

    private void Update()
    {
        if (currentTarget == null) return;

        // 1. The 3D Bobbing Math (Stays exactly the same!)
        Vector3 basePosition = currentTarget.position + (Vector3.up * heightOffset);
        float bobbingOffset = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        
        transform.position = basePosition + new Vector3(0, bobbingOffset, 0);
        // transform.LookAt(Camera.main.transform);
        transform.Rotate(0, 80f * Time.deltaTime, 0);

        // 2. The Off-Screen UI Math
        UpdateOffScreenPointer();
    }

    private void UpdateOffScreenPointer()
    {
        if (uiPointerInstance == null || uiPointerRect == null) return;

        // Find where the target is relative to the camera screen
        Vector3 screenPos = Camera.main.WorldToScreenPoint(currentTarget.position);
        
        // Is it behind us, or off the edges of the screen?
        bool isOffScreen = screenPos.z < 0 || screenPos.x < 0 || screenPos.x > Screen.width || screenPos.y < 0 || screenPos.y > Screen.height;

        if (isOffScreen)
        {
            uiPointerInstance.SetActive(true);

            // If the object is behind the camera, flip the coordinates so the arrow points backwards
            if (screenPos.z < 0) screenPos *= -1;

            Vector3 screenCenter = new Vector3(Screen.width / 2f, Screen.height / 2f, 0);
            Vector3 dir = (screenPos - screenCenter).normalized;

            // Rotate the UI Arrow to point at the target
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            uiPointerRect.localEulerAngles = new Vector3(0, 0, angle + 90f); // -90 assumes your sprite faces UP

            // Clamp the UI Arrow to the edges of the screen
            Vector3 screenBounds = screenCenter - new Vector3(edgePadding, edgePadding, 0);
            float xClamp = dir.x > 0 ? screenBounds.x : -screenBounds.x;
            float yClamp = dir.y > 0 ? screenBounds.y : -screenBounds.y;
            Vector3 clampedPos = Vector3.zero;

            if (dir.x == 0) 
            {
                clampedPos = new Vector3(0, yClamp, 0);
            } 
            else 
            {
                float m = dir.y / dir.x;
                if (Mathf.Abs(xClamp * m) < screenBounds.y)
                    clampedPos = new Vector3(xClamp, xClamp * m, 0);
                else
                    clampedPos = new Vector3(yClamp / m, yClamp, 0);
            }

            uiPointerRect.position = screenCenter + clampedPos;
        }
        else
        {
            // The target is safely on-screen, so hide the UI pointer!
            uiPointerInstance.SetActive(false);
        }
    }

    public void PointAt(Transform newTarget)
    {
        currentTarget = newTarget;
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        if (uiPointerInstance != null) uiPointerInstance.SetActive(false);
    }

    private void OnDestroy()
    {
        // Clean up the UI element when the 3D arrow gets destroyed by your Dynamic Spawner
        if (uiPointerInstance != null) Destroy(uiPointerInstance);
    }
}