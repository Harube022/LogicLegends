using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Local mannequin inspection only; never changes saved equipment or gameplay characters.</summary>
public sealed class CharacterInspectView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private Camera previewCamera;
    [SerializeField] private bool fitCamera = true;
    [SerializeField, Range(0.8f,1.25f)]
    [Tooltip("Lower values make the Shop character larger. 1.0 fits the full turn with no extra margin.")]
    private float framingPadding = 0.8f;
    [SerializeField, Min(1)] private float degreesPerFullDrag = 360;
    private Transform character, outfit;
    private Quaternion originalRotation, cameraRotation;
    private Vector3 cameraPosition;
    private float cameraSize;
    private bool capturedCamera, refit;
    private int? dragPointer;

    public void ShowCharacter(Transform body, Transform selectedOutfit)
    {
        if (character != body)
        {
            Clear();
            character = body;
            if (character != null) originalRotation = character.localRotation;
        }
        outfit = selectedOutfit;
        if (character == null || outfit == null) return;
        if (fitCamera && previewCamera != null && !capturedCamera)
        {
            cameraPosition = previewCamera.transform.position;
            cameraRotation = previewCamera.transform.rotation;
            cameraSize = previewCamera.orthographicSize;
            capturedCamera = true;
        }
        FitCharacter();
        refit = true; // Recheck once after the newly activated skinned mesh has updated.
    }

    private void LateUpdate()
    {
        if (refit) { FitCharacter(); refit = false; }
    }

    private void OnValidate()
    {
        // Let designers tune the Shop framing live while the preview is open.
        if (Application.isPlaying && capturedCamera && character != null && outfit != null)
            FitCharacter();
    }

    private void FitCharacter()
    {
        if (!fitCamera || previewCamera == null || character == null || outfit == null) return;
        bool found = false;
        var bounds = new Bounds();
        foreach (var renderer in outfit.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        if (!found || bounds.size.y <= .01f) return;
        var target = new Vector3(character.position.x,bounds.center.y,character.position.z);
        float aspect = Mathf.Max(.1f,previewCamera.aspect);
        float fit = 0f;
        if (previewCamera.orthographic)
        {
            fit = 0f;
        }

        // Fit the renderer bounds at 5-degree yaw intervals. The earlier radius-plus-height
        // estimate added depth margin twice, which made the Shop character look too small.
        // Projecting the corners into camera space keeps the whole outfit visible while using
        // the available preview area more effectively.
        float tangentY = previewCamera.orthographic ? 0f : Mathf.Tan(previewCamera.fieldOfView*Mathf.Deg2Rad*.5f);
        float tangentX = tangentY*aspect;
        Quaternion inverseCameraRotation = Quaternion.Inverse(cameraRotation);
        Vector3 pivot = character.position;
        float orthographicSize = 0f;
        for (int yaw = 0; yaw < 360; yaw += 5)
        {
            Quaternion turn = Quaternion.AngleAxis(yaw,Vector3.up);
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,new Vector3(x,y,z));
                        Vector3 turned = pivot + turn*(corner-pivot);
                        Vector3 cameraSpace = inverseCameraRotation*(turned-target);
                        if (previewCamera.orthographic)
                        {
                            orthographicSize = Mathf.Max(orthographicSize,
                                Mathf.Abs(cameraSpace.y)*framingPadding,
                                Mathf.Abs(cameraSpace.x)*framingPadding/aspect);
                        }
                        else
                        {
                            fit = Mathf.Max(fit,
                                Mathf.Abs(cameraSpace.y)*framingPadding/tangentY-cameraSpace.z,
                                Mathf.Abs(cameraSpace.x)*framingPadding/tangentX-cameraSpace.z,
                                previewCamera.nearClipPlane+.1f-cameraSpace.z);
                        }
                    }
        }
        float distance = Mathf.Max(.1f,fit);
        if (previewCamera.orthographic) previewCamera.orthographicSize = Mathf.Max(.1f,orthographicSize);
        previewCamera.transform.position = target - (cameraRotation*Vector3.forward)*distance;
        previewCamera.transform.rotation = cameraRotation;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || character == null ||
            !character.gameObject.activeInHierarchy || dragPointer.HasValue) return;
        dragPointer = eventData.pointerId;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragPointer != eventData.pointerId || character == null || !character.gameObject.activeInHierarchy) return;
        var rect = (RectTransform)transform;
        var canvas = GetComponentInParent<Canvas>();
        float width = Mathf.Max(1,rect.rect.width*(canvas != null ? canvas.scaleFactor : 1));
        float angle = -eventData.delta.x/width*degreesPerFullDrag;
        character.rotation = Quaternion.AngleAxis(angle,Vector3.up)*character.rotation;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragPointer == eventData.pointerId) dragPointer = null;
    }

    private void OnDisable() => Clear();
    private void OnDestroy() => Clear();
    public void Clear()
    {
        dragPointer = null;
        refit = false;
        if (character != null) character.localRotation = originalRotation;
        if (capturedCamera && previewCamera != null)
        {
            previewCamera.transform.SetPositionAndRotation(cameraPosition,cameraRotation);
            previewCamera.orthographicSize = cameraSize;
        }
        capturedCamera = false;
        character = outfit = null;
    }
}
