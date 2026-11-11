using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>Unlit yellow and small visual halos; never adds lights, bloom, input or motion.</summary>
[DisallowMultipleComponent]
public sealed class GuidanceArrowGlow : MonoBehaviour
{
    public static readonly Color Yellow = new Color(1f, .86f, .08f, 1f);
    Renderer sourceRenderer;
    Renderer[] haloRenderers;
    SpriteRenderer sourceSprite;
    Image sourceImage;
    Image[] haloImages;
    TMP_Text text;
    Material originalTextMaterial, textMaterial;
    TMP_FontAsset styledFont;
    bool prepared;

    public static void Attach(GameObject visual)
    {
        if (visual != null && visual.GetComponent<GuidanceArrowGlow>() == null) visual.AddComponent<GuidanceArrowGlow>();
    }
    public static void AttachHierarchy(Transform arrow)
    {
        if (arrow == null) return;
        foreach (var label in arrow.GetComponentsInChildren<TMP_Text>(true))
            if (label.GetComponentInParent<GuidanceArrowGlow>() == null) Attach(label.gameObject);
        foreach (var renderer in arrow.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.GetComponent<TMP_Text>() != null || renderer.GetComponent<TMP_SubMesh>() != null) continue;
            if (renderer.GetComponentInParent<GuidanceArrowGlow>() != null) continue;
            bool yellow = renderer is SpriteRenderer;
            foreach (var material in renderer.sharedMaterials) yellow |= IsYellow(material);
            if (yellow) Attach(renderer.gameObject);
        }
        foreach (var graphic in arrow.GetComponentsInChildren<Image>(true))
            if (graphic.GetComponentInParent<GuidanceArrowGlow>() == null) Attach(graphic.gameObject);
    }
    static bool IsYellow(Material material)
    {
        if (material == null) return false;
        var color = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.HasProperty("_Color") ? material.color : Color.white;
        return color.r > .6f && color.g > .5f && color.b < .45f;
    }
    public static Material NewYellowMaterial(string name)
    {
        var template = Resources.Load<Material>("Guidance/YellowArrow");
        var material = template != null ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        material.name = name + " Glow"; material.hideFlags = HideFlags.DontSave;
        material.SetColor("_BaseColor", Yellow); material.SetFloat("_Cull", 0);
        return material;
    }
    void Start() { Prepare(); }
    void Prepare()
    {
        if (prepared) return;
        prepared = true;
        text = GetComponent<TMP_Text>();
        if (text != null) { StyleText(); return; }
        sourceImage = GetComponent<Image>();
        if (sourceImage != null) { ImageHalos(); return; }
        sourceRenderer = GetComponent<Renderer>();
        sourceSprite = sourceRenderer as SpriteRenderer;
        if (sourceSprite != null) { SpriteHalos(); return; }
        var filter = GetComponent<MeshFilter>();
        var fill = Resources.Load<Material>("Guidance/YellowArrow");
        if (sourceRenderer == null || filter == null || filter.sharedMesh == null || fill == null) return;
        sourceRenderer.sharedMaterial = fill;
        sourceRenderer.shadowCastingMode = ShadowCastingMode.Off; sourceRenderer.receiveShadows = false;
        haloRenderers = new Renderer[2];
        for (int i = 0; i < 2; i++)
        {
            var halo = new GameObject("Guidance Glow Halo " + i, typeof(MeshFilter), typeof(MeshRenderer));
            halo.layer = gameObject.layer;
            halo.transform.SetParent(transform, false); halo.transform.localScale = Vector3.one * (i == 0 ? 1.25f : 1.42f);
            halo.transform.localPosition = Vector3.forward * (.06f + .01f * i);
            halo.GetComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            var renderer = halo.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = Resources.Load<Material>("Guidance/ArrowHalo" + i);
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            haloRenderers[i] = renderer;
        }
    }
    void StyleText()
    {
        if (text.fontSharedMaterial == null) return;
        var source = text.fontSharedMaterial;
        if (source == textMaterial && text.font != styledFont) source = text.font.material;
        if (textMaterial != null) Destroy(textMaterial);
        originalTextMaterial = source; styledFont = text.font;
        textMaterial = new Material(originalTextMaterial) { name = "Yellow Guidance Text Glow", hideFlags = HideFlags.DontSave };
        // TMP's SDF underlay supplies a soft halo even without HDR or post-processing.
        if (textMaterial.HasProperty("_UnderlayColor"))
        {
            textMaterial.EnableKeyword("UNDERLAY_ON");
            textMaterial.SetColor("_UnderlayColor", new Color(1f, .7f, .02f, .45f));
            textMaterial.SetFloat("_UnderlayOffsetX", 0); textMaterial.SetFloat("_UnderlayOffsetY", 0);
            textMaterial.SetFloat("_UnderlayDilate", .12f); textMaterial.SetFloat("_UnderlaySoftness", .25f);
        }
        if (textMaterial.HasProperty("_OutlineWidth"))
        { textMaterial.SetFloat("_OutlineWidth", .07f); textMaterial.SetColor("_OutlineColor", new Color(.24f, .15f, .005f, 1)); }
        text.fontSharedMaterial = textMaterial; text.color = Yellow;
        text.raycastTarget = false;
        text.extraPadding = true; text.UpdateMeshPadding();
    }
    void SpriteHalos()
    {
        sourceSprite.color = Yellow; haloRenderers = new Renderer[2];
        for (int i = 0; i < 2; i++)
        {
            var halo = new GameObject("Guidance Glow Halo " + i, typeof(SpriteRenderer));
            halo.layer = gameObject.layer;
            halo.transform.SetParent(transform, false); halo.transform.localScale = Vector3.one * (i == 0 ? 1.18f : 1.32f);
            var renderer = halo.GetComponent<SpriteRenderer>(); renderer.sharedMaterial = sourceSprite.sharedMaterial;
            renderer.sortingLayerID = sourceSprite.sortingLayerID; renderer.sortingOrder = sourceSprite.sortingOrder - i - 1;
            renderer.color = new Color(Yellow.r, Yellow.g, Yellow.b, i == 0 ? .22f : .1f);
            renderer.sprite = sourceSprite.sprite; haloRenderers[i] = renderer;
        }
    }
    void ImageHalos()
    {
        sourceImage.color = Yellow; sourceImage.raycastTarget = false; haloImages = new Image[2];
        for (int i = 0; i < 2; i++)
        {
            var halo = new GameObject("Guidance Glow Halo " + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            halo.layer = gameObject.layer;
            var rect = (RectTransform)halo.transform; rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one * (i == 0 ? 1.18f : 1.32f); rect.SetAsFirstSibling();
            var image = halo.GetComponent<Image>(); image.sprite = sourceImage.sprite; image.preserveAspect = sourceImage.preserveAspect;
            image.color = new Color(Yellow.r, Yellow.g, Yellow.b, i == 0 ? .22f : .1f); image.raycastTarget = false;
            haloImages[i] = image;
        }
    }
    void LateUpdate()
    {
        if (!prepared) return;
        if (text != null && (text.font != styledFont || text.fontSharedMaterial != textMaterial)) StyleText();
        if (haloRenderers != null)
            foreach (var halo in haloRenderers)
            {
                if (halo == null) continue;
                halo.enabled = sourceRenderer != null && sourceRenderer.enabled;
                if (sourceSprite != null && halo is SpriteRenderer sprite)
                { sprite.sprite = sourceSprite.sprite; sprite.flipX = sourceSprite.flipX; sprite.flipY = sourceSprite.flipY; }
            }
        if (haloImages != null) foreach (var halo in haloImages) if (halo != null) halo.enabled = sourceImage != null && sourceImage.enabled;
    }
    void OnDestroy()
    {
        if (textMaterial != null)
        { if (text != null && text.fontSharedMaterial == textMaterial) text.fontSharedMaterial = originalTextMaterial; Destroy(textMaterial); }
    }
}
