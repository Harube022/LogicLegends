using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Run after the third-person camera and Cinemachine position the output camera.
[DefaultExecutionOrder(10000)]
public class PlayerVisibilityController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Renderer[] playerRenderers;
    [SerializeField] private Transform cameraTransform;

    [Header("Proximity Settings")]
    [Tooltip("Camera distance from CameraTarget where the character is invisible.")]
    [SerializeField] private float fadeEndDistance = 1.0f;
    [Tooltip("Camera distance from CameraTarget where fading starts.")]
    [SerializeField] private float fadeStartDistance = 2.0f;

    private sealed class RendererState
    {
        public Renderer renderer;
        public Material[] originals;
        public Material[] fading;
        public Color[] colors;
        public bool usingFade;
    }

    private readonly List<RendererState> states = new List<RendererState>();
    private Transform cameraTarget;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private void OnEnable()
    {
        cameraTarget = transform.Find("CameraTarget");
        RefreshRenderers();
    }

    /// <summary>Called by PlayerEquipmentLoader after changing outfits.</summary>
    public void RefreshRenderers()
    {
        ReleaseMaterials();
        var renderers = new List<Renderer>();
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            // Exclude particles, UI and objects carried by the player.
            if (!(renderer is SkinnedMeshRenderer) && !(renderer is MeshRenderer)) continue;
            if (renderer.GetComponentInParent<GrabbableObject>() != null) continue;
            renderers.Add(renderer);
            Material[] originals = renderer.sharedMaterials;
            states.Add(new RendererState
            {
                renderer = renderer,
                originals = originals,
                colors = new Color[originals.Length]
            });
        }
        playerRenderers = renderers.ToArray();
    }

    private void LateUpdate()
    {
        // Menu models must stay visible in their dedicated preview cameras.
        if (gameObject.scene.name == "Main Menu")
        {
            RestoreOpaqueMaterials();
            return;
        }
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
        if (cameraTransform == null)
        {
            RestoreOpaqueMaterials();
            return;
        }
        Vector3 reference = cameraTarget != null
            ? cameraTarget.position : transform.position + Vector3.up * 1.5f;
        float distance = Vector3.Distance(reference, cameraTransform.position);
        float start = Mathf.Max(fadeStartDistance, fadeEndDistance + 0.01f);
        ApplyFade(Mathf.InverseLerp(fadeEndDistance, start, distance));
    }

    private void ApplyFade(float alpha)
    {
        foreach (RendererState state in states)
        {
            if (state.renderer == null) continue;
            if (alpha >= 1f)
            {
                Restore(state);
                continue;
            }
            if (!state.renderer.gameObject.activeInHierarchy || !state.renderer.enabled) continue;
            if (state.fading == null) CreateFadeMaterials(state);
            if (!state.usingFade)
            {
                state.renderer.sharedMaterials = state.fading;
                state.usingFade = true;
            }
            for (int i = 0; i < state.fading.Length; i++)
            {
                Material material = state.fading[i];
                if (material == null || material == state.originals[i]) continue;
                Color color = state.colors[i];
                color.a *= alpha;
                material.SetColor(BaseColorId, color);
            }
        }
    }

    private static void CreateFadeMaterials(RendererState state)
    {
        state.fading = new Material[state.originals.Length];
        for (int i = 0; i < state.originals.Length; i++)
        {
            Material original = state.originals[i];
            // Both character prefabs use URP Lit; leave other shaders unchanged.
            if (original == null || original.shader.name != "Universal Render Pipeline/Lit")
            {
                state.fading[i] = original;
                continue;
            }
            Material material = new Material(original)
            {
                name = original.name + " (Camera Fade)",
                hideFlags = HideFlags.DontSave,
                renderQueue = (int)RenderQueue.Transparent
            };
            state.colors[i] = original.GetColor(BaseColorId);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.SetFloat("_BlendModePreserveSpecular", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_AlphaToMask", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_ALPHAMODULATE_ON");
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetShaderPassEnabled("ShadowCaster", false);
            material.SetShaderPassEnabled("DepthOnly", false);
            material.SetShaderPassEnabled("DepthNormals", false);
            state.fading[i] = material;
        }
    }

    private static void Restore(RendererState state)
    {
        if (state.usingFade && state.renderer != null)
            state.renderer.sharedMaterials = state.originals;
        state.usingFade = false;
    }

    private void RestoreOpaqueMaterials()
    {
        foreach (RendererState state in states) Restore(state);
    }

    private void ReleaseMaterials()
    {
        foreach (RendererState state in states)
        {
            Restore(state);
            if (state.fading == null) continue;
            for (int i = 0; i < state.fading.Length; i++)
            {
                Material material = state.fading[i];
                if (material == null || material == state.originals[i]) continue;
                if (Application.isPlaying) Destroy(material);
                else DestroyImmediate(material);
            }
        }
        states.Clear();
    }

    private void OnDisable() => ReleaseMaterials();
    private void OnDestroy() => ReleaseMaterials();
}
