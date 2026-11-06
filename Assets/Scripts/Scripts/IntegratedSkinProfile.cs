using UnityEngine;

/// <summary>Local visual mapping; the existing equipment ID remains the account key.</summary>
[DisallowMultipleComponent]
public sealed class IntegratedSkinProfile : MonoBehaviour
{
    public string itemID, gender, category;
    public Transform leftHand, rightHand;
    public float referenceHeight;
    public Vector3 carryOffset;
    public float minimumCarryForward = .65f;
    [Tooltip("Stable reference measured from this skin's matching Carry pose, in model space.")]
    public Vector3 carryReferenceLocal;
    public float carryHandSpanLocal;

    public static bool TryVisualBounds(Transform outfit, out Bounds bounds)
    {
        bool found = false; bounds = new Bounds();
        var mesh = new Mesh();
        try
        {
            foreach (var renderer in outfit.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!renderer.enabled) continue;
                renderer.BakeMesh(mesh, true);
                foreach (var vertex in mesh.vertices)
                {
                    var point = renderer.transform.TransformPoint(vertex);
                    if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; } else bounds.Encapsulate(point);
                }
                mesh.Clear();
            }
        }
        finally { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
        return found;
    }

    void OnEnable()
    {
        var rig = GetComponentInParent<PlayerCarryRig>();
        if (rig != null) rig.SelectSkin(this);
    }
    void OnDisable()
    {
        var rig = GetComponentInParent<PlayerCarryRig>();
        if (rig != null) rig.DeselectSkin(this);
    }
}
