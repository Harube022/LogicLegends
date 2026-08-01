using System.Collections.Generic;
using UnityEngine;

public class PlayerVisibilityController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Renderer[] playerRenderers;
    [SerializeField] private Transform cameraTransform;

    [Header("Proximity Settings")]
    [Tooltip("Distance where the character becomes completely invisible.")]
    [SerializeField] private float fadeEndDistance = 1.0f;

    [Tooltip("Distance where the character starts fading out.")]
    [SerializeField] private float fadeStartDistance = 2.0f;

    private MaterialPropertyBlock propertyBlock;
    private Dictionary<Renderer, Color> originalColors = new Dictionary<Renderer, Color>();

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
    }

    private void Start()
    {
        RefreshRenderers();

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    /// <summary>
    /// Call this if clothes/outfits are swapped dynamically at runtime.
    /// </summary>
    public void RefreshRenderers()
    {
        playerRenderers = GetComponentsInChildren<Renderer>(true);
        originalColors.Clear();

        foreach (Renderer rend in playerRenderers)
        {
            if (rend == null || rend.sharedMaterial == null) continue;

            // 1. Read original color from material ONCE on start
            Color matColor = Color.white;
            if (rend.sharedMaterial.HasProperty(BaseColorId))
            {
                matColor = rend.sharedMaterial.GetColor(BaseColorId);
            }
            else if (rend.sharedMaterial.HasProperty(ColorId))
            {
                matColor = rend.sharedMaterial.GetColor(ColorId);
            }

            // 2. Safety check: If material color returned dark/black, default to White so texture isn't multiplied by black
            if (matColor.r <= 0.05f && matColor.g <= 0.05f && matColor.b <= 0.05f)
            {
                matColor = Color.white;
            }

            if (!originalColors.ContainsKey(rend))
            {
                originalColors.Add(rend, matColor);
            }
        }
    }

    private void Update()
    {
        if (cameraTransform == null)
        {
            if (Camera.main != null) cameraTransform = Camera.main.transform;
            else return;
        }

        if (playerRenderers == null || playerRenderers.Length == 0) return;

        float distance = Vector3.Distance(transform.position, cameraTransform.position);

        foreach (Renderer rend in playerRenderers)
        {
            if (rend == null || !rend.gameObject.activeInHierarchy) continue;

            // Instantly hide character when camera gets closer than fadeEndDistance
            if (distance <= fadeEndDistance)
            {
                if (rend.enabled) rend.enabled = false;
                continue;
            }

            // Re-enable renderer when camera moves away
            if (!rend.enabled) rend.enabled = true;

            // Calculate fade and apply cached original color safely
            if (originalColors.TryGetValue(rend, out Color baseColor))
            {
                float alpha = Mathf.InverseLerp(fadeEndDistance, fadeStartDistance, distance);
                baseColor.a = alpha;

                rend.GetPropertyBlock(propertyBlock);

                if (rend.sharedMaterial.HasProperty(BaseColorId))
                {
                    propertyBlock.SetColor(BaseColorId, baseColor);
                }
                else if (rend.sharedMaterial.HasProperty(ColorId))
                {
                    propertyBlock.SetColor(ColorId, baseColor);
                }

                rend.SetPropertyBlock(propertyBlock);
            }
        }
    }
}